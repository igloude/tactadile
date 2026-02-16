using System.Runtime.InteropServices;
using Tactadile.Native;

namespace Tactadile.Core;

public static class EdgeSnapHelper
{
    /// <summary>
    /// Magic value set on dwExtraInfo for all synthetic keystrokes we inject.
    /// KeyboardHook checks this to skip our own SendInput events.
    /// </summary>
    public static readonly IntPtr Signature = new(0x574D4F56); // "WMOV"

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tactadile", "edge-snap-debug.log");

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n"); }
        catch { /* ignore */ }
    }

    private const int EdgeThreshold = 20;

    /// <summary>
    /// If the cursor is at a screen edge, sends the appropriate Win+Arrow
    /// keystroke via SendInput to trigger native Windows snap.
    /// Returns true if a snap was triggered.
    /// </summary>
    public static bool TrySnap(IntPtr hwnd, POINT cursor)
    {
        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        IntPtr hMonitor = NativeMethods.MonitorFromPoint(cursor, NativeConstants.MONITOR_DEFAULTTONEAREST);
        NativeMethods.GetMonitorInfo(hMonitor, ref mi);
        RECT bounds = mi.rcMonitor;

        Log($"TrySnap: cursor=({cursor.X},{cursor.Y}) bounds=L{bounds.Left},T{bounds.Top},R{bounds.Right},B{bounds.Bottom} hwnd=0x{hwnd:X}");

        // Detect which edge the cursor is at (top takes priority over sides).
        ushort arrowVk;
        if (cursor.Y <= bounds.Top + EdgeThreshold)
            arrowVk = NativeConstants.VK_UP;
        else if (cursor.X <= bounds.Left + EdgeThreshold)
            arrowVk = NativeConstants.VK_LEFT;
        else if (cursor.X >= bounds.Right - EdgeThreshold)
            arrowVk = NativeConstants.VK_RIGHT;
        else
        {
            Log("TrySnap: no edge detected, returning false");
            return false;
        }

        Log($"TrySnap: edge detected, arrowVk=0x{arrowVk:X2}");

        bool fgResult = NativeMethods.SetForegroundWindow(hwnd);
        Log($"TrySnap: SetForegroundWindow returned {fgResult}");

        SendSnapKeystroke(arrowVk);
        return true;
    }

    private static void SendSnapKeystroke(ushort arrowVk)
    {
        var inputs = new List<INPUT>();

        // Release any non-Win modifiers that are physically held,
        // otherwise the OS sees Win+Shift+Arrow (move to monitor) instead of Win+Arrow (snap).
        bool shiftHeld = (NativeMethods.GetAsyncKeyState(NativeConstants.VK_SHIFT) & 0x8000) != 0;
        bool ctrlHeld = (NativeMethods.GetAsyncKeyState(NativeConstants.VK_CONTROL) & 0x8000) != 0;
        bool altHeld = (NativeMethods.GetAsyncKeyState(NativeConstants.VK_MENU) & 0x8000) != 0;

        Log($"SendSnapKeystroke: arrowVk=0x{arrowVk:X2} shiftHeld={shiftHeld} ctrlHeld={ctrlHeld} altHeld={altHeld}");

        if (shiftHeld) inputs.Add(MakeKeyInput(NativeConstants.VK_SHIFT, keyUp: true));
        if (ctrlHeld) inputs.Add(MakeKeyInput(NativeConstants.VK_CONTROL, keyUp: true));
        if (altHeld) inputs.Add(MakeKeyInput(NativeConstants.VK_MENU, keyUp: true));

        // Self-contained Win+Arrow sequence
        inputs.Add(MakeKeyInput(NativeConstants.VK_LWIN, keyUp: false));
        inputs.Add(MakeKeyInput(arrowVk, keyUp: false));
        inputs.Add(MakeKeyInput(arrowVk, keyUp: true));
        inputs.Add(MakeKeyInput(NativeConstants.VK_LWIN, keyUp: true));

        var inputArray = inputs.ToArray();
        Log($"SendSnapKeystroke: sending {inputArray.Length} inputs, INPUT size={Marshal.SizeOf<INPUT>()}");
        uint sent = NativeMethods.SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<INPUT>());
        int err = Marshal.GetLastWin32Error();
        Log($"SendSnapKeystroke: SendInput returned {sent} (expected {inputArray.Length}), lastError={err}");
    }

    private static INPUT MakeKeyInput(ushort vk, bool keyUp)
    {
        return new INPUT
        {
            type = NativeConstants.INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = keyUp ? NativeConstants.KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = Signature
                }
            }
        };
    }
}
