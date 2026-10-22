namespace EmojiSelector.UI;

/// <summary>
/// Where everything sits in the <see cref="EmojiGrid"/>, in content coordinates (y = 0 at the top of the first
/// section, before scrolling): one section per category — its header, then its emojis in fixed-size cells, as many
/// columns as the width holds. Every cell has the same width, so the columns line up; a section may have taller
/// cells (<see cref="Section.RowHeight"/>). Each section starts on a new row.
/// </summary>
/// <remarks>
/// A section with <b>sub-categories</b> (<see cref="Section.Breaks"/>) starts a new row at each of them, and leaves
/// the label column on its left — <see cref="LabelWidth"/>, the same for every such section, so their cells line up
/// too: it holds fewer columns. A sub-category longer than a row continues on the next ones.
/// </remarks>
internal sealed class EmojiGridLayout
{
    private readonly Section[] sections;
    private readonly int[] counts;
    private readonly int[] headerTops;
    private readonly int[] cellsTops;

    // Each section's left edge, column count, and the index of the first emoji of each of its rows.
    private readonly int[] lefts;
    private readonly int[] columns;
    private readonly int[][] rowStarts;
    private readonly int width;

    /// <param name="sections">Each section's emoji count and row bounds, in order.</param>
    /// <param name="width">The width the cells fit in, padding included.</param>
    /// <param name="viewportHeight">The visible height: the content is tall enough to bring the last header to the top.</param>
    /// <param name="labelWidth">The label column of the sections with sub-categories, 0 when there is none.</param>
    public EmojiGridLayout(IReadOnlyList<Section> sections, int width, int viewportHeight, int cellSize, int headerHeight, int padding,
        int labelWidth = 0)
    {
        this.sections = [.. sections];
        this.CellSize = cellSize;
        this.HeaderHeight = headerHeight;
        this.Padding = padding;
        this.LabelWidth = labelWidth;
        this.width = width;
        this.Columns = Math.Max(1, (width - 2 * padding) / cellSize);
        int labelledColumns = Math.Max(1, (width - 2 * padding - labelWidth) / cellSize);

        this.lefts = [.. sections.Select(section => section.Breaks is null ? padding : padding + labelWidth)];
        this.columns = [.. sections.Select(section => section.Breaks is null ? this.Columns : labelledColumns)];

        // A section with a row limit shows only the emojis its rows hold: more or fewer as the columns change.
        this.counts = [.. sections.Select((section, index) =>
            section.MaxRows is int maxRows ? Math.Min(section.Count, maxRows * this.columns[index]) : section.Count)];
        this.rowStarts = [.. sections.Select((section, index) => RowStartsOf(section.Breaks, this.counts[index], this.columns[index]))];
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

    /// <summary>The columns of a section without sub-categories.</summary>
    public int Columns { get; }

    public int CellSize { get; }

    public int HeaderHeight { get; }

    public int Padding { get; }

    /// <summary>The label column of the sections with sub-categories, 0 when there is none.</summary>
    public int LabelWidth { get; }

    public int ContentHeight { get; }

    public int SectionCount => this.counts.Length;

    /// <summary>The top of <paramref name="section"/>'s header: the scroll offset bringing it to the top.</summary>
    public int HeaderTop(int section) => this.headerTops[section];

    /// <summary>The cell of an emoji, in content coordinates.</summary>
    public Rectangle CellBounds(int section, int index)
    {
        int row = this.RowOf(section, index);
        return new(
            this.lefts[section] + (index - this.rowStarts[section][row]) * this.CellSize,
            this.cellsTops[section] + row * this.RowHeight(section),
            this.CellSize,
            this.RowHeight(section));
    }

    /// <summary>
    /// The label of the sub-category starting at <paramref name="start"/> in <paramref name="section"/>, in content
    /// coordinates: the label column, on the sub-category's first row.
    /// </summary>
    public Rectangle LabelBounds(int section, int start)
    {
        Rectangle cell = this.CellBounds(section, start);
        return new(this.Padding, cell.Top, this.LabelWidth, cell.Height);
    }

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

    /// <summary>The emoji under <paramref name="point"/> (content coordinates), null over a header, a label or a blank.</summary>
    public (int Section, int Index)? HitTest(Point point)
    {
        for (int section = 0; section < this.counts.Length; section++)
        {
            int top = this.cellsTops[section];
            int bottom = top + this.RowCount(section) * this.RowHeight(section);
            if (point.Y >= top && point.Y < bottom)
            {
                int row = (point.Y - top) / this.RowHeight(section);
                int column = (point.X - this.lefts[section]) / this.CellSize;
                if (point.X < this.lefts[section] || column >= this.columns[section] || row >= this.rowStarts[section].Length)
                {
                    return null;
                }

                return column < this.RowLength(section, row) ? (section, this.rowStarts[section][row] + column) : null;
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
        int rowHeight = this.RowHeight(section);
        int rows = this.rowStarts[section].Length;
        if (rows == 0)
        {
            return (0, new Point(this.lefts[section], this.cellsTops[section]));
        }

        int row = Math.Clamp((point.Y - this.cellsTops[section]) / rowHeight, 0, rows - 1);
        int column = Math.Clamp((point.X - this.lefts[section] + this.CellSize / 2) / this.CellSize, 0, this.RowLength(section, row));
        return (this.rowStarts[section][row] + column,
            new Point(this.lefts[section] + column * this.CellSize, this.cellsTops[section] + row * rowHeight));
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
            int lastRow = Math.Min(this.rowStarts[section].Length - 1, (bottom - 1 - cellsTop) / rowHeight);

            for (int row = firstRow; row <= lastRow; row++)
            {
                int start = this.rowStarts[section][row];
                for (int column = 0; column < this.RowLength(section, row); column++)
                {
                    yield return (section, start + column);
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
        int row = this.RowOf(cell.Section, cell.Index);
        int column = cell.Index - this.rowStarts[cell.Section][row];
        if (row + 1 < this.rowStarts[cell.Section].Length)
        {
            return (cell.Section, this.CellOfRow(cell.Section, row + 1, column));
        }

        int next = this.NextNonEmpty(cell.Section);
        return next < this.counts.Length ? (next, this.CellOfRow(next, 0, column)) : null;
    }

    /// <summary>
    /// The emoji one row above <paramref name="cell"/>, same column: the previous section's last row above a
    /// section's first row; a shorter row gives its last emoji. Null on the grid's first row.
    /// </summary>
    public (int Section, int Index)? Above((int Section, int Index) cell)
    {
        int row = this.RowOf(cell.Section, cell.Index);
        int column = cell.Index - this.rowStarts[cell.Section][row];
        if (row > 0)
        {
            return (cell.Section, this.CellOfRow(cell.Section, row - 1, column));
        }

        int previous = this.PreviousNonEmpty(cell.Section);
        return previous < 0 ? null : (previous, this.CellOfRow(previous, this.rowStarts[previous].Length - 1, column));
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

    private int RowCount(int section) => Math.Max(this.sections[section].MinRows, this.rowStarts[section].Length);

    // The emojis of a row: up to the next row's first one, or the section's end.
    private int RowLength(int section, int row) =>
        (row + 1 < this.rowStarts[section].Length ? this.rowStarts[section][row + 1] : this.counts[section]) - this.rowStarts[section][row];

    // The row holding an emoji: the last one starting at or before it.
    private int RowOf(int section, int index)
    {
        int[] starts = this.rowStarts[section];
        int found = Array.BinarySearch(starts, index);
        return found >= 0 ? found : Math.Max(0, ~found - 1);
    }

    // The emoji of a row at a column; a shorter row gives its last emoji.
    private int CellOfRow(int section, int row, int column) =>
        this.rowStarts[section][row] + Math.Min(column, this.RowLength(section, row) - 1);

    // The first emoji of each row: a new row every columns emojis, and at every break — a sub-category's first emoji.
    private static int[] RowStartsOf(IReadOnlyList<int>? breaks, int count, int columns)
    {
        var starts = new List<int>();
        int[] bounds = [.. (breaks ?? []).Where(start => start > 0 && start < count).Prepend(0).Distinct().Order(), count];
        for (int run = 0; run + 1 < bounds.Length; run++)
        {
            for (int start = bounds[run]; start < bounds[run + 1]; start += columns)
            {
                starts.Add(start);
            }
        }

        return [.. starts];
    }

    /// <summary>
    /// A section's size: its emoji count, at least <paramref name="MinRows"/> rows (room for a message when it is
    /// empty), at most <paramref name="MaxRows"/> when set; its cells <paramref name="RowHeight"/> high when set.
    /// <paramref name="Breaks"/>, when set, holds its sub-categories' first emojis: each one starts a row, the label
    /// column left on their left.
    /// </summary>
    public readonly record struct Section(int Count, int MinRows = 0, int? MaxRows = null, int? RowHeight = null,
        IReadOnlyList<int>? Breaks = null);
}
