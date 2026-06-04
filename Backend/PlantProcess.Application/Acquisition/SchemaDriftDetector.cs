namespace PlantProcess.Application.Acquisition;

/// <summary>
/// PPIQ-T030:
/// Detects source schema drift without silently producing nulls.
/// Removed or type-changed fields block ingestion. Added optional fields are warnings.
/// </summary>
public sealed class SchemaDriftDetector
{
    public IReadOnlyList<SchemaDriftFinding> Detect(
        IReadOnlyList<SchemaFieldSnapshot> expected,
        IReadOnlyList<SchemaFieldSnapshot> actual)
    {
        var findings = new List<SchemaDriftFinding>();

        var expectedByName = expected.ToDictionary(x => x.FieldName, StringComparer.OrdinalIgnoreCase);
        var actualByName = actual.ToDictionary(x => x.FieldName, StringComparer.OrdinalIgnoreCase);

        foreach (var expectedField in expectedByName.Values)
        {
            if (!actualByName.TryGetValue(expectedField.FieldName, out var actualField))
            {
                findings.Add(new SchemaDriftFinding(
                    expectedField.FieldName,
                    "Removed",
                    expectedField.IsRequired ? "Critical" : "Warning",
                    expectedField.DataType,
                    "<missing>",
                    expectedField.IsRequired,
                    $"Source field '{expectedField.FieldName}' disappeared. Review mapping before next ingestion."));
                continue;
            }

            if (!expectedField.DataType.Equals(actualField.DataType, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new SchemaDriftFinding(
                    expectedField.FieldName,
                    "TypeChanged",
                    "Critical",
                    expectedField.DataType,
                    actualField.DataType,
                    BlocksIngestion: true,
                    $"Source field '{expectedField.FieldName}' changed type from {expectedField.DataType} to {actualField.DataType}. Remap or cast explicitly."));
            }

            if (!string.Equals(expectedField.Unit, actualField.Unit, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new SchemaDriftFinding(
                    expectedField.FieldName,
                    "UnitChanged",
                    "Warning",
                    expectedField.Unit ?? "<none>",
                    actualField.Unit ?? "<none>",
                    BlocksIngestion: false,
                    $"Source field '{expectedField.FieldName}' unit changed. Confirm unit conversion before canonical mapping."));
            }
        }

        foreach (var actualField in actualByName.Values)
        {
            if (!expectedByName.ContainsKey(actualField.FieldName))
            {
                findings.Add(new SchemaDriftFinding(
                    actualField.FieldName,
                    "Added",
                    "Info",
                    "<missing>",
                    actualField.DataType,
                    BlocksIngestion: false,
                    $"New source field '{actualField.FieldName}' detected. Map it or explicitly ignore it."));
            }
        }

        return findings
            .OrderByDescending(x => x.BlocksIngestion)
            .ThenBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}