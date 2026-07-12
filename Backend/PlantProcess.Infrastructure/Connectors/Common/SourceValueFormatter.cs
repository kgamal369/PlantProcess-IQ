using System;
using System.Globalization;

namespace PlantProcess.Infrastructure.Connectors.Common;

// Generic, provider-agnostic serialisation of a source value to its cursor/text form.
// DateTime and DateTimeOffset are emitted as ISO-8601 round-trip ("o") so that:
//  (1) the value sent back to the remote source as an incremental cursor is unambiguous
//      regardless of the remote server datestyle, and
//  (2) any downstream culture-sensitive parse is deterministic on any machine locale.
public static class SourceValueFormatter
{
    public static string? Format(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }
}
