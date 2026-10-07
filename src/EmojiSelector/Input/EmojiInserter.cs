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

    /// <summary>Types <paramref name="emoji"/> into the foreground window.</summary>
    public static void Type(string emoji)
    {
        var inputs = new Input[emoji.Length * 2];
        for (int i = 0; i < emoji.Length; i++)
        {
            inputs[2 * i] = KeyInput(emoji[i], KeyEventFUnicode);
            inputs[2 * i + 1] = KeyInput(emoji[i], KeyEventFUnicode | KeyEventFKeyUp);
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input KeyInput(char codeUnit, uint flags) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion { Keyboard = new KeyboardInput { Scan = codeUnit, Flags = flags } },
    };

    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
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
    private static extern bool ShowWindow(IntPtr window, int command);
}
