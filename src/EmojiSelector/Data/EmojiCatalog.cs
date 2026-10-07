using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmojiSelector.Data;

/// <summary>
/// The emojis the app offers, by <b>category</b>, read from the Emojibase data embedded in the exe
/// (<c>Data/Emojibase/compact.en.json</c> and <c>compact.fr.json</c>, MIT — see the LICENSE next to them).
/// </summary>
/// <remarks>
/// The categories follow the Win+; panel: its order (Activities before Travel & Places, unlike Unicode's) and its
/// Smileys & People, which merges two Unicode groups. Left out: the components (skin-tone and hair swatches), the
/// flags (Segoe UI Emoji has no flag glyphs), the skin-tone variants and the entries without a group (the regional
/// indicator letters). An emoji newer than the system font is kept: it shows as a box.
/// <para>
/// The English data is the list; the French data, joined by hexcode, only adds <b>keywords</b> — an emoji missing
/// from it keeps its English ones.
/// </para>
/// </remarks>
internal static class EmojiCatalog
{
    public const string EnglishResourceName = "EmojiSelector.Data.Emojibase.compact.en.json";

    public const string FrenchResourceName = "EmojiSelector.Data.Emojibase.compact.fr.json";

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

    /// <summary>Reads the embedded data into the categories, in tab order, each one's emojis in Unicode order.</summary>
    public static IReadOnlyList<EmojiCategory> Load()
    {
        Entry[] entries = ReadEntries(EnglishResourceName);
        Dictionary<string, Entry> french = ReadEntries(FrenchResourceName).ToDictionary(entry => entry.Hexcode);

        return Tabs
            .Select(tab => new EmojiCategory(tab.Name, tab.Icon, entries
                .Where(entry => entry.Group is int group && tab.Groups.Contains(group))
                .OrderBy(entry => entry.Order)
                .Select(entry => new Emoji(entry.Unicode, Capitalize(entry.Label),
                    Keywords(entry, french.GetValueOrDefault(entry.Hexcode))))
                .ToList()))
            .ToList();
    }

    private static Entry[] ReadEntries(string resourceName)
    {
        using Stream stream = typeof(EmojiCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
        return JsonSerializer.Deserialize<Entry[]>(stream)
            ?? throw new InvalidOperationException($"Empty embedded resource {resourceName}.");
    }

    // The words of an emoji's names and tags, in English and in French, each once: a name's when a name has it.
    private static IReadOnlyList<EmojiKeyword> Keywords(Entry english, Entry? french)
    {
        var words = new Dictionary<string, bool>();
        foreach (Entry? entry in new[] { english, french })
        {
            if (entry is null)
            {
                continue;
            }

            foreach (string word in EmojiSearch.Words(entry.Label))
            {
                words[word] = true;
            }

            foreach (string word in (entry.Tags ?? []).SelectMany(EmojiSearch.Words))
            {
                words.TryAdd(word, false);
            }
        }

        return words.Select(word => new EmojiKeyword(word.Key, word.Value)).ToList();
    }

    // Emojibase's labels are lowercase: "grinning face" → "Grinning face".
    private static string Capitalize(string label) =>
        label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label[1..];

    // One entry of compact.json. Its skin-tone variants ("skins") are not read.
    private sealed class Entry
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
    }
}
