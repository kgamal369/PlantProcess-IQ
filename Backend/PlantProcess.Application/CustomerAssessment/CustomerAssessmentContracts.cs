// ============================================================================
// Customer data intake and capability assessment contracts.
//
// These types describe what was supplied about an unknown customer input
// structure and what the product concluded about it. They are onboarding
// evidence. They are not a semantic definition authority: nothing here is
// resolved at runtime, published, or promoted to a canonical dimension,
// measure, mapping or relationship.
//
// GENERICITY LAW
//   No customer, plant or industry vocabulary appears in this file. Every
//   customer-specific string arrives as opaque declared data. The declaration
//   codes below are product vocabulary - the questions the product asks of any
//   customer - never answers about a particular one.
// ============================================================================

using System;
using System.Collections.Generic;

namespace PlantProcess.Application.CustomerAssessment
{
    /// <summary>
    /// The complete status taxonomy. Five values, no others.
    /// </summary>
    public enum AssessmentStatus
    {
        /// <summary>Insufficient evidence to conclude. The default.</summary>
        Unknown = 0,

        /// <summary>The supplied intake establishes the answer.</summary>
        Known = 1,

        /// <summary>The supplied intake positively establishes absence.</summary>
        Missing = 2,

        /// <summary>An explicit rule states the area does not apply.</summary>
        NotApplicable = 3,

        /// <summary>A named missing authority or input prevents a conclusion.</summary>
        Blocked = 4
    }

    public static class AssessmentStatusCodes
    {
        public const string Known = "KNOWN";
        public const string Unknown = "UNKNOWN";
        public const string Missing = "MISSING";
        public const string NotApplicable = "NOT_APPLICABLE";
        public const string Blocked = "BLOCKED";

