using PlantProcess.Application.Definitions;

namespace PlantProcess.Infrastructure.Definitions;

/// <summary>
/// PPIQ T-090. THE ONE PLACE THAT KNOWS WHAT A KIND IS.
///
/// Sixteen definition kinds sit on one store. The difference between them is
/// four facts - the surface they belong to, the typed detail table carrying
/// their specialised columns, and the set of fields that table declares as
/// semantically writable. Those facts are data here, not branches in a service.
///
/// WHY THIS IS A LOOKUP AND NOT A SWITCH. A switch on kind inside the service
/// would make sixteen persistence paths wearing one interface, which is the
/// architecture this task exists to remove.
///
/// STATIC ON PURPOSE. The frozen T-039 validation constructs the service as
/// new DefinitionService(db) with no container. A registry that had to be
/// injected would force that test to change, and the test not changing is the
/// proof that the visible contract did not move.
///
/// WHY THE FIELD SETS LIVE HERE AND NOT IN information_schema. The physical
/// catalogue answers "does this column exist", which is not the same question
/// as "may a caller write this field". A column added for internal bookkeeping
/// would silently become writable if the catalogue were the only authority.
/// The registry declares the semantic contract; the catalogue is consulted
/// afterwards only to prove the two have not drifted apart.
///
/// GENERATED FROM THE DDL. These rows are produced from script 832's own
/// CREATE TABLE statements rather than transcribed, because a registry copied
/// by hand is a second record of one fact and will eventually disagree with
/// the schema it claims to describe.
///
/// SURFACE ASSIGNMENT IS A TECH LEAD RULING (28 Aug 2026). report is S2
/// because it is a presentation and delivery definition; scenario is S3
/// because it is a counterfactual analysis over a pinned model and does not
/// author the model. SM-06 outcome semantics are a typed child of the S1
/// transformation version and deliberately absent from this table - they are
/// not a seventeenth kind.
/// </summary>
public static class DefinitionKindRegistry
{
    /// <summary>
    /// What the store needs to know about one kind. DetailTable is null for the
    /// kinds Chapter 3 deliberately leaves payload-only: their whole definition
    /// lives in the version row, and inventing a detail table for them would be
    /// inventing structure the design does not have.
    /// </summary>
    public sealed record KindContract(
        DefinitionKind Kind,
        string StorageKind,
        string Surface,
        string? DetailTable,
        IReadOnlyList<DeclaredField> WritableFields)
    {
        public bool TryField(string name, out DeclaredField field)
        {
            foreach (var candidate in WritableFields)
            {
                if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
                {
                    field = candidate;
                    return true;
                }
            }

            field = default!;
            return false;
        }
    }

    /// <summary>
    /// A semantically writable field and the PHYSICAL storage type script 832
    /// declares for it. Storage type is not semantic meaning: ordinal_rank_map
    /// is jsonb, which says it must parse as JSON and says nothing about the
    /// logical shape SM-06 requires of it. Those are two gates and this is the
    /// first one. The type drives normalisation: a Json field is parsed and
    /// refused when invalid, and a Text field is left opaque rather than
    /// speculatively parsed. Without the type the writer would have to guess,
    /// and guessing produced two different hashing modes in an earlier draft.
    /// </summary>
    public sealed record DeclaredField(string Name, StorageType Storage);

    private static readonly IReadOnlyList<KindContract> All = new List<KindContract>
    {
        new(DefinitionKind.Transformation, "transformation", "S1", "transformation_details",
            new DeclaredField[] { new("target_entities", StorageType.Json), new("alias_declarations", StorageType.Json), new("emitted_relationship_ids", StorageType.Json), new("projection_mode", StorageType.Text) }),
        new(DefinitionKind.Page, "page", "S2", "page_details",
            new DeclaredField[] { new("layout_json", StorageType.Json), new("sheets", StorageType.Json), new("audience_roles", StorageType.Json), new("default_filters", StorageType.Json) }),
        new(DefinitionKind.Widget, "widget", "S2", "widget_details",
            new DeclaredField[] { new("widget_kind", StorageType.Text), new("chart_type", StorageType.Text), new("dimension_code", StorageType.Text), new("measure_code", StorageType.Text), new("column_roles", StorageType.Json), new("saved_filter_json", StorageType.Json), new("source_kind", StorageType.Text), new("intelligence_source", StorageType.Text) }),
        new(DefinitionKind.Report, "report", "S2", "report_details",
            new DeclaredField[] { new("sections", StorageType.Json), new("period_declaration", StorageType.Json), new("recipients", StorageType.Json), new("schedule_declaration", StorageType.Json), new("delivery_targets", StorageType.Json) }),
        new(DefinitionKind.Filter, "filter", "S2", null,
            Array.Empty<DeclaredField>()),
        new(DefinitionKind.MasterDimension, "master_dimension", "S2", null,
            Array.Empty<DeclaredField>()),
        new(DefinitionKind.MasterMeasure, "master_measure", "S2", null,
            Array.Empty<DeclaredField>()),
        new(DefinitionKind.Hierarchy, "hierarchy", "S2", null,
            Array.Empty<DeclaredField>()),
        new(DefinitionKind.Bookmark, "bookmark", "S2", null,
            Array.Empty<DeclaredField>()),
        new(DefinitionKind.SavedQuery, "saved_query", "S2", null,
            Array.Empty<DeclaredField>()),
        new(DefinitionKind.Analysis, "analysis", "S3", "analysis_details",
            new DeclaredField[] { new("outcome_code", StorageType.Text), new("grain_code", StorageType.Text), new("window_declaration", StorageType.Json), new("method_code", StorageType.Text), new("population_filters", StorageType.Json), new("stratification_dimensions", StorageType.Json) }),
        new(DefinitionKind.FeatureSet, "feature_set", "S3", "feature_set_details",
            new DeclaredField[] { new("feature_list", StorageType.Json), new("grain_code", StorageType.Text), new("window_declaration", StorageType.Json), new("missing_value_policy", StorageType.Text), new("scaling_policy", StorageType.Text) }),
        new(DefinitionKind.Practice, "practice", "S3", "practice_details",
            new DeclaredField[] { new("context_dimensions", StorageType.Json), new("parameter_set", StorageType.Json), new("tolerances", StorageType.Json), new("window_rule", StorageType.Json), new("outcomes", StorageType.Json), new("confounders", StorageType.Json), new("minimum_support", StorageType.Integer) }),
        new(DefinitionKind.Scenario, "scenario", "S3", "scenario_details",
            new DeclaredField[] { new("variables", StorageType.Json), new("ranges", StorageType.Json), new("fixed_assumptions", StorageType.Json), new("baseline_declaration", StorageType.Json), new("model_version_ref", StorageType.Uuid) }),
        new(DefinitionKind.Model, "model", "S4", "model_details",
            new DeclaredField[] { new("algorithm_code", StorageType.Text), new("hyperparameters", StorageType.Json), new("split_strategy", StorageType.Json), new("acceptance_floor", StorageType.Json) }),
        new(DefinitionKind.LogRule, "log_rule", "S5", "log_rule_details",
            new DeclaredField[] { new("condition_expression", StorageType.Text), new("severity", StorageType.Text), new("message_template", StorageType.Text), new("scope_declaration", StorageType.Json) }),
    };

