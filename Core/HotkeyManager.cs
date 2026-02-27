using System.Runtime.InteropServices;
using Tactadile.Config;
using Tactadile.Native;

namespace Tactadile.Core;

public sealed class HotkeyManager : IDisposable
{
    private readonly ConfigManager _configManager;
    private readonly Action<ActionType, uint, uint> _actionCallback; // (action, modFlags, vk)
    private readonly HiddenMessageWindow _messageWindow;
    private readonly Dictionary<int, ActionType> _registeredHotkeys = new();
    private int _nextId = 1;

    public HotkeyManager(ConfigManager configManager, Action<ActionType, uint, uint> actionCallback)
    {
        _configManager = configManager;
        _actionCallback = actionCallback;
        _messageWindow = new HiddenMessageWindow(OnWmHotkey);
    }

    public void RegisterAll()
    {
        UnregisterAll();
        var config = _configManager.CurrentConfig;
        foreach (var (name, binding) in config.Hotkeys)
        {
            if (!ConfigManager.TryParseAction(binding.Action, out var actionType))
                continue;
            if (!ConfigManager.TryParseModifiers(binding.Modifiers, out uint modifiers))
                continue;
            // Modifier-only hotkeys (empty key) are handled by ModifierSession
            if (!ConfigManager.TryParseKey(binding.Key, out uint vk))
                continue;

            int id = _nextId++;
            if (NativeMethods.RegisterHotKey(
                _messageWindow.Handle, id,
                modifiers | NativeConstants.MOD_NOREPEAT, vk))
            {
                _registeredHotkeys[id] = actionType;
            }
            // Silently skip if registration fails (key combo already taken)
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _registeredHotkeys.Keys)
        {
            NativeMethods.UnregisterHotKey(_messageWindow.Handle, id);
        }
        _registeredHotkeys.Clear();
        _nextId = 1;
    }

    private void OnWmHotkey(int hotkeyId, IntPtr lParam)
    {
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var action))
        {
            // WM_HOTKEY lParam: low word = modifier flags, high word = VK code
            uint modFlags = (uint)((long)lParam & 0xFFFF);
            uint vk = (uint)((long)lParam >> 16);
            _actionCallback(action, modFlags, vk);
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        _messageWindow.Dispose();
    }

    private sealed class HiddenMessageWindow : IDisposable
    {
        private readonly Action<int, IntPtr> _hotkeyCallback;
        private readonly WndProc _wndProcDelegate; // prevent GC
        private IntPtr _hwnd;

        public IntPtr Handle => _hwnd;

        public HiddenMessageWindow(Action<int, IntPtr> hotkeyCallback)
        {
            _hotkeyCallback = hotkeyCallback;
            _wndProcDelegate = WndProcCallback;

            var hInstance = NativeMethods.GetModuleHandle(null);
            var wc = new WNDCLASS
            {
                lpfnWndProc = _wndProcDelegate,
                lpszClassName = "Tactadile_HotkeyMsgWnd",
                hInstance = hInstance
            };
            NativeMethods.RegisterClass(ref wc);

            _hwnd = NativeMethods.CreateWindowEx(
                0, wc.lpszClassName, "", 0,
                0, 0, 0, 0,
                NativeConstants.HWND_MESSAGE,
                IntPtr.Zero, hInstance, IntPtr.Zero);
        }

        private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == (uint)NativeConstants.WM_HOTKEY)
                _hotkeyCallback(wParam.ToInt32(), lParam);
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
