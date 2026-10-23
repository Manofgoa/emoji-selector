using System.Diagnostics;
using System.Text;

namespace EmojiSelector.Drawing;

/// <summary>
/// The <b>flag images</b>: the country and subdivision flags Segoe UI Emoji cannot draw — it shows two letters, or a bare
/// black flag —, Twemoji's SVGs (CC-BY 4.0). <see cref="EmojiRenderer"/> draws an emoji from its image when it has one,
/// through Segoe otherwise.
/// </summary>
/// <remarks>
/// Two layers, read once: the images embedded in the exe (<c>Drawing/Twemoji/</c>), and on top of them
/// <see cref="FolderName"/> next to the exe, holding only the images a later Twemoji version changed or added — written
/// by <c>Check for emoji updates…</c> (see <c>Data/FlagImagesUpdate</c>), never from the embedded copy. The folder is
/// used while its version is not older than the embedded one; an older one (the exe was updated since) is ignored and
/// deleted. An image is a file named as Twemoji names it: the emoji's code points in lowercase hex, joined by '-', FE0F
/// left out (<see cref="FileNameOf"/>). Thread-safe: every renderer reads the same images.
/// </remarks>
internal static class FlagImages
{
    public const string FolderName = "flag-images";

    // The folder an update writes before it replaces FolderName: never half a version in place.
    public const string NewFolderName = FolderName + ".new";

    public const string LicenseFileName = "LICENSE-GRAPHICS";

    public const string VersionFileName = "version.txt";

    public const string ImageExtension = ".svg";

    private const string ResourcePrefix = "EmojiSelector.Drawing.Twemoji.";

    private static readonly Lazy<Layers> Loaded = new(Load);

    public static string FolderPath => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>The Twemoji version in use: the folder's when it is used, the embedded one otherwise.</summary>
    public static Version Version => Loaded.Value.Version;

    /// <summary>How many images the folder gives on top of the embedded ones — 0 when it is not used.</summary>
    public static int FolderImageCount => Loaded.Value.FolderImageCount;

    /// <summary>The embedded images, by file name: the ones an update compares the published ones with.</summary>
    public static IReadOnlyDictionary<string, byte[]> Embedded => Loaded.Value.Embedded;

    /// <summary>The Twemoji version of the embedded images.</summary>
    public static Version EmbeddedVersion => Loaded.Value.EmbeddedVersion;

    /// <summary>The SVG drawing <paramref name="emoji"/>; false when it has none — Segoe draws it.</summary>
    public static bool TryGet(string emoji, out byte[] svg)
    {
        svg = [];
        if (!NeedsImage(emoji))
        {
            return false;
        }

        Layers layers = Loaded.Value;
        string name = FileNameOf(emoji);
        if (layers.Folder.TryGetValue(name, out byte[]? image) || layers.Embedded.TryGetValue(name, out image))
        {
            svg = image;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether Segoe UI Emoji cannot draw <paramref name="emoji"/>: it holds a regional indicator (a country flag, 🇫🇷,
    /// shown as two letters) or a tag character (a subdivision flag, England, shown as a bare 🏴).
    /// </summary>
    public static bool NeedsImage(string emoji)
    {
        foreach (Rune rune in emoji.EnumerateRunes())
        {
            if (rune.Value is >= 0x1F1E6 and <= 0x1F1FF or >= 0xE0020 and <= 0xE007F)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Twemoji's file name of an emoji: <c>1f1eb-1f1f7.svg</c> for 🇫🇷.</summary>
    public static string FileNameOf(string emoji) =>
        string.Join('-', emoji.EnumerateRunes().Where(rune => rune.Value != 0xFE0F).Select(rune => rune.Value.ToString("x")))
            + ImageExtension;

    /// <summary>A Twemoji version, <c>major.minor.patch</c>; null when the text is not one.</summary>
    public static Version? ParseVersion(string text) =>
        Version.TryParse(text.Trim(), out Version? version) && version.Build >= 0 && version.Revision < 0 ? version : null;

    /// <summary>
    /// Writes an update's images to the folder, as a set: everything to <see cref="NewFolderName"/> first,
    /// <c>version.txt</c> last, then that folder put in place of the old one. Throws <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/> when it cannot be written; the new folder is then deleted.
    /// </summary>
    public static void Write(Version version, IReadOnlyDictionary<string, byte[]> images, byte[] license)
    {
        string newPath = Path.Combine(AppContext.BaseDirectory, NewFolderName);
        try
        {
            DeleteFolder(newPath);
            Directory.CreateDirectory(newPath);
            foreach ((string name, byte[] content) in images)
            {
                File.WriteAllBytes(Path.Combine(newPath, name), content);
            }

            File.WriteAllBytes(Path.Combine(newPath, LicenseFileName), license);
            File.WriteAllText(Path.Combine(newPath, VersionFileName), version + Environment.NewLine);
            DeleteFolder(FolderPath);
            Directory.Move(newPath, FolderPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            try
            {
                DeleteFolder(newPath);
            }
            catch (Exception deleteException) when (deleteException is IOException or UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    private static Layers Load()
    {
        Dictionary<string, byte[]> embedded = ReadEmbedded();
        Version embeddedVersion = ParseVersion(Encoding.UTF8.GetString(ReadResource(VersionFileName)))
            ?? throw new InvalidOperationException("The embedded flag images' version is not valid.");

        (Version? folderVersion, Dictionary<string, byte[]> folder) = ReadFolder();
        if (folderVersion is not null && folderVersion < embeddedVersion)
        {
            Debug.WriteLine($"FlagImages: {FolderName} older than {embeddedVersion} — ignored and deleted");
            try
            {
                DeleteFolder(FolderPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"FlagImages: {FolderName} not deleted — {exception.Message}");
            }
        }

        return folderVersion is not null && folderVersion >= embeddedVersion
            ? new Layers(folderVersion, folder, embeddedVersion, embedded)
            : new Layers(embeddedVersion, [], embeddedVersion, embedded);
    }

    // The folder's version and images; no version when the folder or its version.txt is missing, unreadable or not a
    // version. An image that cannot be read is left out: the embedded one is used.
    private static (Version? Version, Dictionary<string, byte[]> Images) ReadFolder()
    {
        var images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string versionPath = Path.Combine(FolderPath, VersionFileName);
            if (!File.Exists(versionPath) || ParseVersion(File.ReadAllText(versionPath)) is not Version version)
            {
                return (null, images);
            }

            foreach (string path in Directory.EnumerateFiles(FolderPath, "*" + ImageExtension))
            {
                try
                {
                    images[Path.GetFileName(path)] = File.ReadAllBytes(path);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine($"FlagImages: {path} not read — {exception.Message}");
                }
            }

            return (version, images);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"FlagImages: {FolderName} not read — {exception.Message}");
            return (null, images);
        }
    }

    private static Dictionary<string, byte[]> ReadEmbedded() => typeof(FlagImages).Assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
            && name.EndsWith(ImageExtension, StringComparison.Ordinal))
        .ToDictionary(name => name[ResourcePrefix.Length..], name => ReadResource(name[ResourcePrefix.Length..]),
            StringComparer.OrdinalIgnoreCase);

    private static byte[] ReadResource(string fileName)
    {
        string resourceName = ResourcePrefix + fileName;
        using Stream stream = typeof(FlagImages).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void DeleteFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    // The images in use: the folder's on top of the embedded ones.
    private sealed record Layers(
        Version Version, Dictionary<string, byte[]> Folder, Version EmbeddedVersion, Dictionary<string, byte[]> Embedded)
    {
        public int FolderImageCount => this.Folder.Count;
    }
}
