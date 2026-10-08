using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EmojiSelector.Data;

/// <summary>
/// The app's settings, in <see cref="FileName"/> next to the exe: the size the user resized the window to, its client
/// area in logical pixels (96 DPI), the window's opacity, the emoji the user chose for the tray icon, whether the
/// frequent tab is shown, the details panel's French row and highlight colour, and the default skin tones.
/// A write keeps the keys it does not know, should later settings add some.
/// </summary>
/// <remarks>
/// Best effort, like the emoji cache: a missing or invalid file reads as no setting, and a folder that cannot be
/// written (an exe under Program Files) only means the setting is not kept.
/// </remarks>
internal static class SettingsFile
{
    public const string FileName = "settings.json";

    /// <summary>The file being written, before it replaces <see cref="FileName"/>.</summary>
    public const string TemporaryFileName = FileName + ".new";

    private const string WindowWidthKey = "windowWidth";
    private const string WindowHeightKey = "windowHeight";
    private const string TrayEmojiKey = "trayEmoji";
    private const string ShowFrequentKey = "showFrequent";
    private const string ShowFrenchKey = "showFrench";
    private const string HighlightColorKey = "highlightColor";
    private const string OpacityKey = "opacity";
    private const string SkinToneKey = "skinTone";
    private const string SecondSkinToneKey = "secondSkinTone";

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>The saved window size, logical pixels; null when there is none, or none that can be read.</summary>
    public static Size? ReadWindowSize()
    {
        JsonObject? settings = Read();
        if (settings?[WindowWidthKey] is JsonValue width && settings[WindowHeightKey] is JsonValue height
            && width.TryGetValue(out int widthValue) && height.TryGetValue(out int heightValue)
            && widthValue > 0 && heightValue > 0)
        {
            return new Size(widthValue, heightValue);
        }

        return null;
    }

    /// <summary>Saves the window size, logical pixels; null removes it.</summary>
    public static void WriteWindowSize(Size? size)
    {
        JsonObject settings = Read() ?? [];
        if (size is Size value)
        {
            settings[WindowWidthKey] = value.Width;
            settings[WindowHeightKey] = value.Height;
        }
        else
        {
            settings.Remove(WindowWidthKey);
            settings.Remove(WindowHeightKey);
        }

        Write(settings);
    }

    /// <summary>The emoji chosen for the tray icon; null when there is none, or none that can be read.</summary>
    public static string? ReadTrayEmoji() =>
        Read()?[TrayEmojiKey] is JsonValue emoji && emoji.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    /// <summary>Saves the emoji chosen for the tray icon.</summary>
    public static void WriteTrayEmoji(string emoji)
    {
        JsonObject settings = Read() ?? [];
        settings[TrayEmojiKey] = emoji;
        Write(settings);
    }

    /// <summary>Whether the frequent tab is shown; true when the file, the key or a readable value is missing.</summary>
    public static bool ReadShowFrequent() =>
        Read()?[ShowFrequentKey] is not JsonValue value || !value.TryGetValue(out bool show) || show;

    /// <summary>Saves whether the frequent tab is shown.</summary>
    public static void WriteShowFrequent(bool show)
    {
        JsonObject settings = Read() ?? [];
        settings[ShowFrequentKey] = show;
        Write(settings);
    }

    /// <summary>Whether the details panel shows its French row; true when the file, the key or a readable value is missing.</summary>
    public static bool ReadShowFrench() =>
        Read()?[ShowFrenchKey] is not JsonValue value || !value.TryGetValue(out bool show) || show;

    /// <summary>Saves whether the details panel shows its French row.</summary>
    public static void WriteShowFrench(bool show)
    {
        JsonObject settings = Read() ?? [];
        settings[ShowFrenchKey] = show;
        Write(settings);
    }

    /// <summary>The colour highlighting the search's matches, <c>#RRGGBB</c>; null when there is none, or none that can be read.</summary>
    public static Color? ReadHighlightColor() =>
        Read()?[HighlightColorKey] is JsonValue value && value.TryGetValue(out string? text)
            && text is ['#', .. string hex] && hex.Length == 6
            && int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb)
            ? Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF)
            : null;

    /// <summary>Saves the colour highlighting the search's matches, as <c>#RRGGBB</c>.</summary>
    public static void WriteHighlightColor(Color color)
    {
        JsonObject settings = Read() ?? [];
        settings[HighlightColorKey] = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        Write(settings);
    }

    /// <summary>The window's opacity, percent, as saved — any integer; null when there is none, or none that can be read.</summary>
    public static int? ReadOpacity() =>
        Read()?[OpacityKey] is JsonValue value && value.TryGetValue(out int percent) ? percent : null;

    /// <summary>Saves the window's opacity, percent.</summary>
    public static void WriteOpacity(int percent)
    {
        JsonObject settings = Read() ?? [];
        settings[OpacityKey] = percent;
        Write(settings);
    }

    /// <summary>The default skin tone; <see cref="SkinTone.None"/> when there is none, or none that can be read.</summary>
    public static SkinTone ReadSkinTone() =>
        Read()?[SkinToneKey] is JsonValue value && value.TryGetValue(out string? key) && SkinTones.Parse(key) is SkinTone tone
            ? tone
            : SkinTone.None;

    /// <summary>Saves the default skin tone.</summary>
    public static void WriteSkinTone(SkinTone tone)
    {
        JsonObject settings = Read() ?? [];
        settings[SkinToneKey] = SkinTones.KeyOf(tone);
        Write(settings);
    }

    /// <summary>
    /// The second person's default skin tone, for the two-person emojis; null — the same as the first person's — when
    /// there is none, or none that can be read. Never <see cref="SkinTone.None"/>.
    /// </summary>
    public static SkinTone? ReadSecondSkinTone() =>
        Read()?[SecondSkinToneKey] is JsonValue value && value.TryGetValue(out string? key)
            && SkinTones.Parse(key) is SkinTone tone && tone != SkinTone.None
            ? tone
            : null;

    /// <summary>Saves the second person's default skin tone; null removes it: the same as the first person's.</summary>
    public static void WriteSecondSkinTone(SkinTone? tone)
    {
        JsonObject settings = Read() ?? [];
        if (tone is SkinTone value)
        {
            settings[SecondSkinToneKey] = SkinTones.KeyOf(value);
        }
        else
        {
            settings.Remove(SecondSkinToneKey);
        }

        Write(settings);
    }

    private static JsonObject? Read()
    {
        try
        {
            return File.Exists(FilePath) ? JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"SettingsFile: settings not read — {exception.Message}");
            return null;
        }
    }

    // Written to a temporary file, then moved in place: a crash never leaves a half-written file. Any failure is
    // swallowed — the settings are only not kept.
    private static void Write(JsonObject settings)
    {
        string temporary = Path.Combine(AppContext.BaseDirectory, TemporaryFileName);
        try
        {
            // The emojis written as themselves, not \uXXXX escapes, like usage.json.
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(temporary, EmojiUsage.ReadableEmojis(settings.ToJsonString(options)));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"SettingsFile: settings not written — {exception.Message}");
            try
            {
                File.Delete(temporary);
            }
            catch (Exception deleteException) when (deleteException is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
