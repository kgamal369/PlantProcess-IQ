// Operational Transition and Stabilisation contract.
//
// Backlog origin: T-234.
//
// Answers one question: at a given moment, is a scope running steadily, moving between
// contexts, or settling after having moved?
//
// Two laws hold this contract together.
//
// A transition is not downtime. The process is doing something deliberate - changing
// context, being set up, being cleaned, recovering - and treating that as lost time
// produces a plant that appears to fail constantly while behaving exactly as intended.
// There is no downtime concept anywhere in this file.
//
// Stabilisation is never guessed. Thirty minutes, ten subjects and "until it looks
// steady" are all inventions, and an invented settling window silently decides which
// samples count as normal. The basis is declared, and a missing declaration refuses.
//
// TransitionKindCode is an opaque declared identifier rather than a fixed vocabulary.
// The original enumeration ended in "custom", which is the set admitting it is not
// closed; freezing the product on one lane's list is a mistake already made and
// withdrawn once in this kernel.
//
// Deliberately out of scope: statistics, reconciliation and persistence. This contract
// classifies regime and nothing else.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// What a scope is doing. Mixed belongs to a window rather than an instant: a single
/// moment has one regime, a span may cover several, and pooling those is what the
/// downstream guard exists to prevent.
/// </summary>
public enum OperationalRegime
{
    Unknown,
    Stable,
    Transition,
    Stabilising,
    Mixed
}

/// <summary>
/// How the end of settling is decided. There is no default member: every declaration
/// states which basis applies, including the explicit choice that nothing settles.
/// </summary>
public enum StabilisationBasis
{
    Time,
    SubjectCount,
    Condition,
    None
}

/// <summary>
/// One declared transition on one scope, and how settling after it is decided.
/// <para>
/// Kind and context codes are opaque declared identifiers. This contract stores them and
/// classifies by them; what they mean belongs to the customer.
/// </para>
/// </summary>
public sealed record TransitionDeclaration(
    string ScopeKey,
    string TransitionKindCode,
    string FromContextCode,
    string ToContextCode,
    DateTimeOffset Start,
    DateTimeOffset End,
    StabilisationBasis StabilisationBasis,
    TimeSpan StabilisationDuration,
    int StabilisationSubjectCount,
    string StabilisationConditionCode)
{
    /// <summary>Half-open, so adjacent transitions cannot both apply at the boundary.</summary>
    public bool CoversInstant(DateTimeOffset at) => at >= Start && at < End;
}

/// <summary>
/// What a caller knows about settling at the moment being classified. Subject counts and
/// condition outcomes are observations the caller holds; this kernel will not invent
/// either, and will not accept an unsupplied one as satisfied.
/// </summary>
public sealed record StabilisationObservation(
    int SubjectsCompletedSinceTransitionEnd,
    bool? DeclaredConditionSatisfied)
{
    /// <summary>Nothing known beyond the clock. Sufficient only for Time and None bases.</summary>
    public static StabilisationObservation None { get; } = new(0, null);

    public static StabilisationObservation WithSubjectsCompleted(int completed) => new(completed, null);

    public static StabilisationObservation WithConditionOutcome(bool satisfied) => new(0, satisfied);
}

public sealed record RegimeClassification(
    bool IsDecided,
    OperationalRegime Regime,
    TransitionDeclaration? Transition,
    string Code,
    TerminalState Outcome,
    ExclusionAttribution Attribution);

/// <summary>
/// Refusal and classification codes. Stable strings, so a consumer can branch on them
/// without parsing prose.
/// </summary>
public static class OperationalTransitionCodes
{
    public const string ScopeNotDeclared = "TR01 transition_scope_not_declared";
    public const string StabilisationBasisNotDeclared = "TR02 stabilisation_basis_not_declared";
    public const string StabilisationObservationNotSupplied = "TR03 stabilisation_observation_not_supplied";
    public const string ConflictingDeclaration = "TR04 conflicting_declaration";
    public const string InvalidDeclaration = "TR05 invalid_declaration";
    public const string EmptyWindow = "TR06 empty_window";

    public const string RegimeClassified = "TR20 regime_classified";

    /// <summary>
    /// The mixed-regime code the committed validation fixture already names. Kept
    /// verbatim so the downstream pooling guard and the fixture agree on one string.
    /// </summary>
    public const string MixedProcessRegime = "RG01 mixed_process_regime";
}

