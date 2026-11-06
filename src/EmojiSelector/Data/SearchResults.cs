namespace EmojiSelector.Data;

/// <summary>
/// What <see cref="EmojiSearch.Find"/> found, in two lists, each most relevant first: the emojis every typed word
/// matched on its own, then the ones found through a synonym — shown in a section of their own, so they never mix with
/// the real results. An emoji is in one list only.
/// </summary>
internal sealed record SearchResults(IReadOnlyList<Emoji> Direct, IReadOnlyList<Emoji> ViaSynonyms)
{
    /// <summary>Nothing found: a blank text.</summary>
    public static SearchResults None { get; } = new([], []);

    /// <summary>Both lists in one, the direct results first: near mode's jump order.</summary>
    public IReadOnlyList<Emoji> All => [.. this.Direct, .. this.ViaSynonyms];
}
