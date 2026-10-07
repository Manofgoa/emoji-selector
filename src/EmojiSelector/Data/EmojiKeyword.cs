namespace EmojiSelector.Data;

/// <summary>
/// One word of an emoji's <b>keywords</b>, normalized (see <see cref="EmojiSearch.Normalize"/>): <c>tete</c>,
/// <c>de</c> and <c>chat</c> for <c>tête de chat</c>. <paramref name="IsName"/>: it comes from the emoji's name, in
/// English or in French, rather than from one of its tags — a name match ranks first.
/// </summary>
internal sealed record EmojiKeyword(string Word, bool IsName);
