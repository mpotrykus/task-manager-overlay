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
    // The fullscreen dimming backdrop and click-off-to-close catcher live in their own window so
    // this one can be sized and positioned to fit just the panel - see ScrimWindow.
    private readonly ScrimWindow _scrim = new();

    public OverlayViewModel ViewModel { get; }

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        PreviewKeyDown += OnPreviewKeyDown;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        _scrim.ScrimClicked += (_, _) => Close();
    }

    /// <summary>Closing the panel should always take the scrim down with it, whether the panel
    /// closed itself (Escape) or was closed in response to the scrim being clicked.</summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        _scrim.Close();
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
    /// that land on empty space below the last row rather than on an actual process row. Reads the
    /// row straight off the clicked ListBoxItem's DataContext rather than trusting SelectedRow, since
    /// the active and suspended lists share that property and a double-click on one can otherwise pick
    /// up a stale selection from the other.</summary>
    private void OnProcessRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindAncestor<ListBoxItem>(source) is not { } item)
            return;

        if (item.DataContext is ProcessRowViewModel row)
            ViewModel.SelectedRow = row;

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

    /// <summary>Moves keyboard focus into the search box and selects any existing text, so typing immediately replaces the filter.</summary>
    public void FocusSearchBox()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        FocusSearchBox();
    }

    private void SizeAndCenterOnPrimaryScreen()
    {
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        // The panel scales uniformly with the screen size but never exceeds 80% of the screen's
        // width or height, whichever limit the panel's fixed 1020x810 aspect ratio hits first.
        const double panelAspect = 1020.0 / 810.0;
        double width = screenWidth * 0.8;
        double height = width / panelAspect;
        if (height > screenHeight * 0.8)
        {
            height = screenHeight * 0.8;
            width = height * panelAspect;
        }

        Width = width;
        Height = height;
        Left = (screenWidth - width) / 2;
        Top = (screenHeight - height) / 2;
    }

    public void ShowOverlay()
    {
        _scrim.SizeToPrimaryScreen();
        _scrim.Show();

        // Owning the panel to the scrim (rather than just relying on show/activate order) makes
        // Windows enforce that the panel always stays above the scrim in the Z order, regardless
        // of any timing quirks from the Acrylic blur composition call in OnSourceInitialized.
        Owner = _scrim;

        SizeAndCenterOnPrimaryScreen();
        Show();
        Activate();
        Focus();
    }
}
