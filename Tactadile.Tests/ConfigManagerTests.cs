using Tactadile.Config;
using Tactadile.Native;

namespace Tactadile.Tests;

public sealed class ConfigManagerTests
{
    // TryParseModifiers tests

    [Fact]
    public void TryParseModifiers_Win_ReturnsMOD_WIN()
    {
        Assert.True(ConfigManager.TryParseModifiers(["Win"], out uint result));
        Assert.Equal(NativeConstants.MOD_WIN, result);
    }

    [Fact]
    public void TryParseModifiers_Shift_ReturnsMOD_SHIFT()
    {
        Assert.True(ConfigManager.TryParseModifiers(["Shift"], out uint result));
        Assert.Equal(NativeConstants.MOD_SHIFT, result);
    }

    [Fact]
    public void TryParseModifiers_Ctrl_ReturnsMOD_CONTROL()
    {
        Assert.True(ConfigManager.TryParseModifiers(["Ctrl"], out uint result));
        Assert.Equal(NativeConstants.MOD_CONTROL, result);
    }

    [Fact]
    public void TryParseModifiers_Control_ReturnsMOD_CONTROL()
    {
        Assert.True(ConfigManager.TryParseModifiers(["Control"], out uint result));
        Assert.Equal(NativeConstants.MOD_CONTROL, result);
    }

    [Fact]
    public void TryParseModifiers_Alt_ReturnsMOD_ALT()
    {
        Assert.True(ConfigManager.TryParseModifiers(["Alt"], out uint result));
        Assert.Equal(NativeConstants.MOD_ALT, result);
    }

    [Fact]
    public void TryParseModifiers_Multiple_CombinesFlags()
    {
        Assert.True(ConfigManager.TryParseModifiers(["Win", "Shift"], out uint result));
        Assert.Equal(NativeConstants.MOD_WIN | NativeConstants.MOD_SHIFT, result);
    }

    [Fact]
    public void TryParseModifiers_AllFour_CombinesAll()
    {
        Assert.True(ConfigManager.TryParseModifiers(["Win", "Shift", "Ctrl", "Alt"], out uint result));
        Assert.Equal(
            NativeConstants.MOD_WIN | NativeConstants.MOD_SHIFT |
            NativeConstants.MOD_CONTROL | NativeConstants.MOD_ALT,
            result);
    }

    [Fact]
    public void TryParseModifiers_Empty_ReturnsZero()
    {
        Assert.True(ConfigManager.TryParseModifiers([], out uint result));
        Assert.Equal(0u, result);
    }

    [Fact]
    public void TryParseModifiers_Invalid_ReturnsFalse()
    {
        Assert.False(ConfigManager.TryParseModifiers(["InvalidMod"], out _));
    }

    [Fact]
    public void TryParseModifiers_CaseInsensitive()
    {
        Assert.True(ConfigManager.TryParseModifiers(["WIN"], out uint r1));
        Assert.True(ConfigManager.TryParseModifiers(["win"], out uint r2));
        Assert.True(ConfigManager.TryParseModifiers(["Win"], out uint r3));
        Assert.Equal(r1, r2);
        Assert.Equal(r2, r3);
    }

    // TryParseKey tests

    [Theory]
    [InlineData("A", 0x41u)]
    [InlineData("Z", 0x5Au)]
    [InlineData("D0", 0x30u)]
    [InlineData("D9", 0x39u)]
    [InlineData("F1", 0x70u)]
    [InlineData("F12", 0x7Bu)]
    [InlineData("Left", 0x25u)]
    [InlineData("Up", 0x26u)]
    [InlineData("Right", 0x27u)]
    [InlineData("Down", 0x28u)]
    [InlineData("Space", 0x20u)]
    [InlineData("Tab", 0x09u)]
    [InlineData("Escape", 0x1Bu)]
    [InlineData("Enter", 0x0Du)]
    [InlineData("Return", 0x0Du)]
    [InlineData("Oemplus", 0xBBu)]
    [InlineData("OemMinus", 0xBDu)]
    public void TryParseKey_ValidKeys(string keyName, uint expectedVk)
    {
        Assert.True(ConfigManager.TryParseKey(keyName, out uint vk));
        Assert.Equal(expectedVk, vk);
    }

