namespace Tactadile.Config;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public bool EdgeSnappingEnabled { get; set; } = true;
    public Dictionary<string, HotkeyBinding> Hotkeys { get; set; } = new();
    public bool GesturesEnabled { get; set; } = true;
    public Dictionary<string, GestureBinding> Gestures { get; set; } = new();
}

public sealed class HotkeyBinding
{
    public List<string> Modifiers { get; set; } = new();
    public string Key { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public sealed class GestureBinding
{
    public string Type { get; set; } = string.Empty;
    public List<string> Modifiers { get; set; } = new();
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, double> Parameters { get; set; } = new();
}
