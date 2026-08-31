namespace PlantProcess.Application.Definitions;

/// <summary>
/// PPIQ T-090. The sixteen canonical definition kinds.
///
/// NUMERIC VALUES 1..11 ARE FROZEN. They are the M1 contract and are ordered by
/// when they were introduced, not by surface. An earlier draft of this file was
/// generated in surface order and silently renumbered eight of the eleven -
/// Analysis 4 to 11, Model 5 to 15, Filter 9 to 5 - while still containing the
/// same sixteen names, so a literal-set check passed. Any persisted integer or
/// serialised contract would have changed meaning underneath it. Members 12..16
/// are additive and nothing existing moves.
///
/// THE ENUM IS NOT THE AUTHORITY ON ITS OWN. Three records of these sixteen
/// facts exist - this enum, DefinitionKindRegistry, and ck_definition_store_kind
/// in script 831. Architecture gates prove all three agree on the literal set,
/// and prove separately that historic numeric values did not move.
///
/// SM-06 outcome semantics are deliberately absent: a typed child contract of
/// the S1 transformation version, not a seventeenth kind.
/// </summary>
public enum DefinitionKind
{
    /// <summary>Surface S1, stored as 'transformation'.</summary>
    Transformation = 1,

    /// <summary>Surface S2, stored as 'page'.</summary>
    Page = 2,

    /// <summary>Surface S2, stored as 'widget'.</summary>
    Widget = 3,

    /// <summary>Surface S3, stored as 'analysis'.</summary>
    Analysis = 4,

    /// <summary>Surface S4, stored as 'model'.</summary>
    Model = 5,

    /// <summary>Surface S5, stored as 'log_rule'.</summary>
    LogRule = 6,

    /// <summary>Surface S2, stored as 'master_dimension'.</summary>
    MasterDimension = 7,

    /// <summary>Surface S2, stored as 'master_measure'.</summary>
    MasterMeasure = 8,

    /// <summary>Surface S2, stored as 'filter'.</summary>
    Filter = 9,

    /// <summary>Surface S2, stored as 'hierarchy'.</summary>
    Hierarchy = 10,

    /// <summary>Surface S2, stored as 'bookmark'.</summary>
    Bookmark = 11,

    /// <summary>Surface S2, stored as 'saved_query'. Added by T-090.</summary>
    SavedQuery = 12,

    /// <summary>Surface S3, stored as 'feature_set'. Added by T-090.</summary>
    FeatureSet = 13,

    /// <summary>Surface S3, stored as 'practice'. Added by T-090.</summary>
    Practice = 14,

    /// <summary>Surface S2, stored as 'report'. Added by T-090.</summary>
    Report = 15,

    /// <summary>Surface S3, stored as 'scenario'. Added by T-090.</summary>
    Scenario = 16,
}
