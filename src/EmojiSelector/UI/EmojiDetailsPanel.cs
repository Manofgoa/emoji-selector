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

    // The emoticons' font size in points, the grid's captions'.
    private const float EmoticonFontSize = 8.25F;

    private const char CopyGlyph = '';
    private const char CheckGlyph = '';

    // How long the check mark replaces the copy glyph after a copy.
    private const int CopiedMilliseconds = 1000;

    private const string TagSeparator = ", ";

    private const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

    private readonly IReadOnlyList<Emoji> catalog;
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
    private bool buttonHovered;
    private bool buttonPressed;
    private bool copied;

    // The width FitHeight last fitted the height to: -1 to fit it again.
    private int heightWidth = -1;

    /// <param name="catalog">Every emoji the panel may show: the height fits the one with the most text.</param>
    public EmojiDetailsPanel(IReadOnlyList<Emoji> catalog)
    {
        this.catalog = catalog;
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

    private int PanelPadding => this.LogicalToDeviceUnits(LogicalPadding);

    private int EmojiSize => this.LogicalToDeviceUnits(LogicalEmojiSize);

    private int ButtonSize => this.LogicalToDeviceUnits(LogicalButtonSize);

    // Where the rows start: right of the emoji.
    private int TextLeft => this.PanelPadding + this.EmojiSize + this.PanelPadding;

    // A name starts after the widest flag: the names of both rows line up.
    private int NameIndent => this.LogicalToDeviceUnits(LogicalUsFlagWidth + LogicalFlagGap);

    private Rectangle ButtonBounds => new(this.Width - this.PanelPadding - this.ButtonSize, this.PanelPadding, this.ButtonSize, this.ButtonSize);

    /// <summary>
    /// The panel's height at <paramref name="width"/>: the emoji with the most text, the French row included while
    /// shown, fits — device pixels, at the current DPI.
    /// </summary>
    public int HeightFor(int width)
    {
        int textWidth = this.TextWidthFor(width);
        int text = this.catalog.Count == 0 ? 0 : this.catalog.Max(emoji => this.TextHeight(emoji, textWidth));
        int emojiColumn = this.EmojiSize + (this.catalog.Any(emoji => emoji.Emoticons.Count > 0) ? this.LineHeight(this.emoticonFont) : 0);
        return 2 * this.PanelPadding + Math.Max(Math.Max(emojiColumn, this.ButtonSize), text);
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
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        this.SetButtonHovered(this.shownEmoji is not null && this.ButtonBounds.Contains(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        this.SetButtonHovered(false);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && this.buttonHovered)
        {
            this.buttonPressed = true;
            this.Invalidate(this.ButtonBounds);
        }
    }

    // The button acts when released over it.
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !this.buttonPressed)
        {
            return;
        }

        this.buttonPressed = false;
        this.Invalidate(this.ButtonBounds);
        if (this.ButtonBounds.Contains(e.Location))
        {
            this.CopyEmoji();
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

    // Between the emoji and the button.
    private int TextWidthFor(int width) => Math.Max(1, width - this.TextLeft - this.PanelPadding - this.ButtonSize - this.PanelPadding);

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

    // The emoticons centred under the emoji, from top, the search's matches highlighted like the names and tags.
    private void PaintEmoticons(Graphics graphics, string emoticons, int top)
    {
        int left = (this.TextLeft - MeasureWidth(emoticons, this.emoticonFont)) / 2;
        int lineHeight = this.LineHeight(this.emoticonFont);
        using var highlight = new SolidBrush(this.highlightColor);
        foreach ((int start, int length) in EmojiSearch.MatchSpans(emoticons, this.searchText))
        {
            int offset = MeasureWidth(emoticons[..start], this.emoticonFont);
            graphics.FillRectangle(highlight, left + offset, top, MeasureWidth(emoticons.Substring(start, length), this.emoticonFont),
                lineHeight);
        }

        TextRenderer.DrawText(graphics, emoticons, this.emoticonFont, new Point(left, top), SystemColors.GrayText, TextFlags);
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
        if (this.buttonHovered || this.buttonPressed)
        {
            using var hover = new SolidBrush(SystemColors.ControlLight);
            graphics.FillRectangle(hover, bounds);
        }

        TextRenderer.DrawText(graphics, (this.copied ? CheckGlyph : CopyGlyph).ToString(), this.iconFont, bounds,
            this.copied ? SystemColors.Highlight : SystemColors.ControlText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFlags);
    }

    private void SetButtonHovered(bool hovered)
    {
        if (hovered == this.buttonHovered)
        {
            return;
        }

        this.buttonHovered = hovered;
        this.UpdateToolTip();
        this.Invalidate(this.ButtonBounds);
    }

    // The button's tooltip: the emoji's first code point, U+1F602 — U+1F468 for a sequence.
    private void UpdateToolTip()
    {
        string? codePoint = this.buttonHovered && this.shownEmoji is Emoji emoji
            ? "U+" + emoji.Hexcode.Split('-')[0]
            : null;
        this.toolTip.SetToolTip(this, codePoint);
    }

    // The emoji itself, as text. The window stays, and the copy is no use: the frequent tab ignores it.
    private void CopyEmoji()
    {
        if (this.shownEmoji is not Emoji emoji)
        {
            return;
        }

        try
        {
            Clipboard.SetText(emoji.Text);
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

    // The shown emoji drawn at the panel's emoji size, the previous bitmap freed.
    private void RenderEmoji()
    {
        Bitmap? previous = this.emojiBitmap;
        this.emojiBitmap = this.shownEmoji is Emoji emoji ? this.renderer.Render(emoji.Text, this.EmojiSize) : null;
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
