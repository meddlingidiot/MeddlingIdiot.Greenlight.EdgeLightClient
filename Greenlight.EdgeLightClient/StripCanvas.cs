using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Greenlight.EdgeLightClient;

/// <summary>Draws the strip: the whole window, one colour, at one alpha.</summary>
public sealed class StripCanvas : Control
{
    private readonly EdgeScene _scene;
    private readonly EdgeConfig _config;

    public StripCanvas(EdgeScene scene, EdgeConfig config)
    {
        _scene = scene;
        _config = config;

        // Belt and braces with the Win32 click-through: nothing in this window should ever be
        // a thing the mouse can land on.
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var frame = _scene.Frame();
        var colour = Palette.Parse(_config.ColourFor(frame.Shown));

        // The colour's own alpha, the config's opacity and the scene's fade all stack. A colour
        // written as "#804BFF86" is somebody asking for a quieter green, and they should get it.
        var alpha = frame.StripAlpha * _config.Opacity * (colour.A / 255.0);
        if (alpha < 0.003) return;

        context.FillRectangle(new SolidColorBrush(Palette.Opaque(colour), alpha), new Rect(Bounds.Size));
    }
}

/// <summary>Colour arithmetic the two canvases share.</summary>
internal static class Palette
{
    private static readonly Color Fallback = Color.FromRgb(0x6A, 0x70, 0x79);

    /// <summary>A hex from the file, or grey when it is not one.</summary>
    public static Color Parse(string hex) => Color.TryParse(hex, out var parsed) ? parsed : Fallback;

    public static Color Opaque(Color colour) => Color.FromRgb(colour.R, colour.G, colour.B);

    public static Color Mix(Color a, Color b, double t)
    {
        var k = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * k),
            (byte)(a.R + (b.R - a.R) * k),
            (byte)(a.G + (b.G - a.G) * k),
            (byte)(a.B + (b.B - a.B) * k));
    }

    /// <summary>Take a colour towards white, for the lit filament behind the glass.</summary>
    public static Color Lighten(Color colour, double towards) => Mix(colour, Colors.White, towards);

    /// <summary>Take a colour towards black, for the unlit side of the glass.</summary>
    public static Color Darken(Color colour, double towards) => Mix(colour, Colors.Black, towards);
}
