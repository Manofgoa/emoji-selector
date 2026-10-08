using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EmojiSelector.Data;

/// <summary>
/// The app's settings, in <see cref="FileName"/> next to the exe: the size the user resized the window to, its client
/// area in logical pixels (96 DPI), and whether the frequent tab is shown. A write keeps the keys it does not know,
/// should later settings add some.
/// </summary>
/// <remarks>
/// Best effort, like the emoji cache: a missing or invalid file reads as no setting, and a folder that cannot be
/// written (an exe under Program Files) only means the setting is not kept.
/// </remarks>
internal static class SettingsFile
{
    public const string FileName = "settings.json";

    private const string WindowWidthKey = "windowWidth";
    private const string WindowHeightKey = "windowHeight";
    private const string ShowFrequentKey = "showFrequent";

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
        string temporary = FilePath + ".new";
        try
        {
            File.WriteAllText(temporary, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
