using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Acquisition;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.SourceSystems;

/// <summary>
/// T-025: detects upstream schema drift for a source dataset by comparing the
/// persisted field baseline (SourceFieldDefinition) against the live source schema,
/// and degrades dependent mappings so nothing fails silently. Reuses the existing
/// SchemaDriftDetector. Migration-free: degradation = deactivating the affected
/// mapping, with the drifted column named in the finding and the log.
/// </summary>
public interface IConnectorSchemaDriftService
{
    Task<SchemaDriftCheckResult> CheckDatasetAsync(
        Guid datasetId,
        bool degradeAffectedMappings,
        CancellationToken cancellationToken);
}

public sealed record SchemaDriftCheckResult
{
    public string DatasetCode { get; set; } = string.Empty;
    public bool BaselineExisted { get; set; }
    public int BaselineFieldCount { get; set; }
    public int LiveFieldCount { get; set; }
    public IReadOnlyList<SchemaDriftFinding> Findings { get; set; } = new List<SchemaDriftFinding>();
    public IReadOnlyList<string> DegradedMappingCodes { get; set; } = new List<string>();
    public string? ErrorMessage { get; set; }
}

public sealed class ConnectorSchemaDriftService : IConnectorSchemaDriftService
{
    private static readonly SchemaDriftDetector Detector = new();

    private readonly IPlantProcessDbContext _dbContext;
    private readonly IDataSourceConnectorFactory _connectorFactory;
    private readonly ILogger<ConnectorSchemaDriftService> _logger;

    public ConnectorSchemaDriftService(
        IPlantProcessDbContext dbContext,
        IDataSourceConnectorFactory connectorFactory,
        ILogger<ConnectorSchemaDriftService> logger)
    {
        _dbContext = dbContext;
        _connectorFactory = connectorFactory;
        _logger = logger;
    }

    public async Task<SchemaDriftCheckResult> CheckDatasetAsync(
        Guid datasetId,
        bool degradeAffectedMappings,
        CancellationToken cancellationToken)
    {
        var result = new SchemaDriftCheckResult();

        var dataset = await _dbContext.SourceDatasetDefinitions
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken);
        if (dataset is null)
        {
            result.ErrorMessage = "Dataset not found.";
            return result;
        }

        result.DatasetCode = dataset.DatasetCode;

        var profile = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == dataset.ConnectionProfileId, cancellationToken);
        if (profile is null || !profile.IsActive)
        {
            result.ErrorMessage = "Connection profile not found or inactive.";
            return result;
        }

        var sourceSystem = await _dbContext.SourceSystemDefinitions
            .FirstOrDefaultAsync(x => x.Id == profile.SourceSystemDefinitionId, cancellationToken);
        if (sourceSystem is null)
        {
            result.ErrorMessage = "Source system definition not found.";
            return result;
        }

        // Baseline = persisted field definitions captured at discovery.
        var baselineRows = await _dbContext.SourceFieldDefinitions
            .Where(x => x.SourceDatasetDefinitionId == dataset.Id && x.IsActive)
            .OrderBy(x => x.Ordinal)
            .ToListAsync(cancellationToken);

        result.BaselineExisted = baselineRows.Count > 0;
        result.BaselineFieldCount = baselineRows.Count;

        if (baselineRows.Count == 0)
        {
            result.ErrorMessage = "No persisted field baseline for this dataset. Run schema discovery with field persistence first.";
            return result;
        }

        IReadOnlyList<DiscoveredSourceField> liveFields;
        try
        {
            var schemaReader = _connectorFactory.GetSchemaReader(profile.ProviderType);
            liveFields = await schemaReader.DiscoverFieldsForDatasetAsync(profile, dataset, cancellationToken);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = "Live schema discovery failed: " + ex.Message;
            _logger.LogError(ex, "Schema drift live discovery failed for dataset {DatasetCode}", dataset.DatasetCode);
            return result;
        }

        result.LiveFieldCount = liveFields.Count;

        var expected = baselineRows
            .Select(x => new SchemaFieldSnapshot(x.FieldName, x.SourceDataType, null, !x.IsNullable))
            .ToList();

        var actual = liveFields
            .Select(f => new SchemaFieldSnapshot(f.FieldName, f.SourceDataType, null, !f.IsNullable))
            .ToList();

        var findings = Detector.Detect(expected, actual);
        result.Findings = findings;

        if (findings.Count == 0)
        {
            return result;
        }

        _logger.LogWarning(
            "Schema drift detected on dataset {DatasetCode}: {Count} finding(s) [{Fields}]",
            dataset.DatasetCode,
            findings.Count,
            string.Join(", ", findings.Select(f => f.FieldName + ":" + f.DriftType)));

        if (!degradeAffectedMappings)
        {
            return result;
        }

        var driftedFields = findings
            .Select(f => f.FieldName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidateMappings = await _dbContext.MappingDefinitions
            .Where(m => m.SourceSystemDefinitionId == sourceSystem.Id
                && m.SourceObjectName == dataset.SourceObjectName
                && m.IsActive)
            .ToListAsync(cancellationToken);

        var degraded = new List<string>();
        foreach (var mapping in candidateMappings)
        {
            var referenced = driftedFields.FirstOrDefault(field => MappingReferencesField(mapping.MappingJson, field));
            if (referenced is null)
            {
                continue;
            }

            mapping.Deactivate();
            degraded.Add(mapping.MappingCode);

            _logger.LogWarning(
                "Mapping {MappingCode} degraded (deactivated): source column {Field} drifted on dataset {DatasetCode}.",
                mapping.MappingCode,
                referenced,
                dataset.DatasetCode);
        }

        if (degraded.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        result.DegradedMappingCodes = degraded;
        return result;
    }

    /// <summary>
    /// True when the mapping configuration references the given source field by name
    /// (as a JSON property name or string value), case-insensitively.
    /// </summary>
    public static bool MappingReferencesField(string? mappingJson, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(mappingJson) || string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(mappingJson);
            return ElementReferences(doc.RootElement, fieldName);
        }
        catch (JsonException)
        {
            return mappingJson.Contains(fieldName, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool ElementReferences(JsonElement element, string fieldName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (ElementReferences(prop.Value, fieldName))
                    {
                        return true;
                    }
                }

                return false;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ElementReferences(item, fieldName))
                    {
                        return true;
                    }
                }

                return false;

            case JsonValueKind.String:
                return string.Equals(element.GetString(), fieldName, StringComparison.OrdinalIgnoreCase);

            default:
                return false;
        }
    }
}