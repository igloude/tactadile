using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tactadile.Config;

public record DisplaySection(string Name, List<string> Keys);

public sealed class ConfigManager : IDisposable
{
    public static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tactadile");
    public static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private AppConfig _config;
    private readonly object _lock = new();

    public event Action<AppConfig>? ConfigChanged;

    public AppConfig CurrentConfig
    {
        get { lock (_lock) return _config; }
    }

    public ConfigManager()
    {
        _config = LoadOrCreateConfig();
        StartWatching();
    }

    public void Reload()
    {
        try
        {
            var config = LoadOrCreateConfig();
            lock (_lock) _config = config;
            ConfigChanged?.Invoke(config);
        }
        catch
        {
            // Keep previous valid config on failure
        }
    }

    public void Save(AppConfig config)
    {
        lock (_lock) _config = config;
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    public static void ExportConfig(string destinationPath)
    {
        if (File.Exists(ConfigFilePath))
            File.Copy(ConfigFilePath, destinationPath, overwrite: true);
    }

    public void ImportConfig(string sourcePath)
    {
        if (!File.Exists(sourcePath)) return;
        try
        {
            var json = File.ReadAllText(sourcePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config != null)
            {
                Save(config);
                ConfigChanged?.Invoke(config);
            }
        }
        catch
        {
            // Invalid import file — ignore
        }
    }

    private AppConfig LoadOrCreateConfig()
    {
        if (File.Exists(ConfigFilePath))
        {
            try
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null)
                {
                    BackfillDefaults(config);
                    return config;
                }
            }
            catch
            {
                // Fall through to create default
            }
        }

