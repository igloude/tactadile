using Tactadile.Config;
using Tactadile.Native;

namespace Tactadile.Core;

/// <summary>
/// Matches mouse button events against hotkey bindings that use mouse buttons as keys.
/// Runs synchronously inside the WH_MOUSE_LL callback so it can suppress matched events.
/// Reads current keyboard modifier state via GetAsyncKeyState.
/// </summary>
public sealed class MouseHotkeyMatcher
{
    private Dictionary<(uint modFlags, uint mouseId), (ActionType action, Dictionary<string, double> parameters)> _lookup = new();

    /// <summary>Fired when a mouse button event matches a configured hotkey binding.</summary>
    public event Action<ActionType, Dictionary<string, double>>? ActionMatched;

    /// <summary>True if any mouse-button hotkey bindings are configured.</summary>
    public bool HasAnyBindings => _lookup.Count > 0;

    /// <summary>
    /// Rebuild lookup from config. Called on startup and config reload.
    /// </summary>
    public void BuildLookup(AppConfig config)
    {
        var newLookup = new Dictionary<(uint modFlags, uint mouseId), (ActionType, Dictionary<string, double>)>();

        foreach (var (_, binding) in config.Hotkeys)
        {
            if (string.IsNullOrEmpty(binding.Key)) continue;
            if (!ConfigManager.IsMouseButton(binding.Key)) continue;
            if (!ConfigManager.TryParseMouseButton(binding.Key, out uint mouseId)) continue;
            if (!ConfigManager.TryParseAction(binding.Action, out var actionType)) continue;
            if (!ConfigManager.TryParseModifiers(binding.Modifiers, out uint modFlags)) continue;

            newLookup[(modFlags, mouseId)] = (actionType, binding.Parameters);
        }

        _lookup = newLookup;
    }

    /// <summary>
    /// Called from MouseHook callback. Returns true if the event was consumed (should be suppressed).
    /// </summary>
    public bool TryMatch(int messageType, MSLLHOOKSTRUCT hookData, uint mouseId)
    {
        if (_lookup.Count == 0) return false;

        uint currentMods = GetCurrentModifierFlags();

        if (_lookup.TryGetValue((currentMods, mouseId), out var entry))
        {
            ActionMatched?.Invoke(entry.action, entry.parameters);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads current modifier key state synchronously via GetAsyncKeyState.
    /// Safe to call from hook callbacks.
    /// </summary>
    private static uint GetCurrentModifierFlags()
    {
        uint flags = 0;
        if ((NativeMethods.GetAsyncKeyState(NativeConstants.VK_LWIN) & 0x8000) != 0 ||
            (NativeMethods.GetAsyncKeyState(0x5C) & 0x8000) != 0) // VK_RWIN
            flags |= NativeConstants.MOD_WIN;
        if ((NativeMethods.GetAsyncKeyState(NativeConstants.VK_SHIFT) & 0x8000) != 0)
            flags |= NativeConstants.MOD_SHIFT;
        if ((NativeMethods.GetAsyncKeyState(NativeConstants.VK_CONTROL) & 0x8000) != 0)
            flags |= NativeConstants.MOD_CONTROL;
        if ((NativeMethods.GetAsyncKeyState(NativeConstants.VK_MENU) & 0x8000) != 0)
            flags |= NativeConstants.MOD_ALT;
        return flags;
    }
}
