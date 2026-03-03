using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Tactadile.Config;
using Tactadile.Core;
using Tactadile.Core.FancyZones;
using Tactadile.Core.Ipc;
using Tactadile.Helpers;
using Tactadile.Native;
using Tactadile.Licensing;
using Tactadile.UI;

namespace Tactadile;

public partial class App : Application
{
    private readonly ConfigManager _configManager;
    private readonly LicenseManager _licenseManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly WindowManipulator _manipulator;
    private readonly WindowDetector _windowDetector;
    private readonly KeyboardHook _keyboardHook;
    private readonly ModifierSession _modifierSession;
    private readonly DragHandler _dragHandler;
    private readonly SnapCycleTracker _snapTracker = new();
    private readonly MouseHook _mouseHook;
    private readonly GestureEngine _gestureEngine;
    private readonly WindowEventHook _windowEventHook;
    private readonly LaunchRuleEngine _launchRuleEngine;
    private readonly WinSnapOverrideManager _winSnapOverride;
    private readonly FancyZonesLayoutProvider _fancyZonesProvider;
    private readonly CommandPipeServer _commandPipeServer;
    private readonly TrayIconManager _trayIcon;
    private readonly DispatcherQueue _dispatcherQueue;

    private MainWindow? _mainWindow;
    private Window? _lifetimeWindow; // Hidden window to keep WinUI alive while tray icon runs

    public KeyboardHook KeyboardHook => _keyboardHook;

    public App()
    {
        this.InitializeComponent();

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _configManager = new ConfigManager();

        _winSnapOverride = new WinSnapOverrideManager();
        _winSnapOverride.RecoverIfNeeded();
        if (_configManager.CurrentConfig.DisableNativeSnap)
            _winSnapOverride.Enable();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => _winSnapOverride.Dispose();

        _licenseManager = new LicenseManager();
        _licenseManager.Initialize();

        _manipulator = new WindowManipulator();
        _windowDetector = new WindowDetector();

        _keyboardHook = new KeyboardHook();

        _modifierSession = new ModifierSession();
        _modifierSession.BuildLookup(_configManager.CurrentConfig);
        _modifierSession.ActionTriggered += OnModifierSessionAction;

        _dragHandler = new DragHandler(_manipulator, _keyboardHook, _dispatcherQueue);
        _dragHandler.EdgeSnappingEnabled = _configManager.CurrentConfig.EdgeSnappingEnabled;
        _hotkeyManager = new HotkeyManager(_configManager, OnHotkeyAction);

        _mouseHook = new MouseHook();
        _gestureEngine = new GestureEngine(_mouseHook, _dragHandler, _dispatcherQueue);
        _gestureEngine.BuildLookup(_configManager.CurrentConfig);
        _gestureEngine.GestureTriggered += OnGestureAction;

        _keyboardHook.KeyStateChanged += _modifierSession.OnKeyStateChanged;
        _modifierSession.ModifierFlagsChanged += _gestureEngine.OnModifiersChanged;

        _keyboardHook.Install();
        _mouseHook.Install();

        _windowEventHook = new WindowEventHook();
        _launchRuleEngine = new LaunchRuleEngine(_windowEventHook, _manipulator, _dispatcherQueue);
        _launchRuleEngine.LoadRules(_configManager.CurrentConfig);

        _fancyZonesProvider = new FancyZonesLayoutProvider();
        _fancyZonesProvider.Initialize();
        _fancyZonesProvider.LayoutsChanged += OnFancyZonesLayoutsChanged;

        _commandPipeServer = new CommandPipeServer(
            executeAction: (actionName, parameters) =>
            {
                if (ConfigManager.TryParseAction(actionName, out var action))
                    DispatchAction(action, parameters);
            },
            getActions: BuildActionInfoList,
            dispatcherQueue: _dispatcherQueue);

        _configManager.ConfigChanged += OnConfigChanged;

        _trayIcon = new TrayIconManager(
            onShowSettings: ShowMainWindow,
            onShowAbout: ShowAbout,
            onCreateRule: CreateRuleFromCurrentWindow,
            onExit: ExitApplication);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Create a hidden window to keep WinUI alive when the settings window is closed.
        // Without this, WinUI exits when the last visible window closes.
        _lifetimeWindow = new Window { Title = "" };
        var presenter = _lifetimeWindow.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        presenter?.SetBorderAndTitleBar(false, false);
        _lifetimeWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(0, 0));
        _lifetimeWindow.AppWindow.Move(new Windows.Graphics.PointInt32(-32000, -32000));

