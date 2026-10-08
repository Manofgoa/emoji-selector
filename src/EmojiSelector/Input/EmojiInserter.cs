using System.Runtime.InteropServices;

namespace EmojiSelector.Input;

/// <summary>
/// Inserts an emoji into another app's window: brings it back to the foreground, then types the emoji's character
/// sequence with <c>SendInput</c> and <c>KEYEVENTF_UNICODE</c> — one key down / key up pair per UTF-16 code unit,
/// no clipboard involved.
/// </summary>
/// <remarks>
/// Windows blocks input sent into an elevated (administrator) window from a non-elevated app (UIPI): the emoji is
/// then not inserted, silently.
/// </remarks>
internal static class EmojiInserter
{
    /// <summary>
    /// Brings <paramref name="window"/> to the foreground. Called while the app is still the foreground one: only the
    /// foreground app may hand the foreground over.
    /// </summary>
    public static void Activate(IntPtr window)
    {
        if (IsIconic(window))
        {
            ShowWindow(window, SwRestore);
        }

        SetForegroundWindow(window);
    }

    /// <summary>
    /// Types <paramref name="emoji"/> into the foreground window. A Ctrl key held — Ctrl+Enter, Ctrl+click — is
    /// released before the characters and pressed again after them, in the same call: the window sees plain
    /// characters, never a Ctrl shortcut, and the keyboard's state matches the user's fingers once done. Every event
    /// carries <see cref="ShortcutHook.InjectedMarker"/>: the hook lets them through.
    /// </summary>
    public static void Type(string emoji)
    {
        ushort[] heldControls = [.. ControlKeys.Where(IsDown)];
        var inputs = new List<Input>(emoji.Length * 2 + heldControls.Length * 2);
        inputs.AddRange(heldControls.Select(key => VirtualKeyInput(key, KeyEventFKeyUp)));
        foreach (char codeUnit in emoji)
        {
            inputs.Add(KeyInput(codeUnit, KeyEventFUnicode));
            inputs.Add(KeyInput(codeUnit, KeyEventFUnicode | KeyEventFKeyUp));
        }

        inputs.AddRange(heldControls.Select(key => VirtualKeyInput(key, 0)));
        SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<Input>());
    }

    private static Input KeyInput(char codeUnit, uint flags) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion { Keyboard = new KeyboardInput { Scan = codeUnit, Flags = flags, ExtraInfo = ShortcutHook.InjectedMarker } },
    };

    // The right Ctrl key is an extended key: without the flag, Windows would see the left one.
    private static Input VirtualKeyInput(ushort virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = virtualKey == VkRControl ? flags | KeyEventFExtendedKey : flags,
                ExtraInfo = ShortcutHook.InjectedMarker,
            },
        },
    };

    private static bool IsDown(ushort virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private const uint InputKeyboard = 1;
    private const uint KeyEventFExtendedKey = 0x0001;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const ushort VkLControl = 0xA2;
    private const ushort VkRControl = 0xA3;
    private static readonly ushort[] ControlKeys = [VkLControl, VkRControl];
    private const int SwRestore = 9;

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
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool ShowWindow(IntPtr window, int command);
}
