using Tactadile.Config;
using Tactadile.Core;
using Tactadile.Native;

namespace Tactadile.Tests;

public sealed class ModifierSessionTests
{
    // VK codes (matching ModifierSession's private constants)
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_RSHIFT = 0xA1;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_RCONTROL = 0xA3;
    private const uint VK_LMENU = 0xA4;
    private const uint VK_RMENU = 0xA5;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;

    private static AppConfig CreateConfigWithBinding(string key, string action, params string[] modifiers)
    {
        return new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["test"] = new()
                {
                    Modifiers = modifiers.ToList(),
                    Key = key,
                    Action = action
                }
            }
        };
    }

    // BuildLookup tests

    [Fact]
    public void BuildLookup_ValidBinding_CreatesEntry()
    {
        var session = new ModifierSession();
        var config = CreateConfigWithBinding("Z", "MoveDrag", "Win", "Shift");

        session.BuildLookup(config);

        // Verify by triggering the key combo
        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Press Win+Shift (modifiers)
        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);

        // Press Z — should trigger MoveDrag
        session.OnKeyStateChanged(0x5A, true); // VK_Z

        Assert.Single(triggered);
        Assert.Equal(ActionType.MoveDrag, triggered[0]);
    }

    [Fact]
    public void BuildLookup_EmptyKey_WithModifiers_RegistersAsModifierOnly()
    {
        var session = new ModifierSession();
        var config = new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["test"] = new()
                {
                    Modifiers = ["Win", "Shift"],
                    Key = "",
                    Action = "Minimize"
                }
            }
        };

        session.BuildLookup(config);

        // Empty key + non-zero modifiers becomes a modifier-only hotkey
        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);

        Assert.Single(triggered);
        Assert.Equal(ActionType.Minimize, triggered[0]);
    }

    [Fact]
    public void BuildLookup_EmptyKey_NoModifiers_SkipsEntry()
    {
        var session = new ModifierSession();
        var config = new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["test"] = new()
                {
                    Modifiers = [],
                    Key = "",
                    Action = "Minimize"
                }
            }
        };

        session.BuildLookup(config);

        // No key and no modifiers — should not trigger anything
        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        session.OnKeyStateChanged(VK_LWIN, true);
        Assert.Empty(triggered);
    }

    [Fact]
    public void BuildLookup_InvalidAction_SkipsEntry()
    {
        var session = new ModifierSession();
        var config = CreateConfigWithBinding("Z", "InvalidAction", "Win", "Shift");

        session.BuildLookup(config);

        // Should not crash
        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);
        session.OnKeyStateChanged(0x5A, true);

        Assert.Empty(triggered);
    }

    [Fact]
    public void BuildLookup_MultipleBindings_AllRegistered()
    {
        var session = new ModifierSession();
        var config = new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["move"] = new() { Modifiers = ["Win", "Shift"], Key = "Z", Action = "MoveDrag" },
                ["resize"] = new() { Modifiers = ["Win", "Shift"], Key = "X", Action = "ResizeDrag" },
                ["snap"] = new() { Modifiers = ["Win", "Shift"], Key = "Left", Action = "SnapLeft" }
            }
        };

        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Press Win+Shift+Z
        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);
        session.OnKeyStateChanged(0x5A, true); // Z
        Assert.Equal(ActionType.MoveDrag, triggered.Last());

        // Release Z, press X (key switch)
        session.OnKeyStateChanged(0x5A, false); // Z up
        session.OnKeyStateChanged(0x58, true);  // X down
        Assert.Equal(ActionType.ResizeDrag, triggered.Last());
    }

    // Key switch detection tests

    [Fact]
    public void KeySwitch_WhileModifiersHeld_FiresAction()
    {
        var session = new ModifierSession();
        var config = new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["move"] = new() { Modifiers = ["Win", "Shift"], Key = "Z", Action = "MoveDrag" },
                ["resize"] = new() { Modifiers = ["Win", "Shift"], Key = "X", Action = "ResizeDrag" }
            }
        };
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Hold modifiers
        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);

        // Press Z (seeds the session)
        session.OnKeyStateChanged(0x5A, true);
        Assert.Single(triggered);
        Assert.Equal(ActionType.MoveDrag, triggered[0]);

        // Release Z
        session.OnKeyStateChanged(0x5A, false);

        // Press X (key switch)
        session.OnKeyStateChanged(0x58, true);
        Assert.Equal(2, triggered.Count);
        Assert.Equal(ActionType.ResizeDrag, triggered[1]);
    }

    [Fact]
    public void ReleasingAllModifiers_EndsSession()
    {
        var session = new ModifierSession();
        var config = CreateConfigWithBinding("Z", "MoveDrag", "Win", "Shift");
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Start session
        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);
        session.OnKeyStateChanged(0x5A, true);
        Assert.Single(triggered);

        // Release everything
        session.OnKeyStateChanged(0x5A, false);
        session.OnKeyStateChanged(VK_LSHIFT, false);
        session.OnKeyStateChanged(VK_LWIN, false);

        // Press again — should trigger again since it's a new session
        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);
        session.OnKeyStateChanged(0x5A, true);
        Assert.Equal(2, triggered.Count);
    }

    // ConsumeIfFired tests

    [Fact]
    public void ConsumeIfFired_AfterTrigger_ReturnsTrue()
    {
        var session = new ModifierSession();
        var config = CreateConfigWithBinding("Z", "MoveDrag", "Win", "Shift");
        session.BuildLookup(config);

        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);
        session.OnKeyStateChanged(0x5A, true);

        Assert.True(session.ConsumeIfFired());
    }

    [Fact]
    public void ConsumeIfFired_SecondCall_ReturnsFalse()
    {
        var session = new ModifierSession();
        var config = CreateConfigWithBinding("Z", "MoveDrag", "Win", "Shift");
        session.BuildLookup(config);

        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);
        session.OnKeyStateChanged(0x5A, true);

        Assert.True(session.ConsumeIfFired());
        Assert.False(session.ConsumeIfFired()); // Already consumed
    }

    [Fact]
    public void ConsumeIfFired_WithoutTrigger_ReturnsFalse()
    {
        var session = new ModifierSession();
        Assert.False(session.ConsumeIfFired());
    }

    // ModifierFlagsChanged event tests

    [Fact]
    public void ModifierFlagsChanged_FiresOnModifierPress()
    {
        var session = new ModifierSession();
        session.BuildLookup(new AppConfig());

        var flags = new List<uint>();
        session.ModifierFlagsChanged += f => flags.Add(f);

        session.OnKeyStateChanged(VK_LWIN, true);
        Assert.Single(flags);
        Assert.Equal(NativeConstants.MOD_WIN, flags[0]);
    }

    [Fact]
    public void ModifierFlagsChanged_FiresOnModifierRelease()
    {
        var session = new ModifierSession();
        session.BuildLookup(new AppConfig());

        var flags = new List<uint>();
        session.ModifierFlagsChanged += f => flags.Add(f);

        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LWIN, false);

        Assert.Equal(2, flags.Count);
        Assert.Equal(NativeConstants.MOD_WIN, flags[0]);
        Assert.Equal(0u, flags[1]); // All modifiers released
    }

    [Fact]
    public void ModifierFlagsChanged_CombinesMultipleModifiers()
    {
        var session = new ModifierSession();
        session.BuildLookup(new AppConfig());

        var flags = new List<uint>();
        session.ModifierFlagsChanged += f => flags.Add(f);

        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(VK_LSHIFT, true);

        Assert.Equal(2, flags.Count);
        Assert.Equal(NativeConstants.MOD_WIN, flags[0]);
        Assert.Equal(NativeConstants.MOD_WIN | NativeConstants.MOD_SHIFT, flags[1]);
    }

    // Left/Right variant handling

    [Fact]
    public void LeftAndRightVariants_BothMapToSameModFlag()
    {
        var session = new ModifierSession();
        session.BuildLookup(new AppConfig());

        var flags = new List<uint>();
        session.ModifierFlagsChanged += f => flags.Add(f);

        // Press left Win
        session.OnKeyStateChanged(VK_LWIN, true);
        Assert.Equal(NativeConstants.MOD_WIN, flags.Last());

        session.OnKeyStateChanged(VK_LWIN, false);

        // Press right Win
        session.OnKeyStateChanged(VK_RWIN, true);
        Assert.Equal(NativeConstants.MOD_WIN, flags.Last());
    }

    [Fact]
    public void ReleasingOneVariant_RemovesBothVariants()
    {
        var session = new ModifierSession();
        session.BuildLookup(new AppConfig());

        var flags = new List<uint>();
        session.ModifierFlagsChanged += f => flags.Add(f);

        session.OnKeyStateChanged(VK_LSHIFT, true);
        Assert.Equal(NativeConstants.MOD_SHIFT, flags.Last());

        // Release left shift should clear the modifier even though right isn't pressed
        session.OnKeyStateChanged(VK_LSHIFT, false);
        Assert.Equal(0u, flags.Last());
    }

    // OnHotkeyFired tests

    [Fact]
    public void OnHotkeyFired_SeedsSession_EnablesKeySwitch()
    {
        var session = new ModifierSession();
        var config = new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["move"] = new() { Modifiers = ["Win", "Shift"], Key = "Z", Action = "MoveDrag" },
                ["resize"] = new() { Modifiers = ["Win", "Shift"], Key = "X", Action = "ResizeDrag" }
            }
        };
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Simulate WM_HOTKEY seeding
        session.OnHotkeyFired(NativeConstants.MOD_WIN | NativeConstants.MOD_SHIFT, 0x5A); // Win+Shift+Z

        // Now simulate key switch via hook
        session.OnKeyStateChanged(0x5A, false); // Release Z
        session.OnKeyStateChanged(0x58, true);  // Press X

        Assert.Single(triggered);
        Assert.Equal(ActionType.ResizeDrag, triggered[0]);
    }

    // Non-modifier key without modifiers held should not trigger

    [Fact]
    public void PrimaryKey_WithoutModifiers_DoesNotTrigger()
    {
        var session = new ModifierSession();
        var config = CreateConfigWithBinding("Z", "MoveDrag", "Win", "Shift");
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Press Z without modifiers
        session.OnKeyStateChanged(0x5A, true);
        Assert.Empty(triggered);
    }

    // Wrong modifier combo should not trigger

    [Fact]
    public void WrongModifiers_DoNotTrigger()
    {
        var session = new ModifierSession();
        var config = CreateConfigWithBinding("Z", "MoveDrag", "Win", "Shift");
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Press Ctrl+Z instead of Win+Shift+Z
        session.OnKeyStateChanged(VK_LCONTROL, true);
        session.OnKeyStateChanged(0x5A, true);
        Assert.Empty(triggered);
    }

    // Guard rail: key alone must never trigger a modifier+key combo

    [Fact]
    public void KeyAlone_WithWinKeyComboConfigured_DoesNotTrigger()
    {
        var session = new ModifierSession();
        var config = CreateConfigWithBinding("T", "TaskView", "Win");
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Press T alone — must NOT trigger TaskView
        session.OnKeyStateChanged(0x54, true); // VK_T
        session.OnKeyStateChanged(0x54, false);
        Assert.Empty(triggered);
    }

    [Fact]
    public void KeyAlone_WithMultipleModifierCombos_NeverTriggers()
    {
        var session = new ModifierSession();
        var config = new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["a"] = new() { Modifiers = ["Win"], Key = "T", Action = "TaskView" },
                ["b"] = new() { Modifiers = ["Win", "Shift"], Key = "T", Action = "Minimize" },
                ["c"] = new() { Modifiers = ["Ctrl", "Alt"], Key = "T", Action = "Maximize" }
            }
        };
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Press T alone — must NOT trigger any of the three bindings
        session.OnKeyStateChanged(0x54, true);
        session.OnKeyStateChanged(0x54, false);
        Assert.Empty(triggered);
    }

    [Fact]
    public void FullPressReleaseCycle_LeavesCleanState()
    {
        var session = new ModifierSession();
        var config = CreateConfigWithBinding("T", "TaskView", "Win");
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Full Win+T press-release cycle
        session.OnKeyStateChanged(VK_LWIN, true);
        session.OnKeyStateChanged(0x54, true);
        Assert.Single(triggered);

        session.OnKeyStateChanged(0x54, false);
        session.OnKeyStateChanged(VK_LWIN, false);

        triggered.Clear();

        // Now press T alone — must NOT trigger
        session.OnKeyStateChanged(0x54, true);
        session.OnKeyStateChanged(0x54, false);
        Assert.Empty(triggered);
    }

    [Fact]
    public void MouseButtonBinding_WithModifiers_DoesNotRegisterAsModifierOnly()
    {
        var session = new ModifierSession();
        var config = new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["mouse_test"] = new()
                {
                    Modifiers = ["Ctrl"],
                    Key = "MouseX1",
                    Action = "TaskView"
                }
            }
        };
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Press Ctrl alone — must NOT trigger TaskView
        session.OnKeyStateChanged(VK_LCONTROL, true);
        Assert.Empty(triggered);

        session.OnKeyStateChanged(VK_LCONTROL, false);
        Assert.Empty(triggered);
    }

    [Fact]
    public void MultipleKeys_WithoutModifiers_NeverTrigger()
    {
        var session = new ModifierSession();
        var config = new AppConfig
        {
            Hotkeys = new Dictionary<string, HotkeyBinding>
            {
                ["a"] = new() { Modifiers = ["Win"], Key = "A", Action = "MoveDrag" },
                ["b"] = new() { Modifiers = ["Win"], Key = "B", Action = "ResizeDrag" },
                ["c"] = new() { Modifiers = ["Win"], Key = "C", Action = "Minimize" }
            }
        };
        session.BuildLookup(config);

        var triggered = new List<ActionType>();
        session.ActionTriggered += a => triggered.Add(a);

        // Type "abc" without any modifiers
        session.OnKeyStateChanged(0x41, true);  // A
        session.OnKeyStateChanged(0x41, false);
        session.OnKeyStateChanged(0x42, true);  // B
        session.OnKeyStateChanged(0x42, false);
        session.OnKeyStateChanged(0x43, true);  // C
        session.OnKeyStateChanged(0x43, false);

        Assert.Empty(triggered);
    }
}
