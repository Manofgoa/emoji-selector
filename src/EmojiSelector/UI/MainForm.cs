using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using EmojiSelector.Data;
using EmojiSelector.Drawing;
using EmojiSelector.Input;

namespace EmojiSelector.UI;

internal sealed class MainForm : Form
{
    /// <summary>
    /// The command-line option giving the second title: <c>--title &lt;text&gt;</c> shows
    /// <c>Emoji Selector — &lt;text&gt;</c>, so instances running side by side tell each other apart.
    /// </summary>
    public const string TitleArgument = "--title";

    /// <summary>
    /// The command-line option starting the app hidden in the notification area, its window shown by Win+; or the tray
    /// icon: the Start with Windows shortcut passes it.
    /// </summary>
    public const string BackgroundArgument = "--background";

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

    // The window's minimum height, the details panel's height added, in logical pixels.
    private const int LogicalMinimumHeight = 240;

    public const string OpenAppFolderText = "Open app folder";

    public const string ResetWindowSizeText = "Reset window size";

    public const string WindowOpacityText = "Window opacity";
    public const string AcrylicText = "Acrylic";
    public const string AcrylicVeilText = "Acrylic veil";

    // The strengths Acrylic veil offers, percent.
    private static readonly int[] VeilPercents = [10, 20, 30, 40, 50, 60, 70, 80, 90];

    // The search box and its ✕ on the backdrop: native controls, faded whole — field and text — so the white field
    // does not stand out as a bar.
    private const byte SearchBoxAlpha = 0xB0;

    // The opacities the settings menu offers, percent, the most opaque first: a saved value that is not one of them
    // takes the nearest, a tie going to the more opaque (see NearestOpacity).
    private static readonly int[] OpacityPercents = [100, 98, 96, 94, 92];

    public const string ShowFrequentText = "Show frequently used";

    public const string HideFrequentText = "Hide frequently used";

    public const string ClearFrequentText = "Clear frequently used";

    public const string ShowFrenchText = "Show French names";

    public const string HighlightColorText = "Highlight color…";

    public const string EmojiZoomText = "Emoji zoom";

    // The emojis' zoom, percent: from MinZoom to MaxZoom by ZoomStep, 100 by default. A saved one between two steps, or
    // out of the range, takes the nearest step (see NearestZoom).
    private const int MinZoom = 70;
    private const int MaxZoom = 400;
    private const int ZoomStep = 10;
    private const int DefaultZoom = 100;

    // The zoom box's width in the settings menu, in logical pixels.
    private const int LogicalZoomBoxWidth = 64;

    public const string AlwaysShowSkinToneText = "Always show skin tone";

    public const string StartWithWindowsText = "Start with Windows";

    public const string StartupFailedText = "Could not change the startup shortcut: {0}";

    public const string ClearFrequentQuestion =
        "Clear the frequently used emojis? Their counts are deleted and cannot be brought back.";

    public const string CheckEmojiUpdatesText = "Check for emoji updates…";

    public const string UpToDateText = "Emoji data is up to date (Emojibase {0}, Twemoji flags {1}).";

    public const string CheckFailedText = "Could not check for emoji updates: {0}";

    public const string UpdateAvailableQuestion = "{0}\n\nUpdate now?";

    public const string EmojiDataAvailableLine =
        "The emoji list, names and keywords: Emojibase {0} is available (in use: {1}).";

    public const string FlagImagesAvailableLine = "The flag images: Twemoji {0} is available (in use: {1}).";

    public const string UpdateFailedText = "Could not update the emoji data: {0}";

    public const string FlagImagesUpdateFailedText = "Could not update the flag images: {0}";

    public const string InvalidDownloadText = "the downloaded files are not valid emoji data.";

    public const string RestartQuestion = "Updated to {0}. Restart now to use it?";

    public const string RestartFailedText = "Could not restart the app: {0}";

    public const string ResetAllText = "Reset all settings…";

    public const string ResetAllQuestion =
        "Reset Emoji Selector to its first-launch state?\n\n"
        + "This deletes, and cannot be undone:\n"
        + "• the custom groups and their emojis\n"
        + "• the use counters of Frequently used\n"
        + "• the window size and opacity, the emoji zoom, the tray emoji, the details panel's settings\n"
        + "• the skin tones, the default ones and the emojis' own\n"
        + "• the special characters' list — back to the default one\n"
        + "• Start with Windows\n"
        + "• the downloaded emoji data — back to version {0}\n"
        + "• the downloaded flag images — back to Twemoji {1}\n"
        + "• the emoji image cache\n\n"
        + "The app then restarts.";

    public const string ResetFailuresText = "Some files could not be deleted:\n\n{0}";

    public const string ResetRestartFailedText =
        "The settings were reset. Start the app again to finish.\n\nCould not restart the app: {0}";

    public const string FrequentHeader = "Frequently used";

    public const string NoFrequentText = "No emoji used yet";

    public const string RemoveFrequentText = "Remove from frequently used";

    // The frequent tab's glyph: FavoriteStar, in Segoe Fluent Icons and Segoe MDL2 Assets.
    private const char FrequentIcon = '';

    // The frequent section shows as many emojis as this many rows of the grid hold.
    private const int FrequentRows = 3;

    // A use count beyond this one shows as "999+": it never overflows its cell.
    private const int MaxShownCount = 999;

    // A keep-open insertion takes the foreground back this long after typing, milliseconds: SendInput is asynchronous,
    // and taking it back at once could route the emoji's characters to this window.
    private const int KeepOpenHandBackDelay = 50;

    // An insertion checks this often whether the previous window is ready to receive the emoji (see TypeWhenReady),
    // milliseconds.
    private const int TypingPollInterval = 10;

    // Once the previous window is ready, the emoji is typed this long after, milliseconds: a Chromium app hands the
    // keyboard focus on to its page — the Claude desktop app's prompt box — after its window got it.
    private const int TypingSettleDelay = 100;

    // The emoji is typed this long after the previous window was activated whether it is ready or not, milliseconds:
    // never worse than typing at once.
    private const int TypingTimeout = 1000;

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

    public const string SkinToneText = "Skin tone";

    public const string UseDefaultToneText = "Use default tone";

    public const string FirstPersonText = "First person";

    public const string SecondPersonText = "Second person";

    public const string SpecialHeader = "Special characters";

    public const string ShowSpecialText = "Show special characters";

    public const string ShowEmojiSubcategoriesText = "Show emoji sub-categories";

    public const string ShowSpecialSubcategoriesText = "Show special character sub-categories";

    public const string InvalidSpecialText = "{0} is not valid: {1}\n\nThe default list is used until it is fixed.";

    // The special characters' tab glyph: Characters, in Segoe Fluent Icons and Segoe MDL2 Assets.
    private const char SpecialIcon = '';

    // The custom tab's glyph: Heart, in Segoe Fluent Icons and Segoe MDL2 Assets.
    private const char CustomIcon = '';

    private readonly TrayIcon trayIcon;
    private readonly Icon appIcon;
    private readonly ContextMenuStrip settingsMenu;
    // The catalog's categories, without the frequent tab.
    private readonly IReadOnlyList<EmojiCategory> categories;

    // The Emojibase version the categories come from.
    private readonly Version emojiDataVersion;

    // The Emojibase version Check for emoji updates wrote to emoji-data\, used from the next launch; null until then.
    private Version? downloadedEmojiDataVersion;

    // The Twemoji version Check for emoji updates wrote to flag-images\, used from the next launch; null until then.
    private Version? downloadedFlagImagesVersion;
    private readonly Dictionary<string, Emoji> emojisByText;
    private readonly TableLayoutPanel searchBar;
    private readonly TextBox searchBox;
    private readonly Button clearButton;
    private readonly CategoryTabStrip tabStrip;
    private readonly EmojiGrid grid;
    private readonly EmojiDetailsPanel detailsPanel;
    private readonly EmojiUsage usage = EmojiUsage.Load();
    private readonly CustomGroups customGroups = CustomGroups.Load();
    private readonly SkinToneChoices skinToneChoices = SkinToneChoices.Load();

    // Every variant's text: a tray emoji may be one.
    private readonly HashSet<string> variantTexts;

    // The default skin tone: every emoji with variants without a tone of its own, both persons of a two-person one.
    private SkinTone skinTone = SettingsFile.ReadSkinTone();

    // The tone a skin-tone menu's highlighted item previews (see PreviewWhileHighlighted), null while none is. Never
    // saved: skinTone and skinToneChoices keep the real tones.
    private TonePreview? tonePreview;

    // Whether the frequent tab is shown: its tab and its section first, or neither. The counters count either way.
    private bool showFrequent = SettingsFile.ReadShowFrequent();

    // The special characters, one category after the catalog's, its families as its sub-categories — from
    // special-characters.json, or the embedded default while that file is not valid.
    private readonly EmojiCategory specialCategory;

    // Whether the special characters' tab is shown: its tab and its section last, or neither.
    private bool showSpecial = SettingsFile.ReadShowSpecial();

    // Whether the catalog's categories, and the special characters', show their sub-categories: a label at the start of
    // a row, their emojis to its right.
    private bool showEmojiSubcategories = SettingsFile.ReadShowEmojiSubcategories();
    private bool showSpecialSubcategories = SettingsFile.ReadShowSpecialSubcategories();

    // Why special-characters.json is not valid, told once on the window's first show; null when there is nothing to tell.
    private string? specialCharactersError;

