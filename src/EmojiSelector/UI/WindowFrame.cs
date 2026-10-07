using System.Runtime.InteropServices;

namespace EmojiSelector.UI;

/// <summary>
/// The window's frame without its caption: the client area takes the caption's place, the left, right and bottom
/// resize borders, the shadow and the rounded corners stay Windows' own, the top resize border is answered by
/// hit-testing. The messages and hit-test codes <see cref="MainForm"/> and <see cref="CategoryTabStrip"/> share.
/// </summary>
internal static class WindowFrame
{
    public const int WmNcCalcSize = 0x0083;
    public const int WmNcHitTest = 0x0084;

    public const int HtTransparent = -1;
    public const int HtClient = 1;
    public const int HtCaption = 2;
    public const int HtTop = 12;
    public const int HtTopLeft = 13;
    public const int HtTopRight = 14;

    private const int SmCxSizeFrame = 32;
    private const int SmCxPaddedBorder = 92;

    /// <summary>The thickness of a side resize border at <paramref name="dpi"/>, its invisible part included.</summary>
    public static int ResizeBorder(int dpi) =>
        GetSystemMetricsForDpi(SmCxSizeFrame, (uint)dpi) + GetSystemMetricsForDpi(SmCxPaddedBorder, (uint)dpi);

    /// <summary>
    /// Whether the point of a <c>WM_NCHITTEST</c> sent to <paramref name="control"/> lies in its window's top resize
    /// band: a control at the window's top lets those points through (<see cref="HtTransparent"/>) to the window.
    /// </summary>
    public static bool IsInTopResizeBand(Control control, IntPtr lParam) =>
        control.TopLevelControl is Control window
        && window.PointToClient(HitTestPoint(lParam)).Y < ResizeBorder(control.DeviceDpi);

    /// <summary>The screen point of a <c>WM_NCHITTEST</c>, packed in its lParam as two signed 16-bit values.</summary>
    public static Point HitTestPoint(IntPtr lParam)
    {
        long value = lParam.ToInt64();
        return new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);
}
