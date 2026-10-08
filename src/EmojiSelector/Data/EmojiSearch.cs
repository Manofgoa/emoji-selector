using System.Globalization;
using System.Text;

namespace EmojiSelector.Data;

/// <summary>
/// The <b>search box</b>'s matching and ranking. The typed text is split into words; an emoji is a result when
/// <b>every typed word is contained</b> in one of the words of its <b>keywords</b>, English or French, case and
/// diacritics ignored.
/// </summary>
/// <remarks>
/// A typed word's match against a keyword word is graded, best first: its tier — the whole word, then its start,
/// then anywhere else —, then the share of the word it covers (<c>caca</c> covers all of <c>caca</c>, 44 % of
/// <c>cacahuete</c>), then a name's word before a tag's. Each typed word keeps its best match in the emoji. The
/// results are ranked by the worst tier among the typed words, then the lowest coverage, then the number of typed
/// words matched in a name; ties keep the catalog order.
/// </remarks>
internal static class EmojiSearch
{
    // The tiers of a match, best first.
    private const int Exact = 0;
    private const int Start = 1;
    private const int Inside = 2;

    /// <summary>The emojis matching <paramref name="text"/>, most relevant first; none for a blank text.</summary>
    public static IReadOnlyList<Emoji> Find(IReadOnlyList<EmojiCategory> categories, string text)
    {
        string[] typed = Words(text);
        if (typed.Length == 0)
        {
            return [];
        }

        // OrderBy is a stable sort: equal ranks keep the catalog order.
        return categories
            .SelectMany(category => category.Emojis)
            .Select(emoji => (Emoji: emoji, Rank: RankOf(emoji, typed)))
            .Where(result => result.Rank is not null)
            .Select(result => (result.Emoji, Rank: result.Rank!.Value))
            .OrderBy(result => result.Rank.WorstTier)
            .ThenByDescending(result => result.Rank.LowestCoverage)
            .ThenByDescending(result => result.Rank.NameMatches)
            .Select(result => result.Emoji)
            .ToList();
    }

    /// <summary>
    /// Lower case, diacritics removed (<c>é</c> → <c>e</c>, <c>œ</c> → <c>oe</c>), every character but letters and
    /// digits turned into a space.
    /// </summary>
    public static string Normalize(string text)
    {
        string decomposed = text.ToLowerInvariant().Replace("œ", "oe").Replace("æ", "ae").Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
            }
        }

        return builder.ToString();
    }

    /// <summary>The normalized words of <paramref name="text"/>: <c>Tête de chat</c> → <c>tete</c>, <c>de</c>, <c>chat</c>.</summary>
    public static string[] Words(string text) => Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// The parts of <paramref name="raw"/> — a name, tags — matched by the words of <paramref name="text"/>, as the
    /// search matches them: every occurrence of every typed word inside a word, case and diacritics ignored. Positions
    /// in <paramref name="raw"/> as written, overlapping matches merged, in order; none for a blank text.
    /// </summary>
    /// <remarks>
    /// <paramref name="raw"/> is normalized one character at a time, each normalized character remembering the raw
    /// characters it stands for: <c>œ</c> gives two (<c>oe</c>), so a match on either covers the whole <c>œ</c>; a
    /// combining mark gives none and joins the character before it.
    /// </remarks>
    public static IReadOnlyList<(int Start, int Length)> MatchSpans(string raw, string text)
    {
        string[] typed = Words(text);
        if (typed.Length == 0)
        {
            return [];
        }

        var normalized = new StringBuilder(raw.Length);
        var starts = new List<int>(raw.Length);
        var ends = new List<int>(raw.Length);
        for (int index = 0; index < raw.Length;)
        {
            int length = char.IsSurrogatePair(raw, index) ? 2 : 1;
            string part = Normalize(raw.Substring(index, length));
            normalized.Append(part);
            for (int i = 0; i < part.Length; i++)
            {
                starts.Add(index);
                ends.Add(index + length);
            }

            if (part.Length == 0 && ends.Count > 0)
            {
                ends[^1] = index + length;
            }

            index += length;
        }

        string searched = normalized.ToString();
        var spans = new List<(int Start, int End)>();
        foreach (string word in typed)
        {
            for (int at = searched.IndexOf(word, StringComparison.Ordinal); at >= 0;
                at = searched.IndexOf(word, at + 1, StringComparison.Ordinal))
            {
                spans.Add((starts[at], ends[at + word.Length - 1]));
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

    // Null when a typed word matches none of the emoji's keyword words.
    private static Rank? RankOf(Emoji emoji, string[] typed)
    {
        int worstTier = Exact;
        double lowestCoverage = 1;
        int nameMatches = 0;
        foreach (string word in typed)
        {
            if (BestMatch(emoji.Keywords, word) is not Match match)
            {
                return null;
            }

            worstTier = Math.Max(worstTier, match.Tier);
            lowestCoverage = Math.Min(lowestCoverage, match.Coverage);
            if (match.IsName)
            {
                nameMatches++;
            }
        }

        return new Rank(worstTier, lowestCoverage, nameMatches);
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

    // A typed word against one keyword word.
    private readonly record struct Match(int Tier, double Coverage, bool IsName);

    // An emoji against every typed word.
    private readonly record struct Rank(int WorstTier, double LowestCoverage, int NameMatches);
}
