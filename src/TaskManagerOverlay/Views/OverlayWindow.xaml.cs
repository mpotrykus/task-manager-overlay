using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TaskManagerOverlay.Services;
using TaskManagerOverlay.ViewModels;

namespace TaskManagerOverlay.Views;

public partial class OverlayWindow : Window
{
    private bool _forceClose;

    public OverlayViewModel ViewModel { get; }

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Moves keyboard focus into the search box and selects any existing text, so typing immediately replaces the filter.</summary>
    public void FocusSearchBox()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
    }

    private void OnFocusSearchHintClick(object sender, RoutedEventArgs e) => FocusSearchBox();

    private void OnSortPrevHintClick(object sender, RoutedEventArgs e) => ViewModel.CycleSortMode(-1);

    private void OnSortNextHintClick(object sender, RoutedEventArgs e) => ViewModel.CycleSortMode(1);

    private void OnScrimMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only close when the click landed directly on the scrim itself, not on the panel or
        // any of its descendants (which would bubble the event up to this same handler).
        if (e.OriginalSource == sender)
            Close();
    }

    /// <summary>Fully exits the app regardless of the "Close to Tray" setting - used by the tray icon's explicit Exit action.</summary>
    public void RequestQuit()
    {
        _forceClose = true;
        Close();
    }

    /// <summary>
    /// The single source of truth for what "close the overlay" means: every dismiss gesture
    /// (Escape, the X button, clicking the scrim, the controller's B button, Alt+F4) calls
    /// Close(), which always routes through here. RequestQuit() is the only way to bypass the
    /// "Close to Tray" setting and force a full exit.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceClose && AppSettingsService.CloseToTray)
        {
            e.Cancel = true;
            HideOverlay();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // The window only actually closes when "Close to Tray" is off (or RequestQuit forced it) -
        // either way, there's no tray-resident mode to fall back to, so exit the whole app.
        System.Windows.Application.Current.Shutdown();
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

    public void HideOverlay()
    {
        Hide();
    }

    public void ToggleOverlay()
    {
        if (IsVisible)
            HideOverlay();
        else
            ShowOverlay();
    }
}
