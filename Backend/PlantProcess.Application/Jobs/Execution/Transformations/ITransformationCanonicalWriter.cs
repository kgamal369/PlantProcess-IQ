using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Jobs.Execution.Transformations;

/// <summary>
/// THE CANONICAL WRITE PORT.
///
/// The executor decides what to write and never how it is stored. Identity, provenance
/// and idempotency are the writer's contract rather than each caller's, because a
/// per-definition identity strategy is precisely how two definitions end up writing
/// rows that can never be reconciled.
///
/// COMMISSIONED IS NOT THE SAME AS AUTHORABLE. A canonical projection target is a lawful
/// authoring target. A commissioned target is one for which this runtime holds a proven
/// write and idempotency path. An implementation answers only for the targets it can
/// prove, and every other target is refused before any canonical mutation.
///
/// Identity is the governed source provenance supplied by the accepted source record.
/// The same source identity against the same target must never produce a second
/// canonical row. Reprojection and supersession semantics are not decided here.
/// </summary>
public interface ITransformationCanonicalWriter
{
    /// <summary>Whether this runtime holds a proven write path for the target.</summary>
    bool IsCommissioned(string targetEntity);

    /// <summary>
    /// The first bound field, in ordinal order, that has no sanctioned write path through
    /// the target's domain construction, or null when every bound field has one.
    /// </summary>
    string? FirstUnwritableField(string targetEntity, IReadOnlyCollection<string> boundFields);

    /// <summary>
    /// Writes one governed batch as one unit. Every row is validated before anything is
    /// written; a refusal leaves canonical state unchanged and carries a JOB_EXEC_* code.
    /// </summary>
    Task<ApplicationResult<CanonicalWriteResult>> WriteAsync(
        CanonicalWriteRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// One output row: its governed source identity, and the author-bound business fields
/// keyed by canonical field name. System-owned fields never appear here.
/// </summary>
public sealed record CanonicalWriteRow(
    string SourceSystem,
    string SourceRecordId,
    IReadOnlyDictionary<string, object?> Fields);

/// <summary>
/// One governed write. TargetEntity is a canonical projection target name, never a
/// physical relation. The definition identity and version are the run's provenance;
/// they are never the row's identity.
/// </summary>
public sealed record CanonicalWriteRequest(
    string TargetEntity,
    Guid TargetDefinitionId,
    int ResolvedVersion,
    Guid JobRunHistoryId,
    IReadOnlyList<CanonicalWriteRow> Rows);

/// <summary>
/// RowsUnchanged counts rows whose source identity already held exactly the same
/// canonical effect. They are accepted and not written again.
/// </summary>
public sealed record CanonicalWriteResult(
    int RowsInserted,
    int RowsUnchanged)
{
    public int RowsAccepted => RowsInserted + RowsUnchanged;
}
