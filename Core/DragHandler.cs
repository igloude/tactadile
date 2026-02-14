using WinMove.Native;

namespace WinMove.Core;

public sealed class DragHandler : IDisposable
{
    private enum DragMode { None, Move, Resize }

    private const int MinWindowSize = 100;

    private readonly WindowManipulator _manipulator;
    private readonly KeyboardHook _keyboardHook;
    private readonly System.Windows.Forms.Timer _pollTimer;

    private DragMode _mode = DragMode.None;
    private IntPtr _targetHwnd = IntPtr.Zero;
    private POINT _dragStartCursor;
    private RECT _dragStartWindowRect;

    public bool IsDragging => _mode != DragMode.None;

    /// <summary>
    /// Creates a DragHandler with an externally-owned KeyboardHook.
    /// The hook's lifecycle is managed by the caller (TrayApplicationContext).
    /// </summary>
    public DragHandler(WindowManipulator manipulator, KeyboardHook keyboardHook)
    {
        _manipulator = manipulator;
        _keyboardHook = keyboardHook;
        _keyboardHook.KeyStateChanged += OnKeyStateChanged;

        _pollTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps
        _pollTimer.Tick += OnPollTick;
    }

    public void StartMoveDrag(IntPtr hwnd)
    {
        if (_mode == DragMode.Move) return; // Already in move mode

        // If currently in a different drag mode, switch seamlessly
        if (_mode != DragMode.None)
        {
            SwitchDragMode(DragMode.Move);
            return;
        }

        if (NativeMethods.IsZoomed(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);

        NativeMethods.GetCursorPos(out _dragStartCursor);
        NativeMethods.GetWindowRect(hwnd, out _dragStartWindowRect);

        _targetHwnd = hwnd;
        _mode = DragMode.Move;
        _pollTimer.Start();
    }

    public void StartResizeDrag(IntPtr hwnd)
    {
        if (_mode == DragMode.Resize) return; // Already in resize mode

        // If currently in a different drag mode, switch seamlessly
        if (_mode != DragMode.None)
        {
            SwitchDragMode(DragMode.Resize);
            return;
        }

        if (NativeMethods.IsZoomed(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);

        NativeMethods.GetCursorPos(out _dragStartCursor);
        NativeMethods.GetWindowRect(hwnd, out _dragStartWindowRect);

        _targetHwnd = hwnd;
        _mode = DragMode.Resize;
        _pollTimer.Start();
    }

    /// <summary>
    /// Switch between move and resize mid-drag. Re-captures the current cursor
    /// position and window rect as the new baseline so the switch is seamless.
    /// </summary>
    private void SwitchDragMode(DragMode newMode)
    {
        NativeMethods.GetCursorPos(out _dragStartCursor);
        NativeMethods.GetWindowRect(_targetHwnd, out _dragStartWindowRect);
        _mode = newMode;
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        if (_mode == DragMode.None) return;

        NativeMethods.GetCursorPos(out POINT currentCursor);
        int deltaX = currentCursor.X - _dragStartCursor.X;
        int deltaY = currentCursor.Y - _dragStartCursor.Y;

        if (_mode == DragMode.Move)
        {
            _manipulator.SetPosition(_targetHwnd,
                _dragStartWindowRect.Left + deltaX,
                _dragStartWindowRect.Top + deltaY);
        }
        else if (_mode == DragMode.Resize)
        {
            ApplyResize(deltaX, deltaY);
        }
    }

    private void OnKeyStateChanged(uint vkCode, bool isDown)
    {
        if (_mode == DragMode.None) return;

        // End drag when ANY key is released (modifier or primary).
        // Seamless key switching still works: ModifierSession detects the new
        // primary key-down and fires ActionTriggered, which starts a fresh drag
        // via DispatchAction (since IsDragging is now false).
        if (!isDown)
        {
            EndDrag();
        }
    }

    public void EndDrag()
    {
        _pollTimer.Stop();
        _mode = DragMode.None;
        _targetHwnd = IntPtr.Zero;
    }

    private void ApplyResize(int deltaX, int deltaY)
    {
        // Get monitor work area to clamp resize within screen bounds
        var monitor = MonitorHelper.GetMonitorForWindow(_targetHwnd);
        var work = monitor.WorkArea;

        int unclampedWidth = _dragStartWindowRect.Width + deltaX;
        int unclampedHeight = _dragStartWindowRect.Height + deltaY;

        int newWidth = Math.Max(unclampedWidth, MinWindowSize);
        int newHeight = Math.Max(unclampedHeight, MinWindowSize);

        // Clamp so window right edge doesn't exceed work area right
        int maxWidth = work.Left + work.Width - _dragStartWindowRect.Left;
        newWidth = Math.Min(newWidth, maxWidth);

        // Clamp so window bottom edge doesn't exceed work area bottom
        int maxHeight = work.Top + work.Height - _dragStartWindowRect.Top;
        newHeight = Math.Min(newHeight, maxHeight);

        // Rebase the drag origin to absorb any overshoot from clamping.
        // This eliminates the "dead zone" — when the user reverses direction
        // after hitting a boundary, the window responds immediately.
        int actualDeltaX = newWidth - _dragStartWindowRect.Width;
        int actualDeltaY = newHeight - _dragStartWindowRect.Height;

        if (actualDeltaX != deltaX)
            _dragStartCursor.X += (deltaX - actualDeltaX);
        if (actualDeltaY != deltaY)
            _dragStartCursor.Y += (deltaY - actualDeltaY);

        _manipulator.MoveWindow(_targetHwnd,
            _dragStartWindowRect.Left, _dragStartWindowRect.Top,
            newWidth, newHeight);
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
        // Don't dispose _keyboardHook — lifecycle is managed by TrayApplicationContext
        _keyboardHook.KeyStateChanged -= OnKeyStateChanged;
    }
}
