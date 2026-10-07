using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmojiSelector.Data;

/// <summary>
/// The emojis the app offers, by <b>category</b>, read from the Emojibase data embedded in the exe
/// (<c>Data/Emojibase/compact.json</c>, MIT — see the LICENSE next to it).
/// </summary>
/// <remarks>
/// The categories follow the Win+; panel: its order (Activities before Travel & Places, unlike Unicode's) and its
/// Smileys & People, which merges two Unicode groups. Left out: the components (skin-tone and hair swatches), the
/// flags (Segoe UI Emoji has no flag glyphs), the skin-tone variants and the entries without a group (the regional
/// indicator letters). An emoji newer than the system font is kept: it shows as a box.
/// </remarks>
internal static class EmojiCatalog
{
    public const string ResourceName = "EmojiSelector.Data.Emojibase.compact.json";

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
        using Stream stream = typeof(EmojiCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        Entry[] entries = JsonSerializer.Deserialize<Entry[]>(stream)
            ?? throw new InvalidOperationException($"Empty embedded resource {ResourceName}.");

        return Tabs
            .Select(tab => new EmojiCategory(tab.Name, tab.Icon, entries
                .Where(entry => entry.Group is int group && tab.Groups.Contains(group))
                .OrderBy(entry => entry.Order)
                .Select(entry => new Emoji(entry.Unicode, Capitalize(entry.Label)))
                .ToList()))
            .ToList();
    }

    // Emojibase's labels are lowercase: "grinning face" → "Grinning face".
    private static string Capitalize(string label) =>
        label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label[1..];

    // One entry of compact.json. Its skin-tone variants ("skins") and tags are not read.
    private sealed class Entry
    {
        [JsonPropertyName("unicode")]
        public string Unicode { get; init; } = "";

        [JsonPropertyName("label")]
        public string Label { get; init; } = "";

        [JsonPropertyName("group")]
        public int? Group { get; init; }

        [JsonPropertyName("order")]
        public int Order { get; init; }
    }
}
