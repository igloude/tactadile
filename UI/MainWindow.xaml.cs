using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tactadile.Config;
using Tactadile.Core.FancyZones;
using Tactadile.Licensing;

namespace Tactadile.UI;

public record NavigationContext(ConfigManager Config, LicenseManager License);
public record AutoPositionNavigationContext(ConfigManager Config, LicenseManager License, bool ShowAppPicker)
    : NavigationContext(Config, License);
public record FancyZonesNavigationContext(ConfigManager Config, LicenseManager License, FancyZonesLayoutProvider FancyZonesProvider)
    : NavigationContext(Config, License);

public sealed partial class MainWindow : Window
{
    private readonly ConfigManager _configManager;
    private readonly LicenseManager _licenseManager;
    private readonly FancyZonesLayoutProvider? _fancyZonesProvider;

    public bool IsClosed { get; private set; }

    public MainWindow(ConfigManager configManager, LicenseManager licenseManager, FancyZonesLayoutProvider? fancyZonesProvider = null)
    {
        _configManager = configManager;
        _licenseManager = licenseManager;
        _fancyZonesProvider = fancyZonesProvider;
        this.InitializeComponent();

        // Set window size
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 980));

        // Set window icon
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(iconPath))
            appWindow.SetIcon(iconPath);

        // Navigate to default page
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(Pages.HotkeysPage), new NavigationContext(_configManager, _licenseManager));

        this.Closed += (s, e) => IsClosed = true;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            Type? pageType = tag switch
            {
                "HotkeysPage" => typeof(Pages.HotkeysPage),
                "GeneralPage" => typeof(Pages.GeneralPage),
                "AutoPositionPage" => typeof(Pages.AutoPositionPage),
                "FancyZonesPage" => typeof(Pages.FancyZonesPage),
                "LicensePage" => typeof(Pages.LicensePage),
                "AboutPage" => typeof(Pages.AboutPage),
                _ => null
            };
            if (pageType == null) return;

            object navParam = tag == "FancyZonesPage" && _fancyZonesProvider != null
                ? new FancyZonesNavigationContext(_configManager, _licenseManager, _fancyZonesProvider)
                : new NavigationContext(_configManager, _licenseManager);
            ContentFrame.Navigate(pageType, navParam);
        }
    }

    public void NavigateToAbout()
    {
        foreach (var item in NavView.FooterMenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == "AboutPage")
            {
                NavView.SelectedItem = navItem;
                break;
            }
        }
    }

    public void NavigateToAutoPosition(bool showAppPicker = false)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == "AutoPositionPage")
            {
                NavView.SelectedItem = navItem;
                break;
            }
        }

        if (showAppPicker)
        {
            ContentFrame.Navigate(typeof(Pages.AutoPositionPage),
                new AutoPositionNavigationContext(_configManager, _licenseManager, showAppPicker));
        }
    }
}
