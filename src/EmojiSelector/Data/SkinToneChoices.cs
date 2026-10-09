using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EmojiSelector.Data;

/// <summary>
/// The emojis' <b>own skin tones</b>, overriding the default one: saved in <see cref="FileName"/> next to the exe, keyed
/// by the emoji's text — the yellow emoji's, never a variant's —, a tone (<c>"medium"</c>, <c>"none"</c>) or, for a
/// two-person emoji, a tone per person (<c>["light", "dark"]</c>). Read at launch, written after each change.
/// </summary>
/// <remarks>
/// A missing or invalid file is no tone of its own; a value that cannot be read is ignored and kept, like an emoji
/// the catalog no longer has. A folder that cannot be written keeps the tones in memory until the app ends.
/// </remarks>
internal sealed class SkinToneChoices
{
    public const string FileName = "skin-tones.json";

    /// <summary>The file being written, before it replaces <see cref="FileName"/>.</summary>
    public const string TemporaryFileName = FileName + ".tmp";

    private readonly JsonObject choices;

    private SkinToneChoices(JsonObject choices)
    {
        this.choices = choices;
    }

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>The tones saved in <see cref="FileName"/>; none when it is missing or cannot be read.</summary>
    public static SkinToneChoices Load()
    {
        try
        {
            return new SkinToneChoices(JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject ?? []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new SkinToneChoices([]);
        }
    }

    /// <summary>The own tone of <paramref name="emoji"/>, the yellow emoji's text; null when it follows the default tone.</summary>
    public SkinTonePair? Get(string emoji)
    {
        switch (this.choices[emoji])
        {
            case JsonValue value when value.TryGetValue(out string? key) && SkinTones.Parse(key) is SkinTone tone:
                return SkinTonePair.Of(tone);
            case JsonArray { Count: 2 } pair
                when pair[0] is JsonValue first && first.TryGetValue(out string? firstKey) && SkinTones.Parse(firstKey) is SkinTone firstTone
                && pair[1] is JsonValue second && second.TryGetValue(out string? secondKey) && SkinTones.Parse(secondKey) is SkinTone secondTone
                && (firstTone == SkinTone.None) == (secondTone == SkinTone.None):
                return new SkinTonePair(firstTone, secondTone);
            default:
                return null;
        }
    }

    /// <summary>Gives <paramref name="emoji"/> a tone of its own, saved; null makes it follow the default tone again.</summary>
    public void Set(string emoji, SkinTonePair? tones)
    {
        if (tones is not SkinTonePair pair)
        {
            if (!this.choices.Remove(emoji))
            {
                return;
            }
        }
        else if (pair.First == pair.Second)
        {
            this.choices[emoji] = SkinTones.KeyOf(pair.First);
        }
        else
        {
            this.choices[emoji] = new JsonArray(SkinTones.KeyOf(pair.First), SkinTones.KeyOf(pair.Second));
        }

        this.Save();
    }

    // The whole file, through a temporary one then a replace: a crash never leaves it half-written.
    private void Save()
    {
        string temporary = Path.Combine(AppContext.BaseDirectory, TemporaryFileName);
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(temporary, EmojiUsage.ReadableEmojis(this.choices.ToJsonString(options)));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not an error: the tones live in memory until the app ends.
            Debug.WriteLine($"SkinToneChoices: tones not written — {exception.Message}");
        }
    }
}
