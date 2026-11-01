using Microsoft.Playwright;

namespace Blazor.Swiper.E2E;

/// <summary>
/// Produces a real drag, which is the only way to reach the code paths a swipe takes.
/// </summary>
internal static class SwipeHelper
{
    /// <summary>
    /// Drags from the right of the slider to the left, far enough past Swiper's long-swipe ratio
    /// to commit to the next slide.
    /// </summary>
    /// <remarks>
    /// Stepped with a frame's delay between moves rather than jumped in one go: Swiper decides
    /// whether a gesture is a swipe from the movement between successive pointer events, and a
    /// single large jump gives it nothing to measure. The first of those moves is also what raises
    /// sliderFirstMove, which is the entire basis of the user-driven distinction.
    /// </remarks>
    public static Task SwipeLeftAsync(IPage page, ILocator slider) => SwipeAsync(page, slider, 0.85f, 0.15f);

    /// <summary>The same drag the other way.</summary>
    public static Task SwipeRightAsync(IPage page, ILocator slider) => SwipeAsync(page, slider, 0.15f, 0.85f);

    private static async Task SwipeAsync(IPage page, ILocator slider, float fromRatio, float toRatio)
    {
        if (DeviceInput.IsActive)
        {
            await SwipeOnDeviceAsync(page, slider, fromRatio, toRatio);
            return;
        }

        var box = await slider.BoundingBoxAsync() ?? throw new InvalidOperationException("The slider has no layout box.");
        var y = box.Y + (box.Height / 2);
        var startX = box.X + (box.Width * fromRatio);
        var endX = box.X + (box.Width * toRatio);

        await page.Mouse.MoveAsync(startX, y);
        await page.Mouse.DownAsync();

        const int steps = 15;
        for (var step = 1; step <= steps; step++)
        {
            await page.Mouse.MoveAsync(startX + ((endX - startX) * step / steps), y);
            await Task.Delay(16);
        }

        await page.Mouse.UpAsync();
    }

    /// <summary>
    /// Clicks an element - on a phone with a real tap.
    /// </summary>
    /// <remarks>
    /// The iPhone's Web Inspector bridge has no input of its own and fakes a click by dispatching one at
    /// <c>document.elementFromPoint</c>, which stops at a shadow host: a click on Swiper's arrows or bullets,
    /// inside &lt;swiper-container&gt;'s shadow DOM, reaches the host instead. A lone click is not what Swiper
    /// reads as a tap on a slide either - it listens for the touch that comes before one.
    /// </remarks>
    public static async Task TapAsync(IPage page, ILocator target)
    {
        if (!DeviceInput.IsActive)
        {
            await target.ClickAsync();
            return;
        }

        await target.ScrollIntoViewIfNeededAsync();
        var box = await target.BoundingBoxAsync() ?? throw new InvalidOperationException("The element has no layout box.");
        var screen = await DeviceInput.CalibrateAsync(page);
        await DeviceInput.TapAsync(0, (screen.X(box.X + (box.Width / 2)), screen.Y(box.Y + (box.Height / 2))));
    }

    private static async Task SwipeOnDeviceAsync(IPage page, ILocator slider, float fromRatio, float toRatio)
    {
        await slider.ScrollIntoViewIfNeededAsync();
        var box = await slider.BoundingBoxAsync() ?? throw new InvalidOperationException("The slider has no layout box.");
        var screen = await DeviceInput.CalibrateAsync(page);

        var y = screen.Y(box.Y + (box.Height / 2));
        await DeviceInput.SwipeAsync(screen.X(box.X + (box.Width * fromRatio)), y, screen.X(box.X + (box.Width * toRatio)), y, 300);
    }
}

/// <summary>
/// Real input on a phone, through its own input stack rather than CDP: adb on Android, WebDriverAgent's
/// XCUITest touches on an iPhone.
/// </summary>
internal static class DeviceInput
{
    public static string? Adb => Environment.GetEnvironmentVariable("E2E_ADB");

    /// <summary>A WebDriverAgent session on the iPhone, as its URL: <c>http://phone:8100/session/{id}</c>.</summary>
    public static string? Wda => Environment.GetEnvironmentVariable("E2E_WDA");

    public static bool IsActive => Adb is not null || Wda is not null;

    /// <summary>
    /// Whether the phone is the iPhone, whose page is reached through the Web Inspector bridge. Safari offers no
    /// input over it, so the bridge fakes a wheel as a page scroll and sends no arrow keys at all.
    /// </summary>
    public static bool IsIPhone => Wda is not null;

    private static readonly HttpClient _wdaClient = new() { Timeout = TimeSpan.FromSeconds(90) };

    /// <summary>Where the page sits on the phone's screen, in the current orientation.</summary>
    public sealed record Screen(double Ratio, double OffsetX, double OffsetY)
    {
        public int X(double clientX) => (int)Math.Round((clientX + OffsetX) * Ratio);

        public int Y(double clientY) => (int)Math.Round((clientY + OffsetY) * Ratio);
    }

