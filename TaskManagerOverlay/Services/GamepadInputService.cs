using TaskManagerOverlay.Models;
using TaskManagerOverlay.Native;

namespace TaskManagerOverlay.Services;

/// <summary>
/// Polls all four XInput controller slots independently on a dedicated background thread while
/// started. Some systems report a slot as "connected" (ERROR_SUCCESS) even though nothing real is
/// driving it (e.g. a stale/ghost XInput registration), while the actual controller - including
/// virtual pads created by remapping tools like reWASD - lands on a different slot. Rather than
/// latching onto a single "active" slot, every slot is tracked and processed each tick so real
/// input is picked up regardless of which index it arrives on; idle/phantom slots simply never
/// produce button edges. Start()/Stop() are expected to be tied to the window's focus state so
/// the poll thread only runs while the window can actually consume its input.
/// </summary>
public sealed class GamepadInputService : IDisposable
{
    private const XInputButtons RepeatableDirections =
        XInputButtons.DPadUp | XInputButtons.DPadDown | XInputButtons.DPadLeft | XInputButtons.DPadRight;

    private const int MaxControllers = 4;

    private static readonly XInputButtons[] Directions =
    {
        XInputButtons.DPadUp, XInputButtons.DPadDown, XInputButtons.DPadLeft, XInputButtons.DPadRight
    };

    private Thread? _pollThread;
    private volatile bool _running;

    public event Action<GamepadInputEvent>? InputEvent;

    private sealed class SlotState
    {
        public bool WasConnected;
        public ushort PrevButtons;
        public readonly Dictionary<XInputButtons, DateTime> PressStart = new();
        public readonly Dictionary<XInputButtons, DateTime> LastFired = new();
    }

    public void Start()
    {
        if (_pollThread is not null)
            return;

        _running = true;
        _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "XInputPoll" };
        _pollThread.Start();
    }

    public void Stop()
    {
        if (_pollThread is null)
            return;

        _running = false;
        _pollThread.Join(AppConstants.GamepadPollIntervalMs * 4);
        _pollThread = null;
    }

    private void PollLoop()
    {
        var slots = new SlotState[MaxControllers];
        for (int i = 0; i < MaxControllers; i++)
            slots[i] = new SlotState();

        while (_running)
        {
            for (int i = 0; i < MaxControllers; i++)
                PollSlot(slots[i], (uint)i);

            Thread.Sleep(AppConstants.GamepadPollIntervalMs);
        }
    }

    private void PollSlot(SlotState slot, uint userIndex)
    {
        bool connected = XInputInterop.XInputGetState(userIndex, out var state) == XInputInterop.ErrorSuccess;

        if (connected != slot.WasConnected)
        {
            InputEvent?.Invoke(new GamepadConnectionChanged(connected));
            slot.WasConnected = connected;
            slot.PrevButtons = 0;
            slot.PressStart.Clear();
            slot.LastFired.Clear();
        }

        if (!connected)
            return;

        ushort buttons = (ushort)((XInputButtons)state.Gamepad.wButtons | StickToDpadBits(state.Gamepad.sThumbLX, state.Gamepad.sThumbLY));
        ushort prevButtons = slot.PrevButtons;

        var pressedEdge = (XInputButtons)(buttons & ~prevButtons);

        var nonRepeatPressed = pressedEdge & ~RepeatableDirections;
        if (nonRepeatPressed != 0)
            InputEvent?.Invoke(new GamepadButtonsPressed(nonRepeatPressed));

        DateTime now = DateTime.UtcNow;
        foreach (var direction in Directions)
        {
            bool isDown = (buttons & (ushort)direction) != 0;
            bool wasDown = (prevButtons & (ushort)direction) != 0;

            if (isDown && !wasDown)
            {
                InputEvent?.Invoke(new GamepadButtonsPressed(direction));
                slot.PressStart[direction] = now;
                slot.LastFired[direction] = now;
            }
            else if (isDown && wasDown && slot.PressStart.TryGetValue(direction, out var startedAt))
            {
                if ((now - startedAt).TotalMilliseconds >= AppConstants.NavigationInitialRepeatDelayMs
                    && (now - slot.LastFired[direction]).TotalMilliseconds >= AppConstants.NavigationRepeatIntervalMs)
                {
                    InputEvent?.Invoke(new GamepadButtonsPressed(direction));
                    slot.LastFired[direction] = now;
                }
            }
            else if (!isDown)
            {
                slot.PressStart.Remove(direction);
                slot.LastFired.Remove(direction);
            }
        }

        slot.PrevButtons = buttons;
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

    public void Dispose() => Stop();
}
