using System.Diagnostics;
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

    // A side resize border at 96 DPI, its invisible part included: SM_CXSIZEFRAME + SM_CXPADDEDBORDER.
    private const int LogicalSideBorder = 8;

    public const string OpenAppFolderText = "Open app folder";

    private readonly TrayIcon trayIcon;
    private readonly ContextMenuStrip settingsMenu;
    private readonly IReadOnlyList<EmojiCategory> categories;
    private readonly TextBox searchBox;
    private readonly Button clearButton;
    private readonly CategoryTabStrip tabStrip;
    private readonly EmojiGrid grid;
    private readonly ForegroundTracker foregroundTracker = new();
    private readonly ShortcutHook shortcutHook;

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
        this.ClientSize = new Size(440, 450);

        // No caption (see WndProc): nothing to minimize or maximize from, and Windows refuses Win+Up, Win+Down and
        // the double-click on the drag area.
        this.MinimizeBox = false;
        this.MaximizeBox = false;

        // The search box on top, the tabs below it, the grid filling the rest. Docking runs from the last control
        // added: the search bar first, then the strip.
        this.categories = EmojiCatalog.Load();
        this.grid = new EmojiGrid(this.categories) { Dock = DockStyle.Fill };
        this.tabStrip = new CategoryTabStrip(this.categories) { Dock = DockStyle.Top };

        // Never narrower than the tab strip needs, its side resize borders added.
        this.MinimumSize = new Size(this.tabStrip.LogicalMinimumWidth + 2 * LogicalSideBorder, 240);
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
        this.grid.KeyPress += this.OnGridKeyPress;
        this.clearButton.Click += (_, _) => this.ClearSearch();
        this.tabStrip.TabClicked += (_, category) => this.grid.SelectCategory(category);
        this.tabStrip.CloseClicked += (_, _) => this.Close();
        this.settingsMenu = this.CreateSettingsMenu();
        this.tabStrip.SettingsClicked += (_, bounds) =>
            this.settingsMenu.Show(this.tabStrip, new Point(bounds.Right, bounds.Bottom), ToolStripDropDownDirection.BelowLeft);
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
        // A user close (the close cross, Alt+F4) hides the window to the tray. Any other reason — Exit in the tray icon's menu,
        // Windows shutting down, the Task Manager — lets the app end.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
        }

        base.OnFormClosing(e);
    }

    // Every show, whatever its path: the search starts over, the box ready for typing, the grid back at the top on its
    // first emoji — the one Enter inserts.
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (this.Visible)
        {
            this.ClearSearch();
            this.grid.ResetToTop();
        }
    }

    // The keyboard has two places: the search box, and the grid once ↓ hands it over. The grid only takes the focus
    // that way — it is not selectable, a click never focuses it.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (this.grid.Focused)
        {
            if (this.grid.MoveSelection(keyData))
            {
                return true;
            }

            // ↑ on the grid's first row: back to the box.
            if (keyData == Keys.Up)
            {
                this.FocusSearchBox();
                return true;
            }
        }
        else if (this.searchBox.Focused)
        {
            switch (keyData)
            {
                // No result: nothing to select, the keyboard stays in the box.
                case Keys.Down:
                    this.grid.ResetToTop();
                    if (this.grid.SelectedEmoji is not null)
                    {
                        this.grid.Focus();
                    }

                    return true;

                // Only ↓ leaves the box.
                case Keys.PageUp or Keys.PageDown or Keys.Tab or (Keys.Shift | Keys.Tab):
                    return true;
            }
        }

        if (this.grid.Focused || this.searchBox.Focused)
        {
            // Enter inserts the selection: the first result while searching, unless the arrows moved it.
            if (keyData == Keys.Enter)
            {
                if (this.grid.SelectedEmoji is Emoji emoji)
                {
                    this.InsertEmoji(emoji);
                }

                return true;
            }

            // Esc clears the box, or hides the window when the box is already empty.
            if (keyData == Keys.Escape)
            {
                if (this.searchBox.TextLength > 0)
                {
                    this.ClearSearch();
                }
                else
                {
                    this.Hide();
                }

                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // The window frame is computed again, now that WndProc answers WM_NCCALCSIZE.
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            // The caption becomes client area: the default computation, its top put back to the window's top. The
            // left, right and bottom resize borders, the shadow and the rounded corners stay Windows' own.
            case WindowFrame.WmNcCalcSize when m.WParam != IntPtr.Zero:
                int top = Marshal.ReadInt32(m.LParam, sizeof(int));
                base.WndProc(ref m);
                Marshal.WriteInt32(m.LParam, sizeof(int), top);
                return;

            case WindowFrame.WmNcHitTest:
                base.WndProc(ref m);
                if ((int)m.Result == WindowFrame.HtClient)
                {
                    m.Result = this.HitTestClient(WindowFrame.HitTestPoint(m.LParam));
                }

                return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !this.IsDisposed)
        {
            this.shortcutHook.Dispose();
            this.settingsMenu.Dispose();
            this.trayIcon.Dispose();
            this.foregroundTracker.Dispose();
        }

        base.Dispose(disposing);
    }

    // The caption took the top resize border with it: the top band of the client area answers for it, as thick as
    // the side borders. The tab strip's drag area answers as the caption: Windows moves the window, snaps it to the
    // sides of the screen and opens its system menu on a right click.
    private int HitTestClient(Point screenPoint)
    {
        Point point = this.PointToClient(screenPoint);
        int border = WindowFrame.ResizeBorder(this.DeviceDpi);
        if (point.Y >= border)
        {
            return this.tabStrip.IsDragArea(this.tabStrip.PointToClient(screenPoint)) ? WindowFrame.HtCaption
                : WindowFrame.HtClient;
        }

        return point.X < border ? WindowFrame.HtTopLeft
            : point.X >= this.ClientSize.Width - border ? WindowFrame.HtTopRight
            : WindowFrame.HtTop;
    }

    // The menu of the tab strip's settings button, shown under it, its right edge on the button's.
    private ContextMenuStrip CreateSettingsMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(OpenAppFolderText, image: null, (_, _) => OpenAppFolder());
        menu.Opened += (_, _) => this.tabStrip.SettingsMenuOpen = true;
        menu.Closed += (_, _) => this.tabStrip.SettingsMenuOpen = false;
        return menu;
    }

    // The folder holding the exe, in the File Explorer, the exe selected. The window stays as it is: the File Explorer
    // comes in front of it.
    private static void OpenAppFolder()
    {
        if (Environment.ProcessPath is string exe)
        {
            using Process? explorer = Process.Start("explorer.exe", $"/select,\"{exe}\"");
        }
    }

    // The search box, and the ✕ next to it while it holds text.
    private TableLayoutPanel CreateSearchBar()
    {
        int padding = this.LogicalToDeviceUnits(LogicalSearchPadding);
        var bar = new SearchBar
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
            this.grid.ShowCategories();
            this.tabStrip.Greyed = false;
        }
        else
        {
            this.grid.ShowSearchResults(EmojiSearch.Find(this.categories, text));
            this.tabStrip.Greyed = true;
        }
    }

    // A character typed while the grid has the keyboard goes back to the box, typed at the end of its text. Backspace
    // too: it edits the search.
    private void OnGridKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar) && e.KeyChar != '\b')
        {
            return;
        }

        e.Handled = true;
        this.FocusSearchBox();
        this.searchBox.Select(this.searchBox.TextLength, 0);
        SendMessageW(this.searchBox.Handle, WmChar, e.KeyChar, 0);
    }

    // The grid may hold the focus without being selectable: the box is focused directly, not only made active.
    private void FocusSearchBox()
    {
        this.ActiveControl = this.searchBox;
        this.searchBox.Focus();
    }

    private void FitClearButton() => this.clearButton.Size = new Size(this.searchBox.Height, this.searchBox.Height);

    private void ClearSearch()
    {
        this.searchBox.Clear();
        this.FocusSearchBox();
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
        this.Activate();
    }

    // Win+;. Shown in front → hidden, the previous window getting the foreground back so typing resumes there. Hidden,
    // or shown but covered → placed under the text cursor of the previous window and brought to the foreground.
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

        // Placed before being shown, so it does not appear at its old place first; placed again once shown, when its
        // frame can be read and a move to a monitor of another DPI has resized it.
        Rectangle anchor = CaretLocator.Locate(previous);
        this.PlaceAt(anchor);
        this.Show();
        this.PlaceAt(anchor);
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
    private const int WmChar = 0x0102;
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoMove = 0x2;
    private const uint SwpNoZOrder = 0x4;
    private const uint SwpNoActivate = 0x10;
    private const uint SwpFrameChanged = 0x20;

    // The search bar sits at the window's top: its top band lets the hit test through to the window, which answers it
    // as the top resize border.
    private sealed class SearchBar : TableLayoutPanel
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WindowFrame.WmNcHitTest && WindowFrame.IsInTopResizeBand(this, m.LParam))
            {
                m.Result = WindowFrame.HtTransparent;
                return;
            }

            base.WndProc(ref m);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr SendMessageW(IntPtr window, int message, nint wParam, string lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr SendMessageW(IntPtr window, int message, nint wParam, nint lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

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
