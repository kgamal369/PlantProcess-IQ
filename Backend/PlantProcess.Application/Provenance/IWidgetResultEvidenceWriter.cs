using PlantProcess.Application.Assistant;

namespace PlantProcess.Application.Provenance;

/// <summary>
/// PR-050-01. One widget execution offered for persistence as T-073
/// WidgetResult evidence.
///
/// The identity is REQUIRED and is required in full. T-073 hashes PageCode and
/// WidgetCode into the query fingerprint and renders both into the evidence
/// sentence, so an execution that cannot name its page and widget has no
/// truthful evidence identity. A caller that lacks them must refuse and say so;
/// it must not hand this type an empty string and let the store write
/// "On page , widget  shows ...".
/// </summary>
public sealed record WidgetResultEvidenceWriteRequest(
    Guid TenantId,
    WidgetEvidenceIdentity Identity,
    NormalisedWidgetResult Result,
    string FilterContextJson,
    DateTime GeneratedAtUtc);

/// <summary>
/// PR-050-01. The single write authority for T-073 WidgetResult evidence.
///
/// WHY THIS SEAM EXISTS. The write already existed, but only as a private
/// method inside the Assistant reindex producer, and that producer depends on
/// the dashboard query service. Making the query service write evidence
/// directly would have closed a dependency cycle; making it write its own rows
/// would have created a second provenance system. Extracting the existing
/// capability behind one contract does neither.
///
/// Both callers - the Assistant reindex producer and the dashboard widget
/// query service - go through this. There is deliberately no second write path.
///
/// The implementation owns fingerprint derivation, so determinism is decided in
/// ONE place: the same identity, filter context and normalised result always
/// resolve to the same evidence id, and a materially different population
/// always resolves to a different one.
/// </summary>
public interface IWidgetResultEvidenceWriter
{
    /// <summary>
    /// Writes the snapshot, or returns the id of the one an earlier identical
    /// execution already wrote. Returns null when the row can be neither
    /// inserted nor found, which the caller must treat as evidence unavailable
    /// rather than as evidence.
    /// </summary>
    Task<Guid?> WriteAsync(WidgetResultEvidenceWriteRequest request, CancellationToken cancellationToken);
}