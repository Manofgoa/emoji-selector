using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace EmojiSelector.Drawing;

/// <summary>
/// Every emoji's bitmap at one size, <b>pre-rendered in the background</b> so the grid never renders on the UI thread.
/// The first run renders them all and saves them in the <see cref="FolderName"/> folder next to the exe, as one atlas
/// image and the key it was built for; the next runs reload the atlas instead, as long as the key still matches.
/// </summary>
/// <remarks>
/// A bitmap is published to <see cref="TryGet"/> once complete, then only read by the UI thread.
/// <see cref="BitmapsReady"/> tells the UI thread new bitmaps are there, coalesced: one pending notification at most.
/// Writing the cache is best effort: a folder that cannot be written (an exe under Program Files) only means the
/// emojis are rendered again at the next launch.
/// </remarks>
internal sealed class EmojiBitmapCache : IDisposable
{
    public const string FolderName = "cache";

    // The atlas lays the emojis out on this many columns, in pre-render order.
    private const int AtlasColumns = 64;

    // Bumped whenever the atlas or the rendering changes in a way the rest of the key does not tell.
    private const int FormatVersion = 1;

    private readonly IReadOnlyList<string> emojis;
    private readonly SynchronizationContext? uiContext;
    private Run? run;
    private int notificationPending;

    /// <param name="emojis">Every emoji, in the order they are pre-rendered: the first ones are ready first.</param>
    public EmojiBitmapCache(IReadOnlyList<string> emojis)
    {
        this.emojis = emojis;
        this.uiContext = SynchronizationContext.Current;
    }

    /// <summary>Bitmaps were published since the last notification. Raised on the UI thread.</summary>
    public event EventHandler? BitmapsReady;

    /// <summary>The size, in pixels, of the bitmaps being pre-rendered; 0 before <see cref="Start"/>.</summary>
    public int Size => this.run?.Size ?? 0;

