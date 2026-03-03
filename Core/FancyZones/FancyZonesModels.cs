using System.Text.Json.Serialization;

namespace Tactadile.Core.FancyZones;

// ── Resolved output ──

public readonly record struct FancyZonesZoneRect(int X, int Y, int Width, int Height);

// ── custom-layouts.json ──

public sealed class FancyZonesCustomLayoutsFile
{
    [JsonPropertyName("custom-layouts")]
    public List<FancyZonesLayout> CustomLayouts { get; set; } = new();
}

public sealed class FancyZonesLayout
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // "canvas" or "grid"

    [JsonPropertyName("info")]
    public System.Text.Json.JsonElement Info { get; set; }
}

public sealed class FancyZonesCanvasInfo
{
    [JsonPropertyName("ref-width")]
    public int RefWidth { get; set; }

    [JsonPropertyName("ref-height")]
    public int RefHeight { get; set; }

    [JsonPropertyName("zones")]
    public List<FancyZonesCanvasZone> Zones { get; set; } = new();

    [JsonPropertyName("sensitivity-radius")]
    public int SensitivityRadius { get; set; }
}

public sealed class FancyZonesCanvasZone
{
    public int X { get; set; }
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public sealed class FancyZonesGridInfo
{
    [JsonPropertyName("rows")]
    public int Rows { get; set; }

    [JsonPropertyName("columns")]
    public int Columns { get; set; }

    [JsonPropertyName("rows-percentage")]
    public List<int> RowsPercentage { get; set; } = new();

    [JsonPropertyName("columns-percentage")]
    public List<int> ColumnsPercentage { get; set; } = new();

    [JsonPropertyName("cell-child-map")]
    public List<List<int>> CellChildMap { get; set; } = new();

    [JsonPropertyName("show-spacing")]
    public bool ShowSpacing { get; set; }

    [JsonPropertyName("spacing")]
    public int Spacing { get; set; }

    [JsonPropertyName("sensitivity-radius")]
    public int SensitivityRadius { get; set; }
}

// ── applied-layouts.json ──

public sealed class FancyZonesAppliedLayoutsFile
{
    [JsonPropertyName("applied-layouts")]
    public List<FancyZonesAppliedLayout> AppliedLayouts { get; set; } = new();
}

public sealed class FancyZonesAppliedLayout
{
    [JsonPropertyName("device")]
    public FancyZonesDevice Device { get; set; } = new();

    [JsonPropertyName("applied-layout")]
    public FancyZonesLayoutRef AppliedLayoutRef { get; set; } = new();
}

public sealed class FancyZonesDevice
{
    [JsonPropertyName("monitor")]
    public string Monitor { get; set; } = "";

    [JsonPropertyName("monitor-id")]
    public string MonitorId { get; set; } = "";

    [JsonPropertyName("virtual-desktop")]
    public string VirtualDesktop { get; set; } = "";
}

public sealed class FancyZonesLayoutRef
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // "custom" or "blank" or template name
}

// ── layout-templates.json ──

public sealed class FancyZonesLayoutTemplatesFile
{
    [JsonPropertyName("layout-templates")]
    public List<FancyZonesLayout> LayoutTemplates { get; set; } = new();
}