    // The window's opacity, percent: one of OpacityPercents, 100 when nothing is saved.
    private int opacityPercent = NearestOpacity(SettingsFile.ReadOpacity() ?? 100);

    // Windows' Acrylic backdrop under the whole window, in place of the opacity: the two are one setting.
    private bool acrylic = SettingsFile.ReadAcrylic();

    // The emojis' zoom, percent — the grid's cells and emojis, the details panel's large emoji —: a step of the range,
    // DefaultZoom when nothing is saved.
    private int emojiZoom = NearestZoom(SettingsFile.ReadEmojiZoom() ?? DefaultZoom);

    // The settings menu's zoom box, and whether the code is setting its value: its ValueChanged is not the user's then.
    private PercentUpDown? zoomBox;
    private bool settingZoomBox;

    // How many sections the custom tab has in the grid: one per shown group, the placeholder alone while there is no
    // group, none — and no custom tab — while every group is hidden.
    private int customSectionCount;

    // Near mode: the matches of the term, most relevant first — empty outside near mode —, and the one F3 / Shift+F3
    // last jumped to.
    private IReadOnlyList<Emoji> nearMatches = [];

    private int nearIndex;

    // The groups shown, in the user's order: the group of each custom section.
    private List<int> shownGroups = [];
    private readonly ForegroundTracker foregroundTracker = new();
    private readonly ShortcutHook shortcutHook;

    // Running while a keep-open insertion waits to take the foreground back (see InsertKeepingOpen).
    private readonly System.Windows.Forms.Timer handBackTimer = new() { Interval = KeepOpenHandBackDelay };

    // Running while an insertion waits for the previous window before typing (see TypeWhenReady).
    private readonly System.Windows.Forms.Timer typingTimer = new() { Interval = TypingPollInterval };

    // The insertion typingTimer waits for; null when none.
    private PendingTyping? pendingTyping;

    // The invisible resize borders around the visible frame, read the last time the window was shown: a hidden
    // window has no frame to read them from.
    private Padding frameMargins;

    // The client size when the user started a resize, logical pixels: a resize leaving it unchanged — a move, a drag to
    // a monitor of another DPI — saves nothing.
    private Size sizeBeforeResize;

    // Released before a restart, so the restarted app is the first instance.
    private readonly SingleInstance singleInstance;

    // Started with BackgroundArgument and not shown yet: Application.Run's show is skipped.
    private bool startHidden;

    // The text cursor Win+; places the window under while it shows it: the first show sizes the window in OnLoad,
    // after it was placed. Null, the first show places it in the corner of the mouse's monitor.
    private Rectangle? showAnchor;

    // OnLoad placed the window in the corner before its frame could be measured: OnShown places it there again.
    private bool placeInCornerWhenShown;

    // The window is where PlaceInCorner put it, not moved since: a change of DPI places it there again (see
    // OnDpiChanged).
    private bool inCorner;

    /// <summary>The second title given with <see cref="TitleArgument"/>, null without one.</summary>
    public string? SecondTitle { get; }

    public MainForm(string? secondTitle, bool startHidden, SingleInstance singleInstance)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        this.SecondTitle = secondTitle;
        this.startHidden = startHidden;
        this.singleInstance = singleInstance;
        this.Text = secondTitle is null ? AppTitle : $"{AppTitle} — {secondTitle}";
        // The exe's icon, fixed: the emoji chosen for the tray icon never changes it.
        this.appIcon = LoadAppIcon();
        this.Icon = this.appIcon;
        // Sized in OnLoad, once the bars are laid out at the window's DPI, then placed in the corner of the mouse's
        // monitor (see PlaceInCorner).
        this.StartPosition = FormStartPosition.Manual;

        // No caption (see WndProc): nothing to minimize or maximize from, and Windows refuses Win+Up, Win+Down and
        // the double-click on the drag area.
        this.MinimizeBox = false;
        this.MaximizeBox = false;

        // Below 100 %, a layered window: the whole of it fades, in front or not. Set before the handle exists, so a
        // --background launch shows it at its opacity from the first show.
        // With the Acrylic backdrop, fully opaque: a layered window has no backdrop.
        this.Opacity = this.acrylic ? 1.0 : this.opacityPercent / 100.0;
        // The veil's saved strength — one written by hand taken to the nearest offered —, before the first paint.
        int veil = SettingsFile.ReadAcrylicVeil() ?? UI.Glass.DefaultVeilPercent;
        UI.Glass.VeilPercent = VeilPercents.MinBy(offered => Math.Abs(offered - veil));

        // The search box on top, the tabs below it, the details panel at the bottom, the grid filling the rest. Docking
        // runs from the last control added: the search bar first, then the strip, then the panel.
        // The frequent tab first, the custom tab next, then the catalog's, then the special characters' — the first two
        // and the last while shown. The search box searches the catalog's and the special characters' only: the frequent
        // section and the custom groups would give their emojis twice.
        EmojiDataFolder.EmojiData emojiData = EmojiDataFolder.Load();
        this.emojiDataVersion = emojiData.Version;
        this.categories = emojiData.Categories;
        SpecialCharacters.LoadResult special = SpecialCharacters.Load(SpecialHeader, SpecialIcon);
        this.specialCategory = special.Category;
        this.specialCharactersError = special.Error;
        // A special character whose text the catalog has is the catalog's emoji there: the custom groups find either.
        this.emojisByText = this.categories.SelectMany(category => category.Emojis).ToDictionary(emoji => emoji.Text);
        foreach (Emoji character in this.specialCategory.Emojis)
        {
            this.emojisByText.TryAdd(character.Text, character);
        }

        this.variantTexts = [.. this.emojisByText.Values.SelectMany(emoji => emoji.Variants).Select(variant => variant.Text)];
        List<EmojiCategory> customSections = this.CreateCustomSections();
        this.customSectionCount = customSections.Count;
        List<Emoji> offered = [.. this.categories.SelectMany(category => category.Emojis),
            .. this.specialCategory.Emojis];
        this.grid = new EmojiGrid(
            [.. this.CreateFrequentSections(), .. customSections, .. this.ShownCatalogSections(), .. this.ShownSpecialSections()],
            offered, this.emojiZoom)
        {
            Dock = DockStyle.Fill,
            ShownText = this.ShownText,
        };
        this.tabStrip = new CategoryTabStrip(this.CreateTabs())
        {
            Dock = DockStyle.Top,
        };
        this.detailsPanel = new EmojiDetailsPanel(offered)
        {
            Dock = DockStyle.Bottom,
            ShowFrench = SettingsFile.ReadShowFrench(),
            AlwaysShowTone = SettingsFile.ReadAlwaysShowSkinTone(),
            HighlightColor = SettingsFile.ReadHighlightColor() ?? EmojiDetailsPanel.DefaultHighlightColor,
            ShownText = this.ShownText,
            DefaultTone = this.skinTone,
            ZoomPercent = this.emojiZoom,
        };

        // Never narrower than the tab strip needs, its side resize borders added. Never lower than the details panel
        // needs, room left for the grid: see UpdateMinimumHeight.
        this.MinimumSize = new Size(this.tabStrip.LogicalMinimumWidth + 2 * LogicalSideBorder, LogicalMinimumHeight);
        this.searchBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        this.clearButton = new Button { Text = "✕", Visible = false, TabStop = false, FlatStyle = FlatStyle.Flat };
        this.clearButton.FlatAppearance.BorderSize = 0;
        this.Controls.Add(this.grid);
        this.Controls.Add(this.detailsPanel);
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
        this.grid.SelectedEmojiChanged += (_, _) => this.detailsPanel.ShownEmoji = this.grid.SelectedEmoji;
        this.detailsPanel.SizeChanged += (_, _) => this.UpdateMinimumHeight();
        this.detailsPanel.ToneSwatchClicked += (_, bounds) => this.ShowToneMenu(bounds);
        this.grid.EmojiClicked += (_, click) => this.InsertEmoji(click.Emoji, click.KeepOpen);
        this.grid.EmojiRightClicked += (_, click) => this.ShowEmojiMenu(click);
        this.grid.SectionMenuClicked += (_, request) => this.ShowSectionMenu(request);
        this.grid.EmojiMoved += (_, move) => this.MoveEmoji(move);
        this.grid.ZoomRequested += (_, request) => this.SetEmojiZoom(this.emojiZoom + request.Steps * ZoomStep, request.Anchor);
        ResumeLayout(performLayout: false);

        // The emoji the user chose — in its skin tone —, while the catalog still has it; the setting is left as it is
        // otherwise.
        string? trayEmoji = SettingsFile.ReadTrayEmoji();
        this.trayIcon = new TrayIcon(this.Text,
            trayEmoji is not null && (this.emojisByText.GetValueOrDefault(trayEmoji) is { IsText: false } || this.variantTexts.Contains(trayEmoji))
                ? trayEmoji : TrayIcon.DefaultEmoji);
        this.trayIcon.Clicked += this.OnTrayIconClicked;
        this.trayIcon.ExitRequested += (_, _) => Application.Exit();

