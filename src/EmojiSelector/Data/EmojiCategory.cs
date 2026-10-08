namespace EmojiSelector.Data;

/// <summary>
/// A <b>category</b>: a group of emojis by theme, browsed in its tab. <paramref name="Icon"/> is its tab's glyph in
/// Segoe Fluent Icons (Segoe MDL2 Assets on Windows 10, same code points). Its section in the grid shows at most
/// <paramref name="MaxRows"/> rows when set, and <paramref name="EmptyText"/>, when set, while it has no emoji.
/// <paramref name="Captions"/>, when set, holds one short text per emoji, shown under it in its cell.
/// <paramref name="HasMenu"/> gives its section's header a "…" button (a <b>custom group</b>'s, the frequent section's).
/// </summary>
internal sealed record EmojiCategory(string Name, char Icon, IReadOnlyList<Emoji> Emojis, int? MaxRows = null, string? EmptyText = null,
    IReadOnlyList<string>? Captions = null, bool HasMenu = false);
