using Microsoft.Win32;

namespace TaskManagerOverlay.Services;

/// <summary>Persists this app's own user preferences under HKCU (separate from the Windows autostart Run-key entry).</summary>
public static class AppSettingsService
{
    private const string SettingsKeyPath = @"Software\TaskManagerOverlay";
    private const string OpenOverlayOnStartValueName = "OpenOverlayOnStart";
    private const string CloseToTrayValueName = "CloseToTray";

    public static bool OpenOverlayOnStart
    {
        get => GetBool(OpenOverlayOnStartValueName, defaultValue: false);
        set => SetBool(OpenOverlayOnStartValueName, value);
    }

    /// <summary>When true (the default), closing the overlay window just hides it and the app stays resident in the tray.
    /// When false, closing the window fully exits the app - for using it like a normal, non-agent application.</summary>
    public static bool CloseToTray
    {
        get => GetBool(CloseToTrayValueName, defaultValue: true);
        set => SetBool(CloseToTrayValueName, value);
    }

    private static bool GetBool(string valueName, bool defaultValue)
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, writable: false);
        var raw = key?.GetValue(valueName);
        return raw is int value ? value != 0 : defaultValue;
    }

    private static void SetBool(string valueName, bool value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, writable: true)
                         ?? Registry.CurrentUser.CreateSubKey(SettingsKeyPath);
        key.SetValue(valueName, value ? 1 : 0, RegistryValueKind.DWord);
    }
}
