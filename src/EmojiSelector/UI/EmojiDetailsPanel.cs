using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using EmojiSelector.Data;
using EmojiSelector.Drawing;

namespace EmojiSelector.UI;

/// <summary>
/// The <b>details panel</b>, under the grid, showing the grid's selection (<see cref="ShownEmoji"/>): the emoji drawn
/// large, its emoticons under it; one row per language — its flag, the name, then every tag, wrapped, never cut —, the
/// English row always, the French one while <see cref="ShowFrench"/>; at the right, a copy button copying the emoji to
/// the clipboard, its first code point as a tooltip. While the search box holds text (<see cref="SearchText"/>), the
/// characters the search matches are highlighted in <see cref="HighlightColor"/>.
/// <para>
/// The emoji is drawn and copied as its <see cref="ShownText"/>: in the skin tone in use. While it has skin-tone
/// variants, a <b>tone bar</b> under the copy button shows the six tones, the <see cref="DefaultTone"/> ringed; a click
/// raises <see cref="DefaultToneChosen"/>. A two-person emoji gets a second bar under it, the second person's
/// <see cref="SecondTone"/> — its first slot "same as the first person" —, raising <see cref="SecondToneChosen"/>;
/// dimmed and inactive while the default tone is <see cref="SkinTone.None"/>.
/// </para>
/// <para>
/// Its height is fixed: the one the emoji with the most text needs at the panel's width (<see cref="HeightFor"/>), set
/// by the parent's layout (<see cref="FitHeight"/>), so moving the selection never moves the grid. Drawn with GDI, the
/// emoji with its own <see cref="EmojiRenderer"/>: the grid's cache renders at the grid's size only.
/// </para>
/// </summary>
internal sealed class EmojiDetailsPanel : Control
{
    /// <summary>The highlight colour while the user chose none: fluorescent yellow.</summary>
    public static readonly Color DefaultHighlightColor = Color.FromArgb(0xFF, 0xFF, 0x00);

