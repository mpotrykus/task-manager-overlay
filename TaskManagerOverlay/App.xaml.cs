using System.Windows;
using TaskManagerOverlay.Services;
using TaskManagerOverlay.ViewModels;
using TaskManagerOverlay.Views;

namespace TaskManagerOverlay;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    private GamepadInputService? _gamepadInput;
    private ProcessMonitorService? _processMonitor;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, AppConstants.SingleInstanceMutexName, out bool createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("Task Manager Overlay is already running.", "Task Manager Overlay");
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var processControl = new ProcessControlService();
        _processMonitor = new ProcessMonitorService();

        var overlayViewModel = new OverlayViewModel(processControl)
        {
            IsGpuMonitoringAvailable = _processMonitor.IsGpuMonitoringAvailable
        };
        var overlayWindow = new OverlayWindow(overlayViewModel);

        _processMonitor.Refreshed += (samples, stats) =>
            overlayWindow.Dispatcher.BeginInvoke(() => overlayViewModel.ApplySnapshot(samples, stats));

        _gamepadInput = new GamepadInputService();

        _ = new GamepadNavigationCoordinator(overlayWindow, _gamepadInput);

        overlayWindow.ShowOverlay();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _gamepadInput?.Dispose();
        _processMonitor?.Dispose();

        if (_ownsMutex)
            _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
