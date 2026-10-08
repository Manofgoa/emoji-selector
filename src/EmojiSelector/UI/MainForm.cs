using System.Diagnostics;
using System.Globalization;
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

    // The default size, in emojis: this many columns wide, and high enough for a section's header then this many full
    // rows when that section is scrolled to the top.
    private const int DefaultColumns = 16;
    private const int DefaultRows = 8;

    public const string OpenAppFolderText = "Open app folder";

    public const string ResetWindowSizeText = "Reset window size";

    public const string ShowFrequentText = "Show frequently used";

    public const string HideFrequentText = "Hide frequently used";

    public const string ClearFrequentText = "Clear frequently used";

    public const string ClearFrequentQuestion =
        "Clear the frequently used emojis? Their counts are deleted and cannot be brought back.";

    public const string FrequentHeader = "Frequently used";

    public const string NoFrequentText = "No emoji used yet";

    public const string RemoveFrequentText = "Remove from frequently used";

    // The frequent tab's glyph: FavoriteStar, in Segoe Fluent Icons and Segoe MDL2 Assets.
    private const char FrequentIcon = '';

    // The frequent section shows as many emojis as this many rows of the grid hold.
    private const int FrequentRows = 3;

    // A use count beyond this one shows as "999+": it never overflows its cell.
    private const int MaxShownCount = 999;

    public const string CustomHeader = "Custom";

    public const string NoGroupText = "Create a group from ⚙ → New group…";

    public const string EmptyGroupText = "Right-click an emoji to add it here";

    public const string NewGroupText = "New group…";

    public const string NewGroupTitle = "New group";

    public const string RenameGroupText = "Rename…";

    public const string RenameGroupTitle = "Rename group";

    public const string ReorderText = "Reorder";

    public const string MoveUpText = "Move up";

    public const string MoveDownText = "Move down";

    public const string HideGroupText = "Hide group";

    public const string ShowGroupsText = "Show groups";

    public const string DeleteGroupText = "Delete group";

    public const string DeleteGroupQuestion = "Delete the group \"{0}\"? Its list of emojis cannot be brought back.";

    public const string UseAsTrayIconText = "Use as tray icon";

    public const string AddToText = "Add to";

    public const string RemoveText = "Remove";

    // The custom tab's glyph: Heart, in Segoe Fluent Icons and Segoe MDL2 Assets.
    private const char CustomIcon = '';

    private readonly TrayIcon trayIcon;
    private readonly ContextMenuStrip settingsMenu;
    // The catalog's categories, without the frequent tab.
    private readonly IReadOnlyList<EmojiCategory> categories;
    private readonly Dictionary<string, Emoji> emojisByText;
    private readonly TableLayoutPanel searchBar;
    private readonly TextBox searchBox;
    private readonly Button clearButton;
    private readonly CategoryTabStrip tabStrip;
    private readonly EmojiGrid grid;
    private readonly EmojiUsage usage = EmojiUsage.Load();
    private readonly CustomGroups customGroups = CustomGroups.Load();

    // Whether the frequent tab is shown: its tab and its section first, or neither. The counters count either way.
    private bool showFrequent = SettingsFile.ReadShowFrequent();

    // How many sections the custom tab has in the grid: one per shown group, the placeholder alone while there is no
    // group, none — and no custom tab — while every group is hidden.
    private int customSectionCount;

    // The groups shown, in the user's order: the group of each custom section.
    private List<int> shownGroups = [];
    private readonly ForegroundTracker foregroundTracker = new();
    private readonly ShortcutHook shortcutHook;

    // The invisible resize borders around the visible frame, read the last time the window was shown: a hidden
    // window has no frame to read them from.
    private Padding frameMargins;

    // The client size when the user started a resize, logical pixels: a resize leaving it unchanged — a move, a drag to
    // a monitor of another DPI — saves nothing.
    private Size sizeBeforeResize;

    /// <summary>The second title given with <see cref="TitleArgument"/>, null without one.</summary>
    public string? SecondTitle { get; }

    public MainForm(string? secondTitle)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        this.SecondTitle = secondTitle;
        this.Text = secondTitle is null ? AppTitle : $"{AppTitle} — {secondTitle}";
        // Sized in OnLoad, once the bars are laid out at the window's DPI.
        this.StartPosition = FormStartPosition.CenterScreen;

        // No caption (see WndProc): nothing to minimize or maximize from, and Windows refuses Win+Up, Win+Down and
        // the double-click on the drag area.
        this.MinimizeBox = false;
        this.MaximizeBox = false;

        // The search box on top, the tabs below it, the grid filling the rest. Docking runs from the last control
        // added: the search bar first, then the strip.
        // The frequent tab first, the custom tab next, then the catalog's — the first two while shown. The search box
        // searches the catalog's only: the frequent section and the custom groups would give their emojis twice.
        this.categories = EmojiCatalog.Load();
        this.emojisByText = this.categories.SelectMany(category => category.Emojis).ToDictionary(emoji => emoji.Text);
        List<EmojiCategory> customSections = this.CreateCustomSections();
        this.customSectionCount = customSections.Count;
        this.grid = new EmojiGrid([.. this.CreateFrequentSections(), .. customSections, .. this.categories],
            this.categories.SelectMany(category => category.Emojis))
        {
            Dock = DockStyle.Fill,
        };
        this.tabStrip = new CategoryTabStrip(this.CreateTabs())
        {
            Dock = DockStyle.Top,
        };

        // Never narrower than the tab strip needs, its side resize borders added.
        this.MinimumSize = new Size(this.tabStrip.LogicalMinimumWidth + 2 * LogicalSideBorder, 240);
        this.searchBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        this.clearButton = new Button { Text = "✕", Visible = false, TabStop = false, FlatStyle = FlatStyle.Flat };
        this.clearButton.FlatAppearance.BorderSize = 0;
        this.Controls.Add(this.grid);
        this.Controls.Add(this.tabStrip);
        this.searchBar = this.CreateSearchBar();
        this.Controls.Add(this.searchBar);
        // The placeholder stays while the box has the focus — it always has it, and PlaceholderText hides on focus.
        this.searchBox.HandleCreated += (_, _) => SendMessageW(this.searchBox.Handle, EmSetCueBanner, 1, SearchPlaceholder);
        // The ✕ is a square as high as the box, with its margins: showing it never changes the bar's height.
        this.clearButton.Margin = this.searchBox.Margin;
        this.FitClearButton();
        this.searchBox.SizeChanged += (_, _) => this.FitClearButton();
        this.searchBox.TextChanged += (_, _) => this.OnSearchTextChanged();
        this.grid.KeyPress += this.OnGridKeyPress;
        this.clearButton.Click += (_, _) => this.ClearSearch();
        this.tabStrip.TabClicked += (_, tab) => this.grid.SelectCategory(this.SectionOf(tab));
        this.tabStrip.CloseClicked += (_, _) => this.Close();
        this.settingsMenu = this.CreateSettingsMenu();
        this.tabStrip.SettingsClicked += (_, bounds) =>
            this.settingsMenu.Show(this.tabStrip, new Point(bounds.Right, bounds.Bottom), ToolStripDropDownDirection.BelowLeft);
        this.grid.ActiveCategoryChanged += (_, _) => this.tabStrip.ActiveTab = this.TabOf(this.grid.ActiveCategory);
        this.grid.EmojiClicked += (_, emoji) => this.InsertEmoji(emoji);
        this.grid.EmojiRightClicked += (_, click) => this.ShowEmojiMenu(click);
        this.grid.SectionMenuClicked += (_, request) => this.ShowSectionMenu(request);
        this.grid.EmojiMoved += (_, move) => this.MoveEmoji(move);
        ResumeLayout(performLayout: false);

        // The emoji the user chose, while the catalog still has it; the setting is left as it is otherwise.
        string? trayEmoji = SettingsFile.ReadTrayEmoji();
        this.trayIcon = new TrayIcon(this.Text, trayEmoji is not null && this.emojisByText.ContainsKey(trayEmoji) ? trayEmoji : TrayIcon.DefaultEmoji);
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

    // Every show, whatever its path: the search starts over, the box ready for typing, the grid back at the top — on
    // the first tab — on its first emoji, the one Enter inserts.
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (this.Visible)
        {
            this.ClearSearch();
            this.grid.ResetToTop();
        }
        else
        {
            this.grid.EndReorder();
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
            // Enter inserts the selection: the first result while searching, unless the arrows moved it. Never an emoji
            // of the group in reorder mode.
            if (keyData == Keys.Enter)
            {
                if (this.grid.SelectedEmoji is Emoji emoji && !this.grid.IsSelectionReordered)
                {
                    this.InsertEmoji(emoji);
                }

                return true;
            }

            // Esc ends the reorder mode; otherwise it clears the box, or hides the window when the box is already empty.
            if (keyData == Keys.Escape)
            {
                if (this.grid.IsReordering)
                {
                    this.grid.EndReorder();
                }
                else if (this.searchBox.TextLength > 0)
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

    // Sized before base.OnLoad centres the window: the handle exists, at the DPI of its monitor, and the bars can be
    // measured. The size the user last resized to wins over the default one.
    protected override void OnLoad(EventArgs e)
    {
        this.SetClientArea(SettingsFile.ReadWindowSize() is Size saved ? this.LogicalToDeviceUnits(saved) : this.DefaultClientSize());
        base.OnLoad(e);
    }

    protected override void OnResizeBegin(EventArgs e)
    {
        base.OnResizeBegin(e);
        this.sizeBeforeResize = this.LogicalClientSize();
    }

    // Saved when the user finishes a resize, not at exit: Windows shutting down or the Task Manager may end the app
    // without running its code.
    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        Size size = this.LogicalClientSize();
        if (size != this.sizeBeforeResize)
        {
            SettingsFile.WriteWindowSize(size);
        }
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

    // The client size showing the grid DefaultColumns wide and DefaultRows high, the search bar and the tab strip above
    // it — device pixels, at the current DPI.
    private Size DefaultClientSize()
    {
        // The search bar's preferred height: before the first show, its AutoSize may not have applied yet.
        Size grid = this.grid.SizeFor(DefaultColumns, DefaultRows);
        int searchBarHeight = this.searchBar.GetPreferredSize(new Size(grid.Width, 0)).Height;
        return new Size(grid.Width, searchBarHeight + this.tabStrip.Height + grid.Height);
    }

    // The client size in logical pixels (96 DPI): reloaded on a monitor of another scale, it holds as many emojis.
    private Size LogicalClientSize() => new(
        (int)Math.Round(this.ClientSize.Width * 96.0 / this.DeviceDpi),
        (int)Math.Round(this.ClientSize.Height * 96.0 / this.DeviceDpi));

    // Sizes the window for a client area of clientSize, reduced if needed so the whole window — its invisible resize
    // borders included — fits the working area of its monitor; MinimumSize still wins. The borders are the ones Windows
    // draws: the ClientSize setter counts a caption, which is client area here (see WndProc).
    private void SetClientArea(Size clientSize)
    {
        GetWindowRect(this.Handle, out Rect window);
        GetClientRect(this.Handle, out Rect client);
        var borders = new Size(window.Right - window.Left - client.Right, window.Bottom - window.Top - client.Bottom);
        Rectangle workingArea = Screen.FromControl(this).WorkingArea;
        this.Size = new Size(Math.Min(clientSize.Width + borders.Width, workingArea.Width),
            Math.Min(clientSize.Height + borders.Height, workingArea.Height));
    }

    // The menu of the tab strip's settings button, shown under it, its right edge on the button's.
    private ContextMenuStrip CreateSettingsMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(OpenAppFolderText, image: null, (_, _) => OpenAppFolder());
        menu.Items.Add(NewGroupText, image: null, (_, _) => this.NewGroup());
        var showGroups = new ToolStripMenuItem(ShowGroupsText);
        menu.Items.Add(showGroups);
        menu.Items.Add(ResetWindowSizeText, image: null, (_, _) => this.ResetWindowSize());
        var showFrequentItem = new ToolStripMenuItem(ShowFrequentText);
        showFrequentItem.Click += (_, _) => this.SetShowFrequent(!this.showFrequent);
        menu.Items.Add(showFrequentItem);
        ToolStripItem clearFrequent = menu.Items.Add(ClearFrequentText, image: null, (_, _) => this.ClearFrequent());
        // Hidden, the frequent tab still counts: its counters can still be cleared.
        menu.Opening += (_, _) =>
        {
            this.FillShowGroups(showGroups);
            showFrequentItem.Checked = this.showFrequent;
            clearFrequent.Enabled = !this.usage.IsEmpty;
        };
        menu.Opened += (_, _) => this.tabStrip.SettingsMenuOpen = true;
        menu.Closed += (_, _) => this.tabStrip.SettingsMenuOpen = false;
        return menu;
    }

    // Back to the default size right away, its top-left corner where it is, and at the next launch too: the saved size
    // is removed. The window stays shown.
    private void ResetWindowSize()
    {
        SettingsFile.WriteWindowSize(null);
        this.SetClientArea(this.DefaultClientSize());
    }

    // Every counter reset, after a confirmation: they cannot be brought back. No is the default button.
    private void ClearFrequent()
    {
        DialogResult answer = MessageBox.Show(this, ClearFrequentQuestion, AppTitle, MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (answer == DialogResult.Yes)
        {
            this.usage.Clear();
            if (this.showFrequent)
            {
                this.grid.ReplaceCategory(0, this.CreateFrequentCategory());
            }
        }
    }

    // Show groups ▸ every group, in the user's order, checked while shown: a click hides it or shows it again. Greyed
    // while there is no group.
    private void FillShowGroups(ToolStripMenuItem showGroups)
    {
        while (showGroups.DropDownItems.Count > 0)
        {
            showGroups.DropDownItems[0].Dispose();
        }

        for (int group = 0; group < this.customGroups.Groups.Count; group++)
        {
            int target = group;
            bool hidden = this.customGroups.Groups[group].Hidden;
            var item = new ToolStripMenuItem(MenuName(this.customGroups.Groups[group].Name)) { Checked = !hidden };
            item.Click += (_, _) => this.SetGroupHidden(target, !hidden);
            showGroups.DropDownItems.Add(item);
        }

        showGroups.Enabled = this.customGroups.Groups.Count > 0;
    }

    // A group hidden or shown again, from its "…" button or Show groups; saved with the group.
    private void SetGroupHidden(int group, bool hidden)
    {
        int replaced = this.FirstCustomSection + this.customSectionCount;
        this.customGroups.SetHidden(group, hidden);
        this.RebuildSections(replaced);
    }

    // The frequent tab hidden or shown again, from the settings menu or its section's "…" button; saved.
    private void SetShowFrequent(bool show)
    {
        if (show == this.showFrequent)
        {
            return;
        }

        int replaced = this.FirstCustomSection + this.customSectionCount;
        this.showFrequent = show;
        SettingsFile.WriteShowFrequent(show);
        this.RebuildSections(replaced);
    }

    // The frequent and custom sections and the tabs built again after one of them was hidden or shown: the grid back
    // at the top on its first emoji — unless a search is shown — and the window's minimum width following the tabs.
    // replaced: how many sections the frequent and custom ones were.
    private void RebuildSections(int replaced)
    {
        this.grid.EndReorder();
        List<EmojiCategory> customSections = this.CreateCustomSections();
        this.customSectionCount = customSections.Count;
        this.grid.ReplaceCategories(0, replaced, [.. this.CreateFrequentSections(), .. customSections]);
        this.UpdateTabs();
        if (string.IsNullOrWhiteSpace(this.searchBox.Text))
        {
            this.grid.ResetToTop();
        }

        this.tabStrip.ActiveTab = this.TabOf(this.grid.ActiveCategory);
    }

    // The tab strip given the tabs shown now, and the window never narrower than it needs — device pixels: the form
    // is already scaled to its DPI.
    private void UpdateTabs()
    {
        this.tabStrip.ReplaceTabs(this.CreateTabs());
        this.MinimumSize = new Size(this.LogicalToDeviceUnits(this.tabStrip.LogicalMinimumWidth + 2 * LogicalSideBorder),
            this.MinimumSize.Height);
    }

    // The tabs: the frequent one and the custom one while shown, then the catalog's.
    private List<EmojiCategory> CreateTabs()
    {
        var tabs = new List<EmojiCategory>();
        if (this.showFrequent)
        {
            tabs.Add(new EmojiCategory(FrequentHeader, FrequentIcon, []));
        }

        if (this.customSectionCount > 0)
        {
            tabs.Add(new EmojiCategory(CustomHeader, CustomIcon, []));
        }

        tabs.AddRange(this.categories);
        return tabs;
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

    // The one place told an emoji was used: its counter goes up — the frequent tab hidden too.
    private void OnEmojiUsed(string emoji)
    {
        this.usage.Record(emoji);
        if (this.showFrequent)
        {
            this.grid.ReplaceCategory(0, this.CreateFrequentCategory());
        }
    }

    // The frequent section while shown, none while hidden.
    private List<EmojiCategory> CreateFrequentSections() => this.showFrequent ? [this.CreateFrequentCategory()] : [];

    // The frequent tab: the emojis used most, from the counters, each with its use count under it. One the catalog no
    // longer has is left out. Its "…" button hides it.
    private EmojiCategory CreateFrequentCategory()
    {
        List<Emoji> emojis = this.usage.MostUsed().Select(text => this.emojisByText.GetValueOrDefault(text)).OfType<Emoji>().ToList();
        List<string> counts = emojis.Select(emoji => this.usage.CountOf(emoji.Text))
            .Select(count => count > MaxShownCount ? $"{MaxShownCount}+" : count.ToString(CultureInfo.InvariantCulture))
            .ToList();
        return new EmojiCategory(FrequentHeader, FrequentIcon, emojis, FrequentRows, NoFrequentText, counts, HasMenu: true);
    }

    // The custom tab's sections: one per shown group, under its name with a "…" button; the placeholder pointing to
    // New group… while there is no group; none while every group is hidden. Notes which group each section shows.
    private List<EmojiCategory> CreateCustomSections()
    {
        this.shownGroups = Enumerable.Range(0, this.customGroups.Groups.Count)
            .Where(group => !this.customGroups.Groups[group].Hidden)
            .ToList();
        if (this.customGroups.Groups.Count == 0)
        {
            return [new EmojiCategory(CustomHeader, CustomIcon, [], EmptyText: NoGroupText)];
        }

        return this.shownGroups
            .Select(group => this.customGroups.Groups[group])
            .Select(group => new EmojiCategory(group.Name, CustomIcon, this.ShownEmojis(group), EmptyText: EmptyGroupText, HasMenu: true))
            .ToList();
    }

    // A group's emojis the catalog has, in the group's order: one the catalog no longer has stays in the file only.
    private List<Emoji> ShownEmojis(CustomGroup group) =>
        group.Emojis.Select(text => this.emojisByText.GetValueOrDefault(text)).OfType<Emoji>().ToList();

    // The grid's custom sections built again after a change to the groups; the custom tab comes or goes when the first
    // group is shown or the last one hidden; the active tab follows the sections' new indices.
    private void RefreshCustomSections()
    {
        int customTabCount = this.CustomTabCount;
        List<EmojiCategory> sections = this.CreateCustomSections();
        this.grid.ReplaceCategories(this.FirstCustomSection, this.customSectionCount, sections);
        this.customSectionCount = sections.Count;
        if (this.CustomTabCount != customTabCount)
        {
            this.UpdateTabs();
        }

        this.tabStrip.ActiveTab = this.TabOf(this.grid.ActiveCategory);
    }

    // The sections: the frequent one while shown, the custom ones next, then the catalog's. The tabs alike, every custom
    // section under the custom tab — there while it has a section.
    private int FirstCustomSection => this.showFrequent ? 1 : 0;

    private int CustomTab => this.FirstCustomSection;

    private int CustomTabCount => this.customSectionCount > 0 ? 1 : 0;

    // The tab of a section.
    private int TabOf(int section) =>
        section < this.FirstCustomSection ? section
        : section < this.FirstCustomSection + this.customSectionCount ? this.CustomTab
        : section - this.customSectionCount + this.CustomTabCount;

    // The first section of a tab: up to the custom tab, the tab's own index.
    private int SectionOf(int tab) =>
        tab < this.CustomTab + this.CustomTabCount ? tab : tab - this.CustomTabCount + this.customSectionCount;

    // The group shown in a section; null for a section that is not a group's.
    private int? GroupOf(int section)
    {
        int shown = section - this.FirstCustomSection;
        return shown >= 0 && shown < this.shownGroups.Count ? this.shownGroups[shown] : null;
    }

    // Whether a section is the frequent one: the first while shown — never in search mode, whose results are the
    // first section too.
    private bool IsFrequentSection(int section) =>
        this.showFrequent && section == 0 && string.IsNullOrWhiteSpace(this.searchBox.Text);

    // The section showing a group; null while it is hidden.
    private int? SectionOfGroup(int group) =>
        this.shownGroups.IndexOf(group) is int shown and >= 0 ? this.FirstCustomSection + shown : null;

    // A group's name in a menu: an & is shown, not taken for a mnemonic.
    private static string MenuName(string name) => name.Replace("&", "&&");

    // The name typed in the name dialog; null when cancelled.
    private string? AskGroupName(string title, string name)
    {
        using var dialog = new GroupNameDialog(title, name);
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.GroupName : null;
    }

    // A new group, last, then scrolled to — unless a search is shown.
    private void NewGroup()
    {
        if (this.AskGroupName(NewGroupTitle, string.Empty) is not string name)
        {
            return;
        }

        this.customGroups.Add(name);
        this.RefreshCustomSections();
        if (string.IsNullOrWhiteSpace(this.searchBox.Text) && this.SectionOfGroup(this.customGroups.Groups.Count - 1) is int section)
        {
            this.grid.SelectCategory(section);
        }
    }

    // The menu of a right-clicked emoji: Use as tray icon, checked when the icon shows it; Add to ▸ every group, the
    // ones holding it checked — a click on one of those takes it out; Remove when it was right-clicked in a group,
    // Remove from frequently used in the frequent section.
    private void ShowEmojiMenu(EmojiGrid.EmojiRightClick click)
    {
        var menu = new ContextMenuStrip();
        var useAsTrayIcon = new ToolStripMenuItem(UseAsTrayIconText) { Checked = this.trayIcon.Emoji == click.Emoji.Text };
        useAsTrayIcon.Click += (_, _) => this.UseAsTrayIcon(click.Emoji.Text);
        menu.Items.Add(useAsTrayIcon);
        menu.Items.Add(new ToolStripSeparator());
        var addTo = new ToolStripMenuItem(AddToText) { Enabled = this.customGroups.Groups.Count > 0 };
        for (int group = 0; group < this.customGroups.Groups.Count; group++)
        {
            int target = group;
            var item = new ToolStripMenuItem(MenuName(this.customGroups.Groups[group].Name))
            {
                Checked = this.customGroups.Contains(group, click.Emoji.Text),
            };
            item.Click += (_, _) => this.ToggleInGroup(target, click.Emoji.Text);
            addTo.DropDownItems.Add(item);
        }

        menu.Items.Add(addTo);
        if (this.GroupOf(click.Section) is int shownIn)
        {
            menu.Items.Add(RemoveText, image: null, (_, _) => this.RemoveFromGroup(shownIn, click.Emoji.Text));
        }
        else if (this.IsFrequentSection(click.Section))
        {
            menu.Items.Add(RemoveFrequentText, image: null, (_, _) => this.RemoveFromFrequent(click.Emoji.Text));
        }

        ShowOnce(menu, this.grid, click.Location, ToolStripDropDownDirection.Default);
    }

    // The emoji's counter forgotten: the next one moves up into the section, which reads No emoji used yet once empty.
    // The window stays.
    private void RemoveFromFrequent(string emoji)
    {
        this.usage.Remove(emoji);
        this.grid.ReplaceCategory(0, this.CreateFrequentCategory());
    }

    // The tray icon shows the emoji from now on, and at the next launches: saved like any other, the default included.
    // The window stays.
    private void UseAsTrayIcon(string emoji)
    {
        if (this.trayIcon.Emoji == emoji)
        {
            return;
        }

        this.trayIcon.ShowEmoji(emoji);
        SettingsFile.WriteTrayEmoji(emoji);
    }

    // The menu of a section's "…" button, under it, its right edge on the button's: the frequent section's, or a
    // group's.
    private void ShowSectionMenu(EmojiGrid.SectionMenuRequest request)
    {
        ContextMenuStrip menu;
        if (this.showFrequent && request.Section == 0)
        {
            menu = new ContextMenuStrip();
            menu.Items.Add(HideFrequentText, image: null, (_, _) => this.SetShowFrequent(false));
        }
        else if (this.GroupOf(request.Section) is int group)
        {
            menu = this.CreateGroupMenu(group, request.Section);
        }
        else
        {
            return;
        }

        Rectangle button = request.ButtonBounds;
        ShowOnce(menu, this.grid, new Point(button.Right, button.Bottom), ToolStripDropDownDirection.BelowLeft);
    }

    // A group's menu. Moved up or down past the shown groups only: a hidden one in between keeps its place.
    private ContextMenuStrip CreateGroupMenu(int group, int section)
    {
        int shown = this.shownGroups.IndexOf(group);
        var menu = new ContextMenuStrip();
        menu.Items.Add(RenameGroupText, image: null, (_, _) => this.RenameGroup(group));
        ToolStripItem reorder = menu.Items.Add(ReorderText, image: null, (_, _) => this.grid.StartReorder(section));
        reorder.Enabled = this.ShownEmojis(this.customGroups.Groups[group]).Count > 1;
        ToolStripItem moveUp = menu.Items.Add(MoveUpText, image: null, (_, _) => this.MoveGroup(group, this.shownGroups[shown - 1]));
        moveUp.Enabled = shown > 0;
        ToolStripItem moveDown = menu.Items.Add(MoveDownText, image: null, (_, _) => this.MoveGroup(group, this.shownGroups[shown + 1]));
        moveDown.Enabled = shown < this.shownGroups.Count - 1;
        menu.Items.Add(HideGroupText, image: null, (_, _) => this.SetGroupHidden(group, true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(DeleteGroupText, image: null, (_, _) => this.DeleteGroup(group));
        return menu;
    }

    // A menu built for one show, disposed once closed — after the click on its item is handled.
    private static void ShowOnce(ContextMenuStrip menu, Control control, Point location, ToolStripDropDownDirection direction)
    {
        menu.Closed += (_, _) => control.BeginInvoke(menu.Dispose);
        menu.Show(control, location, direction);
    }

    private void ToggleInGroup(int group, string emoji)
    {
        if (this.customGroups.Contains(group, emoji))
        {
            this.customGroups.RemoveEmoji(group, emoji);
        }
        else
        {
            this.customGroups.AddEmoji(group, emoji);
        }

        this.RefreshCustomSections();
    }

    private void RemoveFromGroup(int group, string emoji)
    {
        this.customGroups.RemoveEmoji(group, emoji);
        this.RefreshCustomSections();
    }

    private void RenameGroup(int group)
    {
        if (this.AskGroupName(RenameGroupTitle, this.customGroups.Groups[group].Name) is string name)
        {
            this.customGroups.Rename(group, name);
            this.RefreshCustomSections();
        }
    }

    // Swapped with its shown neighbour, then kept in view: it now stands where that one stood.
    private void MoveGroup(int group, int neighbour)
    {
        this.grid.EndReorder();
        this.customGroups.Swap(group, neighbour);
        this.RefreshCustomSections();
        if (this.SectionOfGroup(neighbour) is int section)
        {
            this.grid.SelectCategory(section);
        }
    }

    // A group holding emojis is deleted after a confirmation, No the default button; an empty one right away.
    private void DeleteGroup(int group)
    {
        CustomGroup deleted = this.customGroups.Groups[group];
        if (deleted.Emojis.Count > 0)
        {
            string question = string.Format(CultureInfo.InvariantCulture, DeleteGroupQuestion, deleted.Name);
            DialogResult answer = MessageBox.Show(this, question, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }
        }

        this.grid.EndReorder();
        this.customGroups.Delete(group);
        this.RefreshCustomSections();
    }

    // An emoji dragged inside a group in reorder mode: from and to are indices among the emojis shown.
    private void MoveEmoji(EmojiGrid.EmojiMove move)
    {
        if (this.GroupOf(move.Section) is not int group)
        {
            return;
        }

        List<Emoji> shown = this.ShownEmojis(this.customGroups.Groups[group]);
        Emoji moved = shown[move.From];
        shown.RemoveAt(move.From);
        this.customGroups.MoveEmoji(group, moved.Text, move.To < shown.Count ? shown[move.To].Text : null);
        this.RefreshCustomSections();
    }

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
    private static extern bool GetClientRect(IntPtr window, out Rect rect);

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
