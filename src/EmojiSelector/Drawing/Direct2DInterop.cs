using System.Runtime.InteropServices;

namespace EmojiSelector.Drawing;

// The slice of Direct2D, DirectWrite and WIC that EmojiRenderer uses. A COM interface lists its methods in
// vtable order: the slots before a used method are kept as placeholders (never called) so the used ones land on
// their right slot. Parameter types follow d2d1.h, dwrite.h and wincodec.h.

internal static class Direct2DInterop
{
    public const int D2D1FactoryTypeSingleThreaded = 0;
    public const int DWriteFactoryTypeShared = 0;
    public const int DxgiFormatB8G8R8A8Unorm = 87;
    public const int D2D1AlphaModePremultiplied = 1;
    public const int D2D1TextAntialiasModeGrayscale = 2;
    public const int D2D1DrawTextOptionsEnableColorFont = 4;
    public const int DWriteMeasuringModeNatural = 0;
    public const int DWriteFontWeightNormal = 400;
    public const int DWriteFontStyleNormal = 0;
    public const int DWriteFontStretchNormal = 5;
    public const int DWriteTextAlignmentCenter = 2;
    public const int DWriteParagraphAlignmentCenter = 2;
    public const int WicBitmapCacheOnLoad = 2;

    public static readonly Guid WicImagingFactoryClsid = new("cacaf262-9370-4615-a13b-9f5539da4c0a");
    public static readonly Guid WicPixelFormat32bppPBGRA = new("6fddc324-4e03-4bfe-b185-3d77768dc910");

    [DllImport("d2d1.dll", ExactSpelling = true)]
    public static extern int D2D1CreateFactory(int factoryType, in Guid riid, IntPtr factoryOptions,
        [MarshalAs(UnmanagedType.Interface)] out ID2D1Factory factory);

    [DllImport("dwrite.dll", ExactSpelling = true)]
    public static extern int DWriteCreateFactory(int factoryType, in Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IDWriteFactory factory);

    [StructLayout(LayoutKind.Sequential)]
    public struct ColorF(float r, float g, float b, float a)
    {
        public float R = r, G = g, B = b, A = a;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RectF(float left, float top, float right, float bottom)
    {
        public float Left = left, Top = top, Right = right, Bottom = bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RenderTargetProperties
    {
        public int Type;
        public int PixelFormat;
        public int AlphaMode;
        public float DpiX;
        public float DpiY;
        public int Usage;
        public int MinLevel;
    }
}

[ComImport, Guid("06152247-6f50-465a-9245-118bfd3b6007"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID2D1Factory
{
    void ReloadSystemMetrics();
    void GetDesktopDpi();
    void CreateRectangleGeometry();
    void CreateRoundedRectangleGeometry();
    void CreateEllipseGeometry();
    void CreateGeometryGroup();
    void CreateTransformedGeometry();
    void CreatePathGeometry();
    void CreateStrokeStyle();
    void CreateDrawingStateBlock();

    [PreserveSig]
    int CreateWicBitmapRenderTarget(IWICBitmap target, in Direct2DInterop.RenderTargetProperties properties,
        out ID2D1RenderTarget renderTarget);
}

[ComImport, Guid("2cd90694-12e2-11dc-9fed-001143a055f9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID2D1RenderTarget
{
    // ID2D1Resource
    void GetFactory();

    // ID2D1RenderTarget
    void CreateBitmap();
    void CreateBitmapFromWicBitmap();
    void CreateSharedBitmap();
    void CreateBitmapBrush();

    [PreserveSig]
    int CreateSolidColorBrush(in Direct2DInterop.ColorF color, IntPtr brushProperties, out ID2D1SolidColorBrush brush);

    void CreateGradientStopCollection();
    void CreateLinearGradientBrush();
    void CreateRadialGradientBrush();
    void CreateCompatibleRenderTarget();
    void CreateLayer();
    void CreateMesh();
    void DrawLine();
    void DrawRectangle();
    void FillRectangle();
    void DrawRoundedRectangle();
    void FillRoundedRectangle();
    void DrawEllipse();
    void FillEllipse();
    void DrawGeometry();
    void FillGeometry();
    void FillMesh();
    void FillOpacityMask();
    void DrawBitmap();

    [PreserveSig]
    void DrawText([MarshalAs(UnmanagedType.LPWStr)] string text, int length, IDWriteTextFormat textFormat,
        in Direct2DInterop.RectF layoutRect, ID2D1SolidColorBrush defaultFillBrush, int options, int measuringMode);

    void DrawTextLayout();
    void DrawGlyphRun();
    void SetTransform();
    void GetTransform();
    void SetAntialiasMode();
    void GetAntialiasMode();

    [PreserveSig]
    void SetTextAntialiasMode(int textAntialiasMode);

    void GetTextAntialiasMode();
    void SetTextRenderingParams();
    void GetTextRenderingParams();
    void SetTags();
    void GetTags();
    void PushLayer();
    void PopLayer();
    void Flush();
    void SaveDrawingState();
    void RestoreDrawingState();
    void PushAxisAlignedClip();
    void PopAxisAlignedClip();

    [PreserveSig]
    void Clear(in Direct2DInterop.ColorF clearColor);

    [PreserveSig]
    void BeginDraw();

    [PreserveSig]
    int EndDraw(IntPtr tag1, IntPtr tag2);
}

[ComImport, Guid("2cd906a9-12e2-11dc-9fed-001143a055f9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID2D1SolidColorBrush
{
}

[ComImport, Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDWriteFactory
{
    void GetSystemFontCollection();
    void CreateCustomFontCollection();
    void RegisterFontCollectionLoader();
    void UnregisterFontCollectionLoader();
    void CreateFontFileReference();
    void CreateCustomFontFileReference();
    void CreateFontFace();
    void CreateRenderingParams();
    void CreateMonitorRenderingParams();
    void CreateCustomRenderingParams();
    void RegisterFontFileLoader();
    void UnregisterFontFileLoader();

    [PreserveSig]
    int CreateTextFormat([MarshalAs(UnmanagedType.LPWStr)] string fontFamilyName, IntPtr fontCollection,
        int fontWeight, int fontStyle, int fontStretch, float fontSize, [MarshalAs(UnmanagedType.LPWStr)] string localeName,
        out IDWriteTextFormat textFormat);
}

[ComImport, Guid("9c906818-31d7-4fd3-a151-7c5e225db55a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDWriteTextFormat
{
    [PreserveSig]
    int SetTextAlignment(int textAlignment);

    [PreserveSig]
    int SetParagraphAlignment(int paragraphAlignment);
}

[ComImport, Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWICImagingFactory
{
    void CreateDecoderFromFilename();
    void CreateDecoderFromStream();
    void CreateDecoderFromFileHandle();
    void CreateComponentInfo();
    void CreateDecoder();
    void CreateEncoder();
    void CreatePalette();
    void CreateFormatConverter();
    void CreateBitmapScaler();
    void CreateBitmapClipper();
    void CreateBitmapFlipRotator();
    void CreateStream();
    void CreateColorContext();
    void CreateColorTransformer();

    [PreserveSig]
    int CreateBitmap(int width, int height, in Guid pixelFormat, int option, out IWICBitmap bitmap);
}

[ComImport, Guid("00000121-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWICBitmap
{
    // IWICBitmapSource
    void GetSize();
    void GetPixelFormat();
    void GetResolution();
    void CopyPalette();

    [PreserveSig]
    int CopyPixels(IntPtr rect, int stride, int bufferSize, [Out, MarshalAs(UnmanagedType.LPArray)] byte[] buffer);
}
