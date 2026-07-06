using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using TaskManagerOverlay.Models;
using TaskManagerOverlay.Native;
using TaskManagerOverlay.Views;

namespace TaskManagerOverlay.Services;

/// <summary>
/// Routes gamepad input to the overlay window while it has focus: D-pad up/down (or stick) moves
/// the selection by one row, D-pad left/right jumps to the top/bottom of the list, A (open
/// the action modal for the selected process), B (Close), Y (focus search box), and LB/RB (cycle
/// sort mode). While the action modal is open, input is redirected instead: D-pad left/right moves
/// focus between the modal's buttons, A activates whichever button currently has focus (Cancel by
/// default), and B cancels the modal. The underlying <see cref="GamepadInputService"/> poll thread
/// is started/stopped alongside the window's Activated/Deactivated events.
/// </summary>
public sealed class GamepadNavigationCoordinator
{
    private readonly OverlayWindow _window;
    private readonly GamepadInputService _gamepadInput;
    private readonly Dispatcher _dispatcher;

    public GamepadNavigationCoordinator(OverlayWindow window, GamepadInputService gamepadInput)
    {
        _window = window;
        _gamepadInput = gamepadInput;
        _dispatcher = window.Dispatcher;

        gamepadInput.InputEvent += OnGamepadInputEvent;

        window.Activated += (_, _) => _gamepadInput.Start();
        window.Deactivated += (_, _) => _gamepadInput.Stop();

        if (window.IsActive)
            _gamepadInput.Start();
    }

    private void OnGamepadInputEvent(GamepadInputEvent evt)
    {
        if (evt is GamepadButtonsPressed pressed)
            _dispatcher.BeginInvoke(() => HandleButtonsWhileVisible(pressed.Buttons));
    }

    private void HandleButtonsWhileVisible(XInputButtons buttons)
    {
        if (_window.ViewModel.IsActionModalOpen)
        {
            HandleButtonsWhileActionModalOpen(buttons);
            return;
        }

        if (buttons.HasFlag(XInputButtons.DPadUp))
            _window.ViewModel.MoveSelection(-1);
        if (buttons.HasFlag(XInputButtons.DPadDown))
            _window.ViewModel.MoveSelection(1);
        if (buttons.HasFlag(XInputButtons.DPadLeft))
            _window.ViewModel.MoveSelectionToEdge(toStart: true);
        if (buttons.HasFlag(XInputButtons.DPadRight))
            _window.ViewModel.MoveSelectionToEdge(toStart: false);
        if (buttons.HasFlag(XInputButtons.A) && _window.ViewModel.OpenActionModalCommand.CanExecute(null))
            _window.ViewModel.OpenActionModalCommand.Execute(null);
        if (buttons.HasFlag(XInputButtons.B))
            _window.Close();
        if (buttons.HasFlag(XInputButtons.Y))
            _window.FocusSearchBox();
        if (buttons.HasFlag(XInputButtons.LeftShoulder))
            _window.ViewModel.CycleSortMode(-1);
        if (buttons.HasFlag(XInputButtons.RightShoulder))
            _window.ViewModel.CycleSortMode(1);
    }

    private void HandleButtonsWhileActionModalOpen(XInputButtons buttons)
    {
        if (buttons.HasFlag(XInputButtons.B))
        {
            _window.ViewModel.CancelActionModalCommand.Execute(null);
            return;
        }

        // Keyboard focus can end up outside the modal (e.g. the post-open focus call raced against
        // other dispatcher work), which would otherwise make every D-pad press below silently no-op.
        _window.ActionModal.EnsureFocusWithinModal();

        if (buttons.HasFlag(XInputButtons.DPadLeft) && Keyboard.FocusedElement is UIElement focusedForLeft)
            focusedForLeft.MoveFocus(new TraversalRequest(FocusNavigationDirection.Left));
        if (buttons.HasFlag(XInputButtons.DPadRight) && Keyboard.FocusedElement is UIElement focusedForRight)
            focusedForRight.MoveFocus(new TraversalRequest(FocusNavigationDirection.Right));
        if (buttons.HasFlag(XInputButtons.DPadUp) && Keyboard.FocusedElement is UIElement focusedForUp)
            focusedForUp.MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
        if (buttons.HasFlag(XInputButtons.DPadDown) && Keyboard.FocusedElement is UIElement focusedForDown)
            focusedForDown.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
        if (buttons.HasFlag(XInputButtons.A) && Keyboard.FocusedElement is Button { IsEnabled: true } focusedButton)
            focusedButton.Command?.Execute(focusedButton.CommandParameter);
    }
}
