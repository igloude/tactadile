using Microsoft.UI.Dispatching;
using Tactadile.Config;
using Tactadile.Native;

namespace Tactadile.Core;

/// <summary>
/// Matches newly-appearing windows against launch rules and positions them.
/// </summary>
public sealed class LaunchRuleEngine : IDisposable
{
    private readonly WindowEventHook _eventHook;
    private readonly WindowManipulator _manipulator;
    private readonly DispatcherQueue _dispatcherQueue;

    private List<LaunchRule> _rules = new();
    private bool _enabled;
    private bool _started;

    // Track windows we've already positioned to avoid re-triggering
    private readonly HashSet<IntPtr> _recentlyPositioned = new();
    private readonly object _lock = new();

    // Track which apps have had their first window positioned (for ApplyOnlyToFirstWindow)
    private readonly HashSet<string> _firstWindowApplied = new();

    public LaunchRuleEngine(WindowEventHook eventHook, WindowManipulator manipulator,
        DispatcherQueue dispatcherQueue)
    {
        _eventHook = eventHook;
        _manipulator = manipulator;
        _dispatcherQueue = dispatcherQueue;
    }

    /// <summary>
    /// Rebuilds the rule set from config. Call on startup and on config change.
    /// </summary>
    public void LoadRules(AppConfig config)
    {
        _enabled = config.AutoPositionEnabled;
        lock (_lock)
        {
            _rules = config.LaunchRules
                .Where(r => r.Enabled)
                .ToList();
        }
    }

    public void Start()
    {
        if (_started) return;
        _started = true;
        _eventHook.WindowShown += OnWindowShown;
        _eventHook.Install();
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        _eventHook.WindowShown -= OnWindowShown;
        _eventHook.Uninstall();
    }

    private void OnWindowShown(IntPtr hwnd)
    {
        if (!_enabled) return;

        lock (_lock)
        {
            if (_recentlyPositioned.Contains(hwnd)) return;
        }

        var exePath = ProcessInfoHelper.GetExecutablePath(hwnd);
        if (exePath == null) return;
        var processName = Path.GetFileNameWithoutExtension(exePath);

        LaunchRule? matchedRule = null;
        lock (_lock)
        {
            foreach (var rule in _rules)
            {
                bool matchByPath = !string.IsNullOrEmpty(rule.ExecutablePath) &&
                    string.Equals(rule.ExecutablePath, exePath, StringComparison.OrdinalIgnoreCase);
                bool matchByName = !string.IsNullOrEmpty(rule.ProcessName) &&
                    string.Equals(rule.ProcessName, processName, StringComparison.OrdinalIgnoreCase);

                if (matchByPath || matchByName)
                {
                    matchedRule = rule;
                    break;
                }
            }
        }

        if (matchedRule == null) return;

        if (matchedRule.ApplyOnlyToFirstWindow)
        {
            var key = (matchedRule.ExecutablePath ?? matchedRule.ProcessName).ToLowerInvariant();
            lock (_lock)
            {
                if (!_firstWindowApplied.Add(key))
                    return;
            }
        }

        lock (_lock)
        {
            _recentlyPositioned.Add(hwnd);
        }

        // Hide the window off-screen immediately to prevent it from
        // flashing at its old/default position during the delay.
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, -32000, -32000, 0, 0,
            NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE);

        int delay = Math.Clamp(matchedRule.DelayMs, 50, 2000);
        var captured = matchedRule;

        _ = new System.Threading.Timer(_ =>
        {
            _dispatcherQueue.TryEnqueue(() => ApplyRule(hwnd, captured));
        }, null, delay, Timeout.Infinite);

        _ = new System.Threading.Timer(_ =>
        {
            lock (_lock) { _recentlyPositioned.Remove(hwnd); }
        }, null, 5000, Timeout.Infinite);
    }

    private void ApplyRule(IntPtr hwnd, LaunchRule rule)
    {
        if (!NativeMethods.IsWindowVisible(hwnd)) return;

        var monitors = MonitorHelper.GetAllMonitors();
        if (monitors.Count == 0) return;

        MonitorHelper.MonitorInfo monitor;
        if (rule.MonitorIndex >= 0 && rule.MonitorIndex < monitors.Count)
            monitor = monitors[rule.MonitorIndex];
        else
            monitor = monitors[0];

        if (!Enum.TryParse<ZoneType>(rule.Zone, ignoreCase: true, out var zoneType))
            return;

        var rect = ZoneCalculator.Calculate(zoneType, monitor.WorkArea);
        _manipulator.MoveWindow(hwnd, rect.X, rect.Y, rect.Width, rect.Height);
    }

    public void Dispose()
    {
        Stop();
    }
}
