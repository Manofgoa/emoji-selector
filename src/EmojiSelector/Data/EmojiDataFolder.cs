using System.Diagnostics;
using System.Text;

namespace EmojiSelector.Data;

/// <summary>
/// The <b>emoji data</b>: Emojibase's files the list, the names and the keywords come from, in <see cref="FolderName"/>
/// next to the exe and read at launch. A copy of them is embedded in the exe: it recreates the folder when the folder
/// is missing, broken, or older than the exe's own copy.
/// </summary>
/// <remarks>
/// The folder's files are a <b>set</b>, always written together: <c>compact.en.json</c> (the list),
/// <c>compact.fr.json</c> (French keywords), <c>LICENSE</c> (Emojibase's, MIT) and <c>version.txt</c> (the Emojibase
/// version of the set). A folder that cannot be written (an exe under Program Files) is not an error: the embedded copy
/// is used in memory.
/// </remarks>
internal static class EmojiDataFolder
{
    public const string FolderName = "emoji-data";

    public const string EnglishFileName = "compact.en.json";

    public const string FrenchFileName = "compact.fr.json";

    public const string LicenseFileName = "LICENSE";

    public const string VersionFileName = "version.txt";

    private const string ResourcePrefix = "EmojiSelector.Data.Emojibase.";

    // A file being written, before it replaces the one in place.
    private const string TemporarySuffix = ".tmp";

    public static string FolderPath => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>The four files of a set, as they are on disk.</summary>
    public sealed record FileSet(string Version, byte[] English, byte[] French, byte[] License);

    /// <summary>A set that parsed: its version and the categories built from it.</summary>
    public sealed record EmojiData(Version Version, IReadOnlyList<EmojiCategory> Categories);

    /// <summary>
    /// The emoji data to use: the folder's when it is valid and not older than the embedded copy, the embedded copy
    /// otherwise — the folder then rewritten from it, when it can be.
    /// </summary>
    public static EmojiData Load()
    {
        FileSet embedded = ReadEmbedded();
        EmojiData embeddedData = Parse(embedded)
            ?? throw new InvalidOperationException("The embedded emoji data is not valid.");

        FileSet? folder = ReadFolder();
        if (folder is not null && Parse(folder) is EmojiData folderData && folderData.Version >= embeddedData.Version)
        {
            return folderData;
        }

        Debug.WriteLine(folder is null
            ? $"EmojiDataFolder: {FolderName} missing or incomplete — written from the embedded copy"
            : $"EmojiDataFolder: {FolderName} invalid or older than {embeddedData.Version} — written from the embedded copy");
        try
        {
            Write(embedded);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"EmojiDataFolder: {FolderName} not written — {exception.Message}");
        }

        return embeddedData;
    }

    /// <summary>
    /// Parses a set: its version, and both JSON files into the categories. Null when any of them is not valid — the
    /// LICENSE is not read.
    /// </summary>
    public static EmojiData? Parse(FileSet set)
    {
        if (ParseVersion(set.Version) is not Version version
            || EmojiCatalog.Parse(set.English, isList: true) is not EmojiCatalog.Entry[] english
            || EmojiCatalog.Parse(set.French, isList: false) is not EmojiCatalog.Entry[] french)
        {
            return null;
        }

        return new EmojiData(version, EmojiCatalog.Build(english, french));
    }

    /// <summary>The version of the copy embedded in the exe: the one a deleted folder comes back to.</summary>
    public static Version EmbeddedVersion() =>
        ParseVersion(Encoding.UTF8.GetString(ReadResource(VersionFileName)))
            ?? throw new InvalidOperationException("The embedded emoji data's version is not valid.");

    /// <summary>An Emojibase version, <c>major.minor.patch</c>; null when the text is not one.</summary>
    public static Version? ParseVersion(string text) =>
        Version.TryParse(text.Trim(), out Version? version) && version.Build >= 0 && version.Revision < 0 ? version : null;

    /// <summary>
    /// Writes a set to the folder, created if needed: every file to a temporary one first, then each moved in place,
    /// <c>version.txt</c> last. Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> when the
    /// folder cannot be written; the temporary files are then deleted.
    /// </summary>
    public static void Write(FileSet set)
    {
        (string Name, byte[] Content)[] files =
        [
            (EnglishFileName, set.English),
            (FrenchFileName, set.French),
            (LicenseFileName, set.License),
            (VersionFileName, Encoding.UTF8.GetBytes(set.Version.Trim() + Environment.NewLine)),
        ];

        Directory.CreateDirectory(FolderPath);
        try
        {
            foreach ((string name, byte[] content) in files)
            {
                File.WriteAllBytes(Path.Combine(FolderPath, name + TemporarySuffix), content);
            }

            foreach ((string name, _) in files)
            {
                string path = Path.Combine(FolderPath, name);
                File.Move(path + TemporarySuffix, path, overwrite: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            foreach ((string name, _) in files)
            {
                try
                {
                    File.Delete(Path.Combine(FolderPath, name + TemporarySuffix));
                }
                catch (Exception deleteException) when (deleteException is IOException or UnauthorizedAccessException)
                {
                }
            }

            throw;
        }
    }

    // The folder's set; null when the folder or one of its files is missing, or cannot be read.
    private static FileSet? ReadFolder()
    {
        try
        {
            string PathOf(string name) => Path.Combine(FolderPath, name);
            if (!File.Exists(PathOf(VersionFileName)) || !File.Exists(PathOf(EnglishFileName))
                || !File.Exists(PathOf(FrenchFileName)) || !File.Exists(PathOf(LicenseFileName)))
            {
                return null;
            }

            return new FileSet(File.ReadAllText(PathOf(VersionFileName)), File.ReadAllBytes(PathOf(EnglishFileName)),
                File.ReadAllBytes(PathOf(FrenchFileName)), File.ReadAllBytes(PathOf(LicenseFileName)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"EmojiDataFolder: {FolderName} not read — {exception.Message}");
            return null;
        }
    }

    // The set embedded in the exe (Data/Emojibase/).
    private static FileSet ReadEmbedded() => new(
        Encoding.UTF8.GetString(ReadResource(VersionFileName)),
        ReadResource(EnglishFileName),
        ReadResource(FrenchFileName),
        ReadResource(LicenseFileName));

    private static byte[] ReadResource(string fileName)
    {
        string resourceName = ResourcePrefix + fileName;
        using Stream stream = typeof(EmojiDataFolder).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
