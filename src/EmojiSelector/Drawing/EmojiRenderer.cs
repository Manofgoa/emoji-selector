using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using static EmojiSelector.Drawing.Direct2DInterop;

namespace EmojiSelector.Drawing;

/// <summary>
/// Draws an <b>emoji in colour</b>. GDI and GDI+ only draw Segoe UI Emoji in monochrome: the emoji goes through
/// Direct2D and DirectWrite, colour fonts enabled, onto a WIC bitmap whose pixels are then copied to a
/// <see cref="Bitmap"/>. The WIC bitmap, render target, text formats and brush are built once per size and reused:
/// they cost far more than drawing a glyph. Single-threaded: one instance per thread.
/// </summary>
/// <remarks>
/// A <b>special character</b> is drawn as text instead: monochrome, with <see cref="TextFontFamily"/>, smaller —
/// Segoe UI Emoji would draw © ® ™ in colour, and take the others from another font.
/// </remarks>
internal sealed class EmojiRenderer : IDisposable
{
    public const string FontFamily = "Segoe UI Emoji";

    // The font size, as a share of the square: the line box of Segoe UI Emoji is taller than its glyphs, so a
    // font as tall as the square would crop them. At this size a glyph fills about 95% of the square's height.
    public const float FontScale = 0.88f;

    // Centred in its line box, a glyph sits low: the layout box is raised by this share of the square.
    public const float RaiseScale = 0.024f;

    /// <summary>The font of the special characters.</summary>
    public const string TextFontFamily = "Segoe UI";

    // The special characters' font size, as a share of the square: a letter and its accent fit in it with room around,
    // like a glyph among the emojis.
    public const float TextFontScale = 0.8f;

    // Centred in its line box, a capital of Segoe UI sits low: the layout box is raised by this share of the square.
    public const float TextRaiseScale = 0.05f;

    private readonly ID2D1Factory direct2DFactory;
    private readonly IDWriteFactory directWriteFactory;
    private readonly IWICImagingFactory wicFactory;

    // The resources of the size last rendered, rebuilt when another size is asked for.
    private int size;
    private IWICBitmap? wicBitmap;
    private ID2D1RenderTarget? renderTarget;
    private IDWriteTextFormat? textFormat;
    private IDWriteTextFormat? characterFormat;
    private ID2D1SolidColorBrush? brush;

    public EmojiRenderer()
    {
        Marshal.ThrowExceptionForHR(D2D1CreateFactory(
            D2D1FactoryTypeSingleThreaded, typeof(ID2D1Factory).GUID, IntPtr.Zero, out this.direct2DFactory));
        // Isolated, not shared: the shared factory is one COM object per process, and its wrapper, created on the
        // thread of the first renderer, cannot be used from another thread's renderer (E_NOINTERFACE).
        Marshal.ThrowExceptionForHR(DWriteCreateFactory(
            DWriteFactoryTypeIsolated, typeof(IDWriteFactory).GUID, out this.directWriteFactory));
        this.wicFactory = (IWICImagingFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(WicImagingFactoryClsid, throwOnError: true)!)!;
    }

    /// <summary>
    /// Draws <paramref name="emoji"/> centred on a transparent square of <paramref name="size"/> pixels — as a special
    /// character, monochrome with the text font, when <paramref name="asText"/>.
    /// </summary>
    public Bitmap Render(string emoji, int size, bool asText = false)
    {
        try
        {
            this.EnsureResources(size);
            ID2D1RenderTarget renderTarget = this.renderTarget!;
            float raise = size * (asText ? TextRaiseScale : RaiseScale);
            renderTarget.BeginDraw();
            renderTarget.Clear(new ColorF(0, 0, 0, 0));
            renderTarget.DrawText(emoji, emoji.Length, asText ? this.characterFormat! : this.textFormat!,
                new RectF(0, -raise, size, size - raise), this.brush!,
                asText ? D2D1DrawTextOptionsNone : D2D1DrawTextOptionsEnableColorFont, DWriteMeasuringModeNatural);
            Marshal.ThrowExceptionForHR(renderTarget.EndDraw(IntPtr.Zero, IntPtr.Zero));
            return this.CopyToBitmap(size);
        }
        catch
        {
            // A failed draw may leave the render target unusable: the next call rebuilds everything.
            this.ReleaseResources();
            throw;
        }
    }

