using System.Runtime.InteropServices;

namespace TaskManagerOverlay.Native;

internal static class XInputInterop
{
    private const string DllName = "xinput1_4.dll";

    public const int ErrorSuccess = 0;
    public const int ErrorDeviceNotConnected = 1167;

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [DllImport(DllName, EntryPoint = "XInputGetState")]
    public static extern int XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

    public const short LeftThumbDeadzone = 7849;
}

[Flags]
public enum XInputButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000
}
