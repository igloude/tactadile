using System.Diagnostics;
using System.Runtime.InteropServices;
using WinMove.Native;

namespace WinMove.Core;

public sealed class KeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    // Store delegate as field to prevent GC collection while hook is active
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;

    public event Action<uint, bool>? KeyStateChanged; // (vkCode, isDown)

    public KeyboardHook()
    {
        _hookProc = HookCallback;
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
                KeyStateChanged?.Invoke(hookStruct.vkCode, isDown);
            }
        }

        // Always pass to next hook in chain
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
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
