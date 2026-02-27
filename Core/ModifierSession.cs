using Tactadile.Config;
using Tactadile.Native;

namespace Tactadile.Core;

/// <summary>
/// Tracks held modifier keys and detects when the primary key changes while
/// modifiers remain held. This enables "seamless key switching" — e.g. holding
/// Win+Shift and pressing Z then X fires both MoveDrag and ResizeDrag without
/// the user needing to release and re-press modifiers.
///
/// The first combo press is normally handled by RegisterHotKey / WM_HOTKEY (via
/// HotkeyManager). However, if all keys are pressed simultaneously and WM_HOTKEY
/// misses the race, the low-level keyboard hook acts as a fallback and fires the
/// action directly. Subsequent key switches while modifiers stay held are also
/// detected here via the hook and fire ActionTriggered.
/// </summary>
public sealed class ModifierSession
{
    // VK codes for left/right variants of each modifier
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_RSHIFT = 0xA1;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_RCONTROL = 0xA3;
    private const uint VK_LMENU = 0xA4;
    private const uint VK_RMENU = 0xA5;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;

    private readonly HashSet<uint> _activeModifiers = new();
    private uint _activePrimaryKey;
    private bool _primaryKeyReleased = true;
    private bool _sessionSeeded; // True after first WM_HOTKEY seeds the session
    private bool _justFired;     // True when ActionTriggered fired for the current keypress

    private Dictionary<(uint mods, uint vk), ActionType> _lookup = new();
    private Dictionary<uint, ActionType> _modifierOnlyLookup = new();
    private uint _firedModifierOnlyCombo; // tracks which mod-only combo was already fired

    /// <summary>
    /// Fired when a key switch is detected (modifier held + new primary key).
    /// NOT fired for the initial hotkey press (that goes through HotkeyManager).
    /// </summary>
    public event Action<ActionType>? ActionTriggered;

    /// <summary>
    /// Fired whenever the set of held modifier keys changes.
    /// Parameter is the current MOD_* bitmask (0 when all modifiers released).
    /// Used by GestureEngine to arm/disarm gesture detection.
    /// </summary>
    public event Action<uint>? ModifierFlagsChanged;

    /// <summary>
    /// Returns true if ActionTriggered fired for the current keypress, then resets the flag.
    /// Used by OnHotkeyAction to avoid double-dispatching when both the keyboard hook
    /// and WM_HOTKEY fire for the same key event (hook is synchronous, WM_HOTKEY is posted).
    /// </summary>
    public bool ConsumeIfFired()
    {
        if (!_justFired) return false;
        _justFired = false;
        return true;
    }

    /// <summary>
    /// Rebuild the reverse-lookup table from the current config.
    /// Must be called on startup and whenever config changes.
    /// </summary>
    public void BuildLookup(AppConfig config)
    {
        var newLookup = new Dictionary<(uint mods, uint vk), ActionType>();
        var newModOnlyLookup = new Dictionary<uint, ActionType>();

        foreach (var (_, binding) in config.Hotkeys)
        {
            if (!ConfigManager.TryParseAction(binding.Action, out var actionType))
                continue;
            if (!ConfigManager.TryParseModifiers(binding.Modifiers, out uint modFlags))
                continue;

            if (ConfigManager.TryParseKey(binding.Key, out uint vk))
            {
                newLookup[(modFlags, vk)] = actionType;
            }
            else if (modFlags != 0)
            {
                // Modifier-only hotkey
                newModOnlyLookup[modFlags] = actionType;
            }
        }

        _lookup = newLookup;
        _modifierOnlyLookup = newModOnlyLookup;
    }

    /// <summary>
    /// Called by TrayApplicationContext when a WM_HOTKEY fires, to seed the session
    /// with the known modifier+key state so subsequent key switches work correctly.
    /// </summary>
    public void OnHotkeyFired(uint modFlags, uint vk)
    {
        // Seed the active modifiers from the MOD flags
        _activeModifiers.Clear();

        // We store canonical left-variant VK codes to represent each modifier
        if ((modFlags & NativeConstants.MOD_WIN) != 0) _activeModifiers.Add(VK_LWIN);
        if ((modFlags & NativeConstants.MOD_SHIFT) != 0) _activeModifiers.Add(VK_LSHIFT);
        if ((modFlags & NativeConstants.MOD_CONTROL) != 0) _activeModifiers.Add(VK_LCONTROL);
        if ((modFlags & NativeConstants.MOD_ALT) != 0) _activeModifiers.Add(VK_LMENU);

        _activePrimaryKey = vk;
        _primaryKeyReleased = false;
        _sessionSeeded = true;
        // Don't reset _justFired here — it needs to survive until ConsumeIfFired() is called
    }

