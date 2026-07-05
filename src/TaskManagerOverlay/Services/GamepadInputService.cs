using TaskManagerOverlay.Models;
using TaskManagerOverlay.Native;

namespace TaskManagerOverlay.Services;

/// <summary>
/// Polls controller index 0 via XInput on a dedicated background thread. Runs continuously
/// from app startup regardless of window visibility/focus, since XInput state reads are
/// independent of window focus or process integrity level - this is what lets the
/// Back+Start show/hide combo work even while the overlay window is hidden.
/// </summary>
public sealed class GamepadInputService : IDisposable
{
    private const XInputButtons RepeatableDirections =
        XInputButtons.DPadUp | XInputButtons.DPadDown | XInputButtons.DPadLeft | XInputButtons.DPadRight;

    private const XInputButtons ComboMask = XInputButtons.Back | XInputButtons.Start;

    private readonly Thread _pollThread;
    private volatile bool _running = true;

    public event Action<GamepadInputEvent>? InputEvent;

    public GamepadInputService()
    {
        _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "XInputPoll" };
        _pollThread.Start();
    }

    private void PollLoop()
    {
        ushort prevButtons = 0;
        bool wasConnected = false;
        var pressStart = new Dictionary<XInputButtons, DateTime>();
        var lastFired = new Dictionary<XInputButtons, DateTime>();

        while (_running)
        {
            int result = XInputInterop.XInputGetState(0, out var state);
            bool connected = result == XInputInterop.ErrorSuccess;

            if (connected != wasConnected)
            {
                InputEvent?.Invoke(new GamepadConnectionChanged(connected));
                wasConnected = connected;
                prevButtons = 0;
                pressStart.Clear();
                lastFired.Clear();
            }

            if (connected)
            {
                ushort buttons = (ushort)((XInputButtons)state.Gamepad.wButtons | StickToDpadBits(state.Gamepad.sThumbLX, state.Gamepad.sThumbLY));

                var pressedEdge = (XInputButtons)(buttons & ~prevButtons);

                bool comboNow = ((XInputButtons)buttons & ComboMask) == ComboMask;
                bool comboPrev = ((XInputButtons)prevButtons & ComboMask) == ComboMask;
                if (comboNow && !comboPrev)
                    InputEvent?.Invoke(new GamepadToggleComboPressed());

                var nonRepeatPressed = pressedEdge & ~RepeatableDirections;
                if (nonRepeatPressed != 0)
                    InputEvent?.Invoke(new GamepadButtonsPressed(nonRepeatPressed));

                DateTime now = DateTime.UtcNow;
                foreach (var direction in new[]
                         {
                             XInputButtons.DPadUp, XInputButtons.DPadDown,
                             XInputButtons.DPadLeft, XInputButtons.DPadRight
                         })
                {
                    bool isDown = (buttons & (ushort)direction) != 0;
                    bool wasDown = (prevButtons & (ushort)direction) != 0;

                    if (isDown && !wasDown)
                    {
                        InputEvent?.Invoke(new GamepadButtonsPressed(direction));
                        pressStart[direction] = now;
                        lastFired[direction] = now;
                    }
                    else if (isDown && wasDown && pressStart.TryGetValue(direction, out var startedAt))
                    {
                        if ((now - startedAt).TotalMilliseconds >= AppConstants.NavigationInitialRepeatDelayMs
                            && (now - lastFired[direction]).TotalMilliseconds >= AppConstants.NavigationRepeatIntervalMs)
                        {
                            InputEvent?.Invoke(new GamepadButtonsPressed(direction));
                            lastFired[direction] = now;
                        }
                    }
                    else if (!isDown)
                    {
                        pressStart.Remove(direction);
                        lastFired.Remove(direction);
                    }
                }

                prevButtons = buttons;
            }

            Thread.Sleep(AppConstants.GamepadPollIntervalMs);
        }
    }

    private static XInputButtons StickToDpadBits(short thumbLX, short thumbLY)
    {
        XInputButtons bits = XInputButtons.None;
        if (thumbLY > XInputInterop.LeftThumbDeadzone) bits |= XInputButtons.DPadUp;
        if (thumbLY < -XInputInterop.LeftThumbDeadzone) bits |= XInputButtons.DPadDown;
        if (thumbLX < -XInputInterop.LeftThumbDeadzone) bits |= XInputButtons.DPadLeft;
        if (thumbLX > XInputInterop.LeftThumbDeadzone) bits |= XInputButtons.DPadRight;
        return bits;
    }

    public void Dispose()
    {
        _running = false;
        _pollThread.Join(AppConstants.GamepadPollIntervalMs * 4);
    }
}
