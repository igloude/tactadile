using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Tactadile.Config;
using Tactadile.Core;
using Tactadile.Native;
using Tactadile.UI;
using LicenseManager = Tactadile.Licensing.LicenseManager;

namespace Tactadile.UI.Pages;

public sealed partial class HotkeysPage : Page
{
    private ConfigManager? _configManager;
    private LicenseManager? _licenseManager;
    private List<HotkeyBindingViewModel> _bindings = new();
    private List<Expander> _expanders = new();
    private bool _allExpanded = true;

    public HotkeysPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var ctx = e.Parameter as NavigationContext;
        _configManager = ctx?.Config;
        _licenseManager = ctx?.License;
        LoadConfig();
    }

    private void MarkDirty()
    {
        SaveButton.IsEnabled = true;
    }

    private void OnExpandCollapseAll(object sender, RoutedEventArgs e)
    {
        _allExpanded = !_allExpanded;
        foreach (var expander in _expanders)
            expander.IsExpanded = _allExpanded;
        ExpandCollapseButton.Content = _allExpanded ? "Collapse All" : "Expand All";
    }

    private void LoadConfig()
    {
        if (_configManager == null) return;

        SaveButton.IsEnabled = false;
        _bindings.Clear();
        _expanders.Clear();
        SectionsPanel.Children.Clear();
        _allExpanded = true;
        ExpandCollapseButton.Content = "Collapse All";

        var config = _configManager.CurrentConfig;
        var template = (DataTemplate)Resources["HotkeyItemTemplate"];
        var knownKeys = new HashSet<string>();

        foreach (var section in ConfigManager.DisplaySections)
        {
            var items = new List<HotkeyBindingViewModel>();

            foreach (var key in section.Keys)
            {
                knownKeys.Add(key);
                if (!config.Hotkeys.TryGetValue(key, out var binding))
                    continue;
                if (!ConfigManager.TryParseAction(binding.Action, out var actionType))
                    continue;

                var vm = new HotkeyBindingViewModel
                {
                    ConfigKey = key,
                    ActionType = actionType,
                    FriendlyName = ConfigManager.GetFriendlyConfigKeyName(key, actionType),
                    IsProOnly = _licenseManager != null && !_licenseManager.IsActionAllowed(actionType),
                    Binding = new HotkeyBinding
                    {
                        Modifiers = new List<string>(binding.Modifiers),
                        Key = binding.Key,
                        Action = binding.Action,
                        Parameters = new Dictionary<string, double>(binding.Parameters)
                    }
                };
                _bindings.Add(vm);
                items.Add(vm);
            }

            if (items.Count > 0)
            {
                var expander = CreateExpanderForGroup(section.Name, items, template);
                _expanders.Add(expander);
                SectionsPanel.Children.Add(expander);
            }
        }

        // Append any keys not in DisplaySections (future-proofing)
        var ungrouped = new List<HotkeyBindingViewModel>();
        foreach (var (key, binding) in config.Hotkeys)
        {
            if (knownKeys.Contains(key) || _bindings.Any(b => b.ConfigKey == key))
                continue;
            if (!ConfigManager.TryParseAction(binding.Action, out var actionType))
                continue;

            var vm = new HotkeyBindingViewModel
            {
                ConfigKey = key,
                ActionType = actionType,
                FriendlyName = ConfigManager.GetFriendlyConfigKeyName(key, actionType),
                IsProOnly = _licenseManager != null && !_licenseManager.IsActionAllowed(actionType),
                Binding = new HotkeyBinding
                {
                    Modifiers = new List<string>(binding.Modifiers),
                    Key = binding.Key,
                    Action = binding.Action,
                    Parameters = new Dictionary<string, double>(binding.Parameters)
                }
            };
            _bindings.Add(vm);
            ungrouped.Add(vm);
        }

        if (ungrouped.Count > 0)
        {
            var expander = CreateExpanderForGroup("Other", ungrouped, template);
            _expanders.Add(expander);
            SectionsPanel.Children.Add(expander);
        }
    }

    private static Expander CreateExpanderForGroup(string name, List<HotkeyBindingViewModel> items, DataTemplate template)
    {
        var expander = new Expander
        {
            Header = name,
            IsExpanded = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };

        var repeater = new ItemsRepeater
        {
            ItemsSource = items,
            ItemTemplate = template,
        };

        expander.Content = repeater;
        return expander;
    }

    private async void OnChangeClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var vm = (HotkeyBindingViewModel)button.Tag;

        if (vm.IsProOnly)
        {
            var proDialog = new ContentDialog
            {
                Title = "Pro License Required",
                Content = $"{vm.FriendlyName} is available with a Pro license.\nGo to Settings \u2192 License to activate.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await proDialog.ShowAsync();
            return;
        }

        // Modifier checkboxes
        var chkCtrl = new CheckBox { Content = "Ctrl", MinWidth = 0 };
        var chkShift = new CheckBox { Content = "Shift", MinWidth = 0 };
        var chkAlt = new CheckBox { Content = "Alt", MinWidth = 0 };
        var chkWin = new CheckBox { Content = "Win", MinWidth = 0 };

        // Pre-populate from current binding
        foreach (var mod in vm.Binding.Modifiers)
        {
            switch (mod.ToLowerInvariant())
            {
                case "ctrl": case "control": chkCtrl.IsChecked = true; break;
                case "shift": chkShift.IsChecked = true; break;
                case "alt": chkAlt.IsChecked = true; break;
                case "win": chkWin.IsChecked = true; break;
            }
        }

        var modPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modPanel.Children.Add(chkCtrl);
        modPanel.Children.Add(chkShift);
        modPanel.Children.Add(chkAlt);
        modPanel.Children.Add(chkWin);

        // Key capture area
        string capturedKeyName = vm.Binding.Key ?? "";
        bool currentIsMouse = ConfigManager.IsMouseButton(capturedKeyName);

        var keyDisplay = new TextBlock
        {
            Text = (!currentIsMouse && !string.IsNullOrEmpty(capturedKeyName)) ? capturedKeyName : "(none)",
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
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

        // Mouse button dropdown
        var mouseButtonItems = new (string configName, string displayName)[]
        {
            ("MouseLeft", "Left Click"),
            ("MouseRight", "Right Click"),
            ("MouseMiddle", "Middle Click"),
            ("MouseX1", "XButton 1 (Back)"),
            ("MouseX2", "XButton 2 (Forward)"),
            ("MouseScrollUp", "Scroll Up"),
            ("MouseScrollDown", "Scroll Down"),
            ("MouseScrollLeft", "Scroll Left"),
            ("MouseScrollRight", "Scroll Right"),
            ("MouseDoubleClick", "Double Click"),
            ("MouseTripleClick", "Triple Click"),
        };

        var mouseButtonCombo = new ComboBox
        {
            PlaceholderText = "Select mouse button",
            Width = 220,
            Visibility = Visibility.Collapsed
        };

        foreach (var (configName, displayName) in mouseButtonItems)
            mouseButtonCombo.Items.Add(new ComboBoxItem { Content = displayName, Tag = configName });

        if (currentIsMouse)
        {
            for (int i = 0; i < mouseButtonCombo.Items.Count; i++)
            {
                if ((string)((ComboBoxItem)mouseButtonCombo.Items[i]).Tag == capturedKeyName)
                {
                    mouseButtonCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        // Input mode toggle
        var rbKeyboard = new RadioButton { Content = "Keyboard", GroupName = "InputMode", MinWidth = 0 };
        var rbMouse = new RadioButton { Content = "Mouse button", GroupName = "InputMode", MinWidth = 0 };

        if (currentIsMouse)
            rbMouse.IsChecked = true;
        else
            rbKeyboard.IsChecked = true;

        void UpdateInputMode()
        {
            bool isMouse = rbMouse.IsChecked == true;
            keyPanel.Visibility = isMouse ? Visibility.Collapsed : Visibility.Visible;
            mouseButtonCombo.Visibility = isMouse ? Visibility.Visible : Visibility.Collapsed;
        }

        rbKeyboard.Checked += (s, a) => UpdateInputMode();
        rbMouse.Checked += (s, a) => UpdateInputMode();
        UpdateInputMode();

        var inputModePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        inputModePanel.Children.Add(rbKeyboard);
        inputModePanel.Children.Add(rbMouse);

        var layout = new StackPanel { Spacing = 12 };
        layout.Children.Add(new TextBlock { Text = "Modifiers:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        layout.Children.Add(modPanel);
        layout.Children.Add(new TextBlock { Text = "Trigger:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        layout.Children.Add(inputModePanel);
        layout.Children.Add(keyPanel);
        layout.Children.Add(mouseButtonCombo);

        // Parameter inputs for actions that have configurable parameters
        NumberBox? widthBox = null, heightBox = null;
        NumberBox? widthPercentBox = null, heightPercentBox = null;
        ToggleSwitch? cascadeToggle = null;
        NumberBox? distanceBox = null;

        if (vm.ActionType == ActionType.ResizeWindow)
        {
            layout.Children.Add(new TextBlock { Text = "Dimensions:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            widthBox = new NumberBox
            {
                Header = "Width (px)", Value = vm.Binding.Parameters.GetValueOrDefault("Width", 1280),
                Minimum = 100, Maximum = 10000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            heightBox = new NumberBox
            {
                Header = "Height (px)", Value = vm.Binding.Parameters.GetValueOrDefault("Height", 720),
                Minimum = 100, Maximum = 10000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            var dimPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            dimPanel.Children.Add(widthBox);
            dimPanel.Children.Add(heightBox);
            layout.Children.Add(dimPanel);
        }
        else if (vm.ActionType == ActionType.CenterWindow)
        {
            layout.Children.Add(new TextBlock { Text = "Size (% of screen):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            widthPercentBox = new NumberBox
            {
                Header = "Width %", Value = vm.Binding.Parameters.GetValueOrDefault("WidthPercent", 60),
                Minimum = 10, Maximum = 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            heightPercentBox = new NumberBox
            {
                Header = "Height %", Value = vm.Binding.Parameters.GetValueOrDefault("HeightPercent", 80),
                Minimum = 10, Maximum = 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            var pctPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            pctPanel.Children.Add(widthPercentBox);
            pctPanel.Children.Add(heightPercentBox);
            layout.Children.Add(pctPanel);
        }
        else if (vm.ActionType == ActionType.CascadeWindows)
        {
            cascadeToggle = new ToggleSwitch
            {
                Header = "Cascade direction",
                OnContent = "From top-right",
                OffContent = "From top-left",
                IsOn = vm.Binding.Parameters.GetValueOrDefault("CascadeFromRight", 0) != 0
            };
            layout.Children.Add(cascadeToggle);
        }
        else if (vm.ActionType is ActionType.NudgeUp or ActionType.NudgeDown
                 or ActionType.NudgeLeft or ActionType.NudgeRight)
        {
            layout.Children.Add(new TextBlock { Text = "Nudge distance:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            distanceBox = new NumberBox
            {
                Header = "Distance (px)", Value = vm.Binding.Parameters.GetValueOrDefault("Distance", 10),
                Minimum = 1, Maximum = 500, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            layout.Children.Add(distanceBox);
        }

        var dialog = new ContentDialog
        {
            Title = $"Set hotkey for: {vm.FriendlyName}",
            Content = layout,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        // Enable OK based on input mode and validation rules
        void UpdateOkEnabled(object? s = null, RoutedEventArgs? a = null)
        {
            bool hasModifier = chkCtrl.IsChecked == true || chkShift.IsChecked == true
                || chkAlt.IsChecked == true || chkWin.IsChecked == true;

            if (rbMouse.IsChecked == true)
            {
                bool hasSelection = mouseButtonCombo.SelectedItem != null;
                if (!hasSelection)
                {
                    dialog.IsPrimaryButtonEnabled = false;
                    return;
                }

                string selectedMouse = (string)((ComboBoxItem)mouseButtonCombo.SelectedItem!).Tag!;

                // Left/Right click require at least one modifier (safety)
                bool requiresModifier = selectedMouse is "MouseLeft" or "MouseRight";
                dialog.IsPrimaryButtonEnabled = !requiresModifier || hasModifier;
            }
            else
            {
                // Keyboard mode: at least one modifier required
                dialog.IsPrimaryButtonEnabled = hasModifier;
            }
        }

        chkCtrl.Checked += UpdateOkEnabled;
        chkCtrl.Unchecked += UpdateOkEnabled;
        chkShift.Checked += UpdateOkEnabled;
        chkShift.Unchecked += UpdateOkEnabled;
        chkAlt.Checked += UpdateOkEnabled;
        chkAlt.Unchecked += UpdateOkEnabled;
        chkWin.Checked += UpdateOkEnabled;
        chkWin.Unchecked += UpdateOkEnabled;
        rbKeyboard.Checked += UpdateOkEnabled;
        rbMouse.Checked += UpdateOkEnabled;
        mouseButtonCombo.SelectionChanged += (s, a) => UpdateOkEnabled();
        UpdateOkEnabled();

        // Record button: listen for one non-modifier key press
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

            vm.Binding.Modifiers = modList;

            if (rbMouse.IsChecked == true && mouseButtonCombo.SelectedItem is ComboBoxItem selectedItem)
                vm.Binding.Key = (string)selectedItem.Tag;
            else
                vm.Binding.Key = capturedKeyName;

            // Save parameter values
            if (vm.ActionType == ActionType.ResizeWindow && widthBox != null && heightBox != null)
            {
                vm.Binding.Parameters["Width"] = widthBox.Value;
                vm.Binding.Parameters["Height"] = heightBox.Value;
            }
            else if (vm.ActionType == ActionType.CenterWindow && widthPercentBox != null && heightPercentBox != null)
            {
                vm.Binding.Parameters["WidthPercent"] = widthPercentBox.Value;
                vm.Binding.Parameters["HeightPercent"] = heightPercentBox.Value;
            }
            else if (vm.ActionType == ActionType.CascadeWindows && cascadeToggle != null)
            {
                vm.Binding.Parameters["CascadeFromRight"] = cascadeToggle.IsOn ? 1 : 0;
            }
            else if (vm.ActionType is ActionType.NudgeUp or ActionType.NudgeDown
                     or ActionType.NudgeLeft or ActionType.NudgeRight && distanceBox != null)
            {
                vm.Binding.Parameters["Distance"] = distanceBox.Value;
            }

            vm.NotifyChanged();
            MarkDirty();
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var vm = (HotkeyBindingViewModel)button.Tag;

        vm.Binding.Modifiers = new List<string>();
        vm.Binding.Key = "";
        vm.NotifyChanged();
        MarkDirty();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_configManager == null) return;

        var existing = _configManager.CurrentConfig;
        var config = new AppConfig
        {
            Version = existing.Version,
            EdgeSnappingEnabled = existing.EdgeSnappingEnabled,
            OverrideWindowsKeybinds = existing.OverrideWindowsKeybinds,
            GesturesEnabled = existing.GesturesEnabled,
            Gestures = existing.Gestures
        };

        foreach (var vm in _bindings)
        {
            config.Hotkeys[vm.ConfigKey] = vm.Binding;
        }

        _configManager.Save(config);
        SaveButton.IsEnabled = false;
    }

    private async void OnResetDefaults(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Reset to Defaults",
            Content = "Reset all keybindings to defaults?",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (File.Exists(ConfigManager.ConfigFilePath))
                File.Delete(ConfigManager.ConfigFilePath);

            _configManager?.Reload();
            LoadConfig();
        }
    }

    private static bool IsModifierKey(uint vk) => vk is
        0x5B or 0x5C or    // VK_LWIN, VK_RWIN
        0x10 or 0xA0 or 0xA1 or  // VK_SHIFT, VK_LSHIFT, VK_RSHIFT
        0x11 or 0xA2 or 0xA3 or  // VK_CONTROL, VK_LCONTROL, VK_RCONTROL
        0x12 or 0xA4 or 0xA5;    // VK_MENU, VK_LMENU, VK_RMENU

    private static uint NormalizeModifier(uint vk) => vk switch
    {
        0x5B or 0x5C => 0x5B,        // Win
        0x10 or 0xA0 or 0xA1 => 0x10, // Shift
        0x11 or 0xA2 or 0xA3 => 0x11, // Control
        0x12 or 0xA4 or 0xA5 => 0x12, // Alt/Menu
        _ => vk
    };

    private static string ModifierVkToName(uint vk) => vk switch
    {
        0x5B => "Win",
        0x10 => "Shift",
        0x11 => "Ctrl",
        0x12 => "Alt",
        _ => $"0x{vk:X2}"
    };
}

public class HotkeyBindingViewModel : INotifyPropertyChanged
{
    public string ConfigKey { get; set; } = "";
    public ActionType ActionType { get; set; }
    public string FriendlyName { get; set; } = "";
    public HotkeyBinding Binding { get; set; } = new();
    public bool IsProOnly { get; set; }

    public bool IsChangeEnabled => !IsProOnly;
    public Visibility ProBadgeVisibility => IsProOnly ? Visibility.Visible : Visibility.Collapsed;
    public double RowContentOpacity => IsProOnly ? 0.6 : 1.0;

    public string ShortcutDisplay
    {
        get
        {
            if (Binding.Modifiers.Count == 0 && string.IsNullOrEmpty(Binding.Key))
                return "(none)";
            var parts = new List<string>(Binding.Modifiers);
            if (!string.IsNullOrEmpty(Binding.Key))
            {
                parts.Add(ConfigManager.IsMouseButton(Binding.Key)
                    ? ConfigManager.GetFriendlyMouseButtonName(Binding.Key)
                    : Binding.Key);
            }
            return string.Join(" + ", parts);
        }
    }

    public string ParameterSummary
    {
        get
        {
            if (Binding.Parameters.Count == 0)
                return "";
            return ActionType switch
            {
                ActionType.ResizeWindow =>
                    $"{Binding.Parameters.GetValueOrDefault("Width", 1280):0}x{Binding.Parameters.GetValueOrDefault("Height", 720):0}px",
                ActionType.CenterWindow =>
                    $"{Binding.Parameters.GetValueOrDefault("WidthPercent", 60):0}%x{Binding.Parameters.GetValueOrDefault("HeightPercent", 80):0}%",
                ActionType.CascadeWindows =>
                    Binding.Parameters.GetValueOrDefault("CascadeFromRight", 0) != 0 ? "from right" : "from left",
                ActionType.NudgeUp or ActionType.NudgeDown or ActionType.NudgeLeft or ActionType.NudgeRight =>
                    $"{Binding.Parameters.GetValueOrDefault("Distance", 10):0}px",
                _ => ""
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShortcutDisplay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ParameterSummary)));
    }
}
