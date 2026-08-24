// Analysis Subject and Grain contract.
//
// Backlog origin: T-209.
//
// Answers exactly two questions:
//   what thing is being analysed, and
//   at what grain the evidence exists or may be transformed.
//
// Everything here is declared by the customer's engineer. Nothing is assumed. There is
// no default subject, no default grain, no built-in hierarchy, and no meaning inferred
// from what a key happens to be called. Two keys are related only because somebody
// declared them related.
//
// This contract deliberately does NOT own the aggregation vocabulary. A transformation
// is identified by a declared code and carries the metadata that code needs; whether
// duration weighting, an arithmetic mean, a sum, a rate integral or a custom declared
// semantic is valid and executable is the aggregation-semantics kernel's judgement, not
// this file's. The law enforced here is narrower and stronger: no transformation is ever
// chosen automatically.
//
// Refusal reuses the existing kernel language rather than inventing a parallel one: an
// unresolvable subject or grain is ExclusionAttribution.Declaration - the data may be
// perfectly adequate, nothing has said what it means - and never Data.
//
// Persistence of these declarations belongs to the platform lane. The invariants
// enforced below are what that storage must uphold.
using System;
using System.Collections.Generic;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// The single structural normalisation rule for every declared identity in this
/// contract. Trim and nothing else: no case folding, no separator rewriting, no
/// vocabulary interpretation. One rule, applied at every declaration and lookup
/// boundary, so that a key with stray whitespace can never become a second identity.
/// </summary>
public static class DeclaredKey
{
    public static bool TryNormalise(string? candidate, out string normalised)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            normalised = string.Empty;
            return false;
        }

        normalised = candidate.Trim();
        return true;
    }
}

/// <summary>
/// A declared grain key. There is no implicit value: an instance either carries a key
/// somebody declared, or it is Undeclared and every operation on it fails closed.
/// </summary>
public sealed record GrainIdentifier
{
    /// <summary>The absence of a declaration. Never a usable grain.</summary>
    public static readonly GrainIdentifier Undeclared = new(string.Empty);

    private GrainIdentifier(string key) => Key = key;

    public string Key { get; }

    public bool IsDeclared => Key.Length > 0;

    public static bool TryCreate(string? key, out GrainIdentifier identifier)
    {
        if (!DeclaredKey.TryNormalise(key, out var normalised))
        {
            identifier = Undeclared;
            return false;
        }

        identifier = new GrainIdentifier(normalised);
        return true;
    }

    /// <summary>For call sites that already hold a validated key.</summary>
    public static GrainIdentifier Declared(string key)
    {
        if (!TryCreate(key, out var identifier))
        {
            throw new ArgumentException("A grain key must be declared and non-empty.", nameof(key));
        }

        return identifier;
    }

    public override string ToString() => IsDeclared ? Key : "(undeclared)";
}

/// <summary>
/// What is being analysed, and the grain at which its evidence exists. Both are the
/// customer's declaration.
/// </summary>
public sealed record AnalysisSubjectDefinition(string SubjectKey, GrainIdentifier Grain);

/// <summary>
/// One grain level and its declared parent. A root grain has an Undeclared parent.
/// Lineage exists only where it has been declared.
/// </summary>
public sealed record GrainDefinition(GrainIdentifier Grain, GrainIdentifier Parent);

/// <summary>
/// How two grains stand to one another. Undeclared and Unrelated are distinct: the
/// first means somebody has not spoken, the second means somebody has, and the answer
/// is no.
/// </summary>
public enum GrainRelationship
{
    Undeclared,
    Same,
    TargetIsAncestor,
    TargetIsDescendant,
    Unrelated
}

