// Temporal Alignment contract.
//
// Backlog origin: T-217.
//
// Answers one question: given two or more evidence instants, each carrying the
// uncertainty it was admitted under, and a declared alignment tolerance, may they be
// treated as referring to the same moment?
//
// The answer is one of three, and the third is not a failure to compute the other two:
//
//   Coincident    - the separation is provably within tolerance
//   Separated     - the separation provably exceeds tolerance
//   Indeterminate - uncertainty spans the tolerance boundary, so the question has no
//                   answer from this evidence
//
// This preserves the source time authority law: uncertainty that overlaps cannot be
// silently ordered, and here it cannot be silently aligned either. A product that
// collapses Indeterminate into Coincident invents agreement; one that collapses it into
// Separated invents conflict. Both are worse than saying so.
//
// Deliberately out of scope: persistence, protocol handling, causal reasoning, and any
// reconciliation vocabulary about what a disagreement means. This contract reports
// temporal compatibility and nothing else.
using System;
using System.Collections.Generic;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// Whether instants may be treated as the same moment.
/// </summary>
public enum TemporalAlignment
{
    Indeterminate,
    Coincident,
    Separated
}

/// <summary>
/// How close two instants must be to count as the same moment. Declared by the
/// customer's engineer: there is no default tolerance, because the right answer differs
/// between a control loop and a shift report, and guessing it silently decides whether
/// evidence agrees.
/// </summary>
public sealed record TemporalAlignmentPolicy(string PolicyKey, TimeSpan Tolerance);

/// <summary>
/// The separation two instants could have, given what is uncertain about each. Both
/// bounds travel with the verdict so a consumer can see how close the call was.
/// </summary>
public sealed record TemporalSeparation(TimeSpan Minimum, TimeSpan Maximum);

public sealed record TemporalAlignmentVerdict(
    bool IsDecided,
    TemporalAlignment Alignment,
    TemporalSeparation? Separation,
    string Code,
    TerminalState Outcome,
    ExclusionAttribution Attribution);

/// <summary>
/// Refusal and verdict codes. Stable strings, so a consumer can branch on them without
/// parsing prose.
/// </summary>
public static class TemporalAlignmentCodes
{
    public const string PolicyNotDeclared = "TA01 alignment_policy_not_declared";
    public const string InsufficientInstants = "TA02 insufficient_instants";
    public const string IncomparableTimeRoles = "TA03 incomparable_time_roles";
    public const string NegativeTolerance = "TA04 negative_alignment_tolerance";
    public const string ConflictingDeclaration = "TA05 conflicting_declaration";

    public const string Coincident = "TA10 temporally_coincident";
    public const string Separated = "TA11 temporally_separated";
    public const string Indeterminate = "TA12 temporally_indeterminate";
}

/// <summary>
/// The alignment policies in force. Starts empty: no default tolerance of any size,
/// including zero. Declaration invariants match the rest of the kernel, so a durable
/// store need not hold a second idea of what redeclaration means.
/// </summary>
public sealed class TemporalAlignmentPolicyRegistry
{
    private readonly Dictionary<string, TemporalAlignmentPolicy> _policies = new(StringComparer.Ordinal);

    public int PolicyCount => _policies.Count;

    public bool TryDeclarePolicy(TemporalAlignmentPolicy policy, out string code)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!DeclaredKey.TryNormalise(policy.PolicyKey, out var policyKey))
        {
            code = TemporalAlignmentCodes.PolicyNotDeclared;
            return false;
        }

        // A negative tolerance is not a stricter policy, it is a typo.
        if (policy.Tolerance < TimeSpan.Zero)
        {
            code = TemporalAlignmentCodes.NegativeTolerance;
            return false;
        }

        var normalised = policy with { PolicyKey = policyKey };

        if (_policies.TryGetValue(policyKey, out var existing))
        {
            if (existing == normalised)
            {
                code = string.Empty;
                return true;
            }

            code = TemporalAlignmentCodes.ConflictingDeclaration;
            return false;
        }

        _policies[policyKey] = normalised;
        code = string.Empty;
        return true;
    }

    public bool TryGetPolicy(string? policyKey, out TemporalAlignmentPolicy? policy)
    {
        policy = null;

        if (!DeclaredKey.TryNormalise(policyKey, out var normalised)) return false;

        return _policies.TryGetValue(normalised, out policy);
    }
}