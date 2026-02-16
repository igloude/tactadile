using Tactadile.Native;

namespace Tactadile.Core;

public sealed class WindowDetector
{
    // 5-pixel radius, squared to avoid sqrt. Large enough to absorb
    // micro-jitter from typing, small enough that a deliberate nudge breaks it.
    private const int StickyThresholdSquared = 25;

    private IntPtr _lastHwnd = IntPtr.Zero;
    private POINT _lastCursorPos;

    public IntPtr GetWindowUnderCursor()
    {
        if (!NativeMethods.GetCursorPos(out POINT pt))
            return _lastHwnd; // Can't get cursor — use cached

        // If mouse hasn't meaningfully moved and we have a cached target, keep it.
        // This ensures that if a hotkey moves a window out from under the cursor
        // (e.g. snap left), subsequent hotkeys still target that same window
        // until the user physically moves the mouse. A small threshold absorbs
        // micro-movement caused by pressing keyboard keys.
        int dx = pt.X - _lastCursorPos.X;
        int dy = pt.Y - _lastCursorPos.Y;
        if ((dx * dx + dy * dy) <= StickyThresholdSquared && _lastHwnd != IntPtr.Zero)
            return _lastHwnd;

        // Mouse moved — resolve the window under the new position
        IntPtr hwnd = NativeMethods.WindowFromPoint(pt);
        if (hwnd != IntPtr.Zero)
        {
            // Get top-level window — WindowFromPoint may return a child control
            IntPtr root = NativeMethods.GetAncestor(hwnd, NativeConstants.GA_ROOT);
            hwnd = root != IntPtr.Zero ? root : hwnd;
        }

        // Filter out desktop and shell windows (the "empty space" background)
        IntPtr desktop = NativeMethods.GetDesktopWindow();
        IntPtr shell = NativeMethods.GetShellWindow();

        if (hwnd == IntPtr.Zero || hwnd == desktop || hwnd == shell)
        {
            // Mouse moved to empty space — clear cache
            _lastHwnd = IntPtr.Zero;
            _lastCursorPos = pt;
            return IntPtr.Zero;
        }

        // Valid window found — update cache
        _lastHwnd = hwnd;
        _lastCursorPos = pt;
        return hwnd;
    }
}