    /// <summary>
    /// Drops the bitmaps of the previous size, if any, and starts pre-rendering every emoji at
    /// <paramref name="size"/> pixels — from the disk cache when it holds them.
    /// </summary>
    public void Start(int size)
    {
        this.run?.Cancel();
        var run = new Run(size);
        this.run = run;
        var thread = new Thread(() => this.PreRender(run))
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "Emoji pre-render",
        };
        thread.Start();
    }

    /// <summary>The emoji's bitmap, or null while the pre-render has not reached it.</summary>
    public Bitmap? TryGet(string emoji) =>
        this.run is Run run && run.Bitmaps.TryGetValue(emoji, out Bitmap? bitmap) ? bitmap : null;

    public void Dispose()
    {
        this.run?.Cancel();
        this.run = null;
    }

    private static string CacheFolder => Path.Combine(AppContext.BaseDirectory, FolderName);

    private void PreRender(Run run)
    {
        var stopwatch = Stopwatch.StartNew();
        string atlasPath = Path.Combine(CacheFolder, $"emojis-{run.Size}.png");
        string keyPath = Path.Combine(CacheFolder, $"emojis-{run.Size}.key");
        string key = this.BuildKey(run.Size);
        try
        {
            if (this.TryLoadAtlas(run, atlasPath, keyPath, key))
            {
                Debug.WriteLine($"EmojiBitmapCache: {this.emojis.Count} emojis at {run.Size} px loaded from the cache in {stopwatch.ElapsedMilliseconds} ms");
            }
            else if (this.RenderAll(run) is Bitmap atlas)
            {
                using (atlas)
                {
                    Debug.WriteLine($"EmojiBitmapCache: {this.emojis.Count} emojis at {run.Size} px rendered in {stopwatch.ElapsedMilliseconds} ms");
                    WriteAtlas(atlas, atlasPath, keyPath, key);
                }
            }
        }
        finally
        {
            run.Finish();
        }
    }

    // The atlas, when its key matches, cut into one bitmap per emoji and published. False when it is missing,
    // stale or unreadable: the emojis are then rendered.
    private bool TryLoadAtlas(Run run, string atlasPath, string keyPath, string key)
    {
        try
        {
            if (!File.Exists(keyPath) || !File.Exists(atlasPath) || File.ReadAllText(keyPath) != key)
            {
                return false;
            }

            // Read into memory: a Bitmap opened on a file keeps it locked until disposed.
            using var stream = new MemoryStream(File.ReadAllBytes(atlasPath));
            using var atlas = new Bitmap(stream);
            if (atlas.Size != AtlasSize(this.emojis.Count, run.Size))
            {
                return false;
            }

            for (int i = 0; i < this.emojis.Count && !run.IsCancelled; i++)
            {
                var bitmap = new Bitmap(run.Size, run.Size, PixelFormat.Format32bppPArgb);
                CopyPixels(atlas, AtlasCell(i, run.Size), bitmap, Point.Empty);
                this.Publish(run, this.emojis[i], bitmap);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or ExternalException)
        {
            Debug.WriteLine($"EmojiBitmapCache: cache not read — {exception.Message}");
            return false;
        }
    }

    // Renders every emoji, publishing each one, and returns the atlas holding them all — null when cancelled or when
    // an emoji failed, since such an atlas would be incomplete.
    private Bitmap? RenderAll(Run run)
    {
        Size atlasSize = AtlasSize(this.emojis.Count, run.Size);
        var atlas = new Bitmap(atlasSize.Width, atlasSize.Height, PixelFormat.Format32bppPArgb);
        bool complete = true;
        using (var renderer = new EmojiRenderer())
        {
            for (int i = 0; i < this.emojis.Count && !run.IsCancelled; i++)
            {
                Bitmap bitmap;
                try
                {
                    bitmap = renderer.Render(this.emojis[i], run.Size);
                }
                catch (Exception exception) when (exception is COMException or ExternalException)
                {
                    // Its cell stays green: the emoji is missing.
                    Debug.WriteLine($"EmojiBitmapCache: {this.emojis[i]} not rendered — {exception.Message}");
                    complete = false;
                    continue;
                }

                Rectangle cell = AtlasCell(i, run.Size);
                CopyPixels(bitmap, new Rectangle(Point.Empty, bitmap.Size), atlas, cell.Location);
                this.Publish(run, this.emojis[i], bitmap);
            }
        }

        if (!complete || run.IsCancelled)
        {
            atlas.Dispose();
            return null;
        }

        return atlas;
    }

    private void Publish(Run run, string emoji, Bitmap bitmap)
    {
        if (!run.Bitmaps.TryAdd(emoji, bitmap))
        {
            bitmap.Dispose();
            return;
        }

        if (Interlocked.Exchange(ref this.notificationPending, 1) == 0)
        {
            if (this.uiContext is null)
            {
                this.notificationPending = 0;
                return;
            }

            this.uiContext.Post(_ =>
            {
                Volatile.Write(ref this.notificationPending, 0);
                this.BitmapsReady?.Invoke(this, EventArgs.Empty);
            }, null);
        }
    }

    // What the atlas depends on, one "name=value" per line; any difference with the saved key makes it stale.
    private string BuildKey(int size)
    {
        string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "seguiemj.ttf");
        var font = new FileInfo(fontPath);
        string fontStamp = font.Exists ? $"{font.Length};{font.LastWriteTimeUtc.Ticks}" : "missing";
        string emojiHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', this.emojis))));
        return string.Join('\n',
            $"format={FormatVersion}",
            $"size={size}",
            $"font={fontStamp}",
            $"emojis={emojiHash}",
            $"renderer={EmojiRenderer.FontScale.ToString(System.Globalization.CultureInfo.InvariantCulture)};{EmojiRenderer.RaiseScale.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "");
    }

    // Written to temporary files, then moved in place, the key last: a crash never leaves a key over a half-written
    // atlas. Any failure is swallowed — the emojis are rendered again at the next launch.
    private static void WriteAtlas(Bitmap atlas, string atlasPath, string keyPath, string key)
    {
        string atlasTemporary = atlasPath + ".new";
        string keyTemporary = keyPath + ".new";
        try
        {
            Directory.CreateDirectory(CacheFolder);
            atlas.Save(atlasTemporary, ImageFormat.Png);
            File.WriteAllText(keyTemporary, key);
            File.Delete(keyPath);
            File.Move(atlasTemporary, atlasPath, overwrite: true);
            File.Move(keyTemporary, keyPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ExternalException)
        {
            Debug.WriteLine($"EmojiBitmapCache: cache not written — {exception.Message}");
            TryDelete(atlasTemporary);
            TryDelete(keyTemporary);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static Size AtlasSize(int count, int size) =>
        new(AtlasColumns * size, Math.Max(1, (count + AtlasColumns - 1) / AtlasColumns) * size);

    private static Rectangle AtlasCell(int index, int size) =>
        new(index % AtlasColumns * size, index / AtlasColumns * size, size, size);

    // Copies a rectangle of premultiplied pixels, row by row; GDI+ converts a non-premultiplied source on the way.
    private static void CopyPixels(Bitmap source, Rectangle sourceRectangle, Bitmap target, Point targetLocation)
    {
        BitmapData from = source.LockBits(sourceRectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            BitmapData to = target.LockBits(new Rectangle(targetLocation, sourceRectangle.Size), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                byte[] row = new byte[sourceRectangle.Width * 4];
                for (int y = 0; y < sourceRectangle.Height; y++)
                {
                    Marshal.Copy(from.Scan0 + y * from.Stride, row, 0, row.Length);
                    Marshal.Copy(row, 0, to.Scan0 + y * to.Stride, row.Length);
                }
            }
            finally
            {
                target.UnlockBits(to);
            }
        }
        finally
        {
            source.UnlockBits(from);
        }
    }

    // One pre-render at one size. Its bitmaps are disposed exactly once: by whoever comes second between the thread
    // finishing and the run being cancelled — never while the thread may still publish.
    private sealed class Run(int size)
    {
        private readonly object gate = new();
        private bool cancelled;
        private bool finished;

        public int Size { get; } = size;

        public ConcurrentDictionary<string, Bitmap> Bitmaps { get; } = new();

        public bool IsCancelled => Volatile.Read(ref this.cancelled);

        public void Cancel()
        {
            lock (this.gate)
            {
                Volatile.Write(ref this.cancelled, true);
                if (this.finished)
                {
                    this.DisposeBitmaps();
                }
            }
        }

        public void Finish()
        {
            lock (this.gate)
            {
                this.finished = true;
                if (this.cancelled)
                {
                    this.DisposeBitmaps();
                }
            }
        }

        private void DisposeBitmaps()
        {
            foreach (Bitmap bitmap in this.Bitmaps.Values)
            {
                bitmap.Dispose();
            }

            this.Bitmaps.Clear();
        }
    }
}
