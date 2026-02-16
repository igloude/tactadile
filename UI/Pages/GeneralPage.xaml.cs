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
