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
/// The English data is the list; the French data, joined by hexcode, only adds <b>keywords</b> — an emoji missing
/// from it keeps its English ones.
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
                .Select(entry => new Emoji(entry.Unicode, Capitalize(entry.Label),
                    Keywords(entry, frenchByHexcode.GetValueOrDefault(entry.Hexcode))))
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
    }
}
