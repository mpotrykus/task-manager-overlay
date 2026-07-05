using System.Windows;
using TaskManagerOverlay.Services;
using TaskManagerOverlay.ViewModels;
using TaskManagerOverlay.Views;
using DrawingIcon = System.Drawing.SystemIcons;
using WinForms = System.Windows.Forms;

namespace TaskManagerOverlay;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    private WinForms.NotifyIcon? _notifyIcon;
    private GamepadInputService? _gamepadInput;
    private GlobalHotkeyService? _hotkeyService;
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
        _hotkeyService = new GlobalHotkeyService(AppConstants.HotkeyModifiers, AppConstants.HotkeyVirtualKey);

        _ = new OverlayToggleCoordinator(overlayWindow, _gamepadInput, _hotkeyService, _processMonitor);

        SetupTrayIcon(overlayWindow, _hotkeyService.IsRegistered);

        if (AppSettingsService.OpenOverlayOnStart)
        {
            overlayWindow.ShowOverlay();
            _processMonitor.SetVisible(true);
        }
    }

    private void SetupTrayIcon(OverlayWindow overlayWindow, bool hotkeyRegistered)
    {
        var menu = new WinForms.ContextMenuStrip
        {
            Renderer = new TrayMenuRenderer(),
            BackColor = TrayMenuColorTable.Background,
            ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("Segoe UI", 9.5F),
            Padding = new WinForms.Padding(4, 6, 4, 6)
        };
        menu.Items.Add("Show Overlay", null, (_, _) => overlayWindow.Dispatcher.Invoke(overlayWindow.ShowOverlay));

        var autostartItem = new WinForms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = AutostartService.IsEnabled()
        };
        autostartItem.CheckedChanged += (_, _) => AutostartService.SetEnabled(autostartItem.Checked);
        menu.Items.Add(autostartItem);

        var openOnStartItem = new WinForms.ToolStripMenuItem("Open Overlay on Start")
        {
            CheckOnClick = true,
            Checked = AppSettingsService.OpenOverlayOnStart
        };
        openOnStartItem.CheckedChanged += (_, _) => AppSettingsService.OpenOverlayOnStart = openOnStartItem.Checked;
        menu.Items.Add(openOnStartItem);

        var closeToTrayItem = new WinForms.ToolStripMenuItem("Close to Tray")
        {
            CheckOnClick = true,
            Checked = AppSettingsService.CloseToTray,
            ToolTipText = "When checked, closing the overlay window hides it and keeps the app running in the tray. " +
                          "When unchecked, closing the window fully exits the app - use it like a normal application."
        };
        closeToTrayItem.CheckedChanged += (_, _) => AppSettingsService.CloseToTray = closeToTrayItem.Checked;
        menu.Items.Add(closeToTrayItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());
        var exitItem = new WinForms.ToolStripMenuItem("Exit", null, (_, _) => overlayWindow.Dispatcher.Invoke(overlayWindow.RequestQuit))
        {
            ForeColor = System.Drawing.Color.FromArgb(0xEA, 0x54, 0x59)
        };
        menu.Items.Add(exitItem);

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = DrawingIcon.Application,
            Visible = true,
            Text = "Task Manager Overlay",
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => overlayWindow.Dispatcher.Invoke(overlayWindow.ShowOverlay);

        if (!hotkeyRegistered)
        {
            _notifyIcon.ShowBalloonTip(
                4000,
                "Task Manager Overlay",
                "The Ctrl+Alt+O hotkey is already in use by another app. Use the controller's Back+Start combo to toggle the overlay instead.",
                WinForms.ToolTipIcon.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _hotkeyService?.Dispose();
        _gamepadInput?.Dispose();
        _processMonitor?.Dispose();

        if (_ownsMutex)
            _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// Dark palette for the tray's ContextMenuStrip, mirroring the overlay's own accent/background
    /// colors so the WinForms menu doesn't look like a stray piece of a different, lighter app.
    /// </summary>
    private sealed class TrayMenuColorTable : WinForms.ProfessionalColorTable
    {
        public static readonly System.Drawing.Color Background = System.Drawing.Color.FromArgb(0x22, 0x22, 0x22);
        private static readonly System.Drawing.Color BorderColor = System.Drawing.Color.FromArgb(0x3A, 0x3A, 0x3A);
        private static readonly System.Drawing.Color AccentTop = System.Drawing.Color.FromArgb(0x5B, 0x94, 0xFF);
        private static readonly System.Drawing.Color AccentBottom = System.Drawing.Color.FromArgb(0x3A, 0x5B, 0xD9);
        private static readonly System.Drawing.Color AccentPressed = System.Drawing.Color.FromArgb(0x2E, 0x47, 0xB0);
        private static readonly System.Drawing.Color CheckSurface = System.Drawing.Color.FromArgb(0x2A, 0x2A, 0x2A);

        public override System.Drawing.Color ToolStripDropDownBackground => Background;
        public override System.Drawing.Color ImageMarginGradientBegin => Background;
        public override System.Drawing.Color ImageMarginGradientMiddle => Background;
        public override System.Drawing.Color ImageMarginGradientEnd => Background;
        public override System.Drawing.Color MenuBorder => BorderColor;
        public override System.Drawing.Color MenuItemBorder => AccentTop;
        public override System.Drawing.Color SeparatorDark => BorderColor;
        public override System.Drawing.Color SeparatorLight => BorderColor;
        public override System.Drawing.Color MenuItemSelected => AccentTop;
        public override System.Drawing.Color MenuItemSelectedGradientBegin => AccentTop;
        public override System.Drawing.Color MenuItemSelectedGradientEnd => AccentBottom;
        public override System.Drawing.Color MenuItemPressedGradientBegin => AccentPressed;
        public override System.Drawing.Color MenuItemPressedGradientEnd => AccentPressed;
        public override System.Drawing.Color CheckBackground => CheckSurface;
        public override System.Drawing.Color CheckSelectedBackground => AccentBottom;
        public override System.Drawing.Color CheckPressedBackground => AccentPressed;
    }

    /// <summary>
    /// The stock ToolStripProfessionalRenderer draws its checkmark glyph in a near-black tone that's
    /// effectively invisible against <see cref="TrayMenuColorTable"/>'s dark check background, so the
    /// checkmark itself is hand-drawn here in white instead of relying on the theme-independent default.
    /// </summary>
    private sealed class TrayMenuRenderer : WinForms.ToolStripProfessionalRenderer
    {
        public TrayMenuRenderer() : base(new TrayMenuColorTable()) { }

        protected override void OnRenderItemCheck(WinForms.ToolStripItemImageRenderEventArgs e)
        {
            var bounds = e.ImageRectangle;
            using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 1.6f);
            System.Drawing.Point[] points =
            [
                new(bounds.Left + 2, bounds.Top + bounds.Height / 2),
                new(bounds.Left + bounds.Width / 2 - 1, bounds.Bottom - 3),
                new(bounds.Right - 2, bounds.Top + 2),
            ];
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, points);
        }
    }
}