        _hotkeyManager.RegisterAll();
        UpdateHookOverrides();
        _trayIcon.Show();

        if (_configManager.CurrentConfig.AutoPositionEnabled && _licenseManager.IsAutoPositionAllowed)
            _launchRuleEngine.Start();

        _commandPipeServer.Start();

        // Best-effort license refresh (fire-and-forget)
        _ = TryRefreshLicenseAsync();
    }

    private async Task TryRefreshLicenseAsync()
    {
        try { await _licenseManager.TryRefreshAsync(); }
        catch { /* silently ignore */ }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null || _mainWindow.IsClosed)
        {
            _mainWindow = new MainWindow(_configManager, _licenseManager, _fancyZonesProvider);
            _mainWindow.Closed += (s, e) => _mainWindow = null;
        }

        _mainWindow.Activate();
    }

    private void ShowAbout()
    {
        ShowMainWindow();
        _mainWindow?.NavigateToAbout();
    }

    private void CreateRuleFromCurrentWindow()
    {
        ShowMainWindow();
        _mainWindow?.NavigateToAutoPosition(showAppPicker: true);
    }

    private void ExitApplication()
    {
        _commandPipeServer.Dispose();
        _fancyZonesProvider.Dispose();
        _winSnapOverride.Dispose();
        _launchRuleEngine.Dispose();
        _hotkeyManager.UnregisterAll();
        _gestureEngine.Dispose();
        _dragHandler.Dispose();
        _hotkeyManager.Dispose();
        _mouseHook.Dispose();
        _keyboardHook.Dispose();
        _configManager.Dispose();
        _trayIcon.Dispose();

        _mainWindow?.Close();
        _lifetimeWindow?.Close();
        Exit();
    }

    private void OnHotkeyAction(ActionType action, uint modFlags, uint vk, Dictionary<string, double> parameters)
    {
        _modifierSession.OnHotkeyFired(modFlags, vk);

        if (_modifierSession.ConsumeIfFired())
            return;

        DispatchAction(action, parameters);
    }

    private void OnModifierSessionAction(ActionType action)
    {
        DispatchAction(action, null);
    }

    private void OnGestureAction(ActionType action)
    {
        DispatchAction(action, null);
    }

    private void DispatchAction(ActionType action, Dictionary<string, double>? parameters)
    {
        if (!_licenseManager.IsActionAllowed(action))
        {
            _trayIcon.ShowNotification("Tactadile",
                $"{ConfigManager.GetFriendlyActionName(action)} requires a Pro license.");
            return;
        }

        if (_dragHandler.IsDragging)
        {
            if (action is ActionType.MoveDrag or ActionType.ResizeDrag)
            {
                if (action == ActionType.MoveDrag)
                    _dragHandler.StartMoveDrag(IntPtr.Zero);
                else
                    _dragHandler.StartResizeDrag(IntPtr.Zero);
            }
            return;
        }

        // Global actions — no target window needed
        switch (action)
        {
            case ActionType.TaskView:
                KeystrokeSender.Send(NativeConstants.VK_LWIN, NativeConstants.VK_TAB);
                return;
            case ActionType.NextVirtualDesktop:
                KeystrokeSender.Send(
                    new ushort[] { NativeConstants.VK_LWIN, NativeConstants.VK_CONTROL },
                    NativeConstants.VK_RIGHT);
                return;
            case ActionType.PrevVirtualDesktop:
                KeystrokeSender.Send(
                    new ushort[] { NativeConstants.VK_LWIN, NativeConstants.VK_CONTROL },
                    NativeConstants.VK_LEFT);
                return;
            case ActionType.MinimizeAll:
                KeystrokeSender.Send(NativeConstants.VK_LWIN, NativeConstants.VK_D);
                return;
            case ActionType.CascadeWindows:
                bool fromRight = parameters?.GetValueOrDefault("CascadeFromRight", 0) != 0;
                _manipulator.CascadeWindows(fromRight);
                return;
        }

        // Window-targeted actions
        var hwnd = _windowDetector.GetWindowUnderCursor();
        if (hwnd == IntPtr.Zero) return;

        if (action is not (ActionType.SnapLeft or ActionType.SnapRight or ActionType.SnapToFancyZone))
        {
            _snapTracker.Reset();
        }

        switch (action)
        {
            case ActionType.MoveDrag:
                _dragHandler.StartMoveDrag(hwnd);
                break;
            case ActionType.ResizeDrag:
                _dragHandler.StartResizeDrag(hwnd);
                break;
            case ActionType.Minimize:
                _manipulator.Minimize(hwnd);
                break;
            case ActionType.Maximize:
                _manipulator.Maximize(hwnd);
                break;
            case ActionType.Restore:
                _manipulator.Restore(hwnd);
                break;
            case ActionType.ToggleMinimize:
                _manipulator.ToggleMinimize(hwnd);
                break;
            case ActionType.OpacityUp:
                _manipulator.AdjustOpacity(hwnd, increase: true);
                break;
            case ActionType.OpacityDown:
                _manipulator.AdjustOpacity(hwnd, increase: false);
                break;
            case ActionType.SnapLeft:
            case ActionType.SnapRight:
                _snapTracker.Snap(hwnd, action, _manipulator);
                break;
            case ActionType.ZoomIn:
                NativeMethods.SetForegroundWindow(hwnd);
                KeystrokeSender.Send(NativeConstants.VK_CONTROL, NativeConstants.VK_OEM_PLUS);
                break;
            case ActionType.ZoomOut:
                NativeMethods.SetForegroundWindow(hwnd);
                KeystrokeSender.Send(NativeConstants.VK_CONTROL, NativeConstants.VK_OEM_MINUS);
                break;
            case ActionType.ResizeWindow:
                int rw = (int)(parameters?.GetValueOrDefault("Width", 1280) ?? 1280);
                int rh = (int)(parameters?.GetValueOrDefault("Height", 720) ?? 720);
                _manipulator.ResizeWindow(hwnd, rw, rh);
                break;
            case ActionType.CenterWindow:
                double wp = parameters?.GetValueOrDefault("WidthPercent", 60) ?? 60;
                double hp = parameters?.GetValueOrDefault("HeightPercent", 80) ?? 80;
                _manipulator.CenterWindow(hwnd, wp, hp);
                break;
            case ActionType.NudgeUp:
                _manipulator.NudgeWindow(hwnd, 0, -(int)(parameters?.GetValueOrDefault("Distance", 10) ?? 10));
                break;
            case ActionType.NudgeDown:
                _manipulator.NudgeWindow(hwnd, 0, (int)(parameters?.GetValueOrDefault("Distance", 10) ?? 10));
                break;
            case ActionType.NudgeLeft:
                _manipulator.NudgeWindow(hwnd, -(int)(parameters?.GetValueOrDefault("Distance", 10) ?? 10), 0);
                break;
            case ActionType.NudgeRight:
                _manipulator.NudgeWindow(hwnd, (int)(parameters?.GetValueOrDefault("Distance", 10) ?? 10), 0);
                break;
            case ActionType.SnapToFancyZone:
                SnapToFancyZone(hwnd, parameters);
                break;
        }
    }

    private void SnapToFancyZone(IntPtr hwnd, Dictionary<string, double>? parameters)
    {
        if (parameters == null) return;
        int bindingIndex = (int)(parameters.GetValueOrDefault("FancyZoneBindingIndex", -1));
        var config = _configManager.CurrentConfig;
        if (bindingIndex < 0 || bindingIndex >= config.FancyZoneHotkeys.Count) return;

        var fzBinding = config.FancyZoneHotkeys[bindingIndex];
        var monitor = MonitorHelper.GetMonitorForWindow(hwnd);
        var zones = _fancyZonesProvider.ResolveZonesForLayout(fzBinding.LayoutUuid, monitor.WorkArea);

        if (fzBinding.ZoneIndex < 0 || fzBinding.ZoneIndex >= zones.Count)
        {
            _trayIcon.ShowNotification("Tactadile",
                $"FancyZone {fzBinding.ZoneIndex + 1} no longer exists in this layout.");
            return;
        }

        var zone = zones[fzBinding.ZoneIndex];
        _manipulator.MoveWindow(hwnd, zone.X, zone.Y, zone.Width, zone.Height);
    }

    private void OnConfigChanged(AppConfig newConfig)
    {
        // Marshal to UI thread if needed
        if (!_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => OnConfigChanged(newConfig));
            return;
        }

        _hotkeyManager.UnregisterAll();
        _hotkeyManager.RegisterAll();
        UpdateHookOverrides();

        _modifierSession.BuildLookup(newConfig);
        _gestureEngine.BuildLookup(newConfig);
        _dragHandler.EdgeSnappingEnabled = newConfig.EdgeSnappingEnabled;
        _winSnapOverride.SetEnabled(newConfig.DisableNativeSnap);

        _launchRuleEngine.LoadRules(newConfig);
        if (newConfig.AutoPositionEnabled && _licenseManager.IsAutoPositionAllowed)
            _launchRuleEngine.Start();
        else
            _launchRuleEngine.Stop();

        _trayIcon.ShowNotification("Tactadile", "Configuration reloaded");
    }

    private void OnFancyZonesLayoutsChanged()
    {
        if (!_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(OnFancyZonesLayoutsChanged);
            return;
        }

        // Re-register hotkeys so FancyZones bindings pick up layout changes
        _hotkeyManager.UnregisterAll();
        _hotkeyManager.RegisterAll();
        UpdateHookOverrides();
    }

    private List<ActionInfo> BuildActionInfoList()
    {
        var config = _configManager.CurrentConfig;
        var actions = new List<ActionInfo>();

        foreach (var actionType in Enum.GetValues<ActionType>())
        {
            if (actionType == ActionType.SnapToFancyZone) continue; // FZ zones are dynamic

            string hotkey = "";
            foreach (var (_, binding) in config.Hotkeys)
            {
                if (ConfigManager.TryParseAction(binding.Action, out var parsedAction) && parsedAction == actionType)
                {
                    var parts = new List<string>(binding.Modifiers);
                    if (!string.IsNullOrEmpty(binding.Key))
                        parts.Add(binding.Key);
                    if (parts.Count > 0)
                        hotkey = string.Join("+", parts);
                    break;
                }
            }

            actions.Add(new ActionInfo
            {
                Name = actionType.ToString(),
                FriendlyName = ConfigManager.GetFriendlyActionName(actionType),
                Hotkey = hotkey,
                Category = ConfigManager.GetActionCategory(actionType)
            });
        }

        return actions;
    }

    private void UpdateHookOverrides()
    {
        var config = _configManager.CurrentConfig;
        _keyboardHook.SetOverrides(
            config.OverrideWindowsKeybinds,
            _hotkeyManager.FailedCombos);
    }
}