    // Windows 11's icon font, and Windows 10's: the same code points.
    private static readonly string[] IconFonts = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];

    // A thin edge around the flags: the French flag's white band would melt into the background.
    private static readonly Color FlagEdgeColor = Color.FromArgb(0x40, 0, 0, 0);

    // In logical pixels (96 DPI), scaled to the control's DPI.
    private const int LogicalPadding = 8;
    private const int LogicalEmojiSize = 48;
    private const int LogicalButtonSize = 28;
    private const int LogicalButtonIconSize = 16;
    private const int LogicalFlagHeight = 12;
    private const int LogicalUsFlagWidth = 23;
    private const int LogicalFrenchFlagWidth = 18;
    private const int LogicalFlagGap = 6;
    private const int LogicalTagsGap = 2;
    private const int LogicalRowGap = 6;

    // The tone bars: three swatches a row, two rows; the gap above each bar; the ring around the tone in use.
    private const int LogicalSwatchSize = 12;
    private const int LogicalSwatchGap = 4;
    private const int LogicalBarGap = 6;
    private const int LogicalRingWidth = 2;
    private const int SwatchesPerRow = 3;

    // A dimmed bar's swatches, out of 255.
    private const int DimmedAlpha = 80;

    // The emoticons' font size in points, the grid's captions'.
    private const float EmoticonFontSize = 8.25F;

    private const char CopyGlyph = '';
    private const char CheckGlyph = '';

    // How long the check mark replaces the copy glyph after a copy.
    private const int CopiedMilliseconds = 1000;

    private const string TagSeparator = ", ";

    private const string DefaultToneToolTip = "Default skin tone: {0}";
    private const string SecondToneToolTip = "Second person: {0}";
    private const string SameToneToolTip = "Second person: same as the first";

    private const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

    private readonly IReadOnlyList<Emoji> catalog;

    // Whether the catalog has emojis with variants, and two-person ones: the room the tone bars take, whatever the
    // selection.
    private readonly bool hasVariants;
    private readonly bool hasTwoPerson;
    private readonly EmojiRenderer renderer = new();
    private readonly ToolTip toolTip = new();
    private readonly System.Windows.Forms.Timer copiedTimer = new() { Interval = CopiedMilliseconds };
    private readonly Flag usFlag = new("us", LogicalUsFlagWidth);
    private readonly Flag frenchFlag = new("fr", LogicalFrenchFlagWidth);

    // A word's width, in the name font (true) or the tag font (false): measured once, the words repeat across emojis.
    private readonly Dictionary<(bool IsName, string Word), int> wordWidths = [];

    // A font's line height, measured once: the height is computed over the whole catalog.
    private readonly Dictionary<Font, int> lineHeights = [];

    private Font nameFont;
    private Font emoticonFont;
    private Font iconFont;
    private Emoji? shownEmoji;
    private Bitmap? emojiBitmap;
    private string searchText = "";
    private bool showFrench = true;
    private Color highlightColor = DefaultHighlightColor;
    private SkinTone defaultTone;
    private SkinTone? secondTone;

    // The part under the mouse, and the one the left button went down on.
    private Hit hovered;
    private Hit pressed;
    private bool copied;

    // The width FitHeight last fitted the height to: -1 to fit it again.
    private int heightWidth = -1;

    /// <param name="catalog">Every emoji the panel may show: the height fits the one with the most text.</param>
    public EmojiDetailsPanel(IReadOnlyList<Emoji> catalog)
    {
        this.catalog = catalog;
        this.hasVariants = catalog.Any(emoji => emoji.Variants.Count > 0);
        this.hasTwoPerson = catalog.Any(emoji => emoji.IsTwoPerson);
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.Selectable, false);
        this.BackColor = SystemColors.Window;
        this.nameFont = new Font(this.Font, FontStyle.Bold);
        this.emoticonFont = new Font(this.Font.FontFamily, EmoticonFontSize);
        this.iconFont = this.CreateIconFont();
        this.copiedTimer.Tick += (_, _) => this.EndCopied();
    }

    /// <summary>The emoji shown: the grid's selection; null shows an empty panel.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Emoji? ShownEmoji
    {
        get => this.shownEmoji;
        set
        {
            if (ReferenceEquals(value, this.shownEmoji))
            {
                return;
            }

            this.shownEmoji = value;
            this.EndCopied();
            this.RenderEmoji();

            // The bars come and go with the emoji: the part under a still mouse may be another.
            this.hovered = this.IsHandleCreated ? this.HitTest(this.PointToClient(Cursor.Position)) : default;
            this.UpdateToolTip();
            this.Invalidate();
        }
    }

    /// <summary>The search box's text: the characters it matches are highlighted; blank highlights nothing.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SearchText
    {
        get => this.searchText;
        set
        {
            if (value != this.searchText)
            {
                this.searchText = value;
                this.Invalidate();
            }
        }
    }

    /// <summary>Whether the French row is shown under the English one.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowFrench
    {
        get => this.showFrench;
        set
        {
            if (value != this.showFrench)
            {
                this.showFrench = value;
                this.RequestHeight();
                this.Invalidate();
            }
        }
    }

    /// <summary>The colour behind the characters the search matches.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HighlightColor
    {
        get => this.highlightColor;
        set
        {
            this.highlightColor = value;
            this.Invalidate();
        }
    }

    /// <summary>The text the shown emoji is drawn and copied as: in the skin tone in use. The emoji itself until set.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Emoji, string> ShownText { get; set; } = emoji => emoji.Text;

    /// <summary>The default skin tone, ringed in the tone bar.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SkinTone DefaultTone
    {
        get => this.defaultTone;
        set
        {
            this.defaultTone = value;
            this.Invalidate();
        }
    }

    /// <summary>The second person's default skin tone, ringed in the second bar; null, the same as the first person's.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SkinTone? SecondTone
    {
        get => this.secondTone;
        set
        {
            this.secondTone = value;
            this.Invalidate();
        }
    }

    /// <summary>A swatch of the tone bar was clicked: the tone it shows.</summary>
    public event EventHandler<SkinTone>? DefaultToneChosen;

    /// <summary>
    /// A swatch of the second bar was clicked: the tone it shows — null for "same as the first person", or for the tone
    /// the first person already has.
    /// </summary>
    public event EventHandler<SkinTone?>? SecondToneChosen;

    /// <summary>The shown emoji drawn again: its <see cref="ShownText"/> changed with a tone.</summary>
    public void RefreshShownEmoji()
    {
        this.EndCopied();
        this.RenderEmoji();
        this.Invalidate();
    }

    private int PanelPadding => this.LogicalToDeviceUnits(LogicalPadding);

    private int EmojiSize => this.LogicalToDeviceUnits(LogicalEmojiSize);

    private int ButtonSize => this.LogicalToDeviceUnits(LogicalButtonSize);

    // Where the rows start: right of the emoji.
    private int TextLeft => this.PanelPadding + this.EmojiSize + this.PanelPadding;

    // A name starts after the widest flag: the names of both rows line up.
    private int NameIndent => this.LogicalToDeviceUnits(LogicalUsFlagWidth + LogicalFlagGap);

    private Rectangle ButtonBounds => new(this.Width - this.PanelPadding - this.ButtonSize, this.PanelPadding, this.ButtonSize, this.ButtonSize);

    private int SwatchSize => this.LogicalToDeviceUnits(LogicalSwatchSize);

    private int SwatchGap => this.LogicalToDeviceUnits(LogicalSwatchGap);

    private int BarGap => this.LogicalToDeviceUnits(LogicalBarGap);

    private int BarWidth => SwatchesPerRow * this.SwatchSize + (SwatchesPerRow - 1) * this.SwatchGap;

    private int BarHeight => 2 * this.SwatchSize + this.SwatchGap;

    // The copy button, then the tone bars under it when the catalog has variants: as wide as the widest.
    private int RightColumnWidth => this.hasVariants ? Math.Max(this.ButtonSize, this.BarWidth) : this.ButtonSize;

    private int RightColumnHeight => this.ButtonSize
        + (this.hasVariants ? this.BarGap + this.BarHeight : 0)
        + (this.hasTwoPerson ? this.BarGap + this.BarHeight : 0);

    // Whether the shown emoji has its tone bar, and its second one.
    private bool ShowsBar => this.shownEmoji is { Variants.Count: > 0 };

    private bool ShowsSecondBar => this.shownEmoji is { IsTwoPerson: true };

    // The second bar answers only while the default tone gives the first person one.
    private bool SecondBarActive => this.defaultTone != SkinTone.None;

    /// <summary>
    /// The panel's height at <paramref name="width"/>: the emoji with the most text, the French row included while
    /// shown, fits, and so do the copy button and the tone bars — device pixels, at the current DPI.
    /// </summary>
    public int HeightFor(int width)
    {
        int textWidth = this.TextWidthFor(width);
        int text = this.catalog.Count == 0 ? 0 : this.catalog.Max(emoji => this.TextHeight(emoji, textWidth));
        int emojiColumn = this.EmojiSize + (this.catalog.Any(emoji => emoji.Emoticons.Count > 0) ? this.LineHeight(this.emoticonFont) : 0);
        return 2 * this.PanelPadding + Math.Max(Math.Max(emojiColumn, this.RightColumnHeight), text);
    }

    /// <summary>
    /// Sets the height for <paramref name="width"/>, the parent's client width, unless it was already set for it. Called
    /// by the parent before it lays its controls out: set during the layout, the height would come too late for it.
    /// </summary>
    public void FitHeight(int width)
    {
        if (width > 0 && width != this.heightWidth)
        {
            this.heightWidth = width;
            this.Height = this.HeightFor(width);
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        this.Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Font previousName = this.nameFont;
        Font previousEmoticon = this.emoticonFont;
        this.nameFont = new Font(this.Font, FontStyle.Bold);
        this.emoticonFont = new Font(this.Font.FontFamily, EmoticonFontSize);
        previousName.Dispose();
        previousEmoticon.Dispose();
        this.wordWidths.Clear();
        this.lineHeights.Clear();
        this.RequestHeight();
        this.Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Font previousIcon = this.iconFont;
        this.iconFont = this.CreateIconFont();
        previousIcon.Dispose();
        this.wordWidths.Clear();
        this.lineHeights.Clear();
        this.RenderEmoji();
        this.RequestHeight();
        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;

        // The line between the grid and the panel.
        using (var border = new Pen(SystemColors.ControlLight))
        {
            graphics.DrawLine(border, 0, 0, this.Width, 0);
        }

        if (this.shownEmoji is not Emoji emoji)
        {
            return;
        }

        int padding = this.PanelPadding;
        if (this.emojiBitmap is Bitmap bitmap)
        {
            graphics.DrawImage(bitmap, padding, padding, this.EmojiSize, this.EmojiSize);
        }

        if (emoji.Emoticons.Count > 0)
        {
            var emoticons = new Rectangle(0, padding + this.EmojiSize, this.TextLeft, this.LineHeight(this.emoticonFont));
            TextRenderer.DrawText(graphics, string.Join(" ", emoji.Emoticons), this.emoticonFont, emoticons, SystemColors.GrayText,
                TextFlags | TextFormatFlags.HorizontalCenter);
        }

        int textWidth = this.TextWidthFor(this.Width);
        int y = padding;
        bool first = true;
        foreach (Row row in this.RowsOf(emoji))
        {
            if (!first)
            {
                int rowGap = this.LogicalToDeviceUnits(LogicalRowGap);
                using var separator = new Pen(SystemColors.ControlLight);
                graphics.DrawLine(separator, this.TextLeft, y + rowGap, this.TextLeft + textWidth, y + rowGap);
                y += 2 * rowGap + 1;
            }

            first = false;
            int nameLineHeight = this.LineHeight(this.nameFont);
            this.PaintFlag(graphics, row.Flag, this.TextLeft, y + (nameLineHeight - this.LogicalToDeviceUnits(LogicalFlagHeight)) / 2);
            y = this.PaintText(graphics, row.Name, isName: true, this.TextLeft + this.NameIndent, y, textWidth - this.NameIndent);
            if (row.Tags.Length > 0)
            {
                y += this.LogicalToDeviceUnits(LogicalTagsGap);
                y = this.PaintText(graphics, row.Tags, isName: false, this.TextLeft, y, textWidth);
            }
        }

        this.PaintButton(graphics);
        if (this.ShowsBar)
        {
            this.PaintBar(graphics, Part.FirstBar);
        }

        if (this.ShowsSecondBar)
        {
            this.PaintBar(graphics, Part.SecondBar);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        this.SetHovered(this.HitTest(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        this.SetHovered(default);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && this.hovered.Part != Part.None)
        {
            this.pressed = this.hovered;
            this.Invalidate(this.BoundsOf(this.pressed));
        }
    }

    // The button and the swatches act when released over the part pressed.
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || this.pressed.Part == Part.None)
        {
            return;
        }

        Hit pressed = this.pressed;
        this.pressed = default;
        this.Invalidate(this.BoundsOf(pressed));
        if (this.HitTest(e.Location) != pressed)
        {
            return;
        }

        switch (pressed.Part)
        {
            case Part.Button:
                this.CopyEmoji();
                break;
            case Part.FirstBar:
                this.DefaultToneChosen?.Invoke(this, SkinTones.All[pressed.Slot]);
                break;
            case Part.SecondBar:
                SkinTone? tone = pressed.Slot == 0 || SkinTones.All[pressed.Slot] == this.defaultTone ? null : SkinTones.All[pressed.Slot];
                this.SecondToneChosen?.Invoke(this, tone);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.renderer.Dispose();
            this.toolTip.Dispose();
            this.copiedTimer.Dispose();
            this.usFlag.Dispose();
            this.frenchFlag.Dispose();
            this.emojiBitmap?.Dispose();
            this.nameFont.Dispose();
            this.emoticonFont.Dispose();
            this.iconFont.Dispose();
        }

        base.Dispose(disposing);
    }

    // The rows shown: English, then French while shown.
    private IEnumerable<Row> RowsOf(Emoji emoji)
    {
        yield return new Row(this.usFlag, emoji.Name, string.Join(TagSeparator, emoji.EnglishTags));
        if (this.showFrench)
        {
            yield return new Row(this.frenchFlag, emoji.FrenchName, string.Join(TagSeparator, emoji.FrenchTags));
        }
    }

    // Between the emoji and the button's column.
    private int TextWidthFor(int width) => Math.Max(1, width - this.TextLeft - this.PanelPadding - this.RightColumnWidth - this.PanelPadding);

    private int TextHeight(Emoji emoji, int textWidth)
    {
        int height = 0;
        bool first = true;
        foreach (Row row in this.RowsOf(emoji))
        {
            if (!first)
            {
                height += 2 * this.LogicalToDeviceUnits(LogicalRowGap) + 1;
            }

            first = false;
            height += Math.Max(1, this.Wrap(row.Name, isName: true, textWidth - this.NameIndent).Count) * this.LineHeight(this.nameFont);
            if (row.Tags.Length > 0)
            {
                height += this.LogicalToDeviceUnits(LogicalTagsGap)
                    + this.Wrap(row.Tags, isName: false, textWidth).Count * this.LineHeight(this.Font);
            }
        }

        return height;
    }

    // The height computed again — the French row toggled, another font or DPI —, through the parent's layout.
    private void RequestHeight()
    {
        this.heightWidth = -1;
        this.Parent?.PerformLayout();
    }

    // The words of text placed on lines at most width wide, breaking at spaces; a word wider than a line has a line of
    // its own. Each word: its start and length in text, its x from the line's start.
    private List<List<(int Start, int Length, int X)>> Wrap(string text, bool isName, int width)
    {
        var lines = new List<List<(int Start, int Length, int X)>>();
        var line = new List<(int Start, int Length, int X)>();
        int x = 0;
        int space = this.WordWidth(" ", isName);
        for (int start = 0; start < text.Length;)
        {
            int end = text.IndexOf(' ', start);
            if (end < 0)
            {
                end = text.Length;
            }

            if (end > start)
            {
                int wordWidth = this.WordWidth(text[start..end], isName);
                if (line.Count > 0 && x + space + wordWidth > width)
                {
                    lines.Add(line);
                    line = [];
                    x = 0;
                }
                else if (line.Count > 0)
                {
                    x += space;
                }

                line.Add((start, end - start, x));
                x += wordWidth;
            }

            start = end + 1;
        }

        if (line.Count > 0)
        {
            lines.Add(line);
        }

        return lines;
    }

    // Draws text wrapped at width from (left, top), the search's matches highlighted; returns the y under it.
    private int PaintText(Graphics graphics, string text, bool isName, int left, int top, int width)
    {
        Font font = isName ? this.nameFont : this.Font;
        Color color = isName ? SystemColors.ControlText : SystemColors.GrayText;
        int lineHeight = this.LineHeight(font);
        IReadOnlyList<(int Start, int Length)> matches = EmojiSearch.MatchSpans(text, this.searchText);
        using var highlight = new SolidBrush(this.highlightColor);
        int y = top;
        foreach (List<(int Start, int Length, int X)> line in this.Wrap(text, isName, width))
        {
            foreach ((int start, int length, int x) in line)
            {
                foreach ((int matchStart, int matchLength) in matches)
                {
                    int from = Math.Max(start, matchStart);
                    int to = Math.Min(start + length, matchStart + matchLength);
                    if (from < to)
                    {
                        int offset = MeasureWidth(text[start..from], font);
                        graphics.FillRectangle(highlight, left + x + offset, y, MeasureWidth(text[from..to], font), lineHeight);
                    }
                }

                TextRenderer.DrawText(graphics, text.Substring(start, length), font, new Point(left + x, y), color, TextFlags);
            }

            y += lineHeight;
        }

        return y;
    }

    // The flag at the DPI's resolution, scaled to its logical size, edged.
    private void PaintFlag(Graphics graphics, Flag flag, int x, int y)
    {
        var bounds = new Rectangle(x, y, this.LogicalToDeviceUnits(flag.LogicalWidth), this.LogicalToDeviceUnits(LogicalFlagHeight));
        InterpolationMode interpolation = graphics.InterpolationMode;
        PixelOffsetMode pixelOffset = graphics.PixelOffsetMode;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(this.DeviceDpi > 144 ? flag.Large : flag.Small, bounds);
        graphics.InterpolationMode = interpolation;
        graphics.PixelOffsetMode = pixelOffset;
        using var edge = new Pen(FlagEdgeColor);
        graphics.DrawRectangle(edge, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    // The copy glyph, a check mark for a moment after a copy; the grid's header buttons' hover behind it.
    private void PaintButton(Graphics graphics)
    {
        Rectangle bounds = this.ButtonBounds;
        if (this.hovered.Part == Part.Button || this.pressed.Part == Part.Button)
        {
            using var hover = new SolidBrush(SystemColors.ControlLight);
            graphics.FillRectangle(hover, bounds);
        }

        TextRenderer.DrawText(graphics, (this.copied ? CheckGlyph : CopyGlyph).ToString(), this.iconFont, bounds,
            this.copied ? SystemColors.Highlight : SystemColors.ControlText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFlags);
    }

    // A tone bar's six swatches, on two rows of three: the default tone's — None first —, or the second person's —
    // "same as the first person" first, an empty ring. The tone in use ringed in the accent colour, the swatch under
    // the mouse on the hover colour; the second bar dimmed while inactive.
    private void PaintBar(Graphics graphics, Part bar)
    {
        bool active = bar == Part.FirstBar || this.SecondBarActive;
        // A tone's slot is its place in SkinTones.All, the enum's order; the second bar's slot 0 is "same".
        int ringed = bar == Part.FirstBar ? (int)this.defaultTone : (int)(this.secondTone ?? SkinTone.None);
        int ringWidth = this.LogicalToDeviceUnits(LogicalRingWidth);
        SmoothingMode smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        // The hover first: a ring reaches into the gap the neighbour's hover covers.
        foreach (Hit hit in new[] { this.hovered, this.pressed }.Where(hit => active && hit.Part == bar))
        {
            using var hover = new SolidBrush(SystemColors.ControlLight);
            graphics.FillRectangle(hover, Rectangle.Inflate(this.SwatchBounds(bar, hit.Slot), this.SwatchGap / 2, this.SwatchGap / 2));
        }

        for (int slot = 0; slot < SkinTones.All.Count; slot++)
        {
            Rectangle swatch = this.SwatchBounds(bar, slot);
            int alpha = active ? 255 : DimmedAlpha;
            using var outline = new Pen(Color.FromArgb(alpha, SystemColors.ControlDark));
            var circle = new Rectangle(swatch.X, swatch.Y, swatch.Width - 1, swatch.Height - 1);
            if (bar == Part.FirstBar || slot > 0)
            {
                using var fill = new SolidBrush(Color.FromArgb(alpha, SkinTones.ColorOf(SkinTones.All[slot])));
                graphics.FillEllipse(fill, circle);
            }

            graphics.DrawEllipse(outline, circle);
            if (slot == ringed && active)
            {
                using var ring = new Pen(SystemColors.Highlight, ringWidth);
                graphics.DrawEllipse(ring, Rectangle.Inflate(circle, ringWidth, ringWidth));
            }
        }

        graphics.SmoothingMode = smoothing;
    }

    // The part of the panel at a point: the copy button, a swatch of a shown bar — of the second one only while active —,
    // or nothing. A swatch answers on half the gap around it too.
    private Hit HitTest(Point point)
    {
        if (this.shownEmoji is null)
        {
            return default;
        }

        if (this.ButtonBounds.Contains(point))
        {
            return new Hit(Part.Button, 0);
        }

        foreach (Part bar in new[] { Part.FirstBar, Part.SecondBar })
        {
            if (bar == Part.FirstBar ? !this.ShowsBar : !this.ShowsSecondBar || !this.SecondBarActive)
            {
                continue;
            }

            for (int slot = 0; slot < SkinTones.All.Count; slot++)
            {
                if (Rectangle.Inflate(this.SwatchBounds(bar, slot), this.SwatchGap / 2, this.SwatchGap / 2).Contains(point))
                {
                    return new Hit(bar, slot);
                }
            }
        }

        return default;
    }

    // A swatch: its bar right-aligned with the button, the first one a bar gap under it, the second one under the first.
    private Rectangle SwatchBounds(Part bar, int slot)
    {
        int left = this.Width - this.PanelPadding - this.BarWidth;
        int top = this.PanelPadding + this.ButtonSize + this.BarGap + (bar == Part.SecondBar ? this.BarHeight + this.BarGap : 0);
        int step = this.SwatchSize + this.SwatchGap;
        return new Rectangle(left + slot % SwatchesPerRow * step, top + slot / SwatchesPerRow * step, this.SwatchSize, this.SwatchSize);
    }

    // The area to paint again for a part, its ring and hover included.
    private Rectangle BoundsOf(Hit hit) => hit.Part switch
    {
        Part.Button => this.ButtonBounds,
        Part.FirstBar or Part.SecondBar => Rectangle.Inflate(this.SwatchBounds(hit.Part, hit.Slot), this.SwatchGap, this.SwatchGap),
        _ => Rectangle.Empty,
    };

    private void SetHovered(Hit hovered)
    {
        if (hovered == this.hovered)
        {
            return;
        }

        Hit previous = this.hovered;
        this.hovered = hovered;
        this.UpdateToolTip();
        this.Invalidate(this.BoundsOf(previous));
        this.Invalidate(this.BoundsOf(hovered));
    }

    // The tooltip of the part under the mouse: the button's, the emoji's first code point, U+1F602 — U+1F468 for a
    // sequence; a swatch's, its tone.
    private void UpdateToolTip()
    {
        string? text = (this.hovered.Part, this.shownEmoji) switch
        {
            (Part.Button, Emoji emoji) => "U+" + emoji.Hexcode.Split('-')[0],
            (Part.FirstBar, not null) => string.Format(DefaultToneToolTip, SkinTones.NameOf(SkinTones.All[this.hovered.Slot])),
            (Part.SecondBar, not null) => this.hovered.Slot == 0
                ? SameToneToolTip
                : string.Format(SecondToneToolTip, SkinTones.NameOf(SkinTones.All[this.hovered.Slot])),
            _ => null,
        };
        this.toolTip.SetToolTip(this, text);
    }

    // The emoji itself, as text, in its tone. The window stays, and the copy is no use: the frequent tab ignores it.
    private void CopyEmoji()
    {
        if (this.shownEmoji is not Emoji emoji)
        {
            return;
        }

        try
        {
            Clipboard.SetText(this.ShownText(emoji));
        }
        catch (ExternalException exception)
        {
            // Another app holds the clipboard: nothing is copied, and no check mark says otherwise.
            Debug.WriteLine($"EmojiDetailsPanel: emoji not copied — {exception.Message}");
            return;
        }

        this.copied = true;
        this.copiedTimer.Stop();
        this.copiedTimer.Start();
        this.Invalidate(this.ButtonBounds);
    }

    private void EndCopied()
    {
        this.copiedTimer.Stop();
        if (this.copied)
        {
            this.copied = false;
            this.Invalidate(this.ButtonBounds);
        }
    }

    // The shown emoji drawn at the panel's emoji size, in its tone, the previous bitmap freed.
    private void RenderEmoji()
    {
        Bitmap? previous = this.emojiBitmap;
        this.emojiBitmap = this.shownEmoji is Emoji emoji ? this.renderer.Render(this.ShownText(emoji), this.EmojiSize) : null;
        previous?.Dispose();
    }

    private int WordWidth(string word, bool isName)
    {
        if (!this.wordWidths.TryGetValue((isName, word), out int width))
        {
            width = MeasureWidth(word, isName ? this.nameFont : this.Font);
            this.wordWidths[(isName, word)] = width;
        }

        return width;
    }

    private int LineHeight(Font font)
    {
        if (!this.lineHeights.TryGetValue(font, out int height))
        {
            height = TextRenderer.MeasureText("Ag", font, Size.Empty, TextFlags).Height;
            this.lineHeights[font] = height;
        }

        return height;
    }

    private static int MeasureWidth(string text, Font font) =>
        text.Length == 0 ? 0 : TextRenderer.MeasureText(text, font, Size.Empty, TextFlags).Width;

    private Font CreateIconFont()
    {
        string family = IconFonts.FirstOrDefault(name => FontFamily.Families.Any(installed => installed.Name == name))
            ?? IconFonts[^1];
        return new Font(family, this.LogicalToDeviceUnits(LogicalButtonIconSize), GraphicsUnit.Pixel);
    }

    // One language's row: its flag, the emoji's name, its tags joined.
    private readonly record struct Row(Flag Flag, string Name, string Tags);

    // The parts of the panel the mouse acts on.
    private enum Part
    {
        None,
        Button,
        FirstBar,
        SecondBar,
    }

    // A part, and the swatch's slot in a bar — 0 otherwise.
    private readonly record struct Hit(Part Part, int Slot);

    // A flag embedded in the exe (UI/Flags), at 1× and 2×, and its width at 1×.
    private sealed class Flag : IDisposable
    {
        public Flag(string name, int logicalWidth)
        {
            this.Small = Load($"{name}.png");
            this.Large = Load($"{name}@2x.png");
            this.LogicalWidth = logicalWidth;
        }

        public Bitmap Small { get; }

        public Bitmap Large { get; }

        public int LogicalWidth { get; }

        public void Dispose()
        {
            this.Small.Dispose();
            this.Large.Dispose();
        }

        // Copied out of the stream: a Bitmap needs its stream for as long as it lives.
        private static Bitmap Load(string fileName)
        {
            string resourceName = $"EmojiSelector.UI.Flags.{fileName}";
            using Stream stream = typeof(Flag).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
            using var loaded = new Bitmap(stream);
            return new Bitmap(loaded);
        }
    }
}
