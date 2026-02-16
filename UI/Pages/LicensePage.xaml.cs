using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Tactadile.Licensing;

namespace Tactadile.UI.Pages;

public sealed partial class LicensePage : Page
{
    private LicenseManager? _licenseManager;

    public LicensePage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var ctx = e.Parameter as NavigationContext;
        _licenseManager = ctx?.License;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_licenseManager == null) return;

        var tier = _licenseManager.CurrentTier;
        var token = _licenseManager.CurrentToken;

        // Tier badge
        TierBadgeText.Text = tier switch
        {
            LicenseTier.Pro => "PRO",
            LicenseTier.Lifetime => "LIFETIME",
            _ => "FREE"
        };
        TierBadge.Background = tier switch
        {
            LicenseTier.Pro => new SolidColorBrush(ColorHelper.FromArgb(255, 59, 130, 246)),       // Blue
            LicenseTier.Lifetime => new SolidColorBrush(ColorHelper.FromArgb(255, 168, 85, 247)),  // Purple
            _ => new SolidColorBrush(ColorHelper.FromArgb(255, 107, 114, 128))                      // Gray
        };
        TierBadgeText.Foreground = new SolidColorBrush(Colors.White);

        // Updates until
        if (tier == LicenseTier.Pro && token?.UpdatesUntilUtc != null)
        {
            UpdatesUntilText.Text = $"Updates included until: {token.UpdatesUntilUtc.Value:MMMM d, yyyy}";
            UpdatesUntilText.Visibility = Visibility.Visible;
        }
        else if (tier == LicenseTier.Lifetime)
        {
            UpdatesUntilText.Text = "Updates included: Lifetime";
            UpdatesUntilText.Visibility = Visibility.Visible;
        }
        else
        {
            UpdatesUntilText.Visibility = Visibility.Collapsed;
        }

        // Last verified
        if (_licenseManager.LastRefreshUtc.HasValue && tier != LicenseTier.Free)
        {
            LastVerifiedText.Text = $"Last verified: {_licenseManager.LastRefreshUtc.Value:MMMM d, yyyy}";
            LastVerifiedText.Visibility = Visibility.Visible;
        }
        else
        {
            LastVerifiedText.Visibility = Visibility.Collapsed;
        }

        // Stale warning
        StaleWarningBar.IsOpen = _licenseManager.IsStale && tier != LicenseTier.Free;

        // Activate section — show key input when Free, hide when activated
        LicenseKeyInput.IsEnabled = true;
        ActivateButton.IsEnabled = true;

        // Manage section — visible when Pro or Lifetime
        if (tier is LicenseTier.Pro or LicenseTier.Lifetime)
        {
            ManageSection.Visibility = Visibility.Visible;
            RenewButton.Visibility = tier == LicenseTier.Pro ? Visibility.Visible : Visibility.Collapsed;
            UpgradeButton.Visibility = tier == LicenseTier.Pro ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            ManageSection.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnActivateClick(object sender, RoutedEventArgs e)
    {
        if (_licenseManager == null) return;

        var key = LicenseKeyInput.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            ShowActivateStatus("Please enter a license key.", isError: true);
            return;
        }

        ActivateButton.IsEnabled = false;
        LicenseKeyInput.IsEnabled = false;
        ActivateProgress.IsActive = true;
        ActivateStatusText.Visibility = Visibility.Collapsed;

        var (success, error) = await _licenseManager.ActivateAsync(key);

        ActivateProgress.IsActive = false;
        ActivateButton.IsEnabled = true;
        LicenseKeyInput.IsEnabled = true;

        if (success)
        {
            ShowActivateStatus("License activated successfully!", isError: false);
            LicenseKeyInput.Text = "";
            UpdateDisplay();
        }
        else
        {
            ShowActivateStatus(error, isError: true);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        if (_licenseManager == null) return;

        RefreshButton.IsEnabled = false;
        RefreshProgress.IsActive = true;

        var refreshed = await _licenseManager.TryRefreshAsync();

        RefreshProgress.IsActive = false;
        RefreshButton.IsEnabled = true;

        if (refreshed)
        {
            UpdateDisplay();
        }
    }

    private void OnRenewClick(object sender, RoutedEventArgs e)
    {
        var url = _licenseManager?.GetRenewalUrl();
        if (url != null)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void OnUpgradeClick(object sender, RoutedEventArgs e)
    {
        var url = _licenseManager?.GetUpgradeUrl();
        if (url != null)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void ShowActivateStatus(string message, bool isError)
    {
        ActivateStatusText.Text = message;
        ActivateStatusText.Foreground = isError
            ? new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68))    // Red
            : new SolidColorBrush(ColorHelper.FromArgb(255, 34, 197, 94));   // Green
        ActivateStatusText.Visibility = Visibility.Visible;
    }
}