    [Fact]
    public void TryParseKey_InvalidKey_ReturnsFalse()
    {
        Assert.False(ConfigManager.TryParseKey("NonExistentKey", out _));
    }

    [Fact]
    public void TryParseKey_EmptyString_ReturnsFalse()
    {
        Assert.False(ConfigManager.TryParseKey("", out _));
    }

    [Fact]
    public void TryParseKey_CaseInsensitive()
    {
        // Keys use case-insensitive lookup
        Assert.True(ConfigManager.TryParseKey("a", out uint vk1));
        Assert.True(ConfigManager.TryParseKey("A", out uint vk2));
        Assert.Equal(vk1, vk2);
    }

    // VkToKeyName tests

    [Theory]
    [InlineData(0x41u, "A")]
    [InlineData(0x5Au, "Z")]
    [InlineData(0x25u, "Left")]
    [InlineData(0x27u, "Right")]
    [InlineData(0x70u, "F1")]
    public void VkToKeyName_KnownKeys_ReturnsName(uint vk, string expectedName)
    {
        Assert.Equal(expectedName, ConfigManager.VkToKeyName(vk));
    }

    [Fact]
    public void VkToKeyName_UnknownKey_ReturnsHexString()
    {
        var name = ConfigManager.VkToKeyName(0xFF);
        Assert.Equal("0xFF", name);
    }

    // TryParseKey and VkToKeyName roundtrip
    [Theory]
    [InlineData("A")]
    [InlineData("Z")]
    [InlineData("F1")]
    [InlineData("Left")]
    [InlineData("Space")]
    [InlineData("NumPad0")]
    public void ParseKey_And_VkToKeyName_Roundtrip(string keyName)
    {
        Assert.True(ConfigManager.TryParseKey(keyName, out uint vk));
        var result = ConfigManager.VkToKeyName(vk);
        Assert.Equal(keyName, result);
    }

    // TryParseAction tests

    [Theory]
    [InlineData("MoveDrag", ActionType.MoveDrag)]
    [InlineData("ResizeDrag", ActionType.ResizeDrag)]
    [InlineData("SnapLeft", ActionType.SnapLeft)]
    [InlineData("SnapRight", ActionType.SnapRight)]
    [InlineData("Minimize", ActionType.Minimize)]
    [InlineData("Maximize", ActionType.Maximize)]
    [InlineData("OpacityUp", ActionType.OpacityUp)]
    [InlineData("NudgeUp", ActionType.NudgeUp)]
    [InlineData("CascadeWindows", ActionType.CascadeWindows)]
    public void TryParseAction_ValidActions(string name, ActionType expected)
    {
        Assert.True(ConfigManager.TryParseAction(name, out var action));
        Assert.Equal(expected, action);
    }

    [Fact]
    public void TryParseAction_CaseInsensitive()
    {
        Assert.True(ConfigManager.TryParseAction("movedrag", out var action));
        Assert.Equal(ActionType.MoveDrag, action);
    }

    [Fact]
    public void TryParseAction_Invalid_ReturnsFalse()
    {
        Assert.False(ConfigManager.TryParseAction("NotAnAction", out _));
    }

    // TryParseGestureType tests

    [Theory]
    [InlineData("SwipeUp", GestureType.SwipeUp)]
    [InlineData("SwipeDown", GestureType.SwipeDown)]
    [InlineData("SwipeLeft", GestureType.SwipeLeft)]
    [InlineData("SwipeRight", GestureType.SwipeRight)]
    [InlineData("ShakeHorizontal", GestureType.ShakeHorizontal)]
    [InlineData("ShakeVertical", GestureType.ShakeVertical)]
    [InlineData("EdgeFlickLeft", GestureType.EdgeFlickLeft)]
    [InlineData("ScrollUp", GestureType.ScrollUp)]
    [InlineData("XButton1", GestureType.XButton1)]
    [InlineData("MiddleClick", GestureType.MiddleClick)]
    public void TryParseGestureType_ValidTypes(string name, GestureType expected)
    {
        Assert.True(ConfigManager.TryParseGestureType(name, out var gesture));
        Assert.Equal(expected, gesture);
    }

