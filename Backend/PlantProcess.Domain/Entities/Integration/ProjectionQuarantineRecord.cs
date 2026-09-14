using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// PPIQ T-099. A staged row that could not lawfully become canonical, kept as
/// typed evidence instead of as a sentence in a log.
///
/// THE CODE IS AUTHORITATIVE AND THE DETAIL IS EXPLANATORY. Consumers group,
/// count and route on ValidationCode; Detail exists so a person reads the same
/// fact in one line. Nothing here is derived from an exception message.
///
/// The record carries the exact producing mapping AND its version snapshot,
/// because reprocessing under a different version answers a different question
/// and the evidence must be able to say which one it answered.
///
/// There is no new staging state machine. A quarantined staging row stays
/// represented by its existing Failed processing state; this record is the
/// typed evidence beside it.
/// </summary>
public class ProjectionQuarantineRecord : BaseEntity
{
    public const string StateOpen = "Open";
    public const string StateResolved = "Resolved";

    public string ValidationCode { get; private set; } = null!;

    public string Detail { get; private set; } = null!;

    public string? OffendingValue { get; private set; }

    public string SuggestedCorrection { get; private set; } = null!;

    public Guid ImportBatchId { get; private set; }

    public Guid StagingRecordId { get; private set; }

    public int StagingRowNumber { get; private set; }

    public string SourceObjectName { get; private set; } = null!;

    public Guid MappingDefinitionId { get; private set; }

    public string MappingVersion { get; private set; } = null!;

    public string TargetEntityName { get; private set; } = null!;

    public Guid TenantId { get; private set; }

    public string State { get; private set; } = StateOpen;

    public int AttemptCount { get; private set; } = 1;

    public DateTime LastAttemptAtUtc { get; private set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; private set; }

    public Guid? ResolvedCanonicalId { get; private set; }

    private ProjectionQuarantineRecord()
    {
    }

    public ProjectionQuarantineRecord(
        ProjectionValidationCode validationCode,
        string detail,
        string? offendingValue,
        Guid importBatchId,
        Guid stagingRecordId,
        int stagingRowNumber,
        string sourceObjectName,
        Guid mappingDefinitionId,
        string mappingVersion,
        string targetEntityName,
        Guid tenantId,
        bool isSynthetic,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(detail))
            throw new ArgumentException("Detail is required.", nameof(detail));

        if (importBatchId == Guid.Empty)
            throw new ArgumentException("Import batch ID is required.", nameof(importBatchId));

        if (stagingRecordId == Guid.Empty)
            throw new ArgumentException("Staging record ID is required.", nameof(stagingRecordId));

        if (stagingRowNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(stagingRowNumber), "Row number must be greater than zero.");

        if (string.IsNullOrWhiteSpace(sourceObjectName))
            throw new ArgumentException("Source object name is required.", nameof(sourceObjectName));

        if (mappingDefinitionId == Guid.Empty)
            throw new ArgumentException("Mapping definition ID is required.", nameof(mappingDefinitionId));

        if (string.IsNullOrWhiteSpace(mappingVersion))
            throw new ArgumentException("Mapping version is required.", nameof(mappingVersion));

        if (string.IsNullOrWhiteSpace(targetEntityName))
            throw new ArgumentException("Target entity name is required.", nameof(targetEntityName));

        // Never a fallback GUID: quarantine evidence written under an invented
        // tenant would look governed and be untraceable.
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        ValidationCode = validationCode.ToString();
        Detail = detail.Trim();
        OffendingValue = offendingValue;
        SuggestedCorrection = ProjectionValidationCorrection.For(validationCode);
        ImportBatchId = importBatchId;
        StagingRecordId = stagingRecordId;
        StagingRowNumber = stagingRowNumber;
        SourceObjectName = sourceObjectName.Trim();
        MappingDefinitionId = mappingDefinitionId;
        MappingVersion = mappingVersion.Trim();
        TargetEntityName = targetEntityName.Trim();
        TenantId = tenantId;
        State = StateOpen;
        AttemptCount = 1;
        LastAttemptAtUtc = DateTime.UtcNow;
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    /// <summary>
    /// The same row refused again. The record stays Open, the attempt is
    /// counted, and the evidence is refreshed to the refusal that actually
    /// happened this time rather than kept at the first one.
    /// </summary>
    public void RecordFailedAttempt(ProjectionValidationCode validationCode, string detail, string? offendingValue)
    {
        if (State == StateResolved)
            throw new InvalidOperationException("A resolved quarantine record cannot record a failed attempt.");

        if (string.IsNullOrWhiteSpace(detail))
            throw new ArgumentException("Detail is required.", nameof(detail));

        ValidationCode = validationCode.ToString();
        Detail = detail.Trim();
        OffendingValue = offendingValue;
        SuggestedCorrection = ProjectionValidationCorrection.For(validationCode);
        AttemptCount += 1;
        LastAttemptAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    /// <summary>
    /// Resolved ONLY with the canonical identity that resolved it. A queue that
    /// can be emptied by intent rather than by projection is not evidence.
    /// </summary>
    public void MarkResolved(Guid resolvedCanonicalId)
    {
        if (resolvedCanonicalId == Guid.Empty)
            throw new ArgumentException("Resolved canonical ID is required.", nameof(resolvedCanonicalId));

        State = StateResolved;
        ResolvedCanonicalId = resolvedCanonicalId;
        ResolvedAtUtc = DateTime.UtcNow;
        AttemptCount += 1;
        LastAttemptAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }
}