/// <summary>
/// A declared, directional transformation across a grain boundary.
/// <para>
/// TransformationCode is an opaque declared identifier, not a member of a fixed
/// mathematical vocabulary. This contract stores and returns it; the aggregation
/// semantics kernel decides what it means and whether it can be executed. WeightKey
/// carries the parameter a weighted code needs and is empty for codes that need none -
/// again, which is which is not this file's judgement.
/// </para>
/// <para>
/// From/To is not symmetric: declaring how to roll child evidence up says nothing about
/// how to push parent evidence down.
/// </para>
/// </summary>
public sealed record GrainTransformation(
    GrainIdentifier From,
    GrainIdentifier To,
    string TransformationCode,
    string WeightKey);

/// <summary>
/// The verdict on moving evidence from one grain to another.
/// </summary>
public sealed record GrainCompatibility(
    GrainRelationship Relationship,
    bool IsPermitted,
    bool RequiresTransformation,
    GrainTransformation? Transformation,
    string Code,
    TerminalState Outcome,
    ExclusionAttribution Attribution);

/// <summary>
/// What a caller may act on once compatibility has been established. A plan never
/// carries a number; it carries the declaration under which a number may be computed.
/// </summary>
public sealed record GrainAggregationPlan(
    AnalysisSubjectDefinition Subject,
    GrainIdentifier SourceGrain,
    GrainIdentifier TargetGrain,
    bool RequiresTransformation,
    GrainTransformation? Transformation);

/// <summary>
/// Resolution outcome. Either a plan exists, or a refusal explains why not. There is no
/// third result, and a refusal never carries a value that could be mistaken for zero.
/// </summary>
public sealed record GrainResolution(
    bool IsResolved,
    GrainAggregationPlan? Plan,
    GrainCompatibility Verdict);

/// <summary>
/// Refusal and permission codes. Stable strings, so a consumer can branch on them
/// without parsing prose.
/// </summary>
public static class GrainContractCodes
{
    public const string SubjectNotDeclared = "GR01 subject_not_declared";
    public const string GrainNotDeclared = "GR02 grain_not_declared";
    public const string IncompatibleGrain = "GR03 incompatible_grain";
    public const string TransformationNotDeclared = "GR04 grain_transformation_not_declared";
    public const string TransformationCodeNotDeclared = "GR05 transformation_code_not_declared";
    public const string ConflictingDeclaration = "GR06 conflicting_declaration";
    public const string LineageCycle = "GR07 lineage_cycle";

    public const string SameGrain = "GR10 same_grain_no_transformation_required";
    public const string TransformationDeclared = "GR11 declared_transformation_applies";
}

/// <summary>
/// The declarations in force. Starts empty and stays empty until the customer's
/// engineer declares something: there is no seeded subject, grain or lineage of any
/// kind, in any vocabulary.
///
/// <para>
/// Declaration invariants, which the durable store must uphold identically:
/// an identical redeclaration is idempotent; a conflicting redeclaration under the same
/// key fails closed rather than silently overwriting; and a lineage cycle is rejected at
/// declaration time rather than admitted and worked around at traversal time.
/// </para>
/// </summary>
public sealed class AnalysisSubjectAndGrainRegistry
{
    private readonly Dictionary<string, AnalysisSubjectDefinition> _subjects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GrainDefinition> _grains = new(StringComparer.Ordinal);
    private readonly Dictionary<(string From, string To), GrainTransformation> _transformations = new();

    public int SubjectCount => _subjects.Count;
    public int GrainCount => _grains.Count;
    public int TransformationCount => _transformations.Count;

