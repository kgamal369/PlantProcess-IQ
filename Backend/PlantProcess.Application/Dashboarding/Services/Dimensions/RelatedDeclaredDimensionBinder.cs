using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Relationships;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// A declared dimension published against a DIFFERENT entity than the population it is
/// filtering, resolved the only lawful way: by asking the relationship authority.
///
/// The hard part was never the query. It was that the two sides of the question were
/// spoken in different languages. A declaration arrives holding a CLR type; a population
/// holds a CLR type; a published relationship holds canonical entity names. Naming BOTH
/// endpoints through the same catalogue is what makes the question askable at all, and
/// it is why both go through ICanonicalEntityCatalog here rather than one coming from a
/// free string on the declaration. A second string trusted as path authority is a second
/// authority, however harmless it looks.
///
/// What this class does not do is as important as what it does. It does not look at the
/// mapped model to see how the two entities are connected, does not count candidates,
/// does not prefer one path over another and does not decide that a single obvious
/// reference is good enough. All of that is the resolver's, and the plant's preference
/// is data the plant publishes. Change which path is preferred and the answer here
/// changes with no build.
/// </summary>
/// <summary>
/// The keys a related declaration selected, together with the governed plan that
/// reached them. The plan travels with the answer so that evidence and replay can say
/// which relationship, in which version, produced a number - an anonymous key set
/// cannot be explained later.
/// </summary>
public sealed record RelatedSubjectSelection(
    IReadOnlyList<Guid> Keys,
    RelationshipJoinPlanDto Plan);

public interface IRelatedDeclaredDimensionBinder
{
    Task<RelatedSubjectSelection> SubjectKeysWhereDeclaredEqualsAsync(
        DeclaredDimension declared,
        Type subjectEntityType,
        string value,
        string consumerPurpose,
        CancellationToken cancellationToken);
}

public sealed class RelatedDeclaredDimensionBinder : IRelatedDeclaredDimensionBinder
{
    private readonly ICanonicalEntityCatalog _canonicalEntities;
    private readonly IRelationshipJoinPlanner _planner;
    private readonly IRelationshipPlanSubjectKeyExecutor _executor;

    public RelatedDeclaredDimensionBinder(
        ICanonicalEntityCatalog canonicalEntities,
        IRelationshipJoinPlanner planner,
        IRelationshipPlanSubjectKeyExecutor executor)
    {
        _canonicalEntities = canonicalEntities;
        _planner = planner;
        _executor = executor;
    }

    public async Task<RelatedSubjectSelection> SubjectKeysWhereDeclaredEqualsAsync(
        DeclaredDimension declared,
        Type subjectEntityType,
        string value,
        string consumerPurpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(subjectEntityType);

        DeclaredDimensionProjection.RequireBindableAnywhere(declared);

        var sourceType = declared.SourceEntityType!;

        var fromEntity = _canonicalEntities.NameOf(sourceType);
        if (fromEntity is null)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.Unbindable,
                declared.Code,
                "Declared dimension '" + declared.Code + "' binds to " + sourceType.Name +
                ", which the canonical model does not map as an addressable entity.");
        }

        // The published catalogue name and the resolved type must agree. They are two
        // records of one decision, and a declaration where they disagree is a
        // declaration whose meaning depends on which one you read.
        if (!string.IsNullOrWhiteSpace(declared.SourceCatalog) &&
            !string.Equals(declared.SourceCatalog, fromEntity, StringComparison.Ordinal))
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.Unbindable,
                declared.Code,
                "Declared dimension '" + declared.Code + "' names catalogue '" + declared.SourceCatalog +
                "' but resolved to canonical entity '" + fromEntity + "'. The declaration is not normalised silently.");
        }

        var toEntity = _canonicalEntities.NameOf(subjectEntityType);
        if (toEntity is null)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.Unbindable,
                declared.Code,
                "The population of this query is " + subjectEntityType.Name +
                ", which the canonical model does not map as an addressable entity.");
        }

        var planned = await _planner.PlanAsync(fromEntity, toEntity, consumerPurpose, cancellationToken);

        if (!planned.IsSuccess)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.Unbindable,
                declared.Code,
                planned.Error?.Message ?? "The relationship authority could not be consulted.");
        }

        var plan = planned.Value!;

        // A refusal travels by its own name. RL03 is not DB06 wearing a dashboard hat,
        // and translating it back would rebuild the second vocabulary this task removed.
        if (!plan.Planned)
        {
            throw new RelationshipPathRefusalException(
                plan.RefusalCode!,
                declared.Code,
                fromEntity,
                toEntity,
                consumerPurpose,
                plan.RefusalMessage ?? "The relationship authority refused this path.",
                plan.CandidatePaths);
        }

        var keys = await _executor.SubjectKeysAsync(plan, declared, subjectEntityType, value, cancellationToken);
        return new RelatedSubjectSelection(keys, plan);
    }
}

/// <summary>
/// A governed path refusal, surfaced with the resolver's own code.
///
/// It is deliberately not a DimensionBindingRefusalException. A dimension refusal says
/// something about a declaration; this says something about the relationship model, and
/// the plant fixes it by publishing or preferring a relationship rather than by editing
/// a dimension.
/// </summary>
public sealed class RelationshipPathRefusalException : Exception
{
    public RelationshipPathRefusalException(
        string refusalCode,
        string dimensionCode,
        string fromEntity,
        string toEntity,
        string consumerPurpose,
        string message,
        IReadOnlyList<string> candidatePaths)
        : base(message)
    {
        RefusalCode = refusalCode;
        DimensionCode = dimensionCode;
        FromEntity = fromEntity;
        ToEntity = toEntity;
        ConsumerPurpose = consumerPurpose;
        CandidatePaths = candidatePaths;
    }

    public string RefusalCode { get; }
    public string DimensionCode { get; }
    public string FromEntity { get; }
    public string ToEntity { get; }
    public string ConsumerPurpose { get; }
    public IReadOnlyList<string> CandidatePaths { get; }
}
