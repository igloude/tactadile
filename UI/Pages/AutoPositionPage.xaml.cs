using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Tactadile.Config;
using Tactadile.Core;
using Tactadile.UI;
using Windows.UI;

namespace Tactadile.UI.Pages;

public sealed partial class AutoPositionPage : Page
{
    private ConfigManager? _configManager;
    private Tactadile.Licensing.LicenseManager? _licenseManager;
    private bool _loading;
    private List<LaunchRuleViewModel> _allRules = new();
    private bool _showAppPickerOnLoad;

    public AutoPositionPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is AutoPositionNavigationContext ctx)
        {
            _configManager = ctx.Config;
            _licenseManager = ctx.License;
            _showAppPickerOnLoad = ctx.ShowAppPicker;
        }
        else if (e.Parameter is NavigationContext navCtx)
        {
            _configManager = navCtx.Config;
            _licenseManager = navCtx.License;
        }

        LoadRules();
        UpdateProBanner();

        if (_showAppPickerOnLoad)
        {
            _showAppPickerOnLoad = false;
            _ = ShowAddRuleDialogAsync();
        }
    }

    private void UpdateProBanner()
    {
        if (_licenseManager != null && !_licenseManager.IsAutoPositionAllowed)
            ProBanner.IsOpen = true;
        else
            ProBanner.IsOpen = false;
    }

    private void LoadRules()
    {
        if (_configManager == null) return;
        _loading = true;

        var config = _configManager.CurrentConfig;
        EnabledToggle.IsOn = config.AutoPositionEnabled;

        _allRules = config.LaunchRules.Select(r => new LaunchRuleViewModel(r)).ToList();
        ApplyFilter();

        _loading = false;
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(query)
            ? _allRules.ToList()
            : _allRules.Where(r =>
                r.AppName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        RulesList.ItemsSource = null;
        RulesList.Items.Clear();

        foreach (var vm in filtered)
        {
            var item = BuildRuleRow(vm);
            RulesList.Items.Add(item);
        }

        EmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Grid BuildRuleRow(LaunchRuleViewModel vm)
    {
        var grid = new Grid { ColumnSpacing = 12, Padding = new Thickness(4, 8, 4, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var appName = new TextBlock
        {
            Text = vm.AppName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(appName, 0);
        grid.Children.Add(appName);

        var monitorText = new TextBlock
        {
            Text = vm.MonitorDisplay,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        Grid.SetColumn(monitorText, 1);
        grid.Children.Add(monitorText);

        var zoneText = new TextBlock
        {
            Text = vm.ZoneDisplay,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        Grid.SetColumn(zoneText, 2);
        grid.Children.Add(zoneText);

        var toggle = new ToggleSwitch
        {
            IsOn = vm.Enabled,
            MinWidth = 0,
            MinHeight = 0
        };
        toggle.Toggled += (s, e) =>
        {
            if (_loading) return;
            vm.Enabled = toggle.IsOn;
            SaveRules();
        };
        Grid.SetColumn(toggle, 3);
        grid.Children.Add(toggle);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var editBtn = new Button { Content = "Edit" };
        editBtn.Click += async (s, e) =>
        {
            var edited = await ShowRuleConfigDialogAsync(vm, isNew: false);
            if (edited)
            {
                SaveRules();
                ApplyFilter();
            }
        };
        var deleteBtn = new Button { Content = "Delete" };
        deleteBtn.Click += async (s, e) =>
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Rule",
                Content = $"Remove the rule for \"{vm.AppName}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                _allRules.Remove(vm);
                SaveRules();
                ApplyFilter();
            }
        };
        btnPanel.Children.Add(editBtn);
        btnPanel.Children.Add(deleteBtn);
        Grid.SetColumn(btnPanel, 5);
        grid.Children.Add(btnPanel);

        return grid;
    }

    private void OnEnabledToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || _configManager == null) return;
        var config = _configManager.CurrentConfig;
        config.AutoPositionEnabled = EnabledToggle.IsOn;
        _configManager.Save(config);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private async void OnAddRule(object sender, RoutedEventArgs e)
    {
        await ShowAddRuleDialogAsync();
    }

    private async void OnCaptureCurrentWindow(object sender, RoutedEventArgs e)
    {
        await ShowAddRuleDialogAsync();
    }

    private async Task ShowAddRuleDialogAsync()
    {
        var apps = ProcessInfoHelper.GetRunningApps();
        var picked = await ShowAppPickerDialogAsync(apps);
        if (picked == null) return;

        var rule = new LaunchRule
        {
            AppName = picked.ProcessName,
            ExecutablePath = picked.ExecutablePath,
            ProcessName = picked.ProcessName,
            MonitorIndex = 0,
            Zone = "LeftHalf"
        };

        var vm = new LaunchRuleViewModel(rule);
        var confirmed = await ShowRuleConfigDialogAsync(vm, isNew: true);
        if (confirmed)
        {
            _allRules.Add(vm);
            SaveRules();
            ApplyFilter();
        }
    }

    private async Task<RunningAppInfo?> ShowAppPickerDialogAsync(List<RunningAppInfo> apps)
    {
        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 400
        };

        foreach (var app in apps)
        {
            var panel = new StackPanel { Spacing = 2, Padding = new Thickness(4) };
            panel.Children.Add(new TextBlock
            {
                Text = app.ProcessName,
                FontWeight = FontWeights.SemiBold
            });
            panel.Children.Add(new TextBlock
            {
                Text = app.ExecutablePath,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            panel.Tag = app;
            listView.Items.Add(panel);
        }

        var dialog = new ContentDialog
        {
            Title = "Choose an Application",
            Content = listView,
            PrimaryButtonText = "Select",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        dialog.IsPrimaryButtonEnabled = false;
        listView.SelectionChanged += (s, e) =>
            dialog.IsPrimaryButtonEnabled = listView.SelectedItem != null;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && listView.SelectedItem is FrameworkElement el)
            return el.Tag as RunningAppInfo;
        return null;
    }

    private async Task<bool> ShowRuleConfigDialogAsync(LaunchRuleViewModel vm, bool isNew)
    {
        // Monitor picker
        var monitors = MonitorHelper.GetAllMonitors();
        var monitorCombo = new ComboBox { MinWidth = 250 };
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            monitorCombo.Items.Add($"Monitor {i + 1} ({m.WorkArea.Width}\u00d7{m.WorkArea.Height})");
        }
        monitorCombo.SelectedIndex = Math.Clamp(vm.MonitorIndex, 0, Math.Max(0, monitors.Count - 1));

        // Zone tile grid
        var zoneGrid = BuildZoneTileGrid(vm.Zone);

        // Behavior checkboxes
        var chkFirstOnly = new CheckBox
        {
            Content = "Apply only to the first window",
            IsChecked = vm.ApplyOnlyToFirstWindow
        };

        var layout = new StackPanel { Spacing = 16, MinWidth = 450 };
        layout.Children.Add(new TextBlock { Text = "Target Monitor", FontWeight = FontWeights.SemiBold });
        layout.Children.Add(monitorCombo);
        layout.Children.Add(new TextBlock { Text = "Window Zone", FontWeight = FontWeights.SemiBold });
        layout.Children.Add(zoneGrid.Panel);
        layout.Children.Add(chkFirstOnly);

        var dialog = new ContentDialog
        {
            Title = isNew ? $"New Rule: {vm.AppName}" : $"Edit Rule: {vm.AppName}",
            Content = new ScrollViewer { Content = layout, MaxHeight = 600 },
            PrimaryButtonText = isNew ? "Add" : "Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            vm.MonitorIndex = monitorCombo.SelectedIndex;
            vm.Zone = zoneGrid.SelectedZone.ToString();
            vm.ApplyOnlyToFirstWindow = chkFirstOnly.IsChecked == true;
            return true;
        }

        return false;
    }

    private void SaveRules()
    {
        if (_configManager == null) return;
        var config = _configManager.CurrentConfig;
        config.LaunchRules = _allRules.Select(vm => vm.ToRule()).ToList();
        _configManager.Save(config);
    }

    // --- Zone Tile Grid ---

    private record ZoneTileGridResult(FrameworkElement Panel, ZoneType SelectedZone)
    {
        public ZoneType SelectedZone { get; set; } = SelectedZone;
    }

    private static ZoneTileGridResult BuildZoneTileGrid(string currentZone)
    {
        Enum.TryParse<ZoneType>(currentZone, ignoreCase: true, out var selectedZone);

        var result = new ZoneTileGridResult(null!, selectedZone);
        var grid = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
        for (int c = 0; c < 4; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < 4; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var zones = Enum.GetValues<ZoneType>();
        var tiles = new List<Border>();

        for (int i = 0; i < zones.Length; i++)
        {
            var zone = zones[i];
            int row = i / 4;
            int col = i % 4;

            var tile = CreateZoneTile(zone, zone == selectedZone);
            Grid.SetRow(tile, row);
            Grid.SetColumn(tile, col);
            grid.Children.Add(tile);
            tiles.Add(tile);

            var capturedZone = zone;
            tile.Tapped += (s, e) =>
            {
                result.SelectedZone = capturedZone;
                foreach (var t in tiles)
                {
                    bool isSelected = t == tile;
                    t.BorderBrush = isSelected
                        ? new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 212))
                        : new SolidColorBrush(ColorHelper.FromArgb(255, 80, 80, 80));
                    t.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
                    t.Background = isSelected
                        ? new SolidColorBrush(ColorHelper.FromArgb(30, 0, 120, 212))
                        : new SolidColorBrush(Colors.Transparent);
                }
            };
        }

        result = result with { Panel = grid };
        return result;
    }

    private static Border CreateZoneTile(ZoneType zone, bool isSelected)
    {
        // Mini preview: 80x50 grid showing the zone region
        var preview = new Grid
        {
            Width = 80,
            Height = 50,
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 40, 40, 40)),
            CornerRadius = new CornerRadius(2)
        };

        // Add a highlight rectangle for the zone region
        var highlight = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(180, 0, 120, 212)),
            CornerRadius = new CornerRadius(1)
        };
        SetZonePosition(highlight, preview, zone);
        preview.Children.Add(highlight);

        var label = new TextBlock
        {
            Text = ZoneCalculator.GetFriendlyName(zone),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var content = new StackPanel
        {
            Spacing = 4,
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        content.Children.Add(preview);
        content.Children.Add(label);

        var border = new Border
        {
            Child = content,
            CornerRadius = new CornerRadius(6),
            BorderThickness = isSelected ? new Thickness(2) : new Thickness(1),
            BorderBrush = isSelected
                ? new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 212))
                : new SolidColorBrush(ColorHelper.FromArgb(255, 80, 80, 80)),
            Background = isSelected
                ? new SolidColorBrush(ColorHelper.FromArgb(30, 0, 120, 212))
                : new SolidColorBrush(Colors.Transparent),
            MinWidth = 100
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            border, ZoneCalculator.GetFriendlyName(zone));

        return border;
    }

    private static void SetZonePosition(Border highlight, Grid container, ZoneType zone)
    {
        // Position the highlight within the 80x50 preview using margin
        // Container is 80x50. We simulate zone position with margin.
        double w = 80, h = 50;

        (double x, double y, double zw, double zh) = zone switch
        {
            ZoneType.Centered       => (w / 6, h / 6, w * 2 / 3, h * 2 / 3),
            ZoneType.TopHalf        => (0, 0, w, h / 2),
            ZoneType.BottomHalf     => (0, h / 2, w, h / 2),
            ZoneType.TopLeft        => (0, 0, w / 2, h / 2),
            ZoneType.TopRight       => (w / 2, 0, w / 2, h / 2),
            ZoneType.BottomLeft     => (0, h / 2, w / 2, h / 2),
            ZoneType.BottomRight    => (w / 2, h / 2, w / 2, h / 2),
            ZoneType.LeftThird      => (0, 0, w / 3, h),
            ZoneType.LeftHalf       => (0, 0, w / 2, h),
            ZoneType.LeftTwoThirds  => (0, 0, w * 2 / 3, h),
            ZoneType.RightThird    => (w - w / 3, 0, w / 3, h),
            ZoneType.RightHalf      => (w / 2, 0, w / 2, h),
            ZoneType.RightTwoThirds => (w - w * 2 / 3, 0, w * 2 / 3, h),
            _                       => (0, 0, w, h)
        };

        highlight.Width = zw;
        highlight.Height = zh;
        highlight.HorizontalAlignment = HorizontalAlignment.Left;
        highlight.VerticalAlignment = VerticalAlignment.Top;
        highlight.Margin = new Thickness(x, y, 0, 0);
    }
}