    public bool TryDeclareGrain(GrainDefinition definition, out string code)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!definition.Grain.IsDeclared)
        {
            code = GrainContractCodes.GrainNotDeclared;
            return false;
        }

        // A grain cannot be its own parent, and cannot sit beneath one of its own
        // descendants. Invalid lineage is refused entry rather than admitted and
        // survived at traversal time.
        if (definition.Parent.IsDeclared)
        {
            if (string.Equals(definition.Parent.Key, definition.Grain.Key, StringComparison.Ordinal) ||
                IsAncestorOf(definition.Grain, definition.Parent))
            {
                code = GrainContractCodes.LineageCycle;
                return false;
            }

            if (!_grains.ContainsKey(definition.Parent.Key))
            {
                code = GrainContractCodes.GrainNotDeclared;
                return false;
            }
        }

        if (_grains.TryGetValue(definition.Grain.Key, out var existing))
        {
            // Saying the same thing twice is not a conflict. Saying a different thing
            // under the same key is, and it must not win by arriving later.
            if (existing == definition)
            {
                code = string.Empty;
                return true;
            }

            code = GrainContractCodes.ConflictingDeclaration;
            return false;
        }

        _grains[definition.Grain.Key] = definition;
        code = string.Empty;
        return true;
    }

    public bool TryDeclareSubject(AnalysisSubjectDefinition definition, out string code)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!DeclaredKey.TryNormalise(definition.SubjectKey, out var subjectKey))
        {
            code = GrainContractCodes.SubjectNotDeclared;
            return false;
        }

        if (!definition.Grain.IsDeclared || !_grains.ContainsKey(definition.Grain.Key))
        {
            code = GrainContractCodes.GrainNotDeclared;
            return false;
        }

        var normalised = definition with { SubjectKey = subjectKey };

        if (_subjects.TryGetValue(subjectKey, out var existing))
        {
            if (existing == normalised)
            {
                code = string.Empty;
                return true;
            }

            code = GrainContractCodes.ConflictingDeclaration;
            return false;
        }

        _subjects[subjectKey] = normalised;
        code = string.Empty;
        return true;
    }

    public bool TryDeclareTransformation(GrainTransformation transformation, out string code)
    {
        ArgumentNullException.ThrowIfNull(transformation);

        if (!transformation.From.IsDeclared || !_grains.ContainsKey(transformation.From.Key) ||
            !transformation.To.IsDeclared || !_grains.ContainsKey(transformation.To.Key))
        {
            code = GrainContractCodes.GrainNotDeclared;
            return false;
        }

        // The code is the declaration. What it means is decided elsewhere; that it was
        // stated at all is decided here.
        if (!DeclaredKey.TryNormalise(transformation.TransformationCode, out var transformationCode))
        {
            code = GrainContractCodes.TransformationCodeNotDeclared;
            return false;
        }

        var normalised = transformation with
        {
            TransformationCode = transformationCode,
            WeightKey = transformation.WeightKey?.Trim() ?? string.Empty
        };

        var key = (normalised.From.Key, normalised.To.Key);

        if (_transformations.TryGetValue(key, out var existing))
        {
            if (existing == normalised)
            {
                code = string.Empty;
                return true;
            }

            code = GrainContractCodes.ConflictingDeclaration;
            return false;
        }

        _transformations[key] = normalised;
        code = string.Empty;
        return true;
    }

    public bool TryGetSubject(string? subjectKey, out AnalysisSubjectDefinition? subject)
    {
        subject = null;

        if (!DeclaredKey.TryNormalise(subjectKey, out var normalised)) return false;

        return _subjects.TryGetValue(normalised, out subject);
    }

    public bool IsGrainDeclared(GrainIdentifier grain) =>
        grain.IsDeclared && _grains.ContainsKey(grain.Key);

    public GrainIdentifier ParentOf(GrainIdentifier grain)
    {
        if (grain.IsDeclared && _grains.TryGetValue(grain.Key, out var definition))
        {
            return definition.Parent;
        }

        return GrainIdentifier.Undeclared;
    }

    public bool TryGetTransformation(GrainIdentifier from, GrainIdentifier to, out GrainTransformation? transformation)
    {
        transformation = null;

        if (!from.IsDeclared || !to.IsDeclared) return false;

        return _transformations.TryGetValue((from.Key, to.Key), out transformation);
    }

    private bool IsAncestorOf(GrainIdentifier candidateAncestor, GrainIdentifier descendant)
    {
        var current = descendant;
        var guard = 0;

        while (current.IsDeclared && guard++ < 64)
        {
            if (string.Equals(current.Key, candidateAncestor.Key, StringComparison.Ordinal)) return true;
            current = ParentOf(current);
        }

        return false;
    }
}