using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Greenlight.EdgeLightClient;

/// <summary>
/// Lets a window be one pixel tall. Windows will not otherwise allow it.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia creates its top-level windows as <em>overlapped</em> windows, and Windows holds
/// every overlapped window to <c>SM_CXMIN</c> × <c>SM_CYMIN</c> — about 136×39 physical
/// pixels, the smallest thing a caption bar could be — no matter what it answers to
/// <c>WM_GETMINMAXINFO</c>. So a two-pixel strip silently comes out thirty-nine pixels tall,
/// which at the top of the screen is a green bar across the top of every title bar.
/// </para>
/// <para>
/// <em>Popup</em> windows are exempt from that floor. Turning <c>WS_POPUP</c> on after the
/// window exists is enough; nothing else about it changes, and the styles that make it
/// furniture are extended styles and unaffected.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class TinyWindowNative
{
    private const int GwlStyle = -16;
    private const uint WsPopup = 0x80000000;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>Let <paramref name="hwnd"/> be as small as one pixel each way.</summary>
    public static void Allow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        // Widened through uint first: style bits are a bit pattern, not a number, and
        // WS_POPUP is the top bit — as a signed int it would sign-extend into the 64-bit
        // style word.
        var current = (long)GetWindowLongPtrW(hwnd, GwlStyle);
        var updated = current | WsPopup;
        if (updated == current) return;

        SetWindowLongPtrW(hwnd, GwlStyle, (IntPtr)updated);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    /// <summary>
    /// Resize the window directly, in physical pixels. For after <see cref="Allow"/>: the
    /// size Avalonia asked for before the style changed was refused, and Avalonia may believe
    /// it took.
    /// </summary>
    public static void Resize(IntPtr hwnd, int width, int height)
    {
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width, height, SwpNoMove | SwpNoZOrder | SwpNoActivate);
    }
}
