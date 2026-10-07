namespace EmojiSelector.UI;

/// <summary>
/// Where everything sits in the <see cref="EmojiGrid"/>, in content coordinates (y = 0 at the top of the first
/// section, before scrolling): one section per category — its header, then its emojis in fixed-size cells, as many
/// columns as the width holds. Each section starts on a new row.
/// </summary>
internal sealed class EmojiGridLayout
{
    private readonly Section[] sections;
    private readonly int[] counts;
    private readonly int[] headerTops;
    private readonly int[] cellsTops;

    /// <param name="sections">Each section's emoji count and row bounds, in order.</param>
    /// <param name="width">The width the cells fit in, padding included.</param>
    /// <param name="viewportHeight">The visible height: the content is tall enough to bring the last header to the top.</param>
    public EmojiGridLayout(IReadOnlyList<Section> sections, int width, int viewportHeight, int cellSize, int headerHeight, int padding)
    {
        this.sections = [.. sections];
        this.CellSize = cellSize;
        this.HeaderHeight = headerHeight;
        this.Padding = padding;
        this.Columns = Math.Max(1, (width - 2 * padding) / cellSize);

        // A section with a row limit shows only the emojis its rows hold: more or fewer as the columns change.
        this.counts = [.. sections.Select(section =>
            section.MaxRows is int maxRows ? Math.Min(section.Count, maxRows * this.Columns) : section.Count)];
        this.headerTops = new int[this.counts.Length];
        this.cellsTops = new int[this.counts.Length];

        int y = 0;
        for (int section = 0; section < this.counts.Length; section++)
        {
            this.headerTops[section] = y;
            y += headerHeight;
            this.cellsTops[section] = y;
            y += this.RowCount(section) * cellSize;
        }

        // Without the room below the last section, a short last section could never scroll its header to the top:
        // its tab could not become the active one.
        int lastHeaderTop = this.counts.Length == 0 ? 0 : this.headerTops[^1];
        this.ContentHeight = Math.Max(y + padding, lastHeaderTop + viewportHeight);
    }

    public int Columns { get; }

    public int CellSize { get; }

    public int HeaderHeight { get; }

    public int Padding { get; }

    public int ContentHeight { get; }

    public int SectionCount => this.counts.Length;

    /// <summary>The top of <paramref name="section"/>'s header: the scroll offset bringing it to the top.</summary>
    public int HeaderTop(int section) => this.headerTops[section];

    /// <summary>The cell of an emoji, in content coordinates.</summary>
    public Rectangle CellBounds(int section, int index) => new(
        this.Padding + index % this.Columns * this.CellSize,
        this.cellsTops[section] + index / this.Columns * this.CellSize,
        this.CellSize,
        this.CellSize);

    /// <summary>
    /// The section whose tab is active at scroll offset <paramref name="offset"/>: the last one whose header is at
    /// or above the top of the viewport.
    /// </summary>
    public int SectionAt(int offset)
    {
        int section = 0;
        while (section + 1 < this.headerTops.Length && this.headerTops[section + 1] <= offset)
        {
            section++;
        }

        return section;
    }

    /// <summary>The emoji under <paramref name="point"/> (content coordinates), null over a header or a blank.</summary>
    public (int Section, int Index)? HitTest(Point point)
    {
        int column = (point.X - this.Padding) / this.CellSize;
        if (point.X < this.Padding || column >= this.Columns)
        {
            return null;
        }

        for (int section = 0; section < this.counts.Length; section++)
        {
            int top = this.cellsTops[section];
            int bottom = top + this.RowCount(section) * this.CellSize;
            if (point.Y >= top && point.Y < bottom)
            {
                int index = (point.Y - top) / this.CellSize * this.Columns + column;
                return index < this.counts[section] ? (section, index) : null;
            }
        }

        return null;
    }

    /// <summary>The sections' cells overlapping the band [top, bottom), for painting the visible ones only.</summary>
    public IEnumerable<(int Section, int Index)> CellsIn(int top, int bottom)
    {
        for (int section = 0; section < this.counts.Length; section++)
        {
            int cellsTop = this.cellsTops[section];
            if (bottom <= cellsTop)
            {
                continue;
            }

            int firstRow = Math.Max(0, (top - cellsTop) / this.CellSize);
            int lastRow = Math.Min(this.RowCount(section) - 1, (bottom - 1 - cellsTop) / this.CellSize);

            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int column = 0; column < this.Columns; column++)
                {
                    int index = row * this.Columns + column;
                    if (index >= this.counts[section])
                    {
                        break;
                    }

                    yield return (section, index);
                }
            }
        }
    }

    private int RowCount(int section) =>
        Math.Max(this.sections[section].MinRows, (this.counts[section] + this.Columns - 1) / this.Columns);

    /// <summary>
    /// A section's size: its emoji count, at least <paramref name="MinRows"/> rows (room for a message when it is
    /// empty), at most <paramref name="MaxRows"/> when set.
    /// </summary>
    public readonly record struct Section(int Count, int MinRows = 0, int? MaxRows = null);
}
