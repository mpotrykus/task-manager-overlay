using TaskManagerOverlay.Native;

namespace TaskManagerOverlay;

public static class AppConstants
{
    // Global keyboard hotkey: Ctrl+Alt+O
    public const uint HotkeyModifiers = HotkeyInterop.MOD_CONTROL | HotkeyInterop.MOD_ALT;
    public const uint HotkeyVirtualKey = 0x4F; // 'O'

    public const int GamepadPollIntervalMs = 16;
    public const int NavigationInitialRepeatDelayMs = 400;
    public const int NavigationRepeatIntervalMs = 120;

    public const int RefreshIntervalVisibleMs = 1000;
    public const int RefreshIntervalHiddenMs = 3000;

    // GPU Engine instance names can appear/disappear as a process starts/stops using the GPU,
    // independent of the process (PID) set changing - so re-resolve periodically as a safety
    // net in addition to whenever the PID set itself changes.
    public const int GpuInstanceRebuildIntervalMs = 5000;

    // Impact score weights: CPU/GPU steal frame time directly, RAM alone is a weaker live-impact signal.
    public const double WeightCpu = 0.5;
    public const double WeightGpu = 0.4;
    public const double WeightRam = 0.1;

    public static readonly HashSet<string> MonitorExclusionDenylist = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Registry", "Memory Compression", "Secure System",
        "smss", "csrss", "wininit", "services", "lsass", "winlogon"
    };

    public static readonly HashSet<string> ProtectedProcessDenylist = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon",
        "services", "lsass", "svchost", "explorer", "dwm", "fontdrvhost",
        "Memory Compression", "Secure System"
    };

    public const string SingleInstanceMutexName = "Global\\TaskManagerOverlay_SingleInstance_9F3E2C7B";
    public const string AutostartRegistryValueName = "TaskManagerOverlay";
}
