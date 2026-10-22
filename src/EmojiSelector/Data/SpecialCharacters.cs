using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace EmojiSelector.Data;

/// <summary>
/// The <b>special characters</b> the last tab offers (©, «, €, ±…), one section per <b>family</b>, read at launch from
/// <see cref="FileName"/> next to the exe — a file the user may edit. A copy of the default list is embedded in the exe:
/// it writes the file when it is missing, never over an existing one, so the user's edits win over a newer exe.
/// </summary>
/// <remarks>
/// An invalid file — not JSON, or without a <c>families</c> list — is kept as it is, for the user to fix: the embedded
/// list is used meanwhile, and <see cref="LoadResult.Error"/> says why. Inside a valid file, an entry without its
/// character is left out, a character given twice keeps its first entry, and missing names or aliases are empty. A
/// folder that cannot be written (an exe under Program Files) is not an error: the embedded list is used in memory.
/// </remarks>
internal static class SpecialCharacters
{
    public const string FileName = "special-characters.json";

    /// <summary>The file being written, before it replaces <see cref="FileName"/>.</summary>
    public const string TemporaryFileName = FileName + ".tmp";

    private const string ResourceName = "EmojiSelector.Data.SpecialCharacters.json";

    // Lenient on what a hand edit leaves behind: comments and trailing commas are read.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// The families to show, one category each under <paramref name="icon"/>, and the reason the user's file is not
    /// valid — null when it is, or when there was none.
    /// </summary>
    public sealed record LoadResult(IReadOnlyList<EmojiCategory> Families, string? Error);

    /// <summary>
    /// The families of <see cref="FileName"/> when it is valid. Missing, it is written from the embedded list, then that
    /// list is used; invalid or unreadable, the embedded list is used and the file left as it is.
    /// </summary>
    public static LoadResult Load(char icon)
    {
        byte[] embedded = ReadEmbedded();
        IReadOnlyList<EmojiCategory> embeddedFamilies = Parse(embedded, icon, out _)
            ?? throw new InvalidOperationException("The embedded special characters are not valid.");

        byte[]? file;
        try
        {
            file = File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"SpecialCharacters: {FileName} not read — {exception.Message}");
            return new LoadResult(embeddedFamilies, null);
        }

        if (file is null)
        {
            Debug.WriteLine($"SpecialCharacters: {FileName} missing — written from the embedded copy");
            try
            {
                Write(embedded);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"SpecialCharacters: {FileName} not written — {exception.Message}");
            }

            return new LoadResult(embeddedFamilies, null);
        }

        if (Parse(file, icon, out string? error) is IReadOnlyList<EmojiCategory> families)
        {
            return new LoadResult(families, null);
        }

        Debug.WriteLine($"SpecialCharacters: {FileName} invalid — the embedded copy used, the file kept: {error}");
        return new LoadResult(embeddedFamilies, error);
    }

    /// <summary>
    /// Parses a special characters file into its families, one category each under <paramref name="icon"/> — a family
    /// without a character left out. Null when it is not valid, <paramref name="error"/> then saying why.
    /// </summary>
    public static IReadOnlyList<EmojiCategory>? Parse(byte[] json, char icon, out string? error)
    {
        FileEntry? file;
        try
        {
            file = JsonSerializer.Deserialize<FileEntry>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return null;
        }

        if (file?.Families is not List<FamilyEntry?> familyEntries)
        {
            error = "no \"families\" list.";
            return null;
        }

        error = null;
        var seen = new HashSet<string>();
        var families = new List<EmojiCategory>();
        foreach (FamilyEntry? family in familyEntries)
        {
            List<Emoji> characters = (family?.Characters ?? [])
                .Where(entry => !string.IsNullOrEmpty(entry?.Character) && seen.Add(entry!.Character!))
                .Select(entry => CreateCharacter(entry!))
                .ToList();
            if (characters.Count > 0)
            {
                families.Add(new EmojiCategory(family!.Name ?? "", icon, characters));
            }
        }

        return families;
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

    // A special character as an emoji of the grid: its names, then its aliases as its tags in both languages — the
    // details panel shows them, and they are keywords. Every alias typed whole ranks it first, like an emoji's
    // characters, and so does the character itself when it is a symbol (« ranks « first; É is a letter, e would).
    private static Emoji CreateCharacter(CharacterEntry entry)
    {
        string text = entry.Character!;
        string name = entry.Name ?? "";
        string frenchName = entry.FrenchName ?? "";
        string[] aliases = (entry.Aliases ?? []).Where(alias => !string.IsNullOrWhiteSpace(alias)).Select(alias => alias!.Trim())
            .Distinct().ToArray();
        (string Label, string[] Tags)[] languages = [(name, aliases), (frenchName, aliases)];
        IEnumerable<string> ownKey = text.Any(character => !char.IsLetterOrDigit(character)) ? [text] : [];
        return new(text, EmojiCatalog.Capitalize(name), EmojiCatalog.Keywords(languages, EmojiSearch.Words))
        {
            Hexcode = Hexcode(text),
            FrenchName = EmojiCatalog.Capitalize(frenchName),
            EnglishTags = aliases,
            FrenchTags = aliases,
            SymbolKeywords = EmojiCatalog.Keywords(languages, EmojiSearch.SymbolWords),
            CharacterKeys = [.. aliases.Concat(ownKey).Select(EmojiSearch.Fold).Distinct()],
            IsText = true,
        };
    }

    // The code points, hyphen-separated, like Emojibase's hexcodes: © → 00A9.
    private static string Hexcode(string text) =>
        string.Join('-', text.EnumerateRunes().Select(rune => rune.Value.ToString("X4", CultureInfo.InvariantCulture)));

    private static byte[] ReadEmbedded()
    {
        using Stream stream = typeof(SpecialCharacters).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    // The file as read: anything may be missing.
    private sealed record FileEntry(List<FamilyEntry?>? Families);

    private sealed record FamilyEntry(string? Name, List<CharacterEntry?>? Characters);

    private sealed record CharacterEntry(string? Character, string? Name, string? FrenchName, List<string?>? Aliases);
}
