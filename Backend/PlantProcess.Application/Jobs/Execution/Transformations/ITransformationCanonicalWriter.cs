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
/// canonical row.
///
/// PROVENANCE AND REPROJECTION (T-104). Every accepted evaluation records the exact
/// definition, version and immutable hash that produced the canonical effect, the run
/// that executed it and that run's projection generation. The mapping version is
/// provenance, never the row's identity:
///
///   same identity, same effect, same lineage      unchanged, nothing recorded again
///   same identity, same effect, new lineage       reattributed: the new evaluation
///                                                 becomes current, the row is untouched
///   same identity, different effect, Ordinary     typed conflict, nothing written
///   same identity, different effect, Reproject    superseded: same canonical Id, new
///                                                 effect, the prior evaluation retained
///   an older generation against a newer effect    never moves attribution, and a
///                                                 reprojection is refused as stale
///
/// A generation is reserved before the run reads its source, so a delayed run cannot
/// overwrite the effect of a run that started after it. An intentional rollback is a
/// new run of the earlier version and therefore holds a newer generation.
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
    /// Reserves the projection generation of one run from the database authority. It is
    /// called before the run reads its source, and it is the only ordering this port uses.
    /// </summary>
    Task<long> ReserveProjectionGenerationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes one governed batch as one unit. Every row is validated before anything is
    /// written; a refusal leaves canonical state and lineage unchanged and carries a
    /// JOB_EXEC_* code. The canonical effect and its lineage commit together.
    /// </summary>
    Task<ApplicationResult<CanonicalWriteResult>> WriteAsync(
        CanonicalWriteRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// The accepted source revision a row was evaluated from, when the input relation
/// exposes it. Absent for source-shaped relations that carry only governed identity.
/// </summary>
public sealed record CanonicalSourceLineage(
    Guid? BatchId,
    Guid? ReceiptId,
    string? ContentHash,
    Guid? DatasetGovernanceId,
    Guid? TenantId);

/// <summary>
/// One output row: its governed source identity, and the author-bound business fields
/// keyed by canonical field name. System-owned fields never appear here.
/// </summary>
public sealed record CanonicalWriteRow(
    string SourceSystem,
    string SourceRecordId,
    IReadOnlyDictionary<string, object?> Fields,
    CanonicalSourceLineage? Lineage = null);

/// <summary>
/// Whether a run may replace an existing canonical effect. Ordinary never may; only an
/// admitted Reproject request can, and only against an older generation.
/// </summary>
public enum CanonicalProjectionMode
{
    Ordinary = 0,
    Reproject = 1,
}

/// <summary>
/// One governed write. TargetEntity is a canonical projection target name, never a
/// physical relation. The definition identity, version and hash are the run's provenance;
/// they are never the row's identity. The writer verifies them against the definition
/// authority and never trusts them as supplied.
/// </summary>
public sealed record CanonicalWriteRequest(
    string TargetEntity,
    Guid TargetDefinitionId,
    int ResolvedVersion,
    string DefinitionHash,
    Guid JobRunHistoryId,
    CanonicalProjectionMode Mode,
    long ProjectionGeneration,
    IReadOnlyList<CanonicalWriteRow> Rows);

/// <summary>
/// RowsUnchanged counts rows whose source identity already held exactly the same
/// canonical effect and lineage. RowsReattributed kept their effect and moved their
/// current lineage to this run. RowsSuperseded received a new effect by reprojection.
/// </summary>
public sealed record CanonicalWriteResult(
    int RowsInserted,
    int RowsUnchanged,
    int RowsSuperseded = 0,
    int RowsReattributed = 0)
{
    public int RowsAccepted => RowsInserted + RowsUnchanged + RowsSuperseded + RowsReattributed;

    /// <summary>Rows whose canonical business effect was written by this call.</summary>
    public int RowsEffected => RowsInserted + RowsSuperseded;
}
