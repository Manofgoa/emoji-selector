namespace EmojiSelector.UI;

/// <summary>
/// Where everything sits in the <see cref="EmojiGrid"/>, in content coordinates (y = 0 at the top of the first
/// section, before scrolling): one section per category — its header, then its emojis in fixed-size cells, as many
/// columns as the width holds. Every cell has the same width, so the columns line up; a section may have taller
/// cells (<see cref="Section.RowHeight"/>). Each section starts on a new row.
/// </summary>
internal sealed class EmojiGridLayout
{
    private readonly Section[] sections;
    private readonly int[] counts;
    private readonly int[] headerTops;
    private readonly int[] cellsTops;
    private readonly int width;

    /// <param name="sections">Each section's emoji count and row bounds, in order.</param>
    /// <param name="width">The width the cells fit in, padding included.</param>
    /// <param name="viewportHeight">The visible height: the content is tall enough to bring the last header to the top.</param>
    public EmojiGridLayout(IReadOnlyList<Section> sections, int width, int viewportHeight, int cellSize, int headerHeight, int padding)
    {
        this.sections = [.. sections];
        this.CellSize = cellSize;
        this.HeaderHeight = headerHeight;
        this.Padding = padding;
        this.width = width;
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
            y += this.RowCount(section) * this.RowHeight(section);
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
        this.cellsTops[section] + index / this.Columns * this.RowHeight(section),
        this.CellSize,
        this.RowHeight(section));

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
            int bottom = top + this.RowCount(section) * this.RowHeight(section);
            if (point.Y >= top && point.Y < bottom)
            {
                int index = (point.Y - top) / this.RowHeight(section) * this.Columns + column;
                return index < this.counts[section] ? (section, index) : null;
            }
        }

        return null;
    }

    /// <summary>
    /// The button at the right end of <paramref name="section"/>'s header — a custom group's "…" — in content
    /// coordinates: <paramref name="buttonWidth"/> wide, as high as the header.
    /// </summary>
    public Rectangle HeaderButton(int section, int buttonWidth) =>
        new(this.width - this.Padding - buttonWidth, this.headerTops[section], buttonWidth, this.HeaderHeight);

    /// <summary>
    /// Where an emoji dragged inside <paramref name="section"/> would be dropped for <paramref name="point"/> (content
    /// coordinates): the index of the emoji it would go before — the section's count for its end — and the top of the
    /// gap between two cells it goes in, the gap nearest the point on the point's row. A point out of the section
    /// counts as on its nearest row.
    /// </summary>
    public (int Index, Point Gap) Insertion(int section, Point point)
    {
        int count = this.counts[section];
        int rowHeight = this.RowHeight(section);
        int row = Math.Clamp((point.Y - this.cellsTops[section]) / rowHeight, 0, Math.Max(0, this.RowCount(section) - 1));
        int column = Math.Clamp((point.X - this.Padding + this.CellSize / 2) / this.CellSize, 0, this.Columns);
        int index = row * this.Columns + column;
        if (index > count)
        {
            // Past the last emoji: the gap right after it.
            index = count;
            row = Math.Max(0, count - 1) / this.Columns;
            column = count - row * this.Columns;
        }

        return (index, new Point(this.Padding + column * this.CellSize, this.cellsTops[section] + row * rowHeight));
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

            int rowHeight = this.RowHeight(section);
            int firstRow = Math.Max(0, (top - cellsTop) / rowHeight);
            int lastRow = Math.Min(this.RowCount(section) - 1, (bottom - 1 - cellsTop) / rowHeight);

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

    /// <summary>The first emoji of the grid, null when every section is empty.</summary>
    public (int Section, int Index)? First() => this.FirstOf(this.NextNonEmpty(-1));

    /// <summary>The last emoji of the grid, null when every section is empty.</summary>
    public (int Section, int Index)? Last() => this.LastOf(this.PreviousNonEmpty(this.counts.Length));

    /// <summary>The first emoji of <paramref name="section"/>, null when it is empty.</summary>
    public (int Section, int Index)? FirstOf(int section) =>
        section >= 0 && section < this.counts.Length && this.counts[section] > 0 ? (section, 0) : null;

    /// <summary>The last emoji of <paramref name="section"/>, null when it is empty.</summary>
    public (int Section, int Index)? LastOf(int section) =>
        section >= 0 && section < this.counts.Length && this.counts[section] > 0 ? (section, this.counts[section] - 1) : null;

    /// <summary>The emoji after <paramref name="cell"/> in grid order — across rows and sections; null after the last.</summary>
    public (int Section, int Index)? Next((int Section, int Index) cell) =>
        cell.Index + 1 < this.counts[cell.Section] ? (cell.Section, cell.Index + 1) : this.FirstOf(this.NextNonEmpty(cell.Section));

    /// <summary>The emoji before <paramref name="cell"/> in grid order — across rows and sections; null before the first.</summary>
    public (int Section, int Index)? Previous((int Section, int Index) cell) =>
        cell.Index > 0 ? (cell.Section, cell.Index - 1) : this.LastOf(this.PreviousNonEmpty(cell.Section));

    /// <summary>
    /// The emoji one row below <paramref name="cell"/>, same column: the next section's first row after a section's
    /// last row; a shorter row gives its last emoji. Null on the grid's last row.
    /// </summary>
    public (int Section, int Index)? Below((int Section, int Index) cell)
    {
        int row = cell.Index / this.Columns;
        int column = cell.Index % this.Columns;
        if (row + 1 < this.RowCount(cell.Section))
        {
            return (cell.Section, Math.Min((row + 1) * this.Columns + column, this.counts[cell.Section] - 1));
        }

        int next = this.NextNonEmpty(cell.Section);
        return next < this.counts.Length ? (next, Math.Min(column, this.counts[next] - 1)) : null;
    }

    /// <summary>
    /// The emoji one row above <paramref name="cell"/>, same column: the previous section's last row above a
    /// section's first row; a shorter row gives its last emoji. Null on the grid's first row.
    /// </summary>
    public (int Section, int Index)? Above((int Section, int Index) cell)
    {
        int row = cell.Index / this.Columns;
        int column = cell.Index % this.Columns;
        if (row > 0)
        {
            return (cell.Section, (row - 1) * this.Columns + column);
        }

        int previous = this.PreviousNonEmpty(cell.Section);
        if (previous < 0)
        {
            return null;
        }

        int lastRowStart = (this.RowCount(previous) - 1) * this.Columns;
        return (previous, Math.Min(lastRowStart + column, this.counts[previous] - 1));
    }

    private int NextNonEmpty(int section)
    {
        do
        {
            section++;
        }
        while (section < this.counts.Length && this.counts[section] == 0);

        return section;
    }

    private int PreviousNonEmpty(int section)
    {
        do
        {
            section--;
        }
        while (section >= 0 && this.counts[section] == 0);

        return section;
    }

    /// <summary>The height of <paramref name="section"/>'s cells: <see cref="CellSize"/> unless it has its own.</summary>
    public int RowHeight(int section) => this.sections[section].RowHeight ?? this.CellSize;

    private int RowCount(int section) =>
        Math.Max(this.sections[section].MinRows, (this.counts[section] + this.Columns - 1) / this.Columns);

    /// <summary>
    /// A section's size: its emoji count, at least <paramref name="MinRows"/> rows (room for a message when it is
    /// empty), at most <paramref name="MaxRows"/> when set; its cells <paramref name="RowHeight"/> high when set.
    /// </summary>
    public readonly record struct Section(int Count, int MinRows = 0, int? MaxRows = null, int? RowHeight = null);
}
