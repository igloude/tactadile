using System.Text.Json;
using Tactadile.Native;

namespace Tactadile.Core.FancyZones;

/// <summary>
/// Resolves FancyZones layout definitions (canvas or grid) into pixel rectangles
/// for a given monitor work area.
/// </summary>
public static class FancyZonesZoneResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Resolves a layout's zones to pixel rectangles for the given work area.
    /// Returns empty list if the layout type is unknown or data is invalid.
    /// </summary>
    public static List<FancyZonesZoneRect> ResolveZones(FancyZonesLayout layout, RECT workArea)
    {
        try
        {
            return layout.Type.ToLowerInvariant() switch
            {
                "canvas" => ResolveCanvasZones(
                    layout.Info.Deserialize<FancyZonesCanvasInfo>(JsonOptions)!, workArea),
                "grid" => ResolveGridZones(
                    layout.Info.Deserialize<FancyZonesGridInfo>(JsonOptions)!, workArea),
                _ => new List<FancyZonesZoneRect>()
            };
        }
        catch
        {
            return new List<FancyZonesZoneRect>();
        }
    }

    /// <summary>
    /// Resolves canvas layout zones by scaling from reference resolution to actual work area.
    /// </summary>
    public static List<FancyZonesZoneRect> ResolveCanvasZones(FancyZonesCanvasInfo info, RECT workArea)
    {
        if (info.RefWidth <= 0 || info.RefHeight <= 0)
            return new List<FancyZonesZoneRect>();

        int areaW = workArea.Width;
        int areaH = workArea.Height;
        double scaleX = (double)areaW / info.RefWidth;
        double scaleY = (double)areaH / info.RefHeight;

        var zones = new List<FancyZonesZoneRect>(info.Zones.Count);
        foreach (var z in info.Zones)
        {
            zones.Add(new FancyZonesZoneRect(
                X: workArea.Left + (int)(z.X * scaleX),
                Y: workArea.Top + (int)(z.Y * scaleY),
                Width: (int)(z.Width * scaleX),
                Height: (int)(z.Height * scaleY)));
        }

        return zones;
    }

    /// <summary>
    /// Resolves grid layout zones by computing percentage-based rows/columns,
    /// then merging cells that share a zone index via the cell-child-map.
    /// </summary>
    public static List<FancyZonesZoneRect> ResolveGridZones(FancyZonesGridInfo info, RECT workArea)
    {
        if (info.Rows <= 0 || info.Columns <= 0)
            return new List<FancyZonesZoneRect>();
        if (info.RowsPercentage.Count != info.Rows || info.ColumnsPercentage.Count != info.Columns)
            return new List<FancyZonesZoneRect>();
        if (info.CellChildMap.Count != info.Rows)
            return new List<FancyZonesZoneRect>();

        int areaW = workArea.Width;
        int areaH = workArea.Height;
        int spacing = info.ShowSpacing ? info.Spacing : 0;

        // Compute row pixel heights from percentages (sum ~10000)
        int totalRowPct = info.RowsPercentage.Sum();
        if (totalRowPct <= 0) totalRowPct = 10000;
        var rowHeights = new int[info.Rows];
        int usedH = 0;
        for (int r = 0; r < info.Rows; r++)
        {
            if (r == info.Rows - 1)
                rowHeights[r] = areaH - usedH - spacing * (info.Rows - 1);
            else
                rowHeights[r] = areaH * info.RowsPercentage[r] / totalRowPct;
            usedH += rowHeights[r];
        }

        // Compute column pixel widths from percentages
        int totalColPct = info.ColumnsPercentage.Sum();
        if (totalColPct <= 0) totalColPct = 10000;
        var colWidths = new int[info.Columns];
        int usedW = 0;
        for (int c = 0; c < info.Columns; c++)
        {
            if (c == info.Columns - 1)
                colWidths[c] = areaW - usedW - spacing * (info.Columns - 1);
            else
                colWidths[c] = areaW * info.ColumnsPercentage[c] / totalColPct;
            usedW += colWidths[c];
        }

        // Build cell positions
        var cellX = new int[info.Columns];
        cellX[0] = workArea.Left;
        for (int c = 1; c < info.Columns; c++)
            cellX[c] = cellX[c - 1] + colWidths[c - 1] + spacing;

        var cellY = new int[info.Rows];
        cellY[0] = workArea.Top;
        for (int r = 1; r < info.Rows; r++)
            cellY[r] = cellY[r - 1] + rowHeights[r - 1] + spacing;

        // Find all unique zone indices and compute bounding boxes
        var zoneBounds = new Dictionary<int, (int minX, int minY, int maxX, int maxY)>();

        for (int r = 0; r < info.Rows; r++)
        {
            if (info.CellChildMap[r].Count != info.Columns)
                continue;

            for (int c = 0; c < info.Columns; c++)
            {
                int zoneIndex = info.CellChildMap[r][c];
                int x1 = cellX[c];
                int y1 = cellY[r];
                int x2 = x1 + colWidths[c];
                int y2 = y1 + rowHeights[r];

                if (zoneBounds.TryGetValue(zoneIndex, out var existing))
                {
                    zoneBounds[zoneIndex] = (
                        Math.Min(existing.minX, x1),
                        Math.Min(existing.minY, y1),
                        Math.Max(existing.maxX, x2),
                        Math.Max(existing.maxY, y2));
                }
                else
                {
                    zoneBounds[zoneIndex] = (x1, y1, x2, y2);
                }
            }
        }

        // Sort by zone index and convert to rects
        var zones = new List<FancyZonesZoneRect>(zoneBounds.Count);
        foreach (var kvp in zoneBounds.OrderBy(kvp => kvp.Key))
        {
            var (minX, minY, maxX, maxY) = kvp.Value;
            zones.Add(new FancyZonesZoneRect(minX, minY, maxX - minX, maxY - minY));
        }

        return zones;
    }
}
