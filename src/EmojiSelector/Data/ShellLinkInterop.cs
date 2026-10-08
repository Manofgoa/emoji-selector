using System.Runtime.InteropServices;
using System.Text;

namespace EmojiSelector.Data;

// The slice of the Shell's ShellLink object that StartupShortcut uses, to write and read a .lnk shortcut. A COM
// interface lists its methods in vtable order: the slots before a used method are kept as placeholders (never called)
// so the used ones land on their right slot. Parameter types follow shobjidl_core.h. The file itself is loaded and
// saved through the ShellLink's IPersistFile (System.Runtime.InteropServices.ComTypes).

internal static class ShellLinkInterop
{
    public static readonly Guid ShellLinkClsid = new("00021401-0000-0000-c000-000000000046");

    // IPersistFile.Load's mode: read only.
    public const int StgmRead = 0;
}

[ComImport, Guid("000214f9-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int fileLength, IntPtr findData, uint flags);
    void GetIDList();
    void SetIDList();
    void GetDescription();
    void SetDescription();
    void GetWorkingDirectory();
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
    void GetArguments();
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
    void GetHotkey();
    void SetHotkey();
    void GetShowCmd();
    void SetShowCmd();
    void GetIconLocation();
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
    void SetRelativePath();
    void Resolve();
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}
