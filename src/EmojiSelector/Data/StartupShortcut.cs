using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace EmojiSelector.Data;

/// <summary>
/// <b>Start with Windows</b>: the shortcut <see cref="FileName"/> in the user's Startup folder, which Windows runs at
/// sign-in. The shortcut is the setting — nothing is kept elsewhere — and it is only written or deleted when the user
/// asks: never checked, repaired or rewritten at launch.
/// </summary>
/// <remarks>
/// The Task Manager's <i>Startup apps</i> → <i>Disable</i> keeps the shortcut and writes a value named after it under
/// <see cref="ApprovedKey"/>, a binary whose first byte is odd while disabled: Windows then skips the shortcut.
/// </remarks>
internal static class StartupShortcut
{
    public const string FileName = "Emoji Selector.lnk";

    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    // MAX_PATH is not a limit for a shortcut's target; long enough for any path the File Explorer accepts.
    private const int MaxTargetLength = 32768;

    /// <summary>
    /// Whether Windows starts this exe at sign-in: the shortcut exists, its target is this exe — not another copy of
    /// the app — and the Task Manager has not disabled it. Any failure reading them reads as not started.
    /// </summary>
    public static bool IsEnabled()
    {
        if (Environment.ProcessPath is not string exe || ShortcutPath() is not string path || !File.Exists(path))
        {
            return false;
        }

        try
        {
            string? target = ReadTarget(path);
            return target is not null && SamePath(target, exe) && !IsDisabledInTaskManager();
        }
        catch (Exception exception) when (IsFailure(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the shortcut, to this exe with <paramref name="arguments"/> — replacing one to another exe — and removes
    /// the Task Manager's value, so Windows runs it again. Throws on a failure (see <see cref="IsFailure"/>).
    /// </summary>
    public static void Enable(string arguments)
    {
        string exe = Environment.ProcessPath ?? throw new IOException("The app's exe path is unknown.");
        string path = ShortcutPath() ?? throw new IOException("Windows has no Startup folder for this user.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        object link = CreateShellLink();
        try
        {
            var shellLink = (IShellLinkW)link;
            shellLink.SetPath(exe);
            shellLink.SetArguments(arguments);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(exe)!);
            shellLink.SetIconLocation(exe, 0);
            ((IPersistFile)link).Save(path, fRemember: true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }

        RemoveTaskManagerValue();
    }

    /// <summary>
    /// Deletes the shortcut and the Task Manager's value — a missing one is not an error. Throws on a failure (see
    /// <see cref="IsFailure"/>).
    /// </summary>
    public static void Disable()
    {
        if (ShortcutPath() is string path)
        {
            File.Delete(path);
        }

        RemoveTaskManagerValue();
    }

    /// <summary>The exceptions writing or reading the shortcut and the Task Manager's value can throw.</summary>
    public static bool IsFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or COMException or SecurityException
            or InvalidCastException;

    // Null when Windows gives no Startup folder.
    private static string? ShortcutPath()
    {
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return folder.Length == 0 ? null : Path.Combine(folder, FileName);
    }

    private static object CreateShellLink() =>
        Activator.CreateInstance(Type.GetTypeFromCLSID(ShellLinkInterop.ShellLinkClsid, throwOnError: true)!)!;

    private static string? ReadTarget(string path)
    {
        object link = CreateShellLink();
        try
        {
            ((IPersistFile)link).Load(path, ShellLinkInterop.StgmRead);
            var target = new StringBuilder(MaxTargetLength);
            ((IShellLinkW)link).GetPath(target, target.Capacity, IntPtr.Zero, 0);
            return target.Length == 0 ? null : target.ToString();
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    private static bool SamePath(string first, string second)
    {
        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // A missing key or value, or an unreadable one, reads as enabled.
    private static bool IsDisabledInTaskManager()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        return key?.GetValue(FileName) is byte[] { Length: > 0 } state && (state[0] & 1) == 1;
    }

    private static void RemoveTaskManagerValue()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        key?.DeleteValue(FileName, throwOnMissingValue: false);
    }
}
