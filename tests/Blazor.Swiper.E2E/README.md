# Real-browser suite

This project drives the existing BlazingStory app in `demo/`; there is no private test host. The
fixture starts and stops the demo itself on a dynamic port, launches the installed system Google
Chrome through Playwright's `chrome` channel, and navigates straight to each story canvas.

Run from the repository root:

```powershell
dotnet test tests/Blazor.Swiper.E2E/Blazor.Swiper.E2E.csproj -c Release
```

No Playwright browser download is needed. Google Chrome must be installed and discoverable by
Playwright's `chrome` channel.

The same suite also runs on a real Android phone or iPhone, with real touch and real rotation:

```powershell
pwsh .claude/skills/phone-e2e/run-on-phone.ps1 -Phone android
```

`.claude/skills/phone-e2e/SKILL.md` covers the setup, the variables the fixture reads, and which
results are expected on a phone. `DeviceScenarioTests` needs a phone and is skipped without one.

## What this suite is for

bUnit stubs `swiper-interop.js` out entirely, so the unit suite never runs a line of it. Everything
here is unreachable from there: the `<swiper-container>` custom element upgrading, the reveal
handshake that keeps unpositioned slides off the screen, loop's logical index, and above all a real
pointer drag - the only thing that can tell `OnUserSlideChange` apart from `OnSlideChange`.

Every scenario also asserts the console via `AssertNoJsErrors`. The slider can end up on the right
slide while the interop threw on the way there, and the index alone would still read correctly.

## Driving a swipe that Swiper honours

Two rules, both learned by watching the suite pass while testing nothing:

1. **The page must not report touch support.** Swiper binds touch listeners when the browser
   advertises touch and pointer/mouse listeners otherwise. Playwright's touchscreen can tap but not
   drag, so setting `HasTouch = true` on the page makes every swipe silently inert - the slider
   never moves and the failure looks like a broken assertion rather than a broken gesture.

2. **Step the pointer with a frame's delay between moves.** Swiper decides whether a gesture is a
   swipe from the movement between successive pointer events. One large jump gives it nothing to
   measure, and the first of those intermediate moves is what raises `sliderFirstMove` - which is
   the entire basis of the user-driven distinction.

Drag past Swiper's long-swipe ratio (half a slide) or the slider snaps back to where it started.

`WaitForStateAsync` prints the last observed state in its failure message. Read it before touching
readiness waits or retry loops - a swipe that moved the slider but reported
`userSlideChanges = 0` is a library bug, not a flaky harness.
