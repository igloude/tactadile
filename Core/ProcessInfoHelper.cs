using System.Text;
using Tactadile.Native;

namespace Tactadile.Core;

public static class ProcessInfoHelper
{
    /// <summary>
    /// Gets the full executable path for the process owning the given window.
    /// Returns null if access is denied or the window is invalid.
    /// </summary>
    public static string? GetExecutablePath(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        IntPtr hProcess = NativeMethods.OpenProcess(
            NativeConstants.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero) return null;

        try
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            if (NativeMethods.QueryFullProcessImageName(hProcess, 0, sb, ref size) && size > 0)
                return sb.ToString();
            return null;
        }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }
    }

    /// <summary>
    /// Gets the process name (filename without extension) from a window handle.
    /// </summary>
    public static string? GetProcessName(IntPtr hwnd)
    {
        var path = GetExecutablePath(hwnd);
        if (path == null) return null;
        return Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// Gets the window title text.
    /// </summary>
    public static string GetWindowTitle(IntPtr hwnd)
    {
        int len = NativeMethods.GetWindowTextLength(hwnd);
        if (len == 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Enumerates visible top-level windows, de-duplicated by executable path.
    /// Used by the UI to show a list of running apps.
    /// </summary>
    public static List<RunningAppInfo> GetRunningApps()
    {
        var seen = new Dictionary<string, RunningAppInfo>(StringComparer.OrdinalIgnoreCase);

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            if (NativeMethods.GetWindow(hwnd, NativeConstants.GW_OWNER) != IntPtr.Zero) return true;

            int style = NativeMethods.GetWindowLong(hwnd, NativeConstants.GWL_STYLE);
            if ((style & (int)NativeConstants.WS_CAPTION) == 0) return true;

            var title = GetWindowTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title)) return true;

            var exePath = GetExecutablePath(hwnd);
            if (exePath == null) return true;

            if (!seen.ContainsKey(exePath))
            {
                seen[exePath] = new RunningAppInfo
                {
                    ExecutablePath = exePath,
                    ProcessName = Path.GetFileNameWithoutExtension(exePath),
                    DisplayName = Path.GetFileNameWithoutExtension(exePath),
                    Hwnd = hwnd
                };
            }

            return true;
        }, IntPtr.Zero);

        return seen.Values.OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class RunningAppInfo
{
    public string ExecutablePath { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public IntPtr Hwnd { get; set; }
}
