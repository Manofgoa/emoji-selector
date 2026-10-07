namespace EmojiSelector.Data;

/// <summary>
/// A <b>category</b>: a group of emojis by theme, browsed in its tab. <paramref name="Icon"/> is its tab's glyph in
/// Segoe Fluent Icons (Segoe MDL2 Assets on Windows 10, same code points).
/// </summary>
internal sealed record EmojiCategory(string Name, char Icon, IReadOnlyList<Emoji> Emojis);
