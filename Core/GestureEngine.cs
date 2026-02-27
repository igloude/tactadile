using Microsoft.UI.Dispatching;
using Tactadile.Config;
using Tactadile.Core.Recognizers;
using Tactadile.Native;

namespace Tactadile.Core;

/// <summary>
/// Central orchestrator for modifier-gated mouse gesture detection.
/// Arms/disarms based on modifier key state from ModifierSession.
/// Polls cursor position at 60fps when armed, runs motion recognizers,
/// and handles discrete mouse events (buttons, scroll) from MouseHook.
/// </summary>
public sealed class GestureEngine : IDisposable
{
    private const int CooldownMs = 500;
    private const double DefaultSwipeVelocity = 800;
    private const double DefaultSwipeDisplacement = 80;
    private const double DefaultSwipeCrossAxis = 40;
    private const double DefaultSwipeTimeWindow = 300;
    private const double DefaultShakeTimeWindow = 600;
    private const int DefaultShakeReversals = 3;
    private const double DefaultShakeDisplacement = 40;
    private const double DefaultEdgeFlickVelocity = 1200;
    private const int DefaultEdgeThreshold = 20;

    private readonly MouseHook _mouseHook;
    private readonly DragHandler _dragHandler;
    private readonly DispatcherQueueTimer _pollTimer;

    private readonly CursorRingBuffer _ringBuffer = new(64);
    private readonly ShakeRecognizer _shakeRecognizer = new();

    // Gesture lookup: (modifier flags, gesture type) → action
    private Dictionary<(uint mods, GestureType gesture), ActionType> _gestureLookup = new();

    // Per-gesture-type parameters (cached from config)
    private readonly Dictionary<GestureType, Dictionary<string, double>> _gestureParams = new();

    private bool _isArmed;
    private uint _currentModFlags;
    private long _cooldownUntilMs;
    private int _scrollAccumulator;
    private bool _gesturesEnabled = true;

    /// <summary>
    /// Fired when a gesture is recognized. Wired to App.DispatchAction.
    /// </summary>
    public event Action<ActionType>? GestureTriggered;

    public GestureEngine(MouseHook mouseHook, DragHandler dragHandler, DispatcherQueue dispatcherQueue)
    {
        _mouseHook = mouseHook;
        _dragHandler = dragHandler;

        _pollTimer = dispatcherQueue.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
        _pollTimer.IsRepeating = true;
        _pollTimer.Tick += OnPollTick;

        _mouseHook.MouseButtonPressed += OnMouseButton;
        _mouseHook.MouseScrolled += OnMouseScroll;
    }

    /// <summary>
    /// Build the gesture lookup table from config. Call on startup and config reload.
    /// </summary>
    public void BuildLookup(AppConfig config)
    {
        bool wasArmed = _isArmed;
        if (wasArmed) Disarm();

        _gesturesEnabled = config.GesturesEnabled;
        var newLookup = new Dictionary<(uint mods, GestureType gesture), ActionType>();
        _gestureParams.Clear();

        foreach (var (_, binding) in config.Gestures)
        {
            if (!ConfigManager.TryParseGestureType(binding.Type, out var gestureType))
                continue;
            if (!ConfigManager.TryParseAction(binding.Action, out var actionType))
                continue;
            if (!ConfigManager.TryParseModifiers(binding.Modifiers, out uint modFlags))
                continue;

            newLookup[(modFlags, gestureType)] = actionType;

            // Cache parameters keyed by gesture type (last one wins if duplicates)
            _gestureParams[gestureType] = binding.Parameters;
        }

        _gestureLookup = newLookup;

        // Re-arm if modifiers are still held and gestures are enabled
        if (wasArmed && _gesturesEnabled && _currentModFlags != 0)
            Arm();
    }

    /// <summary>
    /// Called by ModifierSession.ModifierFlagsChanged. Arms/disarms gesture detection.
    /// </summary>
    public void OnModifiersChanged(uint modFlags)
    {
        _currentModFlags = modFlags;

        if (!_gesturesEnabled) return;

        if (modFlags != 0 && HasAnyMatchingGesture(modFlags))
        {
            if (!_isArmed) Arm();
        }
        else
        {
            if (_isArmed) Disarm();
        }
    }

    private bool HasAnyMatchingGesture(uint modFlags)
    {
        foreach (var (key, _) in _gestureLookup)
        {
            if (key.mods == modFlags) return true;
        }
        return false;
    }

    private void Arm()
    {
        _isArmed = true;
        _mouseHook.IsArmed = true;
        _ringBuffer.Clear();
        _shakeRecognizer.Reset();
        _scrollAccumulator = 0;
        _cooldownUntilMs = 0;
        _pollTimer.Start();
    }

    private void Disarm()
    {
        _isArmed = false;
        _mouseHook.IsArmed = false;
        _pollTimer.Stop();
        _ringBuffer.Clear();
        _shakeRecognizer.Reset();
        _scrollAccumulator = 0;
    }

