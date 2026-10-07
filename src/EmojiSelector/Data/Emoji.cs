namespace EmojiSelector.Data;

/// <summary>
/// An <b>emoji</b>: its Unicode character sequence, its name (e.g. <c>Grinning face</c>) and the words of its
/// <b>keywords</b>, in English and in French, normalized for the search box (see <see cref="EmojiSearch"/>).
/// </summary>
internal sealed record Emoji(string Text, string Name, IReadOnlyList<EmojiKeyword> Keywords);
