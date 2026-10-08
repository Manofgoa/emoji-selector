using System.Security.Cryptography;
using System.Text.Json.Nodes;
using EmojiSelector.Drawing;

namespace EmojiSelector.Data;

/// <summary>
/// The flag images' part of <c>Check for emoji updates…</c>: the latest Twemoji version, read from jsDelivr, and the
/// flag images it changed or added since the embedded ones — only those are downloaded, then written to
/// <see cref="FlagImages.FolderName"/> by <see cref="FlagImages.Write"/>. Nothing is sent but the requests.
/// </summary>
internal static class FlagImagesUpdate
{
    // The latest tag of the repository, as jsDelivr resolves it.
    private const string LatestUrl = "https://data.jsdelivr.com/v1/packages/gh/jdecked/twemoji/resolved?specifier=latest";

    // {0}: the version. Every file of the repository at that tag, each with its SHA-256 in base64.
    private const string ListingUrl = "https://data.jsdelivr.com/v1/packages/gh/jdecked/twemoji@{0}?structure=flat";

    // {0}: the version, {1}: the file's path in the repository.
    private const string FileUrl = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@{0}/{1}";

    private const string ImagesPath = "assets/svg/";

    /// <summary>A version's images that differ from the embedded ones or that the exe lacks, and its license.</summary>
    public sealed record Download(Version Version, IReadOnlyDictionary<string, byte[]> Images, byte[] License);

    /// <summary>The latest Twemoji version. Throws on a network, HTTP or parse failure (see <see cref="EmojiDataUpdate.IsFailure"/>).</summary>
    public static async Task<Version> GetLatestVersionAsync()
    {
        string json = await EmojiDataUpdate.Client.GetStringAsync(LatestUrl);
        string? text = JsonNode.Parse(json)?["version"]?.GetValue<string>();
        return (text is null ? null : FlagImages.ParseVersion(text))
            ?? throw new InvalidDataException($"jsDelivr gave no usable Twemoji version ({text ?? "none"}).");
    }

    /// <summary>
    /// Downloads, at <paramref name="version"/>, the images of the <paramref name="emojis"/> that need one (see
    /// <see cref="FlagImages.NeedsImage"/>) whose published file differs from the embedded one, or that the exe does not
    /// have. A flag the version does not publish is left out: it keeps the embedded image, or Segoe. Each download is
    /// checked against the listing's hash. Throws on a network, HTTP or parse failure, or a hash that does not match.
    /// </summary>
    public static async Task<Download> DownloadAsync(Version version, IEnumerable<string> emojis)
    {
        Dictionary<string, string> published = await GetImageHashesAsync(version);
        IReadOnlyDictionary<string, byte[]> embedded = FlagImages.Embedded;
        string[] changed = emojis
            .Where(FlagImages.NeedsImage)
            .Select(FlagImages.FileNameOf)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => published.TryGetValue(name, out string? hash)
                && !(embedded.TryGetValue(name, out byte[]? image) && HashOf(image) == hash))
            .ToArray();

        Task<byte[]> license = EmojiDataUpdate.Client.GetByteArrayAsync(string.Format(FileUrl, version, FlagImages.LicenseFileName));
        var images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in changed)
        {
            byte[] image = await EmojiDataUpdate.Client.GetByteArrayAsync(string.Format(FileUrl, version, ImagesPath + name));
            if (HashOf(image) != published[name])
            {
                throw new InvalidDataException($"{name} does not match its published hash.");
            }

            images[name] = image;
        }

        return new Download(version, images, await license);
    }

    // The SVGs of a version's listing, by file name, each with its hash.
    private static async Task<Dictionary<string, string>> GetImageHashesAsync(Version version)
    {
        string json = await EmojiDataUpdate.Client.GetStringAsync(string.Format(ListingUrl, version));
        JsonArray files = JsonNode.Parse(json)?["files"]?.AsArray()
            ?? throw new InvalidDataException("jsDelivr gave no file listing.");
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string prefix = "/" + ImagesPath;
        foreach (JsonNode? file in files)
        {
            if (file?["name"]?.GetValue<string>() is string path && file["hash"]?.GetValue<string>() is string hash
                && path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(FlagImages.ImageExtension, StringComparison.Ordinal))
            {
                hashes[path[prefix.Length..]] = hash;
            }
        }

        return hashes;
    }

    // jsDelivr's hash of a file: its SHA-256, in base64.
    private static string HashOf(byte[] content) => Convert.ToBase64String(SHA256.HashData(content));
}
