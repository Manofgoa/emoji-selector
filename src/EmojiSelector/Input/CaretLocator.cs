using System.Runtime.InteropServices;
using static EmojiSelector.Input.AccessibilityInterop;

namespace EmojiSelector.Input;

/// <summary>
/// Finds where the <b>text cursor</b> of the previous window is, in screen coordinates (physical pixels), for Win+;
/// to show the window under it. Tried in order, the first answering wins: the Win32 caret, the MSAA caret, the
/// UI Automation caret, the UI Automation focused element; when none answers, the mouse pointer.
/// </summary>
internal static class CaretLocator
{
    // The overall limit on the sources asking the other app's process, which may be slow or hung: past it, the
    // mouse pointer is used, so the window never waits on an unresponsive app.
    private static readonly TimeSpan TimeLimit = TimeSpan.FromMilliseconds(200);

    // Created once, on the first thread pool (MTA) thread asking for it: UI Automation is called from the MTA only.
    private static readonly Lazy<IUIAutomation?> Automation = new(CreateAutomation);

    /// <summary>
    /// The text cursor's rectangle in <paramref name="window"/>, else its focused field's, else the mouse pointer as
    /// an empty rectangle. Called on the UI thread; blocks it at most <see cref="TimeLimit"/>.
    /// </summary>
    public static Rectangle Locate(IntPtr window)
    {
        if (window != IntPtr.Zero)
        {
            uint thread = GetWindowThreadProcessId(window, out uint processId);
            var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
            bool hasInfo = GetGUIThreadInfo(thread, ref info);
            if (hasInfo && FromWin32Caret(info) is Rectangle caret && IsOnScreen(caret))
            {
                return caret;
            }

            IntPtr focus = hasInfo && info.Focus != IntPtr.Zero ? info.Focus : window;
            Task<Rectangle?> task = Task.Run(() => FromAccessibility(focus, processId));
            if (task.Wait(TimeLimit) && task.Result is Rectangle found && IsOnScreen(found))
            {
                return found;
            }
        }

        return new Rectangle(Cursor.Position, Size.Empty);
    }

    // Sources 2 to 4, on a thread pool thread: each one asks the other app's process.
    private static Rectangle? FromAccessibility(IntPtr focus, uint processId)
    {
        return Try(() => FromMsaaCaret(focus))
            ?? Try(() => FromUiaCaret(processId))
            ?? Try(() => FromUiaFocusedElement(processId));
    }

    // A source failing — no such interface, the other app gone, a COM error — just lets the next one answer. Every
    // exception: whatever another app's accessibility does, Win+; must still show the window.
    private static Rectangle? Try(Func<Rectangle?> source)
    {
        try
        {
            return source();
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 1. Classic Win32 editors: the caret Windows itself knows of, in its window's client coordinates.
    private static Rectangle? FromWin32Caret(GuiThreadInfo info)
    {
        if (info.Caret == IntPtr.Zero || info.CaretRect.Bottom <= info.CaretRect.Top)
        {
            return null;
        }

        Point topLeft = ToPhysicalScreen(info.Caret, new Point(info.CaretRect.Left, info.CaretRect.Top));
        Point bottomRight = ToPhysicalScreen(info.Caret, new Point(info.CaretRect.Right, info.CaretRect.Bottom));
        return Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }

    // The window's client coordinates are in its own DPI awareness: converted in that context, then to physical
    // pixels — this app is per-monitor aware, the other may be DPI-unaware and scaled by Windows.
    private static Point ToPhysicalScreen(IntPtr window, Point client)
    {
        IntPtr previous = SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(window));
        ClientToScreen(window, ref client);
        SetThreadDpiAwarenessContext(previous);
        LogicalToPhysicalPointForPerMonitorDPI(window, ref client);
        return client;
    }

    // 2. Apps exposing their caret to accessibility tools without a Win32 caret.
    private static Rectangle? FromMsaaCaret(IntPtr focus)
    {
        if (AccessibleObjectFromWindow(focus, ObjIdCaret, AccessibleIid, out IAccessible? caret) != 0 || caret is null)
        {
            return null;
        }

        if (caret.AccLocation(out int left, out int top, out int width, out int height, ChildIdSelf) != 0 || height <= 0)
        {
            return null;
        }

        return new Rectangle(left, top, width, height);
    }

    // 3. Modern apps: the caret range of the focused element's text. A caret range is empty: when it has no
    // rectangle, the character after it gives the caret's left edge and height.
    private static Rectangle? FromUiaCaret(uint processId)
    {
        if (FocusedElement(processId) is not IUIAutomationElement element
            || element.GetCurrentPatternAs(UiaTextPattern2Id, TextPattern2Iid) is not IUIAutomationTextPattern2 text
            || text.GetCaretRange(out _) is not IUIAutomationTextRange range)
        {
            return null;
        }

        if (FirstRectangle(range.GetBoundingRectangles()) is Rectangle caret)
        {
            return caret;
        }

        IUIAutomationTextRange character = range.Clone();
        character.ExpandToEnclosingUnit(TextUnitCharacter);
        return FirstRectangle(character.GetBoundingRectangles()) is Rectangle next
            ? new Rectangle(next.Left, next.Top, 0, next.Height)
            : null;
    }

    // 4. The field has the focus but exposes no caret: its own bounds.
    private static Rectangle? FromUiaFocusedElement(uint processId)
    {
        if (FocusedElement(processId) is not IUIAutomationElement element
            || element.GetCurrentPropertyValue(UiaBoundingRectanglePropertyId) is not double[] bounds
            || FirstRectangle(bounds) is not Rectangle rectangle
            || rectangle.Width <= 0)
        {
            return null;
        }

        return rectangle;
    }

    // The element having the keyboard focus, only when it belongs to the previous window's app: the focus may sit
    // in the taskbar, clicked just before Win+;.
    private static IUIAutomationElement? FocusedElement(uint processId)
    {
        IUIAutomationElement? element = Automation.Value?.GetFocusedElement();
        return element?.GetCurrentPropertyValue(UiaProcessIdPropertyId) is int owner && owner == processId ? element : null;
    }

    // UI Automation gives rectangles as left, top, width, height, four doubles each.
    private static Rectangle? FirstRectangle(double[]? values)
    {
        if (values is null || values.Length < 4 || values[3] <= 0)
        {
            return null;
        }

        return new Rectangle((int)values[0], (int)values[1], (int)values[2], (int)values[3]);
    }

    private static IUIAutomation? CreateAutomation()
    {
        Type? type = Type.GetTypeFromCLSID(UiaClsid);
        return type is null ? null : Activator.CreateInstance(type) as IUIAutomation;
    }

    // An empty rectangle, or one on no monitor, does not count: the next source is tried.
    private static bool IsOnScreen(Rectangle rectangle) =>
        rectangle.Height > 0 && Screen.AllScreens.Any(screen => screen.Bounds.Contains(rectangle.Location));

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr Active;
        public IntPtr Focus;
        public IntPtr Capture;
        public IntPtr MenuOwner;
        public IntPtr MoveSize;
        public IntPtr Caret;
        public Rect CaretRect;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool LogicalToPhysicalPointForPerMonitorDPI(IntPtr window, ref Point point);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
