using WinMove.Config;
using WinMove.Native;

namespace WinMove.Core;

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
        _messageWindow.DestroyHandle();
    }

    private sealed class HiddenMessageWindow : NativeWindow
    {
        private readonly Action<int, IntPtr> _hotkeyCallback;

        public HiddenMessageWindow(Action<int, IntPtr> hotkeyCallback)
        {
            _hotkeyCallback = hotkeyCallback;
            var cp = new CreateParams
            {
                Parent = new IntPtr(-3) // HWND_MESSAGE — message-only window
            };
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeConstants.WM_HOTKEY)
            {
                _hotkeyCallback(m.WParam.ToInt32(), m.LParam);
            }
            base.WndProc(ref m);
        }
    }
}
