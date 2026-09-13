using System.Runtime.Versioning;

namespace Greenlight.EdgeLightClient;

/// <summary>
/// The hazard light's square, just inside the strip at the point along the edge it enters.
/// Exists only while the light is out.
/// </summary>
/// <remarks>
/// The window never moves. The light slides in by being drawn shifted towards the edge and
/// clipped by the window's own bounds — so the entrance is the canvas's, not the window's, and
/// a window that was half off the top of the screen is not a thing Windows ever has to think
/// about.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class BeaconWindow : FurnitureWindow
{
    private readonly EdgeConfig _config;
    private readonly BeaconCanvas _canvas;

    public BeaconWindow(EdgeConfig config, EdgeScene scene) : base("Greenlight hazard light")
    {
        _config = config;
        _canvas = new BeaconCanvas(scene, config);
        Content = _canvas;
    }

    /// <summary>Put the square where the light enters. The size in the file is logical; the layout wants physical.</summary>
    public void LayOut(PixelBox screen)
    {
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;
        var size = (int)Math.Round(_config.Beacon.Size * scaling);

        Cover(EdgeLayout.Beacon(screen, _config.Edge, _config.Thickness, size, _config.Beacon.Entry));
    }

    public void Redraw() => _canvas.InvalidateVisual();
}
