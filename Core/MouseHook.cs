using System.Diagnostics;
using System.Runtime.InteropServices;
using Tactadile.Config;
using Tactadile.Native;

namespace Tactadile.Core;

/// <summary>
/// Low-level mouse hook (WH_MOUSE_LL) for capturing mouse button and scroll events.
/// Supports two dispatch phases:
///   Phase 1: Mouse hotkey matching (always active) — suppresses matched events.
///   Phase 2: Gesture forwarding (only when armed) — existing behavior for gesture system.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    // Store delegate as field to prevent GC collection while hook is active
    private readonly NativeMethods.LowLevelMouseProc _hookProc;

    /// <summary>
    /// Synchronous hotkey matcher callback. Called for all discrete mouse events regardless
    /// of IsArmed. Returns true if the event should be suppressed (hotkey consumed it).
    /// Parameters: (int messageType, MSLLHOOKSTRUCT hookData, uint syntheticMouseId)
    /// </summary>
    public Func<int, MSLLHOOKSTRUCT, uint, bool>? HotkeyMatcher { get; set; }

    /// <summary>
    /// When false, gesture forwarding (Phase 2) is disabled.
    /// Set by GestureEngine when modifiers are held.
    /// </summary>
    public bool IsArmed { get; set; }

    /// <summary>
    /// Fired for XButton1, XButton2, or middle mouse button press (Phase 2, gesture system).
    /// Parameters: (int messageType, MSLLHOOKSTRUCT hookData)
    /// </summary>
    public event Action<int, MSLLHOOKSTRUCT>? MouseButtonPressed;

    /// <summary>
    /// Fired for mouse wheel scroll (Phase 2, gesture system).
    /// Parameter: wheel delta (positive = up, negative = down).
    /// </summary>
    public event Action<int>? MouseScrolled;

    // Double/triple click detection state
    private int _clickCount;
    private uint _lastClickTime;
    private POINT _lastClickPos;
    private readonly int _doubleClickTimeMs;
    private const int DoubleClickDistancePx = 4;

    public MouseHook()
    {
        _hookProc = HookCallback;
        _doubleClickTimeMs = NativeMethods.GetDoubleClickTime();
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
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();

            if (msg is NativeConstants.WM_LBUTTONDOWN or NativeConstants.WM_RBUTTONDOWN
                    or NativeConstants.WM_MBUTTONDOWN or NativeConstants.WM_XBUTTONDOWN
                    or NativeConstants.WM_MOUSEWHEEL or NativeConstants.WM_MOUSEHWHEEL)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                // --- Phase 1: Hotkey matching (always active) ---
                if (HotkeyMatcher != null)
                {
                    if (msg is NativeConstants.WM_LBUTTONDOWN or NativeConstants.WM_RBUTTONDOWN
                            or NativeConstants.WM_MBUTTONDOWN or NativeConstants.WM_XBUTTONDOWN)
                    {
                        // Double/triple click detection (left button only)
                        if (msg == NativeConstants.WM_LBUTTONDOWN)
                        {
                            uint now = hookStruct.time;
                            bool withinTime = (now - _lastClickTime) <= (uint)_doubleClickTimeMs;
                            bool withinDistance =
                                Math.Abs(hookStruct.pt.X - _lastClickPos.X) <= DoubleClickDistancePx &&
                                Math.Abs(hookStruct.pt.Y - _lastClickPos.Y) <= DoubleClickDistancePx;

                            if (withinTime && withinDistance)
                                _clickCount++;
                            else
                                _clickCount = 1;

                            _lastClickTime = now;
                            _lastClickPos = hookStruct.pt;

                            // Try multi-click IDs first (higher specificity)
                            if (_clickCount == 2)
                            {
                                if (HotkeyMatcher(msg, hookStruct, 0x1000A)) // MouseDoubleClick
                                    return (IntPtr)1;
                            }
                            else if (_clickCount >= 3)
                            {
                                if (HotkeyMatcher(msg, hookStruct, 0x1000B)) // MouseTripleClick
                                {
                                    _clickCount = 0;
                                    return (IntPtr)1;
                                }
                            }
                        }

                        // Single-button hotkey matching
                        uint mouseId = ConfigManager.MouseMessageToId(msg, hookStruct.mouseData);
                        if (mouseId != 0 && HotkeyMatcher(msg, hookStruct, mouseId))
                            return (IntPtr)1;
                    }
                    else // WM_MOUSEWHEEL or WM_MOUSEHWHEEL
                    {
                        int delta = (short)(hookStruct.mouseData >> 16);
                        uint scrollId;
                        if (msg == NativeConstants.WM_MOUSEWHEEL)
                            scrollId = delta > 0 ? 0x10006u : 0x10007u; // ScrollUp / ScrollDown
                        else
                            scrollId = delta > 0 ? 0x10009u : 0x10008u; // ScrollRight / ScrollLeft

                        if (HotkeyMatcher(msg, hookStruct, scrollId))
                            return (IntPtr)1;
                    }
                }

                // --- Phase 2: Gesture forwarding (only when armed) ---
                if (IsArmed)
                {
                    if (msg is NativeConstants.WM_XBUTTONDOWN or NativeConstants.WM_MBUTTONDOWN)
                    {
                        MouseButtonPressed?.Invoke(msg, hookStruct);
                    }
                    else if (msg == NativeConstants.WM_MOUSEWHEEL)
                    {
                        int delta = (short)(hookStruct.mouseData >> 16);
                        MouseScrolled?.Invoke(delta);
                    }
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