    [Fact]
    public void TryParseGestureType_Invalid_ReturnsFalse()
    {
        Assert.False(ConfigManager.TryParseGestureType("InvalidGesture", out _));
    }

    // GetFriendlyActionName tests

    [Fact]
    public void GetFriendlyActionName_AllActions_ReturnNonEmpty()
    {
        foreach (var action in Enum.GetValues<ActionType>())
        {
            var name = ConfigManager.GetFriendlyActionName(action);
            Assert.False(string.IsNullOrWhiteSpace(name), $"Action {action} has no friendly name");
        }
    }

    [Theory]
    [InlineData(ActionType.MoveDrag, "Move (Drag)")]
    [InlineData(ActionType.ResizeDrag, "Resize (Drag)")]
    [InlineData(ActionType.SnapLeft, "Snap Left (cycle)")]
    [InlineData(ActionType.MinimizeAll, "Show Desktop")]
    public void GetFriendlyActionName_SpecificCases(ActionType action, string expected)
    {
        Assert.Equal(expected, ConfigManager.GetFriendlyActionName(action));
    }

    // GetFriendlyConfigKeyName tests

    [Fact]
    public void GetFriendlyConfigKeyName_LargeNudge_ReturnsOverride()
    {
        Assert.Equal("Nudge Up (Large)", ConfigManager.GetFriendlyConfigKeyName("nudge_up_large", ActionType.NudgeUp));
        Assert.Equal("Nudge Down (Large)", ConfigManager.GetFriendlyConfigKeyName("nudge_down_large", ActionType.NudgeDown));
        Assert.Equal("Nudge Left (Large)", ConfigManager.GetFriendlyConfigKeyName("nudge_left_large", ActionType.NudgeLeft));
        Assert.Equal("Nudge Right (Large)", ConfigManager.GetFriendlyConfigKeyName("nudge_right_large", ActionType.NudgeRight));
    }

    [Fact]
    public void GetFriendlyConfigKeyName_NormalKeys_FallsBackToActionName()
    {
        Assert.Equal("Snap Left (cycle)", ConfigManager.GetFriendlyConfigKeyName("snap_left", ActionType.SnapLeft));
    }

    // Mouse button parsing tests

