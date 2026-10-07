using EmojiSelector.Data;
using EmojiSelector.Drawing;

namespace EmojiSelector.UI;

/// <summary>
/// Every emoji in <b>one continuous scrolling grid</b>, one section per <b>category</b> under its header (see
/// <see cref="EmojiGridLayout"/>). Each emoji is drawn in colour once, at its size, then reused. The hovered cell is
/// highlighted and the emoji's name shown as a tooltip; a click raises <see cref="EmojiClicked"/>.
/// </summary>
internal sealed class EmojiGrid : Control
{
    // In logical pixels (96 DPI), scaled to the control's DPI.
    private const int LogicalCellSize = 40;
    private const int LogicalEmojiSize = 28;
    private const int LogicalHeaderHeight = 32;
    private const int LogicalPadding = 8;

    // One notch of the mouse wheel scrolls this many rows.
    private const int RowsPerWheelNotch = 2;

    private readonly IReadOnlyList<EmojiCategory> categories;
    private readonly VScrollBar scrollBar = new() { Dock = DockStyle.Right };
    private readonly ToolTip toolTip = new();
    private readonly EmojiRenderer renderer = new();

    // The emojis drawn so far, at the current emoji size.
    private readonly Dictionary<string, Bitmap> bitmaps = [];

    private EmojiGridLayout layout;
    private Font headerFont;
    private (int Section, int Index)? hovered;
    private int activeCategory;

    public EmojiGrid(IReadOnlyList<EmojiCategory> categories)
    {
        this.categories = categories;
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.Selectable, false);
        this.BackColor = SystemColors.Window;
        this.headerFont = new Font(this.Font, FontStyle.Bold);
        this.scrollBar.ValueChanged += (_, _) => this.OnScrolled();
        this.Controls.Add(this.scrollBar);
        this.layout = this.CreateLayout();
        this.UpdateScrollBar();
    }

    /// <summary>An emoji was clicked.</summary>
    public event EventHandler<Emoji>? EmojiClicked;

    /// <summary>The scroll brought another category's section to the top: see <see cref="ActiveCategory"/>.</summary>
    public event EventHandler? ActiveCategoryChanged;

    /// <summary>The category whose tab is active: the section at the top of the viewport.</summary>
    public int ActiveCategory => this.activeCategory;

    private int Offset => this.scrollBar.Value;

    private int MaxOffset => Math.Max(0, this.layout.ContentHeight - this.ViewportHeight);

    private int ViewportHeight => Math.Max(1, this.ClientSize.Height);

    private int EmojiSize => this.LogicalToDeviceUnits(LogicalEmojiSize);

    /// <summary>Scrolls <paramref name="category"/>'s header to the top of the viewport.</summary>
    public void ScrollToCategory(int category)
    {
        this.SetOffset(this.layout.HeaderTop(category));
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
                TextRenderer.DrawText(graphics, this.categories[section].Name, this.headerFont, header, SystemColors.ControlText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        int emojiSize = this.EmojiSize;
        foreach ((int section, int index) in this.layout.CellsIn(offset + e.ClipRectangle.Top, offset + e.ClipRectangle.Bottom))
        {
            Rectangle cell = this.layout.CellBounds(section, index);
            cell.Offset(0, -offset);
            if (this.hovered == (section, index))
            {
                using var brush = new SolidBrush(SystemColors.ControlLight);
                graphics.FillRectangle(brush, Rectangle.Inflate(cell, -1, -1));
            }

            Bitmap bitmap = this.GetBitmap(this.categories[section].Emojis[index].Text, emojiSize);
            graphics.DrawImage(bitmap, cell.X + (cell.Width - emojiSize) / 2, cell.Y + (cell.Height - emojiSize) / 2, emojiSize, emojiSize);
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
        this.ClearBitmaps();
        this.Relayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Font previous = this.headerFont;
        this.headerFont = new Font(this.Font, FontStyle.Bold);
        previous.Dispose();
        this.Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        this.SetOffset(this.Offset - e.Delta * RowsPerWheelNotch * this.layout.CellSize / SystemInformation.MouseWheelScrollDelta);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        this.SetHovered(e.Location);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        this.SetHovered(null);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && this.HitTest(e.Location) is (int section, int index))
        {
            this.EmojiClicked?.Invoke(this, this.categories[section].Emojis[index]);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.ClearBitmaps();
            this.renderer.Dispose();
            this.toolTip.Dispose();
            this.headerFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private EmojiGridLayout CreateLayout() => new(
        this.categories.Select(category => category.Emojis.Count).ToList(),
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
        if (active != this.activeCategory)
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

    private void SetHovered(Point? location)
    {
        (int Section, int Index)? hit = location is Point point ? this.HitTest(point) : null;
        if (hit == this.hovered)
        {
            return;
        }

        this.hovered = hit;
        this.toolTip.SetToolTip(this, hit is (int section, int index) ? this.categories[section].Emojis[index].Name : null);
        this.Invalidate();
    }

    private Bitmap GetBitmap(string emoji, int size)
    {
        if (!this.bitmaps.TryGetValue(emoji, out Bitmap? bitmap))
        {
            bitmap = this.renderer.Render(emoji, size);
            this.bitmaps[emoji] = bitmap;
        }

        return bitmap;
    }

    private void ClearBitmaps()
    {
        foreach (Bitmap bitmap in this.bitmaps.Values)
        {
            bitmap.Dispose();
        }

        this.bitmaps.Clear();
    }
}
