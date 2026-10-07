using System.Runtime.InteropServices;
using EmojiSelector.Data;
using EmojiSelector.Input;

namespace EmojiSelector.UI;

internal sealed class MainForm : Form
{
    /// <summary>
    /// The command-line option giving the second title: <c>--title &lt;text&gt;</c> shows
    /// <c>Emoji Selector — &lt;text&gt;</c>, so instances running side by side tell each other apart.
    /// </summary>
    public const string TitleArgument = "--title";

    public const string AppTitle = "Emoji Selector";

    private readonly TrayIcon trayIcon;
    private readonly CategoryTabStrip tabStrip;
    private readonly EmojiGrid grid;
    private readonly ForegroundTracker foregroundTracker = new();

    // The state the window comes back in from the tray: its last one, never minimized.
    private FormWindowState restoreState = FormWindowState.Normal;

    /// <summary>The second title given with <see cref="TitleArgument"/>, null without one.</summary>
    public string? SecondTitle { get; }

    public MainForm(string? secondTitle)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        this.SecondTitle = secondTitle;
        this.Text = secondTitle is null ? AppTitle : $"{AppTitle} — {secondTitle}";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ClientSize = new Size(400, 450);
        this.MinimumSize = new Size(320, 240);

        // The tabs above, the grid filling the rest. Docking runs from the last control added: the strip first.
        IReadOnlyList<EmojiCategory> categories = EmojiCatalog.Load();
        this.grid = new EmojiGrid(categories) { Dock = DockStyle.Fill };
        this.tabStrip = new CategoryTabStrip(categories) { Dock = DockStyle.Top };
        this.Controls.Add(this.grid);
        this.Controls.Add(this.tabStrip);
        this.tabStrip.TabClicked += (_, category) => this.grid.ScrollToCategory(category);
        this.grid.ActiveCategoryChanged += (_, _) => this.tabStrip.ActiveTab = this.grid.ActiveCategory;
        this.grid.EmojiClicked += (_, emoji) => this.InsertEmoji(emoji);
        ResumeLayout(performLayout: false);

        this.trayIcon = new TrayIcon(this.Text);
        this.trayIcon.Clicked += this.OnTrayIconClicked;
        this.trayIcon.ExitRequested += (_, _) => Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // A user close (✕, Alt+F4) hides the window to the tray. Any other reason — Exit in the tray icon's menu,
        // Windows shutting down, the Task Manager — lets the app end.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
        }

        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // Minimized, the window goes to the tray rather than the taskbar.
        if (this.WindowState == FormWindowState.Minimized)
        {
            this.Hide();
        }
        else
        {
            this.restoreState = this.WindowState;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !this.IsDisposed)
        {
            this.trayIcon.Dispose();
            this.foregroundTracker.Dispose();
        }

        base.Dispose(disposing);
    }

    // The one place telling the tray icon an emoji was used.
    private void OnEmojiUsed(string emoji) => this.trayIcon.ShowEmoji(emoji);

    // A clicked emoji goes into the window that was in front before this one, then the window hides to the tray,
    // like Win+;. The previous window is brought back while this app is still in front: only the foreground app may
    // hand the foreground over. No previous window: the window hides, nothing is typed.
    private void InsertEmoji(Emoji emoji)
    {
        IntPtr target = this.foregroundTracker.PreviousWindow;
        if (target != IntPtr.Zero)
        {
            EmojiInserter.Activate(target);
        }

        this.Hide();
        if (target != IntPtr.Zero)
        {
            EmojiInserter.Type(emoji.Text);
        }

        this.OnEmojiUsed(emoji.Text);
    }

    // Hidden → shown. Shown but covered by another window → brought to the front. Shown in front → hidden.
    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        if (this.Visible && !this.IsCovered())
        {
            this.Hide();
            return;
        }

        this.Show();
        if (this.WindowState == FormWindowState.Minimized)
        {
            this.WindowState = this.restoreState;
        }

        this.Activate();
    }

    // Whether a window above this one in the z-order overlaps it. Ignored: hidden windows, cloaked ones (a
    // suspended Store app's window is "visible" but not drawn), and topmost ones — the taskbar is above every
    // window. Clicking the tray icon makes the taskbar active, so whether this window is active tells nothing.
    private bool IsCovered()
    {
        Rectangle bounds = GetFrameBounds(this.Handle);
        for (IntPtr window = GetWindow(this.Handle, GwHwndPrev); window != IntPtr.Zero; window = GetWindow(window, GwHwndPrev))
        {
            if (!IsWindowVisible(window) || IsCloaked(window) || (GetWindowLongW(window, GwlExStyle) & WsExTopmost) != 0)
            {
                continue;
            }

            if (GetFrameBounds(window).IntersectsWith(bounds))
            {
                return true;
            }
        }

        return false;
    }

    // The bounds Windows draws, without the invisible resize borders GetWindowRect counts.
    private static Rectangle GetFrameBounds(IntPtr window)
    {
        if (DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, out Rect rect, Marshal.SizeOf<Rect>()) != 0)
        {
            GetWindowRect(window, out rect);
        }

        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static bool IsCloaked(IntPtr window) =>
        DwmGetWindowAttribute(window, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private const uint GwHwndPrev = 3;
    private const int GwlExStyle = -20;
    private const int WsExTopmost = 0x8;
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetWindowLongW(IntPtr window, int index);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
