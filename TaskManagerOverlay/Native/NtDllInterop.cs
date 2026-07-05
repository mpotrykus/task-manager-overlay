using System.Runtime.InteropServices;

namespace TaskManagerOverlay.Native;

internal static class NtDllInterop
{
    public const int StatusSuccess = 0;

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern int NtResumeProcess(IntPtr processHandle);
}
