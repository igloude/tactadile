using System.Runtime.InteropServices;
using Tactadile.Native;

namespace Tactadile.Core;

public sealed class MonitorHelper
{
    public readonly record struct MonitorInfo(
        IntPtr Handle,
        RECT MonitorBounds,
        RECT WorkArea,
        uint DpiX,
        uint DpiY);

    public static List<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                NativeMethods.GetMonitorInfo(hMonitor, ref mi);

                NativeMethods.GetDpiForMonitor(hMonitor, NativeConstants.MDT_EFFECTIVE_DPI,
                    out uint dpiX, out uint dpiY);

                monitors.Add(new MonitorInfo(hMonitor, mi.rcMonitor, mi.rcWork, dpiX, dpiY));
                return true;
            }, IntPtr.Zero);

        // Sort left-to-right, top-to-bottom for consistent ordering
        monitors.Sort((a, b) =>
        {
            int cmp = a.MonitorBounds.Left.CompareTo(b.MonitorBounds.Left);
            return cmp != 0 ? cmp : a.MonitorBounds.Top.CompareTo(b.MonitorBounds.Top);
        });

        return monitors;
    }

    public static MonitorInfo GetMonitorForWindow(IntPtr hwnd)
    {
        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeConstants.MONITOR_DEFAULTTONEAREST);

        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        NativeMethods.GetMonitorInfo(hMonitor, ref mi);

        NativeMethods.GetDpiForMonitor(hMonitor, NativeConstants.MDT_EFFECTIVE_DPI,
            out uint dpiX, out uint dpiY);

        return new MonitorInfo(hMonitor, mi.rcMonitor, mi.rcWork, dpiX, dpiY);
    }

    public static MonitorInfo? GetNextMonitor(IntPtr hwnd, bool forward)
    {
        var monitors = GetAllMonitors();
        if (monitors.Count <= 1) return null;

        var current = GetMonitorForWindow(hwnd);
        int currentIndex = monitors.FindIndex(m => m.Handle == current.Handle);
        if (currentIndex < 0) return null;

        int nextIndex = forward
            ? (currentIndex + 1) % monitors.Count
            : (currentIndex - 1 + monitors.Count) % monitors.Count;

        return monitors[nextIndex];
    }
}