    private static readonly Dictionary<DefinitionKind, KindContract> ByKind =
        All.ToDictionary(c => c.Kind);

    private static readonly Dictionary<string, KindContract> ByStorageKind =
        All.ToDictionary(c => c.StorageKind, StringComparer.Ordinal);

    /// <summary>Every kind the canonical store accepts, in declaration order.</summary>
    public static IReadOnlyList<KindContract> Contracts => All;

    /// <summary>
    /// The contract for one kind. An unknown kind is a refusal rather than a
    /// default: a definition written under a surface nobody declared would pass
    /// the database CHECK only by accident.
    /// </summary>
    public static bool TryResolve(DefinitionKind kind, out KindContract contract) =>
        ByKind.TryGetValue(kind, out contract!);

    /// <summary>
    /// Resolves the kind a stored literal represents. Absence throws rather
    /// than defaulting, because a silent default is how the registry and script
    /// 831 would drift apart without anyone noticing.
    /// </summary>
    public static bool TryResolveStorageKind(string storageKind, out KindContract contract) =>
        ByStorageKind.TryGetValue(storageKind, out contract!);

    /// <summary>The storage literal for a kind, exactly as script 831 spells it.</summary>
    public static string StorageKindOf(DefinitionKind kind) => ByKind[kind].StorageKind;

    /// <summary>The S1..S5 surface a kind belongs to.</summary>
    public static string SurfaceOf(DefinitionKind kind) => ByKind[kind].Surface;

    /// <summary>
    /// Columns on a detail table that the writer itself owns. They are named
    /// individually and never matched by pattern: a rule such as "ignore every
    /// column ending _id" would quietly swallow a real semantic field like
    /// scenario_details.model_version_ref the day someone renamed it.
    /// </summary>
    public static readonly IReadOnlyList<string> InfrastructureColumns = new[]
    {
        "definition_version_id"
    };

    /// <summary>
    /// PHYSICAL storage types 832 declares for detail columns, generated from
    /// the DDL. Used to drive safe canonicalisation and persistence validation
    /// only. Semantic validation may be stricter and lives elsewhere.
    /// </summary>
    public enum StorageType
    {
        Text = 1,
        Json = 2,
        Integer = 3,
        Uuid = 4,
        Boolean = 5,
    }

    /// <summary>
    /// The ten SM-06 fields, declared once. outcome_details is not a kind
    /// contract, so its field set is named separately rather than being absent.
    /// </summary>
    public static readonly IReadOnlyList<string> OutcomeFields = new[]
    {
        "outcome_code", "outcome_type", "class_taxonomy_ref", "ordinal_rank_map",
        "grain_code", "detection_position_code", "detection_timestamp_field",
        "direction", "unit_code", "censoring_policy"
    };

    /// <summary>
    /// The sentinel a migration writes when the legacy row could not supply a
    /// required semantic anchor. It may exist in a draft. It may never be
    /// published, because a published version is what downstream leakage gates
    /// treat as fact.
    ///
    /// Compared with IsUnknownSentinel, never by substring. A customer-authored
    /// opaque identifier such as legacy_migrated_unknown_mapping_v2 is a
    /// legitimate value and must not be refused because the sentinel text
    /// appears inside it.
    /// </summary>
    public const string MigratedUnknown = "migrated_unknown";

    /// <summary>
    /// True only when the complete semantic value IS the sentinel. Whitespace
    /// is trimmed because storage may pad; nothing else is inferred.
    /// </summary>
    public static bool IsUnknownSentinel(string? value) =>
        value is not null &&
        string.Equals(value.Trim(), MigratedUnknown, StringComparison.Ordinal);
}
