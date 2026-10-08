using System.Diagnostics;
using EmojiSelector.Drawing;

namespace EmojiSelector.Data;

/// <summary>
/// <b>Reset all settings</b>: deletes everything the app wrote — the settings, the use counters, the custom groups, the
/// render cache, the emoji data and the startup shortcut — so the next launch is a first launch. Each name comes from the
/// constant of the code that owns it; nothing else is touched, never the exe's folder itself.
/// </summary>
internal static class AppReset
{
    /// <summary>
    /// Deletes every item, each one attempted even when another failed; a missing one is not an error. Returns the
    /// failures, one line each — the item and the reason —, empty when everything was deleted.
    /// </summary>
    public static IReadOnlyList<string> DeleteAll()
    {
        var failures = new List<string>();
        foreach ((string name, Action delete) in Items())
        {
            try
            {
                delete();
            }
            catch (Exception exception) when (StartupShortcut.IsFailure(exception))
            {
                Debug.WriteLine($"AppReset: {name} not deleted — {exception.Message}");
                failures.Add($"{name} — {exception.Message}");
            }
        }

        return failures;
    }

    // What the reset deletes, in order, each with the name a failure is reported under.
    private static IEnumerable<(string Name, Action Delete)> Items()
    {
        string[] files =
        [
            SettingsFile.FileName, SettingsFile.TemporaryFileName,
            EmojiUsage.FileName, EmojiUsage.TemporaryFileName,
            CustomGroups.FileName, CustomGroups.TemporaryFileName,
        ];
        foreach (string file in files)
        {
            yield return (file, () => File.Delete(Path.Combine(AppContext.BaseDirectory, file)));
        }

        foreach (string folder in new[] { EmojiBitmapCache.FolderName, EmojiDataFolder.FolderName })
        {
            yield return (folder + Path.DirectorySeparatorChar, () => DeleteFolder(Path.Combine(AppContext.BaseDirectory, folder)));
        }

        // Only the shortcut to this exe: another copy of the app (a worktree's build, main's) keeps its own.
        yield return (StartupShortcut.FileName, StartupShortcut.DisableForThisExe);
    }

    private static void DeleteFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
