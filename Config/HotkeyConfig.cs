namespace Tactadile.Config;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public bool EdgeSnappingEnabled { get; set; } = true;
    public bool OverrideWindowsKeybinds { get; set; } = true;
    public Dictionary<string, HotkeyBinding> Hotkeys { get; set; } = new();
    public bool GesturesEnabled { get; set; } = true;
    public Dictionary<string, GestureBinding> Gestures { get; set; } = new();
    public bool AutoPositionEnabled { get; set; } = false;
    public List<LaunchRule> LaunchRules { get; set; } = new();
}

public sealed class HotkeyBinding
{
    public List<string> Modifiers { get; set; } = new();
    public string Key { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, double> Parameters { get; set; } = new();
}

public sealed class GestureBinding
{
    public string Type { get; set; } = string.Empty;
    public List<string> Modifiers { get; set; } = new();
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, double> Parameters { get; set; } = new();
}

public sealed class LaunchRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AppName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int MonitorIndex { get; set; } = 0;
    public string Zone { get; set; } = "LeftHalf";
    public bool Enabled { get; set; } = true;
    public bool ApplyOnlyToFirstWindow { get; set; } = false;
    public int DelayMs { get; set; } = 150;
}
