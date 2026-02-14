using WinMove.Config;

namespace WinMove.UI;

public sealed class SettingsForm : Form
{
    private readonly ConfigManager _configManager;
    private readonly DataGridView _grid;
    private readonly Button _saveButton;
    private readonly Button _resetButton;
    private readonly Button _exportButton;
    private readonly Button _importButton;

    // Hotkey capture state
    private int _capturingRowIndex = -1;
    private Keys _capturedKey = Keys.None;
    private Keys _capturedModifiers = Keys.None;

    public SettingsForm(ConfigManager configManager)
    {
        _configManager = configManager;

        Text = "win-move Settings";
        Size = new Size(620, 500);
        MinimumSize = new Size(500, 350);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        KeyPreview = true;

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Action",
            HeaderText = "Action",
            FillWeight = 40,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Shortcut",
            HeaderText = "Shortcut",
            FillWeight = 35,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Edit",
            HeaderText = "",
            Text = "Change",
            UseColumnTextForButtonValue = true,
            FillWeight = 12,
            FlatStyle = FlatStyle.System
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Clear",
            HeaderText = "",
            Text = "Clear",
            UseColumnTextForButtonValue = true,
            FillWeight = 10,
            FlatStyle = FlatStyle.System
        });
        // Hidden columns for data
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ConfigKey", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ActionEnum", Visible = false });

        _grid.CellClick += OnCellClick;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(4)
        };

        _saveButton = new Button { Text = "Save", Width = 80 };
        _saveButton.Click += OnSave;

        _resetButton = new Button { Text = "Reset to Defaults", Width = 120 };
        _resetButton.Click += OnResetDefaults;

        _exportButton = new Button { Text = "Export...", Width = 80 };
        _exportButton.Click += OnExport;

        _importButton = new Button { Text = "Import...", Width = 80 };
        _importButton.Click += OnImport;

        buttonPanel.Controls.Add(_saveButton);
        buttonPanel.Controls.Add(_resetButton);
        buttonPanel.Controls.Add(_importButton);
        buttonPanel.Controls.Add(_exportButton);

        Controls.Add(_grid);
        Controls.Add(buttonPanel);

        LoadConfig();
    }

    private void LoadConfig()
    {
        _grid.Rows.Clear();
        var config = _configManager.CurrentConfig;

        foreach (var (key, binding) in config.Hotkeys)
        {
            if (!ConfigManager.TryParseAction(binding.Action, out var actionType))
                continue;

            string friendlyName = ConfigManager.GetFriendlyActionName(actionType);
            string shortcutDisplay = string.IsNullOrEmpty(binding.Key)
                ? "(none)"
                : FormatShortcut(binding.Modifiers, binding.Key);

            int rowIndex = _grid.Rows.Add(friendlyName, shortcutDisplay, "Change", "Clear", key, binding.Action);
            _grid.Rows[rowIndex].Tag = binding;
        }
    }

    private void OnCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var columnName = _grid.Columns[e.ColumnIndex].Name;

        if (columnName == "Clear")
        {
            // Cancel any active capture first
            if (_capturingRowIndex >= 0) CancelCapture();

            // Clear the hotkey binding
            _grid.Rows[e.RowIndex].Cells["Shortcut"].Value = "(none)";
            var binding = new HotkeyBinding
            {
                Modifiers = new List<string>(),
                Key = "",
                Action = _grid.Rows[e.RowIndex].Cells["ActionEnum"].Value?.ToString() ?? ""
            };
            _grid.Rows[e.RowIndex].Tag = binding;
            return;
        }

        if (columnName != "Edit") return;

        // Enter capture mode
        _capturingRowIndex = e.RowIndex;
        _capturedKey = Keys.None;
        _capturedModifiers = Keys.None;
        _grid.Rows[e.RowIndex].Cells["Shortcut"].Value = "Press a key combo...";
        _grid.Rows[e.RowIndex].Cells["Edit"].Value = "Cancel";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_capturingRowIndex < 0)
            return base.ProcessCmdKey(ref msg, keyData);

        // Extract modifiers and key
        var modifiers = keyData & Keys.Modifiers;
        var key = keyData & Keys.KeyCode;

        // Ignore if only modifiers are pressed (no primary key yet)
        if (key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return true; // Swallow the event

        // Escape cancels capture
        if (key == Keys.Escape)
        {
            CancelCapture();
            return true;
        }

        _capturedModifiers = modifiers;
        _capturedKey = key;

        // Update the display
        var modList = new List<string>();
        if (modifiers.HasFlag(Keys.Control)) modList.Add("Ctrl");
        if (modifiers.HasFlag(Keys.Shift)) modList.Add("Shift");
        if (modifiers.HasFlag(Keys.Alt)) modList.Add("Alt");

        // Check for Win key via GetAsyncKeyState
        bool winPressed = (Native.NativeMethods.GetAsyncKeyState(0x5B) & 0x8000) != 0
                       || (Native.NativeMethods.GetAsyncKeyState(0x5C) & 0x8000) != 0;
        if (winPressed) modList.Add("Win");

        string display = FormatShortcut(modList, key.ToString());
        _grid.Rows[_capturingRowIndex].Cells["Shortcut"].Value = display;
        _grid.Rows[_capturingRowIndex].Cells["Edit"].Value = "Change";

        // Store the captured binding on the row tag
        var binding = new HotkeyBinding
        {
            Modifiers = modList,
            Key = key.ToString(),
            Action = _grid.Rows[_capturingRowIndex].Cells["ActionEnum"].Value?.ToString() ?? ""
        };
        _grid.Rows[_capturingRowIndex].Tag = binding;

        _capturingRowIndex = -1;
        return true; // Swallow the key
    }

    private void CancelCapture()
    {
        if (_capturingRowIndex < 0) return;
        var binding = _grid.Rows[_capturingRowIndex].Tag as HotkeyBinding;
        if (binding != null)
        {
            _grid.Rows[_capturingRowIndex].Cells["Shortcut"].Value =
                FormatShortcut(binding.Modifiers, binding.Key);
        }
        _grid.Rows[_capturingRowIndex].Cells["Edit"].Value = "Change";
        _capturingRowIndex = -1;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var config = new AppConfig { Version = 1 };

        foreach (DataGridViewRow row in _grid.Rows)
        {
            var configKey = row.Cells["ConfigKey"].Value?.ToString();
            var binding = row.Tag as HotkeyBinding;
            if (configKey != null && binding != null)
            {
                config.Hotkeys[configKey] = binding;
            }
        }

        _configManager.Save(config);
        // Balloon tip notification is triggered by ConfigChanged event in TrayApplicationContext
    }

    private void OnResetDefaults(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Reset all keybindings to defaults?", "win-move",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            // Delete config to trigger default recreation
            if (File.Exists(ConfigManager.ConfigFilePath))
                File.Delete(ConfigManager.ConfigFilePath);

            // Reload will recreate defaults
            _configManager.Reload();
            LoadConfig();
        }
    }

    private void OnExport(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = "win-move-config.json",
            Title = "Export Configuration"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            ConfigManager.ExportConfig(dialog.FileName);
            MessageBox.Show("Configuration exported.", "win-move",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnImport(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "Import Configuration"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _configManager.ImportConfig(dialog.FileName);
            LoadConfig();
            MessageBox.Show("Configuration imported and applied.", "win-move",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static string FormatShortcut(List<string> modifiers, string key)
    {
        var parts = new List<string>(modifiers) { key };
        return string.Join(" + ", parts);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Just hide instead of close to preserve singleton behavior
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }
}