    private void OnPollTick(DispatcherQueueTimer sender, object args)
    {
        if (!_isArmed) return;

        NativeMethods.GetCursorPos(out POINT pt);
        long now = Environment.TickCount64;
        _ringBuffer.Add(pt.X, pt.Y, now);

        // Skip motion recognition during drag or cooldown
        if (_dragHandler.IsDragging) return;
        if (now < _cooldownUntilMs) return;

        // Run motion recognizers
        var detected = EvaluateMotionGestures(now);
        if (detected != null)
        {
            if (_gestureLookup.TryGetValue((_currentModFlags, detected.Value), out var action))
            {
                GestureTriggered?.Invoke(action);
                EnterCooldown(now);
            }
        }
    }

    private GestureType? EvaluateMotionGestures(long now)
    {
        if (_ringBuffer.Count < 3) return null;

        var newest = _ringBuffer.GetByAge(0);

        // 1. Shake detection
        var shakeResult = _shakeRecognizer.Evaluate(
            newest,
            GetParam(GestureType.ShakeHorizontal, "TimeWindowMs", DefaultShakeTimeWindow),
            (int)GetParam(GestureType.ShakeHorizontal, "MinReversals", DefaultShakeReversals),
            GetParam(GestureType.ShakeHorizontal, "MinDisplacementPx", DefaultShakeDisplacement));

        if (shakeResult != null && _gestureLookup.ContainsKey((_currentModFlags, shakeResult.Value)))
            return shakeResult;

        // 2. Swipe detection
        var swipeResult = SwipeRecognizer.Evaluate(
            _ringBuffer,
            GetSwipeParam("MinVelocityPxPerSec", DefaultSwipeVelocity),
            GetSwipeParam("MinDisplacementPx", DefaultSwipeDisplacement),
            GetSwipeParam("MaxCrossAxisPx", DefaultSwipeCrossAxis),
            GetSwipeParam("TimeWindowMs", DefaultSwipeTimeWindow));

        if (swipeResult != null && _gestureLookup.ContainsKey((_currentModFlags, swipeResult.Value)))
            return swipeResult;

        // 3. Edge flick detection
        var flickResult = EdgeFlickRecognizer.Evaluate(
            _ringBuffer,
            GetParam(GestureType.EdgeFlickLeft, "MinVelocityPxPerSec", DefaultEdgeFlickVelocity),
            (int)GetParam(GestureType.EdgeFlickLeft, "EdgeThresholdPx", DefaultEdgeThreshold));

        if (flickResult != null && _gestureLookup.ContainsKey((_currentModFlags, flickResult.Value)))
            return flickResult;

        return null;
    }

    private void OnMouseButton(int messageType, MSLLHOOKSTRUCT hookData)
    {
        if (!_isArmed || _dragHandler.IsDragging) return;

        GestureType gestureType;
        if (messageType == NativeConstants.WM_XBUTTONDOWN)
        {
            int xButton = (int)(hookData.mouseData >> 16);
            gestureType = xButton == NativeConstants.XBUTTON1
                ? GestureType.XButton1
                : GestureType.XButton2;
        }
        else if (messageType == NativeConstants.WM_MBUTTONDOWN)
        {
            gestureType = GestureType.MiddleClick;
        }
        else
        {
            return;
        }

        if (_gestureLookup.TryGetValue((_currentModFlags, gestureType), out var action))
        {
            GestureTriggered?.Invoke(action);
        }
    }

    private void OnMouseScroll(int delta)
    {
        if (!_isArmed || _dragHandler.IsDragging) return;

        _scrollAccumulator += delta;

        // Fire per WHEEL_DELTA (120) units to normalize high-res scroll mice
        while (_scrollAccumulator >= NativeConstants.WHEEL_DELTA)
        {
            _scrollAccumulator -= NativeConstants.WHEEL_DELTA;
            if (_gestureLookup.TryGetValue((_currentModFlags, GestureType.ScrollUp), out var upAction))
                GestureTriggered?.Invoke(upAction);
        }

        while (_scrollAccumulator <= -NativeConstants.WHEEL_DELTA)
        {
            _scrollAccumulator += NativeConstants.WHEEL_DELTA;
            if (_gestureLookup.TryGetValue((_currentModFlags, GestureType.ScrollDown), out var downAction))
                GestureTriggered?.Invoke(downAction);
        }
    }

    private void EnterCooldown(long now)
    {
        _cooldownUntilMs = now + CooldownMs;
        _ringBuffer.Clear();
        _shakeRecognizer.Reset();
    }

    private double GetParam(GestureType type, string key, double defaultValue)
    {
        if (_gestureParams.TryGetValue(type, out var parameters) &&
            parameters.TryGetValue(key, out var value))
            return value;
        return defaultValue;
    }

    /// <summary>
    /// Get swipe parameter — checks all four swipe directions for the parameter,
    /// since they share the same parameter names.
    /// </summary>
    private double GetSwipeParam(string key, double defaultValue)
    {
        // Check any configured swipe gesture for the parameter
        foreach (var type in new[] { GestureType.SwipeLeft, GestureType.SwipeRight, GestureType.SwipeUp, GestureType.SwipeDown })
        {
            if (_gestureParams.TryGetValue(type, out var parameters) &&
                parameters.TryGetValue(key, out var value))
                return value;
        }
        return defaultValue;
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _mouseHook.MouseButtonPressed -= OnMouseButton;
        _mouseHook.MouseScrolled -= OnMouseScroll;
    }
}
