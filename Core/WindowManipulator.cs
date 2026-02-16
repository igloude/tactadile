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

    private static void RestoreIfMaximized(IntPtr hwnd)
    {
        if (NativeMethods.IsZoomed(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);
    }
}