        var defaultConfig = CreateDefaultConfig();
        Save(defaultConfig);
        return defaultConfig;
    }

    /// <summary>
    /// Adds any hotkey entries from the defaults that are missing in the user's config,
    /// so newly added actions are always visible in the settings UI.
    /// </summary>
    private static void BackfillDefaults(AppConfig config)
    {
        var defaults = CreateDefaultConfig();
        foreach (var (key, binding) in defaults.Hotkeys)
            config.Hotkeys.TryAdd(key, binding);
    }

    private static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            Version = 1,
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["move_drag"] = new() { Modifiers = ["Win", "Shift"], Key = "Z", Action = "MoveDrag" },
                ["resize_drag"] = new() { Modifiers = ["Win", "Shift"], Key = "X", Action = "ResizeDrag" },
                ["snap_left"] = new() { Modifiers = ["Win", "Shift"], Key = "Left", Action = "SnapLeft" },
                ["snap_right"] = new() { Modifiers = ["Win", "Shift"], Key = "Right", Action = "SnapRight" },
                ["minimize"] = new() { Modifiers = [], Key = "", Action = "Minimize" },
                ["toggle_minimize"] = new() { Modifiers = ["Win", "Shift"], Key = "Down", Action = "ToggleMinimize" },
                ["restore"] = new() { Modifiers = [], Key = "", Action = "Restore" },
                ["maximize"] = new() { Modifiers = ["Win", "Shift"], Key = "Up", Action = "Maximize" },
                ["opacity_up"] = new() { Modifiers = ["Win", "Shift"], Key = "Oemplus", Action = "OpacityUp" },
                ["opacity_down"] = new() { Modifiers = ["Win", "Shift"], Key = "OemMinus", Action = "OpacityDown" },
                ["zoom_in"] = new() { Modifiers = [], Key = "", Action = "ZoomIn" },
                ["zoom_out"] = new() { Modifiers = [], Key = "", Action = "ZoomOut" },
                ["task_view"] = new() { Modifiers = [], Key = "", Action = "TaskView" },
                ["next_virtual_desktop"] = new() { Modifiers = [], Key = "", Action = "NextVirtualDesktop" },
                ["prev_virtual_desktop"] = new() { Modifiers = [], Key = "", Action = "PrevVirtualDesktop" },
                ["minimize_all"] = new() { Modifiers = [], Key = "", Action = "MinimizeAll" },
                ["resize_window"] = new() { Modifiers = [], Key = "", Action = "ResizeWindow",
                    Parameters = new() { ["Width"] = 1280, ["Height"] = 720 } },
                ["center_window"] = new() { Modifiers = [], Key = "", Action = "CenterWindow",
                    Parameters = new() { ["WidthPercent"] = 60, ["HeightPercent"] = 80 } },
                ["cascade_windows"] = new() { Modifiers = [], Key = "", Action = "CascadeWindows",
                    Parameters = new() { ["CascadeFromRight"] = 0 } },
                ["nudge_up"] = new() { Modifiers = [], Key = "", Action = "NudgeUp",
                    Parameters = new() { ["Distance"] = 10 } },
                ["nudge_down"] = new() { Modifiers = [], Key = "", Action = "NudgeDown",
                    Parameters = new() { ["Distance"] = 10 } },
                ["nudge_left"] = new() { Modifiers = [], Key = "", Action = "NudgeLeft",
                    Parameters = new() { ["Distance"] = 10 } },
                ["nudge_right"] = new() { Modifiers = [], Key = "", Action = "NudgeRight",
                    Parameters = new() { ["Distance"] = 10 } },
                ["nudge_up_large"] = new() { Modifiers = [], Key = "", Action = "NudgeUp",
                    Parameters = new() { ["Distance"] = 50 } },
                ["nudge_down_large"] = new() { Modifiers = [], Key = "", Action = "NudgeDown",
                    Parameters = new() { ["Distance"] = 50 } },
                ["nudge_left_large"] = new() { Modifiers = [], Key = "", Action = "NudgeLeft",
                    Parameters = new() { ["Distance"] = 50 } },
                ["nudge_right_large"] = new() { Modifiers = [], Key = "", Action = "NudgeRight",
                    Parameters = new() { ["Distance"] = 50 } },
            },
            GesturesEnabled = true,
            Gestures = new Dictionary<string, GestureBinding>
            {
                ["swipe_left"] = new() { Type = "SwipeLeft", Modifiers = ["Win", "Shift"], Action = "SnapLeft",
                    Parameters = new() { ["MinVelocityPxPerSec"] = 800, ["MinDisplacementPx"] = 80, ["MaxCrossAxisPx"] = 40, ["TimeWindowMs"] = 300 } },
                ["swipe_right"] = new() { Type = "SwipeRight", Modifiers = ["Win", "Shift"], Action = "SnapRight",
                    Parameters = new() { ["MinVelocityPxPerSec"] = 800, ["MinDisplacementPx"] = 80, ["MaxCrossAxisPx"] = 40, ["TimeWindowMs"] = 300 } },
                ["swipe_up"] = new() { Type = "SwipeUp", Modifiers = ["Win", "Shift"], Action = "Maximize",
                    Parameters = new() { ["MinVelocityPxPerSec"] = 800, ["MinDisplacementPx"] = 80, ["MaxCrossAxisPx"] = 40, ["TimeWindowMs"] = 300 } },
                ["swipe_down"] = new() { Type = "SwipeDown", Modifiers = ["Win", "Shift"], Action = "ToggleMinimize",
                    Parameters = new() { ["MinVelocityPxPerSec"] = 800, ["MinDisplacementPx"] = 80, ["MaxCrossAxisPx"] = 40, ["TimeWindowMs"] = 300 } },
                ["scroll_up"] = new() { Type = "ScrollUp", Modifiers = ["Win", "Shift"], Action = "OpacityUp", Parameters = new() },
                ["scroll_down"] = new() { Type = "ScrollDown", Modifiers = ["Win", "Shift"], Action = "OpacityDown", Parameters = new() },
                ["xbutton1"] = new() { Type = "XButton1", Modifiers = ["Win", "Shift"], Action = "MoveDrag", Parameters = new() },
                ["xbutton2"] = new() { Type = "XButton2", Modifiers = ["Win", "Shift"], Action = "ResizeDrag", Parameters = new() },
                ["middle_click"] = new() { Type = "MiddleClick", Modifiers = ["Win", "Shift"], Action = "Restore", Parameters = new() },
            }
        };
    }

    private void StartWatching()
    {
        if (!Directory.Exists(ConfigDirectory))
            Directory.CreateDirectory(ConfigDirectory);

        _watcher = new FileSystemWatcher(ConfigDirectory, "config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: FileSystemWatcher often fires multiple events per save
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ => Reload(), null, 300, Timeout.Infinite);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
    }

    // Canonical display order with section breaks (null = divider).
    // Keep in sync with DisplaySections below.
    public static readonly List<string?> DisplayOrder =
    [
        "move_drag", "resize_drag",
        null,
        "snap_left", "snap_right",
        null,
        "minimize", "toggle_minimize", "restore", "maximize",
        null,
        "opacity_up", "opacity_down",
        null,
        "zoom_in", "zoom_out",
        null,
        "task_view", "next_virtual_desktop", "prev_virtual_desktop", "minimize_all",
        null,
        "resize_window", "center_window", "cascade_windows",
        null,
        "nudge_up", "nudge_down", "nudge_left", "nudge_right",
        "nudge_up_large", "nudge_down_large", "nudge_left_large", "nudge_right_large",
    ];

    // Named sections for UI accordion display. Must match DisplayOrder grouping.
    public static readonly List<DisplaySection> DisplaySections =
    [
        new("Drag", ["move_drag", "resize_drag"]),
        new("Snap", ["snap_left", "snap_right"]),
        new("Window State", ["minimize", "toggle_minimize", "restore", "maximize"]),
        new("Opacity", ["opacity_up", "opacity_down"]),
        new("Zoom", ["zoom_in", "zoom_out"]),
        new("Virtual Desktops", ["task_view", "next_virtual_desktop", "prev_virtual_desktop", "minimize_all"]),
        new("Size & Position", ["resize_window", "center_window", "cascade_windows"]),
        new("Nudge", ["nudge_up", "nudge_down", "nudge_left", "nudge_right",
            "nudge_up_large", "nudge_down_large", "nudge_left_large", "nudge_right_large"]),
    ];

    // Parsing helpers used by HotkeyManager

    // Virtual key code map — decoupled from any UI framework enum.
    // Keys match the names produced by WinForms Keys.ToString() for config compatibility.
    private static readonly Dictionary<string, uint> VirtualKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Letters
        ["A"] = 0x41, ["B"] = 0x42, ["C"] = 0x43, ["D"] = 0x44, ["E"] = 0x45,
        ["F"] = 0x46, ["G"] = 0x47, ["H"] = 0x48, ["I"] = 0x49, ["J"] = 0x4A,
        ["K"] = 0x4B, ["L"] = 0x4C, ["M"] = 0x4D, ["N"] = 0x4E, ["O"] = 0x4F,
        ["P"] = 0x50, ["Q"] = 0x51, ["R"] = 0x52, ["S"] = 0x53, ["T"] = 0x54,
        ["U"] = 0x55, ["V"] = 0x56, ["W"] = 0x57, ["X"] = 0x58, ["Y"] = 0x59,
        ["Z"] = 0x5A,

        // Numbers (top row)
        ["D0"] = 0x30, ["D1"] = 0x31, ["D2"] = 0x32, ["D3"] = 0x33, ["D4"] = 0x34,
        ["D5"] = 0x35, ["D6"] = 0x36, ["D7"] = 0x37, ["D8"] = 0x38, ["D9"] = 0x39,

        // Function keys
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73,
        ["F5"] = 0x74, ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77,
        ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,

        // Arrow keys
        ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,

        // Common keys
        ["Space"] = 0x20, ["Tab"] = 0x09, ["Escape"] = 0x1B, ["Enter"] = 0x0D,
        ["Return"] = 0x0D, ["Back"] = 0x08, ["Delete"] = 0x2E, ["Insert"] = 0x2D,
        ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["Prior"] = 0x21, ["Next"] = 0x22,

        // OEM keys (WinForms naming convention)
        ["Oemplus"] = 0xBB,    // =+
        ["OemMinus"] = 0xBD,   // -_
        ["OemPeriod"] = 0xBE,  // .>
        ["Oemcomma"] = 0xBC,   // ,<
        ["Oem1"] = 0xBA,       // ;:
        ["Oem2"] = 0xBF,       // /?
        ["Oem3"] = 0xC0,       // `~
        ["Oem4"] = 0xDB,       // [{
        ["Oem5"] = 0xDC,       // \|
        ["Oem6"] = 0xDD,       // ]}
        ["Oem7"] = 0xDE,       // '"
        ["OemBackslash"] = 0xE2, // \| (102-key layout)

        // Numpad
        ["NumPad0"] = 0x60, ["NumPad1"] = 0x61, ["NumPad2"] = 0x62, ["NumPad3"] = 0x63,
        ["NumPad4"] = 0x64, ["NumPad5"] = 0x65, ["NumPad6"] = 0x66, ["NumPad7"] = 0x67,
        ["NumPad8"] = 0x68, ["NumPad9"] = 0x69,
        ["Multiply"] = 0x6A, ["Add"] = 0x6B, ["Subtract"] = 0x6D,
        ["Decimal"] = 0x6E, ["Divide"] = 0x6F,
    };

    // Reverse map for displaying VK code as key name
    private static readonly Dictionary<uint, string> VkToNameMap;

    // Mouse button name map — keyed by config string name, value is a synthetic ID
    // in a reserved range (0x10000+) that won't collide with any Win32 VK code.
    private static readonly Dictionary<string, uint> MouseButtonMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MouseLeft"]        = 0x10001,
        ["MouseRight"]       = 0x10002,
        ["MouseMiddle"]      = 0x10003,
        ["MouseX1"]          = 0x10004,
        ["MouseX2"]          = 0x10005,
        ["MouseScrollUp"]    = 0x10006,
        ["MouseScrollDown"]  = 0x10007,
        ["MouseScrollLeft"]  = 0x10008,
        ["MouseScrollRight"] = 0x10009,
        ["MouseDoubleClick"] = 0x1000A,
        ["MouseTripleClick"] = 0x1000B,
        // Higher XButtons for gaming mice (3-20)
        ["MouseX3"]          = 0x10010,
        ["MouseX4"]          = 0x10011,
        ["MouseX5"]          = 0x10012,
        ["MouseX6"]          = 0x10013,
        ["MouseX7"]          = 0x10014,
        ["MouseX8"]          = 0x10015,
        ["MouseX9"]          = 0x10016,
        ["MouseX10"]         = 0x10017,
        ["MouseX11"]         = 0x10018,
        ["MouseX12"]         = 0x10019,
        ["MouseX13"]         = 0x1001A,
        ["MouseX14"]         = 0x1001B,
        ["MouseX15"]         = 0x1001C,
        ["MouseX16"]         = 0x1001D,
        ["MouseX17"]         = 0x1001E,
        ["MouseX18"]         = 0x1001F,
        ["MouseX19"]         = 0x10020,
        ["MouseX20"]         = 0x10021,
    };

    private static readonly Dictionary<uint, string> MouseIdToNameMap;

    static ConfigManager()
    {
        VkToNameMap = new Dictionary<uint, string>();
        foreach (var (name, vk) in VirtualKeyMap)
        {
            // First mapping wins (prefer canonical name)
            VkToNameMap.TryAdd(vk, name);
        }

        MouseIdToNameMap = new Dictionary<uint, string>();
        foreach (var (name, id) in MouseButtonMap)
            MouseIdToNameMap.TryAdd(id, name);
    }

    public static bool TryParseModifiers(List<string> modifiers, out uint result)
    {
        result = 0;
        foreach (var mod in modifiers)
        {
            switch (mod.ToLowerInvariant())
            {
                case "win": result |= Native.NativeConstants.MOD_WIN; break;
                case "shift": result |= Native.NativeConstants.MOD_SHIFT; break;
                case "ctrl":
                case "control": result |= Native.NativeConstants.MOD_CONTROL; break;
                case "alt": result |= Native.NativeConstants.MOD_ALT; break;
                default: return false;
            }
        }
        return true;
    }

    public static bool TryParseKey(string keyName, out uint vk)
    {
        return VirtualKeyMap.TryGetValue(keyName, out vk);
    }

    public static string VkToKeyName(uint vk)
    {
        if (VkToNameMap.TryGetValue(vk, out var name)) return name;
        if (MouseIdToNameMap.TryGetValue(vk, out var mouseName)) return mouseName;
        return $"0x{vk:X2}";
    }

    /// <summary>Returns true if the key name refers to a mouse button.</summary>
    public static bool IsMouseButton(string? keyName)
        => !string.IsNullOrEmpty(keyName) && MouseButtonMap.ContainsKey(keyName);

    /// <summary>Parses a mouse button name to its synthetic ID.</summary>
    public static bool TryParseMouseButton(string keyName, out uint mouseId)
        => MouseButtonMap.TryGetValue(keyName, out mouseId);

    /// <summary>Converts a synthetic mouse ID back to its config name.</summary>
    public static string MouseIdToName(uint mouseId)
        => MouseIdToNameMap.TryGetValue(mouseId, out var name) ? name : $"Mouse0x{mouseId:X}";

    /// <summary>
    /// Maps a WM_* message type and mouseData (from MSLLHOOKSTRUCT) to a synthetic mouse ID.
    /// Returns 0 if the message is not a recognized mouse button event.
    /// </summary>
    public static uint MouseMessageToId(int messageType, uint mouseData)
    {
        int xButton = (int)(mouseData >> 16);
        return messageType switch
        {
            Native.NativeConstants.WM_LBUTTONDOWN => 0x10001,
            Native.NativeConstants.WM_RBUTTONDOWN => 0x10002,
            Native.NativeConstants.WM_MBUTTONDOWN => 0x10003,
            Native.NativeConstants.WM_XBUTTONDOWN => xButton switch
            {
                1 => 0x10004,  // XBUTTON1
                2 => 0x10005,  // XBUTTON2
                >= 3 and <= 20 => (uint)(0x10010 + (xButton - 3)),
                _ => 0
            },
            _ => 0
        };
    }

    /// <summary>Returns a friendly display name for a mouse button key name.</summary>
    public static string GetFriendlyMouseButtonName(string keyName)
    {
        return keyName switch
        {
            "MouseLeft" => "Left Click",
            "MouseRight" => "Right Click",
            "MouseMiddle" => "Middle Click",
            "MouseX1" => "XButton 1 (Back)",
            "MouseX2" => "XButton 2 (Forward)",
            "MouseScrollUp" => "Scroll Up",
            "MouseScrollDown" => "Scroll Down",
            "MouseScrollLeft" => "Scroll Left",
            "MouseScrollRight" => "Scroll Right",
            "MouseDoubleClick" => "Double Click",
            "MouseTripleClick" => "Triple Click",
            _ when keyName.StartsWith("MouseX", StringComparison.OrdinalIgnoreCase)
                => keyName.Replace("MouseX", "XButton ", StringComparison.OrdinalIgnoreCase),
            _ => keyName
        };
    }

    public static bool TryParseAction(string actionName, out ActionType actionType)
    {
        return Enum.TryParse(actionName, ignoreCase: true, out actionType);
    }

    public static string GetFriendlyActionName(ActionType action)
    {
        return action switch
        {
            ActionType.MoveDrag => "Move (Drag)",
            ActionType.ResizeDrag => "Resize (Drag)",
            ActionType.Minimize => "Minimize",
            ActionType.Maximize => "Maximize / Restore",
            ActionType.Restore => "Restore",
            ActionType.OpacityUp => "Increase Opacity",
            ActionType.OpacityDown => "Decrease Opacity",
            ActionType.SnapLeft => "Snap Left (cycle)",
            ActionType.SnapRight => "Snap Right (cycle)",
            ActionType.ToggleMinimize => "Minimize / Restore",
            ActionType.ZoomIn => "Zoom In",
            ActionType.ZoomOut => "Zoom Out",
            ActionType.TaskView => "Task View",
            ActionType.NextVirtualDesktop => "Next Virtual Desktop",
            ActionType.PrevVirtualDesktop => "Previous Virtual Desktop",
            ActionType.MinimizeAll => "Show Desktop",
            ActionType.ResizeWindow => "Resize Window",
            ActionType.CascadeWindows => "Cascade Windows",
            ActionType.CenterWindow => "Center Window",
            ActionType.NudgeUp => "Nudge Up",
            ActionType.NudgeDown => "Nudge Down",
            ActionType.NudgeLeft => "Nudge Left",
            ActionType.NudgeRight => "Nudge Right",
            _ => action.ToString()
        };
    }

    public static string GetFriendlyConfigKeyName(string configKey, ActionType action)
    {
        return configKey switch
        {
            "nudge_up_large" => "Nudge Up (Large)",
            "nudge_down_large" => "Nudge Down (Large)",
            "nudge_left_large" => "Nudge Left (Large)",
            "nudge_right_large" => "Nudge Right (Large)",
            _ => GetFriendlyActionName(action)
        };
    }

    public static bool TryParseGestureType(string name, out GestureType gestureType)
    {
        return Enum.TryParse(name, ignoreCase: true, out gestureType);
    }
}
