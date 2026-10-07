namespace EmojiSelector.UI;

/// <summary>
/// Where the window goes when Win+; shows it: under the <b>text cursor</b>, or above it when there is no room below,
/// kept inside the monitor's working area. Pure computation, no Windows call: every rectangle is in screen
/// coordinates, physical pixels.
/// </summary>
internal static class WindowPlacement
{
    /// <summary>The gap between the text cursor and the window, at 96 DPI.</summary>
    public const int Gap = 4;

    /// <summary>
    /// The top-left corner of the window's visible frame, of size <paramref name="frame"/>, placed against
    /// <paramref name="anchor"/> — the text cursor, the focused field, or the mouse pointer as an empty rectangle.
    /// </summary>
    /// <param name="workingArea">The working area of the monitor holding the anchor: the taskbar excluded.</param>
    /// <param name="gap">The gap between the anchor and the window, already scaled to the monitor's DPI.</param>
    public static Point Place(Rectangle anchor, Size frame, Rectangle workingArea, int gap)
    {
        // Below the anchor; above it when the bottom would leave the working area; neither fitting, clamped.
        int top = anchor.Bottom + gap;
        if (top + frame.Height > workingArea.Bottom)
        {
            int above = anchor.Top - gap - frame.Height;
            top = above >= workingArea.Top ? above : workingArea.Bottom - frame.Height;
        }

        // Left edges aligned, shifted left as far as needed; a window wider than the area keeps its left edge in it.
        int left = Math.Min(anchor.Left, workingArea.Right - frame.Width);
        return new Point(Math.Max(left, workingArea.Left), Math.Max(top, workingArea.Top));
    }
}
