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
    private const int FormatVersion = 2;

    // A size whose bitmaps, every emoji's, would take more memory than this is rendered on demand (see Request): at
    // 400 % on a 150 % screen, an emoji is 168 px, ~110 KB — the whole list ~420 MB, its atlas an image GDI+ refuses.
    private const long CompleteMaxBytes = 100L * 1024 * 1024;

    // On demand, the bitmaps kept: about this much memory, never fewer than the ones asked for.
    private const long OnDemandMaxBytes = 150L * 1024 * 1024;

    private readonly IReadOnlyList<string> emojis;

    // The texts drawn as special characters, monochrome with the text font (see EmojiRenderer).
    private readonly IReadOnlySet<string> characters;
    private readonly SynchronizationContext? uiContext;

    // Held while an atlas is written, so StopWriting returns only once no write is under way.
    private readonly object writeGate = new();
    private Run? run;

    // A run of a previous size, kept while the current one has not finished: its bitmaps stand in, stretched, for the
    // ones the current run has not reached yet. Null when none.
    private Run? fallback;
    private int notificationPending;
    private bool writingStopped;

    /// <param name="emojis">Every emoji, in the order they are pre-rendered: the first ones are ready first.</param>
    /// <param name="characters">The ones among them drawn as <b>special characters</b>.</param>
    public EmojiBitmapCache(IReadOnlyList<string> emojis, IReadOnlySet<string> characters)
    {
        this.emojis = emojis;
        this.characters = characters;
        this.uiContext = SynchronizationContext.Current;
    }

    /// <summary>Bitmaps were published since the last notification. Raised on the UI thread.</summary>
    public event EventHandler? BitmapsReady;

    /// <summary>The size, in pixels, of the bitmaps being pre-rendered; 0 before <see cref="Start"/>.</summary>
    public int Size => this.run?.Size ?? 0;

    /// <summary>
    /// Whether the size is rendered <b>on demand</b>: only the emojis <see cref="Request"/> asks for, in its order.
    /// </summary>
    public bool IsOnDemand => this.run?.OnDemand ?? false;

    /// <summary>
    /// Starts pre-rendering every emoji at <paramref name="size"/> pixels — from the disk cache when it holds them —, or,
    /// for a size too large to hold them all, rendering on demand (see <see cref="Request"/>), nothing written to disk.
    /// The previous size's pre-render stops; its bitmaps stand in for the new ones until the new run has finished (see
    /// <see cref="TryGet"/>) — of the previous runs, the one holding the most bitmaps, so quick changes of size keep the
    /// last complete one. On demand, a complete size always stands in: <paramref name="baseSize"/>'s — the 100 % size,
    /// its atlas on disk —, pre-rendered too when no complete run is at hand.
    /// </summary>
    public void Start(int size, int baseSize)
    {
        if (this.run is Run previous)
        {
            previous.Stop();
            if (previous.Bitmaps.Count > (this.fallback?.Bitmaps.Count ?? 0))
            {
                this.fallback?.Release();
                this.fallback = previous;
            }
            else
            {
                previous.Release();
            }
        }

        // Back to the size the stand-in is still pre-rendering — on demand, the base size —: it becomes the run.
        if (this.fallback is { OnDemand: false, IsCancelled: false } running && running.Size == size)
        {
            this.run = running;
            this.fallback = null;
            return;
        }

        var run = new Run(size, onDemand: (long)size * size * 4 * this.emojis.Count > CompleteMaxBytes);
        this.run = run;
        // A stand-in that will never be complete — on demand, or stopped before the end by a quick change of size —
        // leaves cells green: the base size is pre-rendered in its place.
        if (run.OnDemand && (this.fallback is null or { OnDemand: true }
            || this.fallback.IsCancelled && this.fallback.Bitmaps.Count < this.emojis.Count))
        {
            this.fallback?.Release();
            this.fallback = new Run(baseSize, onDemand: false);
            this.StartThread(this.fallback);
        }

        this.StartThread(run);
    }

    /// <summary>
    /// On demand: the emojis to render, the first ones first — the cells in view, then their neighbours. Asked again,
    /// the list replaces the previous one. Beyond about <see cref="OnDemandMaxBytes"/>, the bitmaps of emojis no longer
    /// asked for are freed. Called on the UI thread, before the cells are drawn. Nothing for a complete pre-render.
    /// </summary>
    public void Request(IReadOnlyList<string> emojis)
    {
        if (this.run is not { OnDemand: true } run || !run.SetWanted(emojis))
        {
            return;
        }

        int capacity = (int)Math.Max(emojis.Count, OnDemandMaxBytes / (4L * run.Size * run.Size));
        if (run.Bitmaps.Count <= capacity)
        {
            return;
        }

        // Freed here, on the UI thread, the one drawing them: never one being drawn.
        HashSet<string> wanted = [.. emojis];
        foreach (string emoji in run.Bitmaps.Keys)
        {
            if (!wanted.Contains(emoji) && run.Bitmaps.TryRemove(emoji, out Bitmap? bitmap))
            {
                bitmap.Dispose();
                if (run.Bitmaps.Count <= capacity)
                {
                    break;
                }
            }
        }
    }

    private void StartThread(Run run)
    {
        var thread = new Thread(() => this.PreRender(run))
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = run.OnDemand ? "Emoji on-demand render" : "Emoji pre-render",
        };
        thread.Start();
    }

    /// <summary>
    /// The emoji's bitmap at <see cref="Size"/>; while the pre-render has not reached it, the previous size's bitmap,
    /// which the caller draws stretched; null when no size has it yet.
    /// </summary>
    public Bitmap? TryGet(string emoji) =>
        this.run is Run run && run.Bitmaps.TryGetValue(emoji, out Bitmap? bitmap) ? bitmap
        : this.fallback is Run fallback && fallback.Bitmaps.TryGetValue(emoji, out bitmap) ? bitmap
        : null;

    /// <summary>
    /// No atlas written to <see cref="FolderName"/> from now on — the bitmaps keep being pre-rendered and shown. Waits
    /// for a write under way to end: once it returns, the folder can be deleted without an atlas landing in it again.
    /// </summary>
    public void StopWriting()
    {
        lock (this.writeGate)
        {
            this.writingStopped = true;
        }
    }

    public void Dispose()
    {
        this.run?.Release();
        this.run = null;
        this.fallback?.Release();
        this.fallback = null;
    }

    private static string CacheFolder => Path.Combine(AppContext.BaseDirectory, FolderName);

    private void PreRender(Run run)
    {
        if (run.OnDemand)
        {
            try
            {
                this.RenderOnDemand(run);
            }
            finally
            {
                run.Finish();
            }

            return;
        }

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
                    lock (this.writeGate)
                    {
                        if (!this.writingStopped)
                        {
                            WriteAtlas(atlas, atlasPath, keyPath, key);
                        }
                    }
                }
            }
        }
        finally
        {
            run.Finish();
            this.uiContext?.Post(_ => this.ReleaseFallback(run), null);
        }
    }

    // The current run finished: the previous size's bitmaps are no longer needed. On the UI thread, the one reading them.
    private void ReleaseFallback(Run finished)
    {
        if (finished == this.run && this.fallback is Run fallback)
        {
            this.fallback = null;
            fallback.Release();
            // An emoji the run could not render showed the previous size's bitmap: it shows green now.
            this.BitmapsReady?.Invoke(this, EventArgs.Empty);
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
                    bitmap = renderer.Render(this.emojis[i], run.Size, this.characters.Contains(this.emojis[i]));
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

    // On demand: waits for a list of emojis, renders the ones it lacks in its order, and starts over as soon as another
    // list comes — until stopped.
    private void RenderOnDemand(Run run)
    {
        using var renderer = new EmojiRenderer();
        while (!run.IsCancelled)
        {
            run.Signal.WaitOne();
            (int version, IReadOnlyList<string> wanted) = run.Wanted;
            foreach (string emoji in wanted)
            {
                if (run.IsCancelled || run.Wanted.Version != version)
                {
                    break;
                }

                if (run.Bitmaps.ContainsKey(emoji))
                {
                    continue;
                }

                try
                {
                    this.Publish(run, emoji, renderer.Render(emoji, run.Size, this.characters.Contains(emoji)));
                }
                catch (Exception exception) when (exception is COMException or ExternalException)
                {
                    Debug.WriteLine($"EmojiBitmapCache: {emoji} not rendered — {exception.Message}");
                }
            }
        }
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
        // A special character is marked: drawn as text, its bitmap is not the emoji's.
        string emojiHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
            this.emojis.Select(emoji => this.characters.Contains(emoji) ? "text:" + emoji : emoji)))));
        return string.Join('\n',
            $"format={FormatVersion}",
            $"size={size}",
            $"font={fontStamp}",
            $"emojis={emojiHash}",
            $"renderer={EmojiRenderer.FontScale.ToString(System.Globalization.CultureInfo.InvariantCulture)};{EmojiRenderer.RaiseScale.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"text={EmojiRenderer.TextFontFamily};{EmojiRenderer.TextFontScale.ToString(System.Globalization.CultureInfo.InvariantCulture)};{EmojiRenderer.TextRaiseScale.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            // The flag images in use: another Twemoji version, embedded or downloaded, may draw the same flags otherwise.
            $"flags={FlagImages.Version};{FlagImages.FolderImageCount}",
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

    // One pre-render at one size. Stopped, its thread publishes no more bitmaps, but the ones it has may still stand in
    // for another size's. Its bitmaps are disposed exactly once: by whoever comes second between the thread finishing
    // and the run being released — never while the thread may still publish, nor while the UI thread may still read them.
    private sealed class Run(int size, bool onDemand)
    {
        private readonly object gate = new();
        private bool stopped;
        private bool released;
        private bool finished;

        // On demand: the emojis asked for last, and how many lists came before.
        private (int Version, IReadOnlyList<string> Emojis) wanted = (0, []);

        public int Size { get; } = size;

        public bool OnDemand { get; } = onDemand;

        public ConcurrentDictionary<string, Bitmap> Bitmaps { get; } = new();

        public bool IsCancelled => Volatile.Read(ref this.stopped);

        // On demand: set when a list comes, or the run stops — the thread wakes up.
        public AutoResetEvent Signal { get; } = new(false);

        public (int Version, IReadOnlyList<string> Emojis) Wanted
        {
            get
            {
                lock (this.gate)
                {
                    return this.wanted;
                }
            }
        }

        // A new list for the thread; false, and the thread left alone, when it is the same as the last one.
        public bool SetWanted(IReadOnlyList<string> emojis)
        {
            lock (this.gate)
            {
                if (this.wanted.Emojis.SequenceEqual(emojis))
                {
                    return false;
                }

                this.wanted = (this.wanted.Version + 1, [.. emojis]);
            }

            this.Signal.Set();
            return true;
        }

        // The thread stops at its next emoji; the bitmaps stay.
        public void Stop()
        {
            Volatile.Write(ref this.stopped, true);
            this.Signal.Set();
        }

        // Stopped, and no longer read: the bitmaps go once the thread has finished.
        public void Release()
        {
            lock (this.gate)
            {
                Volatile.Write(ref this.stopped, true);
                this.Signal.Set();
                this.released = true;
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
                if (this.released)
                {
                    this.DisposeBitmaps();
                }
            }
        }

        public override string ToString() => $"{this.Size} px{(this.OnDemand ? ", on demand" : "")}, {this.Bitmaps.Count} bitmaps";

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
