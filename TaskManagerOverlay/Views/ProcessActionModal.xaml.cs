using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TaskManagerOverlay.Views;

public partial class ProcessActionModal : UserControl
{
    public ProcessActionModal()
    {
        InitializeComponent();
    }

    /// <summary>Clicking directly on the modal's own backdrop (not the modal card) cancels it,
    /// mirroring the outer scrim's click-off-to-close behavior - see ScrimWindow.OnMouseLeftButtonDown.</summary>
    private void OnScrimMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender && DataContext is ViewModels.OverlayViewModel viewModel)
        {
            viewModel.CancelActionModalCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Moves keyboard focus to the Cancel button whenever the modal opens, so it's
    /// the safe default selection for both keyboard (Enter) and gamepad (A) confirmation.</summary>
    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
            Dispatcher.BeginInvoke(() => CancelModalButton.Focus());
    }
}
