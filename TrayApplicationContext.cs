using System.Diagnostics;
using WinMove.Config;
using WinMove.Core;
using WinMove.UI;

namespace WinMove;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ConfigManager _configManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly WindowManipulator _manipulator;
    private readonly WindowDetector _windowDetector;
    private readonly KeyboardHook _keyboardHook;
    private readonly ModifierSession _modifierSession;
    private readonly DragHandler _dragHandler;
    private readonly SnapCycleTracker _snapTracker = new();
    private SettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        _configManager = new ConfigManager();
        _manipulator = new WindowManipulator();
        _windowDetector = new WindowDetector();

        // KeyboardHook is owned here and shared between DragHandler and ModifierSession
        _keyboardHook = new KeyboardHook();

        _modifierSession = new ModifierSession();
        _modifierSession.BuildLookup(_configManager.CurrentConfig);
        _modifierSession.ActionTriggered += OnModifierSessionAction;

        _dragHandler = new DragHandler(_manipulator, _keyboardHook);
        _hotkeyManager = new HotkeyManager(_configManager, OnHotkeyAction);

        // Wire keyboard hook to modifier session
        _keyboardHook.KeyStateChanged += _modifierSession.OnKeyStateChanged;
        _keyboardHook.Install();

        _configManager.ConfigChanged += OnConfigChanged;

        _trayIcon = CreateTrayIcon();
        _hotkeyManager.RegisterAll();
    }

    /// <summary>
    /// Called by HotkeyManager when a registered WM_HOTKEY fires (first combo press).
    /// </summary>
    private void OnHotkeyAction(ActionType action, uint modFlags, uint vk)
    {
        // Seed the modifier session so subsequent key switches are detected
        _modifierSession.OnHotkeyFired(modFlags, vk);
        DispatchAction(action);
    }

    /// <summary>
    /// Called by ModifierSession when a key switch is detected while modifiers are held.
    /// </summary>
    private void OnModifierSessionAction(ActionType action)
    {
        DispatchAction(action);
    }

    private void DispatchAction(ActionType action)
    {
        // If dragging, only allow switching between drag modes
        if (_dragHandler.IsDragging)
        {
            if (action is ActionType.MoveDrag or ActionType.ResizeDrag)
            {
                // DragHandler handles the seamless switch internally
                if (action == ActionType.MoveDrag)
                    _dragHandler.StartMoveDrag(IntPtr.Zero); // hwnd ignored during switch
                else
                    _dragHandler.StartResizeDrag(IntPtr.Zero);
            }
            return;
        }

        var hwnd = _windowDetector.GetWindowUnderCursor();
        if (hwnd == IntPtr.Zero) return;

        // Reset snap cycle on non-snap actions
        if (action is not (ActionType.SnapLeft or ActionType.SnapRight))
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
        }
    }

    private void OnConfigChanged(AppConfig newConfig)
    {
        if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
        {
            _trayIcon.ContextMenuStrip.BeginInvoke(() => OnConfigChanged(newConfig));
            return;
        }

        _hotkeyManager.UnregisterAll();
        _hotkeyManager.RegisterAll();

        // Rebuild modifier session lookup with new config
        _modifierSession.BuildLookup(newConfig);

        _trayIcon.ShowBalloonTip(2000, "win-move", "Configuration reloaded", ToolTipIcon.Info);
    }

    private NotifyIcon CreateTrayIcon()
    {
        var contextMenu = new ContextMenuStrip();

        contextMenu.Items.Add("Settings", null, (s, e) => ShowSettings());
        contextMenu.Items.Add("Reload Config", null, (s, e) => _configManager.Reload());
        contextMenu.Items.Add("Open Config Folder", null, (s, e) =>
        {
            Process.Start(new ProcessStartInfo(ConfigManager.ConfigDirectory)
            {
                UseShellExecute = true
            });
        });
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("About", null, (s, e) =>
        {
            MessageBox.Show("win-move v1.0\nWindow management utility\n\nAll actions target the window under the mouse cursor.",
                "About win-move", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

        var icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "win-move",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        icon.DoubleClick += (s, e) => ShowSettings();

        return icon;
    }

    private void ShowSettings()
    {
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            _settingsForm.Show();
            _settingsForm.BringToFront();
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_configManager);
        _settingsForm.Show();
    }

    private static Icon LoadIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(iconPath))
            return new Icon(iconPath);

        return SystemIcons.Application;
    }

    private void ExitApplication()
    {
        _hotkeyManager.UnregisterAll();
        _dragHandler.Dispose();
        _hotkeyManager.Dispose();
        _keyboardHook.Dispose();
        _configManager.Dispose();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _hotkeyManager.Dispose();
            _dragHandler.Dispose();
            _keyboardHook.Dispose();
            _configManager.Dispose();
        }
        base.Dispose(disposing);
    }
}
