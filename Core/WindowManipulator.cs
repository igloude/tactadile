using System.Runtime.InteropServices;
using Tactadile.Native;

namespace Tactadile.Core;

public sealed class WindowManipulator
{
    private const byte OpacityStep = 25;  // ~10% per step
    private const byte OpacityMin = 25;   // Never fully invisible
    private const byte OpacityMax = 255;

    // Saves the normal-position rect before we manipulate a window via SetWindowPos.
    // This lets us restore the original size after snap/move operations, since
    // SetWindowPos overwrites Windows' internal "normal position" tracking.
    private readonly Dictionary<IntPtr, RECT> _savedNormalRect = new();

    public void Minimize(IntPtr hwnd)
    {
        NativeMethods.ShowWindow(hwnd, NativeConstants.SW_MINIMIZE);
    }

    public void Maximize(IntPtr hwnd)
    {
        if (NativeMethods.IsZoomed(hwnd))
            RestoreToNormal(hwnd);
        else
        {
            SaveNormalRect(hwnd);
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_MAXIMIZE);
        }
    }

    public void Restore(IntPtr hwnd)
    {
        RestoreToNormal(hwnd);
    }

    public void ToggleMinimize(IntPtr hwnd)
    {
        if (NativeMethods.IsIconic(hwnd))
            RestoreToNormal(hwnd);
        else
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_MINIMIZE);
    }

    public void MoveWindow(IntPtr hwnd, int x, int y, int width, int height)
    {
        RestoreIfMaximized(hwnd);
        SaveNormalRect(hwnd);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height,
            NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE);
    }

    public void SetPosition(IntPtr hwnd, int x, int y)
    {
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE);
    }

    // Opacity adjustment
    public void AdjustOpacity(IntPtr hwnd, bool increase)
    {
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE);
        byte currentAlpha = OpacityMax;

        if ((exStyle & NativeConstants.WS_EX_LAYERED) != 0)
        {
            NativeMethods.GetLayeredWindowAttributes(hwnd, out _, out currentAlpha, out _);
        }
        else
        {
            NativeMethods.SetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE,
                exStyle | NativeConstants.WS_EX_LAYERED);
        }

        byte newAlpha = increase
            ? (byte)Math.Min(currentAlpha + OpacityStep, OpacityMax)
            : (byte)Math.Max(currentAlpha - OpacityStep, OpacityMin);

        if (newAlpha >= OpacityMax)
        {
            // Fully opaque — remove layered style for performance
            NativeMethods.SetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE,
                exStyle & ~NativeConstants.WS_EX_LAYERED);
        }
        else
        {
            NativeMethods.SetLayeredWindowAttributes(hwnd, 0, newAlpha, NativeConstants.LWA_ALPHA);
        }
    }

    /// <summary>
    /// Saves the window's current normal-position rect if not already tracked,
    /// and only when the window is in a normal (non-maximized, non-minimized) state.
    /// </summary>
    private void SaveNormalRect(IntPtr hwnd)
    {
        if (_savedNormalRect.ContainsKey(hwnd))
            return;

        if (NativeMethods.IsZoomed(hwnd) || NativeMethods.IsIconic(hwnd))
            return;

        var wp = new WINDOWPLACEMENT { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (NativeMethods.GetWindowPlacement(hwnd, ref wp))
            _savedNormalRect[hwnd] = wp.rcNormalPosition;
    }

    /// <summary>
    /// Restores a window to its saved normal rect, or falls back to SW_RESTORE.
    /// Clears the saved rect afterward.
    /// </summary>
    private void RestoreToNormal(IntPtr hwnd)
    {
        if (_savedNormalRect.Remove(hwnd, out var savedRect))
        {
            var wp = new WINDOWPLACEMENT { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };
            NativeMethods.GetWindowPlacement(hwnd, ref wp);
            wp.showCmd = NativeConstants.SW_SHOWNORMAL;
            wp.rcNormalPosition = savedRect;
            NativeMethods.SetWindowPlacement(hwnd, ref wp);
        }
        else
        {
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);
        }
    }

    public void ResizeWindow(IntPtr hwnd, int width, int height)
    {
        RestoreIfMaximized(hwnd);
        NativeMethods.GetWindowRect(hwnd, out RECT currentRect);
        SaveNormalRect(hwnd);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero,
            currentRect.Left, currentRect.Top, width, height,
            NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE);
    }

    public void CenterWindow(IntPtr hwnd, double widthPercent, double heightPercent)
    {
        RestoreIfMaximized(hwnd);
        var monitor = MonitorHelper.GetMonitorForWindow(hwnd);
        var work = monitor.WorkArea;

        int newWidth = (int)(work.Width * widthPercent / 100.0);
        int newHeight = (int)(work.Height * heightPercent / 100.0);
        int x = work.Left + (work.Width - newWidth) / 2;
        int y = work.Top + (work.Height - newHeight) / 2;

        SaveNormalRect(hwnd);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, newWidth, newHeight,
            NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE);
    }

    public void NudgeWindow(IntPtr hwnd, int deltaX, int deltaY)
    {
        RestoreIfMaximized(hwnd);
        NativeMethods.GetWindowRect(hwnd, out RECT currentRect);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero,
            currentRect.Left + deltaX, currentRect.Top + deltaY, 0, 0,
            NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE);
    }

    public void CascadeWindows(bool fromRight)
    {
        var windows = GetCascadeableWindows();
        if (windows.Count == 0) return;

        var monitors = MonitorHelper.GetAllMonitors();
        if (monitors.Count == 0) return;
        var work = monitors[0].WorkArea;

        const int offsetStep = 30;
        int cascadeWidth = (int)(work.Width * 0.7);
        int cascadeHeight = (int)(work.Height * 0.7);

        for (int i = 0; i < windows.Count; i++)
        {
            IntPtr hwnd = windows[i];
            int offset = i * offsetStep;

            int x = fromRight
                ? work.Right - cascadeWidth - offset
                : work.Left + offset;
            int y = work.Top + offset;

            if (x < work.Left) x = work.Left;
            if (x + cascadeWidth > work.Right) cascadeWidth = work.Right - x;
            if (y + cascadeHeight > work.Top + work.Height)
                cascadeHeight = work.Top + work.Height - y;

            RestoreIfMaximized(hwnd);
            MoveWindow(hwnd, x, y, cascadeWidth, cascadeHeight);
        }
    }

    private static List<IntPtr> GetCascadeableWindows()
    {
        var results = new List<IntPtr>();
        IntPtr desktop = NativeMethods.GetDesktopWindow();
        IntPtr shell = NativeMethods.GetShellWindow();

        NativeMethods.EnumWindows((hwnd, lParam) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            if (NativeMethods.IsIconic(hwnd)) return true;
            if (hwnd == desktop || hwnd == shell) return true;
            if (NativeMethods.GetWindowTextLength(hwnd) == 0) return true;
            if (NativeMethods.GetWindow(hwnd, NativeConstants.GW_OWNER) != IntPtr.Zero) return true;

            int style = NativeMethods.GetWindowLong(hwnd, NativeConstants.GWL_STYLE);
            if ((style & (int)NativeConstants.WS_CHILD) != 0) return true;

            results.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        return results;
    }

    private static void RestoreIfMaximized(IntPtr hwnd)
    {
        if (NativeMethods.IsZoomed(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);
    }
}
