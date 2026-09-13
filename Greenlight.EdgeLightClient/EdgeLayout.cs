using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Greenlight.EdgeLightClient;

/// <summary>A rectangle in physical screen pixels.</summary>
public readonly record struct PixelBox(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// Where the strip and the hazard light go, worked out from the screen, the edge and the
/// numbers in the file. Arithmetic only, so it can be tested without a monitor.
/// </summary>
public static class EdgeLayout
{
    /// <summary>The strip: the full length of <paramref name="edge"/>, <paramref name="thickness"/> pixels deep.</summary>
    public static PixelBox Strip(PixelBox screen, ScreenEdge edge, int thickness)
    {
        var t = Math.Clamp(thickness, 1, Math.Max(1, Math.Min(screen.Width, screen.Height)));

        return edge switch
        {
            ScreenEdge.Top => new PixelBox(screen.X, screen.Y, screen.Width, t),
            ScreenEdge.Bottom => new PixelBox(screen.X, screen.Y + screen.Height - t, screen.Width, t),
            ScreenEdge.Left => new PixelBox(screen.X, screen.Y, t, screen.Height),
            _ => new PixelBox(screen.X + screen.Width - t, screen.Y, t, screen.Height),
        };
    }

    /// <summary>
    /// The hazard light's square: just inside the strip, <paramref name="entry"/> of the way
    /// along the edge, and kept entirely on screen whatever the number.
    /// </summary>
    /// <remarks>
    /// Just inside the strip rather than over it, so the light comes out from under the strip
    /// — the strip is the edge as far as the eye is concerned, and the light should appear to
    /// emerge from it, not through it.
    /// </remarks>
    public static PixelBox Beacon(PixelBox screen, ScreenEdge edge, int thickness, int size, double entry)
    {
        var strip = Strip(screen, edge, thickness);
        var along = Math.Clamp(entry, 0, 1);

        return edge switch
        {
            ScreenEdge.Top => new PixelBox(Along(screen.X, screen.Width, size, along), strip.Y + strip.Height, size, size),
            ScreenEdge.Bottom => new PixelBox(Along(screen.X, screen.Width, size, along), strip.Y - size, size, size),
            ScreenEdge.Left => new PixelBox(strip.X + strip.Width, Along(screen.Y, screen.Height, size, along), size, size),
            _ => new PixelBox(strip.X - size, Along(screen.Y, screen.Height, size, along), size, size),
        };
    }

    private static int Along(int start, int length, int size, double entry) =>
        start + (int)Math.Round(Math.Max(0, length - size) * entry);
}

/// <summary>The primary monitor, in physical pixels.</summary>
/// <remarks>
/// The primary rather than the one the mouse is on or the virtual screen: a strip is a fixed
/// thing along a fixed edge, and the primary is the one edge everybody has.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class PrimaryScreen
{
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public static PixelBox Bounds()
    {
        var cx = GetSystemMetrics(SmCxScreen);
        var cy = GetSystemMetrics(SmCyScreen);
        return cx > 0 && cy > 0 ? new PixelBox(0, 0, cx, cy) : new PixelBox(0, 0, 1280, 720);
    }
}
