using System.Runtime.InteropServices;

namespace EmojiSelector.Input;

// The slice of UI Automation and MSAA that CaretLocator uses. A COM interface lists its methods in vtable order: the
// slots before a used method are kept as placeholders (never called) so the used ones land on their right slot.
// Parameter types follow UIAutomationClient.h and oleacc.h.

internal static class AccessibilityInterop
{
    public const int UiaTextPattern2Id = 10024;
    public const int UiaBoundingRectanglePropertyId = 30001;
    public const int UiaProcessIdPropertyId = 30002;
    public const int TextUnitCharacter = 0;
    public const uint ObjIdCaret = 0xFFFFFFF8;
    public const int ChildIdSelf = 0;

    public static readonly Guid UiaClsid = new("ff48dba4-60ef-4201-aa87-54103eef594e");
    public static readonly Guid TextPattern2Iid = new("506a921a-fcc9-409f-b23b-37eb74106872");
    public static readonly Guid AccessibleIid = new("618736e0-3c3d-11cf-810c-00aa00389b71");

    [DllImport("oleacc.dll", ExactSpelling = true)]
    public static extern int AccessibleObjectFromWindow(IntPtr window, uint objectId, in Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IAccessible? accessible);
}

[ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomation
{
    void CompareElements();
    void CompareRuntimeIds();
    void GetRootElement();
    void ElementFromHandle();
    void ElementFromPoint();

    IUIAutomationElement? GetFocusedElement();
}

[ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationElement
{
    void SetFocus();
    void GetRuntimeId();
    void FindFirst();
    void FindAll();
    void FindFirstBuildCache();
    void FindAllBuildCache();
    void BuildUpdatedCache();

    [return: MarshalAs(UnmanagedType.Struct)]
    object? GetCurrentPropertyValue(int propertyId);

    void GetCurrentPropertyValueEx();
    void GetCachedPropertyValue();
    void GetCachedPropertyValueEx();

    [return: MarshalAs(UnmanagedType.IUnknown)]
    object? GetCurrentPatternAs(int patternId, in Guid riid);
}

[ComImport, Guid("506a921a-fcc9-409f-b23b-37eb74106872"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationTextPattern2
{
    // IUIAutomationTextPattern's slots.
    void RangeFromPoint();
    void RangeFromChild();
    void GetSelection();
    void GetVisibleRanges();
    void GetDocumentRange();
    void GetSupportedTextSelection();

    void RangeFromAnnotation();

    IUIAutomationTextRange? GetCaretRange(out int isActive);
}

[ComImport, Guid("a543cc6a-f4ae-494b-8239-c814481187a8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationTextRange
{
    IUIAutomationTextRange Clone();

    void Compare();
    void CompareEndpoints();

    void ExpandToEnclosingUnit(int unit);

    void FindAttribute();
    void FindText();
    void GetAttributeValue();

    [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_R8)]
    double[]? GetBoundingRectangles();
}

[ComImport, Guid("618736e0-3c3d-11cf-810c-00aa00389b71"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IAccessible
{
    void GetAccParent();
    void GetAccChildCount();
    void GetAccChild();
    void GetAccName();
    void GetAccValue();
    void GetAccDescription();
    void GetAccRole();
    void GetAccState();
    void GetAccHelp();
    void GetAccHelpTopic();
    void GetAccKeyboardShortcut();
    void GetAccFocus();
    void GetAccSelection();
    void GetAccDefaultAction();
    void AccSelect();

    [PreserveSig]
    int AccLocation(out int left, out int top, out int width, out int height,
        [MarshalAs(UnmanagedType.Struct)] object child);
}
