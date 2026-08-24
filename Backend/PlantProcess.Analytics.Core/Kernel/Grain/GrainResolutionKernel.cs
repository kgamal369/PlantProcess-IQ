// Grain resolution kernel.
//
// Backlog origin: T-209.
//
// Decides whether evidence may move from one grain to another, and under which declared
// transformation. It computes no statistics and interprets no transformation code: it
// produces the declaration a caller must honour, or a refusal. What a code means, and
// whether it is executable, belongs to the aggregation semantics kernel.
//
// Every path out of this file is either a plan or a refusal carrying a code. There is no
// path that returns a number, a default grain, or a silently chosen transformation.
using System;

namespace PlantProcess.Analytics.Core.Kernel;

public static class GrainResolutionKernel
{
    /// <summary>
    /// How the target grain stands to the source, using declared lineage only. Two keys
    /// are related because a declaration says so, never because of what they are called.
    /// </summary>
    public static GrainRelationship Relate(
        AnalysisSubjectAndGrainRegistry registry,
        GrainIdentifier source,
        GrainIdentifier target)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (!registry.IsGrainDeclared(source) || !registry.IsGrainDeclared(target))
        {
            return GrainRelationship.Undeclared;
        }

        if (string.Equals(source.Key, target.Key, StringComparison.Ordinal))
        {
            return GrainRelationship.Same;
        }

        if (WalksUpTo(registry, source, target)) return GrainRelationship.TargetIsAncestor;
        if (WalksUpTo(registry, target, source)) return GrainRelationship.TargetIsDescendant;

        return GrainRelationship.Unrelated;
    }

    /// <summary>
    /// Whether evidence may move from the source grain to the target, and what that
    /// requires. Compatibility is explicit and directional.
    /// </summary>
    public static GrainCompatibility Compatibility(
        AnalysisSubjectAndGrainRegistry registry,
        GrainIdentifier source,
        GrainIdentifier target)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var relationship = Relate(registry, source, target);

        switch (relationship)
        {
            case GrainRelationship.Undeclared:
                return Refuse(relationship, GrainContractCodes.GrainNotDeclared);

            case GrainRelationship.Same:
                // Same grain is the one case that needs nothing declared beyond the grain
                // itself. It is not an exception to the rule; there is no boundary to cross.
                return new GrainCompatibility(
                    relationship,
                    IsPermitted: true,
                    RequiresTransformation: false,
                    Transformation: null,
                    GrainContractCodes.SameGrain,
                    TerminalState.Finding,
                    ExclusionAttribution.None);

            case GrainRelationship.Unrelated:
                return Refuse(relationship, GrainContractCodes.IncompatibleGrain);

            default:
                if (!registry.TryGetTransformation(source, target, out var transformation) || transformation is null)
                {
                    // Declared lineage is not permission. Knowing that one grain sits under
                    // another says nothing about how evidence combines across that boundary,
                    // and nothing here will pick a transformation on the customer's behalf.
                    return Refuse(relationship, GrainContractCodes.TransformationNotDeclared);
                }

                return new GrainCompatibility(
                    relationship,
                    IsPermitted: true,
                    RequiresTransformation: true,
                    transformation,
                    GrainContractCodes.TransformationDeclared,
                    TerminalState.Finding,
                    ExclusionAttribution.None);
        }
    }

    /// <summary>
    /// Resolve a named subject onto a target grain. Fails closed on an undeclared
    /// subject, an undeclared grain, an unrelated grain, or a declared boundary with no
    /// declared transformation.
    /// </summary>
    public static GrainResolution Resolve(
        AnalysisSubjectAndGrainRegistry registry,
        string? subjectKey,
        GrainIdentifier targetGrain)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (!registry.TryGetSubject(subjectKey, out var subject) || subject is null)
        {
            return Unresolved(GrainRelationship.Undeclared, GrainContractCodes.SubjectNotDeclared);
        }

        if (!registry.IsGrainDeclared(targetGrain))
        {
            return Unresolved(GrainRelationship.Undeclared, GrainContractCodes.GrainNotDeclared);
        }

        var compatibility = Compatibility(registry, subject.Grain, targetGrain);

        if (!compatibility.IsPermitted)
        {
            return new GrainResolution(IsResolved: false, Plan: null, compatibility);
        }

        var plan = new GrainAggregationPlan(
            subject,
            subject.Grain,
            targetGrain,
            compatibility.RequiresTransformation,
            compatibility.Transformation);

        return new GrainResolution(IsResolved: true, plan, compatibility);
    }

    private static bool WalksUpTo(
        AnalysisSubjectAndGrainRegistry registry,
        GrainIdentifier from,
        GrainIdentifier candidateAncestor)
    {
        var current = registry.ParentOf(from);
        var guard = 0;

        // The registry rejects cyclic lineage at declaration time, so this bound should
        // be unreachable. It stays as defence in depth: if a future durable store ever
        // admits a cycle, traversal terminates instead of hanging the caller.
        while (current.IsDeclared && guard++ < 64)
        {
            if (string.Equals(current.Key, candidateAncestor.Key, StringComparison.Ordinal)) return true;
            current = registry.ParentOf(current);
        }

        return false;
    }

    private static GrainCompatibility Refuse(GrainRelationship relationship, string code) =>
        new(relationship,
            IsPermitted: false,
            RequiresTransformation: false,
            Transformation: null,
            code,
            TerminalState.RefusedByGuard,
            ExclusionAttribution.Declaration);

    private static GrainResolution Unresolved(GrainRelationship relationship, string code) =>
        new(IsResolved: false, Plan: null, Refuse(relationship, code));
}