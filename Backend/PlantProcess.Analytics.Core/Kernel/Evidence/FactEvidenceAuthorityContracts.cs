// Fact Evidence Authority contract.
//
// Backlog origin: T-218.
//
// Answers one question: for a named semantic fact, at a given moment, which declared
// source carries the authority to state it, and which merely support or corroborate it?
//
// There is no global source ranking. No source outranks another in general, because
// authority is a property of the fact, not of the equipment that produced the record.
// The same source is routinely primary for one fact, supporting for a second, and
// irrelevant to a third, and a product that ranks sources globally will confidently
// answer the wrong question for two of the three.
//
// Nothing here names a plant, a vendor or a class of equipment. Facts and sources are
// opaque declared identities.
//
// Deliberately out of scope: reconciliation, conflict, causal reasoning and persistence.
// A missing primary is missing, never a conflict - conflict is a downstream judgement
// that requires two authorities to compare, and this contract stops before that.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// What a source is to a fact. Irrelevant is a real answer, not an absence: it is the
/// correct response to asking a source about something it has no standing to state.
/// </summary>
public enum EvidenceRole
{
    Irrelevant,
    Primary,
    Supporting,
    Corroborating
}

/// <summary>
/// A named semantic fact and the minimum evidence quality it will accept. The floor is
/// declared per fact, because how good a record must be to be believed depends on what
/// is being claimed.
/// </summary>
public sealed record SemanticFactDeclaration(string FactKey, double QualityFloor);

/// <summary>
/// What one source is to one fact, over a declared effective interval. Effective dating
/// is part of the declaration: an authority arrangement that changed last year should
/// not silently rewrite what was true before it changed.
/// </summary>
public sealed record FactSourceAuthorityDeclaration(
    string FactKey,
    string SourceKey,
    EvidenceRole Role,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveTo)
{
    /// <summary>Half-open: from is inclusive, to is exclusive, so adjacent intervals cannot both apply.</summary>
    public bool IsEffectiveAt(DateTimeOffset asOf) => asOf >= EffectiveFrom && asOf < EffectiveTo;
}

/// <summary>
/// A record a source is offering about a fact, with the quality declared for it. This
/// contract never inspects the value - only whether the source may speak and whether the
/// record clears the fact's floor.
/// </summary>
public sealed record OfferedEvidence(
    string FactKey,
    string SourceKey,
    double Quality,
    DateTimeOffset At);

/// <summary>
/// Who may state the fact, and who else has something to say about it.
/// </summary>
public sealed record ResolvedFactAuthority(
    string FactKey,
    string PrimarySourceKey,
    IReadOnlyList<string> SupportingSourceKeys,
    IReadOnlyList<string> CorroboratingSourceKeys,
    DateTimeOffset AsOf);

public sealed record FactAuthorityResolution(
    bool IsResolved,
    ResolvedFactAuthority? Authority,
    string Code,
    TerminalState Outcome,
    ExclusionAttribution Attribution);

/// <summary>
/// Refusal and resolution codes. Stable strings, so a consumer can branch on them
/// without parsing prose.
/// </summary>
public static class FactAuthorityCodes
{
    public const string FactDeclarationAbsent = "FA01 fact_declaration_absent";
    public const string PrimaryAuthorityNotDeclared = "FA02 primary_authority_not_declared";
    public const string PrimaryAuthorityUnavailable = "FA03 primary_authority_unavailable";
    public const string AmbiguousPrimaryAuthority = "FA04 ambiguous_primary_authority";
    public const string InsufficientEvidenceQuality = "FA05 insufficient_evidence_quality";
    public const string SourceIrrelevantForFact = "FA06 source_irrelevant_for_fact";
    public const string ConflictingDeclaration = "FA07 conflicting_declaration";
    public const string InvalidDeclaration = "FA08 invalid_declaration";

    public const string AuthorityResolved = "FA20 authority_resolved";
}

/// <summary>
/// The authority declarations in force. Starts empty: no fact, no source, no assumed
/// ranking of any kind. Declaration invariants match the rest of the kernel.
/// </summary>
public sealed class FactEvidenceAuthorityRegistry
{
    private readonly Dictionary<string, SemanticFactDeclaration> _facts = new(StringComparer.Ordinal);
    private readonly List<FactSourceAuthorityDeclaration> _bindings = new();

    public int FactCount => _facts.Count;
    public int BindingCount => _bindings.Count;

    public bool TryDeclareFact(SemanticFactDeclaration declaration, out string code)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!DeclaredKey.TryNormalise(declaration.FactKey, out var factKey))
        {
            code = FactAuthorityCodes.FactDeclarationAbsent;
            return false;
        }

        if (double.IsNaN(declaration.QualityFloor) || declaration.QualityFloor < 0d || declaration.QualityFloor > 1d)
        {
            code = FactAuthorityCodes.InvalidDeclaration;
            return false;
        }

        var normalised = declaration with { FactKey = factKey };

        if (_facts.TryGetValue(factKey, out var existing))
        {
            if (existing == normalised)
            {
                code = string.Empty;
                return true;
            }

            code = FactAuthorityCodes.ConflictingDeclaration;
            return false;
        }

        _facts[factKey] = normalised;
        code = string.Empty;
        return true;
    }

    public bool TryDeclareAuthority(FactSourceAuthorityDeclaration declaration, out string code)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!DeclaredKey.TryNormalise(declaration.FactKey, out var factKey) ||
            !DeclaredKey.TryNormalise(declaration.SourceKey, out var sourceKey))
        {
            code = FactAuthorityCodes.InvalidDeclaration;
            return false;
        }

        if (!_facts.ContainsKey(factKey))
        {
            code = FactAuthorityCodes.FactDeclarationAbsent;
            return false;
        }

        if (declaration.EffectiveTo <= declaration.EffectiveFrom)
        {
            code = FactAuthorityCodes.InvalidDeclaration;
            return false;
        }

        var normalised = declaration with { FactKey = factKey, SourceKey = sourceKey };

        foreach (var existing in _bindings)
        {
            if (!string.Equals(existing.FactKey, factKey, StringComparison.Ordinal) ||
                !string.Equals(existing.SourceKey, sourceKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (existing == normalised)
            {
                code = string.Empty;
                return true;
            }

            // Two answers for what one source is to one fact at one moment is not extra
            // information. Succession is declared as adjacent intervals, not as overlap.
            if (existing.EffectiveFrom < normalised.EffectiveTo && normalised.EffectiveFrom < existing.EffectiveTo)
            {
                code = FactAuthorityCodes.ConflictingDeclaration;
                return false;
            }
        }

        _bindings.Add(normalised);
        code = string.Empty;
        return true;
    }

    public bool TryGetFact(string? factKey, out SemanticFactDeclaration? fact)
    {
        fact = null;

        if (!DeclaredKey.TryNormalise(factKey, out var normalised)) return false;

        return _facts.TryGetValue(normalised, out fact);
    }

    public IReadOnlyList<FactSourceAuthorityDeclaration> BindingsAt(string? factKey, DateTimeOffset asOf)
    {
        if (!DeclaredKey.TryNormalise(factKey, out var normalised))
        {
            return Array.Empty<FactSourceAuthorityDeclaration>();
        }

        return _bindings
            .Where(b => string.Equals(b.FactKey, normalised, StringComparison.Ordinal) && b.IsEffectiveAt(asOf))
            .OrderBy(b => b.SourceKey, StringComparer.Ordinal)
            .ToArray();
    }
}