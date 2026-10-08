using System.Drawing.Drawing2D;
using EmojiSelector.Data;
using EmojiSelector.Drawing;

namespace EmojiSelector.UI;

/// <summary>
/// Every emoji in <b>one continuous scrolling grid</b>, one section per <b>category</b> under its header (see
/// <see cref="EmojiGridLayout"/>). The emojis' bitmaps come from an <see cref="EmojiBitmapCache"/>, pre-rendered in
/// the background: a cell whose emoji is not ready yet is filled with <see cref="MissingColor"/>. One emoji is the
/// <b>selection</b>, framed in the accent colour: the mouse moving over an emoji selects it, the keyboard moves it
/// (<see cref="MoveSelection"/>), and <see cref="SelectedEmojiChanged"/> tells the details panel. A click raises
/// <see cref="EmojiClicked"/>.
/// While the search box holds text, the sections give way to one <c>Search results</c> section
/// (<see cref="ShowSearchResults"/>), until <see cref="ShowCategories"/> brings them back where they were.
/// <para>
/// A right click on an emoji raises <see cref="EmojiRightClicked"/>. A section with a menu — a <b>custom group</b>'s, the
/// frequent one — has a "…" button at the right end of its header, raising <see cref="SectionMenuClicked"/>. A group's
/// <b>reorder mode</b> (<see cref="StartReorder"/>) turns the button into <see cref="DoneText"/>: its emojis are dragged
/// and dropped inside it (<see cref="EmojiMoved"/>), and neither a click nor Enter inserts them, until
/// <see cref="EndReorder"/>.
/// </para>
/// </summary>
internal sealed class EmojiGrid : Control
{
    public const string SearchResultsHeader = "Search results";

    public const string NoResultText = "No emoji found";

    public const string DoneText = "Done";

    private const string MenuText = "…";

    // The insertion marker shown while an emoji is dragged, in logical pixels.
    private const int LogicalInsertionWidth = 3;

    // In logical pixels (96 DPI), scaled to the control's DPI.
    private const int LogicalCellSize = 40;
    private const int LogicalEmojiSize = 28;
    private const int LogicalHeaderHeight = 32;
    private const int LogicalPadding = 8;
    private const int LogicalSelectionWidth = 2;

    // A captioned section's cells: as wide as the others, taller to hold the caption under the emoji. The emoji's
    // distance from the top of its cell, and the caption's font size in points.
    private const int LogicalCaptionedCellHeight = 54;
    private const int LogicalCaptionedEmojiTop = 4;
    private const float CaptionFontSize = 8.25F;

    // One notch of the mouse wheel scrolls this many rows.
    private const int RowsPerWheelNotch = 2;

    private readonly List<EmojiCategory> categories;
    private readonly VScrollBar scrollBar = new() { Dock = DockStyle.Right };
    private readonly EmojiBitmapCache bitmaps;

    // A cell whose emoji is not pre-rendered yet: loud on purpose, so a missing emoji never passes for an empty slot.
    private static readonly Color MissingColor = Color.FromArgb(0x39, 0xFF, 0x14);

    // The sections shown: the categories, or the search results alone.
    private IReadOnlyList<EmojiCategory> sections;

    // While searching, the scroll offset the category view comes back to; null in the category view.
    private int? categoriesOffset;

    private EmojiGridLayout layout;
    private Font headerFont;
    private Font captionFont;
    private (int Section, int Index)? hovered;
    private (int Section, int Index)? selection;

    // The emoji SelectedEmojiChanged last told of: the same cell may show another emoji once the sections change.
    private Emoji? reportedEmoji;

    // Where the cursor was last seen, on the screen: a mouse message at the same place is not a move.
    private Point cursorPosition;
    private int activeCategory;

    // The section in reorder mode, null when none. While the left button is down on one of its emojis: that emoji's
    // index and where it was pressed; once the mouse moved far enough to drag it, where it would be dropped.
    private int? reorderSection;
    private int? dragFrom;
    private Point dragStart;
    private (int Index, Point Gap)? insertion;

    // The section whose header button is under the mouse, and the one pressed: -1 when none.
    private int hoveredHeaderButton = -1;
    private int pressedHeaderButton = -1;

