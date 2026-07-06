using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TaskManagerOverlay.Views;

/// <summary>A labeled metric tile with a value and a small history sparkline - used for the
/// CPU/RAM/GPU tiles, which were otherwise identical Border/StackPanel/Viewbox blocks
/// differing only in the bound values and colors.</summary>
public partial class StatCard : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatCard));

    public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(StatCard));

    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(Brush), typeof(StatCard));

    public static readonly DependencyProperty StrokeBrushProperty =
        DependencyProperty.Register(nameof(StrokeBrush), typeof(Brush), typeof(StatCard));

    public static readonly DependencyProperty FillBrushProperty =
        DependencyProperty.Register(nameof(FillBrush), typeof(Brush), typeof(StatCard));

    public static readonly DependencyProperty HistoryPointsProperty =
        DependencyProperty.Register(nameof(HistoryPoints), typeof(string), typeof(StatCard));

    public static readonly DependencyProperty HistoryFillPointsProperty =
        DependencyProperty.Register(nameof(HistoryFillPoints), typeof(string), typeof(StatCard));

    public static readonly DependencyProperty CardPaddingProperty =
        DependencyProperty.Register(nameof(CardPadding), typeof(Thickness), typeof(StatCard),
            new PropertyMetadata(new Thickness(18, 15, 18, 15)));

    public static readonly DependencyProperty ValueFontSizeProperty =
        DependencyProperty.Register(nameof(ValueFontSize), typeof(double), typeof(StatCard),
            new PropertyMetadata(20.0));

    public static readonly DependencyProperty ChartHeightProperty =
        DependencyProperty.Register(nameof(ChartHeight), typeof(double), typeof(StatCard),
            new PropertyMetadata(26.0));

    public StatCard()
    {
        InitializeComponent();
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? ValueText
    {
        get => (string?)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public Brush? ValueBrush
    {
        get => (Brush?)GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    public Brush? StrokeBrush
    {
        get => (Brush?)GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public string? HistoryPoints
    {
        get => (string?)GetValue(HistoryPointsProperty);
        set => SetValue(HistoryPointsProperty, value);
    }

    public string? HistoryFillPoints
    {
        get => (string?)GetValue(HistoryFillPointsProperty);
        set => SetValue(HistoryFillPointsProperty, value);
    }

    public Thickness CardPadding
    {
        get => (Thickness)GetValue(CardPaddingProperty);
        set => SetValue(CardPaddingProperty, value);
    }

    public double ValueFontSize
    {
        get => (double)GetValue(ValueFontSizeProperty);
        set => SetValue(ValueFontSizeProperty, value);
    }

    public double ChartHeight
    {
        get => (double)GetValue(ChartHeightProperty);
        set => SetValue(ChartHeightProperty, value);
    }
}
