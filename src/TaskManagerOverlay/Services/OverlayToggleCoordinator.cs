using System.Windows.Threading;
using TaskManagerOverlay.Models;
using TaskManagerOverlay.Native;
using TaskManagerOverlay.Views;

namespace TaskManagerOverlay.Services;

/// <summary>
/// Routes gamepad and hotkey input to the overlay window. Back+Start (via GamepadInputService)
/// and the global keyboard hotkey (via GlobalHotkeyService) both toggle visibility; while the
/// overlay is visible, D-pad/stick navigation, A (Suspend/Resume), X (Kill), B (Close),
/// Y (focus search box), and LB/RB (cycle sort mode) route to the view model/window.
/// </summary>
public sealed class OverlayToggleCoordinator
{
    private readonly OverlayWindow _window;
    private readonly Dispatcher _dispatcher;
    private readonly ProcessMonitorService _processMonitor;

    public OverlayToggleCoordinator(
        OverlayWindow window,
        GamepadInputService gamepadInput,
        GlobalHotkeyService hotkey,
        ProcessMonitorService processMonitor)
    {
        _window = window;
        _dispatcher = window.Dispatcher;
        _processMonitor = processMonitor;

        gamepadInput.InputEvent += OnGamepadInputEvent;
        hotkey.HotkeyPressed += OnHotkeyPressed;
    }

    private void OnHotkeyPressed()
    {
        _dispatcher.BeginInvoke(ToggleOverlay);
    }

    private void OnGamepadInputEvent(GamepadInputEvent evt)
    {
        switch (evt)
        {
            case GamepadToggleComboPressed:
                _dispatcher.BeginInvoke(ToggleOverlay);
                break;

            case GamepadButtonsPressed pressed:
                _dispatcher.BeginInvoke(() => HandleButtonsWhileVisible(pressed.Buttons));
                break;
        }
    }

    private void ToggleOverlay()
    {
        _window.ToggleOverlay();
        _processMonitor.SetVisible(_window.IsVisible);
    }

    private void HandleButtonsWhileVisible(XInputButtons buttons)
    {
        if (!_window.IsVisible)
            return;

        if (buttons.HasFlag(XInputButtons.DPadUp))
            _window.ViewModel.MoveSelection(-1);
        if (buttons.HasFlag(XInputButtons.DPadDown))
            _window.ViewModel.MoveSelection(1);
        if (buttons.HasFlag(XInputButtons.A) && _window.ViewModel.SuspendResumeCommand.CanExecute(null))
            _window.ViewModel.SuspendResumeCommand.Execute(null);
        if (buttons.HasFlag(XInputButtons.X) && _window.ViewModel.KillCommand.CanExecute(null))
            _window.ViewModel.KillCommand.Execute(null);
        if (buttons.HasFlag(XInputButtons.B))
            CloseOverlay();
        if (buttons.HasFlag(XInputButtons.Y))
            _window.FocusSearchBox();
        if (buttons.HasFlag(XInputButtons.LeftShoulder))
            _window.ViewModel.CycleSortMode(-1);
        if (buttons.HasFlag(XInputButtons.RightShoulder))
            _window.ViewModel.CycleSortMode(1);
    }

    private void CloseOverlay()
    {
        // Close() is the overlay's single source of truth for what "close" means (hide-to-tray
        // vs. fully exit, per the "Close to Tray" setting) - route through it rather than hiding directly.
        _window.Close();
        _processMonitor.SetVisible(_window.IsVisible);
    }
}
