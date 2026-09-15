// T-106 Phase A: explicit dependency freshness policy (Design v4.10.3 Ch4 5.3.6, D-3).
// Pure decision kernel. The evaluator wiring and the allow_stale_reuse column are later slices.
using System;

namespace PlantProcess.Application.Jobs.Dependencies;

/// <summary>The freshness decision for one declared edge.</summary>
public enum FreshnessResolution
{
    Satisfied = 0,
    StaleAccepted = 1,
    Blocked = 2,
    SkippedOptional = 3
}

/// <summary>
/// What is known about one upstream edge at evaluation time. Ages are minutes measured
/// from the upstream completion instant; a null age means no prior success exists.
/// </summary>
public sealed record FreshnessInput(
    bool IsRequired,
    bool HasCurrentCycleSuccess,
    double? PriorSuccessAgeMinutes,
    int? StalenessToleranceMinutes,
    bool AllowStaleReuse);

public sealed record FreshnessOutcome(
    FreshnessResolution Resolution,
    double? UpstreamAgeMinutes,
    int? ToleranceMinutes,
    string Reason);

/// <summary>
/// One rule, stated once. A current-cycle success satisfies. A prior-cycle success satisfies
/// while it is inside the declared tolerance, and also when no tolerance was declared at all.
/// Past that ceiling the edge must explicitly permit stale reuse to get stale_accepted;
/// otherwise a required edge blocks and an optional edge is skipped. Nothing is inferred
/// from a comment.
/// </summary>
public static class DependencyFreshnessPolicy
{
    public static FreshnessOutcome Evaluate(FreshnessInput input)
    {
        if (input is null) { throw new ArgumentNullException(nameof(input)); }

        if (input.HasCurrentCycleSuccess)
        {
            return new FreshnessOutcome(
                FreshnessResolution.Satisfied, input.PriorSuccessAgeMinutes, input.StalenessToleranceMinutes,
                "Upstream produced a successful result in the current dependency cycle.");
        }

        if (input.PriorSuccessAgeMinutes is null)
        {
            return Refuse(input, "Upstream has no successful result to reuse.");
        }

        if (input.PriorSuccessAgeMinutes.Value < 0)
        {
            return Refuse(input, "Upstream completion is in the future; the age is not usable.");
        }

        // No declared tolerance is no declared freshness ceiling. A successful upstream
        // satisfies the edge however old it is, because nobody said otherwise.
        if (input.StalenessToleranceMinutes is null)
        {
            return new FreshnessOutcome(
                FreshnessResolution.Satisfied, input.PriorSuccessAgeMinutes, null,
                "Prior upstream success reused: the edge declares no freshness tolerance.");
        }

        // Inside the declared ceiling the result is simply fresh enough. Permission is not
        // consulted here: allow_stale_reuse permits going BEYOND the ceiling, it is not a
        // permission to reuse a prior cycle at all.
        if (input.PriorSuccessAgeMinutes.Value <= input.StalenessToleranceMinutes.Value)
        {
            return new FreshnessOutcome(
                FreshnessResolution.Satisfied, input.PriorSuccessAgeMinutes, input.StalenessToleranceMinutes,
                "Prior upstream success is inside the declared tolerance.");
        }

        if (!input.AllowStaleReuse)
        {
            return Refuse(input, "Prior upstream success is older than the declared tolerance and this edge does not permit stale reuse.");
        }

        return new FreshnessOutcome(
            FreshnessResolution.StaleAccepted, input.PriorSuccessAgeMinutes, input.StalenessToleranceMinutes,
            "Prior upstream success is older than the declared tolerance and the edge permits stale reuse.");
    }

    private static FreshnessOutcome Refuse(FreshnessInput input, string reason)
        => new(
            input.IsRequired ? FreshnessResolution.Blocked : FreshnessResolution.SkippedOptional,
            input.PriorSuccessAgeMinutes,
            input.StalenessToleranceMinutes,
            input.IsRequired ? reason : reason + " The edge is optional, so the job ran without it.");
}
