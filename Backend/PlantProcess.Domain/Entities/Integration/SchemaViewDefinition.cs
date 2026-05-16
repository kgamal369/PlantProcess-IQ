using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// Represents a controlled SQL view/query definition over PlantProcess IQ raw/staging/canonical tables.
/// 
/// Product purpose:
/// - Let customers build a schema view over raw snapshots/dump rows.
/// - Support safe joins between staging/import/source-dataset metadata.
/// - Prepare data for mapping into the canonical model.
/// 
/// Security boundary:
/// - SQL text must be SELECT/WITH only.
/// - No INSERT/UPDATE/DELETE/DROP/ALTER/etc.
/// - Preview execution is limited by timeout and max rows.
/// </summary>
public class SchemaViewDefinition : BaseEntity
{
    public string SchemaViewCode { get; private set; } = null!;

    public string SchemaViewName { get; private set; } = null!;

    public string ViewKind { get; private set; } = null!;
    // SqlView, JoinView, KpiView, MappingPreparationView

    public Guid? PrimarySourceDatasetDefinitionId { get; private set; }

    public string SqlText { get; private set; } = null!;

    public string SourceDatasetIdsJson { get; private set; } = "[]";

    public string OutputSchemaJson { get; private set; } = "[]";

    public int MaxPreviewRows { get; private set; } = 100;

    public int TimeoutSeconds { get; private set; } = 15;

    public bool IsApproved { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTime? LastValidatedAtUtc { get; private set; }

    public string? LastValidationStatus { get; private set; }

    public string? LastValidationMessage { get; private set; }

    public string? Description { get; private set; }

    private SchemaViewDefinition()
    {
    }

    public SchemaViewDefinition(
        string schemaViewCode,
        string schemaViewName,
        string viewKind,
        string sqlText,
        bool isSynthetic,
        Guid? primarySourceDatasetDefinitionId = null,
        string? sourceDatasetIdsJson = null,
        int maxPreviewRows = 100,
        int timeoutSeconds = 15,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(schemaViewCode))
            throw new ArgumentException("Schema view code is required.", nameof(schemaViewCode));

        if (string.IsNullOrWhiteSpace(schemaViewName))
            throw new ArgumentException("Schema view name is required.", nameof(schemaViewName));

        if (string.IsNullOrWhiteSpace(viewKind))
            throw new ArgumentException("View kind is required.", nameof(viewKind));

        if (string.IsNullOrWhiteSpace(sqlText))
            throw new ArgumentException("SQL text is required.", nameof(sqlText));

        if (maxPreviewRows is < 1 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(maxPreviewRows), "Max preview rows must be between 1 and 5000.");

        if (timeoutSeconds is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Timeout seconds must be between 1 and 120.");

        SchemaViewCode = schemaViewCode.Trim();
        SchemaViewName = schemaViewName.Trim();
        ViewKind = NormalizeViewKind(viewKind);
        SqlText = sqlText.Trim();
        PrimarySourceDatasetDefinitionId = primarySourceDatasetDefinitionId;
        SourceDatasetIdsJson = string.IsNullOrWhiteSpace(sourceDatasetIdsJson)
            ? "[]"
            : sourceDatasetIdsJson.Trim();

        MaxPreviewRows = maxPreviewRows;
        TimeoutSeconds = timeoutSeconds;
        Description = Clean(description);

        IsApproved = false;
        IsActive = true;

        IsSynthetic = isSynthetic;
        SourceSystem = Clean(sourceSystem);
        SourceRecordId = Clean(sourceRecordId);
    }

    public void Update(
        string schemaViewName,
        string viewKind,
        string sqlText,
        Guid? primarySourceDatasetDefinitionId,
        string? sourceDatasetIdsJson,
        int maxPreviewRows,
        int timeoutSeconds,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(schemaViewName))
            throw new ArgumentException("Schema view name is required.", nameof(schemaViewName));

        if (string.IsNullOrWhiteSpace(viewKind))
            throw new ArgumentException("View kind is required.", nameof(viewKind));

        if (string.IsNullOrWhiteSpace(sqlText))
            throw new ArgumentException("SQL text is required.", nameof(sqlText));

        if (maxPreviewRows is < 1 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(maxPreviewRows), "Max preview rows must be between 1 and 5000.");

        if (timeoutSeconds is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Timeout seconds must be between 1 and 120.");

        SchemaViewName = schemaViewName.Trim();
        ViewKind = NormalizeViewKind(viewKind);
        SqlText = sqlText.Trim();
        PrimarySourceDatasetDefinitionId = primarySourceDatasetDefinitionId;
        SourceDatasetIdsJson = string.IsNullOrWhiteSpace(sourceDatasetIdsJson)
            ? "[]"
            : sourceDatasetIdsJson.Trim();

        MaxPreviewRows = maxPreviewRows;
        TimeoutSeconds = timeoutSeconds;
        Description = Clean(description);

        // Any SQL edit resets approval until preview/validation is passed again.
        IsApproved = false;

        MarkAsUpdated();
    }

    public void MarkValidationResult(
        bool isSuccess,
        string message,
        string? outputSchemaJson)
    {
        LastValidatedAtUtc = DateTime.UtcNow;
        LastValidationStatus = isSuccess ? "Success" : "Failed";
        LastValidationMessage = string.IsNullOrWhiteSpace(message)
            ? (isSuccess ? "Schema view validation succeeded." : "Schema view validation failed.")
            : message.Trim();

        if (isSuccess && !string.IsNullOrWhiteSpace(outputSchemaJson))
            OutputSchemaJson = outputSchemaJson.Trim();

        MarkAsUpdated();
    }

    public void Approve()
    {
        IsApproved = true;
        MarkAsUpdated();
    }

    public void RevokeApproval()
    {
        IsApproved = false;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    private static string NormalizeViewKind(string value)
    {
        var normalized = value.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "sql" => "SqlView",
            "sqlview" => "SqlView",
            "join" => "JoinView",
            "joinview" => "JoinView",
            "kpi" => "KpiView",
            "kpiview" => "KpiView",
            "mapping" => "MappingPreparationView",
            "mappingpreparationview" => "MappingPreparationView",
            _ => normalized
        };
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}