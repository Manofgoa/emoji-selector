using System.Diagnostics;
using System.Text.Json;

namespace EmojiSelector.Data;

/// <summary>
/// The <b>synonyms</b> of the search box: groups of equivalent words (<c>car, auto, voiture, bagnole</c>), read at launch
/// from <see cref="FileName"/> next to the exe — a file the user may edit. A typed word equal to a word of a group also
/// looks for the other words of that group, as whole keyword words (see <see cref="EmojiSearch.Find"/>). A copy of the
/// default list is embedded in the exe: it writes the file when it is missing, never over an existing one, so the
/// user's edits win over a newer exe.
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

    private const string ResourceName = "EmojiSelector.Data.Synonyms.json";

    // Lenient on what a hand edit leaves behind: comments and trailing commas are read.
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Each word of a group, normalized, to the other words of every group holding it.
    private readonly Dictionary<string, string[]> alternatives;

    private SearchSynonyms(Dictionary<string, string[]> alternatives, int groupCount)
    {
        this.alternatives = alternatives;
        this.GroupCount = groupCount;
    }

    /// <summary>No synonym at all: every word looks for itself only.</summary>
    public static SearchSynonyms Empty { get; } = new([], 0);

    /// <summary>How many groups were kept.</summary>
    public int GroupCount { get; }

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>The synonyms to use, and the reason the user's file is not valid — null when it is, or when there was none.</summary>
    public sealed record LoadResult(SearchSynonyms Synonyms, string? Error);

    /// <summary>
    /// The other words of every group holding <paramref name="word"/> — a normalized word (see
    /// <see cref="EmojiSearch.Normalize"/>) —; none when no group holds it.
    /// </summary>
    public IReadOnlyList<string> AlternativesOf(string word) =>
        this.alternatives.TryGetValue(word, out string[]? found) ? found : [];

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
    private static SearchSynonyms FromGroups(IEnumerable<IEnumerable<string>> groups)
    {
        var others = new Dictionary<string, HashSet<string>>();
        int groupCount = 0;
        foreach (IEnumerable<string> group in groups)
        {
            List<string> words = group
                .Select(EmojiSearch.Words)
                .Where(entryWords => entryWords.Length == 1)
                .Select(entryWords => entryWords[0])
                .Distinct()
                .ToList();
            if (words.Count < 2)
            {
                continue;
            }

            groupCount++;
            foreach (string word in words)
            {
                if (!others.TryGetValue(word, out HashSet<string>? set))
                {
                    set = [];
                    others.Add(word, set);
                }

                set.UnionWith(words.Where(other => other != word));
            }
        }

        return new SearchSynonyms(others.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()), groupCount);
    }

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
