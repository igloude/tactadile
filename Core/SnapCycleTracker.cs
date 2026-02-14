using WinMove.Config;
using WinMove.Native;

namespace WinMove.Core;

/// <summary>
/// Tracks snap cycling state. Repeated snap presses in the same direction
/// cycle through widths: 2/3 → 1/2 → 1/3. Changing direction or window
/// resets the cycle.
/// </summary>
public sealed class SnapCycleTracker
{
    private static readonly double[] WidthFractions = [2.0 / 3, 1.0 / 2, 1.0 / 3];

    private IntPtr _hwnd = IntPtr.Zero;
    private ActionType _direction;
    private int _cycleIndex;

    /// <summary>
    /// Snap the window in the given direction, cycling width on repeated presses.
    /// </summary>
    public void Snap(IntPtr hwnd, ActionType direction, WindowManipulator manipulator)
    {
        if (hwnd != _hwnd || direction != _direction)
        {
            // New window or direction change — reset cycle
            _hwnd = hwnd;
            _direction = direction;
            _cycleIndex = 0;
        }
        else
        {
            // Same window + same direction — advance cycle (cap at last entry)
            _cycleIndex = Math.Min(_cycleIndex + 1, WidthFractions.Length - 1);
        }

        // Restore if maximized before snapping
        if (NativeMethods.IsZoomed(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);

        var monitor = MonitorHelper.GetMonitorForWindow(hwnd);
        var work = monitor.WorkArea;

        double fraction = WidthFractions[_cycleIndex];
        int width = (int)(work.Width * fraction);
        int x = direction == ActionType.SnapLeft
            ? work.Left
            : work.Left + work.Width - width;

        manipulator.MoveWindow(hwnd, x, work.Top, width, work.Height);
    }

    /// <summary>
    /// Reset the cycle. Call when a non-snap action is performed.
    /// </summary>
    public void Reset()
    {
        _hwnd = IntPtr.Zero;
        _cycleIndex = 0;
    }
}
