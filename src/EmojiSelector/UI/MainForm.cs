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

    public const string SearchPlaceholder = "Search emojis";

    // In logical pixels (96 DPI), scaled to the form's DPI.
    private const int LogicalSearchPadding = 8;

    private readonly TrayIcon trayIcon;
    private readonly IReadOnlyList<EmojiCategory> categories;
    private readonly TextBox searchBox;
    private readonly Button clearButton;
    private readonly CategoryTabStrip tabStrip;
    private readonly EmojiGrid grid;

    // The emojis matching the search box, most relevant first; null while it is blank.
    private IReadOnlyList<Emoji>? searchResults;
    private readonly ForegroundTracker foregroundTracker = new();
    private readonly ShortcutHook shortcutHook;

    // The state the window comes back in from the tray: its last one, never minimized.
    private FormWindowState restoreState = FormWindowState.Normal;

    // The invisible resize borders around the visible frame, read the last time the window was shown: a hidden
    // window has no frame to read them from.
    private Padding frameMargins;

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

        // The search box on top, the tabs below it, the grid filling the rest. Docking runs from the last control
        // added: the search bar first, then the strip.
        this.categories = EmojiCatalog.Load();
        this.grid = new EmojiGrid(this.categories) { Dock = DockStyle.Fill };
        this.tabStrip = new CategoryTabStrip(this.categories) { Dock = DockStyle.Top };
        this.searchBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        this.clearButton = new Button { Text = "✕", Visible = false, TabStop = false, FlatStyle = FlatStyle.Flat };
        this.clearButton.FlatAppearance.BorderSize = 0;
        this.Controls.Add(this.grid);
        this.Controls.Add(this.tabStrip);
        this.Controls.Add(this.CreateSearchBar());
        // The placeholder stays while the box has the focus — it always has it, and PlaceholderText hides on focus.
        this.searchBox.HandleCreated += (_, _) => SendMessageW(this.searchBox.Handle, EmSetCueBanner, 1, SearchPlaceholder);
        // The ✕ is a square as high as the box, with its margins: showing it never changes the bar's height.
        this.clearButton.Margin = this.searchBox.Margin;
        this.FitClearButton();
        this.searchBox.SizeChanged += (_, _) => this.FitClearButton();
        this.searchBox.TextChanged += (_, _) => this.OnSearchTextChanged();
        this.searchBox.KeyDown += this.OnSearchBoxKeyDown;
        this.clearButton.Click += (_, _) => this.ClearSearch();
        this.tabStrip.TabClicked += (_, category) => this.grid.ScrollToCategory(category);
        this.grid.ActiveCategoryChanged += (_, _) => this.tabStrip.ActiveTab = this.grid.ActiveCategory;
        this.grid.EmojiClicked += (_, emoji) => this.InsertEmoji(emoji);
        ResumeLayout(performLayout: false);

        this.trayIcon = new TrayIcon(this.Text);
        this.trayIcon.Clicked += this.OnTrayIconClicked;
        this.trayIcon.ExitRequested += (_, _) => Application.Exit();

        // Created after the controls: the hook posts Win+; through the UI thread's synchronization context.
        this.shortcutHook = new ShortcutHook();
        this.shortcutHook.Pressed += this.OnShortcutPressed;
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

    // Every show, whatever its path: the search starts over, the box ready for typing.
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (this.Visible)
        {
            this.ClearSearch();
        }
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
            this.shortcutHook.Dispose();
            this.trayIcon.Dispose();
            this.foregroundTracker.Dispose();
        }

        base.Dispose(disposing);
    }

    // The search box, and the ✕ next to it while it holds text.
    private TableLayoutPanel CreateSearchBar()
    {
        int padding = this.LogicalToDeviceUnits(LogicalSearchPadding);
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = SystemColors.Window,
            Padding = new Padding(padding, padding, padding, padding / 2),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.Controls.Add(this.searchBox, 0, 0);
        bar.Controls.Add(this.clearButton, 1, 0);
        return bar;
    }

    // Blank → the category view, tabs enabled. Otherwise → the results alone, tabs greyed.
    private void OnSearchTextChanged()
    {
        string text = this.searchBox.Text;
        this.clearButton.Visible = text.Length > 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            this.searchResults = null;
            this.grid.ShowCategories();
            this.tabStrip.Greyed = false;
        }
        else
        {
            this.searchResults = EmojiSearch.Find(this.categories, text);
            this.grid.ShowSearchResults(this.searchResults);
            this.tabStrip.Greyed = true;
        }
    }

    // Enter inserts the first result. Esc clears the box, or hides the window when the box is already empty.
    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            if (this.searchResults is [Emoji first, ..])
            {
                this.InsertEmoji(first);
            }
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            if (this.searchBox.TextLength > 0)
            {
                this.ClearSearch();
            }
            else
            {
                this.Hide();
            }
        }
    }

    private void FitClearButton() => this.clearButton.Size = new Size(this.searchBox.Height, this.searchBox.Height);

    private void ClearSearch()
    {
        this.searchBox.Clear();
        this.ActiveControl = this.searchBox;
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

    // Win+;. Shown in front → hidden, the previous window getting the foreground back so typing resumes there. Hidden,
    // or shown but covered → placed under the text cursor of the previous window and brought to the foreground. A
    // window last maximized comes back maximized, not placed.
    private void OnShortcutPressed(object? sender, EventArgs e)
    {
        IntPtr previous = this.foregroundTracker.PreviousWindow;
        if (this.Visible && !this.IsCovered())
        {
            if (previous != IntPtr.Zero)
            {
                EmojiInserter.Activate(previous);
            }

            this.Hide();
            return;
        }

        if (this.restoreState == FormWindowState.Maximized)
        {
            this.Show();
            this.WindowState = FormWindowState.Maximized;
        }
        else
        {
            // Placed before being shown, so it does not appear at its old place first; placed again once shown, when
            // its frame can be read and a move to a monitor of another DPI has resized it.
            Rectangle anchor = CaretLocator.Locate(previous);
            bool minimized = this.WindowState == FormWindowState.Minimized;
            if (!minimized)
            {
                this.PlaceAt(anchor);
            }

            this.Show();
            if (minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }

            this.PlaceAt(anchor);
        }

        this.TakeForeground();
    }

    // Moves the window so its visible frame sits against the anchor, as WindowPlacement computes it.
    private void PlaceAt(Rectangle anchor)
    {
        if (this.Visible)
        {
            Rectangle frame = GetFrameBounds(this.Handle);
            this.frameMargins = new Padding(frame.Left - this.Left, frame.Top - this.Top, this.Right - frame.Right,
                this.Bottom - frame.Bottom);
        }

        var frameSize = new Size(this.Width - this.frameMargins.Horizontal, this.Height - this.frameMargins.Vertical);
        Rectangle workingArea = Screen.FromPoint(anchor.Location).WorkingArea;
        int gap = WindowPlacement.Gap * this.DeviceDpi / 96;
        Point location = WindowPlacement.Place(anchor, frameSize, workingArea, gap);
        this.Location = new Point(location.X - this.frameMargins.Left, location.Y - this.frameMargins.Top);
    }

    // The shortcut hook's dummy key makes this app the last one to have sent input, which lets it take the
    // foreground. Should Windows still refuse, the input of the thread in front is attached to this one for the time
    // of the call.
    private void TakeForeground()
    {
        this.Activate();
        IntPtr foreground = GetForegroundWindow();
        if (foreground == this.Handle || foreground == IntPtr.Zero)
        {
            return;
        }

        uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
        uint ownThread = GetCurrentThreadId();
        AttachThreadInput(ownThread, foregroundThread, true);
        SetForegroundWindow(this.Handle);
        AttachThreadInput(ownThread, foregroundThread, false);
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
    private const int EmSetCueBanner = 0x1501;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr SendMessageW(IntPtr window, int message, nint wParam, string lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetWindowLongW(IntPtr window, int index);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, bool doAttach);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetCurrentThreadId();

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
