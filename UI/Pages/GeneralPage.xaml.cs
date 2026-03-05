using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Tactadile.Config;
using Tactadile.Helpers;
using Tactadile.UI;

namespace Tactadile.UI.Pages;

public sealed partial class GeneralPage : Page
{
    private ConfigManager? _configManager;
    private bool _loading;

    public GeneralPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var ctx = e.Parameter as NavigationContext;
        _configManager = ctx?.Config;
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (_configManager == null) return;

        _loading = true;
        StartupToggle.IsOn = StartupHelper.IsEnabled;
        EdgeSnapToggle.IsOn = _configManager.CurrentConfig.EdgeSnappingEnabled;
        OverrideKeybindsToggle.IsOn = _configManager.CurrentConfig.OverrideWindowsKeybinds;
        DisableNativeSnapToggle.IsOn = _configManager.CurrentConfig.DisableNativeSnap;
        BlockCopilotToggle.IsOn = _configManager.CurrentConfig.BlockCopilot;
        WinKeyDelayToggle.IsOn = _configManager.CurrentConfig.WinKeyDelayEnabled;
        WinKeyDelaySlider.Value = _configManager.CurrentConfig.WinKeyDelayMs;
        WinKeyDelayValueText.Text = $"{_configManager.CurrentConfig.WinKeyDelayMs} ms";
        WinKeyDelaySliderPanel.Visibility = WinKeyDelayToggle.IsOn
            ? Visibility.Visible : Visibility.Collapsed;
        _loading = false;
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        StartupHelper.SetEnabled(StartupToggle.IsOn);
    }

    private void OnEdgeSnapToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || _configManager == null) return;

        var config = _configManager.CurrentConfig;
        config.EdgeSnappingEnabled = EdgeSnapToggle.IsOn;
        _configManager.Save(config);
    }

    private void OnOverrideKeybindsToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || _configManager == null) return;

        var config = _configManager.CurrentConfig;
        config.OverrideWindowsKeybinds = OverrideKeybindsToggle.IsOn;
        _configManager.Save(config);
    }

    private void OnDisableNativeSnapToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || _configManager == null) return;

        var config = _configManager.CurrentConfig;
        config.DisableNativeSnap = DisableNativeSnapToggle.IsOn;
        _configManager.Save(config);
    }

    private void OnBlockCopilotToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || _configManager == null) return;

        var config = _configManager.CurrentConfig;
        config.BlockCopilot = BlockCopilotToggle.IsOn;
        _configManager.Save(config);
    }

    private void OnWinKeyDelayToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || _configManager == null) return;

        var config = _configManager.CurrentConfig;
        config.WinKeyDelayEnabled = WinKeyDelayToggle.IsOn;
        _configManager.Save(config);
        WinKeyDelaySliderPanel.Visibility = WinKeyDelayToggle.IsOn
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnWinKeyDelaySliderChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loading || _configManager == null) return;

        var config = _configManager.CurrentConfig;
        config.WinKeyDelayMs = (int)WinKeyDelaySlider.Value;
        _configManager.Save(config);
        WinKeyDelayValueText.Text = $"{config.WinKeyDelayMs} ms";
    }

    private void OnOpenConfigFolder(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(ConfigManager.ConfigDirectory)
        {
            UseShellExecute = true
        });
    }

    private void OnReloadConfig(object sender, RoutedEventArgs e)
    {
        _configManager?.Reload();
        LoadSettings();
    }
}
