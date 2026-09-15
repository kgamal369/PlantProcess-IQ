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
/// One rule, stated once: a current-cycle success satisfies; otherwise a prior success may be
/// reused only when the edge opts in and its age is inside the declared tolerance; everything
/// else blocks a required edge and skips an optional one. Nothing is inferred from a comment.
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

        if (!input.AllowStaleReuse)
        {
            return Refuse(input, "Upstream result is from an earlier cycle and this edge does not permit stale reuse.");
        }

        if (input.StalenessToleranceMinutes is null)
        {
            return Refuse(input, "Stale reuse is permitted but no staleness tolerance is declared, so no age is acceptable.");
        }

        if (input.PriorSuccessAgeMinutes.Value < 0)
        {
            return Refuse(input, "Upstream completion is in the future; the age is not usable.");
        }

        // The boundary is inclusive: an age exactly equal to the tolerance is inside it.
        if (input.PriorSuccessAgeMinutes.Value <= input.StalenessToleranceMinutes.Value)
        {
            return new FreshnessOutcome(
                FreshnessResolution.StaleAccepted, input.PriorSuccessAgeMinutes, input.StalenessToleranceMinutes,
                "Prior upstream success reused: age is inside the declared tolerance and the edge permits reuse.");
        }

        return Refuse(input, "Prior upstream success is older than the declared staleness tolerance.");
    }

    private static FreshnessOutcome Refuse(FreshnessInput input, string reason)
        => new(
            input.IsRequired ? FreshnessResolution.Blocked : FreshnessResolution.SkippedOptional,
            input.PriorSuccessAgeMinutes,
            input.StalenessToleranceMinutes,
            input.IsRequired ? reason : reason + " The edge is optional, so the job ran without it.");
}
