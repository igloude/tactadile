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

    // Ref-counted modifier tracking for override matching
    private int _winCount, _shiftCount, _ctrlCount, _altCount;

    public event Action<uint, bool>? KeyStateChanged; // (vkCode, isDown)

    public KeyboardHook()
    {
        _hookProc = HookCallback;
    }

    /// <summary>
    /// Updates the set of combos that should be suppressed by the hook.
    /// Actions are dispatched via ModifierSession (KeyStateChanged still fires).
    /// </summary>
    public void SetOverrides(bool enabled, IReadOnlySet<(uint modFlags, uint vk)> combos)
    {
        _overrideEnabled = enabled;
        _overrideCombos = new HashSet<(uint, uint)>(combos);
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
                }

                // Suppress overridden combos on key-down of non-modifier keys
                if (_overrideEnabled && isDown && !IsModifierVk(hookStruct.vkCode))
                {
                    var combo = (ComputeModFlags(), hookStruct.vkCode);
                    if (_overrideCombos.Contains(combo))
                    {
                        // Fire KeyStateChanged so ModifierSession dispatches the action
                        KeyStateChanged?.Invoke(hookStruct.vkCode, isDown);
                        return (IntPtr)1; // Suppress key from reaching Windows
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

    private static bool IsModifierVk(uint vk)
    {
        return vk is 0xA0 or 0xA1  // LSHIFT, RSHIFT
            or 0xA2 or 0xA3        // LCONTROL, RCONTROL
            or 0xA4 or 0xA5        // LMENU, RMENU
            or 0x5B or 0x5C;       // LWIN, RWIN
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
