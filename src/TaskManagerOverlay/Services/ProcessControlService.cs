using System.ComponentModel;
using System.Diagnostics;
using TaskManagerOverlay.Native;

namespace TaskManagerOverlay.Services;

public sealed class ProcessControlService
{
    private readonly int _selfPid = Environment.ProcessId;

    public bool IsProtected(int pid, string processName)
    {
        if (pid == 0 || pid == 4 || pid == _selfPid)
            return true;

        return AppConstants.ProtectedProcessDenylist.Contains(processName);
    }

    public bool TrySuspend(int pid, string processName)
    {
        if (IsProtected(pid, processName))
            return false;

        IntPtr handle = Kernel32Interop.OpenProcess(Kernel32Interop.PROCESS_SUSPEND_RESUME, false, (uint)pid);
        if (handle == IntPtr.Zero)
            return false;

        try
        {
            return NtDllInterop.NtSuspendProcess(handle) == NtDllInterop.StatusSuccess;
        }
        finally
        {
            Kernel32Interop.CloseHandle(handle);
        }
    }

    public bool TryResume(int pid, string processName)
    {
        if (IsProtected(pid, processName))
            return false;

        IntPtr handle = Kernel32Interop.OpenProcess(Kernel32Interop.PROCESS_SUSPEND_RESUME, false, (uint)pid);
        if (handle == IntPtr.Zero)
            return false;

        try
        {
            return NtDllInterop.NtResumeProcess(handle) == NtDllInterop.StatusSuccess;
        }
        finally
        {
            Kernel32Interop.CloseHandle(handle);
        }
    }

    public bool TryKill(int pid, string processName)
    {
        if (IsProtected(pid, processName))
            return false;

        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
            return true;
        }
        catch (Win32Exception)
        {
            // Access denied - protected/PPL process even when elevated.
            return false;
        }
        catch (InvalidOperationException)
        {
            // Already exited between selection and action; the next refresh tick will drop the row.
            return true;
        }
        catch (ArgumentException)
        {
            // No process with that PID anymore.
            return true;
        }
    }
}
