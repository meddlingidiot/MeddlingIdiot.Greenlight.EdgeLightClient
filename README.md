# MeddlingIdiot.Greenlight.EdgeLightClient

A strip a few pixels deep along one edge of your screen, the colour of your pipeline — and an
old rotating hazard light that comes out of the edge when a build breaks.

A runnable reference consumer of the [Greenlight](https://github.com/meddlingidiot/MeddlingIdiot.Greenlight)
SDK, and a demonstration of how little an app needs to do to use it. The strip is **green**
while everything passes and **amber** while a pull request wants you. While a build is running
it pulses — how deeply is up to you, from a barely-there breath to all the way out and back.
When a pipeline breaks it turns **red**, and a hazard light slides out from under the strip and
starts to turn: a red glass dome with a reflector sweeping round inside it, throwing a flash
across the desk each time it comes round to face you. It goes on turning until the pipeline is
fixed, then slides back in. With no Greenlight on the machine at all the strip goes a cold grey:
a green line on a four-minute-old snapshot would be lying to you.

The strip is one to five pixels — the point of it is that it is *there* rather than that it is
*seen*. It can run along any of the four edges of the primary monitor, and the light can come
in anywhere along that edge, at whatever size you like.

The point of it is what it does **not** have. No Azure DevOps client, no GitHub client, no
token, no polling loop — everything it knows arrives through the SDK, from the Greenlight
already running on the machine. Strip out the drawing and the tray icon and the integration is
about twenty lines, all of them in [`App.cs`](Greenlight.EdgeLightClient/App.cs).

## Running it

```bash
dotnet run --project Greenlight.EdgeLightClient
```

Windows only: the click-through windows and the screen size are Win32. The SDK itself is not —
it is plain .NET, and the same twenty lines work anywhere.

## The tray

The strip ignores the mouse by design — two pixels that swallowed clicks would be two pixels the
taskbar, the title bar and the scroll bar were all missing. So everything lives on the mascot
in the notification area:

- **Strip on the screen** — take it away and bring it back. Clicking the icon does the same.
- **Which edge** — top, bottom, left or right of the primary monitor.
- **How thick** — a hairline, two, three or five pixels. Physical pixels, because at this size
  the difference is the whole strip.
- **How solid** — how much the strip shows through.
- **How much it pulses** — how deep the pulse goes while a build is running: not at all,
  gently, plenty, or all the way out.
- **The hazard light** — whether it comes out at all, how big it is, and where along the edge
  it enters.
- **Grey strip when Greenlight is away** — a grey strip rather than none at all. On by
  default, because with it off "Greenlight has stopped" and "the strip has stopped" look
  identical.
- **Start with Windows** — read from the registry every time it is shown, so it agrees with
  Task Manager's Startup tab rather than with what we last wrote there.
- **Edit the colours…** — opens `edgelight.json`. **Reload the file** picks up hand edits
  without a restart.

Every setting is written straight back to the file, so the menu and the JSON are never two
different sets of settings.

## The file

`%AppData%\Greenlight.EdgeLight\edgelight.json`, written with the defaults on first run. One
colour per thing Greenlight can say, in any hex Avalonia can parse — a colour with its own
alpha (`#804BFF86`) is somebody asking for a quieter green, and they get it. The red is also
the colour of the hazard light's glass.

```json
{
  "WhenOff":   "#6A7079",
  "WhenGreen": "#4BFF86",
  "WhenAmber": "#FFCE42",
  "WhenRed":   "#FF4E3C",
  "Edge": "Top",
  "Thickness": 2,
  "Opacity": 1.0,
  "PulseAmount": 0.6,
  "Beacon": { "Enabled": true, "Size": 72, "Entry": 0.5 },
  "ShowWhenOff": true
}
```

`Thickness` is physical pixels. `Beacon.Size` is the side of the square the light lives in, in
logical pixels; `Beacon.Entry` is how far along the edge it comes in, 0 at the start and 1 at
the end, and its square is kept on screen whatever the number. A file that cannot be parsed
falls back to the defaults rather than refusing to start: it is a desk toy, and a stray comma
should not cost you the whole thing.

## How it is put together

| | |
|---|---|
| [`App.cs`](Greenlight.EdgeLightClient/App.cs) | The whole Greenlight integration |
| [`EdgeScene.cs`](Greenlight.EdgeLightClient/EdgeScene.cs) | Which colour is showing, how deep the pulse is, how far out the light is and where its reflector points. No Avalonia, so it is testable |
| [`EdgeLayout.cs`](Greenlight.EdgeLightClient/EdgeLayout.cs) | Where the strip and the light go for each edge. Also testable |
| [`StripWindow.cs`](Greenlight.EdgeLightClient/StripWindow.cs) | The strip, the frame loop, and the light's window coming and going |
| [`BeaconWindow.cs`](Greenlight.EdgeLightClient/BeaconWindow.cs) / [`BeaconCanvas.cs`](Greenlight.EdgeLightClient/BeaconCanvas.cs) | The hazard light: a square just inside the strip, and the drawing that slides into it |
| [`EdgeTray.cs`](Greenlight.EdgeLightClient/EdgeTray.cs) | The tray icon and its menu |
| [`EdgeConfig.cs`](Greenlight.EdgeLightClient/EdgeConfig.cs) | The file, and the clamping that keeps a hand edit from producing something you cannot find |
| [`FurnitureWindow.cs`](Greenlight.EdgeLightClient/FurnitureWindow.cs) | What the two windows share: transparent, topmost, click-through, never activated |
| [`TinyWindowNative.cs`](Greenlight.EdgeLightClient/TinyWindowNative.cs) | The one thing that lets a window be two pixels tall |

Two things are worth knowing. **Windows will not let an ordinary window be smaller than about
136×39 pixels** — the smallest thing a caption bar could be — whatever it says in
`WM_GETMINMAXINFO`, and Avalonia's windows are ordinary ones. So a two-pixel strip comes out
thirty-nine pixels tall, silently, and the fix is to make it a popup window after it exists
(`TinyWindowNative`). And **the light slides in without its window moving**: its window sits
still just inside the strip and the drawing is shifted towards the edge and clipped, so at
reveal zero the whole thing is drawn just outside the window and nothing shows.

The scene is free of Avalonia so the changeover, the pulse depth and the light's coming and
going can be tested without a window — and so the thing that would make the toy look broken, a
light that never quite goes back in after the pipeline is fixed, is a test rather than something
you would only notice the next morning.

```bash
dotnet test
```

## Building on it

```bash
dotnet add package MeddlingIdiot.Greenlight.Sdk
```

That is exactly what this repository does — the SDK comes from nuget.org like any other
dependency. Greenlight itself is a separate product and is not open source — this sample is.
The two things it wants you to notice: the client sits in a disabled state and reconnects on
its own when Greenlight is not running, so there is nothing to guard; and the events arrive on
a background thread, so anything touching your UI has to get itself back onto the UI thread.
Both are worked through in `App.cs`.
