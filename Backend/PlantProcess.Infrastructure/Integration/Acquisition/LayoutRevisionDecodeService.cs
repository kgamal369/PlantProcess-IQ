// The seam between a stored immutable layout revision and the decoder.
//
// It resolves ONE exact revision inside the caller's tenant and dataset scope and
// refuses anything else. "Latest" is never substituted: a payload decoded under a
// revision the caller did not name is a different contract wearing the same name.
//
// It owns no storage and no schedule. Reading is T-268's store; decoding is the
// pure kernel; accepting and persisting belongs to T-272.
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Application.Integration.Acquisition.Decoding;
using PlantProcess.Infrastructure.Persistence;
using System.Text.Json;

namespace PlantProcess.Infrastructure.Integration.Acquisition;

/// <summary>One decoded occurrence, with everything an acceptance authority needs to judge it.</summary>
public sealed record DecodedSourceEvent(
    Guid DatasetGovernanceId,
    int LayoutRevision,
    string LayoutSemanticHash,
    SourceEventIdentity Identity,
    RawBlockDecodeResult Decoded,
    DeliveryAttempt Delivery);

public sealed class LayoutRevisionDecodeService
{
    private readonly IndustrialAcquisitionStore _store;

    public LayoutRevisionDecodeService(PlantProcessDbContext db)
    {
        _store = new IndustrialAcquisitionStore(db ?? throw new ArgumentNullException(nameof(db)));
    }

    public async Task<AcquisitionOutcome<DecodedSourceEvent>> DecodeAsync(
        Guid tenantId,
        Guid datasetId,
        int layoutRevision,
        ReadOnlyMemory<byte> payload,
        IReadOnlyList<Guid> identityFieldIds,
        DeliveryAttempt delivery,
        DecodeBudget? budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identityFieldIds);
        ArgumentNullException.ThrowIfNull(delivery);

        if (layoutRevision < 1)
        {
            return AcquisitionOutcome<DecodedSourceEvent>.Refuse(DecodeCodes.LayoutRevisionMismatch,
                "a decode names the exact layout revision it was authored against; there is no latest.");
        }

        var governance = await _store.ResolveGovernanceAsync(tenantId, datasetId, cancellationToken);
        if (!governance.IsAccepted)
        {
            return AcquisitionOutcome<DecodedSourceEvent>.Refuse(governance.Refusal!);
        }

        var layouts = await _store.ListLayoutsAsync(
            tenantId, governance.Value!.GovernanceId, layoutRevision, cancellationToken);
        if (layouts.Count != 1)
        {
            return AcquisitionOutcome<DecodedSourceEvent>.Refuse(DecodeCodes.LayoutRevisionMismatch,
                "layout revision " + layoutRevision + " does not exist for this dataset; no other revision is substituted.");
        }

        var layout = layouts[0];
        using var document = JsonDocument.Parse(layout.DocumentJson);
        var layoutElement = document.RootElement.Clone();

        // A typed source item is read by its provider, not decoded from bytes here.
        var normalized = RawLayoutKernel.Normalize(layoutElement);
        if (!normalized.IsAccepted)
        {
            return AcquisitionOutcome<DecodedSourceEvent>.Refuse(DecodeCodes.LayoutInvalid, normalized.Refusal!.Detail);
        }

        var facts = await _store.LoadFieldFactsAsync(tenantId, governance.Value.GovernanceId, cancellationToken);
        var typed = TypedValueGuard.Check(normalized.Value!.Members, facts);
        if (typed is not null)
        {
            return AcquisitionOutcome<DecodedSourceEvent>.Refuse(typed.Code, typed.Detail);
        }

        var decoded = RawBlockDecoder.Decode(layoutElement, layoutRevision, payload.Span, budget);
        if (!decoded.Accepted)
        {
            var first = decoded.Diagnostics[0];
            return AcquisitionOutcome<DecodedSourceEvent>.Refuse(first.Code, first.Detail);
        }

        if (!string.Equals(decoded.LayoutSemanticHash, layout.SemanticHash, StringComparison.Ordinal))
        {
            return AcquisitionOutcome<DecodedSourceEvent>.Refuse(DecodeCodes.LayoutRevisionMismatch,
                "the stored revision does not re-normalise to its own semantic hash; it was not written by the layout authority.");
        }

        var identity = SourceEventIdentity.Derive(
            governance.Value.GovernanceId, layoutRevision, decoded, identityFieldIds);
        if (!identity.IsAccepted)
        {
            return AcquisitionOutcome<DecodedSourceEvent>.Refuse(identity.Refusal!);
        }

        return AcquisitionOutcome<DecodedSourceEvent>.Accept(new DecodedSourceEvent(
            governance.Value.GovernanceId, layoutRevision, layout.SemanticHash, identity.Value!, decoded, delivery));
    }
}
