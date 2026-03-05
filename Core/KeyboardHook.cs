using System.Diagnostics;
using System.Runtime.InteropServices;
using Tactadile.Native;

namespace Tactadile.Core;

public sealed class KeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    // Store delegate as field to prevent GC collection while hook is active
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;

    // Override mode: suppress keys that failed RegisterHotKey
    private volatile bool _overrideEnabled;
    private HashSet<(uint modFlags, uint vk)> _overrideCombos = new();

    // Block Copilot (Win+C) independently of the override system
    private volatile bool _blockCopilot;

    // Ref-counted modifier tracking for override matching
    private int _winCount, _shiftCount, _ctrlCount, _altCount;

    // Win key delay: suppress immediate Start Menu on solo Win release
    private bool _winKeyDelayEnabled;
    private int _winKeyDelayMs = 250;
    private Timer? _winDelayTimer;
    private bool _winPressedAlone;

    public event Action<uint, bool>? KeyStateChanged; // (vkCode, isDown)

    public KeyboardHook()
    {
        _hookProc = HookCallback;
    }

    /// <summary>
    /// Updates the set of combos that should be suppressed by the hook.
    /// Actions are dispatched via ModifierSession (KeyStateChanged still fires).
    /// </summary>
    public void SetBlockCopilot(bool block) => _blockCopilot = block;

    public void SetOverrides(bool enabled, IReadOnlySet<(uint modFlags, uint vk)> combos)
    {
        _overrideEnabled = enabled;
        _overrideCombos = new HashSet<(uint, uint)>(combos);
    }

    public void SetWinKeyDelay(bool enabled, int delayMs)
    {
        _winKeyDelayEnabled = enabled;
        _winKeyDelayMs = delayMs;
    }

    /// <summary>
    /// Marks Win as used in an action so the delay timer won't fire Start Menu.
    /// Called from App.DispatchAction to cover gestures and modifier-only hotkeys.
    /// </summary>
    public void ClearWinPressedAlone()
    {
        _winPressedAlone = false;
    }

    public void Install()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeConstants.WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();

            bool isDown = msg is NativeConstants.WM_KEYDOWN or NativeConstants.WM_SYSKEYDOWN;
            bool isUp = msg is NativeConstants.WM_KEYUP or NativeConstants.WM_SYSKEYUP;

            if (isDown || isUp)
            {
                // Skip synthetic keystrokes injected by our own SendInput calls
                if (hookStruct.dwExtraInfo == EdgeSnapHelper.Signature)
                    return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

                // Track modifier state for override matching
                if (IsModifierVk(hookStruct.vkCode))
                {
                    int delta = isDown ? 1 : -1;
                    switch (hookStruct.vkCode)
                    {
                        case 0x5B: case 0x5C: _winCount = Math.Max(0, _winCount + delta); break;
                        case 0xA0: case 0xA1: _shiftCount = Math.Max(0, _shiftCount + delta); break;
                        case 0xA2: case 0xA3: _ctrlCount = Math.Max(0, _ctrlCount + delta); break;
                        case 0xA4: case 0xA5: _altCount = Math.Max(0, _altCount + delta); break;
                    }

                    // Win key delay tracking
                    if (hookStruct.vkCode is 0x5B or 0x5C)
                    {
                        if (isDown)
                        {
                            _winPressedAlone = true;
                            CancelWinDelayTimer();
                        }
                        else if (_winCount == 0 && _winPressedAlone && _winKeyDelayEnabled)
                        {
                            // Suppress Win UP, inject menu mask + synthetic Win UP
                            // to release Windows' internal Win state without opening Start Menu
                            SendMenuMaskKey();
                            SendSyntheticWinUp();
                            StartWinDelayTimer();
                            KeyStateChanged?.Invoke(hookStruct.vkCode, isDown);
                            return (IntPtr)1;
                        }
                    }
                }

                // Track non-modifier key-down while Win is held → Win used in chord
                if (isDown && !IsModifierVk(hookStruct.vkCode) && _winCount > 0)
                    _winPressedAlone = false;

                // Suppress overridden combos on key-down of non-modifier keys
                if (_overrideEnabled && isDown && !IsModifierVk(hookStruct.vkCode))
                {
                    uint modFlags = ComputeModFlags();
                    var combo = (modFlags, hookStruct.vkCode);
                    if (_overrideCombos.Contains(combo))
                    {
                        // Fire KeyStateChanged so ModifierSession dispatches the action
                        KeyStateChanged?.Invoke(hookStruct.vkCode, isDown);

                        // Inject a "menu mask key" to prevent Start Menu ghost activation.
                        // When WIN is held and we suppress the letter key, Windows would
                        // interpret the bare WIN release as "open Start Menu". Injecting
                        // a dummy keystroke (VK 0xE8, unassigned) tricks Windows into
                        // thinking another key was pressed during the WIN hold.
                        if ((modFlags & NativeConstants.MOD_WIN) != 0)
                            SendMenuMaskKey();

                        return (IntPtr)1; // Suppress key from reaching Windows
                    }
                }

                // Block Copilot (Win+C) independently of override system
                if (_blockCopilot && isDown && !IsModifierVk(hookStruct.vkCode))
                {
                    uint mf = ComputeModFlags();
                    if ((mf & NativeConstants.MOD_WIN) != 0 && hookStruct.vkCode == 0x43)
                    {
                        SendMenuMaskKey();
                        return (IntPtr)1;
                    }
                }

                KeyStateChanged?.Invoke(hookStruct.vkCode, isDown);
            }
        }

        // Always pass to next hook in chain
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private uint ComputeModFlags()
    {
        uint flags = 0;
        if (_winCount > 0) flags |= NativeConstants.MOD_WIN;
        if (_shiftCount > 0) flags |= NativeConstants.MOD_SHIFT;
        if (_ctrlCount > 0) flags |= NativeConstants.MOD_CONTROL;
        if (_altCount > 0) flags |= NativeConstants.MOD_ALT;
        return flags;
    }

    /// <summary>
    /// Injects a down+up of an unassigned VK code (0xE8) to prevent Windows
    /// from opening the Start Menu when the WIN key is released after a
    /// suppressed combo. Same technique used by AutoHotkey ("menu mask key").
    /// </summary>
    private static void SendMenuMaskKey()
    {
        const ushort VK_MASK = 0xE8; // Unassigned VK code

        var inputs = new INPUT[2];
        inputs[0] = new INPUT
        {
            type = NativeConstants.INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_MASK,
                    dwExtraInfo = EdgeSnapHelper.Signature
                }
            }
        };
        inputs[1] = new INPUT
        {
            type = NativeConstants.INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_MASK,
                    dwFlags = NativeConstants.KEYEVENTF_KEYUP,
                    dwExtraInfo = EdgeSnapHelper.Signature
                }
            }
        };

        NativeMethods.SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    private static bool IsModifierVk(uint vk)
    {
        return vk is 0xA0 or 0xA1  // LSHIFT, RSHIFT
            or 0xA2 or 0xA3        // LCONTROL, RCONTROL
            or 0xA4 or 0xA5        // LMENU, RMENU
            or 0x5B or 0x5C;       // LWIN, RWIN
    }

    private void CancelWinDelayTimer()
    {
        var old = Interlocked.Exchange(ref _winDelayTimer, null);
        old?.Dispose();
    }

    private void StartWinDelayTimer()
    {
        var timer = new Timer(_ => SimulateWinKeyTap(), null, _winKeyDelayMs, Timeout.Infinite);
        var old = Interlocked.Exchange(ref _winDelayTimer, timer);
        old?.Dispose();
    }

    /// <summary>
    /// Timer callback: injects a Win key tap so Windows opens the Start Menu.
    /// </summary>
    private void SimulateWinKeyTap()
    {
        var inputs = new INPUT[2];
        inputs[0] = MakeKeyInput(NativeConstants.VK_LWIN, keyUp: false);
        inputs[1] = MakeKeyInput(NativeConstants.VK_LWIN, keyUp: true);
        NativeMethods.SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Injects a synthetic Win UP so Windows knows the key is released,
    /// without triggering the Start Menu (menu mask key was sent first).
    /// </summary>
    private static void SendSyntheticWinUp()
    {
        var inputs = new INPUT[1];
        inputs[0] = MakeKeyInput(NativeConstants.VK_LWIN, keyUp: true);
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKeyInput(ushort vk, bool keyUp)
    {
        return new INPUT
        {
            type = NativeConstants.INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = keyUp ? NativeConstants.KEYEVENTF_KEYUP : 0u,
                    dwExtraInfo = EdgeSnapHelper.Signature
                }
            }
        };
    }

    public void Dispose()
    {
        CancelWinDelayTimer();
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
