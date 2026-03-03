using System.Text.Json;
using Tactadile.Native;

namespace Tactadile.Core.FancyZones;

/// <summary>
/// Reads, parses, and watches FancyZones layout data files.
/// Detects FancyZones installation and provides resolved zone rectangles.
/// </summary>
public sealed class FancyZonesLayoutProvider : IDisposable
{
    private static readonly string FancyZonesDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "PowerToys", "FancyZones");
    private static readonly string CustomLayoutsPath =
        Path.Combine(FancyZonesDataDir, "custom-layouts.json");
    private static readonly string AppliedLayoutsPath =
        Path.Combine(FancyZonesDataDir, "applied-layouts.json");
    private static readonly string LayoutTemplatesPath =
        Path.Combine(FancyZonesDataDir, "layout-templates.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private List<FancyZonesLayout> _customLayouts = new();
    private List<FancyZonesLayout> _templateLayouts = new();
    private List<FancyZonesAppliedLayout> _appliedLayouts = new();
    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private readonly object _lock = new();

    /// <summary>Fires when layout files change on disk.</summary>
    public event Action? LayoutsChanged;

    /// <summary>True if the FancyZones data directory exists.</summary>
    public bool IsAvailable => Directory.Exists(FancyZonesDataDir);

    /// <summary>All custom layouts defined by the user.</summary>
    public IReadOnlyList<FancyZonesLayout> CustomLayouts
    {
        get { lock (_lock) return _customLayouts.AsReadOnly(); }
    }

    /// <summary>All built-in template layouts.</summary>
    public IReadOnlyList<FancyZonesLayout> TemplateLayouts
    {
        get { lock (_lock) return _templateLayouts.AsReadOnly(); }
    }

    /// <summary>All layouts (custom + templates).</summary>
    public IReadOnlyList<FancyZonesLayout> AllLayouts
    {
        get
        {
            lock (_lock)
            {
                var all = new List<FancyZonesLayout>(_customLayouts.Count + _templateLayouts.Count);
                all.AddRange(_customLayouts);
                all.AddRange(_templateLayouts);
                return all.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Initialize: detect FancyZones, load layout files, start watching for changes.
    /// </summary>
    public void Initialize()
    {
        if (!IsAvailable) return;
        LoadAll();
        StartWatching();
    }

    /// <summary>Re-read all layout files from disk.</summary>
    public void Reload()
    {
        LoadAll();
        LayoutsChanged?.Invoke();
    }

    /// <summary>
    /// Finds a layout by UUID across custom layouts and templates.
    /// </summary>
    public FancyZonesLayout? FindLayout(string uuid)
    {
        if (string.IsNullOrEmpty(uuid)) return null;
        lock (_lock)
        {
            return _customLayouts.FirstOrDefault(l =>
                       string.Equals(l.Uuid, uuid, StringComparison.OrdinalIgnoreCase))
                   ?? _templateLayouts.FirstOrDefault(l =>
                       string.Equals(l.Uuid, uuid, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Resolves zones for a specific layout UUID against a monitor work area.
    /// Returns empty list if layout not found or resolution fails.
    /// </summary>
    public List<FancyZonesZoneRect> ResolveZonesForLayout(string layoutUuid, RECT workArea)
    {
        var layout = FindLayout(layoutUuid);
        if (layout == null) return new List<FancyZonesZoneRect>();
        return FancyZonesZoneResolver.ResolveZones(layout, workArea);
    }

    /// <summary>
    /// Gets the zone count for a layout without fully resolving rectangles.
    /// Falls back to resolving against a dummy rect if needed.
    /// </summary>
    public int GetZoneCount(FancyZonesLayout layout)
    {
        try
        {
            if (layout.Type.Equals("canvas", StringComparison.OrdinalIgnoreCase))
            {
                var info = layout.Info.Deserialize<FancyZonesCanvasInfo>(JsonOptions);
                return info?.Zones.Count ?? 0;
            }

            if (layout.Type.Equals("grid", StringComparison.OrdinalIgnoreCase))
            {
                var info = layout.Info.Deserialize<FancyZonesGridInfo>(JsonOptions);
                if (info?.CellChildMap == null) return 0;
                var indices = new HashSet<int>();
                foreach (var row in info.CellChildMap)
                    foreach (var idx in row)
                        indices.Add(idx);
                return indices.Count;
            }
        }
        catch { }

        return 0;
    }

    private void LoadAll()
    {
        var customLayouts = LoadCustomLayouts();
        var templateLayouts = LoadTemplateLayouts();
        var appliedLayouts = LoadAppliedLayouts();

        lock (_lock)
        {
            _customLayouts = customLayouts;
            _templateLayouts = templateLayouts;
            _appliedLayouts = appliedLayouts;
        }
    }

    private static List<FancyZonesLayout> LoadCustomLayouts()
    {
        if (!File.Exists(CustomLayoutsPath))
            return new List<FancyZonesLayout>();

        try
        {
            var json = File.ReadAllText(CustomLayoutsPath);
            var file = JsonSerializer.Deserialize<FancyZonesCustomLayoutsFile>(json, JsonOptions);
            return file?.CustomLayouts ?? new List<FancyZonesLayout>();
        }
        catch
        {
            return new List<FancyZonesLayout>();
        }
    }

    private static List<FancyZonesLayout> LoadTemplateLayouts()
    {
        if (!File.Exists(LayoutTemplatesPath))
            return new List<FancyZonesLayout>();

        try
        {
            var json = File.ReadAllText(LayoutTemplatesPath);
            var file = JsonSerializer.Deserialize<FancyZonesLayoutTemplatesFile>(json, JsonOptions);
            return file?.LayoutTemplates ?? new List<FancyZonesLayout>();
        }
        catch
        {
            return new List<FancyZonesLayout>();
        }
    }

    private static List<FancyZonesAppliedLayout> LoadAppliedLayouts()
    {
        if (!File.Exists(AppliedLayoutsPath))
            return new List<FancyZonesAppliedLayout>();

        try
        {
            var json = File.ReadAllText(AppliedLayoutsPath);
            var file = JsonSerializer.Deserialize<FancyZonesAppliedLayoutsFile>(json, JsonOptions);
            return file?.AppliedLayouts ?? new List<FancyZonesAppliedLayout>();
        }
        catch
        {
            return new List<FancyZonesAppliedLayout>();
        }
    }

    private void StartWatching()
    {
        if (!Directory.Exists(FancyZonesDataDir)) return;

        _watcher = new FileSystemWatcher(FancyZonesDataDir, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: FancyZones may write multiple files rapidly
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ => Reload(), null, 300, Timeout.Infinite);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
    }
}
