using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace TaskManagerOverlay.Views;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is true);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is true);
}

public sealed class SuspendResumeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Resume" : "Suspend";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NullableGpuPercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double d ? $"{d:0.0}%" : "N/A";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Hides the suspended-processes section entirely when nothing is suspended.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a CPU/GPU percentage (double or null) to a heat-scale brush, mirroring the
/// color coding Windows' own Task Manager uses to flag hot processes at a glance. Pass
/// ConverterParameter="Fill" to get a ~13% wash of the same color, for sparkline area fills.</summary>
public sealed class PercentToHeatBrushConverter : IValueConverter
{
    private static readonly Color UnknownColor = Color.FromRgb(0x66, 0x66, 0x66);
    private static readonly Color NormalColor = Color.FromRgb(0xCF, 0xCF, 0xCF);
    private static readonly Color ElevatedColor = Color.FromRgb(0xE0, 0xA5, 0x00);
    private static readonly Color HighColor = Color.FromRgb(0xE5, 0x48, 0x4D);

    private static readonly SolidColorBrush UnknownOpaque = Freeze(UnknownColor);
    private static readonly SolidColorBrush NormalOpaque = Freeze(NormalColor);
    private static readonly SolidColorBrush ElevatedOpaque = Freeze(ElevatedColor);
    private static readonly SolidColorBrush HighOpaque = Freeze(HighColor);

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        Color color = value is double percent
            ? percent switch { >= 60 => HighColor, >= 25 => ElevatedColor, _ => NormalColor }
            : UnknownColor;

        if (!string.Equals(parameter as string, "Fill", StringComparison.OrdinalIgnoreCase))
        {
            return color == HighColor ? HighOpaque
                : color == ElevatedColor ? ElevatedOpaque
                : color == NormalColor ? NormalOpaque
                : UnknownOpaque;
        }

        return Freeze(Color.FromArgb(0x22, color.R, color.G, color.B));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
