using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Tactadile.Config;
using Tactadile.Core;
using Tactadile.Core.FancyZones;
using Tactadile.Native;
using LicenseManager = Tactadile.Licensing.LicenseManager;

namespace Tactadile.UI.Pages;

public sealed partial class FancyZonesPage : Page
{
    private ConfigManager? _configManager;
    private LicenseManager? _licenseManager;
    private FancyZonesLayoutProvider? _provider;
    private bool _loading;

    // Working copy of bindings, indexed by (layoutUuid, zoneIndex)
    private readonly Dictionary<(string layoutUuid, int zoneIndex), FancyZoneHotkeyBinding> _bindings = new();

    public FancyZonesPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is FancyZonesNavigationContext fzCtx)
        {
            _configManager = fzCtx.Config;
            _licenseManager = fzCtx.License;
            _provider = fzCtx.FancyZonesProvider;
        }
        else if (e.Parameter is NavigationContext ctx)
        {
            _configManager = ctx.Config;
            _licenseManager = ctx.License;
        }

        LoadPage();
    }

    private void LoadPage()
    {
        _loading = true;

        var isProRequired = _licenseManager != null && !_licenseManager.IsActionAllowed(ActionType.SnapToFancyZone);
        ProBanner.IsOpen = isProRequired;
        EnabledToggle.IsEnabled = !isProRequired;

        // Status
        bool available = _provider?.IsAvailable ?? false;
        StatusText.Text = available ? "FancyZones detected" : "FancyZones not detected";
        StatusIcon.Glyph = available ? "\uE73E" : "\uE711"; // Checkmark or Warning
        StatusIcon.Foreground = available
            ? new SolidColorBrush(Colors.Green)
            : new SolidColorBrush(Colors.Orange);

        if (_configManager != null)
            EnabledToggle.IsOn = _configManager.CurrentConfig.FancyZonesEnabled;

        // Load existing bindings into working copy
        _bindings.Clear();
        if (_configManager != null)
        {
            foreach (var b in _configManager.CurrentConfig.FancyZoneHotkeys)
            {
                _bindings[(b.LayoutUuid, b.ZoneIndex)] = new FancyZoneHotkeyBinding
                {
                    Modifiers = new List<string>(b.Modifiers),
                    Key = b.Key,
                    LayoutUuid = b.LayoutUuid,
                    ZoneIndex = b.ZoneIndex
                };
            }
        }

        BuildLayoutList();
        SaveButton.IsEnabled = false;
        _loading = false;
    }

    private void BuildLayoutList()
    {
        LayoutsPanel.Children.Clear();

        if (_provider == null || !_provider.IsAvailable)
        {
            LayoutsPanel.Children.Add(new TextBlock
            {
                Text = "Install PowerToys FancyZones to use this feature.\nFancyZones data directory was not found.",
                Foreground = new SolidColorBrush(Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var layouts = _provider.AllLayouts;
        if (layouts.Count == 0)
        {
            LayoutsPanel.Children.Add(new TextBlock
            {
                Text = "No FancyZones layouts found. Create a layout in the FancyZones editor (Win+Shift+`).",
                Foreground = new SolidColorBrush(Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var layout in layouts)
        {
            int zoneCount = _provider.GetZoneCount(layout);
            if (zoneCount == 0) continue;

            var section = new StackPanel { Spacing = 8 };

            // Layout header
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new TextBlock
            {
                Text = $"{layout.Name}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            });
            header.Children.Add(new TextBlock
            {
                Text = $"({zoneCount} zones, {layout.Type})",
                Foreground = new SolidColorBrush(Colors.Gray),
                VerticalAlignment = VerticalAlignment.Center
            });
            section.Children.Add(header);

            // Zone preview (mini grid) — resolve against a reference rect
            var previewRect = new RECT { Left = 0, Top = 0, Right = 400, Bottom = 200 };
            var previewZones = FancyZonesZoneResolver.ResolveZones(layout, previewRect);
            if (previewZones.Count > 0)
            {
                var previewCanvas = BuildZonePreview(previewZones, 400, 200);
                section.Children.Add(previewCanvas);
            }

            // Per-zone hotkey rows
            for (int z = 0; z < zoneCount; z++)
            {
                var row = BuildZoneRow(layout.Uuid, z);
                section.Children.Add(row);
            }

            // Divider
            section.Children.Add(new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                Margin = new Thickness(0, 4, 0, 0)
            });

            LayoutsPanel.Children.Add(section);
        }
    }

    private Canvas BuildZonePreview(List<FancyZonesZoneRect> zones, int canvasWidth, int canvasHeight)
    {
        var canvas = new Canvas
        {
            Width = canvasWidth,
            Height = canvasHeight,
            Background = new SolidColorBrush(Colors.Transparent)
        };

        var colors = new[]
        {
            Colors.CornflowerBlue, Colors.MediumSeaGreen, Colors.Coral,
            Colors.MediumOrchid, Colors.DarkKhaki, Colors.CadetBlue,
            Colors.IndianRed, Colors.MediumAquamarine
        };

        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            var color = colors[i % colors.Length];

            var border = new Border
            {
                Width = Math.Max(z.Width - 2, 1),
                Height = Math.Max(z.Height - 2, 1),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };

            var label = new TextBlock
            {
                Text = $"{i + 1}",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = label;

            Canvas.SetLeft(border, z.X + 1);
            Canvas.SetTop(border, z.Y + 1);
            canvas.Children.Add(border);
        }

        return canvas;
    }

    private Grid BuildZoneRow(string layoutUuid, int zoneIndex)
    {
        var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(8, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Zone label
        var label = new TextBlock
        {
            Text = $"Zone {zoneIndex + 1}",
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        // Shortcut display
        var binding = GetBinding(layoutUuid, zoneIndex);
        var shortcutText = new TextBlock
        {
            Text = FormatShortcut(binding),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        Grid.SetColumn(shortcutText, 1);
        grid.Children.Add(shortcutText);

        // Change button
        var changeBtn = new Button { Content = "Change" };
        int capturedZone = zoneIndex;
        string capturedUuid = layoutUuid;
        changeBtn.Click += async (s, e) =>
        {
            await ShowHotkeyDialog(capturedUuid, capturedZone);
            // Rebuild this row's display
            shortcutText.Text = FormatShortcut(GetBinding(capturedUuid, capturedZone));
        };
        Grid.SetColumn(changeBtn, 2);
        grid.Children.Add(changeBtn);

        // Clear button
        var clearBtn = new Button { Content = "Clear" };
        clearBtn.Click += (s, e) =>
        {
            _bindings.Remove((capturedUuid, capturedZone));
            shortcutText.Text = "(none)";
            MarkDirty();
        };
        Grid.SetColumn(clearBtn, 3);
        grid.Children.Add(clearBtn);

        return grid;
    }

    private FancyZoneHotkeyBinding? GetBinding(string layoutUuid, int zoneIndex)
    {
        _bindings.TryGetValue((layoutUuid, zoneIndex), out var binding);
        return binding;
    }

    private static string FormatShortcut(FancyZoneHotkeyBinding? binding)
    {
        if (binding == null || (binding.Modifiers.Count == 0 && string.IsNullOrEmpty(binding.Key)))
            return "(none)";
        var parts = new List<string>(binding.Modifiers);
        if (!string.IsNullOrEmpty(binding.Key))
            parts.Add(binding.Key);
        return string.Join(" + ", parts);
    }

    private async Task ShowHotkeyDialog(string layoutUuid, int zoneIndex)
    {
        var existing = GetBinding(layoutUuid, zoneIndex);

        var chkCtrl = new CheckBox { Content = "Ctrl", MinWidth = 0 };
        var chkShift = new CheckBox { Content = "Shift", MinWidth = 0 };
        var chkAlt = new CheckBox { Content = "Alt", MinWidth = 0 };
        var chkWin = new CheckBox { Content = "Win", MinWidth = 0 };

        if (existing != null)
        {
            foreach (var mod in existing.Modifiers)
            {
                switch (mod.ToLowerInvariant())
                {
                    case "ctrl": case "control": chkCtrl.IsChecked = true; break;
                    case "shift": chkShift.IsChecked = true; break;
                    case "alt": chkAlt.IsChecked = true; break;
                    case "win": chkWin.IsChecked = true; break;
                }
            }
        }

        var modPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modPanel.Children.Add(chkCtrl);
        modPanel.Children.Add(chkShift);
        modPanel.Children.Add(chkAlt);
        modPanel.Children.Add(chkWin);

        string capturedKeyName = existing?.Key ?? "";
        var keyDisplay = new TextBlock
        {
            Text = string.IsNullOrEmpty(capturedKeyName) ? "(none)" : capturedKeyName,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 14,
            MinWidth = 80
        };

        var recordButton = new Button { Content = "Record" };
        var clearKeyButton = new Button { Content = "Clear" };

        var keyPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        keyPanel.Children.Add(keyDisplay);
        keyPanel.Children.Add(recordButton);
        keyPanel.Children.Add(clearKeyButton);

        var layout = new StackPanel { Spacing = 12 };
        layout.Children.Add(new TextBlock { Text = "Modifiers:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        layout.Children.Add(modPanel);
        layout.Children.Add(new TextBlock { Text = "Key:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        layout.Children.Add(keyPanel);

        var dialog = new ContentDialog
        {
            Title = $"Set hotkey for Zone {zoneIndex + 1}",
            Content = layout,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        void UpdateOkEnabled(object? s = null, RoutedEventArgs? a = null)
        {
            dialog.IsPrimaryButtonEnabled =
                chkCtrl.IsChecked == true ||
                chkShift.IsChecked == true ||
                chkAlt.IsChecked == true ||
                chkWin.IsChecked == true;
        }

        chkCtrl.Checked += UpdateOkEnabled;
        chkCtrl.Unchecked += UpdateOkEnabled;
        chkShift.Checked += UpdateOkEnabled;
        chkShift.Unchecked += UpdateOkEnabled;
        chkAlt.Checked += UpdateOkEnabled;
        chkAlt.Unchecked += UpdateOkEnabled;
        chkWin.Checked += UpdateOkEnabled;
        chkWin.Unchecked += UpdateOkEnabled;
        UpdateOkEnabled();

        // Record button: capture one non-modifier key press
        var app = (App)Application.Current;
        var hook = app.KeyboardHook;

        recordButton.Click += (s, a) =>
        {
            recordButton.Content = "Press a key...";
            recordButton.IsEnabled = false;

            void OnKeyState(uint vkCode, bool isDown)
            {
                if (!isDown) return;
                if (IsModifierKey(vkCode)) return;

                hook.KeyStateChanged -= OnKeyState;
                capturedKeyName = ConfigManager.VkToKeyName(vkCode);

                DispatcherQueue.TryEnqueue(() =>
                {
                    keyDisplay.Text = capturedKeyName;
                    recordButton.Content = "Record";
                    recordButton.IsEnabled = true;
                });
            }

            hook.KeyStateChanged += OnKeyState;
        };

        clearKeyButton.Click += (s, a) =>
        {
            capturedKeyName = "";
            keyDisplay.Text = "(none)";
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var modList = new List<string>();
            if (chkCtrl.IsChecked == true) modList.Add("Ctrl");
            if (chkShift.IsChecked == true) modList.Add("Shift");
            if (chkAlt.IsChecked == true) modList.Add("Alt");
            if (chkWin.IsChecked == true) modList.Add("Win");

            _bindings[(layoutUuid, zoneIndex)] = new FancyZoneHotkeyBinding
            {
                Modifiers = modList,
                Key = capturedKeyName,
                LayoutUuid = layoutUuid,
                ZoneIndex = zoneIndex
            };

            MarkDirty();
        }
    }

    private void MarkDirty()
    {
        SaveButton.IsEnabled = true;
    }

    private void OnEnabledToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || _configManager == null) return;
        MarkDirty();
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        _provider?.Reload();
        LoadPage();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_configManager == null) return;

        var existing = _configManager.CurrentConfig;
        existing.FancyZonesEnabled = EnabledToggle.IsOn;

        // Convert bindings map to list
        existing.FancyZoneHotkeys = _bindings.Values
            .Where(b => b.Modifiers.Count > 0 || !string.IsNullOrEmpty(b.Key))
            .ToList();

        _configManager.Save(existing);
        SaveButton.IsEnabled = false;
    }

    private static bool IsModifierKey(uint vk) => vk is
        0x5B or 0x5C or    // VK_LWIN, VK_RWIN
        0x10 or 0xA0 or 0xA1 or  // VK_SHIFT, VK_LSHIFT, VK_RSHIFT
        0x11 or 0xA2 or 0xA3 or  // VK_CONTROL, VK_LCONTROL, VK_RCONTROL
        0x12 or 0xA4 or 0xA5;    // VK_MENU, VK_LMENU, VK_RMENU
}
