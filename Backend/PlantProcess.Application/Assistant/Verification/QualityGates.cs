using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PlantProcess.Application.Assistant.Verification;

/// <summary>
/// T-181. THE ELEVEN GATES, AND THE HONEST TREATMENT OF THE FOUR THAT CANNOT RUN HERE.
///
/// Q-01 to Q-07 are properties of the planner, the evidence and the verifier, and all
/// three exist in this build, so they are measured on a fixed probe set.
///
/// Q-08 to Q-11 are runtime measurements: time to first token, total latency,
/// throughput, and memory per concurrent session. There is no serving runtime in this
/// isolated build, so there is nothing to measure. They are therefore reported as
/// CapabilityUnavailable with the reason and the owner, and carry no value.
///
/// THIS IS THE POINT, NOT A GAP. Filling four gates with plausible numbers so the
/// report reads all-green would make the harness worthless precisely where it matters
/// most, and would erase the distinction this project keeps insisting on: between
/// implementation complete and runtime measured. T-137 supplies the runtime; T-138
/// reruns these gates at cutover.
/// </summary>
public enum QualityGateId
{
    Q01_ToolSelectionAccuracy = 1,
    Q02_Groundedness = 2,
    Q03_CitationCorrectness = 3,
    Q04_UnsupportedClaimRate = 4,
    Q05_RefusalCorrectness = 5,
    Q06_CausalOverreachRate = 6,
    Q07_MultilingualFidelity = 7,
    Q08_TimeToFirstToken = 8,
    Q09_TotalAnswerLatency = 9,
    Q10_ServingThroughput = 10,
    Q11_MemoryPerConcurrentSession = 11
}

/// <summary>Whether a gate produced a number, and if not, why not.</summary>
public enum MeasurementState
{
    /// <summary>A real measurement was taken on the probe set.</summary>
    Measured = 0,

    /// <summary>The gate applies but nothing measured it in this run.</summary>
    NotMeasured = 1,

    /// <summary>The gate does not apply to this configuration.</summary>
    NotApplicable = 2,

    /// <summary>
    /// The capability the gate measures does not exist in this build.
    ///
    /// Distinct from NotMeasured on purpose: nobody forgot, and nothing is pending.
    /// There is no serving runtime here to time.
    /// </summary>
    CapabilityUnavailable = 3
}

/// <summary>The verdict for one gate. NotMeasured is never quietly a pass.</summary>
public enum GateVerdict
{
    Pass = 0,
    Fail = 1,
    NotMeasured = 2
}

/// <summary>One gate's result. Machine readable, reproducible, and free of a clock reading.</summary>
public sealed record QualityGateResult(
    QualityGateId GateId,
    string GateName,
    MeasurementState State,
    GateVerdict Verdict,
    long? Numerator,
    long? Denominator,
    double? Value,
    string Unit,
    ImmutableArray<string> ProbeIds,
    string Reason,
    string OwnerTaskWhenUnavailable)
{
    public static QualityGateResult Measured(
        QualityGateId gateId,
        string gateName,
        long numerator,
        long denominator,
        string unit,
        ImmutableArray<string> probeIds,
        string reason)
    {
        var value = denominator == 0 ? 0.0 : (double)numerator / denominator;
        return new QualityGateResult(
            gateId,
            gateName,
            MeasurementState.Measured,
            numerator == denominator ? GateVerdict.Pass : GateVerdict.Fail,
            numerator,
            denominator,
            value,
            unit,
            probeIds,
            reason,
            string.Empty);
    }

    public static QualityGateResult Unavailable(
        QualityGateId gateId,
        string gateName,
        string unit,
        string reason,
        string ownerTask) =>
        new(
            gateId,
            gateName,
            MeasurementState.CapabilityUnavailable,
            GateVerdict.NotMeasured,
            null,
            null,
            null,
            unit,
            ImmutableArray<string>.Empty,
            reason,
            ownerTask);

    public string Render() =>
        Value.HasValue
            ? Value.Value.ToString("0.######", CultureInfo.InvariantCulture)
            : "not-measured";
}

/// <summary>
/// The whole report.
///
/// The fingerprint deliberately excludes any timestamp, so the same probe set on the
/// same build produces the same identity and two runs can be compared at all.
/// </summary>
public sealed record QualityReport(
    string HarnessVersion,
    ImmutableArray<QualityGateResult> Gates)
{
    public const string Version = "ppiq.assistant.quality/1";

    public ImmutableArray<QualityGateResult> MeasuredGates =>
        Gates.Where(g => g.State == MeasurementState.Measured).ToImmutableArray();

    public ImmutableArray<QualityGateResult> UnavailableGates =>
        Gates.Where(g => g.State == MeasurementState.CapabilityUnavailable).ToImmutableArray();

    public bool AllMeasuredGatesPass =>
        MeasuredGates.All(g => g.Verdict == GateVerdict.Pass);

    /// <summary>
    /// Never true while any gate is unmeasured.
    ///
    /// A report cannot claim the whole suite is green while four of its gates never
    /// ran, and there is no property here that would let it.
    /// </summary>
    public bool AllElevenGatesMeasuredAndPassing =>
        Gates.Length == 11
        && Gates.All(g => g.State == MeasurementState.Measured && g.Verdict == GateVerdict.Pass);

    public string ReportFingerprint()
    {
        var builder = new StringBuilder();
        builder.Append(HarnessVersion).Append('|');

        foreach (var gate in Gates.OrderBy(g => (int)g.GateId))
        {
            builder.Append((int)gate.GateId).Append(':');
            builder.Append(gate.State).Append(':');
            builder.Append(gate.Verdict).Append(':');
            builder.Append(gate.Numerator?.ToString(CultureInfo.InvariantCulture) ?? "-").Append('/');
            builder.Append(gate.Denominator?.ToString(CultureInfo.InvariantCulture) ?? "-").Append(':');
            builder.Append(gate.Render()).Append(':');
            builder.Append(gate.Unit).Append(':');
            builder.Append(string.Join(",", gate.ProbeIds)).Append(';');
        }

        return builder.ToString();
    }
}
