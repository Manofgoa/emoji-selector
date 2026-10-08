using System.Runtime.InteropServices;
using EmojiSelector.Drawing;

namespace EmojiSelector.UI;

/// <summary>
/// The <b>tray icon</b>: the app's icon in the notification area, shown as long as the app runs. It shows the emoji
/// the user chose (<see cref="Emoji"/>), <see cref="DefaultEmoji"/> while none was. A left click raises
/// <see cref="Clicked"/>; a right click opens a menu whose <c>Exit</c> raises <see cref="ExitRequested"/>.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    /// <summary>The emoji shown while the user chose none: 🙂.</summary>
    public const string DefaultEmoji = "\U0001F642";

    public const string ExitText = "Exit";

    // NotifyIcon.Text throws beyond 127 characters.
    private const int MaxTooltipLength = 127;

    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip menu;
    private readonly EmojiRenderer renderer = new();
    private Icon? icon;

    /// <param name="tooltip">The text shown when the mouse hovers the icon: the window's title.</param>
    /// <param name="emoji">The emoji the icon shows.</param>
    public TrayIcon(string tooltip, string emoji)
    {
        this.menu = new ContextMenuStrip();
        this.menu.Items.Add(ExitText, image: null, (_, _) => this.ExitRequested?.Invoke(this, EventArgs.Empty));
        this.notifyIcon = new NotifyIcon
        {
            Text = tooltip.Length <= MaxTooltipLength ? tooltip : tooltip[..(MaxTooltipLength - 1)] + "…",
            ContextMenuStrip = this.menu,
        };
        this.notifyIcon.MouseClick += this.OnMouseClick;
        this.ShowEmoji(emoji);
        this.notifyIcon.Visible = true;
    }

    /// <summary>A left click on the icon.</summary>
    public event EventHandler? Clicked;

    /// <summary><c>Exit</c> was chosen in the icon's menu.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>The emoji the icon shows.</summary>
    public string Emoji { get; private set; } = DefaultEmoji;

    /// <summary>Redraws the icon with <paramref name="emoji"/>: the emoji the user chose.</summary>
    public void ShowEmoji(string emoji)
    {
        this.Emoji = emoji;
        // The notification area draws its icons at the small-icon size.
        int size = SystemInformation.SmallIconSize.Width;
        Icon? previous = this.icon;
        using (Bitmap bitmap = this.renderer.Render(emoji, size))
        {
            IntPtr handle = bitmap.GetHicon();
            try
            {
                // Icon.FromHandle does not own the handle: the clone does, and frees it on Dispose.
                using Icon borrowed = Icon.FromHandle(handle);
                this.icon = (Icon)borrowed.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        this.notifyIcon.Icon = this.icon;
        previous?.Dispose();
    }

    public void Dispose()
    {
        // Hidden before being disposed, so no ghost icon stays in the notification area.
        this.notifyIcon.Visible = false;
        this.notifyIcon.Dispose();
        this.menu.Dispose();
        this.icon?.Dispose();
        this.renderer.Dispose();
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            this.Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