public class LaunchRuleViewModel : INotifyPropertyChanged
{
    private readonly LaunchRule _rule;

    public LaunchRuleViewModel(LaunchRule rule) => _rule = rule;

    public string Id => _rule.Id;
    public string AppName
    {
        get => _rule.AppName;
        set { _rule.AppName = value; OnPropertyChanged(nameof(AppName)); }
    }
    public string ExecutablePath => _rule.ExecutablePath;
    public string ProcessName => _rule.ProcessName;
    public int MonitorIndex
    {
        get => _rule.MonitorIndex;
        set { _rule.MonitorIndex = value; OnPropertyChanged(null); }
    }
    public string Zone
    {
        get => _rule.Zone;
        set { _rule.Zone = value; OnPropertyChanged(null); }
    }
    public bool Enabled
    {
        get => _rule.Enabled;
        set { _rule.Enabled = value; OnPropertyChanged(nameof(Enabled)); }
    }
    public bool ApplyOnlyToFirstWindow
    {
        get => _rule.ApplyOnlyToFirstWindow;
        set { _rule.ApplyOnlyToFirstWindow = value; }
    }

    public string MonitorDisplay => $"Monitor {MonitorIndex + 1}";
    public string ZoneDisplay =>
        Enum.TryParse<ZoneType>(Zone, out var zt)
            ? ZoneCalculator.GetFriendlyName(zt) : Zone;

    public LaunchRule ToRule() => new()
    {
        Id = _rule.Id,
        AppName = _rule.AppName,
        ExecutablePath = _rule.ExecutablePath,
        ProcessName = _rule.ProcessName,
        MonitorIndex = _rule.MonitorIndex,
        Zone = _rule.Zone,
        Enabled = _rule.Enabled,
        ApplyOnlyToFirstWindow = _rule.ApplyOnlyToFirstWindow,
        DelayMs = _rule.DelayMs
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
