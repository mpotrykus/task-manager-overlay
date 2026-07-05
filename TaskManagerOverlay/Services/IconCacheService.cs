using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaskManagerOverlay.Services;

/// <summary>
/// Extracts and caches process icons keyed by executable path (not PID) - many rows share
/// the same exe, and re-extracting per PID per refresh tick would be wasteful.
/// </summary>
public sealed class IconCacheService
{
    private readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ImageSource? _fallbackIcon = TryLoadFallback();

    public ImageSource? GetIcon(string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
            return _fallbackIcon;

        if (_cache.TryGetValue(executablePath, out var cached))
            return cached ?? _fallbackIcon;

        ImageSource? extracted = TryExtract(executablePath);
        _cache[executablePath] = extracted;
        return extracted ?? _fallbackIcon;
    }

    private static ImageSource? TryExtract(string executablePath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null)
                return null;

            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? TryLoadFallback()
    {
        try
        {
            using var icon = SystemIcons.Application;
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            return null;
        }
    }
}
