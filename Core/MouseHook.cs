using System.Diagnostics;
using System.Runtime.InteropServices;
using Tactadile.Native;

namespace Tactadile.Core;

/// <summary>
/// Low-level mouse hook (WH_MOUSE_LL) for capturing mouse button and scroll events.
/// Only processes discrete events (XButton, middle click, scroll wheel) — cursor
/// position is tracked separately via GetCursorPos polling in GestureEngine.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    // Store delegate as field to prevent GC collection while hook is active
    private readonly NativeMethods.LowLevelMouseProc _hookProc;

    /// <summary>
    /// When false, the hook callback passes through without processing.
    /// Set by GestureEngine when modifiers are held.
    /// </summary>
    public bool IsArmed { get; set; }

    /// <summary>
    /// Fired for XButton1, XButton2, or middle mouse button press.
    /// Parameters: (int messageType, MSLLHOOKSTRUCT hookData)
    /// </summary>
    public event Action<int, MSLLHOOKSTRUCT>? MouseButtonPressed;

    /// <summary>
    /// Fired for mouse wheel scroll.
    /// Parameter: wheel delta (positive = up, negative = down).
    /// </summary>
    public event Action<int>? MouseScrolled;

    public MouseHook()
    {
        _hookProc = HookCallback;
    }

    public void Install()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeConstants.WH_MOUSE_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsArmed)
        {
            int msg = wParam.ToInt32();

            if (msg is NativeConstants.WM_XBUTTONDOWN
                    or NativeConstants.WM_MBUTTONDOWN
                    or NativeConstants.WM_MOUSEWHEEL)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if (msg == NativeConstants.WM_MOUSEWHEEL)
                {
                    // Wheel delta is in the high word of mouseData (signed)
                    int delta = (short)(hookStruct.mouseData >> 16);
                    MouseScrolled?.Invoke(delta);
                }
                else
                {
                    MouseButtonPressed?.Invoke(msg, hookStruct);
                }
            }
        }

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
