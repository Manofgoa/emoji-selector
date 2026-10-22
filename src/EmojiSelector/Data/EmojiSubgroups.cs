using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EmojiSelector.Data;

/// <summary>
/// The emojis' <b>sub-categories</b>: Emojibase's subgroups (<c>face-smiling</c>, <c>hand-fingers-partial</c>…) and the
/// subgroup of each emoji, from <c>subgroups.json</c> — a file of the <b>emoji data</b> set (see
/// <see cref="EmojiDataFolder"/>) derived from Emojibase's <c>en/data.json</c> and the <c>messages.json</c> of both
/// languages: its <c>compact.json</c> has no subgroup.
/// </summary>
/// <remarks>
/// <c>{ "subgroups": [ { "key", "name", "frenchName" } ], "emojis": { "&lt;hexcode&gt;": &lt;subgroup&gt; } }</c>: a
/// subgroup's number is its index in the list — Emojibase's order.
/// </remarks>
internal sealed class EmojiSubgroups
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IReadOnlyDictionary<string, int> byHexcode;

    private EmojiSubgroups(IReadOnlyList<Subgroup> subgroups, IReadOnlyDictionary<string, int> byHexcode)
    {
        this.Subgroups = subgroups;
        this.byHexcode = byHexcode;
    }

    /// <summary>The subgroups, in Emojibase's order.</summary>
    public IReadOnlyList<Subgroup> Subgroups { get; }

    /// <summary>The subgroup of the emoji of <paramref name="hexcode"/>; null when the file does not place it.</summary>
    public Subgroup? Of(string hexcode) =>
        this.byHexcode.TryGetValue(hexcode, out int subgroup) ? this.Subgroups[subgroup] : null;

    /// <summary>
    /// Parses <c>subgroups.json</c>; null when it is not a non-empty subgroup list with a name each and an emoji map
    /// whose numbers are in the list.
    /// </summary>
    public static EmojiSubgroups? Parse(byte[] json)
    {
        FileEntry? file;
        try
        {
            file = JsonSerializer.Deserialize<FileEntry>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (file?.Subgroups is not { Count: > 0 } subgroups || file.Emojis is not { Count: > 0 } emojis
            || subgroups.Any(subgroup => subgroup?.Key is null || subgroup.Name is null)
            || emojis.Values.Any(subgroup => subgroup < 0 || subgroup >= subgroups.Count))
        {
            return null;
        }

        return new EmojiSubgroups(
            subgroups.Select(subgroup => new Subgroup(subgroup!.Key!, subgroup.Name!, subgroup.FrenchName ?? "")).ToList(),
            emojis);
    }

    /// <summary>
    /// <c>subgroups.json</c> derived from Emojibase's files: <paramref name="data"/>, <c>en/data.json</c> (each emoji's
    /// subgroup number), and the <c>messages.json</c> of English and French (the subgroups' names). Throws
    /// <see cref="JsonException"/> or <see cref="InvalidDataException"/> when they are not Emojibase's.
    /// </summary>
    public static byte[] Derive(byte[] data, byte[] englishMessages, byte[] frenchMessages)
    {
        Dictionary<int, (string Key, string Name)> english = Messages(englishMessages);
        Dictionary<int, (string Key, string Name)> french = Messages(frenchMessages);
        int count = english.Count == 0 ? 0 : english.Keys.Max() + 1;
        if (count == 0 || Enumerable.Range(0, count).Any(order => !english.ContainsKey(order)))
        {
            throw new InvalidDataException("The subgroups of messages.json are not numbered from 0 without a gap.");
        }

        var emojis = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (JsonNode? entry in JsonNode.Parse(data)?.AsArray() ?? throw new InvalidDataException("data.json is not a list."))
        {
            if (entry?["hexcode"]?.GetValue<string>() is string hexcode && entry["subgroup"]?.GetValue<int>() is int subgroup
                && subgroup >= 0 && subgroup < count)
            {
                emojis.TryAdd(hexcode, subgroup);
            }
        }

        if (emojis.Count == 0)
        {
            throw new InvalidDataException("data.json gives no emoji a subgroup.");
        }

        var file = new FileEntry(
            [.. Enumerable.Range(0, count).Select(order => new SubgroupEntry(english[order].Key, english[order].Name,
                french.TryGetValue(order, out var name) ? name.Name : ""))],
            emojis.ToDictionary());
        return JsonSerializer.SerializeToUtf8Bytes(file, JsonOptions);
    }

    // messages.json's subgroups by their order: key and name. Two subgroups sharing an order keep the last one listed:
    // Emojibase 17 lists food-marine and food-sweet both at 46, and its data.json puts the sweets there.
    private static Dictionary<int, (string Key, string Name)> Messages(byte[] json) =>
        (JsonNode.Parse(json)?["subgroups"]?.AsArray() ?? throw new InvalidDataException("messages.json has no subgroups."))
            .Where(subgroup => subgroup?["key"] is not null && subgroup["message"] is not null && subgroup["order"] is not null)
            .GroupBy(subgroup => subgroup!["order"]!.GetValue<int>())
            .ToDictionary(group => group.Key,
                group => (group.Last()!["key"]!.GetValue<string>(), group.Last()!["message"]!.GetValue<string>()));

    // The file as read: anything may be missing.
    private sealed record FileEntry(List<SubgroupEntry?>? Subgroups, Dictionary<string, int>? Emojis);

    private sealed record SubgroupEntry(string? Key, string? Name, string? FrenchName);
}

/// <summary>An Emojibase subgroup: its key (<c>face-smiling</c>) and its names, lower case (<c>smiling</c>, <c>souriant</c>).</summary>
internal sealed record Subgroup(string Key, string Name, string FrenchName);
