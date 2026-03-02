using Tactadile.Config;
using Tactadile.Core;

namespace Tactadile.Tests;

public sealed class EnumTests
{
    [Fact]
    public void ActionType_HasExpectedCount()
    {
        var values = Enum.GetValues<ActionType>();
        Assert.Equal(23, values.Length);
    }

    [Fact]
    public void GestureType_HasExpectedCount()
    {
        var values = Enum.GetValues<GestureType>();
        Assert.Equal(14, values.Length);
    }

    [Fact]
    public void ZoneType_HasExpectedCount()
    {
        var values = Enum.GetValues<ZoneType>();
        Assert.Equal(13, values.Length);
    }

    [Fact]
    public void AllActionTypes_ParseByName()
    {
        foreach (var action in Enum.GetValues<ActionType>())
        {
            var name = action.ToString();
            Assert.True(ConfigManager.TryParseAction(name, out var parsed));
            Assert.Equal(action, parsed);
        }
    }

    [Fact]
    public void AllGestureTypes_ParseByName()
    {
        foreach (var gesture in Enum.GetValues<GestureType>())
        {
            var name = gesture.ToString();
            Assert.True(ConfigManager.TryParseGestureType(name, out var parsed));
            Assert.Equal(gesture, parsed);
        }
    }

    [Fact]
    public void AllActionTypes_HaveFriendlyNames()
    {
        foreach (var action in Enum.GetValues<ActionType>())
        {
            var name = ConfigManager.GetFriendlyActionName(action);
            Assert.False(string.IsNullOrWhiteSpace(name));
        }
    }

    [Fact]
    public void AllZoneTypes_HaveFriendlyNames()
    {
        foreach (var zone in Enum.GetValues<ZoneType>())
        {
            var name = ZoneCalculator.GetFriendlyName(zone);
            Assert.False(string.IsNullOrWhiteSpace(name));
        }
    }

    [Fact]
    public void ActionType_ContainsExpectedValues()
    {
        Assert.True(Enum.IsDefined(ActionType.MoveDrag));
        Assert.True(Enum.IsDefined(ActionType.ResizeDrag));
        Assert.True(Enum.IsDefined(ActionType.SnapLeft));
        Assert.True(Enum.IsDefined(ActionType.SnapRight));
        Assert.True(Enum.IsDefined(ActionType.Minimize));
        Assert.True(Enum.IsDefined(ActionType.Maximize));
        Assert.True(Enum.IsDefined(ActionType.Restore));
        Assert.True(Enum.IsDefined(ActionType.OpacityUp));
        Assert.True(Enum.IsDefined(ActionType.OpacityDown));
        Assert.True(Enum.IsDefined(ActionType.ToggleMinimize));
        Assert.True(Enum.IsDefined(ActionType.ZoomIn));
        Assert.True(Enum.IsDefined(ActionType.ZoomOut));
        Assert.True(Enum.IsDefined(ActionType.TaskView));
        Assert.True(Enum.IsDefined(ActionType.NextVirtualDesktop));
        Assert.True(Enum.IsDefined(ActionType.PrevVirtualDesktop));
        Assert.True(Enum.IsDefined(ActionType.MinimizeAll));
        Assert.True(Enum.IsDefined(ActionType.ResizeWindow));
        Assert.True(Enum.IsDefined(ActionType.CascadeWindows));
        Assert.True(Enum.IsDefined(ActionType.CenterWindow));
        Assert.True(Enum.IsDefined(ActionType.NudgeUp));
        Assert.True(Enum.IsDefined(ActionType.NudgeDown));
        Assert.True(Enum.IsDefined(ActionType.NudgeLeft));
        Assert.True(Enum.IsDefined(ActionType.NudgeRight));
    }

    [Fact]
    public void GestureType_ContainsExpectedValues()
    {
        Assert.True(Enum.IsDefined(GestureType.ShakeHorizontal));
        Assert.True(Enum.IsDefined(GestureType.ShakeVertical));
        Assert.True(Enum.IsDefined(GestureType.SwipeUp));
        Assert.True(Enum.IsDefined(GestureType.SwipeDown));
        Assert.True(Enum.IsDefined(GestureType.SwipeLeft));
        Assert.True(Enum.IsDefined(GestureType.SwipeRight));
        Assert.True(Enum.IsDefined(GestureType.EdgeFlickLeft));
        Assert.True(Enum.IsDefined(GestureType.EdgeFlickRight));
        Assert.True(Enum.IsDefined(GestureType.EdgeFlickUp));
        Assert.True(Enum.IsDefined(GestureType.ScrollUp));
        Assert.True(Enum.IsDefined(GestureType.ScrollDown));
        Assert.True(Enum.IsDefined(GestureType.XButton1));
        Assert.True(Enum.IsDefined(GestureType.XButton2));
        Assert.True(Enum.IsDefined(GestureType.MiddleClick));
    }
}