    public void Dispose()
    {
        this.ReleaseResources();
        Release(this.wicFactory);
        Release(this.directWriteFactory);
        Release(this.direct2DFactory);
    }

    private void EnsureResources(int size)
    {
        if (this.renderTarget is not null && this.size == size)
        {
            return;
        }

        this.ReleaseResources();
        Marshal.ThrowExceptionForHR(this.wicFactory.CreateBitmap(
            size, size, WicPixelFormat32bppPBGRA, WicBitmapCacheOnLoad, out IWICBitmap wicBitmap));
        this.wicBitmap = wicBitmap;
        var properties = new RenderTargetProperties
        {
            PixelFormat = DxgiFormatB8G8R8A8Unorm,
            AlphaMode = D2D1AlphaModePremultiplied,
            DpiX = 96,
            DpiY = 96,
        };
        Marshal.ThrowExceptionForHR(this.direct2DFactory.CreateWicBitmapRenderTarget(wicBitmap, properties, out ID2D1RenderTarget renderTarget));
        this.renderTarget = renderTarget;
        Marshal.ThrowExceptionForHR(this.directWriteFactory.CreateTextFormat(FontFamily, IntPtr.Zero,
            DWriteFontWeightNormal, DWriteFontStyleNormal, DWriteFontStretchNormal, size * FontScale, "", out IDWriteTextFormat textFormat));
        this.textFormat = textFormat;
        Marshal.ThrowExceptionForHR(textFormat.SetTextAlignment(DWriteTextAlignmentCenter));
        Marshal.ThrowExceptionForHR(textFormat.SetParagraphAlignment(DWriteParagraphAlignmentCenter));
        Marshal.ThrowExceptionForHR(this.directWriteFactory.CreateTextFormat(TextFontFamily, IntPtr.Zero,
            DWriteFontWeightNormal, DWriteFontStyleNormal, DWriteFontStretchNormal, size * TextFontScale, "", out IDWriteTextFormat characterFormat));
        this.characterFormat = characterFormat;
        Marshal.ThrowExceptionForHR(characterFormat.SetTextAlignment(DWriteTextAlignmentCenter));
        Marshal.ThrowExceptionForHR(characterFormat.SetParagraphAlignment(DWriteParagraphAlignmentCenter));

        // The brush paints a glyph the font has no colour version of, and the special characters.
        Marshal.ThrowExceptionForHR(renderTarget.CreateSolidColorBrush(new ColorF(0, 0, 0, 1), IntPtr.Zero, out ID2D1SolidColorBrush brush));
        this.brush = brush;

        // ClearType needs an opaque background: on a transparent one, grayscale antialiasing.
        renderTarget.SetTextAntialiasMode(D2D1TextAntialiasModeGrayscale);
        this.size = size;
    }

    // WIC's premultiplied BGRA is the memory layout of GDI+'s Format32bppPArgb: the pixels go straight in.
    private Bitmap CopyToBitmap(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try
        {
            Marshal.ThrowExceptionForHR(this.wicBitmap!.CopyPixels(IntPtr.Zero, data.Stride, data.Stride * size, data.Scan0));
        }
        catch
        {
            bitmap.UnlockBits(data);
            bitmap.Dispose();
            throw;
        }

        bitmap.UnlockBits(data);
        return bitmap;
    }

    private void ReleaseResources()
    {
        Release(this.brush);
        Release(this.textFormat);
        Release(this.characterFormat);
        Release(this.renderTarget);
        Release(this.wicBitmap);
        this.brush = null;
        this.textFormat = null;
        this.characterFormat = null;
        this.renderTarget = null;
        this.wicBitmap = null;
    }

    private static void Release(object? comObject)
    {
        if (comObject is not null)
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
