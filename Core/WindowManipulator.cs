using WinMove.Native;

namespace WinMove.Core;

public sealed class WindowManipulator
{
    private const byte OpacityStep = 25;  // ~10% per step
    private const byte OpacityMin = 25;   // Never fully invisible
    private const byte OpacityMax = 255;

    public void Minimize(IntPtr hwnd)
    {
        NativeMethods.ShowWindow(hwnd, NativeConstants.SW_MINIMIZE);
    }

    public void Maximize(IntPtr hwnd)
    {
        if (NativeMethods.IsZoomed(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);
        else
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_MAXIMIZE);
    }

    public void Restore(IntPtr hwnd)
    {
        NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);
    }

    public void MoveWindow(IntPtr hwnd, int x, int y, int width, int height)
    {
        RestoreIfMaximized(hwnd);
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

    private static void RestoreIfMaximized(IntPtr hwnd)
    {
        if (NativeMethods.IsZoomed(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);
    }
}
