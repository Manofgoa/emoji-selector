using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace EmojiSelector.Data;

/// <summary>
/// The <b>synonyms</b> of the search box: groups of equivalent words (<c>car, auto, voiture, bagnole</c>), read at launch
/// from <see cref="FileName"/> next to the exe — a file the user may edit. A typed word starting a word of a group —
/// <see cref="MinPrefixLength"/> letters at least, <c>bag</c> starts <c>bagnole</c> — also looks for the words of that
/// group, as whole keyword words (see <see cref="EmojiSearch.Find"/>); the details panel lists an emoji's synonyms
/// (<see cref="SynonymsOf"/>). A copy of the default list is embedded in the exe: it writes the file when it is missing,
/// never over an existing one, so the user's edits win over a newer exe.
/// </summary>
/// <remarks>
/// An invalid file — not JSON, or without a <c>groups</c> list — is kept as it is, for the user to fix: the embedded
/// list is used meanwhile, and <see cref="LoadResult.Error"/> says why. Inside a valid file, a group that is not a list
/// is left out, an entry that is not one word once normalized (<c>pomme de terre</c>, <c>:)</c>) is left out, and a
/// group left with fewer than two words is left out. A folder that cannot be written (an exe under Program Files) is not
/// an error: the embedded list is used in memory.
/// </remarks>
internal sealed class SearchSynonyms
{
    public const string FileName = "synonyms.json";

    /// <summary>The file being written, before it replaces <see cref="FileName"/>.</summary>
    public const string TemporaryFileName = FileName + ".tmp";

    /// <summary>The fewest letters a typed word needs to expand: one letter would start half the dictionary.</summary>
    public const int MinPrefixLength = 2;

    private const string ResourceName = "EmojiSelector.Data.Synonyms.json";

    // Lenient on what a hand edit leaves behind: comments and trailing commas are read.
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // The groups kept, in the file's order, their words normalized; each word to the groups holding it, in order.
    private readonly string[][] groups;
    private readonly Dictionary<string, int[]> groupsOfWord;

    // The words of every group, sorted (ordinal): the ones a typed start begins sit together.
    private readonly string[] sortedWords;

    // The synonyms of each emoji asked for, by reference: the details panel asks for the whole catalog's.
    private readonly ConditionalWeakTable<Emoji, IReadOnlyList<string>> synonymsOfEmoji = [];

    private SearchSynonyms(string[][] groups)
    {
        this.groups = groups;
        this.groupsOfWord = groups
            .SelectMany((words, group) => words.Select(word => (Word: word, Group: group)))
            .GroupBy(pair => pair.Word)
            .ToDictionary(byWord => byWord.Key, byWord => byWord.Select(pair => pair.Group).ToArray());
        this.sortedWords = [.. this.groupsOfWord.Keys.Order(StringComparer.Ordinal)];
    }

    /// <summary>No synonym at all: every word looks for itself only.</summary>
    public static SearchSynonyms Empty { get; } = new([]);

    /// <summary>How many groups were kept.</summary>
    public int GroupCount => this.groups.Length;

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>The synonyms to use, and the reason the user's file is not valid — null when it is, or when there was none.</summary>
    public sealed record LoadResult(SearchSynonyms Synonyms, string? Error);

    /// <summary>
    /// The words <paramref name="typed"/> — a normalized word (see <see cref="EmojiSearch.Normalize"/>) — also looks
    /// for: every word of every group holding a word it starts or equals (<c>bag</c> → the group of <c>bagnole</c>, so
    /// <c>car</c>, <c>voiture</c>…), each once, in the file's order. None under <see cref="MinPrefixLength"/> letters,
    /// or when no group word starts with it.
    /// </summary>
    public IReadOnlyList<string> AlternativesOf(string typed)
    {
        if (typed.Length < MinPrefixLength)
        {
            return [];
        }

        // The first word not before typed, then every word starting with it.
        int first = Array.BinarySearch(this.sortedWords, typed, StringComparer.Ordinal);
        if (first < 0)
        {
            first = ~first;
        }

        var matched = new SortedSet<int>();
        for (int index = first;
            index < this.sortedWords.Length && this.sortedWords[index].StartsWith(typed, StringComparison.Ordinal);
            index++)
        {
            matched.UnionWith(this.groupsOfWord[this.sortedWords[index]]);
        }

        return [.. matched.SelectMany(group => this.groups[group]).Distinct()];
    }

