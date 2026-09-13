using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Greenlight.EdgeLightClient;

/// <summary>
/// Draws the hazard light: an old rotating beacon — a dark mount against the edge, a red glass
/// dome, and a reflector turning inside it that throws a flash across the desk each time it
/// comes round to face you.
/// </summary>
/// <remarks>
/// Drawn once, mounted on the top edge, and rotated into place for the other three. The
/// slide-in is a translation towards the edge that the window's own bounds clip, so at reveal
/// 0 the whole thing is drawn just outside the window and nothing shows.
/// </remarks>
public sealed class BeaconCanvas : Control
{
    /// <summary>How many rings the flash is built from. See the same constant in the other clients.</summary>
    private const int BloomRings = 16;

    private static readonly Color MountLight = Color.FromRgb(88, 93, 102);
    private static readonly Color MountMid = Color.FromRgb(48, 52, 60);
    private static readonly Color MountDark = Color.FromRgb(24, 26, 31);
    private static readonly IPen GlassEdge = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 240, 240)), 1);

    private readonly EdgeScene _scene;
    private readonly EdgeConfig _config;

    public BeaconCanvas(EdgeScene scene, EdgeConfig config)
    {
        _scene = scene;
        _config = config;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var frame = _scene.Frame();
        if (frame.BeaconReveal <= 0) return;

        var s = Math.Min(Bounds.Width, Bounds.Height);
        if (s <= 0) return;

        var red = Palette.Opaque(Palette.Parse(_config.WhenRed));

        // Into place for the edge it is on, then out towards the edge by however much of it is
        // still put away. The rotation is about the square's centre, so a square rotates onto
        // itself and the edge that was the top is now whichever edge the strip is on.
        var centre = s / 2;
        var turn = _config.Edge switch
        {
            ScreenEdge.Bottom => Math.PI,
            ScreenEdge.Left => -Math.PI / 2,
            ScreenEdge.Right => Math.PI / 2,
            _ => 0,
        };
        var place = Matrix.CreateTranslation(-centre, -centre) * Matrix.CreateRotation(turn) * Matrix.CreateTranslation(centre, centre);
        var slide = Matrix.CreateTranslation(0, -(1 - frame.BeaconReveal) * s);

        using (context.PushTransform(slide * place))
        using (context.PushOpacity(_config.Opacity))
        {
            Draw(context, s, red, frame.BeaconTurn);
        }
    }

    /// <summary>The light, mounted on the top edge of an <paramref name="s"/>-by-<paramref name="s"/> square.</summary>
    private static void Draw(DrawingContext context, double s, Color red, double turn)
    {
        // Where the reflector is pointing. 0.25 of a turn is straight at the viewer; the flash
        // is the moment it sweeps past, cubed so it is a flash and not a slow brightening.
        var angle = turn * 2 * Math.PI;
        var facing = Math.Sin(angle);
        var flash = Math.Pow(Math.Max(0, facing), 3);

        var cx = s / 2;
        var domeTop = s * 0.20;
        var domeHalf = s * 0.26;
        var domeStraight = s * 0.24;
        var domeCentreY = domeTop + domeStraight * 0.55;

        // ── the flash, thrown out past the glass ─────────────────────────────
        if (flash > 0.01)
        {
            var reach = s * 0.50;
            for (var i = BloomRings; i >= 1; i--)
            {
                var t = (double)i / BloomRings;
                var alpha = 0.10 * Math.Pow(1 - t, 1.6) * flash;
                if (alpha < 0.002) continue;

                var r = domeHalf + (reach - domeHalf) * t;
                context.DrawEllipse(new SolidColorBrush(red, alpha), null, new Point(cx, domeCentreY + domeStraight * 0.2), r * 1.1, r);
            }
        }

        // ── the mount ────────────────────────────────────────────────────────
        // A plate against the edge and a collar the glass sits in. Brushed metal, drawn as
        // three flat bands: this is a light on the edge of a screen, not a product render.
        var plate = new Rect(s * 0.18, 0, s * 0.64, s * 0.13);
        context.FillRectangle(new SolidColorBrush(MountMid), plate, 0);
        context.FillRectangle(new SolidColorBrush(MountLight), new Rect(plate.X, plate.Y, plate.Width, s * 0.025));
        context.FillRectangle(new SolidColorBrush(MountDark), new Rect(plate.X, plate.Bottom - s * 0.03, plate.Width, s * 0.03));

        var collar = new Rect(cx - domeHalf * 1.12, plate.Bottom, domeHalf * 2.24, s * 0.07);
        context.FillRectangle(new SolidColorBrush(MountDark), collar, 0);
        context.FillRectangle(new SolidColorBrush(MountMid), new Rect(collar.X, collar.Y, collar.Width, s * 0.02));

        // ── the glass ────────────────────────────────────────────────────────
        var dome = new StreamGeometry();
        using (var g = dome.Open())
        {
            g.BeginFigure(new Point(cx - domeHalf, domeTop), isFilled: true);
            g.LineTo(new Point(cx - domeHalf, domeTop + domeStraight));
            g.ArcTo(new Point(cx + domeHalf, domeTop + domeStraight), new Size(domeHalf, domeHalf), 0, false, SweepDirection.CounterClockwise);
            g.LineTo(new Point(cx + domeHalf, domeTop));
            g.EndFigure(isClosed: true);
        }

        // The glass lights up as the beam comes round: dark ruby with the reflector facing
        // away, the full red with it facing you.
        var glass = Palette.Mix(Palette.Darken(red, 0.45), red, 0.35 + 0.65 * Math.Max(0, facing));
        context.DrawGeometry(new SolidColorBrush(glass, 0.62 + 0.3 * flash), GlassEdge, dome);

        // ── the reflector, seen through the glass ────────────────────────────
        // It goes left-to-right across the dome as it turns, and is small and dim on the far
        // side of its circle — which is all the depth a side view of a beacon needs.
        var lampX = cx + Math.Cos(angle) * domeHalf * 0.62;
        var lampY = domeCentreY + domeStraight * 0.15;
        var near = 0.5 + 0.5 * facing;
        var lampR = s * 0.075 * (0.7 + 0.5 * near);

        context.DrawEllipse(new SolidColorBrush(Palette.Lighten(red, 0.35), 0.25 + 0.45 * near), null, new Point(lampX, lampY), lampR * 2.1, lampR * 1.7);
        context.DrawEllipse(new SolidColorBrush(Palette.Lighten(red, 0.55 + 0.4 * flash), 0.55 + 0.45 * near), null, new Point(lampX, lampY), lampR, lampR);
        context.DrawEllipse(new SolidColorBrush(Palette.Lighten(red, 0.9), 0.35 + 0.65 * flash), null, new Point(lampX, lampY), lampR * 0.45, lampR * 0.45);

        // ── a catch of light on the glass ────────────────────────────────────
        // Fixed, unlike everything else: it is the room reflected in the dome, not the lamp.
        context.DrawEllipse(new SolidColorBrush(Colors.White, 0.16), null, new Point(cx - domeHalf * 0.45, domeTop + domeStraight * 0.35), domeHalf * 0.18, domeStraight * 0.45);
    }
}
