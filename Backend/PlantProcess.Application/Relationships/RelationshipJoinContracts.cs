namespace PlantProcess.Application.Relationships;

/// <summary>
/// T-058. One key comparison in an executable join, already oriented for the
/// direction the plan travels.
///
/// Orientation matters and is the reason this is not just the published member.
/// A relationship is declared once, left to right, but a plan may traverse it in
/// either direction; traversing it backwards and keeping the declared column
/// order would silently join the wrong columns to each other.
/// </summary>
public sealed record RelationshipJoinPredicateDto(
    string FromColumn,
    string ToColumn,
    string Comparison,
    short Order);

/// <summary>T-058. One hop of an executable join.</summary>
public sealed record RelationshipJoinStepDto(
    Guid RelationshipId,
    string RelationshipCode,
    string FromEntity,
    string ToEntity,
    string JoinType,
    string Cardinality,
    bool CrossesGrain,
    string? AttributionRule,
    IReadOnlyList<RelationshipJoinPredicateDto> Predicates);

/// <summary>
/// T-058. What a query compiler needs in order to join two entities, or the
/// named reason it may not.
///
/// A refused plan carries NO steps. There is deliberately no partial plan: a
/// consumer handed half a join would run it and return numbers that look like
/// an answer.
/// </summary>
public sealed record RelationshipJoinPlanDto(
    string FromEntity,
    string ToEntity,
    string Purpose,
    bool Planned,
    IReadOnlyList<RelationshipJoinStepDto> Steps,
    bool CrossesGrain,
    bool RequiresAttribution,
    string? RefusalCode,
    string? RefusalMessage,
    IReadOnlyList<string> CandidatePaths);