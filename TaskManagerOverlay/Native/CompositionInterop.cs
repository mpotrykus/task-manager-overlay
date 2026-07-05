using System.Runtime.InteropServices;

namespace TaskManagerOverlay.Native;

/// <summary>
/// Undocumented user32 API (available since Windows 10 1803) used to enable the native "Acrylic"
/// frosted-glass blur-behind effect on a window. There's no public replacement that reaches back
/// to Windows 10 - the officially supported DWM system backdrop (Mica/Acrylic) is Windows 11
/// 22H2+ only - so this stays the way to get a native frosted background across both.
/// </summary>
internal static class CompositionInterop
{
    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public uint AccentFlags;
        public uint GradientColor;
        public uint AnimationId;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <summary>
    /// Turns on Acrylic blur-behind for the given window. The tint blends into the blur itself -
    /// wherever the WPF content above it is transparent, the desktop shows through blurred and
    /// tinted by this color; wherever WPF content is opaque, the tint has no visible effect.
    /// </summary>
    public static void EnableAcrylicBlur(IntPtr hwnd, byte r, byte g, byte b, byte alpha)
    {
        uint abgrTint = (uint)(alpha << 24 | b << 16 | g << 8 | r);

        var accent = new AccentPolicy
        {
            AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            GradientColor = abgrTint,
        };

        int accentSize = Marshal.SizeOf<AccentPolicy>();
        IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentSize,
                Data = accentPtr,
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
