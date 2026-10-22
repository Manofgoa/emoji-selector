using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace EmojiSelector.UI;

/// <summary>
/// Drawing on Windows' <b>Acrylic</b> backdrop, the window's blurred background (<see cref="Apply"/>). Under the frame
/// extended into the client area, DWM blends every pixel with the blur by its alpha; GDI writes an alpha of 0, so a
/// control drawn on the backdrop paints into a premultiplied 32-bit buffer with GDI+, over a light veil (<see cref="VeilPercent"/>)
/// (<see cref="Paint"/>, <see cref="DrawText(Graphics, bool, string, Font, Rectangle, Color, TextFormatFlags)"/>,
/// <see cref="MeasureWidth"/>) and copies it, alpha included. A native child (the search box, a button, a scroll bar)
/// cannot: it is made a layered child at full alpha, so DWM composes it opaque (<see cref="SetOpaque"/>) — which needs
/// the app manifest's Windows 10 declaration.
/// </summary>
internal static class Glass
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtNone = 1;
    private const int DwmsbtTransientWindow = 3;

    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x80000;
    private const int LwaAlpha = 0x2;
    private const int SrcCopy = 0x00CC0020;

    /// <summary>
    /// The strength of the white veil over the blur, under everything drawn on it, percent: the text keeps its contrast
    /// whatever is behind the window. Set from <c>Acrylic veil ▸</c>.
    /// </summary>
    public static int VeilPercent { get; set; } = DefaultVeilPercent;

    /// <summary>The veil's strength when none is saved, percent.</summary>
    public const int DefaultVeilPercent = 40;

    /// <summary>The grey text on the backdrop: darker than <see cref="SystemColors.GrayText"/>, unreadable on the blur.</summary>
    public static readonly Color GrayText = Color.FromArgb(0x3C, 0x3C, 0x3C);

    /// <summary>Asks the window for the Acrylic backdrop under the whole window, the frame extended over it — or none.</summary>
    public static void Apply(IntPtr window, bool acrylic)
    {
        int type = acrylic ? DwmsbtTransientWindow : DwmsbtNone;
        DwmSetWindowAttribute(window, DwmwaSystemBackdropType, ref type, sizeof(int));
        Margins margins = acrylic ? new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 } : default;
        DwmExtendFrameIntoClientArea(window, ref margins);
    }

    /// <summary>
    /// Paints the clip rectangle of <paramref name="e"/> with <paramref name="paint"/> on a transparent premultiplied
    /// buffer — GDI+ only —, then copies it onto the control, alpha included.
    /// </summary>
    public static void Paint(PaintEventArgs e, Action<Graphics> paint)
    {
        Rectangle clip = e.ClipRectangle;
        if (clip.Width <= 0 || clip.Height <= 0)
        {
            return;
        }

        var header = new BitmapInfoHeader
        {
            Size = Marshal.SizeOf<BitmapInfoHeader>(),
            Width = clip.Width,
            Height = -clip.Height,
            Planes = 1,
            BitCount = 32,
        };
        IntPtr dib = CreateDIBSection(IntPtr.Zero, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
        if (dib == IntPtr.Zero)
        {
            return;
        }

        try
        {
            using (var bitmap = new Bitmap(clip.Width, clip.Height, clip.Width * 4, PixelFormat.Format32bppPArgb, bits))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(VeilPercent * 255 / 100, Color.White));
                graphics.TranslateTransform(-clip.X, -clip.Y);
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                paint(graphics);
            }

            IntPtr target = e.Graphics.GetHdc();
            try
            {
                IntPtr memory = CreateCompatibleDC(target);
                IntPtr previous = SelectObject(memory, dib);
                BitBlt(target, clip.X, clip.Y, clip.Width, clip.Height, memory, 0, 0, SrcCopy);
                SelectObject(memory, previous);
                DeleteDC(memory);
            }
            finally
            {
                e.Graphics.ReleaseHdc(target);
            }
        }
        finally
        {
            DeleteObject(dib);
        }
    }

    /// <summary>TextRenderer.DrawText off the backdrop; GDI+'s DrawString, with the same alignment, on it.</summary>
    public static void DrawText(Graphics graphics, bool glass, string text, Font font, Rectangle bounds, Color color,
        TextFormatFlags flags)
    {
        if (!glass)
        {
            TextRenderer.DrawText(graphics, text, font, bounds, color, flags);
            return;
        }

        using var format = CreateFormat(flags);
        using var brush = new SolidBrush(OnGlass(color));
        graphics.DrawString(text, font, brush, bounds, format);
    }

    /// <summary>TextRenderer.DrawText off the backdrop; GDI+'s DrawString on it.</summary>
    public static void DrawText(Graphics graphics, bool glass, string text, Font font, Point location, Color color,
        TextFormatFlags flags)
    {
        if (!glass)
        {
            TextRenderer.DrawText(graphics, text, font, location, color, flags);
            return;
        }

        using var format = CreateFormat(flags);
        using var brush = new SolidBrush(OnGlass(color));
        graphics.DrawString(text, font, brush, location, format);
    }

    // The Graphics the words are measured with, as the buffers of Paint draw them.
    private static readonly Graphics Measuring = CreateMeasuring();

    /// <summary>The width of <paramref name="text"/> drawn by <see cref="DrawText(Graphics, bool, string, Font, Point, Color, TextFormatFlags)"/> on the backdrop.</summary>
    public static int MeasureWidth(string text, Font font)
    {
        using var format = CreateFormat(TextFormatFlags.NoPrefix);
        return (int)Math.Ceiling(Measuring.MeasureString(text, font, PointF.Empty, format).Width);
    }

    private static Graphics CreateMeasuring()
    {
        Graphics graphics = Graphics.FromImage(new Bitmap(1, 1, PixelFormat.Format32bppPArgb));
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        return graphics;
    }

    /// <summary>
    /// A native child composed on the backdrop — a layered child at <paramref name="alpha"/>, opaque at 255 — or back to
    /// a plain child.
    /// </summary>
    public static void SetOpaque(Control control, bool opaque, byte alpha = 255)
    {
        // A hidden control has no handle yet (the search's ✕): applied once it is created.
        if (!control.IsHandleCreated)
        {
            EventHandler? created = null;
            created = (_, _) =>
            {
                control.HandleCreated -= created;
                SetOpaque(control, opaque, alpha);
            };
            control.HandleCreated += created;
            return;
        }

        int style = GetWindowLongW(control.Handle, GwlExStyle);
        int wanted = opaque ? style | WsExLayered : style & ~WsExLayered;
        if (wanted != style)
        {
            SetWindowLongW(control.Handle, GwlExStyle, wanted);
        }

        if (opaque)
        {
            SetLayeredWindowAttributes(control.Handle, 0, alpha, LwaAlpha);
        }

        control.Invalidate(invalidateChildren: true);
    }

    // The system's grey text darkened on the backdrop; any other colour as it is.
    private static Color OnGlass(Color color) => color.ToArgb() == SystemColors.GrayText.ToArgb() ? GrayText : color;

    private static StringFormat CreateFormat(TextFormatFlags flags)
    {
        var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
        format.Alignment = flags.HasFlag(TextFormatFlags.HorizontalCenter) ? StringAlignment.Center
            : flags.HasFlag(TextFormatFlags.Right) ? StringAlignment.Far
            : StringAlignment.Near;
        format.LineAlignment = flags.HasFlag(TextFormatFlags.VerticalCenter) ? StringAlignment.Center
            : flags.HasFlag(TextFormatFlags.Bottom) ? StringAlignment.Far
            : StringAlignment.Near;
        format.Trimming = flags.HasFlag(TextFormatFlags.EndEllipsis) ? StringTrimming.EllipsisCharacter : StringTrimming.None;
        format.HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None;
        return format;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left, Right, Top, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfoHeader header, uint usage, out IntPtr bits,
        IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX,
        int sourceY, int operation);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr gdiObject);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetWindowLongW(IntPtr window, int index);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int SetWindowLongW(IntPtr window, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);
}