    // The left button was released after pressing a header button or an emoji being dragged: the click following it
    // inserts nothing.
    private bool ignoreClick;

    /// <param name="categories">The sections, in order.</param>
    /// <param name="emojis">
    /// Every emoji the sections can show, each once, in grid order: the ones pre-rendered. A section built from others
    /// (the frequent emojis) leaves this list — and so the disk cache's key — unchanged.
    /// </param>
    public EmojiGrid(IReadOnlyList<EmojiCategory> categories, IEnumerable<Emoji> emojis)
    {
        this.categories = [.. categories];
        this.sections = this.categories;
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.Selectable, false);
        this.BackColor = SystemColors.Window;
        this.headerFont = new Font(this.Font, FontStyle.Bold);
        this.captionFont = new Font(this.Font.FontFamily, CaptionFontSize);
        this.scrollBar.ValueChanged += (_, _) => this.OnScrolled();
        this.Controls.Add(this.scrollBar);
        this.layout = this.CreateLayout();
        this.UpdateScrollBar();

        // In grid order: the first screen is ready first.
        this.bitmaps = new EmojiBitmapCache(emojis.Select(emoji => emoji.Text).ToList());
        this.bitmaps.BitmapsReady += (_, _) => this.Invalidate();
        this.EnsureBitmapSize();
    }

    /// <summary>An emoji was clicked — not one of the section in reorder mode.</summary>
    public event EventHandler<Emoji>? EmojiClicked;

    /// <summary>An emoji was right-clicked.</summary>
    public event EventHandler<EmojiRightClick>? EmojiRightClicked;

    /// <summary>A section's "…" button was clicked.</summary>
    public event EventHandler<SectionMenuRequest>? SectionMenuClicked;

    /// <summary>An emoji of the section in reorder mode was dragged to another place in it.</summary>
    public event EventHandler<EmojiMove>? EmojiMoved;

    /// <summary>The selected emoji changed — to null included: see <see cref="SelectedEmoji"/>.</summary>
    public event EventHandler? SelectedEmojiChanged;

    /// <summary>
    /// The scroll brought another category's section to the top: see <see cref="ActiveCategory"/>. Not raised while
    /// the search results are shown.
    /// </summary>
    public event EventHandler? ActiveCategoryChanged;

    /// <summary>The category whose tab is active: the section at the top of the viewport.</summary>
    public int ActiveCategory => this.activeCategory;

    /// <summary>The selected emoji — the one Enter inserts; null when the grid shows no emoji.</summary>
    public Emoji? SelectedEmoji => this.selection is (int section, int index) ? this.sections[section].Emojis[index] : null;

    /// <summary>Whether a section is in reorder mode.</summary>
    public bool IsReordering => this.reorderSection is not null;

    /// <summary>Whether the selected emoji belongs to the section in reorder mode: Enter does not insert it.</summary>
    public bool IsSelectionReordered => this.reorderSection is int section && this.selection?.Section == section;

    private bool IsSearching => this.categoriesOffset is not null;

    private bool IsDragging => this.insertion is not null;

    private int Offset => this.scrollBar.Value;

    private int MaxOffset => Math.Max(0, this.layout.ContentHeight - this.ViewportHeight);

    private int ViewportHeight => Math.Max(1, this.ClientSize.Height);

    private int EmojiSize => this.LogicalToDeviceUnits(LogicalEmojiSize);

    /// <summary>
    /// The client size holding exactly <paramref name="columns"/> columns, and a section's header followed by
    /// <paramref name="rows"/> full rows when that section is scrolled to the top — device pixels, at the current DPI.
    /// </summary>
    public Size SizeFor(int columns, int rows)
    {
        int cellSize = this.LogicalToDeviceUnits(LogicalCellSize);
        return new Size(
            columns * cellSize + 2 * this.LogicalToDeviceUnits(LogicalPadding) + this.scrollBar.Width,
            this.LogicalToDeviceUnits(LogicalHeaderHeight) + rows * cellSize);
    }

    /// <summary>Scrolls <paramref name="category"/>'s header to the top of the viewport and selects its first emoji.</summary>
    public void SelectCategory(int category)
    {
        this.SetOffset(this.layout.HeaderTop(category));
        this.SetSelection(this.layout.FirstOf(category), ensureVisible: false);
    }

    /// <summary>Scrolls back to the top and selects the first emoji: the state the window opens in.</summary>
    public void ResetToTop()
    {
        // The window may appear under the cursor: where it stands is no move.
        this.cursorPosition = Cursor.Position;
        this.SetOffset(0);
        this.SetSelection(this.layout.First(), ensureVisible: false);
    }

    /// <summary>
    /// Asks for the selected emoji's menu, as a right click on it would — the Menu key, Shift+F10: its cell scrolled
    /// into view, the menu at the cell's bottom-left corner. Nothing when no emoji is selected.
    /// </summary>
    public void OpenSelectionMenu()
    {
        if (this.selection is not (int section, int index))
        {
            return;
        }

        this.SetSelection(this.selection, ensureVisible: true);
        Rectangle cell = this.layout.CellBounds(section, index);
        var location = new Point(cell.Left, cell.Bottom - this.Offset);
        this.EmojiRightClicked?.Invoke(this, new EmojiRightClick(section, this.sections[section].Emojis[index], location, FromKeyboard: true));
    }

    /// <summary>
    /// Whether <paramref name="keyData"/> is one of the keys <see cref="MoveSelection"/> answers: arrows, Home / End
    /// and their Ctrl variants, Page Up / Page Down, Tab / Shift+Tab.
    /// </summary>
    public static bool IsNavigationKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End
            or (Keys.Control | Keys.Home) or (Keys.Control | Keys.End) or Keys.PageUp or Keys.PageDown
            or Keys.Tab or (Keys.Shift | Keys.Tab);

    /// <summary>
    /// Moves the selection for a navigation key — arrows, Home / End and their Ctrl variants, Page Up / Page Down,
    /// Tab / Shift+Tab — and scrolls it into view. Returns false when the key is not one of them, and for ↑ on the
    /// grid's first row, which has nothing above it: the caller hands the keyboard back to the search box.
    /// </summary>
    public bool MoveSelection(Keys keyData)
    {
        if (keyData is Keys.Tab or (Keys.Shift | Keys.Tab))
        {
            // The categories are greyed while searching: Tab has none to go to.
            if (!this.IsSearching && this.sections.Count > 0)
            {
                int current = this.selection?.Section ?? this.activeCategory;
                int step = keyData == Keys.Tab ? 1 : this.sections.Count - 1;
                this.SelectCategory((current + step) % this.sections.Count);
            }

            return true;
        }

        if (this.selection is not (int, int) cell)
        {
            bool isNavigation = IsNavigationKey(keyData);
            if (isNavigation)
            {
                this.SetSelection(this.layout.First(), ensureVisible: true);
            }

            return isNavigation;
        }

        int pageRows = Math.Max(1, this.ViewportHeight / this.layout.CellSize);
        (int Section, int Index)? target;
        switch (keyData)
        {
            case Keys.Left:
                target = this.layout.Previous(cell);
                break;
            case Keys.Right:
                target = this.layout.Next(cell);
                break;
            case Keys.Up:
                target = this.layout.Above(cell);
                if (target is null)
                {
                    return false;
                }

                break;
            case Keys.Down:
                target = this.layout.Below(cell);
                break;
            case Keys.Home:
                target = this.layout.FirstOf(cell.Section);
                break;
            case Keys.End:
                target = this.layout.LastOf(cell.Section);
                break;
            case Keys.Control | Keys.Home:
                target = this.layout.First();
                break;
            case Keys.Control | Keys.End:
                target = this.layout.Last();
                break;
            case Keys.PageUp:
                target = Repeat(cell, pageRows, this.layout.Above);
                break;
            case Keys.PageDown:
                target = Repeat(cell, pageRows, this.layout.Below);
                break;
            default:
                return false;
        }

        // At a grid edge the target is null: the selection stays where it is.
        if (target is not null)
        {
            this.SetSelection(target, ensureVisible: true);
        }

        return true;
    }

    /// <summary>
    /// Shows <paramref name="results"/>, in their order, as one <c>Search results</c> section scrolled to the top;
    /// the first call keeps the category view's scroll position for <see cref="ShowCategories"/>.
    /// </summary>
    public void ShowSearchResults(IReadOnlyList<Emoji> results)
    {
        this.EndReorder();
        this.categoriesOffset ??= this.Offset;
        this.sections = [new EmojiCategory(SearchResultsHeader, ' ', results, EmptyText: NoResultText)];
        this.Relayout();
        this.SetOffset(0);
        this.SetSelection(this.layout.First(), ensureVisible: false);
    }

    /// <summary>
    /// Replaces the section of <paramref name="index"/> — its tab stays the same. The sections below it move with
    /// its height; while searching, the change shows when the categories come back. The selection stays on its
    /// emoji: wherever it moved in the replaced section, on its cell in another one — the view then moving with the
    /// change of height, so the emoji keeps its place on screen, under the mouse for a Ctrl+click. Gone from the
    /// replaced section, it goes back to the first emoji in view.
    /// </summary>
    public void ReplaceCategory(int index, EmojiCategory category)
    {
        (int Section, int Index)? before = this.selection;
        Emoji? selected = this.SelectedEmoji;
        int? selectedTop = before is (int section, int cell) ? this.layout.CellBounds(section, cell).Top : null;
        this.categories[index] = category;
        if (this.IsSearching)
        {
            return;
        }

        int offset = this.Offset;
        this.Relayout();
        if (before is (int kept, int keptIndex) && kept != index && selectedTop is int top)
        {
            this.SetOffset(offset + this.layout.CellBounds(kept, keptIndex).Top - top);
            this.SetSelection(before, ensureVisible: false);
            return;
        }

        this.SetSelection(this.CellOf(index, selected) ?? this.FirstVisible(), ensureVisible: false);
    }

    // The cell of emoji in the section of index, while the section shows it — the frequent section is cut to its rows.
    private (int Section, int Index)? CellOf(int index, Emoji? emoji)
    {
        if (emoji is null || this.layout.LastOf(index) is not (_, int last))
        {
            return null;
        }

        IReadOnlyList<Emoji> emojis = this.sections[index].Emojis;
        for (int i = 0; i <= last; i++)
        {
            if (emojis[i].Text == emoji.Text)
            {
                return (index, i);
            }
        }

        return null;
    }

    /// <summary>
    /// Replaces the <paramref name="count"/> sections from <paramref name="index"/> with <paramref name="replacement"/>
    /// — the custom groups' sections, the frequent one hidden or shown. The view stays on what it showed: when it was
    /// below the replaced sections, it moves with their change of height. The selection goes back to the first emoji in view;
    /// the reorder mode stays on its section while that one still has a menu.
    /// </summary>
    public void ReplaceCategories(int index, int count, IReadOnlyList<EmojiCategory> replacement)
    {
        EmojiGridLayout before = this.IsSearching ? this.CreateLayout(this.categories) : this.layout;
        int below = index + count;
        int? anchor = below < before.SectionCount ? before.HeaderTop(below) : null;
        this.categories.RemoveRange(index, count);
        this.categories.InsertRange(index, replacement);
        if (this.reorderSection is int reordered && (reordered >= this.categories.Count || !this.categories[reordered].HasMenu))
        {
            this.EndReorder();
        }

        EmojiGridLayout after = this.CreateLayout(this.categories);
        int shift = anchor is int top && index + replacement.Count < after.SectionCount
            ? after.HeaderTop(index + replacement.Count) - top
            : 0;
        if (this.IsSearching)
        {
            if (this.categoriesOffset >= anchor)
            {
                this.categoriesOffset += shift;
            }

            return;
        }

        int offset = this.Offset;
        this.Relayout();
        if (offset >= anchor)
        {
            this.SetOffset(offset + shift);
        }

        this.SetSelection(this.FirstVisible(), ensureVisible: false);
    }

    /// <summary>
    /// Turns the reorder mode on for <paramref name="section"/>, a section with a menu: its "…" button becomes
    /// <see cref="DoneText"/>, its emojis are dragged and dropped instead of inserted.
    /// </summary>
    public void StartReorder(int section)
    {
        if (!this.IsSearching && this.sections[section].HasMenu)
        {
            this.EndDrag();
            this.reorderSection = section;
            this.Invalidate();
        }
    }

    /// <summary>Turns the reorder mode off — <see cref="DoneText"/>, Esc, the window hiding; nothing when it is off.</summary>
    public void EndReorder()
    {
        if (this.reorderSection is not null)
        {
            this.EndDrag();
            this.reorderSection = null;
            this.Invalidate();
        }
    }

    /// <summary>
    /// The pre-rendered emojis no longer written to the disk cache — still shown. Once it returns, no write is under
    /// way: the cache folder can be deleted.
    /// </summary>
    public void StopCacheWriting() => this.bitmaps.StopWriting();

    /// <summary>Brings the categories back, at the scroll position they had before the search.</summary>
    public void ShowCategories()
    {
        if (this.categoriesOffset is not int offset)
        {
            return;
        }

        this.categoriesOffset = null;
        this.sections = this.categories;
        this.Relayout();
        this.SetOffset(offset);
        this.SetSelection(this.FirstVisible(), ensureVisible: false);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        int offset = this.Offset;
        int width = this.ClientSize.Width - this.scrollBar.Width;

        for (int section = 0; section < this.layout.SectionCount; section++)
        {
            var header = new Rectangle(this.layout.Padding, this.layout.HeaderTop(section) - offset,
                width - 2 * this.layout.Padding, this.layout.HeaderHeight);
            if (header.IntersectsWith(e.ClipRectangle))
            {
                // The name ends before the header's button: its ellipsis never runs under it.
                Rectangle name = header;
                if (this.HeaderButtonBounds(section) is Rectangle button)
                {
                    name.Width = Math.Max(0, button.Left - header.Left);
                    this.PaintHeaderButton(graphics, section, button);
                }

                TextRenderer.DrawText(graphics, this.sections[section].Name, this.headerFont, name, SystemColors.ControlText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            // An empty section's message fills the row the layout keeps for it, under the header.
            if (this.sections[section] is { Emojis.Count: 0, EmptyText: string emptyText })
            {
                var message = new Rectangle(this.layout.Padding, header.Bottom, width - 2 * this.layout.Padding, this.layout.RowHeight(section));
                TextRenderer.DrawText(graphics, emptyText, this.Font, message, SystemColors.GrayText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        int emojiSize = this.EmojiSize;
        foreach ((int section, int index) in this.layout.CellsIn(offset + e.ClipRectangle.Top, offset + e.ClipRectangle.Bottom))
        {
            Rectangle cell = this.layout.CellBounds(section, index);
            cell.Offset(0, -offset);
            Bitmap? bitmap = this.bitmaps.TryGet(this.sections[section].Emojis[index].Text);
            if (bitmap is null)
            {
                using var missingBrush = new SolidBrush(MissingColor);
                graphics.FillRectangle(missingBrush, Rectangle.Inflate(cell, -1, -1));
            }
            else if (this.sections[section].Captions is IReadOnlyList<string> captions)
            {
                // A captioned emoji sits at the top of its cell, its caption in the band left below it.
                int emojiY = cell.Y + this.LogicalToDeviceUnits(LogicalCaptionedEmojiTop);
                graphics.DrawImage(bitmap, cell.X + (cell.Width - emojiSize) / 2, emojiY, emojiSize, emojiSize);
                var caption = new Rectangle(cell.X, emojiY + emojiSize, cell.Width, cell.Bottom - emojiY - emojiSize);
                TextRenderer.DrawText(graphics, captions[index], this.captionFont, caption, SystemColors.GrayText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            else
            {
                graphics.DrawImage(bitmap, cell.X + (cell.Width - emojiSize) / 2, cell.Y + (cell.Height - emojiSize) / 2, emojiSize, emojiSize);
            }

            // The frame follows the cell: a rectangle around a captioned emoji and its count.
            if (this.selection == (section, index))
            {
                using var pen = new Pen(SystemColors.Highlight, this.LogicalToDeviceUnits(LogicalSelectionWidth)) { Alignment = PenAlignment.Inset };
                Rectangle frame = Rectangle.Inflate(cell, -1, -1);
                graphics.DrawRectangle(pen, frame.X, frame.Y, frame.Width - 1, frame.Height - 1);
            }
        }

        // Where the dragged emoji would be dropped: a bar in the gap between two cells.
        if (this.insertion is (_, Point gap) && this.reorderSection is int reordered)
        {
            int barWidth = this.LogicalToDeviceUnits(LogicalInsertionWidth);
            using var bar = new SolidBrush(SystemColors.Highlight);
            graphics.FillRectangle(bar, gap.X - barWidth / 2, gap.Y - offset, barWidth, this.layout.RowHeight(reordered));
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        this.Relayout();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        this.EnsureBitmapSize();
        this.Relayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // The handle may be created on a monitor of another DPI than the one the constructor assumed.
        this.EnsureBitmapSize();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Font previous = this.headerFont;
        this.headerFont = new Font(this.Font, FontStyle.Bold);
        previous.Dispose();
        Font previousCaption = this.captionFont;
        this.captionFont = new Font(this.Font.FontFamily, CaptionFontSize);
        previousCaption.Dispose();
        this.Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        this.SetOffset(this.Offset - e.Delta * RowsPerWheelNotch * this.layout.CellSize / SystemInformation.MouseWheelScrollDelta);
    }

    // Only a real move selects: Windows also sends a mouse move when the content scrolls or the window appears under a
    // still mouse, and neither may hand the selection to the emoji under it.
    // While an emoji is dragged, the mouse only moves the insertion marker: no hover, no selection.
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (this.dragFrom is not null && this.reorderSection is int reordered)
        {
            Size dragSize = SystemInformation.DragSize;
            if (this.IsDragging || Math.Abs(e.X - this.dragStart.X) > dragSize.Width / 2
                || Math.Abs(e.Y - this.dragStart.Y) > dragSize.Height / 2)
            {
                this.insertion = this.layout.Insertion(reordered, new Point(e.X, e.Y + this.Offset));
                this.Invalidate();
                return;
            }
        }

        Point position = this.PointToScreen(e.Location);
        if (position == this.cursorPosition)
        {
            return;
        }

        this.cursorPosition = position;
        this.SetHovered(e.Location);
        if (this.hovered is not null)
        {
            this.SetSelection(this.hovered, ensureVisible: false);
        }
    }

    // The selection stays: the keyboard still has one to act on.
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        this.SetHovered(null);
    }

    // A press on a header button, or on an emoji of the section in reorder mode: the start of a drag.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        this.pressedHeaderButton = this.HeaderButtonAt(e.Location);
        if (this.pressedHeaderButton >= 0)
        {
            this.Invalidate();
        }
        else if (this.reorderSection is int reordered && this.HitTest(e.Location) is (int section, int index) && section == reordered)
        {
            this.dragFrom = index;
            this.dragStart = e.Location;
            this.Capture = true;
        }
    }

    // A header button acts when released over the one pressed; a drag drops its emoji. The right button asks for the
    // emoji's menu.
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right)
        {
            if (this.HitTest(e.Location) is (int section, int index))
            {
                this.EmojiRightClicked?.Invoke(this, new EmojiRightClick(section, this.sections[section].Emojis[index], e.Location));
            }

            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // A press that began on a header button or a dragged emoji is no click, wherever it is released.
        this.ignoreClick = this.pressedHeaderButton >= 0 || this.dragFrom is not null;
        int pressed = this.pressedHeaderButton;
        if (pressed >= 0)
        {
            this.pressedHeaderButton = -1;
            this.Invalidate();
            if (this.HeaderButtonAt(e.Location) == pressed)
            {
                this.OnHeaderButtonClicked(pressed);
            }

            return;
        }

        if (this.dragFrom is int from && this.insertion is (int at, _) && this.reorderSection is int reordered)
        {
            // Dropped before itself or just after itself: it stays where it is.
            int to = at > from ? at - 1 : at;
            this.EndDrag();
            if (to != from)
            {
                this.EmojiMoved?.Invoke(this, new EmojiMove(reordered, from, to));
            }

            return;
        }

        this.EndDrag();
    }

    // The emojis of the section in reorder mode are dragged, never inserted.
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        bool ignore = this.ignoreClick;
        this.ignoreClick = false;
        if (e.Button == MouseButtons.Left && !ignore && this.HitTest(e.Location) is (int section, int index) && section != this.reorderSection)
        {
            this.EmojiClicked?.Invoke(this, this.sections[section].Emojis[index]);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.bitmaps.Dispose();
            this.headerFont.Dispose();
            this.captionFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private EmojiGridLayout CreateLayout() => this.CreateLayout(this.sections);

    private EmojiGridLayout CreateLayout(IReadOnlyList<EmojiCategory> sections) => new(
        sections.Select(section => new EmojiGridLayout.Section(
            section.Emojis.Count, MinRows: section.EmptyText is null ? 0 : 1, section.MaxRows,
            section.Captions is null ? null : this.LogicalToDeviceUnits(LogicalCaptionedCellHeight))).ToList(),
        this.ClientSize.Width - this.scrollBar.Width,
        this.ViewportHeight,
        this.LogicalToDeviceUnits(LogicalCellSize),
        this.LogicalToDeviceUnits(LogicalHeaderHeight),
        this.LogicalToDeviceUnits(LogicalPadding));

    private void Relayout()
    {
        this.layout = this.CreateLayout();
        this.UpdateScrollBar();
        this.Invalidate();
    }

    // The scroll bar's largest value is Maximum - LargeChange + 1: the offset showing the end of the content.
    private void UpdateScrollBar()
    {
        this.scrollBar.Minimum = 0;
        this.scrollBar.Maximum = this.layout.ContentHeight - 1;
        this.scrollBar.LargeChange = this.ViewportHeight;
        this.scrollBar.SmallChange = this.layout.CellSize;
        if (this.scrollBar.Value > this.MaxOffset)
        {
            this.scrollBar.Value = this.MaxOffset;
        }

        this.OnScrolled();
    }

    private void SetOffset(int offset)
    {
        this.scrollBar.Value = Math.Clamp(offset, 0, this.MaxOffset);
    }

    private void OnScrolled()
    {
        if (this.IsHandleCreated)
        {
            this.SetHovered(this.PointToClient(Cursor.Position));
        }

        this.Invalidate();

        int active = this.layout.SectionAt(this.Offset);
        if (!this.IsSearching && active != this.activeCategory)
        {
            this.activeCategory = active;
            this.ActiveCategoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private (int Section, int Index)? HitTest(Point location)
    {
        if (!this.ClientRectangle.Contains(location) || location.X >= this.ClientSize.Width - this.scrollBar.Width)
        {
            return null;
        }

        return this.layout.HitTest(new Point(location.X, location.Y + this.Offset));
    }

    private void SetSelection((int Section, int Index)? cell, bool ensureVisible)
    {
        if (ensureVisible && cell is (int section, int index))
        {
            Rectangle bounds = this.layout.CellBounds(section, index);
            if (bounds.Top < this.Offset)
            {
                this.SetOffset(bounds.Top);
            }
            else if (bounds.Bottom > this.Offset + this.ViewportHeight)
            {
                this.SetOffset(bounds.Bottom - this.ViewportHeight);
            }
        }

        if (cell != this.selection)
        {
            this.selection = cell;
            this.Invalidate();
        }

        Emoji? selected = this.SelectedEmoji;
        if (!ReferenceEquals(selected, this.reportedEmoji))
        {
            this.reportedEmoji = selected;
            this.SelectedEmojiChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // The first emoji whose cell is entirely in view; the first one partly in view otherwise.
    private (int Section, int Index)? FirstVisible()
    {
        (int Section, int Index)? partly = null;
        foreach ((int section, int index) in this.layout.CellsIn(this.Offset, this.Offset + this.ViewportHeight))
        {
            if (this.layout.CellBounds(section, index).Top >= this.Offset)
            {
                return (section, index);
            }

            partly ??= (section, index);
        }

        return partly ?? this.layout.First();
    }

    private static (int Section, int Index) Repeat((int Section, int Index) cell, int times,
        Func<(int Section, int Index), (int Section, int Index)?> step)
    {
        for (int i = 0; i < times && step(cell) is (int, int) next; i++)
        {
            cell = next;
        }

        return cell;
    }

    private void SetHovered(Point? location)
    {
        int headerButton = location is Point buttonPoint ? this.HeaderButtonAt(buttonPoint) : -1;
        if (headerButton != this.hoveredHeaderButton)
        {
            this.hoveredHeaderButton = headerButton;
            this.Invalidate();
        }

        (int Section, int Index)? hit = location is Point point ? this.HitTest(point) : null;
        if (hit == this.hovered)
        {
            return;
        }

        this.hovered = hit;
        this.Invalidate();
    }

    // The button at the right end of a section's header, client coordinates: "…" as wide as the header is high,
    // Done as wide as its text. Null for a section without a menu.
    private Rectangle? HeaderButtonBounds(int section)
    {
        if (!this.sections[section].HasMenu)
        {
            return null;
        }

        int width = section == this.reorderSection
            ? TextRenderer.MeasureText(DoneText, this.headerFont).Width + 2 * this.layout.Padding
            : this.layout.HeaderHeight;
        Rectangle bounds = this.layout.HeaderButton(section, width);
        bounds.Offset(0, -this.Offset);
        return bounds;
    }

    // The section whose header button is under location, client coordinates; -1 when none.
    private int HeaderButtonAt(Point location)
    {
        if (location.X >= this.ClientSize.Width - this.scrollBar.Width)
        {
            return -1;
        }

        for (int section = 0; section < this.layout.SectionCount; section++)
        {
            if (this.HeaderButtonBounds(section) is Rectangle bounds && bounds.Contains(location))
            {
                return section;
            }
        }

        return -1;
    }

    // "…" grey like the tab glyphs, Done in the accent colour; the tab strip's hover behind either.
    private void PaintHeaderButton(Graphics graphics, int section, Rectangle bounds)
    {
        if (section == this.hoveredHeaderButton || section == this.pressedHeaderButton)
        {
            using var hover = new SolidBrush(SystemColors.ControlLight);
            graphics.FillRectangle(hover, Rectangle.Inflate(bounds, 0, -2));
        }

        bool done = section == this.reorderSection;
        TextRenderer.DrawText(graphics, done ? DoneText : MenuText, this.headerFont, bounds,
            done ? SystemColors.Highlight : SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    // Done ends the reorder mode; "…" asks for the section's menu, shown under the button.
    private void OnHeaderButtonClicked(int section)
    {
        if (section == this.reorderSection)
        {
            this.EndReorder();
        }
        else if (this.HeaderButtonBounds(section) is Rectangle bounds)
        {
            this.SectionMenuClicked?.Invoke(this, new SectionMenuRequest(section, bounds));
        }
    }

    private void EndDrag()
    {
        this.dragFrom = null;
        if (this.insertion is not null)
        {
            this.insertion = null;
            this.Invalidate();
        }

        this.Capture = false;
    }

    // Pre-renders the emojis at the current DPI's size, unless that is already the size being pre-rendered.
    private void EnsureBitmapSize()
    {
        if (this.bitmaps.Size != this.EmojiSize)
        {
            this.bitmaps.Start(this.EmojiSize);
            this.Invalidate();
        }
    }

    /// <summary>
    /// An emoji right-clicked: its section, the emoji, and where, in the grid's coordinates. From the keyboard — the
    /// Menu key, Shift+F10 — the selected emoji, under its cell.
    /// </summary>
    public readonly record struct EmojiRightClick(int Section, Emoji Emoji, Point Location, bool FromKeyboard = false);

    /// <summary>A section's "…" button clicked: the section, and the button's bounds in the grid's coordinates.</summary>
    public readonly record struct SectionMenuRequest(int Section, Rectangle ButtonBounds);

    /// <summary>
    /// An emoji dragged inside the section in reorder mode: its index before the move, and its index after it.
    /// </summary>
    public readonly record struct EmojiMove(int Section, int From, int To);
}
