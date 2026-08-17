namespace PlantProcess.Application.Relationships;

/// <summary>
/// T-057. The frozen refusal vocabulary for the plant relationship model.
///
/// This catalogue is the product's, not this task's. T-057 does not exercise
/// every code, but it must not publish a reduced second vocabulary that a later
/// task then has to widen - a consumer that learned four codes must not have to
/// relearn them when the resolver lands.
/// </summary>
public static class RelationshipRefusalCodes
{
    /// <summary>Two unretired paths between one entity pair and no preferred path. Both paths are named; nothing is guessed.</summary>
    public const string AmbiguousPath = "RL01";

    /// <summary>An unproven relationship reached by an automated consumer. Manual exploration is still permitted.</summary>
    public const string UnprovenRelationship = "RL02";

    /// <summary>No declared path between the two entities.</summary>
    public const string NoPath = "RL03";

    /// <summary>Retirement refused because an active dependent still resolves through it.</summary>
    public const string RetirementBlocked = "RL04";
}

/// <summary>T-057. Publication-time refusals, distinct from resolution refusals.</summary>
public static class RelationshipPublicationCodes
{
    public const string GrainConversionWithoutAttribution = "TR09";
    public const string MembersOutOfOrderOrIncomplete = "TR08";
    public const string UnknownVocabulary = "RL00";
}

public static class RelationshipJoinTypes
{
    public const string Inner = "inner";
    public const string Left = "left";
    public const string Right = "right";
    public const string Full = "full";
    public static readonly IReadOnlyList<string> All = new[] { Inner, Left, Right, Full };
}

public static class RelationshipCardinalities
{
    public const string OneToOne = "1-1";
    public const string OneToMany = "1-n";
    public const string ManyToOne = "n-1";
    public const string ManyToMany = "n-m";
    public static readonly IReadOnlyList<string> All = new[] { OneToOne, OneToMany, ManyToOne, ManyToMany };
}

public static class RelationshipAttributionRules
{
    public const string Weighted = "weighted";
    public const string EqualSplit = "equal_split";
    public const string FirstParent = "first_parent";
    public const string None = "none";
    public static readonly IReadOnlyList<string> All = new[] { Weighted, EqualSplit, FirstParent, None };
}

public static class RelationshipAmbiguityStates
{
    public const string Unambiguous = "unambiguous";
    public const string Ambiguous = "ambiguous";
    public const string Resolved = "resolved";
    public static readonly IReadOnlyList<string> All = new[] { Unambiguous, Ambiguous, Resolved };
}

public static class RelationshipValidationStates
{
    public const string Unproven = "unproven";
    public const string Validated = "validated";
    public const string Failed = "failed";
    public static readonly IReadOnlyList<string> All = new[] { Unproven, Validated, Failed };
}

/// <summary>
/// T-057. The purposes a consumer may resolve for. The list is the product's
/// sixteen consumers plus evidence walk-back; it is exhaustive and binding,
/// because an unproven relationship is usable by exploration and not by
/// training, and that decision cannot be made without knowing who is asking.
///
/// Frozen here; enforced by the resolver, which is T-058.
/// </summary>
public static class RelationshipConsumerPurposes
{
    public const string Projection = "projection";
    public const string RegistryGeneration = "registry_generation";
    public const string QueryCompiler = "query_compiler";
    public const string AssociativeFiltering = "associative_filtering";
    public const string DrillDown = "drill_down";
    public const string DrillThrough = "drill_through";
    public const string Genealogy = "genealogy";
    public const string StatisticalAnalysis = "statistical_analysis";
    public const string Correlation = "correlation";
    public const string FeatureEngineering = "feature_engineering";
    public const string ModelTraining = "model_training";
    public const string ModelScoring = "model_scoring";
    public const string PracticeLearning = "practice_learning";
    public const string PredictionAndRemediation = "prediction_and_remediation";
    public const string ValueCalculation = "value_calculation";
    public const string AssistantRetrieval = "assistant_retrieval";
    public const string EvidenceWalkBack = "evidence_walk_back";
    public const string Explore = "explore";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Projection, RegistryGeneration, QueryCompiler, AssociativeFiltering, DrillDown,
        DrillThrough, Genealogy, StatisticalAnalysis, Correlation, FeatureEngineering,
        ModelTraining, ModelScoring, PracticeLearning, PredictionAndRemediation,
        ValueCalculation, AssistantRetrieval, EvidenceWalkBack, Explore
    };

    /// <summary>
    /// Exploration is manual and may traverse an unproven relationship.
    /// Everything else is automated and may not. This is the RL02 boundary.
    /// </summary>
    public static bool IsAutomated(string purpose) =>
        !string.Equals(purpose, Explore, StringComparison.Ordinal);
}

/// <summary>One ordered key pair. Order matters: real plants key on two or three columns.</summary>
public sealed record RelationshipMemberDto(
    string LeftColumn,
    string RightColumn,
    short MemberOrder,
    string Comparison = "=");

/// <summary>
/// T-057. One declared relationship as every consumer sees it.
///
/// This shape is the FINAL EXTERNAL CONTRACT. The physical storage behind it in
/// M1 is compatibility persistence and is deliberately not nameable from here;
/// T-095 replaces that storage and this record does not change.
/// </summary>
public sealed record RelationshipDto(
    Guid Id,
    string RelationshipCode,
    string LeftEntity,
    string RightEntity,
    string JoinType,
    string Cardinality,
    string GrainLeft,
    string GrainRight,
    bool IsGrainConverting,
    string? AttributionRule,
    string? AttributionExpression,
    bool IsPreferredPath,
    string AmbiguityState,
    string ValidationState,
    Guid SourceDefinitionId,
    int SourceDefinitionVersion,
    DateTime EffectiveFromUtc,
    DateTime? RetiredAtUtc,
    IReadOnlyList<RelationshipMemberDto> Members);

/// <summary>An entity and how many unretired relationships touch it.</summary>
public sealed record RelationshipEntityDto(string Entity, int RelationshipCount);

/// <summary>
/// T-057. What a definition publication declares. There is deliberately no
/// authoring endpoint for this: relationships are EMITTED by publishing a
/// transformation definition, never authored directly. A public create endpoint
/// would be a temporary M1 contract that M2 must delete.
/// </summary>
public sealed record RelationshipDeclaration(
    string RelationshipCode,
    string LeftEntity,
    string RightEntity,
    string JoinType,
    string Cardinality,
    string GrainLeft,
    string GrainRight,
    string? AttributionRule,
    string? AttributionExpression,
    bool IsPreferredPath,
    IReadOnlyList<RelationshipMemberDto> Members);

public sealed record RelationshipPublicationRequest(
    Guid SourceDefinitionId,
    int SourceDefinitionVersion,
    IReadOnlyList<RelationshipDeclaration> Relationships);

/// <summary>
/// T-057 freezes this shape; T-058 makes it execute. A consumer written against
/// it today will not be rewritten when resolution lands.
/// </summary>
public sealed record RelationshipPathStepDto(Guid RelationshipId, string FromEntity, string ToEntity);

public sealed record RelationshipResolutionDto(
    string FromEntity,
    string ToEntity,
    string Purpose,
    bool Resolved,
    IReadOnlyList<RelationshipPathStepDto> Path,
    bool CrossesGrain,
    string? RefusalCode,
    string? RefusalMessage,
    IReadOnlyList<string> CandidatePaths);