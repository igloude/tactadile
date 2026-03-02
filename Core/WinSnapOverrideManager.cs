using Microsoft.Win32;
using System.Text.Json;
using Tactadile.Config;
using Tactadile.Native;

namespace Tactadile.Core;

public sealed class WinSnapOverrideManager : IDisposable
{
    private static readonly string StateFilePath = Path.Combine(
        ConfigManager.ConfigDirectory, "snap-override-state.json");

    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    private bool _isOverrideActive;
    private SnapState? _savedState;

    /// <summary>
    /// On startup, restores original settings if a previous session crashed
    /// with overrides still active (sentinel file exists).
    /// </summary>
    public void RecoverIfNeeded()
    {
        if (!File.Exists(StateFilePath)) return;

        try
        {
            var state = ReadStateFile();
            if (state != null)
                RestoreState(state);
        }
        catch { /* Corrupt file — cannot restore, just clean up */ }
        finally
        {
            DeleteStateFile();
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
            Enable();
        else
            Disable();
    }

    public void Enable()
    {
        if (_isOverrideActive) return;

        _savedState = CaptureCurrentState();
        WriteStateFile(_savedState);
        ApplyOverrides();
        _isOverrideActive = true;
    }

    public void Disable()
    {
        if (!_isOverrideActive) return;

        if (_savedState != null)
            RestoreState(_savedState);

        DeleteStateFile();
        _savedState = null;
        _isOverrideActive = false;
    }

    public void Dispose() => Disable();

    // ── Private helpers ─────────────────────────────────────────────

    private static SnapState CaptureCurrentState()
    {
        int winArrangement = 0;
        NativeMethods.SystemParametersInfo(
            NativeConstants.SPI_GETWINARRANGEMENT, 0, ref winArrangement, 0);

        return new SnapState
        {
            WinArrangement = winArrangement,
            SnapAssistFlyout = ReadRegistryDword("EnableSnapAssistFlyout"),
            SnapAssist = ReadRegistryDword("SnapAssist"),
            DisallowShaking = ReadRegistryDword("DisallowShaking"),
        };
    }

    private static void ApplyOverrides()
    {
        // Aero Snap + Win+Arrow (immediate via SPIF_SENDCHANGE)
        int disabled = 0;
        NativeMethods.SystemParametersInfo(
            NativeConstants.SPI_SETWINARRANGEMENT, 0, ref disabled,
            NativeConstants.SPIF_SENDCHANGE);

        // Snap Layouts (Win11 maximize hover)
        WriteRegistryDword("EnableSnapAssistFlyout", 0);

        // Snap Assist (window suggestions after snap)
        WriteRegistryDword("SnapAssist", 0);

        // Aero Shake (inverted: 1 = shake disabled)
        WriteRegistryDword("DisallowShaking", 1);

        BroadcastSettingChange();
    }

    private static void RestoreState(SnapState state)
    {
        // Aero Snap + Win+Arrow
        int value = state.WinArrangement;
        NativeMethods.SystemParametersInfo(
            NativeConstants.SPI_SETWINARRANGEMENT, 0, ref value,
            NativeConstants.SPIF_SENDCHANGE);

        // Registry values: restore original or delete if they didn't exist
        RestoreRegistryValue("EnableSnapAssistFlyout", state.SnapAssistFlyout);
        RestoreRegistryValue("SnapAssist", state.SnapAssist);
        RestoreRegistryValue("DisallowShaking", state.DisallowShaking);

        BroadcastSettingChange();
    }

    private static void BroadcastSettingChange()
    {
        NativeMethods.SendMessageTimeout(
            NativeConstants.HWND_BROADCAST, NativeConstants.WM_SETTINGCHANGE,
            IntPtr.Zero, "Policy", NativeConstants.SMTO_ABORTIFHUNG, 1000, out _);
    }

    // ── Registry helpers ────────────────────────────────────────────

    private static int? ReadRegistryDword(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey, false);
        var value = key?.GetValue(valueName);
        return value is int i ? i : null;
    }

    private static void WriteRegistryDword(string valueName, int value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey, true);
        key?.SetValue(valueName, value, RegistryValueKind.DWord);
    }

    private static void RestoreRegistryValue(string valueName, int? originalValue)
    {
        if (originalValue.HasValue)
            WriteRegistryDword(valueName, originalValue.Value);
        else
            DeleteRegistryValue(valueName);
    }

    private static void DeleteRegistryValue(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey, true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    // ── State file helpers ──────────────────────────────────────────

    private static void WriteStateFile(SnapState state)
    {
        var json = JsonSerializer.Serialize(state);
        File.WriteAllText(StateFilePath, json);
    }

    private static SnapState? ReadStateFile()
    {
        var json = File.ReadAllText(StateFilePath);
        return JsonSerializer.Deserialize<SnapState>(json);
    }

    private static void DeleteStateFile()
    {
        try { File.Delete(StateFilePath); }
        catch { /* best effort */ }
    }

    private sealed class SnapState
    {
        public int WinArrangement { get; set; }
        public int? SnapAssistFlyout { get; set; }
        public int? SnapAssist { get; set; }
        public int? DisallowShaking { get; set; }
    }
}