/// <summary>
/// The transition declarations in force. Starts empty, and an undeclared scope is not
/// quietly assumed to be running steadily: silence about a scope is silence, which is
/// why classification refuses rather than answering Stable.
///
/// <para>
/// Declaring a scope is itself a statement - "transitions for this scope are declared
/// here, and moments no declaration covers are steady state" - so Stable is a
/// consequence of a declaration rather than an inference from its absence.
/// </para>
/// </summary>
public sealed class OperationalTransitionRegistry
{
    private readonly HashSet<string> _scopes = new(StringComparer.Ordinal);
    private readonly List<TransitionDeclaration> _transitions = new();

    public int ScopeCount => _scopes.Count;
    public int TransitionCount => _transitions.Count;

    public bool TryDeclareScope(string? scopeKey, out string code)
    {
        if (!DeclaredKey.TryNormalise(scopeKey, out var normalised))
        {
            code = OperationalTransitionCodes.ScopeNotDeclared;
            return false;
        }

        _scopes.Add(normalised);
        code = string.Empty;
        return true;
    }

    public bool IsScopeDeclared(string? scopeKey) =>
        DeclaredKey.TryNormalise(scopeKey, out var normalised) && _scopes.Contains(normalised);

    public bool TryDeclareTransition(TransitionDeclaration declaration, out string code)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!DeclaredKey.TryNormalise(declaration.ScopeKey, out var scopeKey))
        {
            code = OperationalTransitionCodes.ScopeNotDeclared;
            return false;
        }

        if (!_scopes.Contains(scopeKey))
        {
            code = OperationalTransitionCodes.ScopeNotDeclared;
            return false;
        }

        if (!DeclaredKey.TryNormalise(declaration.TransitionKindCode, out var kindCode) ||
            !DeclaredKey.TryNormalise(declaration.FromContextCode, out var fromCode) ||
            !DeclaredKey.TryNormalise(declaration.ToContextCode, out var toCode))
        {
            code = OperationalTransitionCodes.InvalidDeclaration;
            return false;
        }

        if (declaration.End <= declaration.Start)
        {
            code = OperationalTransitionCodes.InvalidDeclaration;
            return false;
        }

        var conditionCode = string.Empty;

        // A basis without its parameter is not a declaration, it is a gap wearing one.
        switch (declaration.StabilisationBasis)
        {
            case StabilisationBasis.Time:
                if (declaration.StabilisationDuration <= TimeSpan.Zero)
                {
                    code = OperationalTransitionCodes.StabilisationBasisNotDeclared;
                    return false;
                }
                break;

            case StabilisationBasis.SubjectCount:
                if (declaration.StabilisationSubjectCount <= 0)
                {
                    code = OperationalTransitionCodes.StabilisationBasisNotDeclared;
                    return false;
                }
                break;

            case StabilisationBasis.Condition:
                if (!DeclaredKey.TryNormalise(declaration.StabilisationConditionCode, out conditionCode))
                {
                    code = OperationalTransitionCodes.StabilisationBasisNotDeclared;
                    return false;
                }
                break;

            case StabilisationBasis.None:
                // An explicit statement that nothing settles. Carrying a parameter as well
                // would mean two answers.
                if (declaration.StabilisationDuration != TimeSpan.Zero ||
                    declaration.StabilisationSubjectCount != 0 ||
                    !string.IsNullOrWhiteSpace(declaration.StabilisationConditionCode))
                {
                    code = OperationalTransitionCodes.InvalidDeclaration;
                    return false;
                }
                break;

            default:
                code = OperationalTransitionCodes.StabilisationBasisNotDeclared;
                return false;
        }

        var normalised = declaration with
        {
            ScopeKey = scopeKey,
            TransitionKindCode = kindCode,
            FromContextCode = fromCode,
            ToContextCode = toCode,
            StabilisationConditionCode = conditionCode
        };

        foreach (var existing in _transitions.Where(t => string.Equals(t.ScopeKey, scopeKey, StringComparison.Ordinal)))
        {
            if (existing == normalised)
            {
                code = string.Empty;
                return true;
            }

            // Two transitions covering one moment on one scope is a question, not extra
            // information.
            if (existing.Start < normalised.End && normalised.Start < existing.End)
            {
                code = OperationalTransitionCodes.ConflictingDeclaration;
                return false;
            }
        }

        _transitions.Add(normalised);
        code = string.Empty;
        return true;
    }

    public IReadOnlyList<TransitionDeclaration> TransitionsFor(string? scopeKey)
    {
        if (!DeclaredKey.TryNormalise(scopeKey, out var normalised))
        {
            return Array.Empty<TransitionDeclaration>();
        }

        return _transitions
            .Where(t => string.Equals(t.ScopeKey, normalised, StringComparison.Ordinal))
            .OrderBy(t => t.Start)
            .ToArray();
    }
}