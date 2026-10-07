using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using static EmojiSelector.Drawing.Direct2DInterop;

namespace EmojiSelector.Drawing;

/// <summary>
/// Draws an <b>emoji in colour</b>. GDI and GDI+ only draw Segoe UI Emoji in monochrome: the emoji goes through
/// Direct2D and DirectWrite, colour fonts enabled, onto a WIC bitmap whose pixels are then copied to a
/// <see cref="Bitmap"/>. Single-threaded: used from the UI thread only.
/// </summary>
internal sealed class EmojiRenderer : IDisposable
{
    public const string FontFamily = "Segoe UI Emoji";

    // The font size, as a share of the square: the line box of Segoe UI Emoji is taller than its glyphs, so a
    // font as tall as the square would crop them. At this size a glyph fills about 95% of the square's height.
    private const float FontScale = 0.88f;

    // Centred in its line box, a glyph sits low: the layout box is raised by this share of the square.
    private const float RaiseScale = 0.024f;

    private readonly ID2D1Factory direct2DFactory;
    private readonly IDWriteFactory directWriteFactory;
    private readonly IWICImagingFactory wicFactory;

    public EmojiRenderer()
    {
        Marshal.ThrowExceptionForHR(D2D1CreateFactory(
            D2D1FactoryTypeSingleThreaded, typeof(ID2D1Factory).GUID, IntPtr.Zero, out this.direct2DFactory));
        Marshal.ThrowExceptionForHR(DWriteCreateFactory(
            DWriteFactoryTypeShared, typeof(IDWriteFactory).GUID, out this.directWriteFactory));
        this.wicFactory = (IWICImagingFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(WicImagingFactoryClsid, throwOnError: true)!)!;
    }

    /// <summary>Draws <paramref name="emoji"/> centred on a transparent square of <paramref name="size"/> pixels.</summary>
    public Bitmap Render(string emoji, int size)
    {
        IWICBitmap? wicBitmap = null;
        ID2D1RenderTarget? renderTarget = null;
        IDWriteTextFormat? textFormat = null;
        ID2D1SolidColorBrush? brush = null;
        try
        {
            Marshal.ThrowExceptionForHR(this.wicFactory.CreateBitmap(
                size, size, WicPixelFormat32bppPBGRA, WicBitmapCacheOnLoad, out wicBitmap));
            var properties = new RenderTargetProperties
            {
                PixelFormat = DxgiFormatB8G8R8A8Unorm,
                AlphaMode = D2D1AlphaModePremultiplied,
                DpiX = 96,
                DpiY = 96,
            };
            Marshal.ThrowExceptionForHR(this.direct2DFactory.CreateWicBitmapRenderTarget(wicBitmap, properties, out renderTarget));
            Marshal.ThrowExceptionForHR(this.directWriteFactory.CreateTextFormat(FontFamily, IntPtr.Zero,
                DWriteFontWeightNormal, DWriteFontStyleNormal, DWriteFontStretchNormal, size * FontScale, "", out textFormat));
            Marshal.ThrowExceptionForHR(textFormat.SetTextAlignment(DWriteTextAlignmentCenter));
            Marshal.ThrowExceptionForHR(textFormat.SetParagraphAlignment(DWriteParagraphAlignmentCenter));

            // The brush only paints a glyph the font has no colour version of.
            Marshal.ThrowExceptionForHR(renderTarget.CreateSolidColorBrush(new ColorF(0, 0, 0, 1), IntPtr.Zero, out brush));

            // ClearType needs an opaque background: on a transparent one, grayscale antialiasing.
            renderTarget.SetTextAntialiasMode(D2D1TextAntialiasModeGrayscale);
            float raise = size * RaiseScale;
            renderTarget.BeginDraw();
            renderTarget.Clear(new ColorF(0, 0, 0, 0));
            renderTarget.DrawText(emoji, emoji.Length, textFormat, new RectF(0, -raise, size, size - raise), brush,
                D2D1DrawTextOptionsEnableColorFont, DWriteMeasuringModeNatural);
            Marshal.ThrowExceptionForHR(renderTarget.EndDraw(IntPtr.Zero, IntPtr.Zero));

            int stride = size * 4;
            byte[] pixels = new byte[stride * size];
            Marshal.ThrowExceptionForHR(wicBitmap.CopyPixels(IntPtr.Zero, stride, pixels.Length, pixels));
            return ToBitmap(pixels, size);
        }
        finally
        {
            Release(brush);
            Release(textFormat);
            Release(renderTarget);
            Release(wicBitmap);
        }
    }

    public void Dispose()
    {
        Release(this.wicFactory);
        Release(this.directWriteFactory);
        Release(this.direct2DFactory);
    }

    // WIC's premultiplied BGRA is the memory layout of GDI+'s Format32bppPArgb.
    private static Bitmap ToBitmap(byte[] pixels, int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            for (int y = 0; y < size; y++)
            {
                Marshal.Copy(pixels, y * size * 4, data.Scan0 + y * data.Stride, size * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static void Release(object? comObject)
    {
        if (comObject is not null)
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
