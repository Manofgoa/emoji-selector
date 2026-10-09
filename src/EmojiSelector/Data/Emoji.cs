namespace EmojiSelector.Data;

/// <summary>
/// An <b>emoji</b>: its Unicode character sequence, its name (e.g. <c>Grinning face</c>) and the words of its
/// <b>keywords</b>, in English and in French, normalized for the search box (see <see cref="EmojiSearch"/>). The
/// <b>details panel</b> shows the rest, as Emojibase gives it.
/// </summary>
internal sealed record Emoji(string Text, string Name, IReadOnlyList<EmojiKeyword> Keywords)
{
    /// <summary>Emojibase's hexcode: the code points, hyphen-separated (<c>1F602</c>, <c>1F468-200D-1F469</c>).</summary>
    public string Hexcode { get; init; } = "";

    /// <summary>The French name, capitalized like <see cref="Name"/>; empty when the French data lacks the emoji.</summary>
    public string FrenchName { get; init; } = "";

    /// <summary>The English tags, as written (<c>laugh</c>, <c>lol</c>).</summary>
    public IReadOnlyList<string> EnglishTags { get; init; } = [];

    /// <summary>The French tags, as written (<c>pleurer de rire</c>, <c>émoticône</c>).</summary>
    public IReadOnlyList<string> FrenchTags { get; init; } = [];

    /// <summary>The emoticons standing for the emoji (<c>:')</c>); most emojis have none.</summary>
    public IReadOnlyList<string> Emoticons { get; init; } = [];

    /// <summary>
    /// The words of its names and tags that hold a symbol, and its emoticons, folded for the search box
    /// (<see cref="EmojiSearch.Fold"/>): <c>?</c>, <c>up!</c>, <c>:)</c>.
    /// </summary>
    public IReadOnlyList<EmojiKeyword> SymbolKeywords { get; init; } = [];

    /// <summary>
    /// Its <b>characters</b> (see <see cref="EmojiCharacters"/>) and its emoticons, folded: a typed word equal to one
    /// ranks the emoji first.
    /// </summary>
    public IReadOnlyList<string> CharacterKeys { get; init; } = [];

    /// <summary>
    /// Its skin-tone variants, in Emojibase's order — five for a one-person emoji, up to twenty-five for a two-person
    /// one; most emojis have none. The emoji itself (<see cref="Text"/>) is the yellow one.
    /// </summary>
    public IReadOnlyList<SkinVariant> Variants { get; init; } = [];

    /// <summary>Whether its variants give each person a tone of their own (🤝, 🧑‍🤝‍🧑): one of them has two different tones.</summary>
    public bool IsTwoPerson { get; init; }
}
