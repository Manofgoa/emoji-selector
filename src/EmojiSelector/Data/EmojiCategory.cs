namespace EmojiSelector.Data;

/// <summary>
/// A <b>category</b>: a group of emojis by theme, browsed in its tab. <paramref name="Icon"/> is its tab's glyph in
/// Segoe Fluent Icons (Segoe MDL2 Assets on Windows 10, same code points). Its section in the grid shows at most
/// <paramref name="MaxRows"/> rows when set, and <paramref name="EmptyText"/>, when set, while it has no emoji.
/// <paramref name="Captions"/>, when set, holds one short text per emoji, shown under it in its cell.
/// <paramref name="HasMenu"/> gives its section's header a "…" button (a <b>custom group</b>'s, the frequent section's).
/// <paramref name="SubCategories"/>, when set, splits its emojis into <b>sub-categories</b>, in order: each one starts a
/// row of the grid, its name at the start of that row.
/// </summary>
internal sealed record EmojiCategory(string Name, char Icon, IReadOnlyList<Emoji> Emojis, int? MaxRows = null, string? EmptyText = null,
    IReadOnlyList<string>? Captions = null, bool HasMenu = false, IReadOnlyList<SubCategory>? SubCategories = null);

/// <summary>
/// A <b>sub-category</b> of an <see cref="EmojiCategory"/>: its name (<c>Smiling</c>, <c>Currencies</c>) and the index
/// of its first emoji in the category; it runs up to the next one's.
/// </summary>
internal sealed record SubCategory(string Name, int Start);
