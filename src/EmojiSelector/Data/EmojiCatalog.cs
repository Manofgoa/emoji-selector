using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmojiSelector.Data;

/// <summary>
/// The emojis the app offers, by <b>category</b>, built from Emojibase's data (<c>compact.en.json</c> and
/// <c>compact.fr.json</c>, MIT — see the LICENSE next to them): the files of <see cref="EmojiDataFolder"/>, or the
/// copy embedded in the exe.
/// </summary>
/// <remarks>
/// The categories follow the Win+; panel: its order (Activities before Travel & Places, unlike Unicode's) and its
/// Smileys & People, which merges two Unicode groups. Left out: the components (skin-tone and hair swatches), the
/// flags (Segoe UI Emoji has no flag glyphs), the skin-tone variants and the entries without a group (the regional
/// indicator letters). An emoji newer than the system font is kept: it shows as a box.
/// <para>
/// The English data is the list; the French data, joined by hexcode, only adds <b>keywords</b> and the French name and
/// tags the details panel shows — an emoji missing from it keeps its English ones.
/// </para>
/// </remarks>
internal static class EmojiCatalog
{
    // Emojibase's group numbers (its meta/groups.json).
    private const int SmileysEmotion = 0;
    private const int PeopleBody = 1;
    private const int AnimalsNature = 3;
    private const int FoodDrink = 4;
    private const int TravelPlaces = 5;
    private const int Activities = 6;
    private const int Objects = 7;
    private const int Symbols = 8;

    // The tabs, in order: name, glyph, Emojibase groups.
    private static readonly (string Name, char Icon, int[] Groups)[] Tabs =
    [
        ("Smileys & People", '', [SmileysEmotion, PeopleBody]),
        ("Animals & Nature", '', [AnimalsNature]),
        ("Food & Drink", '', [FoodDrink]),
        ("Activities", '', [Activities]),
        ("Travel & Places", '', [TravelPlaces]),
        ("Objects", '', [Objects]),
        ("Symbols", '', [Symbols]),
    ];

    /// <summary>
    /// Builds the categories from the parsed data, in tab order, each one's emojis in Unicode order.
    /// </summary>
    /// <param name="english">The list: <c>compact.en.json</c>.</param>
    /// <param name="french">The French keywords: <c>compact.fr.json</c>.</param>
    public static IReadOnlyList<EmojiCategory> Build(Entry[] english, Entry[] french)
    {
        // An emoji given twice keeps its first entry: the app maps the emojis by their text.
        Entry[] entries = english.DistinctBy(entry => entry.Unicode).ToArray();
        Dictionary<string, Entry> frenchByHexcode = french
            .GroupBy(entry => entry.Hexcode)
            .ToDictionary(group => group.Key, group => group.First());

        return Tabs
            .Select(tab => new EmojiCategory(tab.Name, tab.Icon, entries
                .Where(entry => entry.Group is int group && tab.Groups.Contains(group))
                .OrderBy(entry => entry.Order)
                .Select(entry => CreateEmoji(entry, frenchByHexcode.GetValueOrDefault(entry.Hexcode)))
                .ToList()))
            .ToList();
    }

    /// <summary>
    /// Parses one <c>compact.json</c>; null when it is not a non-empty list of entries — or, for the
    /// <paramref name="isList"/> one, when none of its emojis goes in a tab.
    /// </summary>
    public static Entry[]? Parse(byte[] json, bool isList)
    {
        Entry[]? entries;
        try
        {
            entries = JsonSerializer.Deserialize<Entry[]>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (entries is null || entries.Length == 0
            || entries.Any(entry => entry is null || entry.Hexcode is null || entry.Unicode is null || entry.Label is null))
        {
            return null;
        }

        if (isList && !entries.Any(entry => entry.Group is int group && Tabs.Any(tab => tab.Groups.Contains(group))))
        {
            return null;
        }

        return entries;
    }

    // The emoji's characters are added to its tags, in both languages: the details panel shows them, and they are
    // keywords like the others.
    private static Emoji CreateEmoji(Entry english, Entry? french)
    {
        IReadOnlyList<string> characters = EmojiCharacters.Of(english.Unicode);
        IReadOnlyList<string> emoticons = Emoticons(english.Emoticon);
        string[] englishTags = WithCharacters(english.Tags ?? [], characters);
        string[] frenchTags = french is null ? [] : WithCharacters(french.Tags ?? [], characters);
        (string Label, string[] Tags)[] languages = french is null
            ? [(english.Label, englishTags)]
            : [(english.Label, englishTags), (french.Label, frenchTags)];
        return new(english.Unicode, Capitalize(english.Label), Keywords(languages, EmojiSearch.Words))
        {
            Hexcode = english.Hexcode,
            FrenchName = french is null ? "" : Capitalize(french.Label),
            EnglishTags = englishTags,
            FrenchTags = frenchTags,
            Emoticons = emoticons,
            SymbolKeywords = [.. Keywords(languages, EmojiSearch.SymbolWords)
                .Concat(emoticons.Select(emoticon => new EmojiKeyword(EmojiSearch.Fold(emoticon), IsName: false)))
                .DistinctBy(keyword => keyword.Word)],
            CharacterKeys = [.. characters.Concat(emoticons).Select(EmojiSearch.Fold).Distinct()],
        };
    }

    // The tags, then every character they lack, case ignored: 🆗's tag ok already stands for its OK.
    private static string[] WithCharacters(string[] tags, IReadOnlyList<string> characters) =>
        [.. tags, .. characters.Where(character => !tags.Contains(character, StringComparer.OrdinalIgnoreCase))];

    // The words of an emoji's names and tags, in English and in French, each once: a name's when a name has it.
    private static IReadOnlyList<EmojiKeyword> Keywords(
        (string Label, string[] Tags)[] languages, Func<string, IEnumerable<string>> wordsOf)
    {
        var words = new Dictionary<string, bool>();
        foreach ((string label, string[] tags) in languages)
        {
            foreach (string word in wordsOf(label))
            {
                words[word] = true;
            }

            foreach (string word in tags.SelectMany(wordsOf))
            {
                words.TryAdd(word, false);
            }
        }

        return words.Select(word => new EmojiKeyword(word.Key, word.Value)).ToList();
    }

    // Emojibase's emoticon: one string, or an array of them ("xD", "XD").
    private static IReadOnlyList<string> Emoticons(JsonElement? emoticon) => emoticon switch
    {
        { ValueKind: JsonValueKind.String } value => [value.GetString()!],
        { ValueKind: JsonValueKind.Array } values => values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()!)
            .ToList(),
        _ => [],
    };

    // Emojibase's labels are lowercase: "grinning face" → "Grinning face".
    private static string Capitalize(string label) =>
        label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label[1..];

    /// <summary>One entry of <c>compact.json</c>. Its skin-tone variants (<c>skins</c>) are not read.</summary>
    internal sealed class Entry
    {
        [JsonPropertyName("hexcode")]
        public string Hexcode { get; init; } = "";

        [JsonPropertyName("unicode")]
        public string Unicode { get; init; } = "";

        [JsonPropertyName("label")]
        public string Label { get; init; } = "";

        [JsonPropertyName("group")]
        public int? Group { get; init; }

        [JsonPropertyName("order")]
        public int Order { get; init; }

        [JsonPropertyName("tags")]
        public string[]? Tags { get; init; }

        // A string, or an array of strings.
        [JsonPropertyName("emoticon")]
        public JsonElement? Emoticon { get; init; }
    }
}
