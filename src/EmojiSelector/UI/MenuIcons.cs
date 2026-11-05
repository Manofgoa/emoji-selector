using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using EmojiSelector.Drawing;

namespace EmojiSelector.UI;

/// <summary>
/// The <b>icons of the menus</b>: one emoji per action or toggle, drawn in colour in front of its entry — the only
/// place listing them, so an action shown in several menus has one icon. An icon is rendered by Direct2D
/// (<see cref="EmojiRenderer"/>, GDI would draw it in one colour) at <see cref="Oversampling"/> times its side, then
/// scaled down to <see cref="LogicalSize"/> logical pixels in device pixels; each (emoji, size) once, kept for the
/// app's life. UI thread only: the renderer is single-threaded.
/// </summary>
/// <remarks>
/// Never on a submenu's values (percentages, group names, tones) nor on a colour item, which shows its swatch.
/// </remarks>
internal static class MenuIcons
{
    /// <summary>The side of an icon, in logical pixels: a menu's image size.</summary>
    public const int LogicalSize = 16;

    public const string Frequent = "⭐"; // ⭐ — the frequent tab's star
    public const string Clear = "\U0001F9F9"; // 🧹
    public const string NewGroup = "➕"; // ➕
    public const string CustomGroups = "❤️"; // ❤️ — the custom tab's heart
    public const string SubCategories = "\U0001F5C2️"; // 🗂️
    public const string SpecialCharacters = "\U0001F523"; // 🔣
    public const string French = "\U0001F1EB\U0001F1F7"; // 🇫🇷 — a flag image
    public const string Zoom = "\U0001F50D"; // 🔍
    public const string Tone = "\U0001F3A8"; // 🎨
    public const string WindowSize = "\U0001FA9F"; // 🪟
    public const string Opacity = "\U0001F505"; // 🔅
    public const string AcrylicVeil = "\U0001F9CA"; // 🧊
    public const string AppFolder = "\U0001F4C2"; // 📂
    public const string StartWithWindows = "\U0001F680"; // 🚀
    public const string EmojiUpdates = "\U0001F504"; // 🔄
    public const string ResetAll = "♻️"; // ♻️
    public const string Person = "\U0001F464"; // 👤
    public const string Remove = "➖"; // ➖
    public const string Hide = "\U0001F648"; // 🙈
    public const string Rename = "✏️"; // ✏️
    public const string Reorder = "\U0001F500"; // 🔀
    public const string MoveUp = "⬆️"; // ⬆️
    public const string MoveDown = "⬇️"; // ⬇️
    public const string Delete = "\U0001F5D1️"; // 🗑️
    public const string Exit = "\U0001F6AA"; // 🚪

    // Rendered larger, then scaled down: an emoji drawn straight at 16 px loses its details.
    private const int Oversampling = 4;

    private static readonly Dictionary<(string Emoji, int Size), Bitmap> Icons = [];

    // The emoji each item shows, so a menu set again at each opening draws only what changed.
    private static readonly ConditionalWeakTable<ToolStripItem, string> ItemEmojis = [];

    // The control hosts' icons, drawn in the image margin by MenuRenderer.
    private static readonly ConditionalWeakTable<ToolStripControlHost, HostIcon> HostIcons = [];

    private static EmojiRenderer? renderer;

    /// <summary>The side of an icon, in device pixels, at <paramref name="control"/>'s DPI.</summary>
    public static int SizeFor(Control control) => control.LogicalToDeviceUnits(LogicalSize);

    /// <summary>
    /// The icon of <paramref name="emoji"/>, <paramref name="size"/> device pixels square; the same bitmap for the
    /// same emoji and size. Shared: never disposed by its users.
    /// </summary>
    public static Bitmap Get(string emoji, int size)
    {
        if (!Icons.TryGetValue((emoji, size), out Bitmap? icon))
        {
            renderer ??= new EmojiRenderer();
            using Bitmap large = renderer.Render(emoji, size * Oversampling);
            icon = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(icon))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.DrawImage(large, new Rectangle(0, 0, size, size));
            }

            Icons[(emoji, size)] = icon;
        }

        return icon;
    }

    /// <summary>
    /// Shows <paramref name="emoji"/> as <paramref name="item"/>'s image, <paramref name="size"/> device pixels
    /// square, not scaled by the menu; its copy disposed with the item. Set again with the same emoji and size, nothing
    /// changes. Checked, WinForms draws the icon framed in place of the ✓; disabled, greyed.
    /// </summary>
    public static void Set(ToolStripItem item, string emoji, int size)
    {
        bool known = ItemEmojis.TryGetValue(item, out string? shown);
        if (known && shown == emoji && item.Image?.Width == size)
        {
            return;
        }

        if (!known)
        {
            item.Disposed += (_, _) => item.Image?.Dispose();
        }

        ItemEmojis.AddOrUpdate(item, emoji);
        Image? previous = item.Image;
        item.ImageScaling = ToolStripItemImageScaling.None;
        item.Image = (Image)Get(emoji, size).Clone();
        previous?.Dispose();
    }

    /// <summary>
    /// Shows <paramref name="emoji"/> in the image margin of <paramref name="host"/>'s row — a hosted control has no
    /// image of its own —, <paramref name="size"/> device pixels square. Drawn by a menu whose renderer is a
    /// <see cref="MenuRenderer"/>.
    /// </summary>
    public static void SetHostIcon(ToolStripControlHost host, string emoji, int size) =>
        HostIcons.AddOrUpdate(host, new HostIcon(emoji, size));

    private sealed record HostIcon(string Emoji, int Size);

    /// <summary>
    /// WinForms' default menu renderer, which also draws the icons of the control hosts (<see cref="SetHostIcon"/>) in
    /// the image margin, centred on their row, where the items' images are.
    /// </summary>
    public sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            base.OnRenderImageMargin(e);
            Rectangle margin = e.AffectedBounds;
            foreach (ToolStripItem item in e.ToolStrip.Items)
            {
                if (item is ToolStripControlHost host && host.Available && HostIcons.TryGetValue(host, out HostIcon? icon))
                {
                    Rectangle row = host.Bounds;
                    var bounds = new Rectangle(margin.Left + (margin.Width - icon.Size) / 2,
                        row.Top + (row.Height - icon.Size) / 2, icon.Size, icon.Size);
                    e.Graphics.DrawImage(Get(icon.Emoji, icon.Size), bounds);
                }
            }
        }
    }
}
