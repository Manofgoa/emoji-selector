using System.ComponentModel;
using EmojiSelector.Data;

namespace EmojiSelector.UI;

/// <summary>
/// The strip of <b>category</b> tabs above the <see cref="EmojiGrid"/>, like the Win+; panel's: one monochrome
/// glyph per category, grey, the active one in the accent colour and underlined, the category's name as a tooltip.
/// A click raises <see cref="TabClicked"/>. Drawn with GDI: a monochrome icon font needs no Direct2D.
/// While the search box holds text the strip is <see cref="Greyed"/>.
/// </summary>
internal sealed class CategoryTabStrip : Control
{
    // Windows 11's icon font, and Windows 10's: the same code points.
    private static readonly string[] IconFonts = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];

    // In logical pixels (96 DPI), scaled to the control's DPI.
    private const int LogicalTabWidth = 44;
    private const int LogicalHeight = 40;
    private const int LogicalIconSize = 16;
    private const int LogicalUnderlineWidth = 16;
    private const int LogicalUnderlineHeight = 3;
    private const int LogicalPadding = 4;

    private readonly IReadOnlyList<EmojiCategory> categories;
    private readonly ToolTip toolTip = new();
    private Font iconFont;
    private int activeTab;
    private int hoveredTab = -1;
    private bool greyed;

    public CategoryTabStrip(IReadOnlyList<EmojiCategory> categories)
    {
        this.categories = categories;
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.Selectable, false);
        this.BackColor = SystemColors.Window;
        this.Height = this.LogicalToDeviceUnits(LogicalHeight);
        this.iconFont = this.CreateIconFont();
    }

    /// <summary>A tab was clicked: its index, the category's.</summary>
    public event EventHandler<int>? TabClicked;

    /// <summary>The active tab: the category at the top of the grid.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ActiveTab
    {
        get => this.activeTab;
        set
        {
            if (value != this.activeTab)
            {
                this.activeTab = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>
    /// Greyed while searching: every glyph paler than an inactive tab's, no active tab, no hover, no tooltip, clicks
    /// ignored. The built-in <see cref="Control.Enabled"/> does not grey custom drawing.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Greyed
    {
        get => this.greyed;
        set
        {
            if (value != this.greyed)
            {
                this.greyed = value;
                this.SetHovered(-1);
                this.Invalidate();
            }
        }
    }

    private int TabWidth => this.LogicalToDeviceUnits(LogicalTabWidth);

    private int LeftPadding => this.LogicalToDeviceUnits(LogicalPadding);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Color accent = SystemColors.Highlight;
        Color greyedColor = Blend(SystemColors.GrayText, this.BackColor);
        int active = this.greyed ? -1 : this.activeTab;
        for (int tab = 0; tab < this.categories.Count; tab++)
        {
            Rectangle bounds = this.TabBounds(tab);
            if (tab == this.hoveredTab)
            {
                using var hover = new SolidBrush(SystemColors.ControlLight);
                e.Graphics.FillRectangle(hover, Rectangle.Inflate(bounds, -2, -4));
            }

            Color color = this.greyed ? greyedColor : tab == active ? accent : SystemColors.GrayText;
            TextRenderer.DrawText(e.Graphics, this.categories[tab].Icon.ToString(), this.iconFont, bounds, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            if (tab == active)
            {
                int underlineWidth = this.LogicalToDeviceUnits(LogicalUnderlineWidth);
                int underlineHeight = this.LogicalToDeviceUnits(LogicalUnderlineHeight);
                using var underline = new SolidBrush(accent);
                e.Graphics.FillRectangle(underline, bounds.X + (bounds.Width - underlineWidth) / 2,
                    bounds.Bottom - underlineHeight - 2, underlineWidth, underlineHeight);
            }
        }

        using var separator = new Pen(SystemColors.ControlLight);
        e.Graphics.DrawLine(separator, 0, this.Height - 1, this.Width, this.Height - 1);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Font previous = this.iconFont;
        this.iconFont = this.CreateIconFont();
        previous.Dispose();
        this.Height = this.LogicalToDeviceUnits(LogicalHeight);
        this.Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        this.SetHovered(this.greyed ? -1 : this.HitTest(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        this.SetHovered(-1);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int tab = this.HitTest(e.Location);
        if (e.Button == MouseButtons.Left && tab >= 0 && !this.greyed)
        {
            this.TabClicked?.Invoke(this, tab);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.toolTip.Dispose();
            this.iconFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private Rectangle TabBounds(int tab) => new(this.LeftPadding + tab * this.TabWidth, 0, this.TabWidth, this.Height);

    private int HitTest(Point location)
    {
        if (location.X < this.LeftPadding || location.Y < 0 || location.Y >= this.Height)
        {
            return -1;
        }

        int tab = (location.X - this.LeftPadding) / this.TabWidth;
        return tab < this.categories.Count ? tab : -1;
    }

    private void SetHovered(int tab)
    {
        if (tab == this.hoveredTab)
        {
            return;
        }

        this.hoveredTab = tab;
        this.toolTip.SetToolTip(this, tab >= 0 ? this.categories[tab].Name : null);
        this.Invalidate();
    }

    // Halfway between two colours: a greyed glyph, between the inactive grey and the background.
    private static Color Blend(Color color, Color background) => Color.FromArgb(
        (color.R + background.R) / 2, (color.G + background.G) / 2, (color.B + background.B) / 2);

    // Pixels, not points: the glyph keeps its size whatever the font settings, scaled to the control's DPI only.
    private Font CreateIconFont()
    {
        string family = IconFonts.FirstOrDefault(name => FontFamily.Families.Any(installed => installed.Name == name))
            ?? IconFonts[^1];
        return new Font(family, this.LogicalToDeviceUnits(LogicalIconSize), GraphicsUnit.Pixel);
    }
}
