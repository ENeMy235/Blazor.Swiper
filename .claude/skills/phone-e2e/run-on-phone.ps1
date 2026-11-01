<#
.SYNOPSIS
Runs the e2e suite, or part of it, on a real Android phone or iPhone, and puts the phone back afterwards.

.DESCRIPTION
Machine-specific values (phone serial or UDID, the Mac's address) live in a JSON file outside the repo,
~/.blazor-swiper/phones.json by default. phones.example.json next to this script shows its shape.

.EXAMPLE
pwsh .claude/skills/phone-e2e/run-on-phone.ps1 -Phone android
pwsh .claude/skills/phone-e2e/run-on-phone.ps1 -Phone iphone -Filter "FullyQualifiedName~DeviceScenarioTests"
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('android', 'iphone')]
    [string] $Phone,

    # A dotnet test --filter expression. Everything runs when it is left out.
    [string] $Filter,

    [string] $Config = (Join-Path $HOME '.blazor-swiper/phones.json'),

    [string] $ResultsDirectory = (Join-Path ([IO.Path]::GetTempPath()) "blazor-swiper-phone-results/$Phone-$(Get-Date -Format 'yyyyMMdd-HHmmss')"),

    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$project = Join-Path $repositoryRoot 'tests/Blazor.Swiper.E2E/Blazor.Swiper.E2E.csproj'

if (-not (Test-Path $Config)) {
    throw "No phone config at $Config. Copy $(Join-Path $PSScriptRoot 'phones.example.json') there and fill in your phones."
}
$settings = Get-Content $Config -Raw | ConvertFrom-Json

function Get-FreePort {
    # A port this machine has not served the demo on lately: a phone's browser caches the interop module per
    # origin, so a reused port can hand a run the module of an earlier checkout.
    while ($true) {
        $port = Get-Random -Minimum 5300 -Maximum 6000
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Any, $port)
        try {
            $listener.Start()
            return $port
        }
        catch {
        }
        finally {
            $listener.Stop()
        }
    }
}

function Assert-PortFree([int] $port, [string] $what) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "Port $port, which $what needs, is already in use. Stop whatever holds it (Get-NetTCPConnection -LocalPort $port) and run again."
    }
}

function Wait-Until([scriptblock] $condition, [int] $seconds, [string] $what) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        if (& $condition) {
            return
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out after $seconds s waiting for $what."
}