        // Created after the controls: the hook posts Win+; through the UI thread's synchronization context.
        this.shortcutHook = new ShortcutHook();
        this.shortcutHook.Pressed += this.OnShortcutPressed;
        this.handBackTimer.Tick += (_, _) => this.HandForegroundBack();
        this.typingTimer.Tick += (_, _) => this.OnTypingTick();
        this.singleInstance.ShowRequested += this.OnShowRequested;
        this.singleInstance.Listen();
    }

    // Started with BackgroundArgument, Application.Run's show leaves the window hidden: the tray icon, the hook and the
    // pre-render run as on a normal launch, and the first show — Win+; or the tray icon — runs OnLoad then.
    protected override void SetVisibleCore(bool value)
    {
        if (this.startHidden && value)
        {
            this.startHidden = false;
            value = false;
        }

        base.SetVisibleCore(value);
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

    // The keyboard has two places: the search box, and the grid once a navigation key hands it over — ↓, ← / → at the
    // text's edge, or any of them while the box is empty. The grid only takes the focus that way — it is not selectable, a click never focuses it.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (this.grid.Focused)
        {
            // The selection's right-click menu. In the box, these keys keep the box's own menu.
            if (keyData is Keys.Apps or (Keys.Shift | Keys.F10))
            {
                this.grid.OpenSelectionMenu();
                return true;
            }

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
            // An empty box has no caret to move: the navigation keys act as if the grid had the keyboard, from the
            // selection, and hand it over — even when the selection cannot move. ↑ on the grid's first row would hand it
            // back to the box: it stays there. Near mode alike: its arrows walk from the match to its neighbours.
            if ((this.searchBox.TextLength == 0 || this.IsNearMode) && EmojiGrid.IsNavigationKey(keyData))
            {
                if (this.grid.MoveSelection(keyData))
                {
                    this.grid.Focus();
                }

                return true;
            }

            // With text, ↓ — and a plain ← / → at the text's edge: the caret at its start / end, no text selected — has
            // nowhere to go in the box: it acts as in the grid, from the selection, and hands the keyboard over — even
            // when the selection cannot move. No result: nothing to select, the keyboard stays in the box.
            bool atEdge = this.searchBox.SelectionLength == 0
                && (keyData == Keys.Left && this.searchBox.SelectionStart == 0
                    || keyData == Keys.Right && this.searchBox.SelectionStart == this.searchBox.TextLength);
            if (atEdge || keyData == Keys.Down)
            {
                if (this.grid.SelectedEmoji is not null)
                {
                    this.grid.MoveSelection(keyData);
                    this.grid.Focus();
                }

                return true;
            }

            // With text in the box, only ↓ and ← / → at the text's edge leave it.
            if (keyData is Keys.PageUp or Keys.PageDown or Keys.Tab or (Keys.Shift | Keys.Tab))
            {
                return true;
            }
        }

        if (this.grid.Focused || this.searchBox.Focused)
        {
            // Near mode: F3 / Shift+F3 jump to the next / previous match, wrapping around — from the box and the grid.
            if (keyData is Keys.F3 or (Keys.Shift | Keys.F3) && this.IsNearMode)
            {
                this.StepNearMatch(keyData == Keys.F3 ? 1 : -1);
                return true;
            }

            // Enter inserts the selection: the first result while searching, unless the arrows moved it. Never an emoji
            // of the group in reorder mode. Ctrl+Enter inserts it too, the window kept open.
            if (keyData is Keys.Enter or (Keys.Control | Keys.Enter))
            {
                if (this.grid.SelectedEmoji is Emoji emoji && !this.grid.IsSelectionReordered)
                {
                    this.InsertEmoji(emoji, keepOpen: keyData != Keys.Enter);
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

    // Sized on the first show: the handle exists, at the DPI of its monitor, and the bars can be measured. The size the
    // user last resized to wins over the default one. Then placed: under the text cursor on a first show by Win+; —
    // after a start with BackgroundArgument —, in the corner of the mouse's monitor otherwise.
    protected override void OnLoad(EventArgs e)
    {
        this.SetClientArea(SettingsFile.ReadWindowSize() is Size saved ? this.LogicalToDeviceUnits(saved) : this.DefaultClientSize());
        base.OnLoad(e);
        this.ApplyBackdrop();
        if (this.showAnchor is Rectangle anchor)
        {
            this.PlaceAt(anchor);
        }
        else
        {
            this.PlaceInCorner();
            this.placeInCornerWhenShown = true;
        }
    }

    // Placed again in the corner once the first show is done, its frame now measurable: OnLoad could not read it, and a
    // move to a monitor of another DPI may have resized the window.
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (this.placeInCornerWhenShown)
        {
            this.placeInCornerWhenShown = false;
            this.PlaceInCorner();
        }

        // The Acrylic frame computed again once the window is on screen, as a change of DPI does it: applied before the
        // first show only, a band at the top kept a plain colour.
        if (this.acrylic)
        {
            UI.Glass.Apply(this.Handle, acrylic: true);
            SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        // An invalid special-characters.json, told once, over the window — posted, so the window is drawn first.
        if (this.specialCharactersError is string error)
        {
            this.specialCharactersError = null;
            this.BeginInvoke(() => MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, InvalidSpecialText,
                SpecialCharacters.FileName, error), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning));
        }
    }

    // The details panel's height follows the width, set before the docking places the controls: set during it, the
    // grid would keep the space of the panel's previous height.
    protected override void OnLayout(LayoutEventArgs levent)
    {
        // Null while the base constructor runs.
        this.detailsPanel?.FitHeight(this.ClientSize.Width);
        base.OnLayout(levent);
    }

    // The Acrylic backdrop asked of Windows or not, every part of the window drawn on it or not.
    private void ApplyBackdrop()
    {
        if (!this.IsHandleCreated)
        {
            return;
        }

        ((SearchBar)this.searchBar).Glass = this.acrylic;
        UI.Glass.SetOpaque(this.searchBox, this.acrylic, SearchBoxAlpha);
        UI.Glass.SetOpaque(this.clearButton, this.acrylic, SearchBoxAlpha);
        this.tabStrip.Glass = this.acrylic;
        this.grid.Glass = this.acrylic;
        this.detailsPanel.Glass = this.acrylic;
        this.UpdateStyles();
        UI.Glass.Apply(this.Handle, this.acrylic);
        this.Invalidate(invalidateChildren: true);
    }

    // The Acrylic backdrop, from the opacity menu: in place of the opacity — the window fully opaque, a layered window
    // having no backdrop —, applied at once, saved.
    private void SetAcrylic()
    {
        if (!this.acrylic)
        {
            this.acrylic = true;
            this.Opacity = 1.0;
            SettingsFile.WriteAcrylic();
            this.ApplyBackdrop();
        }
    }

    // The strength of the Acrylic backdrop's veil, from the settings menu: drawn again at once, saved.
    private void SetAcrylicVeil(int percent)
    {
        if (percent != UI.Glass.VeilPercent)
        {
            UI.Glass.VeilPercent = percent;
            SettingsFile.WriteAcrylicVeil(percent);
            this.Invalidate(invalidateChildren: true);
        }
    }

    protected override void OnResizeBegin(EventArgs e)
    {
        base.OnResizeBegin(e);
        this.sizeBeforeResize = this.LogicalClientSize();
        // The user moves or resizes the window: it leaves its corner.
        this.inCorner = false;
    }

    // Windows applies the DPI of a monitor of another scale once the window is shown there, after the placement that
    // brought it: resized around its top-left corner, the window would leave its corner, so it is placed there again.
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (this.inCorner && this.Visible)
        {
            this.PlaceInCorner();
        }
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

    // A tool window, like Windows' emoji panel: no taskbar button, no Alt+Tab nor Task View entry. Never ShowInTaskbar =
    // false: WinForms makes the form owned by a hidden window for it — an owned window stays in Alt+Tab, and .NET's
    // Process.MainWindowHandle skips it. WS_EX_APPWINDOW, which forces a taskbar button, is taken out.
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle = (parameters.ExStyle | WsExToolWindow) & ~WsExAppWindow;
            // With the Acrylic backdrop the frame is extended into the client area, and DWM would draw the caption's buttons
            // in it: no caption, no buttons. The resize borders, the shadow and the system menu stay.
            if (this.acrylic)
            {
                parameters.Style &= ~WsCaption;
            }

            return parameters;
        }
    }

    // The window frame is computed again, now that WndProc answers WM_NCCALCSIZE. Windows 11 gives a tool window the small
    // corner radius of a menu: the corners are asked round, as an app window's (Windows 10 has none, and refuses).
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = DwmwcpRound;
        DwmSetWindowAttribute(this.Handle, DwmwaWindowCornerPreference, ref round, sizeof(int));
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

            // The Acrylic backdrop's frame extended again on every activation, as Microsoft's custom frame sample does:
            // extended only before the first show, a band at the top kept a plain colour until a change of DPI made
            // Windows compute the frame again.
            case WmActivate when this.acrylic:
                base.WndProc(ref m);
                UI.Glass.Apply(this.Handle, acrylic: true);
                return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !this.IsDisposed)
        {
            this.shortcutHook.Dispose();
            this.handBackTimer.Dispose();
            this.typingTimer.Dispose();
            this.settingsMenu.Dispose();
            this.trayIcon.Dispose();
            this.foregroundTracker.Dispose();
            this.appIcon.Dispose();
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
    // it, the details panel below it — device pixels, at the current DPI.
    private Size DefaultClientSize()
    {
        // The search bar's preferred height: before the first show, its AutoSize may not have applied yet.
        Size grid = this.grid.SizeFor(DefaultColumns, DefaultRows);
        int searchBarHeight = this.searchBar.GetPreferredSize(new Size(grid.Width, 0)).Height;
        // At 100 % whatever the zoom: the default size does not follow it, the grid and the panel do.
        return new Size(grid.Width,
            searchBarHeight + this.tabStrip.Height + grid.Height + this.detailsPanel.HeightFor(grid.Width, DefaultZoom));
    }

    // The details panel's height follows the window's width: the window's minimum height follows it — device pixels,
    // the form is already scaled to its DPI once its handle exists.
    private void UpdateMinimumHeight()
    {
        if (this.IsHandleCreated)
        {
            this.MinimumSize = new Size(this.MinimumSize.Width,
                this.LogicalToDeviceUnits(LogicalMinimumHeight) + this.detailsPanel.Height);
        }
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

    // The menu of the tab strip's settings button, shown under it, its right edge on the button's. One section per
    // feature, in the window's order, separated by lines: frequently used, custom groups, emoji categories, special
    // characters, details panel, window, app.
    private ContextMenuStrip CreateSettingsMenu()
    {
        var menu = new ContextMenuStrip();
        var showFrequentItem = new ToolStripMenuItem(ShowFrequentText);
        showFrequentItem.Click += (_, _) => this.SetShowFrequent(!this.showFrequent);
        menu.Items.Add(showFrequentItem);
        ToolStripItem clearFrequent = menu.Items.Add(ClearFrequentText, image: null, (_, _) => this.ClearFrequent());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(NewGroupText, image: null, (_, _) => this.NewGroup());
        var showGroups = new ToolStripMenuItem(ShowGroupsText);
        menu.Items.Add(showGroups);
        menu.Items.Add(new ToolStripSeparator());
        var showEmojiSubcategoriesItem = new ToolStripMenuItem(ShowEmojiSubcategoriesText);
        showEmojiSubcategoriesItem.Click += (_, _) => this.SetShowEmojiSubcategories(!this.showEmojiSubcategories);
        menu.Items.Add(showEmojiSubcategoriesItem);
        menu.Items.Add(new ToolStripSeparator());
        var showSpecialItem = new ToolStripMenuItem(ShowSpecialText);
        showSpecialItem.Click += (_, _) => this.SetShowSpecial(!this.showSpecial);
        menu.Items.Add(showSpecialItem);
        var showSpecialSubcategoriesItem = new ToolStripMenuItem(ShowSpecialSubcategoriesText);
        showSpecialSubcategoriesItem.Click += (_, _) => this.SetShowSpecialSubcategories(!this.showSpecialSubcategories);
        menu.Items.Add(showSpecialSubcategoriesItem);
        menu.Items.Add(new ToolStripSeparator());
        var showFrenchItem = new ToolStripMenuItem(ShowFrenchText);
        showFrenchItem.Click += (_, _) => this.SetShowFrench(!this.detailsPanel.ShowFrench);
        menu.Items.Add(showFrenchItem);
        var highlightColorItem = new ToolStripMenuItem(HighlightColorText);
        highlightColorItem.Click += (_, _) => this.ChooseHighlightColor();
        menu.Items.Add(highlightColorItem);
        menu.Items.Add(this.CreateZoomItem());
        var alwaysShowSkinToneItem = new ToolStripMenuItem(AlwaysShowSkinToneText);
        alwaysShowSkinToneItem.Click += (_, _) => this.SetAlwaysShowSkinTone(!this.detailsPanel.AlwaysShowTone);
        menu.Items.Add(alwaysShowSkinToneItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(ResetWindowSizeText, image: null, (_, _) => this.ResetWindowSize());
        var windowOpacity = new ToolStripMenuItem(WindowOpacityText);
        foreach (int percent in OpacityPercents)
        {
            var item = new ToolStripMenuItem($"{percent}%") { Tag = percent };
            item.Click += (_, _) => this.SetOpacity(percent);
            windowOpacity.DropDownItems.Add(item);
        }

        // The Acrylic backdrop, last: in place of an opacity, never with one.
        windowOpacity.DropDownItems.Add(new ToolStripSeparator());
        var acrylicItem = new ToolStripMenuItem(AcrylicText);
        acrylicItem.Click += (_, _) => this.SetAcrylic();
        windowOpacity.DropDownItems.Add(acrylicItem);
        menu.Items.Add(windowOpacity);
        // The veil's strength, greyed while the backdrop is off.
        var acrylicVeil = new ToolStripMenuItem(AcrylicVeilText);
        foreach (int percent in VeilPercents)
        {
            var item = new ToolStripMenuItem($"{percent}%") { Tag = percent };
            item.Click += (_, _) => this.SetAcrylicVeil(percent);
            acrylicVeil.DropDownItems.Add(item);
        }

        menu.Items.Add(acrylicVeil);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(OpenAppFolderText, image: null, (_, _) => OpenAppFolder());
        var startWithWindowsItem = new ToolStripMenuItem(StartWithWindowsText);
        startWithWindowsItem.Click += (_, _) => this.SetStartWithWindows(!startWithWindowsItem.Checked);
        menu.Items.Add(startWithWindowsItem);
        ToolStripItem checkEmojiUpdates = menu.Items.Add(CheckEmojiUpdatesText, image: null, null);
        checkEmojiUpdates.Click += async (_, _) => await this.CheckEmojiUpdatesAsync(checkEmojiUpdates);
        menu.Items.Add(ResetAllText, image: null, (_, _) => this.ResetAllSettings());
        // Hidden, the frequent tab still counts: its counters can still be cleared.
        menu.Opening += (_, _) =>
        {
            this.FillShowGroups(showGroups);
            showFrequentItem.Checked = this.showFrequent;
            showSpecialItem.Checked = this.showSpecial;
            showEmojiSubcategoriesItem.Checked = this.showEmojiSubcategories;
            showSpecialSubcategoriesItem.Checked = this.showSpecialSubcategories;
            clearFrequent.Enabled = !this.usage.IsEmpty;
            showFrenchItem.Checked = this.detailsPanel.ShowFrench;
            alwaysShowSkinToneItem.Checked = this.detailsPanel.AlwaysShowTone;
            foreach (ToolStripMenuItem item in windowOpacity.DropDownItems.OfType<ToolStripMenuItem>())
            {
                item.Checked = item == acrylicItem ? this.acrylic : !this.acrylic && (int)item.Tag! == this.opacityPercent;
            }

            acrylicVeil.Enabled = this.acrylic;
            foreach (ToolStripMenuItem item in acrylicVeil.DropDownItems)
            {
                item.Checked = (int)item.Tag! == UI.Glass.VeilPercent;
            }

            this.SetSwatch(highlightColorItem, this.detailsPanel.HighlightColor);
            this.UpdateZoomBox();
            // Read from the Startup folder each time: the user may delete the shortcut, or disable it in the Task
            // Manager.
            startWithWindowsItem.Checked = StartupShortcut.IsEnabled();
        };
        menu.Opened += (_, _) => this.tabStrip.SettingsMenuOpen = true;
        menu.Closed += (_, _) => this.tabStrip.SettingsMenuOpen = false;
        menu.Disposed += (_, _) => highlightColorItem.Image?.Dispose();
        return menu;
    }

    // The settings menu's zoom row: its label, then a numeric box with its arrows, hosted in the menu — clicking it
    // leaves the menu open. Each change applies at once: the arrows, the arrow keys and the wheel in the box, a typed
    // value on Enter or when the box loses the focus; one between two steps goes to the nearest.
    private ToolStripControlHost CreateZoomItem()
    {
        // No left margin: the label starts where the other items' texts do.
        var label = new Label
        {
            Text = EmojiZoomText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 3, 6, 3),
        };
        this.zoomBox = new PercentUpDown
        {
            Minimum = MinZoom,
            Maximum = MaxZoom,
            Increment = ZoomStep,
            Value = this.emojiZoom,
            TextAlign = HorizontalAlignment.Right,
            Anchor = AnchorStyles.Left,
        };
        this.zoomBox.ValueChanged += (_, _) =>
        {
            if (!this.settingZoomBox && this.zoomBox is PercentUpDown box)
            {
                this.SetEmojiZoom(NearestZoom((int)box.Value), anchor: null);
                // A typed value between two steps shows the step it went to.
                this.UpdateZoomBox();
            }
        };
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        row.Controls.Add(label);
        row.Controls.Add(this.zoomBox);
        return new ToolStripControlHost(row) { AutoSize = true, BackColor = Color.Transparent };
    }

    // The zoom box shows the zoom in use — Ctrl+wheel may have changed it —, at the window's DPI.
    private void UpdateZoomBox()
    {
        if (this.zoomBox is not PercentUpDown box)
        {
            return;
        }

        this.settingZoomBox = true;
        try
        {
            box.Width = this.LogicalToDeviceUnits(LogicalZoomBoxWidth);
            box.Value = this.emojiZoom;
        }
        finally
        {
            this.settingZoomBox = false;
        }
    }

    // The emojis' zoom, from the settings menu's box or Ctrl+wheel over the grid: clamped to the range, applied to the
    // grid — keeping the anchor in place, the selection without one — and the details panel, saved; its value shown
    // under the tone swatch for a moment, even at an end of the range, where the wheel changes nothing.
    private void SetEmojiZoom(int percent, Point? anchor)
    {
        percent = Math.Clamp(percent, MinZoom, MaxZoom);
        if (percent != this.emojiZoom)
        {
            this.emojiZoom = percent;
            this.grid.SetZoom(percent, anchor);
            this.detailsPanel.ZoomPercent = percent;
            SettingsFile.WriteEmojiZoom(percent);
        }

        this.detailsPanel.ShowZoom();
        // Painted now, not once the wheel stops: Windows paints only when no other message waits, and every notch of a
        // Ctrl+wheel brings one.
        this.grid.Update();
        this.detailsPanel.Update();
    }

    // The step nearest a saved zoom, which may have been written by hand, clamped to the range: 95 → 100, 450 → 400; a
    // tie goes to the step nearer 100 %: 85 → 90, 115 → 110.
    private static int NearestZoom(int percent) =>
        Enumerable.Range(0, (MaxZoom - MinZoom) / ZoomStep + 1)
            .Select(step => MinZoom + step * ZoomStep)
            .MinBy(step => (Math.Abs(step - percent), Math.Abs(step - DefaultZoom)));

    // A colour item's image becomes a swatch of the colour in use: a rounded square, outlined so a colour close to the
    // menu's background still shows, at the window's DPI. Drawn again on every opening of the menu, so it follows the
    // colour and the monitor.
    private void SetSwatch(ToolStripMenuItem item, Color color)
    {
        int size = this.LogicalToDeviceUnits(16);
        int corner = this.LogicalToDeviceUnits(6);
        var swatch = new Bitmap(size, size);
        using (Graphics graphics = Graphics.FromImage(swatch))
        using (var fill = new SolidBrush(color))
        using (var outline = new Pen(SystemColors.ControlDark))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, size - 1, size - 1);
            graphics.FillRoundedRectangle(fill, bounds, new Size(corner, corner));
            graphics.DrawRoundedRectangle(outline, bounds, new Size(corner, corner));
        }

        Image? previous = item.Image;
        item.Image = swatch;
        previous?.Dispose();
    }

    // The details panel's French row hidden or shown again, from the settings menu; saved.
    private void SetShowFrench(bool show)
    {
        if (show != this.detailsPanel.ShowFrench)
        {
            this.detailsPanel.ShowFrench = show;
            SettingsFile.WriteShowFrench(show);
        }
    }

    // The details panel's tone swatch shown whatever the selection, or only while it has variants, from the settings
    // menu; saved.
    private void SetAlwaysShowSkinTone(bool show)
    {
        if (show != this.detailsPanel.AlwaysShowTone)
        {
            this.detailsPanel.AlwaysShowTone = show;
            SettingsFile.WriteAlwaysShowSkinTone(show);
        }
    }

    // The window's opacity, from the settings menu: applied at once — the Acrylic backdrop left —, saved.
    private void SetOpacity(int percent)
    {
        if (percent != this.opacityPercent || this.acrylic)
        {
            if (this.acrylic)
            {
                this.acrylic = false;
                this.ApplyBackdrop();
            }

            this.opacityPercent = percent;
            this.Opacity = percent / 100.0;
            SettingsFile.WriteOpacity(percent);
        }
    }

    // The opacity of OpacityPercents nearest a saved one, which may have been written by hand: 97 → 98, 90 → 92.
    // MinBy keeps the first of equals, the more opaque.
    private static int NearestOpacity(int percent) =>
        OpacityPercents.MinBy(offered => Math.Abs(offered - percent));

    // Start with Windows checked or unchecked, from the settings menu: the shortcut written — to this exe, started
    // hidden — or deleted. A failure is said, the item left as it was.
    private void SetStartWithWindows(bool start)
    {
        try
        {
            if (start)
            {
                StartupShortcut.Enable(BackgroundArgument);
            }
            else
            {
                StartupShortcut.Disable();
            }
        }
        catch (Exception exception) when (StartupShortcut.IsFailure(exception))
        {
            this.ShowWarning(string.Format(StartupFailedText, exception.Message));
        }
    }

    // The colour highlighting the search's matches in the details panel, picked in Windows' colour dialog; saved once
    // changed.
    private void ChooseHighlightColor()
    {
        using var dialog = new ColorDialog { Color = this.detailsPanel.HighlightColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Color.ToArgb() != this.detailsPanel.HighlightColor.ToArgb())
        {
            this.detailsPanel.HighlightColor = dialog.Color;
            SettingsFile.WriteHighlightColor(dialog.Color);
        }
    }

    // Check for emoji updates: the latest emojibase-data on npm and the latest Twemoji on jsDelivr, each compared with the
    // version in use — or the one already downloaded. One question names what is newer; each source is then downloaded,
    // validated and written on its own — emoji-data\, flag-images\ —, a failure of one said without undoing the other,
    // then a restart is offered to use them. The flag images follow the emoji list written in the same run: a new flag
    // gets its image when Twemoji has it. The item is greyed while it runs.
    private async Task CheckEmojiUpdatesAsync(ToolStripItem item)
    {
        item.Enabled = false;
        try
        {
            Version currentData = this.downloadedEmojiDataVersion ?? this.emojiDataVersion;
            Version currentFlags = this.downloadedFlagImagesVersion ?? FlagImages.Version;
            Version latestData;
            Version latestFlags;
            try
            {
                Task<Version> data = EmojiDataUpdate.GetLatestVersionAsync();
                Task<Version> flags = FlagImagesUpdate.GetLatestVersionAsync();
                latestData = await data;
                latestFlags = await flags;
            }
            catch (Exception exception) when (EmojiDataUpdate.IsFailure(exception))
            {
                this.ShowWarning(string.Format(CheckFailedText, exception.Message));
                return;
            }

            bool dataNewer = latestData > currentData;
            bool flagsNewer = latestFlags > currentFlags;
            if (!dataNewer && !flagsNewer)
            {
                MessageBox.Show(this, string.Format(UpToDateText, currentData, currentFlags), AppTitle, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var available = new List<string>();
            if (dataNewer)
            {
                available.Add(string.Format(EmojiDataAvailableLine, latestData, currentData));
            }

            if (flagsNewer)
            {
                available.Add(string.Format(FlagImagesAvailableLine, latestFlags, currentFlags));
            }

            if (MessageBox.Show(this, string.Format(UpdateAvailableQuestion, string.Join("\n", available)), AppTitle,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            var updated = new List<string>();
            IReadOnlyList<EmojiCategory> listInUse = this.categories;
            if (dataNewer)
            {
                try
                {
                    EmojiDataFolder.FileSet files = await EmojiDataUpdate.DownloadAsync(latestData);
                    EmojiDataFolder.EmojiData data = EmojiDataFolder.Parse(files)
                        ?? throw new InvalidDataException(InvalidDownloadText);
                    EmojiDataFolder.Write(files);
                    this.downloadedEmojiDataVersion = latestData;
                    listInUse = data.Categories;
                    updated.Add($"Emojibase {latestData}");
                }
                catch (Exception exception) when (EmojiDataUpdate.IsFailure(exception)
                    || exception is IOException or UnauthorizedAccessException)
                {
                    this.ShowWarning(string.Format(UpdateFailedText, exception.Message));
                }
            }

            if (flagsNewer || updated.Count > 0)
            {
                Version target = flagsNewer ? latestFlags : currentFlags;
                try
                {
                    FlagImagesUpdate.Download download = await FlagImagesUpdate.DownloadAsync(target,
                        listInUse.SelectMany(category => category.Emojis).Select(emoji => emoji.Text));

                    // At the embedded version with nothing to add, the folder would only repeat the exe.
                    if (download.Images.Count > 0 || target > FlagImages.EmbeddedVersion)
                    {
                        FlagImages.Write(target, download.Images, download.License);
                        this.downloadedFlagImagesVersion = target;
                        updated.Add($"Twemoji flags {target}");
                    }
                }
                catch (Exception exception) when (EmojiDataUpdate.IsFailure(exception)
                    || exception is IOException or UnauthorizedAccessException)
                {
                    this.ShowWarning(string.Format(FlagImagesUpdateFailedText, exception.Message));
                }
            }

            if (updated.Count == 0)
            {
                return;
            }

            if (MessageBox.Show(this, string.Format(RestartQuestion, string.Join(", ", updated)), AppTitle,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // The app keeps running on a failure: the new emoji data is used at the next launch.
                this.Restart(RestartFailedText, exitOnFailure: false);
            }
        }
        finally
        {
            item.Enabled = true;
        }
    }

    // Reset all settings: after a confirmation listing what is lost — No the default —, every file the app wrote is
    // deleted (AppReset), then the app restarts as on its first launch. The cache stops being written first: the
    // pre-render would put an atlas back. A failed deletion is said, the restart done anyway; a failed restart exits,
    // since the app would write its old settings back from memory on the next action.
    private void ResetAllSettings()
    {
        DialogResult answer = MessageBox.Show(this,
            string.Format(ResetAllQuestion, EmojiDataFolder.EmbeddedVersion(), FlagImages.EmbeddedVersion),
            AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        this.grid.StopCacheWriting();
        IReadOnlyList<string> failures = AppReset.DeleteAll();
        if (failures.Count > 0)
        {
            this.ShowWarning(string.Format(ResetFailuresText, string.Join("\n", failures)));
        }

        this.Restart(ResetRestartFailedText, exitOnFailure: true);
    }

    // Starts the exe again with the same arguments, --title included, BackgroundArgument left out — the user is
    // looking at the window — then ends the app: not a user close, the window is not hidden to the tray. The single
    // instance is released first, or the new process would find this one and exit. Application.Restart would pass
    // BackgroundArgument along. A failure shows failureText, its {0} the reason, then ends the app or keeps it running.
    private void Restart(string failureText, bool exitOnFailure)
    {
        if (Environment.ProcessPath is not string exe)
        {
            if (exitOnFailure)
            {
                this.ShowWarning(string.Format(failureText, "the app's exe path is unknown."));
                Application.Exit();
            }

            return;
        }

        var start = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Environment.CurrentDirectory };
        foreach (string arg in Environment.GetCommandLineArgs().Skip(1))
        {
            if (!arg.Equals(BackgroundArgument, StringComparison.OrdinalIgnoreCase))
            {
                start.ArgumentList.Add(arg);
            }
        }

        this.singleInstance.Dispose();
        try
        {
            using Process? restarted = Process.Start(start);
        }
        catch (Win32Exception exception)
        {
            // Kept running, the app has no single instance any more.
            this.ShowWarning(string.Format(failureText, exception.Message));
            if (exitOnFailure)
            {
                Application.Exit();
            }

            return;
        }

        Application.Exit();
    }

    private void ShowWarning(string text) =>
        MessageBox.Show(this, text, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    // Back to the default size right away, its top-left corner where it is — moved only as far as needed to stay inside
    // the working area, a larger size grown from the corner would leave it —, and at the next launch too: the saved size
    // is removed. The window stays shown.
    private void ResetWindowSize()
    {
        SettingsFile.WriteWindowSize(null);
        this.SetClientArea(this.DefaultClientSize());
        this.inCorner = false;
        Size frameSize = this.FrameSize();
        var frame = new Rectangle(this.Left + this.frameMargins.Left, this.Top + this.frameMargins.Top, frameSize.Width, frameSize.Height);
        this.MoveFrameTo(WindowPlacement.KeepInside(frame, Screen.FromHandle(this.Handle).WorkingArea));
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

    // The special characters' tab hidden or shown again, from the settings menu; saved. Its sections leave the grid —
    // the search results too —, or come back last; the grid back at the top on its first emoji, unless a search is
    // shown.
    private void SetShowSpecial(bool show)
    {
        if (show == this.showSpecial)
        {
            return;
        }

        int replaced = this.SpecialSectionCount;
        this.showSpecial = show;
        SettingsFile.WriteShowSpecial(show);
        this.grid.ReplaceCategories(this.FirstSpecialSection, replaced, this.ShownSpecialSections());
        this.UpdateTabs();
        this.AfterSectionsReplaced();
    }

    // The catalog's sub-categories shown or hidden, from the settings menu; saved. The tabs stay as they are.
    private void SetShowEmojiSubcategories(bool show)
    {
        this.showEmojiSubcategories = show;
        SettingsFile.WriteShowEmojiSubcategories(show);
        this.grid.ReplaceCategories(this.FirstCustomSection + this.customSectionCount, this.categories.Count, this.ShownCatalogSections());
        this.AfterSectionsReplaced();
    }

    // The special characters' sub-categories shown or hidden, from the settings menu; saved — the section rebuilt while
    // shown.
    private void SetShowSpecialSubcategories(bool show)
    {
        this.showSpecialSubcategories = show;
        SettingsFile.WriteShowSpecialSubcategories(show);
        this.grid.ReplaceCategories(this.FirstSpecialSection, this.SpecialSectionCount, this.ShownSpecialSections());
        this.AfterSectionsReplaced();
    }

    // After sections came, went or changed shape: the grid back at the top on its first emoji — the search results
    // found again while a search is shown —, the active tab following.
    private void AfterSectionsReplaced()
    {
        if (string.IsNullOrWhiteSpace(this.searchBox.Text))
        {
            this.grid.ResetToTop();
        }
        else
        {
            this.OnSearchTextChanged();
        }

        this.tabStrip.ActiveTab = this.TabOf(this.grid.ActiveCategory);
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
        else if (this.IsNearMode)
        {
            // The sections before the catalog changed: the match found again where it now is.
            this.SelectNearMatch();
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

    // The tabs: the frequent one and the custom one while shown, then the catalog's, then the special characters' while
    // shown and not empty.
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
        if (this.SpecialSectionCount > 0)
        {
            tabs.Add(new EmojiCategory(SpecialHeader, SpecialIcon, []));
        }

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

    // Blank → the category view, tabs enabled. Near mode (n: / near:) → the category view too, the selection on the
    // term's first match. Otherwise → the results alone, tabs greyed.
    private void OnSearchTextChanged()
    {
        string text = this.searchBox.Text;
        string? nearTerm = EmojiSearch.NearTerm(text);
        this.clearButton.Visible = text.Length > 0;
        this.detailsPanel.SearchText = nearTerm ?? text;
        this.nearMatches = nearTerm is null ? [] : EmojiSearch.Find([.. this.ShownSpecialSections(), .. this.categories], nearTerm);
        this.nearIndex = 0;
        if (nearTerm is not null || string.IsNullOrWhiteSpace(text))
        {
            this.grid.ShowCategories();
            this.tabStrip.Greyed = false;
            this.SelectNearMatch();
        }
        else
        {
            // The special characters before the catalog: on a tie, tm gives ™ before the emoji ™️.
            this.grid.ShowSearchResults(EmojiSearch.Find([.. this.ShownSpecialSections(), .. this.categories], text));
            this.tabStrip.Greyed = true;
        }
    }

    // Whether the search box holds a near mode text (n: / near:): the categories shown, unfiltered.
    private bool IsNearMode => EmojiSearch.NearTerm(this.searchBox.Text) is not null;

    // Whether the grid shows the search results in place of the categories: a text that is neither blank nor near mode.
    private bool IsFiltering => !string.IsNullOrWhiteSpace(this.searchBox.Text) && !this.IsNearMode;

    // Near mode: the match nearIndex points at selected in its catalog or special characters' section — never a copy
    // in the frequent section or a custom group —, its row in the middle of the view. No match: the selection stays.
    private void SelectNearMatch()
    {
        if (this.nearIndex >= this.nearMatches.Count)
        {
            return;
        }

        Emoji match = this.nearMatches[this.nearIndex];
        int firstCatalog = this.FirstCustomSection + this.customSectionCount;
        for (int category = 0; category < this.categories.Count; category++)
        {
            if (IndexOf(this.categories[category].Emojis, match) is int index and >= 0)
            {
                this.grid.SelectCentered(firstCatalog + category, index);
                return;
            }
        }

        if (this.SpecialSectionCount > 0 && IndexOf(this.specialCategory.Emojis, match) is int special and >= 0)
        {
            this.grid.SelectCentered(this.FirstSpecialSection, special);
        }
    }

    // Near mode's F3 / Shift+F3: the next / previous match, wrapping around at the ends.
    private void StepNearMatch(int step)
    {
        if (this.nearMatches.Count == 0)
        {
            return;
        }

        this.nearIndex = (this.nearIndex + step + this.nearMatches.Count) % this.nearMatches.Count;
        this.SelectNearMatch();
    }

    private static int IndexOf(IReadOnlyList<Emoji> emojis, Emoji emoji)
    {
        for (int i = 0; i < emojis.Count; i++)
        {
            if (ReferenceEquals(emojis[i], emoji))
            {
                return i;
            }
        }

        return -1;
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

    // The one place told an emoji was used: its counter goes up — the frequent tab hidden too. A special character is
    // not counted.
    private void OnEmojiUsed(Emoji emoji)
    {
        if (emoji.IsText)
        {
            return;
        }

        this.usage.Record(emoji.Text);
        if (this.showFrequent)
        {
            this.grid.ReplaceCategory(0, this.CreateFrequentCategory());
        }
    }

    // The frequent section while shown, none while hidden.
    private List<EmojiCategory> CreateFrequentSections() => this.showFrequent ? [this.CreateFrequentCategory()] : [];

    // The frequent tab: the emojis used most, from the counters, each with its use count under it. One the catalog no
    // longer has is left out, and so is a special character. Its "…" button hides it.
    private EmojiCategory CreateFrequentCategory()
    {
        List<Emoji> emojis = this.usage.MostUsed().Select(text => this.emojisByText.GetValueOrDefault(text))
            .OfType<Emoji>().Where(emoji => !emoji.IsText).ToList();
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

    // The sections: the frequent one while shown, the custom ones next, then the catalog's, then the special
    // characters' while shown. The tabs alike, every custom section under the custom tab — there while it has a section
    // —, every special section under the special characters' tab.
    private int FirstCustomSection => this.showFrequent ? 1 : 0;

    private int CustomTab => this.FirstCustomSection;

    private int CustomTabCount => this.customSectionCount > 0 ? 1 : 0;

    private int FirstSpecialSection => this.FirstCustomSection + this.customSectionCount + this.categories.Count;

    private int SpecialSectionCount => this.showSpecial && this.specialCategory.Emojis.Count > 0 ? 1 : 0;

    private int SpecialTab => this.CustomTab + this.CustomTabCount + this.categories.Count;

    // The special characters' section while shown and not empty — its sub-categories while shown —, none otherwise.
    private IReadOnlyList<EmojiCategory> ShownSpecialSections() =>
        this.SpecialSectionCount == 0 ? []
        : [this.showSpecialSubcategories ? this.specialCategory : this.specialCategory with { SubCategories = null }];

    // The catalog's categories, their sub-categories while shown.
    private IReadOnlyList<EmojiCategory> ShownCatalogSections() =>
        this.showEmojiSubcategories ? this.categories : [.. this.categories.Select(category => category with { SubCategories = null })];

    // The tab of a section.
    private int TabOf(int section) =>
        section < this.FirstCustomSection ? section
        : section < this.FirstCustomSection + this.customSectionCount ? this.CustomTab
        : section >= this.FirstSpecialSection ? this.SpecialTab
        : section - this.customSectionCount + this.CustomTabCount;

    // The first section of a tab: up to the custom tab, the tab's own index — the special characters' tab's included.
    private int SectionOf(int tab) =>
        tab < this.CustomTab + this.CustomTabCount ? tab : tab - this.CustomTabCount + this.customSectionCount;

    // The group shown in a section; null for a section that is not a group's.
    private int? GroupOf(int section)
    {
        int shown = section - this.FirstCustomSection;
        return shown >= 0 && shown < this.shownGroups.Count ? this.shownGroups[shown] : null;
    }

    // Whether a section is the frequent one: the first while shown — never in search mode, whose results are the
    // first section too; near mode shows the categories, the frequent one included.
    private bool IsFrequentSection(int section) =>
        this.showFrequent && section == 0 && !this.IsFiltering;

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
        if (!this.IsFiltering && this.SectionOfGroup(this.customGroups.Groups.Count - 1) is int section)
        {
            this.grid.SelectCategory(section);
        }
    }

    // The menu of a right-clicked emoji: Use as tray icon, checked when the icon shows it; Add to ▸ every group, the
    // ones holding it checked — a click on one of those takes it out; Remove when it was right-clicked in a group,
    // Remove from frequently used in the frequent section. Opened from the keyboard, its first enabled item is
    // highlighted, as Windows does.
    private void ShowEmojiMenu(EmojiGrid.EmojiRightClick click)
    {
        var menu = new ContextMenuStrip();
        // A special character is no tray icon: its menu starts with Add to.
        if (!click.Emoji.IsText)
        {
            string shownText = this.ShownText(click.Emoji);
            var useAsTrayIcon = new ToolStripMenuItem(UseAsTrayIconText) { Checked = this.trayIcon.Emoji == shownText };
            useAsTrayIcon.Click += (_, _) => this.UseAsTrayIcon(shownText);
            menu.Items.Add(useAsTrayIcon);
            if (click.Emoji.Variants.Count > 0)
            {
                menu.Items.Add(this.CreateSkinToneMenu(click.Emoji));
                menu.Closed += (_, _) => this.SetTonePreview(null);
            }

            menu.Items.Add(new ToolStripSeparator());
        }

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
        if (click.FromKeyboard)
        {
            menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Enabled)?.Select();
        }
    }

    // Skin tone ▸, for an emoji with variants: Use default tone, checked while it has no tone of its own, then its own
    // tone — one of the six for a one-person emoji; No tone then a tone per person for a two-person one —, each tone
    // with its swatch, the emoji's own checked. The swatches go with the menu. Each item previews the tone it would
    // save while highlighted.
    private ToolStripMenuItem CreateSkinToneMenu(Emoji emoji)
    {
        SkinTonePair? own = this.skinToneChoices.Get(emoji.Text);
        var skinTone = new ToolStripMenuItem(SkinToneText);
        var useDefault = new ToolStripMenuItem(UseDefaultToneText) { Checked = own is null };
        useDefault.Click += (_, _) => this.SetOwnTone(emoji, null);
        this.PreviewWhileHighlighted(useDefault, item => new OwnTonePreview(item, emoji.Text, null));
        skinTone.DropDownItems.Add(useDefault);
        if (emoji.IsTwoPerson)
        {
            SkinTonePair yellow = SkinTonePair.Of(SkinTone.None);
            skinTone.DropDownItems.Add(this.CreateToneItem(SkinTone.None, own is { First: SkinTone.None },
                () => this.SetOwnTone(emoji, yellow), item => new OwnTonePreview(item, emoji.Text, yellow)));
            skinTone.DropDownItems.Add(new ToolStripSeparator());
            SkinTonePair shown = this.ShownTones(emoji);
            foreach ((string text, bool first) in new[] { (FirstPersonText, true), (SecondPersonText, false) })
            {
                var person = new ToolStripMenuItem(text);
                foreach (SkinTone tone in SkinTones.Modifiers)
                {
                    // The other person keeps the tone shown now, or takes this one while the emoji is yellow.
                    SkinTonePair pair = first
                        ? new SkinTonePair(tone, shown.Second == SkinTone.None ? tone : shown.Second)
                        : new SkinTonePair(shown.First == SkinTone.None ? tone : shown.First, tone);
                    bool isOwn = own is SkinTonePair ownPair && (first ? ownPair.First : ownPair.Second) == tone;
                    person.DropDownItems.Add(this.CreateToneItem(tone, isOwn, () => this.SetOwnTone(emoji, pair),
                        item => new OwnTonePreview(item, emoji.Text, pair)));
                }

                skinTone.DropDownItems.Add(person);
            }
        }
        else
        {
            skinTone.DropDownItems.Add(new ToolStripSeparator());
            foreach (SkinTone tone in SkinTones.All)
            {
                SkinTonePair tones = SkinTonePair.Of(tone);
                skinTone.DropDownItems.Add(this.CreateToneItem(tone, own?.First == tone, () => this.SetOwnTone(emoji, tones),
                    item => new OwnTonePreview(item, emoji.Text, tones)));
            }
        }

        return skinTone;
    }

    // A tone's item: its name, its swatch, checked when it is the emoji's own, previewing its tone while highlighted;
    // its swatch disposed with it.
    private ToolStripMenuItem CreateToneItem(SkinTone tone, bool isChecked, Action choose, Func<ToolStripItem, TonePreview> preview)
    {
        var item = new ToolStripMenuItem(SkinTones.NameOf(tone)) { Checked = isChecked };
        this.SetSwatch(item, SkinTones.ColorOf(tone));
        item.Click += (_, _) => choose();
        item.Disposed += (_, _) => item.Image?.Dispose();
        this.PreviewWhileHighlighted(item, preview);
        return item;
    }

    // While the item is highlighted — the mouse over it, or the arrow keys on it — the grid, the details panel's emoji
    // and, for the default tone, its swatch show the tone it would save; nothing is saved. Leaving it puts the real
    // tones back, unless the next highlighted item already previews its own.
    private void PreviewWhileHighlighted(ToolStripItem item, Func<ToolStripItem, TonePreview> preview) =>
        item.SelectedChanged += (_, _) =>
        {
            if (item.Selected)
            {
                this.SetTonePreview(preview(item));
            }
            else if (this.tonePreview?.Item == item)
            {
                this.SetTonePreview(null);
            }
        };

    // The tones previewed from now on — null, the real ones —, drawn at once; the swatch shows the previewed default
    // tone, else the real one.
    private void SetTonePreview(TonePreview? preview)
    {
        if (this.tonePreview == preview)
        {
            return;
        }

        this.tonePreview = preview;
        this.detailsPanel.DefaultTone = preview is DefaultTonePreview defaultPreview ? defaultPreview.Tone : this.skinTone;
        this.OnTonesChanged();
    }

    // The text an emoji is shown, inserted and copied as: in its own skin tone, else the default one — while a menu
    // previews a tone, the previewed default tone, or the previewed emoji's previewed own tone.
    private string ShownText(Emoji emoji) => this.tonePreview switch
    {
        DefaultTonePreview preview => SkinTones.ShownText(emoji, this.skinToneChoices.Get(emoji.Text), preview.Tone),
        OwnTonePreview preview when preview.Emoji == emoji.Text => SkinTones.ShownText(emoji, preview.Tones, this.skinTone),
        _ => SkinTones.ShownText(emoji, this.skinToneChoices.Get(emoji.Text), this.skinTone),
    };

    // A tone a skin-tone menu previews, owned by its highlighted item.
    private abstract record TonePreview(ToolStripItem Item);

    // The default tone previewed: every emoji with variants without a tone of its own.
    private sealed record DefaultTonePreview(ToolStripItem Item, SkinTone Tone) : TonePreview(Item);

    // An emoji's own tone previewed — null, Use default tone: that emoji only.
    private sealed record OwnTonePreview(ToolStripItem Item, string Emoji, SkinTonePair? Tones) : TonePreview(Item);

    // The tones of the variant an emoji is shown as; None twice when shown yellow.
    private SkinTonePair ShownTones(Emoji emoji)
    {
        string shown = this.ShownText(emoji);
        return emoji.Variants.FirstOrDefault(variant => variant.Text == shown) is SkinVariant variant
            ? new SkinTonePair(variant.First, variant.Second)
            : SkinTonePair.Of(SkinTone.None);
    }

    // The emoji's own tone, saved — null, it follows the default tone again —; the grid and the details panel follow.
    // The window stays.
    private void SetOwnTone(Emoji emoji, SkinTonePair? tones)
    {
        this.skinToneChoices.Set(emoji.Text, tones);
        this.OnTonesChanged();
    }

    // The details panel's tone swatch clicked: the six tones under it, its right edge on the swatch's, each with its
    // swatch, the default one checked, each one previewed while highlighted. Built for one show.
    private void ShowToneMenu(Rectangle bounds)
    {
        var menu = new ContextMenuStrip();
        foreach (SkinTone tone in SkinTones.All)
        {
            menu.Items.Add(this.CreateToneItem(tone, tone == this.skinTone, () => this.SetDefaultTone(tone),
                item => new DefaultTonePreview(item, tone)));
        }

        menu.Closed += (_, _) => this.SetTonePreview(null);
        ShowOnce(menu, this.detailsPanel, new Point(bounds.Right, bounds.Bottom), ToolStripDropDownDirection.BelowLeft);
    }

    // The default tone, from the details panel's tone menu; saved. The emojis with a tone of their own keep it.
    private void SetDefaultTone(SkinTone tone)
    {
        if (tone == this.skinTone)
        {
            return;
        }

        this.skinTone = tone;
        SettingsFile.WriteSkinTone(tone);
        this.detailsPanel.DefaultTone = tone;
        this.OnTonesChanged();
    }

    // Every section is drawn again in the new tones — they are pre-rendered, nothing to rebuild —, and the details
    // panel's emoji with them.
    private void OnTonesChanged()
    {
        this.grid.Invalidate();
        this.detailsPanel.RefreshShownEmoji();
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
    // hand the foreground over. The emoji is typed once that window is ready (TypeWhenReady), the keys pressed
    // meanwhile swallowed by the hook, so none lands before it. No previous window: the window hides, nothing is
    // typed. Kept open — Ctrl+Enter, Ctrl+click —: see InsertKeepingOpen. One started while another is under way is
    // ignored: its keys were swallowed anyway, a click would type into this window.
    private void InsertEmoji(Emoji emoji, bool keepOpen = false)
    {
        if (this.typingTimer.Enabled || this.handBackTimer.Enabled)
        {
            return;
        }

        if (keepOpen)
        {
            this.InsertKeepingOpen(emoji);
            return;
        }

        IntPtr target = this.foregroundTracker.PreviousWindow;
        if (target != IntPtr.Zero)
        {
            this.shortcutHook.SwallowKeys();
            EmojiInserter.Activate(target);
        }

        this.Hide();
        if (target != IntPtr.Zero)
        {
            this.TypeWhenReady(new PendingTyping(target, this.ShownText(emoji), KeepOpen: false, Environment.TickCount64));
        }

        // Counted for the emoji itself, whatever its tone.
        this.OnEmojiUsed(emoji);
    }

    // Inserts the emoji as InsertEmoji does, but the window stays open: topmost, so the previous window brought to the
    // front never covers it, the keys pressed meanwhile swallowed by the hook, so none lands in the previous window —
    // until HandForegroundBack, once the characters reached it. The keyboard, the selection and the search text stay
    // where they were. No previous window: nothing typed, the use counted.
    private void InsertKeepingOpen(Emoji emoji)
    {
        IntPtr target = this.foregroundTracker.PreviousWindow;
        if (target != IntPtr.Zero)
        {
            this.shortcutHook.SwallowKeys();
            SetWindowPos(this.Handle, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
            EmojiInserter.Activate(target);
            this.TypeWhenReady(new PendingTyping(target, this.ShownText(emoji), KeepOpen: true, Environment.TickCount64));
        }

        this.OnEmojiUsed(emoji);
    }

    // The previous window, just activated, gets the emoji once it is ready (EmojiInserter.IsReady) and TypingSettleDelay
    // has passed since — or TypingTimeout after its activation, ready or not. Activating it only queues the activation,
    // and an app gives the keyboard back to its text field later still: typed at once, the characters may be dropped.
    private void TypeWhenReady(PendingTyping typing)
    {
        this.pendingTyping = typing;
        this.typingTimer.Start();
    }

    // Then a keep-open insertion hands the foreground back after KeepOpenHandBackDelay; a hiding one lets the keys
    // through again at once. A hiding insertion whose window was shown again meanwhile — the tray icon — types nothing:
    // the emoji would land in this window.
    private void OnTypingTick()
    {
        if (this.pendingTyping is not PendingTyping typing)
        {
            this.typingTimer.Stop();
            return;
        }

        long now = Environment.TickCount64;
        if (typing.ReadyAt == 0 && EmojiInserter.IsReady(typing.Target))
        {
            typing.ReadyAt = now;
        }

        bool settled = typing.ReadyAt != 0 && now - typing.ReadyAt >= TypingSettleDelay;
        if (!settled && now - typing.ActivatedAt < TypingTimeout)
        {
            return;
        }

        this.typingTimer.Stop();
        this.pendingTyping = null;
        if (typing.KeepOpen)
        {
            EmojiInserter.Type(typing.Text);
            this.handBackTimer.Start();
            return;
        }

        if (!this.Visible)
        {
            EmojiInserter.Type(typing.Text);
        }

        this.shortcutHook.StopSwallowing();
    }

    // An insertion waiting for its previous window (see TypeWhenReady). ActivatedAt and ReadyAt are
    // Environment.TickCount64 values; ReadyAt is 0 until the window is first seen ready.
    private sealed record PendingTyping(IntPtr Target, string Text, bool KeepOpen, long ActivatedAt)
    {
        public long ReadyAt { get; set; }
    }

    // The end of a keep-open insertion: the window takes the foreground back — the focused control with it —, then
    // leaves the topmost band, still above the previous window, and the keys go through again. A window hidden
    // meanwhile stays hidden.
    private void HandForegroundBack()
    {
        this.handBackTimer.Stop();
        try
        {
            if (this.Visible)
            {
                this.TakeForeground();
            }
        }
        finally
        {
            SetWindowPos(this.Handle, HwndNoTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
            this.shortcutHook.StopSwallowing();
        }
    }

    // Hidden → shown in the corner of the mouse's monitor. Shown but covered by another window → brought to the front.
    // Shown in front → hidden.
    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        if (this.Visible && !this.IsCovered())
        {
            this.Hide();
            return;
        }

        this.ShowOrBringToFront();
    }

    // A later launch of this exe (see SingleInstance): shown and brought to the front, as the tray icon's click does it
    // when hidden or covered — never hidden, a launch is not a toggle. The launching process handed the foreground over.
    private void OnShowRequested(object? sender, EventArgs e)
    {
        this.ShowOrBringToFront();
    }

    // The tray icon's show, and a later launch's. Hidden → placed in the corner of the mouse's monitor, then shown;
    // shown but covered → brought to the front where it is.
    private void ShowOrBringToFront()
    {
        if (!this.Visible)
        {
            // Placed before being shown, so it does not appear at its old place first; placed again once shown, when
            // its frame can be read and a move to a monitor of another DPI has resized it.
            this.PlaceInCorner();
            this.Show();
            this.PlaceInCorner();
        }

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
        this.showAnchor = anchor;
        this.Show();
        this.showAnchor = null;
        this.PlaceAt(anchor);
        this.TakeForeground();
    }

    // Moves the window so its visible frame sits against the anchor, as WindowPlacement computes it.
    private void PlaceAt(Rectangle anchor)
    {
        this.inCorner = false;
        Size frameSize = this.FrameSize();
        Rectangle workingArea = Screen.FromPoint(anchor.Location).WorkingArea;
        int gap = WindowPlacement.Gap * this.DeviceDpi / 96;
        this.MoveFrameTo(WindowPlacement.Place(anchor, frameSize, workingArea, gap));
    }

    // Moves the window so its visible frame sits in the corner of the monitor holding the mouse pointer, next to its
    // notification area, as WindowPlacement computes it.
    private void PlaceInCorner()
    {
        Size frameSize = this.FrameSize();
        Screen screen = Screen.FromPoint(Cursor.Position);
        int margin = WindowPlacement.CornerMargin * this.DeviceDpi / 96;
        this.MoveFrameTo(WindowPlacement.PlaceInCorner(frameSize, screen.Bounds, screen.WorkingArea, margin));
        this.inCorner = true;
    }

    // The size of the window's visible frame: the invisible resize borders and the shadow left out. The margins between
    // the window and its frame are measured while it is shown, and kept for the placements made while it is hidden.
    private Size FrameSize()
    {
        if (this.Visible)
        {
            Rectangle frame = GetFrameBounds(this.Handle);
            this.frameMargins = new Padding(frame.Left - this.Left, frame.Top - this.Top, this.Right - frame.Right,
                this.Bottom - frame.Bottom);
        }

        return new Size(this.Width - this.frameMargins.Horizontal, this.Height - this.frameMargins.Vertical);
    }

    // Moves the window so its visible frame's top-left corner is at location, with the margins FrameSize measured.
    private void MoveFrameTo(Point location) =>
        this.Location = new Point(location.X - this.frameMargins.Left, location.Y - this.frameMargins.Top);

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

    // The app icon embedded in the exe, every size of it: Windows picks the small and the large one. The Icon reads the
    // whole stream.
    private static Icon LoadAppIcon()
    {
        const string resourceName = "EmojiSelector.AppIcon.app.ico";
        using Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
        return new Icon(stream);
    }

    private static bool IsCloaked(IntPtr window) =>
        DwmGetWindowAttribute(window, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private const uint GwHwndPrev = 3;
    private const int GwlExStyle = -20;
    private const int WsExTopmost = 0x8;
    private const int WsExToolWindow = 0x80;
    private const int WsExAppWindow = 0x40000;
    private const int WsCaption = 0xC00000;
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;
    private const int EmSetCueBanner = 0x1501;
    private const int WmChar = 0x0102;
    private const int WmActivate = 0x0006;
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoMove = 0x2;
    private const uint SwpNoZOrder = 0x4;
    private const uint SwpNoActivate = 0x10;
    private const uint SwpFrameChanged = 0x20;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);

    // The search bar sits at the window's top: its top band lets the hit test through to the window, which answers it
    // as the top resize border.
    private sealed class SearchBar : TableLayoutPanel
    {
        // Drawn on the Acrylic backdrop (see Glass): a tinted transparent background.
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Glass
        {
            get => this.glass;
            set
            {
                this.glass = value;
                this.Invalidate();
            }
        }

        private bool glass;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (this.glass)
            {
                UI.Glass.Paint(e, _ => { });
            }
            else
            {
                base.OnPaintBackground(e);
            }
        }

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

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