    /// <summary>
    /// The synonyms of <paramref name="emoji"/>, for the details panel: the other words of every group holding one of
    /// its keyword words, the words among its keyword words left out — each once, in the file's order. 🚗 →
    /// <c>bagnole</c>, <c>tuture</c>…; none when no group holds one of its words.
    /// </summary>
    public IReadOnlyList<string> SynonymsOf(Emoji emoji) => this.synonymsOfEmoji.GetValue(emoji, this.ComputeSynonymsOf);

    private IReadOnlyList<string> ComputeSynonymsOf(Emoji emoji)
    {
        HashSet<string> own = emoji.Keywords.Select(keyword => keyword.Word).ToHashSet(StringComparer.Ordinal);
        var matched = new SortedSet<int>();
        foreach (string word in own)
        {
            if (this.groupsOfWord.TryGetValue(word, out int[]? holding))
            {
                matched.UnionWith(holding);
            }
        }

        return matched.Count == 0
            ? []
            : [.. matched.SelectMany(group => this.groups[group]).Where(word => !own.Contains(word)).Distinct()];
    }

    /// <summary>
    /// The synonyms of <see cref="FileName"/> when it is valid. Missing, the file is written from the embedded list, then
    /// that list is used; invalid or unreadable, the embedded list is used and the file left as it is.
    /// </summary>
    public static LoadResult Load()
    {
        byte[] embedded = ReadEmbedded();
        SearchSynonyms embeddedSynonyms = Parse(embedded, out _)
            ?? throw new InvalidOperationException("The embedded synonyms are not valid.");

        byte[]? file;
        try
        {
            file = File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"SearchSynonyms: {FileName} not read — {exception.Message}");
            return new LoadResult(embeddedSynonyms, null);
        }

        if (file is null)
        {
            Debug.WriteLine($"SearchSynonyms: {FileName} missing — written from the embedded copy");
            try
            {
                Write(embedded);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"SearchSynonyms: {FileName} not written — {exception.Message}");
            }

            return new LoadResult(embeddedSynonyms, null);
        }

        if (Parse(file, out string? error) is SearchSynonyms synonyms)
        {
            return new LoadResult(synonyms, null);
        }

        Debug.WriteLine($"SearchSynonyms: {FileName} invalid — the embedded copy used, the file kept: {error}");
        return new LoadResult(embeddedSynonyms, error);
    }

    /// <summary>
    /// Parses a synonyms file: <c>{ "groups": [ ["car", "voiture"], … ] }</c>. Null when it is not valid — not JSON, or
    /// no <c>groups</c> list —, <paramref name="error"/> then saying why.
    /// </summary>
    public static SearchSynonyms? Parse(byte[] json, out string? error)
    {
        ReadOnlyMemory<byte> content = json;
        if (json.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            content = content[3..];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content, JsonOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("groups", out JsonElement groups)
                || groups.ValueKind != JsonValueKind.Array)
            {
                error = "no \"groups\" list.";
                return null;
            }

            error = null;
            return FromGroups(groups.EnumerateArray()
                .Where(group => group.ValueKind == JsonValueKind.Array)
                .Select(group => group.EnumerateArray()
                    .Where(entry => entry.ValueKind == JsonValueKind.String)
                    .Select(entry => entry.GetString()!)));
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return null;
        }
    }

    // Each entry normalized to one word — none or several left out —, each group kept with two words at least.
    private static SearchSynonyms FromGroups(IEnumerable<IEnumerable<string>> groups) => new(
    [
        .. groups
            .Select(group => group
                .Select(EmojiSearch.Words)
                .Where(entryWords => entryWords.Length == 1)
                .Select(entryWords => entryWords[0])
                .Distinct()
                .ToArray())
            .Where(words => words.Length >= 2),
    ]);

    // Written through a temporary file then a replace: a crash never leaves it half-written. Throws IOException or
    // UnauthorizedAccessException when it cannot be written; the temporary file is then deleted.
    private static void Write(byte[] content)
    {
        string temporary = Path.Combine(AppContext.BaseDirectory, TemporaryFileName);
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception deleteException) when (deleteException is IOException or UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    private static byte[] ReadEmbedded()
    {
        using Stream stream = typeof(SearchSynonyms).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
