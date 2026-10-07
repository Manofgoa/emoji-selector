namespace EmojiSelector.Data;

/// <summary>An <b>emoji</b>: its Unicode character sequence and its name (e.g. <c>Grinning face</c>).</summary>
internal sealed record Emoji(string Text, string Name);