    /// <remarks>
    /// The input speaks screen coordinates and the page client pixels, and the browser's toolbar can sit
    /// above the page or be scrolled away. So one touch on a full-screen overlay first tells where the page
    /// sits on the screen, and the overlay keeps it off the slider. adb's touch is cancelled, so it raises no
    /// click at all; WebDriverAgent's is a tap, whose click lands on the overlay.
    /// </remarks>
    public static async Task<Screen> CalibrateAsync(IPage page)
    {
        var metrics = await page.EvaluateAsync<double[]>(@"() => {
            const overlay = document.createElement('div');
            overlay.id = 'e2e-calibration';
            overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483647;touch-action:none';
            overlay.addEventListener('pointerdown', e => {
                window.e2eCalibration = [e.clientX, e.clientY];
                e.stopPropagation();
            });
            window.e2eCalibration = null;
            document.body.appendChild(overlay);
            return [window.devicePixelRatio, screen.width, screen.height, innerWidth, innerHeight];
        }");
        // adb works in physical pixels, WebDriverAgent in points, which are CSS pixels in Safari. Safari's
        // screen.width stays the portrait one when the phone turns.
        var ratio = Adb is not null ? metrics[0] : 1;
        var landscape = metrics[3] > metrics[4];
        var screenWidth = Adb is not null || !landscape ? metrics[1] : Math.Max(metrics[1], metrics[2]);
        var screenHeight = Adb is not null || !landscape ? metrics[2] : Math.Min(metrics[1], metrics[2]);
        var probeX = (int)(screenWidth * ratio / 2);
        var probeY = (int)(screenHeight * ratio / 2);
        if (Adb is not null)
        {
            await ShellAsync($"input touchscreen motionevent DOWN {probeX} {probeY}; input touchscreen motionevent CANCEL {probeX} {probeY}");
        }
        else
        {
            await TapAsync(0, (probeX, probeY));
        }

        await page.WaitForFunctionAsync("() => window.e2eCalibration !== null", null, new PageWaitForFunctionOptions { Timeout = 5_000 });
        var client = await page.EvaluateAsync<double[]>(@"() => {
            document.getElementById('e2e-calibration').remove();
            return window.e2eCalibration;
        }");

        return new Screen(ratio, (probeX / ratio) - client[0], (probeY / ratio) - client[1]);
    }

    public static Task SwipeAsync(int fromX, int fromY, int toX, int toY, int durationMilliseconds)
    {
        if (Adb is not null)
        {
            return ShellAsync($"input touchscreen swipe {fromX} {fromY} {toX} {toY} {durationMilliseconds}");
        }

        return WdaActionsAsync(
            new { type = "pointerMove", duration = 0, x = fromX, y = fromY },
            new { type = "pointerDown", button = 0 },
            new { type = "pointerMove", duration = durationMilliseconds, x = toX, y = toY },
            new { type = "pointerUp", button = 0 });
    }

    /// <summary>Taps each screen point in turn, <paramref name="gapMilliseconds"/> apart where the backend can time it.</summary>
    public static Task TapAsync(int gapMilliseconds, params (int X, int Y)[] points)
    {
        if (Adb is not null)
        {
            return ShellAsync(string.Join("; ", points.Select(p => $"input touchscreen tap {p.X} {p.Y}")));
        }

        return TapOneRequestEachAsync(gapMilliseconds, points);
    }

    /// <remarks>
    /// WebDriverAgent plays the time between two touches of one action sequence as a few milliseconds, whatever
    /// the pauses or moves in it, and iOS then clicks only the last tap or none. Measured on an iPhone 11: two
    /// requests sent 150 ms apart click twice, about 600 ms apart, since WebDriverAgent runs them one at a time.
    /// </remarks>
    private static async Task TapOneRequestEachAsync(int gapMilliseconds, (int X, int Y)[] points)
    {
        var taps = new List<Task>();
        foreach (var point in points)
        {
            if (taps.Count > 0)
            {
                // Requests sent together can reach WebDriverAgent in either order.
                await Task.Delay(Math.Max(gapMilliseconds, 100));
            }

            taps.Add(WdaActionsAsync(
                new { type = "pointerMove", duration = 0, x = point.X, y = point.Y },
                new { type = "pointerDown", button = 0 },
                new { type = "pause", duration = 30 },
                new { type = "pointerUp", button = 0 }));
        }

        await Task.WhenAll(taps);
    }

    public static Task OpenUrlAsync(string url) => WdaPostAsync("url", new { url });

    public static async Task RotateAsync(bool landscape)
    {
        if (Adb is not null)
        {
            await ShellAsync($"settings put system accelerometer_rotation 0; settings put system user_rotation {(landscape ? 1 : 0)}");
            return;
        }

        await WdaPostAsync("orientation", new { orientation = landscape ? "LANDSCAPE" : "PORTRAIT" });
    }

    public static async Task RestoreRotationAsync()
    {
        if (Adb is not null)
        {
            await ShellAsync("settings put system user_rotation 0; settings put system accelerometer_rotation 1");
            return;
        }

        await WdaPostAsync("orientation", new { orientation = "PORTRAIT" });
    }

    private static Task WdaActionsAsync(params object[] steps)
    {
        return WdaPostAsync("actions", new
        {
            actions = new[]
            {
                new { type = "pointer", id = "finger1", parameters = new { pointerType = "touch" }, actions = steps }
            }
        });
    }

    private static async Task WdaPostAsync(string path, object body)
    {
        using var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        using var response = await _wdaClient.PostAsync($"{Wda}/{path}", content);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode || text.Contains("\"error\""))
        {
            throw new InvalidOperationException($"WebDriverAgent {path} failed: {text}");
        }
    }

    public static async Task<string> ShellAsync(string command)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(Adb!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("shell");
        startInfo.ArgumentList.Add(command);
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output.Trim();
    }
}
