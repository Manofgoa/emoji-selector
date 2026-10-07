namespace EmojiSelector.Data;

/// <summary>
/// A <b>category</b>: a group of emojis by theme, browsed in its tab. <paramref name="Icon"/> is its tab's glyph in
/// Segoe Fluent Icons (Segoe MDL2 Assets on Windows 10, same code points). Its section in the grid shows at most
/// <paramref name="MaxRows"/> rows when set, and <paramref name="EmptyText"/>, when set, while it has no emoji.
/// </summary>
internal sealed record EmojiCategory(string Name, char Icon, IReadOnlyList<Emoji> Emojis, int? MaxRows = null, string? EmptyText = null);
