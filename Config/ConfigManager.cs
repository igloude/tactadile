using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinMove.Config;

public sealed class ConfigManager : IDisposable
{
    public static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "win-move");
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
                if (config != null) return config;
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

    private static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            Version = 1,
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["move_drag"] = new() { Modifiers = ["Win", "Shift"], Key = "Z", Action = "MoveDrag" },
                ["resize_drag"] = new() { Modifiers = ["Win", "Shift"], Key = "X", Action = "ResizeDrag" },
                ["minimize"] = new() { Modifiers = ["Win", "Shift"], Key = "Down", Action = "Minimize" },
                ["maximize"] = new() { Modifiers = ["Win", "Shift"], Key = "Up", Action = "Maximize" },
                ["restore"] = new() { Modifiers = ["Win", "Shift"], Key = "R", Action = "Restore" },
                ["opacity_up"] = new() { Modifiers = ["Win", "Shift"], Key = "Oemplus", Action = "OpacityUp" },
                ["opacity_down"] = new() { Modifiers = ["Win", "Shift"], Key = "OemMinus", Action = "OpacityDown" },
                ["snap_left"] = new() { Modifiers = ["Win", "Shift"], Key = "Left", Action = "SnapLeft" },
                ["snap_right"] = new() { Modifiers = ["Win", "Shift"], Key = "Right", Action = "SnapRight" },
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

    // Parsing helpers used by HotkeyManager
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
        vk = 0;
        if (Enum.TryParse<Keys>(keyName, ignoreCase: true, out var key))
        {
            vk = (uint)key;
            return true;
        }
        return false;
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
            _ => action.ToString()
        };
    }
}
