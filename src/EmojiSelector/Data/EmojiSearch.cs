using System.Globalization;
using System.Text;

namespace EmojiSelector.Data;

/// <summary>
/// The <b>search box</b>'s matching and ranking. The typed text is split at spaces into words; an emoji is a result
/// when <b>every typed word is contained</b> in one of the words of its <b>keywords</b>, English or French, case and
/// diacritics ignored. A typed word holding a symbol (<c>?</c>, <c>:)</c>, <c>up!</c>) is looked for in the emoji's
/// <b>symbol keywords</b>; failing that, its letters-and-digits parts are, as if typed apart (<c>d'or</c> →
/// <c>d</c>, <c>or</c>).
/// </summary>
/// <remarks>
/// A typed word's match against a keyword word is graded, best first: its tier — one of the emoji's
/// <b>characters</b> or emoticons (see <see cref="EmojiCharacters"/>), the whole word, its start, anywhere else —,
/// then the share of the word it covers (<c>caca</c> covers all of <c>caca</c>, 44 % of <c>cacahuete</c>), then a
/// name's word before a tag's. Each typed word keeps its best match in the emoji. The results are ranked by the worst
/// tier among the typed words, then the lowest coverage, then the number of typed words matched in a name; ties keep
/// the catalog order.
/// <para>
/// A typed word matching nothing in an emoji may match through its <b>synonyms</b> (see <see cref="SearchSynonyms"/>):
/// a word of a group it starts, equal to a whole keyword word. Such an emoji goes to a second list, after every emoji
/// whose typed words all match on their own; among them, the same keys, a synonym counting as a tag's whole word.
/// </para>
/// </remarks>
internal static class EmojiSearch
{
    // The tiers of a match, best first.
    private const int Character = 0;
    private const int Exact = 1;
    private const int Start = 2;
    private const int Inside = 3;

    // The prefixes of near mode, case ignored.
    private static readonly string[] NearPrefixes = ["near:", "n:"];

