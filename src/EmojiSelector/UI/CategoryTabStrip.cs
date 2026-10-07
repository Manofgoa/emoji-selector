using System.ComponentModel;
using EmojiSelector.Data;

namespace EmojiSelector.UI;

/// <summary>
/// The strip of <b>category</b> tabs above the <see cref="EmojiGrid"/>, like the Win+; panel's: one monochrome
/// glyph per category, grey, the active one in the accent colour and underlined, the category's name as a tooltip.
/// A click raises <see cref="TabClicked"/>. Drawn with GDI: a monochrome icon font needs no Direct2D.
/// While the search box holds text the strip is <see cref="Greyed"/>.
/// <para>
/// The window has no caption: right of the tabs, the strip holds the <b>drag area</b>, an empty band moving the window
/// (<see cref="IsDragArea"/>), the <b>settings button</b> raising <see cref="SettingsClicked"/>, then the <b>close
/// cross</b> raising <see cref="CloseClicked"/>. The two buttons are never greyed.
/// </para>
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
    private const int LogicalButtonWidth = 46;
    private const int LogicalCloseIconSize = 10;
    private const int LogicalMinimumDragWidth = 24;

    private const char CloseGlyph = '\uE8BB';
    private const char SettingsGlyph = '\uE713';
    public const string CloseText = "Close";
    public const string SettingsText = "Settings";

    // A press on the settings button this soon after its menu closed is the press that closed it: not a new click.
    private const int MenuClosingPressMilliseconds = 250;

    // Windows' own close button: red on hover, a lighter red pressed, the glyph white on both.
    private static readonly Color CloseHoverColor = Color.FromArgb(0xC4, 0x2B, 0x1C);
    private static readonly Color ClosePressedColor = Color.FromArgb(0xC7, 0x49, 0x3C);

    private readonly IReadOnlyList<EmojiCategory> categories;
    private readonly ToolTip toolTip = new();
    private Font iconFont;
    private Font closeFont;
    private int activeTab;
    private int hoveredTab = -1;
    private StripButton hoveredButton;
    private StripButton pressedButton;
    private bool greyed;
    private bool settingsMenuOpen;
    // When the settings menu last closed under a press on the button; null when it did not.
    private long? settingsMenuClosedByPressAt;

    public CategoryTabStrip(IReadOnlyList<EmojiCategory> categories)
    {
        this.categories = categories;
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.Selectable, false);
        this.BackColor = SystemColors.Window;
        this.Height = this.LogicalToDeviceUnits(LogicalHeight);
        this.iconFont = this.CreateIconFont(LogicalIconSize);
        this.closeFont = this.CreateIconFont(LogicalCloseIconSize);
    }

    private enum StripButton
    {
        None,
        Settings,
        Close,
    }

    /// <summary>A tab was clicked: its index, the category's.</summary>
    public event EventHandler<int>? TabClicked;

    /// <summary>The close cross was clicked: pressed and released over it.</summary>
    public event EventHandler? CloseClicked;

    /// <summary>The settings button was clicked: its bounds, in the strip's coordinates, to show the menu under it.</summary>
    public event EventHandler<Rectangle>? SettingsClicked;

    /// <summary>The narrowest the strip can be with every tab, a drag area and the buttons, in logical pixels.</summary>
    public int LogicalMinimumWidth =>
        LogicalPadding + this.categories.Count * LogicalTabWidth + LogicalMinimumDragWidth + 2 * LogicalButtonWidth;

    /// <summary>
    /// Whether the settings menu is open: the button stays drawn as hovered meanwhile. Closed by a press on the button
    /// itself, that press does not open it again.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SettingsMenuOpen
    {
        get => this.settingsMenuOpen;
        set
        {
            if (value == this.settingsMenuOpen)
            {
                return;
            }

            this.settingsMenuOpen = value;
            if (!value && (MouseButtons & MouseButtons.Left) != 0
                && this.SettingsBounds.Contains(this.PointToClient(MousePosition)))
            {
                this.settingsMenuClosedByPressAt = Environment.TickCount64;
            }

            this.Invalidate();
        }
    }

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
                this.SetHovered(-1, this.hoveredButton);
                this.Invalidate();
            }
        }
    }

    private int TabWidth => this.LogicalToDeviceUnits(LogicalTabWidth);

    private int LeftPadding => this.LogicalToDeviceUnits(LogicalPadding);

    private int ButtonWidth => this.LogicalToDeviceUnits(LogicalButtonWidth);

    private Rectangle CloseBounds => new(this.Width - this.ButtonWidth, 0, this.ButtonWidth, this.Height);

    private Rectangle SettingsBounds => new(this.Width - 2 * this.ButtonWidth, 0, this.ButtonWidth, this.Height);

    /// <summary>
    /// Whether <paramref name="location"/>, in the strip's coordinates, lies in the drag area: between the last tab
    /// and the settings button. The window answers it as its caption, so Windows moves the window and opens its system
    /// menu.
    /// </summary>
    public bool IsDragArea(Point location) =>
        location.Y >= 0 && location.Y < this.Height
        && location.X >= this.TabBounds(this.categories.Count - 1).Right && location.X < this.SettingsBounds.Left;

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

        this.PaintSettings(e.Graphics);
        this.PaintClose(e.Graphics);

        using var separator = new Pen(SystemColors.ControlLight);
        e.Graphics.DrawLine(separator, 0, this.Height - 1, this.Width, this.Height - 1);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Font previous = this.iconFont;
        Font previousClose = this.closeFont;
        this.iconFont = this.CreateIconFont(LogicalIconSize);
        this.closeFont = this.CreateIconFont(LogicalCloseIconSize);
        previous.Dispose();
        previousClose.Dispose();
        this.Height = this.LogicalToDeviceUnits(LogicalHeight);
        this.Invalidate();
    }

    // The drag area and the window's top resize band let the hit test through to the window, which answers them.
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WindowFrame.WmNcHitTest
            && (WindowFrame.IsInTopResizeBand(this, m.LParam)
                || this.IsDragArea(this.PointToClient(WindowFrame.HitTestPoint(m.LParam)))))
        {
            m.Result = WindowFrame.HtTransparent;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        this.SetHovered(this.greyed ? -1 : this.HitTest(e.Location), this.ButtonAt(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        this.SetHovered(-1, StripButton.None);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            StripButton button = this.ButtonAt(e.Location);
            bool closedTheMenu = Environment.TickCount64 - this.settingsMenuClosedByPressAt < MenuClosingPressMilliseconds;
            this.pressedButton = button == StripButton.Settings && closedTheMenu ? StripButton.None : button;
            this.settingsMenuClosedByPressAt = null;
            this.Invalidate();
        }
    }

    // A button acts when released over the one pressed, like Windows' caption buttons.
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        StripButton pressed = this.pressedButton;
        if (e.Button != MouseButtons.Left || pressed == StripButton.None)
        {
            return;
        }

        this.pressedButton = StripButton.None;
        this.Invalidate();
        if (this.ButtonAt(e.Location) != pressed)
        {
            return;
        }

        if (pressed == StripButton.Close)
        {
            this.CloseClicked?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            this.SettingsClicked?.Invoke(this, this.SettingsBounds);
        }
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
            this.closeFont.Dispose();
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
        return tab < this.categories.Count && location.X < this.SettingsBounds.Left ? tab : -1;
    }

    private StripButton ButtonAt(Point location) =>
        this.CloseBounds.Contains(location) ? StripButton.Close
        : this.SettingsBounds.Contains(location) ? StripButton.Settings
        : StripButton.None;

    private void SetHovered(int tab, StripButton button)
    {
        if (tab == this.hoveredTab && button == this.hoveredButton)
        {
            return;
        }

        this.hoveredTab = tab;
        this.hoveredButton = button;
        string? tip = button switch
        {
            StripButton.Close => CloseText,
            StripButton.Settings => SettingsText,
            _ => tab >= 0 ? this.categories[tab].Name : null,
        };
        this.toolTip.SetToolTip(this, tip);
        this.Invalidate();
    }

    // Grey like an inactive tab, with the tabs' hover — kept while its menu is open.
    private void PaintSettings(Graphics graphics)
    {
        Rectangle bounds = this.SettingsBounds;
        if (this.hoveredButton == StripButton.Settings || this.pressedButton == StripButton.Settings || this.settingsMenuOpen)
        {
            using var hover = new SolidBrush(SystemColors.ControlLight);
            graphics.FillRectangle(hover, Rectangle.Inflate(bounds, -2, -4));
        }

        TextRenderer.DrawText(graphics, SettingsGlyph.ToString(), this.iconFont, bounds, SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }

    // Grey like an inactive tab; on hover, Windows' own red with a white glyph.
    private void PaintClose(Graphics graphics)
    {
        Rectangle bounds = this.CloseBounds;
        Color glyph = SystemColors.GrayText;
        if (this.hoveredButton == StripButton.Close || this.pressedButton == StripButton.Close)
        {
            Color fill = this.pressedButton == StripButton.Close ? ClosePressedColor : CloseHoverColor;
            using var background = new SolidBrush(fill);
            graphics.FillRectangle(background, bounds);
            glyph = Color.White;
        }

        TextRenderer.DrawText(graphics, CloseGlyph.ToString(), this.closeFont, bounds, glyph,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }

    // Halfway between two colours: a greyed glyph, between the inactive grey and the background.
    private static Color Blend(Color color, Color background) => Color.FromArgb(
        (color.R + background.R) / 2, (color.G + background.G) / 2, (color.B + background.B) / 2);

    // Pixels, not points: the glyph keeps its size whatever the font settings, scaled to the control's DPI only.
    private Font CreateIconFont(int logicalSize)
    {
        string family = IconFonts.FirstOrDefault(name => FontFamily.Families.Any(installed => installed.Name == name))
            ?? IconFonts[^1];
        return new Font(family, this.LogicalToDeviceUnits(logicalSize), GraphicsUnit.Pixel);
    }
}