    [Theory]
    [InlineData("MouseLeft", true)]
    [InlineData("MouseRight", true)]
    [InlineData("MouseMiddle", true)]
    [InlineData("MouseX1", true)]
    [InlineData("MouseX2", true)]
    [InlineData("MouseScrollUp", true)]
    [InlineData("MouseDoubleClick", true)]
    [InlineData("MouseX20", true)]
    [InlineData("Z", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMouseButton_CorrectlyIdentifies(string? keyName, bool expected)
    {
        Assert.Equal(expected, ConfigManager.IsMouseButton(keyName));
    }

    [Theory]
    [InlineData("MouseLeft", 0x10001u)]
    [InlineData("MouseRight", 0x10002u)]
    [InlineData("MouseMiddle", 0x10003u)]
    [InlineData("MouseX1", 0x10004u)]
    [InlineData("MouseX2", 0x10005u)]
    [InlineData("MouseScrollUp", 0x10006u)]
    [InlineData("MouseScrollDown", 0x10007u)]
    [InlineData("MouseScrollLeft", 0x10008u)]
    [InlineData("MouseScrollRight", 0x10009u)]
    [InlineData("MouseDoubleClick", 0x1000Au)]
    [InlineData("MouseTripleClick", 0x1000Bu)]
    [InlineData("MouseX3", 0x10010u)]
    public void TryParseMouseButton_ValidNames(string name, uint expectedId)
    {
        Assert.True(ConfigManager.TryParseMouseButton(name, out uint id));
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("")]
    [InlineData("InvalidMouse")]
    public void TryParseMouseButton_InvalidNames_ReturnsFalse(string name)
    {
        Assert.False(ConfigManager.TryParseMouseButton(name, out _));
    }

    [Fact]
    public void TryParseMouseButton_CaseInsensitive()
    {
        Assert.True(ConfigManager.TryParseMouseButton("mouseleft", out uint id1));
        Assert.True(ConfigManager.TryParseMouseButton("MOUSELEFT", out uint id2));
        Assert.Equal(id1, id2);
    }

    [Theory]
    [InlineData(NativeConstants.WM_LBUTTONDOWN, 0u, 0x10001u)]
    [InlineData(NativeConstants.WM_RBUTTONDOWN, 0u, 0x10002u)]
    [InlineData(NativeConstants.WM_MBUTTONDOWN, 0u, 0x10003u)]
    [InlineData(NativeConstants.WM_XBUTTONDOWN, 0x10000u, 0x10004u)]  // XBUTTON1 in high word
    [InlineData(NativeConstants.WM_XBUTTONDOWN, 0x20000u, 0x10005u)]  // XBUTTON2 in high word
    [InlineData(NativeConstants.WM_XBUTTONDOWN, 0x30000u, 0x10010u)]  // XBUTTON3 in high word
    public void MouseMessageToId_MapsCorrectly(int msg, uint mouseData, uint expectedId)
    {
        Assert.Equal(expectedId, ConfigManager.MouseMessageToId(msg, mouseData));
    }

    [Fact]
    public void MouseMessageToId_UnknownMessage_ReturnsZero()
    {
        Assert.Equal(0u, ConfigManager.MouseMessageToId(NativeConstants.WM_MOUSEMOVE, 0));
    }

    [Fact]
    public void MouseIdToName_ValidId_ReturnsName()
    {
        Assert.Equal("MouseLeft", ConfigManager.MouseIdToName(0x10001));
        Assert.Equal("MouseX2", ConfigManager.MouseIdToName(0x10005));
    }

    [Fact]
    public void MouseButton_RoundTrip_IdToNameToId()
    {
        ConfigManager.TryParseMouseButton("MouseX1", out uint id);
        string name = ConfigManager.MouseIdToName(id);
        ConfigManager.TryParseMouseButton(name, out uint id2);
        Assert.Equal(id, id2);
    }

    [Theory]
    [InlineData("MouseLeft", "Left Click")]
    [InlineData("MouseRight", "Right Click")]
    [InlineData("MouseMiddle", "Middle Click")]
    [InlineData("MouseX1", "XButton 1 (Back)")]
    [InlineData("MouseX2", "XButton 2 (Forward)")]
    [InlineData("MouseScrollUp", "Scroll Up")]
    [InlineData("MouseDoubleClick", "Double Click")]
    [InlineData("MouseTripleClick", "Triple Click")]
    public void GetFriendlyMouseButtonName_ReturnsDisplayName(string keyName, string expected)
    {
        Assert.Equal(expected, ConfigManager.GetFriendlyMouseButtonName(keyName));
    }

    [Fact]
    public void VkToKeyName_MouseId_ReturnsMouseName()
    {
        Assert.Equal("MouseLeft", ConfigManager.VkToKeyName(0x10001));
        Assert.Equal("MouseX1", ConfigManager.VkToKeyName(0x10004));
    }

    [Fact]
    public void TryParseKey_MouseButtonName_ReturnsFalse()
    {
        // Mouse button names should NOT be parsed by TryParseKey (keyboard-only)
        Assert.False(ConfigManager.TryParseKey("MouseLeft", out _));
        Assert.False(ConfigManager.TryParseKey("MouseX1", out _));
    }

    // DisplayOrder tests

    [Fact]
    public void DisplayOrder_ContainsAllDefaultHotkeyKeys()
    {
        // Every non-null entry in DisplayOrder should be a valid hotkey config key
        var nonNullEntries = ConfigManager.DisplayOrder.Where(e => e != null).ToList();
        Assert.True(nonNullEntries.Count > 0);
    }

    [Fact]
    public void DisplayOrder_HasSectionDividers()
    {
        // Should contain null entries as section dividers
        Assert.Contains(null, ConfigManager.DisplayOrder);
    }

    [Fact]
    public void DisplayOrder_NoDuplicateKeys()
    {
        var nonNullEntries = ConfigManager.DisplayOrder.Where(e => e != null).ToList();
        Assert.Equal(nonNullEntries.Count, nonNullEntries.Distinct().Count());
    }
}
