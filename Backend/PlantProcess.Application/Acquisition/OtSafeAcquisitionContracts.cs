namespace PlantProcess.Application.Acquisition;

/// <summary>
/// PPIQ-T028: OT-safe edge collector configuration.
/// The platform receives batches only. It never dials into the OT network.
/// </summary>
public sealed record OtSafeEdgeCollectorRegistration(
    Guid CollectorId,
    Guid TenantId,
    string CollectorCode,
    string SiteCode,
    string SourceSystemCode,
    string CredentialFingerprint,
    bool OneWayPushOnly,
    bool NoInboundNetworkPath,
    bool NoControlPath,
    int BufferRetentionHours)
{
    public bool IsOtSafe =>
        OneWayPushOnly &&
        NoInboundNetworkPath &&
        NoControlPath &&
        BufferRetentionHours > 0 &&
        !string.IsNullOrWhiteSpace(CredentialFingerprint);
}

public sealed record OtSafeCollectorStatus(
    Guid CollectorId,
    string CollectorCode,
    string SiteCode,
    DateTimeOffset LastPushAtUtc,
    int LagSeconds,
    int BufferedBatchCount,
    int BufferedRowCount,
    string HealthStatus,
    string NetworkProof,
    string CredentialStatus)
{
    public bool IsHealthy => HealthStatus.Equals("Healthy", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// PPIQ-T028: batch pushed by the customer-side edge collector.
/// </summary>
public sealed record EdgeCollectorPushBatch(
    Guid CollectorId,
    Guid TenantId,
    string SourceSystemCode,
    string SourceObjectName,
    string BatchId,
    DateTimeOffset CapturedFromUtc,
    DateTimeOffset CapturedToUtc,
    IReadOnlyList<EdgeCollectorPushRow> Rows,
    string Signature);

public sealed record EdgeCollectorPushRow(
    int RowNumber,
    DateTimeOffset ObservedAtUtc,
    string TagName,
    string? Unit,
    decimal? NumericValue,
    string? TextValue,
    string QualityFlag,
    string RawJson);

/// <summary>
/// PPIQ-T029: generic historian tag history shape.
/// This is deliberately not steel-specific.
/// </summary>
public sealed record HistorianTagObservation(
    string TagName,
    DateTimeOffset TimestampUtc,
    decimal? NumericValue,
    string? TextValue,
    string? Unit,
    string QualityFlag,
    string SourceRecordId);

public sealed record CanonicalParameterObservationDraft(
    string ParameterCode,
    DateTimeOffset ObservedAtUtc,
    decimal? NumericValue,
    string? TextValue,
    string? Unit,
    string QualityFlag,
    string SourceRecordId);

public sealed record HistorianTagMapping(
    string TagName,
    string ParameterCode,
    string? UnitOverride,
    bool IsActive);

public sealed record SchemaFieldSnapshot(
    string FieldName,
    string DataType,
    string? Unit,
    bool IsRequired);

public sealed record SchemaDriftFinding(
    string FieldName,
    string DriftType,
    string Severity,
    string Before,
    string After,
    bool BlocksIngestion,
    string RemediationPrompt);