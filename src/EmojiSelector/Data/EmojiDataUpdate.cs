using System.Text.Json;
using System.Text.Json.Nodes;

namespace EmojiSelector.Data;

/// <summary>
/// The app's only network access, on the user's click on <c>Check for emoji updates…</c>: the latest
/// <c>emojibase-data</c> version published on npm, and its files from jsDelivr. Nothing is sent but the requests.
/// </summary>
internal static class EmojiDataUpdate
{
    // npm's "latest" tag never points at a pre-release.
    private const string LatestUrl = "https://registry.npmjs.org/emojibase-data/latest";

    // {0}: the version, {1}: the file's path in the package.
    private const string FileUrl = "https://cdn.jsdelivr.net/npm/emojibase-data@{0}/{1}";

    private static readonly HttpClient Client = CreateClient();

    /// <summary>The latest published version. Throws on a network, HTTP or parse failure (see <see cref="IsFailure"/>).</summary>
    public static async Task<Version> GetLatestVersionAsync()
    {
        string json = await Client.GetStringAsync(LatestUrl);
        string? text = JsonNode.Parse(json)?["version"]?.GetValue<string>();
        return (text is null ? null : EmojiDataFolder.ParseVersion(text))
            ?? throw new InvalidDataException($"npm gave no usable version ({text ?? "none"}).");
    }

    /// <summary>
    /// Downloads the files of a version, not validated yet (see <see cref="EmojiDataFolder.Parse"/>). Throws on a
    /// network or HTTP failure.
    /// </summary>
    public static async Task<EmojiDataFolder.FileSet> DownloadAsync(Version version)
    {
        Task<byte[]> english = Client.GetByteArrayAsync(string.Format(FileUrl, version, "en/compact.json"));
        Task<byte[]> french = Client.GetByteArrayAsync(string.Format(FileUrl, version, "fr/compact.json"));
        Task<byte[]> license = Client.GetByteArrayAsync(string.Format(FileUrl, version, "LICENSE"));
        return new EmojiDataFolder.FileSet(version.ToString(), await english, await french, await license);
    }

    /// <summary>Whether an exception is one of the failures a check or a download reports to the user.</summary>
    public static bool IsFailure(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException
            or InvalidOperationException or FormatException;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EmojiSelector");
        return client;
    }
}
