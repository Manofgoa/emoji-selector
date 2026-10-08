using System.Runtime.InteropServices;

namespace EmojiSelector.Input;

/// <summary>
/// The <b>shortcut</b> Win+; — and Win+., the same — caught by a low-level keyboard hook: Windows takes both for its
/// own emoji panel, so <c>RegisterHotKey</c> cannot have them. The hook swallows the <c>;</c> or <c>.</c> key while a
/// Windows key is held, so Windows never sees the shortcut; when the app ends, Windows removes the hook and its own
/// panel answers them again. An app's "Emoji — Windows+Period" menu entry that injects Win+. (Chromium) is caught
/// too; one that opens the panel through an API sends no key, and gets Windows' panel.
/// The hook runs on a thread of its own, with its own message loop: on the UI thread, every key typed in any app
/// would wait whenever the UI is busy, and Windows silently removes a hook too slow to answer
/// (<c>LowLevelHooksTimeout</c>). Must be created on the UI thread, after a control: <see cref="Pressed"/> is posted
/// through its synchronization context.
/// </summary>
/// <remarks>
/// The hook is not called while an elevated (administrator) window has the focus (UIPI): Windows' own panel opens
/// there, which the app could not type into anyway.
/// </remarks>
internal sealed class ShortcutHook : IDisposable
{
    /// <summary>
    /// Marks the events the app injects itself — the hook's dummy key, the emoji's characters — so the hook lets them
    /// through untouched.
    /// </summary>
    public static readonly IntPtr InjectedMarker = new(0x454D4F4A);

    // The longest the keys are swallowed for a keep-open insertion, should StopSwallowing never come: the keyboard is
    // never left swallowed.
    private const long MaxSwallowMilliseconds = 1000;

    // Kept in a field: the hook calls it for as long as it is installed, the garbage collector must not collect it.
    private readonly LowLevelKeyboardProc callback;
    private readonly SynchronizationContext context;
    private readonly Thread thread;
    private uint threadId;

    // The virtual-key code of the `;` or `.` key being swallowed, from its key-down to its key-up; 0 when none.
    private uint swallowedKey;

    // Environment.TickCount64 until which the physical keys are swallowed, the modifiers excepted — a keep-open
    // insertion handing the foreground back. Written by the UI thread, read by the hook's.
    private long swallowUntil;

    // The keys whose key-down was swallowed during a keep-open insertion: their key-up is swallowed too, even once it
    // ended. The hook's thread only.
    private readonly HashSet<uint> swallowedDuringInsertion = [];

    public ShortcutHook()
    {
        this.context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("The shortcut hook must be created on the UI thread.");
        this.callback = this.OnKey;
        using var started = new ManualResetEventSlim();
        this.thread = new Thread(() => this.Run(started)) { IsBackground = true, Name = "Win+; and Win+. hook" };
        this.thread.Start();
        started.Wait();
    }

    /// <summary>Win+; or Win+. was pressed. Raised on the UI thread, once per press: auto-repeat raises nothing.</summary>
    public event EventHandler? Pressed;

    /// <summary>
    /// A keep-open insertion starts: until <see cref="StopSwallowing"/> — one second at most — the keys pressed are
    /// swallowed, never typed into the previous window it hands the keyboard to. The modifiers go through: their state
    /// follows the user's fingers.
    /// </summary>
    public void SwallowKeys() => Volatile.Write(ref this.swallowUntil, Environment.TickCount64 + MaxSwallowMilliseconds);

    /// <summary>The keep-open insertion ended: the keys go through again.</summary>
    public void StopSwallowing() => Volatile.Write(ref this.swallowUntil, 0);

    public void Dispose()
    {
        PostThreadMessageW(this.threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        this.thread.Join();
    }

    // The hook's thread: installs it, runs the message loop the hook is called from, removes it on WM_QUIT.
    private void Run(ManualResetEventSlim started)
    {
        this.threadId = GetCurrentThreadId();

        // Called once, before the hook is installed: it gives the thread its message queue, so the WM_QUIT of
        // Dispose cannot get lost.
        PeekMessageW(out _, IntPtr.Zero, 0, 0, PmNoRemove);
        IntPtr hook = SetWindowsHookExW(WhKeyboardLl, this.callback, GetModuleHandleW(null), 0);
        started.Set();
        while (GetMessageW(out Message message, IntPtr.Zero, 0, 0) > 0)
        {
            DispatchMessageW(in message);
        }

        if (hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hook);
        }
    }

