using Tactadile.Native;

namespace Tactadile.Core;

/// <summary>
/// Listens for new top-level windows appearing system-wide using SetWinEventHook.
/// Fires WindowShown for each new visible top-level window that passes basic filtering.
/// </summary>
public sealed class WindowEventHook : IDisposable
{
    private IntPtr _hook;
    private readonly NativeMethods.WinEventDelegate _winEventDelegate;

    public event Action<IntPtr>? WindowShown;

    public WindowEventHook()
    {
        // Store delegate as field to prevent GC collection (same pattern as KeyboardHook)
        _winEventDelegate = OnWinEvent;
    }

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;

        _hook = NativeMethods.SetWinEventHook(
            NativeConstants.EVENT_OBJECT_SHOW,
            NativeConstants.EVENT_OBJECT_SHOW,
            IntPtr.Zero,
            _winEventDelegate,
            0, 0,
            NativeConstants.WINEVENT_OUTOFCONTEXT | NativeConstants.WINEVENT_SKIPOWNPROCESS);
    }

    public void Uninstall()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != NativeConstants.OBJID_WINDOW) return;
        if (idChild != 0) return;
        if (hwnd == IntPtr.Zero) return;
        if (!NativeMethods.IsWindowVisible(hwnd)) return;
        if (NativeMethods.GetWindow(hwnd, NativeConstants.GW_OWNER) != IntPtr.Zero) return;

        int style = NativeMethods.GetWindowLong(hwnd, NativeConstants.GWL_STYLE);
        if ((style & (int)NativeConstants.WS_CAPTION) == 0) return;

        WindowShown?.Invoke(hwnd);
    }

    public void Dispose()
    {
        Uninstall();
    }
}
