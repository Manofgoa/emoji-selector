using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace EmojiSelector;

/// <summary>
/// One instance per exe: a named mutex keyed by the exe's full path, so two builds in two folders — a worktree's next to
/// the main checkout's — still run side by side, while the same exe launched twice does not. The first instance also
/// creates a named event, which a later launch sets to have its window shown.
/// </summary>
/// <remarks>
/// <c>Local\</c>: one instance per Windows session. A mutex or an event that cannot be created leaves the app running as
/// before, without the single instance — never an error.
/// </remarks>
internal sealed class SingleInstance : IDisposable
{
    private const int AsfwAny = -1;

    private readonly string showEventName;
    private Mutex? mutex;
    private EventWaitHandle? showEvent;
    private ManualResetEvent? stopListening;
    private Thread? listener;

    public SingleInstance()
    {
        string exe = Environment.ProcessPath ?? AppContext.BaseDirectory;
        // A mutex name takes no backslash: the path is hashed, upper-cased first since Windows paths ignore case.
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(exe).ToUpperInvariant())))[..16];
        string name = $@"Local\EmojiSelector-{key}";
        this.showEventName = $"{name}-show";
        try
        {
            this.mutex = new Mutex(initiallyOwned: true, name, out bool createdNew);
            if (!createdNew)
            {
                this.mutex.Dispose();
                this.mutex = null;
                this.IsFirst = false;
                return;
            }

            // Created right after the mutex, before anything is loaded: a launch finding the mutex finds the event too.
            this.showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, this.showEventName);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException
            or WaitHandleCannotBeOpenedException)
        {
            this.Release();
        }
    }

    /// <summary>
    /// Whether this is the only instance of this exe — true too when the mutex could not be created: the app then
    /// runs without the single instance.
    /// </summary>
    public bool IsFirst { get; } = true;

    /// <summary>A later launch of this exe asked for the window. Raised on the UI thread.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>
    /// Starts waiting for the later launches' requests, on a thread of its own. Must be called on the UI thread, after
    /// a control: <see cref="ShowRequested"/> is posted through its synchronization context.
    /// </summary>
    public void Listen()
    {
        if (this.showEvent is null || this.listener is not null)
        {
            return;
        }

        SynchronizationContext context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("The single instance must listen on the UI thread.");
        EventWaitHandle showEvent = this.showEvent;
        var stop = this.stopListening = new ManualResetEvent(false);
        this.listener = new Thread(() =>
        {
            while (WaitHandle.WaitAny([showEvent, stop]) == 0)
            {
                context.Post(_ => this.ShowRequested?.Invoke(this, EventArgs.Empty), null);
            }
        })
        { IsBackground = true, Name = "Single instance" };
        this.listener.Start();
    }

    /// <summary>
    /// From a later launch: asks the first instance to show its window. This process, the one the user just launched,
    /// may hand it the foreground. Nothing when the first instance has no event (it runs without the single instance).
    /// </summary>
    public void ShowFirst()
    {
        if (EventWaitHandle.TryOpenExisting(this.showEventName, out EventWaitHandle? first))
        {
            using (first)
            {
                AllowSetForegroundWindow(AsfwAny);
                first.Set();
            }
        }
    }

    /// <summary>
    /// Releases the mutex and the event: a process started after this call is the first instance. Called on the thread
    /// that created it — a mutex is released by its owner.
    /// </summary>
    public void Dispose() => this.Release();

    private void Release()
    {
        if (this.listener is not null)
        {
            this.stopListening!.Set();
            this.listener.Join();
            this.listener = null;
        }

        this.stopListening?.Dispose();
        this.stopListening = null;
        this.showEvent?.Dispose();
        this.showEvent = null;
        if (this.mutex is not null)
        {
            this.mutex.ReleaseMutex();
            this.mutex.Dispose();
            this.mutex = null;
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
