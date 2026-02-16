using System.Globalization;
using System.Reflection;

namespace Tactadile.Licensing;

public static class BuildMetadata
{
    public static readonly DateTime BuildDateUtc;

    static BuildMetadata()
    {
        var attr = typeof(BuildMetadata).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDateUtc");

        if (attr != null && DateTime.TryParse(attr.Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var dt))
        {
            BuildDateUtc = dt;
        }
        else
        {
            // Dev/debug builds without MSBuild injection — never blocks updates.
            BuildDateUtc = DateTime.MinValue;
        }
    }
}
