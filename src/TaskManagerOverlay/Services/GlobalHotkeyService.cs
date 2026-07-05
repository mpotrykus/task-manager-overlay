using System.Windows.Interop;
using TaskManagerOverlay.Native;

namespace TaskManagerOverlay.Services;

/// <summary>
/// Registers a global keyboard hotkey against a hidden, message-only window (HWND_MESSAGE).
/// Kept separate from the overlay window so the hotkey's lifetime doesn't depend on the
/// overlay's show/hide/transparency state.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private static readonly IntPtr HwndMessage = new(-3);
    private const int HotkeyId = 0xA1B2;

    private readonly HwndSource _source;
    private bool _registered;

    public event Action? HotkeyPressed;

    public bool IsRegistered => _registered;

    public GlobalHotkeyService(uint modifiers, uint virtualKey)
    {
        var parameters = new HwndSourceParameters("TMO_HotkeySink")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0,
            ParentWindow = HwndMessage
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        _registered = HotkeyInterop.RegisterHotKey(_source.Handle, HotkeyId, modifiers | HotkeyInterop.MOD_NOREPEAT, virtualKey);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyInterop.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
            HotkeyInterop.UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
