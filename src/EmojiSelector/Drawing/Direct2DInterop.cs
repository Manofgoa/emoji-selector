using System.Runtime.InteropServices;

namespace EmojiSelector.Drawing;

// The slice of Direct2D, DirectWrite, WIC and Shell that EmojiRenderer uses. A COM interface lists its methods in
// vtable order: the slots before a used method are kept as placeholders (never called) so the used ones land on
// their right slot. Parameter types follow d2d1.h, dwrite.h and wincodec.h.

internal static class Direct2DInterop
{
    public const int D2D1FactoryTypeSingleThreaded = 0;
    public const int DWriteFactoryTypeIsolated = 1;
    public const int DxgiFormatB8G8R8A8Unorm = 87;
    public const int D2D1AlphaModePremultiplied = 1;
    public const int D2D1TextAntialiasModeGrayscale = 2;
    public const int D2D1DrawTextOptionsNone = 0;
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

    // An IStream over a copy of the bytes, for ID2D1DeviceContext5.CreateSvgDocument; released with Marshal.Release.
    [DllImport("shlwapi.dll", ExactSpelling = true)]
    public static extern IntPtr SHCreateMemStream(byte[] init, int size);

    [StructLayout(LayoutKind.Sequential)]
    public struct D2DSizeF(float width, float height)
    {
        public float Width = width, Height = height;
    }

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

// Queried from the WIC bitmap render target (every render target is a device context since Windows 8; this version since
// Windows 10 1703): Direct2D's SVG renderer, for the flag images. Every inherited method is a placeholder, named after its
// interface and numbered by its vtable slot (IUnknown's three first), so CreateSvgDocument lands on slot 115.
[ComImport, Guid("7836d248-68cc-4df6-b9e8-de991bf62eb7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID2D1DeviceContext5
{
    // ID2D1Resource
    void ID2D1Resource_GetFactory(); // slot 3

    // ID2D1RenderTarget
    void ID2D1RenderTarget_CreateBitmap(); // slot 4
    void ID2D1RenderTarget_CreateBitmapFromWicBitmap(); // slot 5
    void ID2D1RenderTarget_CreateSharedBitmap(); // slot 6
    void ID2D1RenderTarget_CreateBitmapBrush(); // slot 7
    void ID2D1RenderTarget_CreateSolidColorBrush(); // slot 8
    void ID2D1RenderTarget_CreateGradientStopCollection(); // slot 9
    void ID2D1RenderTarget_CreateLinearGradientBrush(); // slot 10
    void ID2D1RenderTarget_CreateRadialGradientBrush(); // slot 11
    void ID2D1RenderTarget_CreateCompatibleRenderTarget(); // slot 12
    void ID2D1RenderTarget_CreateLayer(); // slot 13
    void ID2D1RenderTarget_CreateMesh(); // slot 14
    void ID2D1RenderTarget_DrawLine(); // slot 15
    void ID2D1RenderTarget_DrawRectangle(); // slot 16
    void ID2D1RenderTarget_FillRectangle(); // slot 17
    void ID2D1RenderTarget_DrawRoundedRectangle(); // slot 18
    void ID2D1RenderTarget_FillRoundedRectangle(); // slot 19
    void ID2D1RenderTarget_DrawEllipse(); // slot 20
    void ID2D1RenderTarget_FillEllipse(); // slot 21
    void ID2D1RenderTarget_DrawGeometry(); // slot 22
    void ID2D1RenderTarget_FillGeometry(); // slot 23
    void ID2D1RenderTarget_FillMesh(); // slot 24
    void ID2D1RenderTarget_FillOpacityMask(); // slot 25
    void ID2D1RenderTarget_DrawBitmap(); // slot 26
    void ID2D1RenderTarget_DrawText(); // slot 27
    void ID2D1RenderTarget_DrawTextLayout(); // slot 28
    void ID2D1RenderTarget_DrawGlyphRun(); // slot 29
    void ID2D1RenderTarget_SetTransform(); // slot 30
    void ID2D1RenderTarget_GetTransform(); // slot 31
    void ID2D1RenderTarget_SetAntialiasMode(); // slot 32
    void ID2D1RenderTarget_GetAntialiasMode(); // slot 33
    void ID2D1RenderTarget_SetTextAntialiasMode(); // slot 34
    void ID2D1RenderTarget_GetTextAntialiasMode(); // slot 35
    void ID2D1RenderTarget_SetTextRenderingParams(); // slot 36
    void ID2D1RenderTarget_GetTextRenderingParams(); // slot 37
    void ID2D1RenderTarget_SetTags(); // slot 38
    void ID2D1RenderTarget_GetTags(); // slot 39
    void ID2D1RenderTarget_PushLayer(); // slot 40
    void ID2D1RenderTarget_PopLayer(); // slot 41
    void ID2D1RenderTarget_Flush(); // slot 42
    void ID2D1RenderTarget_SaveDrawingState(); // slot 43
    void ID2D1RenderTarget_RestoreDrawingState(); // slot 44
    void ID2D1RenderTarget_PushAxisAlignedClip(); // slot 45
    void ID2D1RenderTarget_PopAxisAlignedClip(); // slot 46
    void ID2D1RenderTarget_Clear(); // slot 47
    void ID2D1RenderTarget_BeginDraw(); // slot 48
    void ID2D1RenderTarget_EndDraw(); // slot 49
    void ID2D1RenderTarget_GetPixelFormat(); // slot 50
    void ID2D1RenderTarget_SetDpi(); // slot 51
    void ID2D1RenderTarget_GetDpi(); // slot 52
    void ID2D1RenderTarget_GetSize(); // slot 53
    void ID2D1RenderTarget_GetPixelSize(); // slot 54
    void ID2D1RenderTarget_GetMaximumBitmapSize(); // slot 55
    void ID2D1RenderTarget_IsSupported(); // slot 56

    // ID2D1DeviceContext
    void ID2D1DeviceContext_CreateBitmap(); // slot 57
    void ID2D1DeviceContext_CreateBitmapFromWicBitmap(); // slot 58
    void ID2D1DeviceContext_CreateColorContext(); // slot 59
    void ID2D1DeviceContext_CreateColorContextFromFilename(); // slot 60
    void ID2D1DeviceContext_CreateColorContextFromWicColorContext(); // slot 61
    void ID2D1DeviceContext_CreateBitmapFromDxgiSurface(); // slot 62
    void ID2D1DeviceContext_CreateEffect(); // slot 63
    void ID2D1DeviceContext_CreateGradientStopCollection(); // slot 64
    void ID2D1DeviceContext_CreateImageBrush(); // slot 65
    void ID2D1DeviceContext_CreateBitmapBrush(); // slot 66
    void ID2D1DeviceContext_CreateCommandList(); // slot 67
    void ID2D1DeviceContext_IsDxgiFormatSupported(); // slot 68
    void ID2D1DeviceContext_IsBufferPrecisionSupported(); // slot 69
    void ID2D1DeviceContext_GetImageLocalBounds(); // slot 70
    void ID2D1DeviceContext_GetImageWorldBounds(); // slot 71
    void ID2D1DeviceContext_GetGlyphRunWorldBounds(); // slot 72
    void ID2D1DeviceContext_GetDevice(); // slot 73
    void ID2D1DeviceContext_SetTarget(); // slot 74
    void ID2D1DeviceContext_GetTarget(); // slot 75
    void ID2D1DeviceContext_SetRenderingControls(); // slot 76
    void ID2D1DeviceContext_GetRenderingControls(); // slot 77
    void ID2D1DeviceContext_SetPrimitiveBlend(); // slot 78
    void ID2D1DeviceContext_GetPrimitiveBlend(); // slot 79
    void ID2D1DeviceContext_SetUnitMode(); // slot 80
    void ID2D1DeviceContext_GetUnitMode(); // slot 81
    void ID2D1DeviceContext_DrawGlyphRun(); // slot 82
    void ID2D1DeviceContext_DrawImage(); // slot 83
    void ID2D1DeviceContext_DrawGdiMetafile(); // slot 84
    void ID2D1DeviceContext_DrawBitmap(); // slot 85
    void ID2D1DeviceContext_PushLayer(); // slot 86
    void ID2D1DeviceContext_InvalidateEffectInputRectangle(); // slot 87
    void ID2D1DeviceContext_GetEffectInvalidRectangleCount(); // slot 88
    void ID2D1DeviceContext_GetEffectInvalidRectangles(); // slot 89
    void ID2D1DeviceContext_GetEffectRequiredInputRectangles(); // slot 90
    void ID2D1DeviceContext_FillOpacityMask(); // slot 91

    // ID2D1DeviceContext1
    void ID2D1DeviceContext1_CreateFilledGeometryRealization(); // slot 92
    void ID2D1DeviceContext1_CreateStrokedGeometryRealization(); // slot 93
    void ID2D1DeviceContext1_DrawGeometryRealization(); // slot 94

    // ID2D1DeviceContext2
    void ID2D1DeviceContext2_CreateInk(); // slot 95
    void ID2D1DeviceContext2_CreateInkStyle(); // slot 96
    void ID2D1DeviceContext2_CreateGradientMesh(); // slot 97
    void ID2D1DeviceContext2_CreateImageSourceFromWic(); // slot 98
    void ID2D1DeviceContext2_CreateLookupTable3D(); // slot 99
    void ID2D1DeviceContext2_CreateImageSourceFromDxgi(); // slot 100
    void ID2D1DeviceContext2_GetGradientMeshWorldBounds(); // slot 101
    void ID2D1DeviceContext2_DrawInk(); // slot 102
    void ID2D1DeviceContext2_DrawGradientMesh(); // slot 103
    void ID2D1DeviceContext2_DrawGdiMetafile(); // slot 104
    void ID2D1DeviceContext2_CreateTransformedImageSource(); // slot 105

    // ID2D1DeviceContext3
    void ID2D1DeviceContext3_CreateSpriteBatch(); // slot 106
    void ID2D1DeviceContext3_DrawSpriteBatch(); // slot 107

    // ID2D1DeviceContext4
    void ID2D1DeviceContext4_CreateSvgGlyphStyle(); // slot 108
    void ID2D1DeviceContext4_DrawText(); // slot 109
    void ID2D1DeviceContext4_DrawTextLayout(); // slot 110
    void ID2D1DeviceContext4_DrawColorBitmapGlyphRun(); // slot 111
    void ID2D1DeviceContext4_DrawSvgGlyphRun(); // slot 112
    void ID2D1DeviceContext4_GetColorBitmapGlyphImage(); // slot 113
    void ID2D1DeviceContext4_GetSvgGlyphImage(); // slot 114
    // ID2D1DeviceContext5
    [PreserveSig]
    int CreateSvgDocument(IntPtr inputXmlStream, Direct2DInterop.D2DSizeF viewportSize, out ID2D1SvgDocument svgDocument);

    [PreserveSig]
    void DrawSvgDocument(ID2D1SvgDocument svgDocument);
}

[ComImport, Guid("86b88e4d-afa4-4d7b-88e4-68a51c4a0aec"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID2D1SvgDocument
{
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
    int CopyPixels(IntPtr rect, int stride, int bufferSize, IntPtr buffer);
}
