using Microsoft.Win32;

namespace TaskManagerOverlay.Services;

/// <summary>
/// Toggles a HKCU Run-key autostart entry. Note: since this app always runs elevated,
/// a Run-key launch still triggers a UAC prompt at every login - accepted tradeoff for v1.
/// </summary>
public static class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppConstants.AutostartRegistryValueName) is not null;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                         ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                key.SetValue(AppConstants.AutostartRegistryValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppConstants.AutostartRegistryValueName, throwOnMissingValue: false);
        }
    }
}
