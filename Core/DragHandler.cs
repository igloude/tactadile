using Microsoft.UI.Dispatching;
using Tactadile.Native;

namespace Tactadile.Core;

public sealed class DragHandler : IDisposable
{
    private enum DragMode { None, Move, Resize }

    private const int MinWindowSize = 100;

    private readonly WindowManipulator _manipulator;
    private readonly KeyboardHook _keyboardHook;
    private readonly DispatcherQueueTimer _pollTimer;
    private readonly DispatcherQueueTimer _deferredSnapTimer;

    private DragMode _mode = DragMode.None;
    private IntPtr _targetHwnd = IntPtr.Zero;
    private POINT _dragStartCursor;
    private RECT _dragStartWindowRect;
    private POINT _lastPolledCursor; // Updated every poll tick — used for edge snap detection
    private int _pollCount;

    // Deferred snap data (replaces Timer.Tag which doesn't exist on DispatcherQueueTimer)
    private IntPtr _pendingSnapHwnd;
    private POINT _pendingSnapCursor;

    public bool IsDragging => _mode != DragMode.None;
    public bool EdgeSnappingEnabled { get; set; } = true;

    public DragHandler(WindowManipulator manipulator, KeyboardHook keyboardHook, DispatcherQueue dispatcherQueue)
    {
        _manipulator = manipulator;
        _keyboardHook = keyboardHook;
        _keyboardHook.KeyStateChanged += OnKeyStateChanged;

        _pollTimer = dispatcherQueue.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
        _pollTimer.IsRepeating = true;
        _pollTimer.Tick += OnPollTick;

        _deferredSnapTimer = dispatcherQueue.CreateTimer();
        _deferredSnapTimer.Interval = TimeSpan.FromMilliseconds(1); // fires on next message loop iteration
        _deferredSnapTimer.IsRepeating = false;
        _deferredSnapTimer.Tick += OnDeferredSnap;
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
        _pollCount = 0;
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

    private void OnPollTick(DispatcherQueueTimer sender, object args)
    {
        if (_mode == DragMode.None) return;

        NativeMethods.GetCursorPos(out POINT currentCursor);
        _lastPolledCursor = currentCursor;
        _pollCount++;
        if (_pollCount % 10 == 0) // Log every 10th tick (~6fps)
            DebugLog($"poll #{_pollCount}: cursor=({currentCursor.X},{currentCursor.Y})");
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

        NativeMethods.GetCursorPos(out POINT cursorNow);
        DebugLog($"EndDrag: mode={_mode} edgeSnap={EdgeSnappingEnabled} hwnd=0x{_targetHwnd:X} pollCount={_pollCount} lastPolled=({_lastPolledCursor.X},{_lastPolledCursor.Y}) cursorNow=({cursorNow.X},{cursorNow.Y})");

        // Capture state before resetting — we need it for the deferred snap.
        bool shouldSnap = _mode == DragMode.Move && EdgeSnappingEnabled && _targetHwnd != IntPtr.Zero;
        IntPtr snapHwnd = _targetHwnd;

        _mode = DragMode.None;
        _targetHwnd = IntPtr.Zero;

        if (shouldSnap)
        {
            // Use the last cursor position from the 60fps poll loop — this is the most
            // recent position while the drag was actively running.
            POINT cursor = _lastPolledCursor;
            DebugLog($"EdgeSnap: cursor=({cursor.X},{cursor.Y})");

            // SendInput cannot be called from inside a low-level keyboard hook callback:
            // it causes re-entrancy and risks the ~300ms hook timeout (Windows silently
            // kills the hook after repeated timeouts). Post to the message loop instead.
            _pendingSnapHwnd = snapHwnd;
            _pendingSnapCursor = cursor;
            _deferredSnapTimer.Start();
        }
    }

    private void OnDeferredSnap(DispatcherQueueTimer sender, object args)
    {
        EdgeSnapHelper.TrySnap(_pendingSnapHwnd, _pendingSnapCursor);
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

    private static readonly string DebugLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tactadile", "edge-snap-debug.log");
    private static void DebugLog(string msg)
    {
        try { File.AppendAllText(DebugLogPath, $"{DateTime.Now:HH:mm:ss.fff} [Drag] {msg}\n"); }
        catch { }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _deferredSnapTimer.Stop();
        // Don't dispose _keyboardHook — lifecycle is managed by App
        _keyboardHook.KeyStateChanged -= OnKeyStateChanged;
    }
}
