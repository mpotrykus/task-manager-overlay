using System.Windows;
using System.Windows.Input;

namespace TaskManagerOverlay.Views;

/// <summary>Fullscreen dimming backdrop shown behind the <see cref="OverlayWindow"/> panel.
/// Kept as its own top-level window (rather than baked into the panel window) so the panel
/// can be sized and positioned to fit just its own content instead of the whole screen.</summary>
public partial class ScrimWindow : Window
{
    public event EventHandler? ScrimClicked;

    public ScrimWindow()
    {
        InitializeComponent();
    }

    public void SizeToPrimaryScreen()
    {
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ScrimClicked?.Invoke(this, EventArgs.Empty);
    }
}
