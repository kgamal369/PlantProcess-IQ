namespace PlantProcess.Application.Acquisition;

/// <summary>
/// PPIQ-T028/T032:
/// Validates the one-way collector doctrine before the batch can enter staging.
/// This class does not create network clients and contains no OT pull/control methods by design.
/// </summary>
public sealed class OtSafeEdgeCollectorGateway
{
    public EdgeCollectorAcceptanceResult Accept(
        OtSafeEdgeCollectorRegistration collector,
        EdgeCollectorPushBatch batch,
        string expectedSignature)
    {
        var errors = new List<string>();

        if (!collector.IsOtSafe)
            errors.Add("Collector is not OT-safe: one-way push, no inbound path, no control path and credential fingerprint are mandatory.");

        if (batch.CollectorId != collector.CollectorId)
            errors.Add("Batch collector id does not match registered collector.");

        if (batch.TenantId != collector.TenantId)
            errors.Add("Batch tenant id does not match registered collector.");

        if (string.IsNullOrWhiteSpace(batch.Signature) || !batch.Signature.Equals(expectedSignature, StringComparison.Ordinal))
            errors.Add("Batch signature is invalid.");

        if (batch.Rows.Count == 0)
            errors.Add("Batch must contain at least one row.");

        if (batch.Rows.GroupBy(x => x.RowNumber).Any(x => x.Count() > 1))
            errors.Add("Batch contains duplicate row numbers.");

        if (batch.Rows.Any(x => x.ObservedAtUtc < batch.CapturedFromUtc || x.ObservedAtUtc > batch.CapturedToUtc))
            errors.Add("One or more rows are outside the declared capture window.");

        return errors.Count > 0
            ? EdgeCollectorAcceptanceResult.Rejected(errors)
            : EdgeCollectorAcceptanceResult.CreateAccepted(batch.Rows.Count);
    }
}

public sealed record EdgeCollectorAcceptanceResult(
    bool Accepted,
    int AcceptedRows,
    IReadOnlyList<string> Errors)
{
    public static EdgeCollectorAcceptanceResult CreateAccepted(int rows)
        => new(true, rows, Array.Empty<string>());

    public static EdgeCollectorAcceptanceResult Rejected(IReadOnlyList<string> errors)
        => new(false, 0, errors);
}
