using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Threading;

namespace Greenlight.EdgeLightClient;

/// <summary>
/// The strip: a line a few pixels deep along one edge of the primary monitor. Owns the frame
/// loop, the scene, and the hazard light that comes out of it when a pipeline breaks.
/// </summary>
/// <remarks>
/// Two windows rather than one band deep enough for both. The strip is one to five pixels; a
/// window deep enough to hold the light as well would be a hundred-pixel transparent band
/// along the edge of every screen, and while nothing could click it, it is a hundred pixels
/// of "why is there a window here" in every debugging tool on the machine. The light gets its
/// own square, shown only while it is out.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class StripWindow : FurnitureWindow
{
    private readonly EdgeConfig _config;
    private readonly StripCanvas _canvas;
    private readonly DispatcherTimer _frames;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private BeaconWindow? _beacon;
    private TimeSpan _lastFrame;
    private TimeSpan _lastHousekeeping;
    private PixelBox _screen;
    private EdgeFrame _drawn;

    public StripWindow(EdgeConfig config, EdgeScene scene) : base("Greenlight edge light")
    {
        _config = config;
        Scene = scene;

        _canvas = new StripCanvas(scene, config);
        Content = _canvas;

        // 60fps. Most frames draw nothing new — see OnFrame — but a pulse or a sweeping beacon
        // that stuttered would be worse than none.
        _frames = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _frames.Tick += OnFrame;
    }

    public EdgeScene Scene { get; }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LayOut();

        _lastFrame = _clock.Elapsed;
        _frames.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _frames.Stop();
        _beacon?.Close();
        _beacon = null;
        base.OnClosed(e);
    }

    /// <summary>
    /// Take up a setting changed from the tray while the strip is out. All of them can be
    /// absorbed where it stands: the canvases read the colours each frame, the scene reads the
    /// rest, and the geometry is simply worked out again.
    /// </summary>
    public void ApplyConfig()
    {
        Scene.ShowWhenOff = _config.ShowWhenOff;
        Scene.PulseAmount = _config.PulseAmount;
        Scene.BeaconEnabled = _config.Beacon.Enabled;
        LayOut();

        // The frame loop skips repaints when the scene has not changed, and a new colour is
        // not a change the scene knows about.
        _canvas.InvalidateVisual();
        _beacon?.Redraw();
    }

    /// <summary>Put the strip along its edge, and the light's square where it enters. Re-run when the screen changes.</summary>
    private void LayOut()
    {
        _screen = PrimaryScreen.Bounds();
        if (_screen.IsEmpty) return;

        Cover(EdgeLayout.Strip(_screen, _config.Edge, _config.Thickness));
        _beacon?.LayOut(_screen);
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = now - _lastFrame;
        _lastFrame = now;

        // Once a second: re-claim the top of the z-order, and notice if the screen has changed
        // size. Cheap, and much less code than listening for every way Windows has of
        // mentioning either.
        if (now - _lastHousekeeping >= TimeSpan.FromSeconds(1))
        {
            _lastHousekeeping = now;
            KeepOnTop();
            _beacon?.KeepOnTop();

            if (PrimaryScreen.Bounds() != _screen) LayOut();
        }

        Scene.Advance(elapsed);
        ShowOrHideBeacon();

        // Only redraw when something would look different. With no build running and the
        // pipeline not broken, that is never — and a window repainting sixty times a second to
        // draw the same two-pixel line is a laptop fan for nothing.
        var frame = Scene.Frame();
        if (frame == _drawn) return;

        _drawn = frame;
        _canvas.InvalidateVisual();
        _beacon?.Redraw();
    }

    /// <summary>The light's window exists only while the light is out, or on its way in or out.</summary>
    private void ShowOrHideBeacon()
    {
        if (Scene.IsBeaconOut && _beacon is null)
        {
            _beacon = new BeaconWindow(_config, Scene);
            _beacon.LayOut(_screen);
            _beacon.Show();
        }
        else if (!Scene.IsBeaconOut && _beacon is not null)
        {
            _beacon.Close();
            _beacon = null;
        }
    }
}
