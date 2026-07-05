using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using TaskManagerOverlay.Native;
using TaskManagerOverlay.ViewModels;

namespace TaskManagerOverlay.Views;

public partial class OverlayWindow : Window
{
    public OverlayViewModel ViewModel { get; }

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        PreviewKeyDown += OnPreviewKeyDown;
        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>Enables native Acrylic blur-behind once the HWND exists (SetWindowCompositionAttribute
    /// needs a real window handle, which isn't available until the window source is initialized).</summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        CompositionInterop.EnableAcrylicBlur(hwnd, r: 0x15, g: 0x15, b: 0x15, alpha: 0x60);
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ViewModel.IsActionModalOpen)
                ViewModel.CancelActionModalCommand.Execute(null);
            else
                Close();
            e.Handled = true;
        }
    }

    /// <summary>Opens the action modal for whatever row was double-clicked - ignores double-clicks
    /// that land on empty space below the last row rather than on an actual process row.</summary>
    private void OnProcessRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindAncestor<ListBoxItem>(source) is null)
            return;

        if (ViewModel.OpenActionModalCommand.CanExecute(null))
            ViewModel.OpenActionModalCommand.Execute(null);
    }

    /// <summary>Keeps the selected row visible whenever selection changes, whether from a mouse
    /// click, keyboard navigation, or the gamepad/sort-mode logic setting SelectedRow programmatically -
    /// none of which WPF scrolls into view on its own.</summary>
    private void OnProcessListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox { SelectedItem: not null } listBox)
            listBox.ScrollIntoView(listBox.SelectedItem);
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>Clicking directly on the action modal's own backdrop (not the modal card) cancels it,
    /// mirroring the outer scrim's click-off-to-close behavior - see OnScrimMouseLeftButtonDown.</summary>
    private void OnActionModalScrimMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender)
        {
            ViewModel.CancelActionModalCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Moves keyboard focus to the Cancel button whenever the action modal opens, so it's
    /// the safe default selection for both keyboard (Enter) and gamepad (A) confirmation.</summary>
    private void OnActionModalVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
            Dispatcher.BeginInvoke(() => CancelModalButton.Focus());
    }

    /// <summary>Moves keyboard focus into the search box and selects any existing text, so typing immediately replaces the filter.</summary>
    public void FocusSearchBox()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
    }

    private void OnScrimMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only close when the click landed directly on the scrim itself, not on the panel or
        // any of its descendants (which would bubble the event up to this same handler).
        if (e.OriginalSource == sender)
            Close();
    }

    private void SizeToPrimaryScreen()
    {
        // The window now covers the whole primary screen so the scrim can darken everything
        // behind the panel and catch clicks outside it; the panel itself stays centered via layout.
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

    public void ShowOverlay()
    {
        SizeToPrimaryScreen();
        Show();
        Activate();
        Focus();
    }
}
