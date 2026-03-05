using System.Text.Json;
using System.Text.Json.Serialization;
using Tactadile.Config;

namespace Tactadile.Tests;

public sealed class ConfigSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void AppConfig_RoundTrips_ThroughJson()
    {
        var config = new AppConfig
        {
            Version = 1,
            EdgeSnappingEnabled = true,
            OverrideWindowsKeybinds = false,
            GesturesEnabled = true,
            AutoPositionEnabled = true,
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["move_drag"] = new()
                {
                    Modifiers = ["Win", "Shift"],
                    Key = "Z",
                    Action = "MoveDrag"
                }
            },
            Gestures = new Dictionary<string, GestureBinding>
            {
                ["swipe_left"] = new()
                {
                    Type = "SwipeLeft",
                    Modifiers = ["Win", "Shift"],
                    Action = "SnapLeft",
                    Parameters = new() { ["MinVelocityPxPerSec"] = 800 }
                }
            },
            LaunchRules = new List<LaunchRule>
            {
                new()
                {
                    AppName = "Notepad",
                    ExecutablePath = @"C:\Windows\notepad.exe",
                    ProcessName = "notepad",
                    MonitorIndex = 0,
                    Zone = "LeftHalf",
                    Enabled = true,
                    DelayMs = 150
                }
            }
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;

        Assert.Equal(config.Version, deserialized.Version);
        Assert.Equal(config.EdgeSnappingEnabled, deserialized.EdgeSnappingEnabled);
        Assert.Equal(config.OverrideWindowsKeybinds, deserialized.OverrideWindowsKeybinds);
        Assert.Equal(config.GesturesEnabled, deserialized.GesturesEnabled);
        Assert.Equal(config.AutoPositionEnabled, deserialized.AutoPositionEnabled);
    }

    [Fact]
    public void HotkeyBinding_RoundTrips()
    {
        var binding = new HotkeyBinding
        {
            Modifiers = ["Win", "Ctrl", "Alt"],
            Key = "F5",
            Action = "TaskView",
            Parameters = new() { ["Width"] = 1280, ["Height"] = 720 }
        };

        var json = JsonSerializer.Serialize(binding, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<HotkeyBinding>(json, JsonOptions)!;

        Assert.Equal(binding.Modifiers, deserialized.Modifiers);
        Assert.Equal(binding.Key, deserialized.Key);
        Assert.Equal(binding.Action, deserialized.Action);
        Assert.Equal(binding.Parameters["Width"], deserialized.Parameters["Width"]);
        Assert.Equal(binding.Parameters["Height"], deserialized.Parameters["Height"]);
    }

    [Fact]
    public void GestureBinding_RoundTrips()
    {
        var binding = new GestureBinding
        {
            Type = "SwipeUp",
            Modifiers = ["Win", "Shift"],
            Action = "Maximize",
            Parameters = new()
            {
                ["MinVelocityPxPerSec"] = 800,
                ["MinDisplacementPx"] = 80,
                ["MaxCrossAxisPx"] = 40,
                ["TimeWindowMs"] = 300
            }
        };

        var json = JsonSerializer.Serialize(binding, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<GestureBinding>(json, JsonOptions)!;

        Assert.Equal(binding.Type, deserialized.Type);
        Assert.Equal(binding.Modifiers, deserialized.Modifiers);
        Assert.Equal(binding.Action, deserialized.Action);
        Assert.Equal(4, deserialized.Parameters.Count);
        Assert.Equal(800, deserialized.Parameters["MinVelocityPxPerSec"]);
    }

    [Fact]
    public void LaunchRule_RoundTrips()
    {
        var rule = new LaunchRule
        {
            Id = "abc123",
            AppName = "VS Code",
            ExecutablePath = @"C:\Program Files\Microsoft VS Code\Code.exe",
            ProcessName = "Code",
            MonitorIndex = 1,
            Zone = "RightTwoThirds",
            Enabled = true,
            ApplyOnlyToFirstWindow = true,
            DelayMs = 200
        };

        var json = JsonSerializer.Serialize(rule, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<LaunchRule>(json, JsonOptions)!;

        Assert.Equal(rule.Id, deserialized.Id);
        Assert.Equal(rule.AppName, deserialized.AppName);
        Assert.Equal(rule.ExecutablePath, deserialized.ExecutablePath);
        Assert.Equal(rule.ProcessName, deserialized.ProcessName);
        Assert.Equal(rule.MonitorIndex, deserialized.MonitorIndex);
        Assert.Equal(rule.Zone, deserialized.Zone);
        Assert.Equal(rule.Enabled, deserialized.Enabled);
        Assert.Equal(rule.ApplyOnlyToFirstWindow, deserialized.ApplyOnlyToFirstWindow);
        Assert.Equal(rule.DelayMs, deserialized.DelayMs);
    }

    [Fact]
    public void AppConfig_Defaults_AreCorrect()
    {
        var config = new AppConfig();

        Assert.Equal(1, config.Version);
        Assert.True(config.EdgeSnappingEnabled);
        Assert.True(config.OverrideWindowsKeybinds);
        Assert.True(config.GesturesEnabled);
        Assert.False(config.AutoPositionEnabled);
        Assert.False(config.WinKeyDelayEnabled);
        Assert.Equal(250, config.WinKeyDelayMs);
        Assert.False(config.BlockCopilot);
        Assert.Empty(config.Hotkeys);
        Assert.Empty(config.Gestures);
        Assert.Empty(config.LaunchRules);
    }

    [Fact]
    public void HotkeyBinding_Defaults_AreCorrect()
    {
        var binding = new HotkeyBinding();

        Assert.Empty(binding.Modifiers);
        Assert.Equal(string.Empty, binding.Key);
        Assert.Equal(string.Empty, binding.Action);
        Assert.Empty(binding.Parameters);
    }

    [Fact]
    public void LaunchRule_Defaults_AreCorrect()
    {
        var rule = new LaunchRule();

        Assert.False(string.IsNullOrEmpty(rule.Id)); // Auto-generated GUID
        Assert.Equal(string.Empty, rule.AppName);
        Assert.Equal(string.Empty, rule.ExecutablePath);
        Assert.Equal(string.Empty, rule.ProcessName);
        Assert.Equal(0, rule.MonitorIndex);
        Assert.Equal("LeftHalf", rule.Zone);
        Assert.True(rule.Enabled);
        Assert.False(rule.ApplyOnlyToFirstWindow);
        Assert.Equal(150, rule.DelayMs);
    }

    [Fact]
    public void Config_WithEmptyBindings_DeserializesCleanly()
    {
        var json = """
        {
            "Version": 1,
            "Hotkeys": {},
            "Gestures": {},
            "LaunchRules": []
        }
        """;

        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;
        Assert.Equal(1, config.Version);
        Assert.Empty(config.Hotkeys);
        Assert.Empty(config.Gestures);
        Assert.Empty(config.LaunchRules);
    }

    [Fact]
    public void Config_CaseInsensitive_DeserializesCorrectly()
    {
        var json = """
        {
            "version": 1,
            "edgesnappingenabled": false,
            "gesturesenabled": false
        }
        """;

        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;
        Assert.Equal(1, config.Version);
        Assert.False(config.EdgeSnappingEnabled);
        Assert.False(config.GesturesEnabled);
    }

    [Fact]
    public void Config_ExtraProperties_AreIgnoredGracefully()
    {
        var json = """
        {
            "Version": 1,
            "SomeNewField": "value",
            "AnotherField": 42,
            "Hotkeys": {}
        }
        """;

        // Should not throw
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;
        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void HotkeyBinding_WithParameters_PreservesValues()
    {
        var json = """
        {
            "Modifiers": ["Win", "Shift"],
            "Key": "R",
            "Action": "ResizeWindow",
            "Parameters": {
                "Width": 1280,
                "Height": 720
            }
        }
        """;

        var binding = JsonSerializer.Deserialize<HotkeyBinding>(json, JsonOptions)!;
        Assert.Equal(1280, binding.Parameters["Width"]);
        Assert.Equal(720, binding.Parameters["Height"]);
    }

    [Fact]
    public void FullConfig_SerializeDeserialize_PreservesAllData()
    {
        var config = new AppConfig
        {
            Version = 1,
            EdgeSnappingEnabled = false,
            OverrideWindowsKeybinds = true,
            GesturesEnabled = true,
            AutoPositionEnabled = true,
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["move_drag"] = new() { Modifiers = ["Win", "Shift"], Key = "Z", Action = "MoveDrag" },
                ["snap_left"] = new() { Modifiers = ["Win", "Shift"], Key = "Left", Action = "SnapLeft" },
                ["nudge_up"] = new()
                {
                    Modifiers = ["Win", "Ctrl"],
                    Key = "Up",
                    Action = "NudgeUp",
                    Parameters = new() { ["Distance"] = 10 }
                }
            },
            Gestures = new Dictionary<string, GestureBinding>
            {
                ["scroll_up"] = new()
                {
                    Type = "ScrollUp",
                    Modifiers = ["Win", "Shift"],
                    Action = "OpacityUp",
                    Parameters = new()
                }
            },
            LaunchRules = new List<LaunchRule>
            {
                new()
                {
                    Id = "rule1",
                    AppName = "Test",
                    ProcessName = "test",
                    MonitorIndex = 0,
                    Zone = "Centered",
                    Enabled = true
                }
            }
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var result = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;

        Assert.Equal(config.Hotkeys.Count, result.Hotkeys.Count);
        Assert.Equal(config.Gestures.Count, result.Gestures.Count);
        Assert.Equal(config.LaunchRules.Count, result.LaunchRules.Count);
        Assert.Equal("MoveDrag", result.Hotkeys["move_drag"].Action);
        Assert.Equal("ScrollUp", result.Gestures["scroll_up"].Type);
        Assert.Equal("Centered", result.LaunchRules[0].Zone);
    }

    [Fact]
    public void WinKeyDelay_RoundTrips()
    {
        var config = new AppConfig
        {
            WinKeyDelayEnabled = true,
            WinKeyDelayMs = 500
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;

        Assert.True(deserialized.WinKeyDelayEnabled);
        Assert.Equal(500, deserialized.WinKeyDelayMs);
    }

    [Fact]
    public void BlockCopilot_RoundTrips()
    {
        var config = new AppConfig
        {
            BlockCopilot = true
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;

        Assert.True(deserialized.BlockCopilot);
    }

    [Fact]
    public void HotkeyBinding_WithMouseKey_RoundTrips()
    {
        var binding = new HotkeyBinding
        {
            Modifiers = ["Win", "Shift"],
            Key = "MouseX1",
            Action = "MoveDrag",
            Parameters = new() { ["Distance"] = 10 }
        };

        var json = JsonSerializer.Serialize(binding, JsonOptions);
        var result = JsonSerializer.Deserialize<HotkeyBinding>(json, JsonOptions)!;

        Assert.Equal("MouseX1", result.Key);
        Assert.Equal(["Win", "Shift"], result.Modifiers);
        Assert.Equal("MoveDrag", result.Action);
        Assert.Equal(10, result.Parameters["Distance"]);
    }

    [Fact]
    public void AppConfig_WithMouseHotkeys_RoundTrips()
    {
        var config = new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["mouse_move"] = new() { Modifiers = ["Ctrl"], Key = "MouseLeft", Action = "MoveDrag" },
                ["bare_xbutton"] = new() { Modifiers = [], Key = "MouseX1", Action = "Minimize" },
                ["scroll_zoom"] = new() { Modifiers = ["Ctrl"], Key = "MouseScrollUp", Action = "ZoomIn" },
            }
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var result = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;

        Assert.Equal(3, result.Hotkeys.Count);
        Assert.Equal("MouseLeft", result.Hotkeys["mouse_move"].Key);
        Assert.Equal("MouseX1", result.Hotkeys["bare_xbutton"].Key);
        Assert.Equal("MouseScrollUp", result.Hotkeys["scroll_zoom"].Key);
    }

    [Fact]
    public void LaunchRule_NewInstance_HasUniqueId()
    {
        var rule1 = new LaunchRule();
        var rule2 = new LaunchRule();

        Assert.NotEqual(rule1.Id, rule2.Id);
        Assert.Equal(32, rule1.Id.Length); // GUID "N" format is 32 hex chars
    }
}
