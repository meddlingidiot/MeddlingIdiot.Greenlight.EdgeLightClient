using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Greenlight.EdgeLightClient;

/// <summary>
/// What the strip and the hazard light have in common: a transparent, always-on-top,
/// click-through window with no chrome that never takes focus.
/// </summary>
[SupportedOSPlatform("windows")]
public abstract class FurnitureWindow : Window
{
    protected FurnitureWindow(string title)
    {
        Title = title;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Never take the caret out of somebody's editor. Paired with WS_EX_NOACTIVATE, which is
        // what makes a click landing here harmless in the first place.
        ShowActivated = false;

        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Both need a real window handle, so neither can run before the window is shown.
        ClickThroughNative.Apply(Handle);
        TinyWindowNative.Allow(Handle);
    }

    /// <summary>Put the window over a rectangle given in physical pixels.</summary>
    /// <remarks>
    /// Position is physical, Width and Height are logical. Mixing those up puts the strip at a
    /// plausible-looking but wrong size on every scaled display, which is most of them — and
    /// at two pixels tall, wrong is invisible.
    /// </remarks>
    protected void Cover(PixelBox box)
    {
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;
        Position = new PixelPoint(box.X, box.Y);
        Width = box.Width / scaling;
        Height = box.Height / scaling;

        // And once more in physical pixels, past Avalonia: see TinyWindowNative.Resize.
        TinyWindowNative.Resize(Handle, box.Width, box.Height);
    }

    /// <summary>Re-claim the top of the z-order. See <see cref="ClickThroughNative.KeepOnTop"/>.</summary>
    public void KeepOnTop() => ClickThroughNative.KeepOnTop(Handle);

    private IntPtr Handle => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
}