        public static string ToWire(AssessmentStatus status)
        {
            switch (status)
            {
                case AssessmentStatus.Known: return Known;
                case AssessmentStatus.Missing: return Missing;
                case AssessmentStatus.NotApplicable: return NotApplicable;
                case AssessmentStatus.Blocked: return Blocked;
                case AssessmentStatus.Unknown: return Unknown;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(status),
                        status,
                        "Unrecognised assessment status. The taxonomy is closed at five values.");
            }
        }
    }

    /// <summary>
    /// The twenty-six required assessment areas. The list is closed: an area is
    /// never merged away to shorten a report.
    /// </summary>
    public static class AssessmentSectionCodes
    {
        public const string SourceInventory = "SOURCE_INVENTORY";
        public const string EntityMap = "ENTITY_MAP";
        public const string IdentityStrategy = "IDENTITY_STRATEGY";
        public const string JoinKeyStrategy = "JOIN_KEY_STRATEGY";
        public const string TimeModel = "TIME_MODEL";
        public const string TimeAvailability = "TIME_AVAILABILITY";
        public const string ParameterCatalogue = "PARAMETER_CATALOGUE";
        public const string CandidateDimensions = "CANDIDATE_DIMENSIONS";
        public const string CandidateMeasures = "CANDIDATE_MEASURES";
        public const string ReferenceAvailability = "REFERENCE_AVAILABILITY";
        public const string AggregationGaps = "AGGREGATION_GAPS";
        public const string DataQuality = "DATA_QUALITY";
        public const string HistoricalCoverage = "HISTORICAL_COVERAGE";
        public const string ManualHistory = "MANUAL_HISTORY";
        public const string MachineHistory = "MACHINE_HISTORY";
        public const string SameWindowReconciliation = "SAME_WINDOW_RECONCILIATION";
        public const string TransitionDefinition = "TRANSITION_DEFINITION";
        public const string StabilisationRules = "STABILISATION_RULES";
        public const string SequenceBoundaries = "SEQUENCE_BOUNDARIES";
        public const string SetupEvidence = "SETUP_EVIDENCE";
        public const string ReconciliationEligibility = "RECONCILIATION_ELIGIBILITY";
        public const string MlEligibility = "ML_ELIGIBILITY";
        public const string OtTrialRequirements = "OT_TRIAL_REQUIREMENTS";
        public const string ObjectiveSets = "OBJECTIVE_SETS";
        public const string MultiObjectiveReadiness = "MULTI_OBJECTIVE_READINESS";
        public const string MissingInformation = "MISSING_INFORMATION";

        /// <summary>Report order. Twenty-six entries, no duplicates.</summary>
        public static readonly IReadOnlyList<string> Ordered = new[]
        {
            SourceInventory,
            EntityMap,
            IdentityStrategy,
            JoinKeyStrategy,
            TimeModel,
            TimeAvailability,
            ParameterCatalogue,
            CandidateDimensions,
            CandidateMeasures,
            ReferenceAvailability,
            AggregationGaps,
            DataQuality,
            HistoricalCoverage,
            ManualHistory,
            MachineHistory,
            SameWindowReconciliation,
            TransitionDefinition,
            StabilisationRules,
            SequenceBoundaries,
            SetupEvidence,
            ReconciliationEligibility,
            MlEligibility,
            OtTrialRequirements,
            ObjectiveSets,
            MultiObjectiveReadiness,
            MissingInformation
        };
    }

    /// <summary>
    /// Declaration codes are the questions the product asks of every customer.
    /// The answers are opaque customer data and never become product constants.
    /// </summary>
    public static class AssessmentDeclarationCodes
    {
        public const string IdentityStrategy = "identity.strategy";
        public const string JoinStrategy = "join.strategy";
        public const string TimeModel = "time.model";
        public const string ReferenceSpecificationAvailable = "reference.specification.available";
        public const string AggregationSemantics = "aggregation.semantics";
        public const string DataQualityProgramme = "data.quality.programme";
        public const string ManualHistoryAvailable = "history.manual.available";
        public const string MachineHistoryAvailable = "history.machine.available";
        public const string TransitionDefinition = "transition.definition";
        public const string StabilisationRule = "stabilisation.rule";
        public const string SequenceBoundary = "sequence.boundary";
        public const string SetupEvidence = "setup.evidence";
        public const string OtTrialRequirement = "ot.trial.requirement";
        public const string ObjectiveSet = "objective.set";
        public const string ObjectiveTradeOff = "objective.tradeoff";

        public static readonly IReadOnlyList<string> All = new[]
        {
            IdentityStrategy,
            JoinStrategy,
            TimeModel,
            ReferenceSpecificationAvailable,
            AggregationSemantics,
            DataQualityProgramme,
            ManualHistoryAvailable,
            MachineHistoryAvailable,
            TransitionDefinition,
            StabilisationRule,
            SequenceBoundary,
            SetupEvidence,
            OtTrialRequirement,
            ObjectiveSet,
            ObjectiveTradeOff
        };
    }

    /// <summary>
    /// Reserved declaration answers. A declaration whose value normalises to
    /// one of these carries a status meaning rather than customer content.
    /// Absence of a declaration is never one of these: it is Unknown.
    /// </summary>
    public static class AssessmentDeclarationAnswers
    {
        public const string None = "none";
        public const string Absent = "absent";
        public const string NotAvailable = "not_available";
        public const string NotApplicable = "not_applicable";

        public static bool EstablishesAbsence(string? normalisedValue)
        {
            return normalisedValue == None
                || normalisedValue == Absent
                || normalisedValue == NotAvailable;
        }

        public static bool EstablishesNotApplicable(string? normalisedValue)
        {
            return normalisedValue == NotApplicable;
        }
    }

    /// <summary>
    /// Declared field role hints. Opaque to the product beyond these four
    /// generic buckets; an unrecognised role is carried, never rejected.
    /// </summary>
    public static class IntakeFieldRoles
    {
        public const string Identity = "identity";
        public const string Time = "time";
        public const string Measure = "measure";
        public const string Attribute = "attribute";
    }

    public static class IntakeTimeSemantics
    {
        public const string Source = "source";
        public const string Server = "server";
        public const string Ingest = "ingest";
    }

    // ------------------------------------------------------------------
    // Intake
    // ------------------------------------------------------------------

    public sealed class CustomerIntakeField
    {
        public string FieldCode { get; init; } = string.Empty;
        public string? DeclaredType { get; init; }
        public string? Role { get; init; }
        public string? UnitCode { get; init; }
        public string? TimeSemantics { get; init; }
        public string? AggregationSemantics { get; init; }
        public bool? IsNullableDeclared { get; init; }
        public double? NullFraction { get; init; }
        public long? DistinctCount { get; init; }
    }

    public sealed class CustomerIntakeTable
    {
        public string TableCode { get; init; } = string.Empty;
        public long? DeclaredRowCount { get; init; }
        public IReadOnlyList<CustomerIntakeField> Fields { get; init; }
            = Array.Empty<CustomerIntakeField>();
    }

    public sealed class CustomerIntakeSource
    {
        public string SourceCode { get; init; } = string.Empty;

        /// <summary>Opaque declared source kind. Never interpreted as a product enum.</summary>
        public string? SourceKind { get; init; }

        /// <summary>True when the customer states this source is a human record.</summary>
        public bool? IsManualRecord { get; init; }

        public DateTimeOffset? EarliestObservationUtc { get; init; }
        public DateTimeOffset? LatestObservationUtc { get; init; }

        public IReadOnlyList<CustomerIntakeTable> Tables { get; init; }
            = Array.Empty<CustomerIntakeTable>();
    }

    public sealed class CustomerIntakeEntity
    {
        public string EntityCode { get; init; } = string.Empty;
        public string? ParentEntityCode { get; init; }

        /// <summary>Qualified reference of the form source.table.field.</summary>
        public string? IdentityFieldRef { get; init; }
    }

    public sealed class CustomerIntakeDeclaration
    {
        public string DeclarationCode { get; init; } = string.Empty;

        /// <summary>
        /// Null means the question was presented and not answered: Unknown.
        /// A reserved answer establishes absence or non-applicability.
        /// Anything else is opaque customer content.
        /// </summary>
        public string? Value { get; init; }
    }

    public sealed class CustomerIntake
    {
        /// <summary>Stable tenant-scoped identity of the structure under assessment.</summary>
        public string LineageCode { get; init; } = string.Empty;

        public string? DisplayName { get; init; }

        public IReadOnlyList<CustomerIntakeSource> Sources { get; init; }
            = Array.Empty<CustomerIntakeSource>();

        public IReadOnlyList<CustomerIntakeEntity> Entities { get; init; }
            = Array.Empty<CustomerIntakeEntity>();

        public IReadOnlyList<CustomerIntakeDeclaration> Declarations { get; init; }
            = Array.Empty<CustomerIntakeDeclaration>();
    }

    // ------------------------------------------------------------------
    // Report
    // ------------------------------------------------------------------

    public sealed class AssessmentEvidence
    {
        public string EvidenceCode { get; init; } = string.Empty;
        public string Statement { get; init; } = string.Empty;

        /// <summary>Qualified intake reference this evidence was read from.</summary>
        public string? IntakeRef { get; init; }
    }

    /// <summary>
    /// A candidate is a possibility carried by the report. Producing one causes
    /// zero canonical promotion. Acceptance happens in a separate governed
    /// workflow that this assessment never invokes.
    /// </summary>
    public sealed class AssessmentCandidate
    {
        public string CandidateKind { get; init; } = string.Empty;
        public string CandidateCode { get; init; } = string.Empty;
        public string? IntakeRef { get; init; }
        public string? Rationale { get; init; }
    }

    public sealed class AssessmentBlocker
    {
        public string BlockerCode { get; init; } = string.Empty;

        /// <summary>The named missing authority or input.</summary>
        public string RequiredInput { get; init; } = string.Empty;

        public string Statement { get; init; } = string.Empty;
    }

    public sealed class AssessmentSection
    {
        public string SectionCode { get; init; } = string.Empty;
        public AssessmentStatus Status { get; init; } = AssessmentStatus.Unknown;
        public string StatusCode => AssessmentStatusCodes.ToWire(Status);
        public string Statement { get; init; } = string.Empty;

        public IReadOnlyList<AssessmentEvidence> Evidence { get; init; }
            = Array.Empty<AssessmentEvidence>();

        public IReadOnlyList<AssessmentCandidate> Candidates { get; init; }
            = Array.Empty<AssessmentCandidate>();

        public IReadOnlyList<AssessmentBlocker> Blockers { get; init; }
            = Array.Empty<AssessmentBlocker>();
    }

    public sealed class CustomerAssessmentReport
    {
        public string LineageCode { get; init; } = string.Empty;
        public string ContractVersion { get; init; } = string.Empty;
        public string RuleVersion { get; init; } = string.Empty;

        /// <summary>Twenty-six sections in AssessmentSectionCodes.Ordered order.</summary>
        public IReadOnlyList<AssessmentSection> Sections { get; init; }
            = Array.Empty<AssessmentSection>();
    }

    /// <summary>
    /// The two semantic versions are kept explicit and are both hashed. A rule
    /// change can alter conclusions even when the customer structure did not.
    /// </summary>
    public static class CustomerAssessmentSemanticVersion
    {
        public const string ContractVersion = "1.0.0";
        public const string RuleVersion = "1.0.0";
    }

    /// <summary>
    /// The two semantic versions are read through this provider rather than
    /// from the constants directly, so that a rule-version change can be
    /// exercised as behaviour instead of asserted as a comment.
    /// </summary>
    public interface ICustomerAssessmentSemanticVersionProvider
    {
        string ContractVersion { get; }
        string RuleVersion { get; }
    }

    public sealed class FrozenSemanticVersionProvider : ICustomerAssessmentSemanticVersionProvider
    {
        public string ContractVersion
        {
            get { return CustomerAssessmentSemanticVersion.ContractVersion; }
        }

        public string RuleVersion
        {
            get { return CustomerAssessmentSemanticVersion.RuleVersion; }
        }
    }
}
