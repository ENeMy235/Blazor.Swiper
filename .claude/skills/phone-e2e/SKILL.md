---
name: phone-e2e
description: Runs this repo's Playwright e2e suite on a real Android phone or iPhone, with real touch and real rotation. Use when asked to test on a phone, device, iPhone, Android, Pixel or Safari, when a change touches touch input, rotation, resizing or cssMode scrolling, or when a result that is green in desktop Chrome has to hold on iOS WebKit.
---

# Running the e2e suite on a real phone

Desktop Chrome cannot show three things this library depends on: a real finger (Swiper takes touch and
pointer input down different paths), a real rotation, and iOS WebKit, which orders layout events
differently from Chromium. A resize fix once passed every desktop test and still failed on the iPhone,
because WebKit delivers the clamped scroll before the `ResizeObserver` callback.

## Run it

From the repository root, with the phone unlocked:

```powershell
pwsh .claude/skills/phone-e2e/run-on-phone.ps1 -Phone android
pwsh .claude/skills/phone-e2e/run-on-phone.ps1 -Phone iphone -Filter "FullyQualifiedName~DeviceScenarioTests"
```

The script sets the phone up, runs `dotnet test` against it and puts the phone back in a `finally`:
rotation, the stay-awake setting, the port forwards, and WebDriverAgent if it started it. `-Filter`
takes a `dotnet test --filter` expression, `-NoBuild` skips the build, and the `.trx` lands in
`%TEMP%\blazor-swiper-phone-results\`, or wherever `-ResultsDirectory` points. If the phone is locked,
the script waits two minutes for you to unlock it.

⚠️ Run one phone at a time. Both runs build the same project and start the demo from the same checkout.

## One-time setup

The machine-specific values (device serial or UDID, the Mac's address, the SSH key) live in
`~/.blazor-swiper/phones.json`, outside the repo. Copy `phones.example.json` from this folder there and
fill it in.

**Android:** turn on USB debugging, authorize this PC (`adb devices` shows the phone as `device`), and
install Chrome.

**iPhone:** Apple offers no real touch on a physical iPhone except through XCUITest, so this needs a Mac
that the iPhone is plugged into by USB.

- On the Mac: SSH login with a key from this PC, Xcode, and [WebDriverAgent](https://github.com/appium/WebDriverAgent)
  built once with `xcodebuild build-for-testing` and signed for your team. The build writes
  `WebDriverAgentRunner_iphoneos<iOS version>-arm64.xctestrun`; put its path in `xctestrun`.
- On the iPhone: Developer Mode, Settings > Apps > Safari > Advanced > Web Inspector, and trust the Mac.
- On this PC: Python 3 behind the `py` launcher. The first run installs pymobiledevice3 into
  `~/.blazor-swiper/ios-venv`.
- The iPhone loads the demo from this PC over Wi-Fi, so both have to be on the same network, and Windows
  Firewall has to let `dotnet` accept connections on it.

🛑 Never accept an iOS update on the iPhone. The runner is built for one iOS version, and an update means
rebuilding and re-signing it on the Mac.

## How the harness reaches the phone

`DemoFixture` and `DeviceInput` (in `SwipeHelper.cs`) read these variables, and the script sets them:

| Variable | What it is |
|---|---|
| `E2E_CDP_ENDPOINT` | The phone browser's DevTools endpoint. Playwright connects to it instead of launching Chrome |
| `E2E_PORT` | The demo's port. The script picks a new one each run, because a phone's browser caches `_content/...` per origin and would serve an earlier checkout's interop module |
| `E2E_HOST` | This PC's address on the LAN. The demo then listens on every interface, since an iPhone has no `adb reverse` |
| `E2E_ADB` | adb, for real touches (`input touchscreen`) and rotation (`user_rotation`) on Android. `ANDROID_SERIAL` picks the phone when more than one is attached |
| `E2E_WDA` | A WebDriverAgent session URL, for real touches and rotation on the iPhone. Setting it is also what marks the run as an iPhone run (`DeviceInput.IsIPhone`) |

- **Android:** `adb forward tcp:9222 localabstract:chrome_devtools_remote` exposes Chrome's DevTools, and
  `adb reverse` gives the phone the demo on `127.0.0.1`.
- **iPhone:** an `ssh -L` tunnel brings the Mac's usbmuxd socket to this PC on port 27115.
  `pymobiledevice3 webinspector cdp` turns Safari's Web Inspector into a DevTools endpoint on port 9223,
  and WebDriverAgent on the Mac provides the touches and the rotation.

## Reading the results

These results are expected and are not bugs in the wrapper or in Swiper:

- **Skipped on the iPhone, with the reason in the skip message:** both mouse-wheel tests, the arrow-key
  test, the breakpoints test (Safari's Web Inspector cannot resize the page) and the two quick taps on
  the story button (the iPhone's taps land too far apart to catch its 300 ms move).
- **`SwiperStorySweepTests` overflow failures on the iPhone:** the demo stories sit in fixed-width boxes
  (`width: 420px`, coverflow 520px, creative 360px plus padding), and Safari's page is 414px wide with a
  16px body margin. The cards effect stacks its cards outside the slider by design. The Pixel's page is
  about 485px wide, so only coverflow overflows there.

## Traps

- 🛑 **Safari's Web Inspector bridge fakes input.** A wheel becomes `window.scrollBy`. A click is
  dispatched at `document.elementFromPoint`, which stops at the shadow host, so Swiper's arrows and
  bullets never receive it. Keys reach only focused fields. Anything a finger does in a test goes
  through `SwipeHelper.TapAsync`, `SwipeLeftAsync` or `SwipeRightAsync`, which send real input on a phone.
- **WebDriverAgent starts a touch about 1.1 s after it is asked for one.** A scenario that races a
  touch against a running move needs a move long enough to still be running (`Drag_DuringAMove` uses 4 s).
- **After each navigation the bridge keeps Playwright's helper world on the old document:** `evaluate`
  sees the new page, but locators see nothing. `NavigateToStoryAsync` reconnects after every navigation,
  so a test must never navigate with `Page.GotoAsync` itself.
- **The bridge cannot open a tab,** so the fixture opens the demo through WebDriverAgent and tells its
  tab apart by a token in the URL.
- **On Android, a viewport resize sticks to Chrome for every later test.** The fixture clears it on each
  navigation.
- 🛑 **WebDriverAgent cannot play several taps in one request.** It plays the time between two touches of
  one action sequence as a few milliseconds, whatever pauses it holds, and iOS then clicks only the last
  tap or none. `DeviceInput.TapAsync` sends each tap in a request of its own, and they land about 600 ms
  apart. A test racing taps against a move needs a move longer than that, and should assert that each
  click came while the previous move was still running, as the tab-toggle test does.
- **Safari's floating toolbar covers the bottom of the page.** Put an element a test taps elsewhere.
- **The Mac's address comes from DHCP.** If SSH times out, look up its current address and update
  `phones.json`. Another iPhone may be paired to the same Mac, which is why the config names the phone by UDID.
- **If a tunnel outlives a crashed run, port 27115 stays in use.** Find it with
  `Get-CimInstance Win32_Process | ? CommandLine -match '27115:/var/run/usbmuxd'` and stop it.

## Writing a test that runs on a phone

- Drive gestures through `SwipeHelper`. It uses the mouse on desktop and the phone's own input stack
  on a phone.
- Start a phone-only test with `Assert.SkipUnless(DeviceInput.IsActive, ...)`. Start a test that needs
  input the iPhone cannot produce with `Assert.SkipWhen(DeviceInput.IsIPhone, "<why>")`.
- Rotate with `DeviceInput.RotateAsync`, and call `DeviceInput.RestoreRotationAsync` in a `finally`.
