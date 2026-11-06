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
/// English row always, the French one while <see cref="ShowFrench"/>, then the emoji's synonyms while it has some, after
/// a ≈, in italic grey (<see cref="Synonyms"/>); at the right, a copy button copying the emoji to
/// the clipboard, its first code point as a tooltip. While the search box holds text (<see cref="SearchText"/>), the
/// characters the search matches are highlighted in <see cref="HighlightColor"/>.
/// <para>
/// The emoji is drawn and copied as its <see cref="ShownText"/>: in the skin tone in use. While it has skin-tone
/// variants — or whatever the selection, an empty panel included, while <see cref="AlwaysShowTone"/> —, a <b>tone
/// swatch</b> under the copy button shows the <see cref="DefaultTone"/>; a click raises <see cref="ToneSwatchClicked"/>,
/// the parent opening the tones' menu under it.
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

    // The tone swatch: as large as the copy button, under it, its colour a circle in the middle.
    private const int LogicalToneGap = 4;
    private const int LogicalToneCircleSize = 16;

    // How much smaller than the text the emoticons are, in points: the grid's captions' size.
    private const float EmoticonFontShrink = 0.75F;

    private const char CopyGlyph = '';
    private const char CheckGlyph = '';

    // How long the check mark replaces the copy glyph after a copy.
    private const int CopiedMilliseconds = 1000;

    // How long the zoom's value stays under the tone swatch after the last change.
    private const int ZoomShownMilliseconds = 2000;

    private const string ZoomText = "{0}%";

    private const string TagSeparator = ", ";

    // In the flags' column of the synonyms' row: the words after it are like the emoji's, not its own.
    private const string SynonymsMark = "≈";

    private const string ToneToolTip = "Skin tone: {0}";

    private const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

    private readonly IReadOnlyList<Emoji> catalog;

    // Whether the catalog has emojis with variants: the room the tone swatch takes, whatever the selection.
    private readonly bool hasVariants;
    private readonly EmojiRenderer renderer = new();
    private readonly ToolTip toolTip = new();
    private readonly System.Windows.Forms.Timer copiedTimer = new() { Interval = CopiedMilliseconds };
    private readonly System.Windows.Forms.Timer zoomShownTimer = new() { Interval = ZoomShownMilliseconds };
    private readonly Flag usFlag = new("us", LogicalUsFlagWidth);
    private readonly Flag frenchFlag = new("fr", LogicalFrenchFlagWidth);

    // A word's width in a font of the rows — the name's bold, the tags' regular, the synonyms' italic, told apart by
    // their style: measured once, the words repeat across emojis.
    private readonly Dictionary<(FontStyle Style, string Word), int> wordWidths = [];

    // A font's line height, measured once: the height is computed over the whole catalog.
    private readonly Dictionary<Font, int> lineHeights = [];

    private Font nameFont;
    private Font synonymFont;
    private Font emoticonFont;
    private Font iconFont;
    private Emoji? shownEmoji;
    private Bitmap? emojiBitmap;
    private string searchText = "";
    private SearchSynonyms synonyms = SearchSynonyms.Empty;
    private bool showFrench = true;
    private Color highlightColor = DefaultHighlightColor;
    private SkinTone defaultTone;
    private bool alwaysShowTone = true;
    private int zoomPercent = 100;

    // The zoom's value shows under the tone swatch, for a moment after a change.
    private bool showsZoom;

    // The part under the mouse, and the one the left button went down on.
    private Part hovered;
    private Part pressed;
    private bool copied;

    // The width FitHeight last fitted the height to: -1 to fit it again.
    private int heightWidth = -1;

    /// <param name="catalog">Every emoji the panel may show: the height fits the one with the most text.</param>
    public EmojiDetailsPanel(IReadOnlyList<Emoji> catalog)
    {
        this.catalog = catalog;
        this.hasVariants = catalog.Any(emoji => emoji.Variants.Count > 0);
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.Selectable, false);
        this.BackColor = SystemColors.Window;
        this.nameFont = new Font(this.Font, FontStyle.Bold);
        this.synonymFont = new Font(this.Font, FontStyle.Italic);
        this.emoticonFont = new Font(this.Font.FontFamily, this.Font.Size - EmoticonFontShrink);
        this.iconFont = this.CreateIconFont();
        this.copiedTimer.Tick += (_, _) => this.EndCopied();
        this.zoomShownTimer.Tick += (_, _) => this.EndZoomShown();
    }

    /// <summary>
    /// The emojis' zoom, percent: the emoji drawn large follows it, and so does the panel's height. The texts, the copy
    /// button and the tone swatch keep their size.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ZoomPercent
    {
        get => this.zoomPercent;
        set
        {
            if (value != this.zoomPercent)
            {
                this.zoomPercent = value;
                this.RenderEmoji();
                this.RequestHeight();
                this.Invalidate();
            }
        }
    }

    /// <summary>
    /// Shows the zoom's value under the tone swatch — at its place while the swatch does not show —, gone
    /// <see cref="ZoomShownMilliseconds"/> after the last call. Its line is always kept: the panel never moves for it.
    /// </summary>
    public void ShowZoom()
    {
        this.showsZoom = true;
        this.zoomShownTimer.Stop();
        this.zoomShownTimer.Start();
        this.Invalidate(this.ZoomBounds);
    }

    private void EndZoomShown()
    {
        this.zoomShownTimer.Stop();
        if (this.showsZoom)
        {
            this.showsZoom = false;
            this.Invalidate(this.ZoomBounds);
        }
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

            // The swatch comes and goes with the emoji: the part under a still mouse may be another.
            this.UpdateHovered();
            this.Invalidate();
        }
    }

    /// <summary>
    /// Whether the tone swatch shows whatever the selection — an emoji without variants, none at all —, rather than
    /// only while the selection has variants. The room for it is kept while it is on, even in a catalog without
    /// variants.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AlwaysShowTone
    {
        get => this.alwaysShowTone;
        set
        {
            if (value != this.alwaysShowTone)
            {
                this.alwaysShowTone = value;
                this.RequestHeight();
                this.UpdateHovered();
                this.Invalidate();
            }
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

    /// <summary>
    /// The search's synonyms: the words they add to the search text are highlighted too, as whole words, and the shown
    /// emoji's own synonyms make its last row (see <see cref="SearchSynonyms.SynonymsOf"/>).
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SearchSynonyms Synonyms
    {
        get => this.synonyms;
        set
        {
            this.synonyms = value;
            this.RequestHeight();
            this.Invalidate();
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

    /// <summary>The default skin tone, the tone swatch's colour.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SkinTone DefaultTone
    {
        get => this.defaultTone;
        set
        {
            this.defaultTone = value;
            this.UpdateToolTip();
            this.Invalidate();
        }
    }

    /// <summary>The tone swatch was clicked: its bounds, in the panel's coordinates — the tones' menu opens under it.</summary>
    public event EventHandler<Rectangle>? ToneSwatchClicked;

    /// <summary>The shown emoji drawn again: its <see cref="ShownText"/> changed with a tone.</summary>
    public void RefreshShownEmoji()
    {
        this.EndCopied();
        this.RenderEmoji();
        this.Invalidate();
    }

    private int PanelPadding => this.LogicalToDeviceUnits(LogicalPadding);

    private int EmojiSize => this.EmojiSizeAt(this.zoomPercent);

    private int ButtonSize => this.LogicalToDeviceUnits(LogicalButtonSize);

    // Where the rows start: right of the emoji.
    private int TextLeft => this.TextLeftAt(this.EmojiSize);

    // The emoji drawn large at a zoom, percent.
    private int EmojiSizeAt(int zoomPercent) => (int)Math.Round(this.LogicalToDeviceUnits(LogicalEmojiSize) * zoomPercent / 100.0);

    private int TextLeftAt(int emojiSize) => this.PanelPadding + emojiSize + this.PanelPadding;

    // A name starts after the widest flag: the names of both rows line up.
    private int NameIndent => this.LogicalToDeviceUnits(LogicalUsFlagWidth + LogicalFlagGap);

    private Rectangle ButtonBounds => new(this.Width - this.PanelPadding - this.ButtonSize, this.PanelPadding, this.ButtonSize, this.ButtonSize);

    // The tone swatch: under the copy button, as large as it.
    private Rectangle ToneBounds => new(this.ButtonBounds.X, this.ButtonBounds.Bottom + this.LogicalToDeviceUnits(LogicalToneGap),
        this.ButtonSize, this.ButtonSize);

    // Whether the panel keeps the tone swatch's room: the catalog has variants, or the swatch always shows.
    private bool HasToneRoom => this.hasVariants || this.alwaysShowTone;

    // The zoom's value: under the tone swatch while it shows, at its place otherwise; centred on the right column, as
    // wide as it and its paddings — "200%" is wider than the button.
    private Rectangle ZoomBounds
    {
        get
        {
            int top = this.ShowsTone ? this.ToneBounds.Bottom + this.LogicalToDeviceUnits(LogicalToneGap) : this.ToneBounds.Y;
            Rectangle button = this.ButtonBounds;
            return new Rectangle(button.X - this.PanelPadding, top, button.Width + 2 * this.PanelPadding, this.LineHeight(this.Font));
        }
    }

    // The copy button, then the tone swatch under it when the panel keeps its room, then the zoom's line — always kept.
    private int RightColumnHeight =>
        (this.HasToneRoom ? this.ToneBounds.Bottom : this.ButtonBounds.Bottom) - this.PanelPadding
            + this.LogicalToDeviceUnits(LogicalToneGap) + this.LineHeight(this.Font);

    // Whether the tone swatch shows: always, or while the shown emoji has variants.
    private bool ShowsTone => this.alwaysShowTone || this.shownEmoji is { Variants.Count: > 0 };

    /// <summary>
    /// The panel's height at <paramref name="width"/>: the emoji with the most text, the French row included while
    /// shown, fits, and so do the copy button, the tone swatch and the zoom's line — device pixels, at the current DPI
    /// and zoom.
    /// </summary>
    public int HeightFor(int width) => this.HeightFor(width, this.zoomPercent);

    /// <summary>
    /// <see cref="HeightFor(int)"/> at another zoom, percent — 100 for the window's default size, which does not follow
    /// the zoom.
    /// </summary>
    public int HeightFor(int width, int zoomPercent)
    {
        int emojiSize = this.EmojiSizeAt(zoomPercent);
        int textWidth = this.TextWidthFor(width, this.TextLeftAt(emojiSize));
        int text = this.catalog.Count == 0 ? 0 : this.catalog.Max(emoji => this.TextHeight(emoji, textWidth));
        int emojiColumn = emojiSize + (this.catalog.Any(emoji => emoji.Emoticons.Count > 0) ? this.LineHeight(this.emoticonFont) : 0);
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
        Font previousSynonym = this.synonymFont;
        Font previousEmoticon = this.emoticonFont;
        this.nameFont = new Font(this.Font, FontStyle.Bold);
        this.synonymFont = new Font(this.Font, FontStyle.Italic);
        this.emoticonFont = new Font(this.Font.FontFamily, this.Font.Size - EmoticonFontShrink);
        previousName.Dispose();
        previousSynonym.Dispose();
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

    // Drawn on the Acrylic backdrop (see Glass): a tinted transparent background, GDI+ text.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Glass
    {
        get => this.glass;
        set
        {
            this.glass = value;
            // The words are measured as they are drawn: GDI+ on the backdrop.
            this.wordWidths.Clear();
            this.lineHeights.Clear();
            this.RequestHeight();
            this.Invalidate();
        }
    }

    private bool glass;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!this.glass)
        {
            base.OnPaintBackground(e);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (this.glass)
        {
            UI.Glass.Paint(e, this.PaintContent);
        }
        else
        {
            this.PaintContent(e.Graphics);
        }
    }

    private void PaintContent(Graphics graphics)
    {

        // The line between the grid and the panel.
        using (var border = new Pen(SystemColors.ControlLight))
        {
            graphics.DrawLine(border, 0, 0, this.Width, 0);
        }

        // Before the selection: the swatch may show in an empty panel, at its place under the copy button's.
        if (this.ShowsTone)
        {
            this.PaintTone(graphics);
        }

        // A moment after a zoom change, an empty panel included.
        if (this.showsZoom)
        {
            UI.Glass.DrawText(graphics, this.glass, string.Format(ZoomText, this.zoomPercent), this.Font, this.ZoomBounds,
                SystemColors.GrayText, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFlags);
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
            this.PaintEmoticons(graphics, string.Join(" ", emoji.Emoticons), padding + this.EmojiSize);
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
            if (row.Flag is not Flag flag)
            {
                // The synonyms: the mark in the flags' column, the words after it, wrapped under one another.
                UI.Glass.DrawText(graphics, this.glass, SynonymsMark, this.Font, new Point(this.TextLeft, y), SystemColors.GrayText, TextFlags);
                y = this.PaintText(graphics, row.Tags, this.synonymFont, this.TextLeft + this.NameIndent, y, textWidth - this.NameIndent);
                continue;
            }

            int nameLineHeight = this.LineHeight(this.nameFont);
            this.PaintFlag(graphics, flag, this.TextLeft, y + (nameLineHeight - this.LogicalToDeviceUnits(LogicalFlagHeight)) / 2);
            y = this.PaintText(graphics, row.Name, this.nameFont, this.TextLeft + this.NameIndent, y, textWidth - this.NameIndent);
            if (row.Tags.Length > 0)
            {
                y += this.LogicalToDeviceUnits(LogicalTagsGap);
                y = this.PaintText(graphics, row.Tags, this.Font, this.TextLeft, y, textWidth);
            }
        }

        this.PaintButton(graphics);
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
        if (e.Button == MouseButtons.Left && this.hovered != Part.None)
        {
            this.pressed = this.hovered;
            this.Invalidate(this.BoundsOf(this.pressed));
        }
    }

    // The copy button and the tone swatch act when released over the part pressed.
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || this.pressed == Part.None)
        {
            return;
        }

        Part pressed = this.pressed;
        this.pressed = Part.None;
        this.Invalidate(this.BoundsOf(pressed));
        if (this.HitTest(e.Location) != pressed)
        {
            return;
        }

        switch (pressed)
        {
            case Part.Button:
                this.CopyEmoji();
                break;
            case Part.Tone:
                this.ToneSwatchClicked?.Invoke(this, this.ToneBounds);
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
            this.zoomShownTimer.Dispose();
            this.usFlag.Dispose();
            this.frenchFlag.Dispose();
            this.emojiBitmap?.Dispose();
            this.nameFont.Dispose();
            this.synonymFont.Dispose();
            this.emoticonFont.Dispose();
            this.iconFont.Dispose();
        }

        base.Dispose(disposing);
    }

    // The rows shown: English, then French while shown, then the synonyms while the emoji has some — no flag, no name.
    private IEnumerable<Row> RowsOf(Emoji emoji)
    {
        yield return new Row(this.usFlag, emoji.Name, string.Join(TagSeparator, emoji.EnglishTags));
        if (this.showFrench)
        {
            yield return new Row(this.frenchFlag, emoji.FrenchName, string.Join(TagSeparator, emoji.FrenchTags));
        }

        if (this.synonyms.SynonymsOf(emoji) is { Count: > 0 } synonyms)
        {
            yield return new Row(null, "", string.Join(TagSeparator, synonyms));
        }
    }

    // Between the emoji and the button.
    private int TextWidthFor(int width) => this.TextWidthFor(width, this.TextLeft);

    private int TextWidthFor(int width, int textLeft) => Math.Max(1, width - textLeft - this.PanelPadding - this.ButtonSize - this.PanelPadding);

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
            if (row.Flag is null)
            {
                height += Math.Max(1, this.Wrap(row.Tags, this.synonymFont, textWidth - this.NameIndent).Count) * this.LineHeight(this.synonymFont);
                continue;
            }

            height += Math.Max(1, this.Wrap(row.Name, this.nameFont, textWidth - this.NameIndent).Count) * this.LineHeight(this.nameFont);
            if (row.Tags.Length > 0)
            {
                height += this.LogicalToDeviceUnits(LogicalTagsGap)
                    + this.Wrap(row.Tags, this.Font, textWidth).Count * this.LineHeight(this.Font);
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
    private List<List<(int Start, int Length, int X)>> Wrap(string text, Font font, int width)
    {
        var lines = new List<List<(int Start, int Length, int X)>>();
        var line = new List<(int Start, int Length, int X)>();
        int x = 0;
        int space = this.WordWidth(" ", font);
        for (int start = 0; start < text.Length;)
        {
            int end = text.IndexOf(' ', start);
            if (end < 0)
            {
                end = text.Length;
            }

            if (end > start)
            {
                int wordWidth = this.WordWidth(text[start..end], font);
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

    // Draws text wrapped at width from (left, top), the search's matches highlighted; returns the y under it. A name in
    // the text's colour, the tags and the synonyms in grey. The synonyms' row highlights the typed words alone: their
    // alternatives are its own words, every one of them would be.
    private int PaintText(Graphics graphics, string text, Font font, int left, int top, int width)
    {
        Color color = font == this.nameFont ? SystemColors.ControlText : SystemColors.GrayText;
        int lineHeight = this.LineHeight(font);
        IReadOnlyList<(int Start, int Length)> matches =
            EmojiSearch.MatchSpans(text, this.searchText, font == this.synonymFont ? null : this.synonyms);
        using var highlight = new SolidBrush(this.highlightColor);
        int y = top;
        foreach (List<(int Start, int Length, int X)> line in this.Wrap(text, font, width))
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

                UI.Glass.DrawText(graphics, this.glass, text.Substring(start, length), font, new Point(left + x, y), color, TextFlags);
            }

            y += lineHeight;
        }

        return y;
    }

    // The emoticons centred under the emoji, from top, the search's matches highlighted like the names and tags.
    private void PaintEmoticons(Graphics graphics, string emoticons, int top)
    {
        int left = (this.TextLeft - MeasureWidth(emoticons, this.emoticonFont)) / 2;
        int lineHeight = this.LineHeight(this.emoticonFont);
        using var highlight = new SolidBrush(this.highlightColor);
        foreach ((int start, int length) in EmojiSearch.MatchSpans(emoticons, this.searchText, this.synonyms))
        {
            int offset = MeasureWidth(emoticons[..start], this.emoticonFont);
            graphics.FillRectangle(highlight, left + offset, top, MeasureWidth(emoticons.Substring(start, length), this.emoticonFont),
                lineHeight);
        }

        UI.Glass.DrawText(graphics, this.glass, emoticons, this.emoticonFont, new Point(left, top), SystemColors.GrayText, TextFlags);
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
        if (this.hovered == Part.Button || this.pressed == Part.Button)
        {
            using var hover = new SolidBrush(SystemColors.ControlLight);
            graphics.FillRectangle(hover, bounds);
        }

        UI.Glass.DrawText(graphics, this.glass, (this.copied ? CheckGlyph : CopyGlyph).ToString(), this.iconFont, bounds,
            this.copied ? SystemColors.Highlight : SystemColors.ControlText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFlags);
    }

    // The tone swatch: the default tone's colour in a circle, outlined; the copy button's hover behind it.
    private void PaintTone(Graphics graphics)
    {
        Rectangle bounds = this.ToneBounds;
        if (this.hovered == Part.Tone || this.pressed == Part.Tone)
        {
            using var hover = new SolidBrush(SystemColors.ControlLight);
            graphics.FillRectangle(hover, bounds);
        }

        int size = this.LogicalToDeviceUnits(LogicalToneCircleSize);
        var circle = new Rectangle(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size - 1, size - 1);
        SmoothingMode smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var fill = new SolidBrush(SkinTones.ColorOf(this.defaultTone)))
        using (var outline = new Pen(SystemColors.ControlDark))
        {
            graphics.FillEllipse(fill, circle);
            graphics.DrawEllipse(outline, circle);
        }

        graphics.SmoothingMode = smoothing;
    }

    // The part of the panel at a point: the copy button while an emoji is shown, the tone swatch while shown — an empty
    // panel included —, or nothing.
    private Part HitTest(Point point)
    {
        if (this.shownEmoji is not null && this.ButtonBounds.Contains(point))
        {
            return Part.Button;
        }

        return this.ShowsTone && this.ToneBounds.Contains(point) ? Part.Tone : Part.None;
    }

    // The part under a still mouse, and its tooltip: the swatch may have come or gone under it.
    private void UpdateHovered()
    {
        this.hovered = this.IsHandleCreated ? this.HitTest(this.PointToClient(Cursor.Position)) : default;
        this.UpdateToolTip();
    }

    // The area to paint again for a part.
    private Rectangle BoundsOf(Part part) => part switch
    {
        Part.Button => this.ButtonBounds,
        Part.Tone => this.ToneBounds,
        _ => Rectangle.Empty,
    };

    private void SetHovered(Part hovered)
    {
        if (hovered == this.hovered)
        {
            return;
        }

        Part previous = this.hovered;
        this.hovered = hovered;
        this.UpdateToolTip();
        this.Invalidate(this.BoundsOf(previous));
        this.Invalidate(this.BoundsOf(hovered));
    }

    // The tooltip of the part under the mouse: the button's, the emoji's first code point, U+1F602 — U+1F468 for a
    // sequence; the tone swatch's, the default tone.
    private void UpdateToolTip()
    {
        string? text = (this.hovered, this.shownEmoji) switch
        {
            (Part.Button, Emoji emoji) => "U+" + emoji.Hexcode.Split('-')[0],
            (Part.Tone, _) => string.Format(ToneToolTip, SkinTones.NameOf(this.defaultTone)),
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
        this.emojiBitmap = this.shownEmoji is Emoji emoji ? this.renderer.Render(this.ShownText(emoji), this.EmojiSize, emoji.IsText) : null;
        previous?.Dispose();
    }

    private int WordWidth(string word, Font font)
    {
        if (!this.wordWidths.TryGetValue((font.Style, word), out int width))
        {
            width = this.MeasureWidth(word, font);
            this.wordWidths[(font.Style, word)] = width;
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

    // Measured as drawn: GDI+ on the backdrop, GDI otherwise — the words keep even spaces between them.
    private int MeasureWidth(string text, Font font) =>
        text.Length == 0 ? 0
        : this.glass ? UI.Glass.MeasureWidth(text, font)
        : TextRenderer.MeasureText(text, font, Size.Empty, TextFlags).Width;

    private Font CreateIconFont()
    {
        string family = IconFonts.FirstOrDefault(name => FontFamily.Families.Any(installed => installed.Name == name))
            ?? IconFonts[^1];
        return new Font(family, this.LogicalToDeviceUnits(LogicalButtonIconSize), GraphicsUnit.Pixel);
    }

    // One language's row: its flag, the emoji's name, its tags joined. The synonyms' row: no flag, no name, the
    // synonyms joined in Tags.
    private readonly record struct Row(Flag? Flag, string Name, string Tags);

    // The parts of the panel the mouse acts on.
    private enum Part
    {
        None,
        Button,
        Tone,
    }

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
