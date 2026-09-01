using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Definitions;

/// <summary>
/// PPIQ T-091. IMPACT PREVIEW OVER THE CANONICAL DEPENDENCY GRAPH.
///
/// DIRECTION IS THE WHOLE CONTRACT. There are two walks over
/// ppiq_meta.definition_dependencies and confusing them silently inverts the
/// answer:
///
///   IMPACT  starts at the definition being changed and walks REVERSE.
///           "Who consumes me?"  edge.depends_on_definition_id = me
///
///   CLOSURE starts at the exported definition and walks FORWARD.
///           "What do I need?"   edge.definition_id = me
///
/// A caller shown the forward closure before publishing would be told that
/// changing a definition affects the things it reads, which is exactly
/// backwards, and would publish a breaking change believing nothing consumed
/// it. The distinction is protected by a named test.
///
/// READ-ONLY BY CONSTRUCTION. This contract has no write method and its
/// implementation opens no transaction. Impact preview is dependency evidence,
/// not publish-by-preview.
/// </summary>
public interface ICanonicalDefinitionGraph
{
    /// <summary>
    /// Everything that consumes the given definition, directly or transitively,
    /// within the caller's tenant. Deduplicated by definition identity at its
    /// shallowest depth, ordered deterministically, and bounded defensively
    /// even though the store rejects cycles.
    /// </summary>
    Task<ApplicationResult<DefinitionImpact>> PreviewImpactAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The forward requirement closure: everything the given definition needs
    /// in order to reproduce its own semantics. This is the export walk and it
    /// must never be used to answer an impact question.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<DefinitionClosureNode>>> ResolveClosureAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken cancellationToken);
}

/// <summary>
/// The blast radius of a pending change. Consumers are already deduplicated
/// and ordered; Summary is a convenience over the same list so a caller does
/// not recount it differently.
/// </summary>
public sealed record DefinitionImpact(
    Guid DefinitionId,
    string DefinitionCode,
    DefinitionKind Kind,
    string Surface,
    IReadOnlyList<ImpactedConsumer> Consumers,
    IReadOnlyList<ImpactSummaryEntry> SummaryByKind,
    int MaximumDepthReached,
    bool Truncated);

/// <summary>
/// One consumer of the changed definition.
///
/// CompatibilityRisk is deliberately three-valued. T-091 does not own a
/// semantic compatibility engine and none exists to consume, so a consumer
/// whose risk cannot be determined from current metadata reports NotEvaluated.
/// A fabricated "Safe" would be read as a fact by the caller deciding whether
/// to publish, and it would not be one.
/// </summary>
public sealed record ImpactedConsumer(
    Guid DefinitionId,
    string DefinitionCode,
    DefinitionKind Kind,
    string Surface,
    int? PublishedVersionNumber,
    Guid? PublishedVersionId,
    int? CurrentVersionNumber,
    ImpactRelationship Relationship,
    int Depth,
    string DependencyKind,
    bool IsRequired,
    int? PinnedDependsOnVersion,
    CompatibilityRisk CompatibilityRisk);

public sealed record ImpactSummaryEntry(DefinitionKind Kind, int ConsumerCount, int BreakingCount);

public enum ImpactRelationship
{
    Direct = 1,
    Transitive = 2,
}

/// <summary>
/// Breaking is only reported where the canonical store itself carries the
/// evidence: a consumer pinned to a specific version of the definition being
/// changed will not follow a new version, which is a fact the edge records.
/// Everything else is NotEvaluated until a compatibility authority exists.
/// </summary>
public enum CompatibilityRisk
{
    NotEvaluated = 0,
    PinnedToExistingVersion = 1,
}

/// <summary>One node of a forward requirement closure, with its dependency depth.</summary>
public sealed record DefinitionClosureNode(
    Guid DefinitionId,
    string DefinitionCode,
    DefinitionKind Kind,
    string Surface,
    int Depth,
    IReadOnlyList<DefinitionClosureEdge> Requires);

public sealed record DefinitionClosureEdge(
    Guid DependsOnDefinitionId,
    string DependsOnDefinitionCode,
    string DependencyKind,
    bool IsRequired,
    int? DependsOnVersion);
