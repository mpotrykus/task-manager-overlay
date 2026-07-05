using TaskManagerOverlay.Native;

namespace TaskManagerOverlay.Models;

public abstract record GamepadInputEvent;

public sealed record GamepadButtonsPressed(XInputButtons Buttons) : GamepadInputEvent;

public sealed record GamepadConnectionChanged(bool IsConnected) : GamepadInputEvent;