function Invoke-E2E([hashtable] $environment) {
    if (-not $NoBuild) {
        dotnet build $project -c Release --nologo -v quiet
        if ($LASTEXITCODE -ne 0) {
            throw 'The e2e project did not build.'
        }
    }

    $arguments = @('test', $project, '-c', 'Release', '--no-build', '--results-directory', $ResultsDirectory, '--logger', "trx;LogFileName=$Phone.trx")
    if ($Filter) {
        $arguments += @('--filter', $Filter)
    }

    foreach ($name in $environment.Keys) {
        Set-Item "env:$name" $environment[$name]
    }
    try {
        Write-Host "Running on the $Phone (demo on port $($environment.E2E_PORT)); results in $ResultsDirectory"
        dotnet @arguments
        $script:testExitCode = $LASTEXITCODE
    }
    finally {
        foreach ($name in $environment.Keys) {
            Remove-Item "env:$name" -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-Android {
    $adb = $settings.android.adb
    $serial = $settings.android.serial
    function Adb {
        & $adb -s $serial @args
        if ($LASTEXITCODE -ne 0) {
            throw "adb $args failed."
        }
    }

    $state = (& $adb -s $serial get-state 2>&1) -join ''
    if ($state -ne 'device') {
        throw "The Android phone $serial is not connected over adb ($state)."
    }

    Assert-PortFree 9222 'the phone Chrome''s DevTools forward'
    $port = Get-FreePort
    $rotation = Adb shell settings get system accelerometer_rotation
    $userRotation = Adb shell settings get system user_rotation
    $stayOn = Adb shell settings get global stay_on_while_plugged_in
    try {
        # The run takes minutes: a screen that sleeps half way through takes every later touch with it.
        Adb shell svc power stayon usb
        Adb shell input keyevent KEYCODE_WAKEUP
        Start-Sleep -Seconds 1
        if ((Adb shell dumpsys window) -match 'isKeyguardShowing=true') {
            Write-Host 'Unlock the Android phone - waiting up to 2 minutes.'
            Wait-Until { -not ((Adb shell dumpsys window) -match 'isKeyguardShowing=true') } 120 'the Android phone to be unlocked'
        }

        Adb forward tcp:9222 localabstract:chrome_devtools_remote | Out-Null
        Adb reverse "tcp:$port" "tcp:$port" | Out-Null
        Adb shell am start -n com.android.chrome/com.google.android.apps.chrome.Main | Out-Null
        Wait-Until { try { Invoke-RestMethod http://127.0.0.1:9222/json/version -TimeoutSec 2 | Out-Null; $true } catch { $false } } 30 'Chrome''s DevTools on the phone'

        Invoke-E2E @{
            E2E_PORT = "$port"
            E2E_CDP_ENDPOINT = 'http://127.0.0.1:9222'
            E2E_ADB = $adb
            # DeviceInput runs adb without -s; this picks the phone when more than one is attached.
            ANDROID_SERIAL = $serial
        }
    }
    finally {
        & $adb -s $serial reverse --remove "tcp:$port" 2>$null
        & $adb -s $serial forward --remove tcp:9222 2>$null
        & $adb -s $serial shell "settings put system user_rotation $userRotation; settings put system accelerometer_rotation $rotation; settings put global stay_on_while_plugged_in $stayOn"
        $after = & $adb -s $serial shell 'settings get system accelerometer_rotation; settings get system user_rotation'
        Write-Host "Phone put back: auto-rotate $($after[0]), rotation $($after[1]) (was $rotation, $userRotation)"
    }
}

function Invoke-IPhone {
    $iphone = $settings.iphone
    $sshKey = $iphone.sshKey -replace '^~', $HOME
    $sshArguments = @('-i', $sshKey, '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10', $iphone.mac)
    $python = if ($iphone.python) { $iphone.python } else { Join-Path $HOME '.blazor-swiper/ios-venv/Scripts/python.exe' }
    if (-not (Test-Path $python)) {
        Write-Host "Installing pymobiledevice3 into $(Split-Path (Split-Path $python))"
        py -3 -m venv (Split-Path (Split-Path $python))
        & $python -m pip install --quiet 'pymobiledevice3==11.47.0'
    }

    Assert-PortFree 27115 'the tunnel to the Mac''s usbmuxd'
    Assert-PortFree 9223 'the Web Inspector bridge'
    $port = Get-FreePort
    $tunnel = $null
    $bridge = $null
    $wdaStartedHere = $false
    $session = $null
    $previousMuxAddress = $env:USBMUXD_SOCKET_ADDRESS
    try {
        # WebDriverAgent is the only real touch and rotation Apple leaves open on a physical iPhone. Its
        # runner is built once on the Mac; this only starts it, or finds the one already running.
        # PowerShell ends what it pipes to ssh with CRLF. The script ends on its own LF, so that CR lands on an
        # empty last line and never on a command.
        $startWda = (@"
log=~/wda/blazor-swiper-e2e.log
if pgrep -f 'test-without-building.*id=$($iphone.udid)' > /dev/null; then
  echo ALREADY_RUNNING
  for f in `$(ls -t ~/wda/*.log); do grep -m1 -o 'ServerURLHere->[^<]*' "`$f" && exit 0; done
  exit 1
fi
cd ~/wda
# Backgrounded as a list (cd && xcodebuild &), a subshell would hold ssh's output open until xcodebuild
# exits, so ssh would never return.
nohup xcodebuild test-without-building -xctestrun $($iphone.xctestrun) -destination 'id=$($iphone.udid)' > `$log 2>&1 < /dev/null &
for i in `$(seq 1 90); do
  sleep 1
  grep -m1 -o 'ServerURLHere->[^<]*' `$log && exit 0
  grep -m1 -E 'not trusted|Testing failed|error:' `$log && exit 1
done
tail -5 `$log
exit 1
"@ -replace "`r", '') + "`n"
        $wdaOutput = $startWda | ssh @sshArguments 'sh -s'
        $wdaUrl = ($wdaOutput | Select-String -Pattern 'ServerURLHere->(\S+)' | Select-Object -First 1).Matches.Groups[1].Value.TrimEnd('/')
        if (-not $wdaUrl) {
            throw "WebDriverAgent did not start on the Mac:`n$($wdaOutput -join "`n")"
        }
        $wdaStartedHere = -not ($wdaOutput -contains 'ALREADY_RUNNING')
        Invoke-RestMethod "$wdaUrl/status" -TimeoutSec 30 | Out-Null
        if ((Invoke-RestMethod "$wdaUrl/wda/locked" -TimeoutSec 30).value) {
            try { Invoke-RestMethod "$wdaUrl/wda/unlock" -Method Post -TimeoutSec 60 | Out-Null } catch { }
            if ((Invoke-RestMethod "$wdaUrl/wda/locked" -TimeoutSec 30).value) {
                Write-Host 'Unlock the iPhone - waiting up to 2 minutes.'
                Wait-Until { -not (Invoke-RestMethod "$wdaUrl/wda/locked" -TimeoutSec 30).value } 120 'the iPhone to be unlocked'
            }
        }

        $phoneAddress = ([Uri]$wdaUrl).Host
        $lanHost = (Find-NetRoute -RemoteIPAddress $phoneAddress | Where-Object IPAddress | Select-Object -First 1).IPAddress

        # The Mac's usbmuxd socket, carried to this machine, is what lets pymobiledevice3 reach Safari's Web
        # Inspector without installing anything on the Mac.
        $tunnel = Start-Process ssh -PassThru -WindowStyle Hidden -ArgumentList (@('-i', $sshKey, '-o', 'BatchMode=yes', '-o', 'ExitOnForwardFailure=yes', '-o', 'ServerAliveInterval=15', '-N', '-L', '127.0.0.1:27115:/var/run/usbmuxd', $iphone.mac))
        Wait-Until { Get-NetTCPConnection -LocalPort 27115 -State Listen -ErrorAction SilentlyContinue } 20 'the usbmuxd tunnel'

        $env:USBMUXD_SOCKET_ADDRESS = '127.0.0.1:27115'
        $bridgeLog = Join-Path $ResultsDirectory 'webinspector-bridge.log'
        New-Item -ItemType Directory -Force $ResultsDirectory | Out-Null
        $bridge = Start-Process $python -PassThru -WindowStyle Hidden -RedirectStandardOutput $bridgeLog -RedirectStandardError "$bridgeLog.err" `
            -ArgumentList @('-m', 'pymobiledevice3', 'webinspector', 'cdp', '--udid', $iphone.udid, '--host', '127.0.0.1', '--port', '9223')
        Wait-Until { try { Invoke-RestMethod http://127.0.0.1:9223/json/version -TimeoutSec 2 | Out-Null; $true } catch { $false } } 60 'the Web Inspector bridge'

        $created = Invoke-RestMethod "$wdaUrl/session" -Method Post -ContentType 'application/json' -TimeoutSec 90 `
            -Body '{"capabilities":{"alwaysMatch":{"bundleId":"com.apple.mobilesafari","shouldWaitForQuiescence":false}}}'
        $session = "$wdaUrl/session/$(if ($created.sessionId) { $created.sessionId } else { $created.value.sessionId })"

        Invoke-E2E @{
            E2E_PORT = "$port"
            E2E_HOST = $lanHost
            E2E_CDP_ENDPOINT = 'http://127.0.0.1:9223'
            E2E_WDA = $session
        }
    }
    finally {
        if ($session) {
            try { Invoke-RestMethod "$session/orientation" -Method Post -ContentType 'application/json' -Body '{"orientation":"PORTRAIT"}' -TimeoutSec 30 | Out-Null } catch { Write-Warning "Could not turn the iPhone back to portrait: $_" }
            try { Invoke-RestMethod $session -Method Delete -TimeoutSec 30 | Out-Null } catch { }
        }
        foreach ($process in @($bridge, $tunnel)) {
            if ($process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force
            }
        }
        $env:USBMUXD_SOCKET_ADDRESS = $previousMuxAddress
        # Left running, WebDriverAgent keeps the phone awake and drains it even on the Mac's USB port.
        if ($wdaStartedHere) {
            ssh @sshArguments "pkill -f 'test-without-building.*id=$($iphone.udid)'"
            Write-Host 'WebDriverAgent stopped on the Mac.'
        }
    }
}

$script:testExitCode = 1
New-Item -ItemType Directory -Force $ResultsDirectory | Out-Null
if ($Phone -eq 'android') {
    Invoke-Android
}
else {
    Invoke-IPhone
}
exit $script:testExitCode
