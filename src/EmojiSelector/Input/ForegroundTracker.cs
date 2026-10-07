using System.Runtime.InteropServices;
using System.Text;

namespace EmojiSelector.Input;

/// <summary>
/// Remembers the <b>previous foreground window</b>: the last window of another app that was in front — the one a
/// clicked emoji is inserted into. The taskbar and the notification area are skipped: clicking the tray icon makes
/// the taskbar the foreground window just before the app's own. Must be created on a thread with a message loop.
/// </summary>
internal sealed class ForegroundTracker : IDisposable
{
    // The shell's windows a click on the tray icon goes through: never a target.
    private static readonly string[] ShellClasses =
    [
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
        "XamlExplorerHostIslandWindow",
    ];

    // Kept in a field: the hook calls it for as long as it is installed, the garbage collector must not collect it.
    private readonly WinEventProc callback;
    private readonly IntPtr hook;
    private IntPtr previousWindow;

    public ForegroundTracker()
    {
        this.callback = this.OnForegroundChanged;
        this.hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, this.callback, 0, 0,
            WinEventOutOfContext | WinEventSkipOwnProcess);
        this.Remember(GetForegroundWindow());
    }

    /// <summary>The window to insert into, <see cref="IntPtr.Zero"/> when none, or when it was closed since.</summary>
    public IntPtr PreviousWindow => this.previousWindow != IntPtr.Zero && IsWindow(this.previousWindow) ? this.previousWindow : IntPtr.Zero;

    public void Dispose()
    {
        if (this.hook != IntPtr.Zero)
        {
            UnhookWinEvent(this.hook);
        }
    }

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        this.Remember(window);
    }

    private void Remember(IntPtr window)
    {
        if (window == IntPtr.Zero || IsOwnProcess(window) || IsShellWindow(window))
        {
            return;
        }

        this.previousWindow = window;
    }

    private static bool IsOwnProcess(IntPtr window)
    {
        GetWindowThreadProcessId(window, out uint processId);
        return processId == Environment.ProcessId;
    }

    private static bool IsShellWindow(IntPtr window)
    {
        var className = new StringBuilder(256);
        return GetClassNameW(window, className, className.Capacity) > 0 && ShellClasses.Contains(className.ToString());
    }

    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback,
        uint processId, uint threadId, uint flags);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr window, StringBuilder className, int maxCount);
}
