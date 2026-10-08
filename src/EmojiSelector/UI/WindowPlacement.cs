namespace EmojiSelector.UI;

/// <summary>
/// Where the window goes: under the <b>text cursor</b> when Win+; shows it, or above it when there is no room below;
/// in the <b>corner next to the notification area</b> when the tray icon or a launch shows it — always kept inside the
/// monitor's working area. Pure computation, no Windows call: every rectangle is in screen coordinates, physical
/// pixels.
/// </summary>
internal static class WindowPlacement
{
    /// <summary>The gap between the text cursor and the window, at 96 DPI.</summary>
    public const int Gap = 4;

    /// <summary>The gap between the window and the two edges of its corner, at 96 DPI — as Windows' own flyouts.</summary>
    public const int CornerMargin = 12;

    /// <summary>The edge of a monitor its taskbar is docked to, as its working area shows it.</summary>
    public enum Edge
    {
        /// <summary>No edge: the taskbar is auto-hidden, or not on this monitor.</summary>
        None,
        Left,
        Top,
        Right,
        Bottom,
    }

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

    /// <summary>
    /// The top-left corner of the window's visible frame, of size <paramref name="frame"/>, in the corner of the
    /// working area next to the notification area — at the end of the taskbar: bottom right with a taskbar at the
    /// bottom, on the right or not seen, top right with one at the top, bottom left with one on the left.
    /// </summary>
    /// <param name="bounds">The monitor's bounds.</param>
    /// <param name="workingArea">The monitor's working area: the taskbar excluded.</param>
    /// <param name="margin">The gap between the window and the corner's two edges, already scaled to the DPI.</param>
    public static Point PlaceInCorner(Size frame, Rectangle bounds, Rectangle workingArea, int margin)
    {
        Edge taskbar = TaskbarEdge(bounds, workingArea);
        int left = taskbar == Edge.Left ? workingArea.Left + margin : workingArea.Right - margin - frame.Width;
        int top = taskbar == Edge.Top ? workingArea.Top + margin : workingArea.Bottom - margin - frame.Height;

        // A window larger than the area keeps its top-left corner in it.
        return new Point(Math.Max(left, workingArea.Left), Math.Max(top, workingArea.Top));
    }

    /// <summary>
    /// The edge the taskbar is docked to: the side where the working area is shorter than the monitor's bounds — the
    /// widest gap when several are (another app bar docked); <see cref="Edge.None"/> when the two are the same.
    /// </summary>
    public static Edge TaskbarEdge(Rectangle bounds, Rectangle workingArea)
    {
        // The bottom first: on equal gaps, the default taskbar's edge wins.
        (Edge Side, int Gap)[] gaps =
        [
            (Edge.Bottom, bounds.Bottom - workingArea.Bottom),
            (Edge.Right, bounds.Right - workingArea.Right),
            (Edge.Top, workingArea.Top - bounds.Top),
            (Edge.Left, workingArea.Left - bounds.Left),
        ];

        Edge edge = Edge.None;
        int widest = 0;
        foreach ((Edge side, int gap) in gaps)
        {
            if (gap > widest)
            {
                edge = side;
                widest = gap;
            }
        }

        return edge;
    }

    /// <summary>
    /// The top-left corner of <paramref name="frame"/> — the window's visible frame — moved as little as needed for
    /// the frame to fit <paramref name="workingArea"/>; a frame larger than the area keeps its top-left corner in it.
    /// </summary>
    public static Point KeepInside(Rectangle frame, Rectangle workingArea)
    {
        int left = Math.Min(frame.Left, workingArea.Right - frame.Width);
        int top = Math.Min(frame.Top, workingArea.Bottom - frame.Height);
        return new Point(Math.Max(left, workingArea.Left), Math.Max(top, workingArea.Top));
    }
}
