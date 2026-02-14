namespace WinMove.Config;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public Dictionary<string, HotkeyBinding> Hotkeys { get; set; } = new();
}

public sealed class HotkeyBinding
{
    public List<string> Modifiers { get; set; } = new();
    public string Key { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