    // Recognises the keys and nothing more, as fast as it can: the event is posted to the UI thread, never raised
    // from here.
    private IntPtr OnKey(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            var key = Marshal.PtrToStructure<KeyboardHookData>(data);
            if (key.ExtraInfo != InjectedMarker
                && (this.SwallowsDuringInsertion((uint)message, key.VirtualKey) || this.Swallows((uint)message, key.VirtualKey)))
            {
                return 1;
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    // While a keep-open insertion hands the foreground back, a key-down — a quick second Ctrl+Enter, Enter's
    // auto-repeat — would land in the previous window: swallowed, the modifiers excepted. A key-up is swallowed only
    // when its key-down was: one whose key-down went through must reach Windows, or the key would stay down.
    private bool SwallowsDuringInsertion(uint message, uint virtualKey)
    {
        if (message is WmKeyUp or WmSysKeyUp)
        {
            return this.swallowedDuringInsertion.Remove(virtualKey);
        }

        if (message is not (WmKeyDown or WmSysKeyDown) || IsModifier(virtualKey)
            || Environment.TickCount64 >= Volatile.Read(ref this.swallowUntil))
        {
            return false;
        }

        // The auto-repeat of a key held since before the insertion is swallowed, but its key-up is not: Windows saw it
        // go down.
        if (!IsDown((int)virtualKey))
        {
            this.swallowedDuringInsertion.Add(virtualKey);
        }

        return true;
    }

    private static bool IsModifier(uint virtualKey) =>
        virtualKey is VkShift or VkControl or VkMenu or VkLWin or VkRWin or (>= VkLShift and <= VkRMenu);

    private bool Swallows(uint message, uint virtualKey)
    {
        if (message is WmKeyUp or WmSysKeyUp)
        {
            // The `;` or `.` key-up matching a swallowed key-down is swallowed too; the Windows key's never is.
            if (virtualKey == this.swallowedKey)
            {
                this.swallowedKey = 0;
                return true;
            }

            return false;
        }

        if (message is not (WmKeyDown or WmSysKeyDown))
        {
            return false;
        }

        if (virtualKey == this.swallowedKey)
        {
            // Auto-repeat of the held shortcut: swallowed, raises nothing.
            return true;
        }

        if ((!IsDown(VkLWin) && !IsDown(VkRWin)) || !IsShortcutKey(virtualKey))
        {
            return false;
        }

        this.swallowedKey = virtualKey;

        // Windows opens the Start menu when the Windows key goes down then up with no key between — what it sees
        // once `;` or `.` is swallowed. A dummy key between them prevents it; sent by this app, it also lets the app take
        // the foreground.
        InjectDummyKey();
        this.context.Post(_ => this.Pressed?.Invoke(this, EventArgs.Empty), null);
        return true;
    }

    private static bool IsShortcutKey(uint virtualKey) => IsPeriodKey(virtualKey) || IsSemicolonKey(virtualKey);

    // VK_OEM_PERIOD whatever the layout — the key Windows answers Win+. on, and the one Chromium's "Emoji" menu
    // entry injects — Shift, Ctrl and Alt up. On AZERTY it is the `; .` key unshifted, Win+; itself.
    private static bool IsPeriodKey(uint virtualKey) =>
        virtualKey == VkOemPeriod && !IsDown(VkShift) && !IsDown(VkControl) && !IsDown(VkMenu);

    // The key typing `;` in the keyboard layout of the window in front — VK_OEM_1 on QWERTY, the `; .` key on
    // AZERTY — with the very modifiers that layout needs for it (none on both). Ctrl and Alt never.
    private static bool IsSemicolonKey(uint virtualKey)
    {
        uint thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        short scan = VkKeyScanExW(';', GetKeyboardLayout(thread));
        if (scan == -1 || (scan & 0xFF) != virtualKey)
        {
            return false;
        }

        bool needsShift = (scan & 0x100) != 0;
        bool needsOther = (scan & 0x600) != 0;
        return !needsOther && IsDown(VkShift) == needsShift && !IsDown(VkControl) && !IsDown(VkMenu);
    }

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static void InjectDummyKey()
    {
        var inputs = new Input[]
        {
            DummyKeyInput(0),
            DummyKeyInput(KeyEventFKeyUp),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input DummyKeyInput(uint flags) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput { VirtualKey = VkUnassigned, Flags = flags, ExtraInfo = InjectedMarker },
        },
    };

    private const int WhKeyboardLl = 13;
    private const uint WmQuit = 0x0012;
    private const uint PmNoRemove = 0x0000;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int VkLShift = 0xA0;
    private const int VkRMenu = 0xA5;
    private const uint VkOemPeriod = 0xBE;

    // A virtual-key code Windows assigns to nothing: the dummy key means nothing to any app.
    private const ushort VkUnassigned = 0xE8;
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Id;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    // The mouse member is the largest: it gives the union, so INPUT, its native size.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr SetWindowsHookExW(int hookType, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool PeekMessageW(out Message message, IntPtr window, uint filterMin, uint filterMax, uint remove);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetMessageW(out Message message, IntPtr window, uint filterMin, uint filterMax);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr DispatchMessageW(in Message message);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool PostThreadMessageW(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanExW(char character, IntPtr layout);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