    /// <summary>
    /// Subscribe this to KeyboardHook.KeyStateChanged.
    /// Tracks modifier and primary key state to detect key switches.
    /// </summary>
    public void OnKeyStateChanged(uint vkCode, bool isDown)
    {
        if (IsModifierKey(vkCode))
        {
            if (isDown)
            {
                _activeModifiers.Add(vkCode);

                // Check modifier-only hotkeys when no primary key is held
                if (_activePrimaryKey == 0 || _primaryKeyReleased)
                {
                    uint modFlags = ConvertToModFlags();
                    if (modFlags != _firedModifierOnlyCombo
                        && _modifierOnlyLookup.TryGetValue(modFlags, out var action))
                    {
                        _firedModifierOnlyCombo = modFlags;
                        _sessionSeeded = true;
                        _justFired = true;
                        ActionTriggered?.Invoke(action);
                    }
                }
            }
            else
            {
                _activeModifiers.Remove(vkCode);
                // Also remove the other variant (L/R) if present
                _activeModifiers.Remove(GetOtherVariant(vkCode));

                // Reset modifier-only fired tracking so the combo can fire again
                _firedModifierOnlyCombo = 0;

                if (_activeModifiers.Count == 0)
                {
                    // All modifiers released — end session
                    _activePrimaryKey = 0;
                    _primaryKeyReleased = true;
                    _sessionSeeded = false;
                }
            }

            ModifierFlagsChanged?.Invoke(ConvertToModFlags());
        }
        else // Primary key
        {
            if (isDown)
            {
                if (_activeModifiers.Count > 0
                    && (vkCode != _activePrimaryKey || _primaryKeyReleased))
                {
                    uint modFlags = ConvertToModFlags();
                    if (_lookup.TryGetValue((modFlags, vkCode), out var action))
                    {
                        _activePrimaryKey = vkCode;
                        _primaryKeyReleased = false;

                        // Seed the session if not already seeded — this handles
                        // the race where all keys are pressed simultaneously and
                        // WM_HOTKEY hasn't fired yet.
                        _sessionSeeded = true;

                        _justFired = true;
                        ActionTriggered?.Invoke(action);
                    }
                    else if (_sessionSeeded)
                    {
                        // Key switch to a non-configured key — still track it
                        _activePrimaryKey = vkCode;
                        _primaryKeyReleased = false;
                    }
                }
            }
            else
            {
                if (vkCode == _activePrimaryKey)
                {
                    _primaryKeyReleased = true;
                }
            }
        }
    }

    private uint ConvertToModFlags()
    {
        uint flags = 0;
        foreach (var vk in _activeModifiers)
        {
            flags |= vk switch
            {
                VK_LWIN or VK_RWIN => NativeConstants.MOD_WIN,
                VK_LSHIFT or VK_RSHIFT => NativeConstants.MOD_SHIFT,
                VK_LCONTROL or VK_RCONTROL => NativeConstants.MOD_CONTROL,
                VK_LMENU or VK_RMENU => NativeConstants.MOD_ALT,
                _ => 0
            };
        }
        return flags;
    }

    private static bool IsModifierKey(uint vkCode)
    {
        return vkCode is VK_LSHIFT or VK_RSHIFT
            or VK_LCONTROL or VK_RCONTROL
            or VK_LMENU or VK_RMENU
            or VK_LWIN or VK_RWIN;
    }

    private static uint GetOtherVariant(uint vkCode)
    {
        return vkCode switch
        {
            VK_LSHIFT => VK_RSHIFT,
            VK_RSHIFT => VK_LSHIFT,
            VK_LCONTROL => VK_RCONTROL,
            VK_RCONTROL => VK_LCONTROL,
            VK_LMENU => VK_RMENU,
            VK_RMENU => VK_LMENU,
            VK_LWIN => VK_RWIN,
            VK_RWIN => VK_LWIN,
            _ => vkCode
        };
    }
}