    /// <summary>
    /// The term of a <b>near mode</b> text — one starting with <c>n:</c> or <c>near:</c>, case and the spaces before the
    /// prefix ignored —: what follows the prefix, trimmed (<c>  N: chien</c> → <c>chien</c>, <c>n:</c> → empty). Null
    /// for any other text: a normal search.
    /// </summary>
    public static string? NearTerm(string text)
    {
        string trimmed = text.TrimStart();
        foreach (string prefix in NearPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[prefix.Length..].Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// The emojis matching <paramref name="text"/>, in two lists, each most relevant first; none for a blank text.
    /// <see cref="SearchResults.Direct"/>: every typed word matched on its own. <see cref="SearchResults.ViaSynonyms"/>:
    /// a typed word matching nothing in the emoji matched through its <paramref name="synonyms"/> — a word of a group it
    /// starts, as a whole keyword word (see <see cref="SearchSynonyms.AlternativesOf"/>). An emoji is in one list only.
    /// </summary>
    public static SearchResults Find(IReadOnlyList<EmojiCategory> categories, string text, SearchSynonyms? synonyms = null)
    {
        TypedWord[] typed = TypedWords(text);
        if (typed.Length == 0)
        {
            return SearchResults.None;
        }

        IReadOnlyList<string>[] alternatives = AlternativesOf(typed, synonyms);

        // OrderBy is a stable sort: equal ranks keep the catalog order.
        var ranked = categories
            .SelectMany(category => category.Emojis)
            .Select(emoji => (Emoji: emoji, Rank: RankOf(emoji, typed, alternatives)))
            .Where(result => result.Rank is not null)
            .Select(result => (result.Emoji, Rank: result.Rank!.Value))
            .OrderBy(result => result.Rank.WorstTier)
            .ThenByDescending(result => result.Rank.LowestCoverage)
            .ThenByDescending(result => result.Rank.NameMatches)
            .ToList();
        return new SearchResults(
            [.. ranked.Where(result => !result.Rank.ViaSynonym).Select(result => result.Emoji)],
            [.. ranked.Where(result => result.Rank.ViaSynonym).Select(result => result.Emoji)]);
    }

    /// <summary>
    /// Lower case, diacritics removed (<c>é</c> → <c>e</c>, <c>œ</c> → <c>oe</c>), the typographic apostrophe read as
    /// a straight one — every other character kept: the form of the symbol keywords (<c>Up!</c> → <c>up!</c>).
    /// </summary>
    public static string Fold(string text)
    {
        string decomposed = text.ToLowerInvariant().Replace("œ", "oe").Replace("æ", "ae").Replace('’', '\'')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Folded (see <see cref="Fold"/>), every character but letters and digits turned into a space.
    /// </summary>
    public static string Normalize(string text)
    {
        string folded = Fold(text);
        var builder = new StringBuilder(folded.Length);
        foreach (char character in folded)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return builder.ToString();
    }

    /// <summary>The normalized words of <paramref name="text"/>: <c>Tête de chat</c> → <c>tete</c>, <c>de</c>, <c>chat</c>.</summary>
    public static string[] Words(string text) => Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// The folded words of <paramref name="text"/>, split at spaces, that hold a symbol — a character neither a letter
    /// nor a digit: <c>médaille d’or</c> → <c>d'or</c>; <c>keycap: #</c> → <c>keycap:</c>, <c>#</c>.
    /// </summary>
    public static IEnumerable<string> SymbolWords(string text) =>
        Fold(text).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Where(HasSymbol);

    /// <summary>
    /// The parts of <paramref name="raw"/> — a name, tags, emoticons — matched by the words of
    /// <paramref name="text"/>, as the search matches them: every occurrence of every typed word inside a word, case
    /// and diacritics ignored — a typed word holding a symbol with its symbols, or its letters-and-digits parts when
    /// <paramref name="raw"/> does not hold it whole. Positions in <paramref name="raw"/> as written, overlapping
    /// matches merged, in order; none for a blank text. The <paramref name="synonyms"/> of each typed word are matched
    /// too, as whole words only — as the search matches them.
    /// </summary>
    public static IReadOnlyList<(int Start, int Length)> MatchSpans(string raw, string text, SearchSynonyms? synonyms = null)
    {
        TypedWord[] typed = TypedWords(text);
        if (typed.Length == 0)
        {
            return [];
        }

        IReadOnlyList<string>[] alternatives = AlternativesOf(typed, synonyms);
        var normalized = new MappedText(raw, Normalize);
        var folded = new MappedText(raw, Fold);
        var spans = new List<(int Start, int End)>();
        for (int index = 0; index < typed.Length; index++)
        {
            foreach (string alternative in alternatives[index])
            {
                normalized.AddMatches(alternative, spans, wholeWord: true);
            }

            TypedWord word = typed[index];
            if (word.HasSymbol && folded.AddMatches(word.Folded, spans))
            {
                continue;
            }

            foreach (string part in word.Parts)
            {
                normalized.AddMatches(part, spans);
            }
        }

        spans.Sort();
        var merged = new List<(int Start, int Length)>();
        int mergedStart = -1;
        int mergedEnd = -1;
        foreach ((int start, int end) in spans)
        {
            if (start > mergedEnd)
            {
                if (mergedStart >= 0)
                {
                    merged.Add((mergedStart, mergedEnd - mergedStart));
                }

                mergedStart = start;
            }

            mergedEnd = Math.Max(mergedEnd, end);
        }

        if (mergedStart >= 0)
        {
            merged.Add((mergedStart, mergedEnd - mergedStart));
        }

        return merged;
    }

    private static bool HasSymbol(string word) => word.Any(character => !char.IsLetterOrDigit(character));

    // A word folding to nothing — combining marks alone — is left out: it would be contained in every keyword.
    private static TypedWord[] TypedWords(string text) => text
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .Select(word => new TypedWord(Fold(word), Words(word)))
        .Where(word => word.Folded.Length > 0)
        .ToArray();

    // The synonyms of each typed word, in the same order: only a word without symbol has some — typed whole, its
    // folded form is its normalized word.
    private static IReadOnlyList<string>[] AlternativesOf(TypedWord[] typed, SearchSynonyms? synonyms) =>
        [.. typed.Select(word => synonyms is null || word.HasSymbol ? [] : synonyms.AlternativesOf(word.Folded))];

    // Null when a typed word matches nothing in the emoji, neither on its own nor through one of its synonyms.
    private static Rank? RankOf(Emoji emoji, TypedWord[] typed, IReadOnlyList<string>[] alternatives)
    {
        int worstTier = Character;
        double lowestCoverage = 1;
        int nameMatches = 0;
        bool viaSynonym = false;
        for (int index = 0; index < typed.Length; index++)
        {
            Match[]? matches = MatchesOf(emoji, typed[index]);
            if (matches is null && SynonymMatch(emoji, alternatives[index]) is Match synonym)
            {
                matches = [synonym];
                viaSynonym = true;
            }

            if (matches is null)
            {
                return null;
            }

            foreach (Match match in matches)
            {
                worstTier = Math.Max(worstTier, match.Tier);
                lowestCoverage = Math.Min(lowestCoverage, match.Coverage);
                if (match.IsName)
                {
                    nameMatches++;
                }
            }
        }

        return new Rank(viaSynonym, worstTier, lowestCoverage, nameMatches);
    }

    // A synonym against the emoji's keywords: a whole keyword word only (car must not find cardigan), counted as a
    // tag's whole word. Null when none of them is one.
    private static Match? SynonymMatch(Emoji emoji, IReadOnlyList<string> alternatives)
    {
        foreach (string alternative in alternatives)
        {
            if (emoji.Keywords.Any(keyword => keyword.Word == alternative))
            {
                return new Match(Exact, 1, IsName: false);
            }
        }

        return null;
    }

    // One typed word against the emoji: one match — or, for a word holding a symbol that no symbol keyword holds, one
    // per letters-and-digits part, as if typed apart. Null when it does not match.
    private static Match[]? MatchesOf(Emoji emoji, TypedWord word)
    {
        if (emoji.CharacterKeys.Contains(word.Folded))
        {
            return [new Match(Character, 1, IsName: true)];
        }

        if (!word.HasSymbol)
        {
            return BestMatch(emoji.Keywords, word.Folded) is Match match ? [match] : null;
        }

        if (BestMatch(emoji.SymbolKeywords, word.Folded) is Match symbol)
        {
            return [symbol];
        }

        if (word.Parts.Length == 0)
        {
            return null;
        }

        var parts = new Match[word.Parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            if (BestMatch(emoji.Keywords, word.Parts[index]) is not Match part)
            {
                return null;
            }

            parts[index] = part;
        }

        return parts;
    }

    private static Match? BestMatch(IReadOnlyList<EmojiKeyword> keywords, string typed)
    {
        Match? best = null;
        foreach (EmojiKeyword keyword in keywords)
        {
            int position = keyword.Word.IndexOf(typed, StringComparison.Ordinal);
            if (position < 0)
            {
                continue;
            }

            int tier = keyword.Word.Length == typed.Length ? Exact : position == 0 ? Start : Inside;
            var match = new Match(tier, (double)typed.Length / keyword.Word.Length, keyword.IsName);
            if (best is not Match current || IsBetter(match, current))
            {
                best = match;
            }
        }

        return best;
    }

    private static bool IsBetter(Match match, Match than) =>
        match.Tier != than.Tier ? match.Tier < than.Tier
        : match.Coverage != than.Coverage ? match.Coverage > than.Coverage
        : match.IsName && !than.IsName;

    // A typed word: folded (see Fold), and its letters-and-digits parts — the word itself when it holds no symbol.
    private readonly record struct TypedWord(string Folded, string[] Parts)
    {
        public bool HasSymbol => this.Parts.Length != 1 || this.Parts[0] != this.Folded;
    }

    // A typed word against one keyword word.
    private readonly record struct Match(int Tier, double Coverage, bool IsName);

    // An emoji against every typed word. ViaSynonym: a typed word matched only through one of its synonyms.
    private readonly record struct Rank(bool ViaSynonym, int WorstTier, double LowestCoverage, int NameMatches);

    // A raw text normalized one character at a time, each normalized character remembering the raw characters it
    // stands for: œ gives two (oe), so a match on either covers the whole œ; a combining mark gives none and joins the
    // character before it.
    private sealed class MappedText
    {
        private readonly string text;
        private readonly List<int> starts;
        private readonly List<int> ends;

        public MappedText(string raw, Func<string, string> normalize)
        {
            var normalized = new StringBuilder(raw.Length);
            this.starts = new List<int>(raw.Length);
            this.ends = new List<int>(raw.Length);
            for (int index = 0; index < raw.Length;)
            {
                int length = char.IsSurrogatePair(raw, index) ? 2 : 1;
                string part = normalize(raw.Substring(index, length));
                normalized.Append(part);
                for (int i = 0; i < part.Length; i++)
                {
                    this.starts.Add(index);
                    this.ends.Add(index + length);
                }

                if (part.Length == 0 && this.ends.Count > 0)
                {
                    this.ends[^1] = index + length;
                }

                index += length;
            }

            this.text = normalized.ToString();
        }

        // Adds every occurrence of word, as positions in the raw text — only those standing as a whole word, between
        // two non-letters, with wholeWord —; false when there is none.
        public bool AddMatches(string word, List<(int Start, int End)> spans, bool wholeWord = false)
        {
            bool found = false;
            for (int at = this.text.IndexOf(word, StringComparison.Ordinal); at >= 0;
                at = this.text.IndexOf(word, at + 1, StringComparison.Ordinal))
            {
                int end = at + word.Length;
                if (wholeWord && ((at > 0 && char.IsLetterOrDigit(this.text[at - 1]))
                    || (end < this.text.Length && char.IsLetterOrDigit(this.text[end]))))
                {
                    continue;
                }

                spans.Add((this.starts[at], this.ends[end - 1]));
                found = true;
            }

            return found;
        }
    }
}
