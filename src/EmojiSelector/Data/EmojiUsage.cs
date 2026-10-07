using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EmojiSelector.Data;

/// <summary>
/// The <b>frequent</b> emojis' counters: each use of an emoji adds 1 to its count and sets its last use, saved in
/// <see cref="FileName"/> next to the exe — read at launch, written after each change. A missing or invalid file is
/// no counter yet; a folder that cannot be written keeps the counters in memory until the app ends.
/// </summary>
internal sealed partial class EmojiUsage
{
    public const string FileName = "usage.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Keyed by the emoji's text. Emojis the catalog no longer has stay here, so they stay in the file.
    private readonly Dictionary<string, Entry> entries;

    private EmojiUsage(Dictionary<string, Entry> entries)
    {
        this.entries = entries;
    }

    /// <summary>No emoji used yet, or every counter cleared.</summary>
    public bool IsEmpty => this.entries.Count == 0;

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>The counters saved in <see cref="FileName"/>; none when it is missing or cannot be read.</summary>
    public static EmojiUsage Load()
    {
        try
        {
            string json = File.ReadAllText(FilePath);
            Dictionary<string, Entry>? entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json, JsonOptions);
            return new EmojiUsage(entries ?? []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new EmojiUsage([]);
        }
    }

    /// <summary>The used emojis' texts, the most used first; equal counts, the most recently used first.</summary>
    public IReadOnlyList<string> MostUsed() => this.entries
        .OrderByDescending(entry => entry.Value.Count)
        .ThenByDescending(entry => entry.Value.LastUsed)
        .Select(entry => entry.Key)
        .ToList();

    /// <summary>One more use of <paramref name="emoji"/>, saved.</summary>
    public void Record(string emoji)
    {
        int count = this.entries.TryGetValue(emoji, out Entry? entry) ? entry.Count : 0;
        this.entries[emoji] = new Entry(count + 1, DateTime.UtcNow);
        this.Save();
    }

    /// <summary>Every counter reset, the file rewritten empty.</summary>
    public void Clear()
    {
        this.entries.Clear();
        this.Save();
    }

    // The whole file, through a temporary one then a replace: a crash never leaves it half-written.
    private void Save()
    {
        string temporary = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temporary, ReadableEmojis(JsonSerializer.Serialize(this.entries, JsonOptions)));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not an error: the counters live in memory until the app ends.
        }
    }

    // System.Text.Json escapes every character beyond the BMP — most emojis — even with the relaxed encoder: the
    // escaped surrogate pairs are turned back into the emojis, so the file reads as it shows.
    private static string ReadableEmojis(string json) => SurrogatePair().Replace(json, match => char.ConvertFromUtf32(
        char.ConvertToUtf32(
            (char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber),
            (char)int.Parse(match.Groups[2].Value, NumberStyles.HexNumber))));

    [GeneratedRegex(@"\\u(D[89AB][0-9A-F]{2})\\u(D[C-F][0-9A-F]{2})", RegexOptions.IgnoreCase)]
    private static partial Regex SurrogatePair();

    private sealed record Entry(int Count, DateTime LastUsed);
}
