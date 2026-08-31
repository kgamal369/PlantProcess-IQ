// ============================================================================
// The customer capability assessment engine.
//
// One pure function: intake in, twenty-six-section report out. No clock, no
// randomness, no identifiers, no database. The same intake and the same two
// semantic versions always produce the same report.
//
// STATUS LAW
//   UNKNOWN is the default and means insufficient evidence to conclude.
//   MISSING is stronger and is reached only when the intake positively
//   establishes absence through a reserved declaration answer.
//   NOT_APPLICABLE is reached only through an explicit named rule.
//   BLOCKED names the input or prior conclusion that prevents a conclusion.
//   Absence of a field is never MISSING.
//
// CANDIDATE LAW
//   Candidate dimensions, measures, identities and joins are carried inside
//   the report. Producing them causes zero canonical promotion. This file
//   references no definition, dimension, measure, mapping or publication
//   service, and calls nothing that could write one.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PlantProcess.Application.CustomerAssessment
{
    public interface ICustomerAssessmentEngine
    {
        CustomerAssessmentReport Evaluate(CustomerIntake intake);
    }

    public static class AssessmentRuleCodes
    {
        /// <summary>
        /// A single declared source cannot participate in a cross-source join.
        /// This is the explicit rule that permits NOT_APPLICABLE on the join
        /// strategy section; it is never inferred from missing evidence.
        /// </summary>
        public const string SingleSourceNoCrossSourceJoin = "RULE.JOIN.SINGLE_SOURCE";
    }

    public sealed class CustomerAssessmentEngine : ICustomerAssessmentEngine
    {
        private sealed class FieldRef
        {
            public string SourceCode = string.Empty;
            public string TableCode = string.Empty;
            public CustomerIntakeField Field = new CustomerIntakeField();
            public string Ref = string.Empty;
        }

        private enum DeclarationOutcomeKind
        {
            NotPresent,
            Unanswered,
            Conflicting,
            Absence,
            NotApplicable,
            Answered
        }

        private sealed class DeclarationOutcome
        {
            public DeclarationOutcomeKind Kind = DeclarationOutcomeKind.NotPresent;
            public string? Answer;

            public AssessmentStatus Status
            {
                get
                {
                    switch (Kind)
                    {
                        case DeclarationOutcomeKind.Answered: return AssessmentStatus.Known;
                        case DeclarationOutcomeKind.Absence: return AssessmentStatus.Missing;
                        case DeclarationOutcomeKind.NotApplicable: return AssessmentStatus.NotApplicable;
                        case DeclarationOutcomeKind.Conflicting: return AssessmentStatus.Blocked;
                        default: return AssessmentStatus.Unknown;
                    }
                }
            }
        }

        private sealed class SectionAccumulator
        {
            private readonly List<AssessmentSection> _sections = new List<AssessmentSection>();
            private readonly Dictionary<string, AssessmentSection> _byCode =
                new Dictionary<string, AssessmentSection>(StringComparer.Ordinal);

            public IReadOnlyList<AssessmentSection> Sections { get { return _sections; } }

            public IReadOnlyDictionary<string, AssessmentSection> ByCode { get { return _byCode; } }

            public void Add(AssessmentSection section)
            {
                _sections.Add(section);
                _byCode[section.SectionCode] = section;
            }

            public AssessmentStatus StatusOf(string sectionCode)
            {
                AssessmentSection found;
                return _byCode.TryGetValue(sectionCode, out found)
                    ? found.Status
                    : AssessmentStatus.Unknown;
            }
        }

        public CustomerAssessmentReport Evaluate(CustomerIntake intake)
        {
            if (intake == null)
            {
                throw new ArgumentNullException(nameof(intake));
            }

            IReadOnlyList<CustomerIntakeSource> sources =
                (intake.Sources ?? Array.Empty<CustomerIntakeSource>())
                .OrderBy(s => CustomerAssessmentNormalization.TrimOnly(s.SourceCode), StringComparer.Ordinal)
                .ToList();

            List<FieldRef> fields = BuildFieldIndex(sources);
            Dictionary<string, DeclarationOutcome> declarations = BuildDeclarationIndex(intake);

            var acc = new SectionAccumulator();

            acc.Add(EvaluateSourceInventory(sources, fields));
            acc.Add(EvaluateEntityMap(intake));
            acc.Add(EvaluateIdentityStrategy(declarations, fields));
            acc.Add(EvaluateJoinKeyStrategy(declarations, sources, fields, acc.StatusOf(AssessmentSectionCodes.IdentityStrategy)));
            acc.Add(EvaluateTimeModel(declarations, fields));
            acc.Add(EvaluateTimeAvailability(fields, acc.StatusOf(AssessmentSectionCodes.TimeModel)));
            acc.Add(EvaluateParameterCatalogue(fields));
            acc.Add(EvaluateCandidateDimensions(fields));
            acc.Add(EvaluateCandidateMeasures(fields));
            acc.Add(EvaluateDeclarationOnly(
                AssessmentSectionCodes.ReferenceAvailability,
                AssessmentDeclarationCodes.ReferenceSpecificationAvailable,
                declarations,
                "Reference or specification availability for the declared parameters."));
            acc.Add(EvaluateAggregationGaps(declarations, fields, acc.StatusOf(AssessmentSectionCodes.CandidateMeasures)));
            acc.Add(EvaluateDataQuality(declarations, fields));
            acc.Add(EvaluateHistoricalCoverage(sources));
            acc.Add(EvaluateManualHistory(declarations, sources));
            acc.Add(EvaluateMachineHistory(declarations, sources));
            acc.Add(EvaluateSameWindowReconciliation(
                sources,
                acc.StatusOf(AssessmentSectionCodes.ManualHistory),
                acc.StatusOf(AssessmentSectionCodes.MachineHistory),
                acc.StatusOf(AssessmentSectionCodes.HistoricalCoverage)));
            acc.Add(EvaluateDeclarationOnly(
                AssessmentSectionCodes.TransitionDefinition,
                AssessmentDeclarationCodes.TransitionDefinition,
                declarations,
                "Declared definition of a transition or changeover between operating regimes."));
            acc.Add(EvaluateStabilisationRules(declarations, acc.StatusOf(AssessmentSectionCodes.TransitionDefinition)));
            acc.Add(EvaluateDeclarationOnly(
                AssessmentSectionCodes.SequenceBoundaries,
                AssessmentDeclarationCodes.SequenceBoundary,
                declarations,
                "Declared sequence or campaign boundary rule."));
            acc.Add(EvaluateDeclarationOnly(
                AssessmentSectionCodes.SetupEvidence,
                AssessmentDeclarationCodes.SetupEvidence,
                declarations,
                "Declared setup or preparation evidence for a production sequence."));
            acc.Add(EvaluateDependentReadiness(
                AssessmentSectionCodes.ReconciliationEligibility,
                "Eligibility to reconcile a manual record against a machine record over the same calendar window.",
                new[]
                {
                    AssessmentSectionCodes.SameWindowReconciliation,
                    AssessmentSectionCodes.IdentityStrategy
                },
                acc.ByCode));
            acc.Add(EvaluateDependentReadiness(
                AssessmentSectionCodes.MlEligibility,
                "Eligibility to fit and evaluate a learned model on the declared structure.",
                new[]
                {
                    AssessmentSectionCodes.ParameterCatalogue,
                    AssessmentSectionCodes.CandidateMeasures,
                    AssessmentSectionCodes.HistoricalCoverage
                },
                acc.ByCode));
            acc.Add(EvaluateDeclarationOnly(
                AssessmentSectionCodes.OtTrialRequirements,
                AssessmentDeclarationCodes.OtTrialRequirement,
                declarations,
                "Declared operational-technology requirements for an on-plant trial."));
            acc.Add(EvaluateDeclarationOnly(
                AssessmentSectionCodes.ObjectiveSets,
                AssessmentDeclarationCodes.ObjectiveSet,
                declarations,
                "Declared customer objective set."));
            acc.Add(EvaluateMultiObjectiveReadiness(
                declarations,
                acc.StatusOf(AssessmentSectionCodes.ObjectiveSets),
                acc.StatusOf(AssessmentSectionCodes.CandidateMeasures)));

            acc.Add(EvaluateMissingInformation(acc.Sections));

            AssertSectionCompleteness(acc.Sections);

            return new CustomerAssessmentReport
            {
                LineageCode = CustomerAssessmentNormalization.TrimOnly(intake.LineageCode),
                ContractVersion = CustomerAssessmentSemanticVersion.ContractVersion,
                RuleVersion = CustomerAssessmentSemanticVersion.RuleVersion,
                Sections = acc.Sections
            };
        }

        // ------------------------------------------------------------------
        // Indexing
        // ------------------------------------------------------------------

        private static List<FieldRef> BuildFieldIndex(IReadOnlyList<CustomerIntakeSource> sources)
        {
            var result = new List<FieldRef>();

            foreach (CustomerIntakeSource source in sources)
            {
                string sourceCode = CustomerAssessmentNormalization.TrimOnly(source.SourceCode);

                IEnumerable<CustomerIntakeTable> tables =
                    (source.Tables ?? Array.Empty<CustomerIntakeTable>())
                    .OrderBy(t => CustomerAssessmentNormalization.TrimOnly(t.TableCode), StringComparer.Ordinal);

                foreach (CustomerIntakeTable table in tables)
                {
                    string tableCode = CustomerAssessmentNormalization.TrimOnly(table.TableCode);

                    IEnumerable<CustomerIntakeField> tableFields =
                        (table.Fields ?? Array.Empty<CustomerIntakeField>())
                        .OrderBy(f => CustomerAssessmentNormalization.TrimOnly(f.FieldCode), StringComparer.Ordinal);

                    foreach (CustomerIntakeField field in tableFields)
                    {
                        string fieldCode = CustomerAssessmentNormalization.TrimOnly(field.FieldCode);
                        result.Add(new FieldRef
                        {
                            SourceCode = sourceCode,
                            TableCode = tableCode,
                            Field = field,
                            Ref = sourceCode + "." + tableCode + "." + fieldCode
                        });
                    }
                }
            }

            return result;
        }

        private static Dictionary<string, DeclarationOutcome> BuildDeclarationIndex(CustomerIntake intake)
        {
            var result = new Dictionary<string, DeclarationOutcome>(StringComparer.Ordinal);

            IEnumerable<IGrouping<string, CustomerIntakeDeclaration>> groups =
                (intake.Declarations ?? Array.Empty<CustomerIntakeDeclaration>())
                .GroupBy(d => CustomerAssessmentNormalization.TrimOnly(d.DeclarationCode), StringComparer.Ordinal);

            foreach (IGrouping<string, CustomerIntakeDeclaration> group in groups)
            {
                List<string?> answers = group
                    .Select(d => CustomerAssessmentNormalization.NormaliseDeclarationAnswer(d.Value))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var outcome = new DeclarationOutcome();

                if (answers.Count > 1)
                {
                    outcome.Kind = DeclarationOutcomeKind.Conflicting;
                }
                else
                {
                    string? answer = answers.Count == 1 ? answers[0] : null;
                    outcome.Answer = answer;

                    if (answer == null)
                    {
                        outcome.Kind = DeclarationOutcomeKind.Unanswered;
                    }
                    else if (AssessmentDeclarationAnswers.EstablishesAbsence(answer))
                    {
                        outcome.Kind = DeclarationOutcomeKind.Absence;
                    }
                    else if (AssessmentDeclarationAnswers.EstablishesNotApplicable(answer))
                    {
                        outcome.Kind = DeclarationOutcomeKind.NotApplicable;
                    }
                    else
                    {
                        outcome.Kind = DeclarationOutcomeKind.Answered;
                    }
                }

                result[group.Key] = outcome;
            }

            return result;
        }

        private static DeclarationOutcome Declaration(
            Dictionary<string, DeclarationOutcome> declarations,
            string code)
        {
            return declarations.TryGetValue(code, out DeclarationOutcome? found)
                ? found
                : new DeclarationOutcome { Kind = DeclarationOutcomeKind.NotPresent };
        }

        private static bool RoleIs(CustomerIntakeField field, string role)
        {
            return string.Equals(
                CustomerAssessmentNormalization.TrimOnly(field.Role),
                role,
                StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------
        // Sections
        // ------------------------------------------------------------------

        private static AssessmentSection EvaluateSourceInventory(
            IReadOnlyList<CustomerIntakeSource> sources,
            IReadOnlyList<FieldRef> fields)
        {
            var evidence = new List<AssessmentEvidence>();

            foreach (CustomerIntakeSource source in sources)
            {
                string code = CustomerAssessmentNormalization.TrimOnly(source.SourceCode);
                int tableCount = (source.Tables ?? Array.Empty<CustomerIntakeTable>()).Count;
                int fieldCount = fields.Count(f => string.Equals(f.SourceCode, code, StringComparison.Ordinal));

                evidence.Add(new AssessmentEvidence
                {
                    EvidenceCode = "SOURCE_DECLARED",
                    IntakeRef = code,
                    Statement = string.Format(
                        CultureInfo.InvariantCulture,
                        "Source {0} declares {1} table(s) and {2} field(s).",
                        code, tableCount, fieldCount)
                });
            }

            if (sources.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.SourceInventory,
                    AssessmentStatus.Unknown,
                    "No source inventory was supplied. This is an absence of evidence, not evidence of absence.");
            }

            if (fields.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.SourceInventory,
                    AssessmentStatus.Unknown,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} source(s) were named but no field inventory was supplied for any of them.",
                        sources.Count),
                    evidence);
            }

            return Section(
                AssessmentSectionCodes.SourceInventory,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} source(s) and {1} field(s) are declared.",
                    sources.Count, fields.Count),
                evidence);
        }

        private static AssessmentSection EvaluateEntityMap(CustomerIntake intake)
        {
            IReadOnlyList<CustomerIntakeEntity> entities =
                (intake.Entities ?? Array.Empty<CustomerIntakeEntity>())
                .OrderBy(e => CustomerAssessmentNormalization.TrimOnly(e.EntityCode), StringComparer.Ordinal)
                .ToList();

            if (entities.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.EntityMap,
                    AssessmentStatus.Unknown,
                    "No entity or equipment structure was declared.");
            }

            var known = new HashSet<string>(
                entities.Select(e => CustomerAssessmentNormalization.TrimOnly(e.EntityCode)),
                StringComparer.Ordinal);

            var evidence = new List<AssessmentEvidence>();
            var blockers = new List<AssessmentBlocker>();

            foreach (CustomerIntakeEntity entity in entities)
            {
                string code = CustomerAssessmentNormalization.TrimOnly(entity.EntityCode);
                string parent = CustomerAssessmentNormalization.TrimOnly(entity.ParentEntityCode);

                evidence.Add(new AssessmentEvidence
                {
                    EvidenceCode = "ENTITY_DECLARED",
                    IntakeRef = code,
                    Statement = parent.Length == 0
                        ? string.Format(CultureInfo.InvariantCulture, "Entity {0} is declared at the root.", code)
                        : string.Format(CultureInfo.InvariantCulture, "Entity {0} declares parent {1}.", code, parent)
                });

                if (parent.Length > 0 && !known.Contains(parent))
                {
                    blockers.Add(new AssessmentBlocker
                    {
                        BlockerCode = "ENTITY_PARENT_UNRESOLVED",
                        RequiredInput = parent,
                        Statement = string.Format(
                            CultureInfo.InvariantCulture,
                            "Entity {0} declares parent {1}, which was not itself declared.",
                            code, parent)
                    });
                }
            }

            if (blockers.Count > 0)
            {
                return Section(
                    AssessmentSectionCodes.EntityMap,
                    AssessmentStatus.Blocked,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} declared entity parent reference(s) do not resolve inside the supplied structure.",
                        blockers.Count),
                    evidence,
                    Array.Empty<AssessmentCandidate>(),
                    blockers);
            }

            return Section(
                AssessmentSectionCodes.EntityMap,
                AssessmentStatus.Known,
                string.Format(CultureInfo.InvariantCulture, "{0} entity node(s) are declared and resolve.", entities.Count),
                evidence);
        }

        private static AssessmentSection EvaluateIdentityStrategy(
            Dictionary<string, DeclarationOutcome> declarations,
            IReadOnlyList<FieldRef> fields)
        {
            DeclarationOutcome declared = Declaration(declarations, AssessmentDeclarationCodes.IdentityStrategy);

            List<FieldRef> identityFields = fields
                .Where(f => RoleIs(f.Field, IntakeFieldRoles.Identity))
                .ToList();

            var candidates = identityFields
                .Select(f => new AssessmentCandidate
                {
                    CandidateKind = "identity",
                    CandidateCode = f.Ref,
                    IntakeRef = f.Ref,
                    Rationale = "Declared with the identity role. A candidate only; nothing is promoted."
                })
                .ToList();

            var evidence = new List<AssessmentEvidence>();
            if (declared.Kind == DeclarationOutcomeKind.Answered)
            {
                evidence.Add(DeclarationEvidence(AssessmentDeclarationCodes.IdentityStrategy, declared.Answer));
            }

            foreach (FieldRef field in identityFields)
            {
                evidence.Add(new AssessmentEvidence
                {
                    EvidenceCode = "IDENTITY_FIELD_DECLARED",
                    IntakeRef = field.Ref,
                    Statement = string.Format(CultureInfo.InvariantCulture, "{0} carries the identity role.", field.Ref)
                });
            }

            AssessmentStatus status = declared.Status;
            string statement;

            if (status == AssessmentStatus.Known)
            {
                statement = "An identity strategy is declared.";
            }
            else if (status == AssessmentStatus.Unknown && identityFields.Count > 0)
            {
                statement = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} identity-role field(s) are present but no identity strategy was declared, so the strategy cannot be concluded.",
                    identityFields.Count);
            }
            else if (status == AssessmentStatus.Missing)
            {
                statement = "The intake states that no identity strategy exists.";
            }
            else if (status == AssessmentStatus.NotApplicable)
            {
                statement = "An explicit declaration states that an identity strategy does not apply.";
            }
            else if (status == AssessmentStatus.Blocked)
            {
                statement = "Conflicting identity strategy declarations were supplied.";
            }
            else
            {
                statement = "No identity strategy was declared and no identity-role field is present.";
            }

            List<AssessmentBlocker> blockers = status == AssessmentStatus.Blocked
                ? new List<AssessmentBlocker>
                {
                    new AssessmentBlocker
                    {
                        BlockerCode = "DECLARATION_CONFLICT",
                        RequiredInput = AssessmentDeclarationCodes.IdentityStrategy,
                        Statement = "Two or more different answers were supplied for the same declaration."
                    }
                }
                : new List<AssessmentBlocker>();

            return Section(
                AssessmentSectionCodes.IdentityStrategy,
                status,
                statement,
                evidence,
                candidates,
                blockers);
        }

        private static AssessmentSection EvaluateJoinKeyStrategy(
            Dictionary<string, DeclarationOutcome> declarations,
            IReadOnlyList<CustomerIntakeSource> sources,
            IReadOnlyList<FieldRef> fields,
            AssessmentStatus identityStatus)
        {
            if (sources.Count <= 1)
            {
                return Section(
                    AssessmentSectionCodes.JoinKeyStrategy,
                    AssessmentStatus.NotApplicable,
                    "A single declared source cannot participate in a cross-source join.",
                    new List<AssessmentEvidence>
                    {
                        new AssessmentEvidence
                        {
                            EvidenceCode = "EXPLICIT_RULE",
                            IntakeRef = AssessmentRuleCodes.SingleSourceNoCrossSourceJoin,
                            Statement = "Not applicable is reached through this named rule, not through absent evidence."
                        }
                    });
            }

            List<string> shared = fields
                .GroupBy(f => CustomerAssessmentNormalization.TrimOnly(f.Field.FieldCode), StringComparer.Ordinal)
                .Where(g => g.Select(x => x.SourceCode).Distinct(StringComparer.Ordinal).Count() > 1)
                .Select(g => g.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            var candidates = shared
                .Select(code => new AssessmentCandidate
                {
                    CandidateKind = "join",
                    CandidateCode = code,
                    Rationale = "The same field code appears in more than one source. A candidate only."
                })
                .ToList();

            DeclarationOutcome declared = Declaration(declarations, AssessmentDeclarationCodes.JoinStrategy);

            if (declared.Status == AssessmentStatus.Known)
            {
                return Section(
                    AssessmentSectionCodes.JoinKeyStrategy,
                    AssessmentStatus.Known,
                    "A join strategy is declared across the supplied sources.",
                    new List<AssessmentEvidence> { DeclarationEvidence(AssessmentDeclarationCodes.JoinStrategy, declared.Answer) },
                    candidates);
            }

            if (declared.Status == AssessmentStatus.Missing || declared.Status == AssessmentStatus.NotApplicable)
            {
                return Section(
                    AssessmentSectionCodes.JoinKeyStrategy,
                    declared.Status,
                    "The intake answers the join strategy question with a reserved answer.",
                    new List<AssessmentEvidence> { DeclarationEvidence(AssessmentDeclarationCodes.JoinStrategy, declared.Answer) },
                    candidates);
            }

            if (identityStatus != AssessmentStatus.Known)
            {
                return Section(
                    AssessmentSectionCodes.JoinKeyStrategy,
                    AssessmentStatus.Blocked,
                    "A cross-source join strategy cannot be concluded while the identity strategy is unresolved.",
                    Array.Empty<AssessmentEvidence>(),
                    candidates,
                    new List<AssessmentBlocker>
                    {
                        new AssessmentBlocker
                        {
                            BlockerCode = "SECTION_PREREQUISITE",
                            RequiredInput = AssessmentSectionCodes.IdentityStrategy,
                            Statement = "Resolve the identity strategy before a join strategy can be concluded."
                        }
                    });
            }

            return Section(
                AssessmentSectionCodes.JoinKeyStrategy,
                AssessmentStatus.Unknown,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "No join strategy was declared. {0} shared field code(s) are carried as candidates.",
                    shared.Count),
                Array.Empty<AssessmentEvidence>(),
                candidates);
        }

        private static AssessmentSection EvaluateTimeModel(
            Dictionary<string, DeclarationOutcome> declarations,
            IReadOnlyList<FieldRef> fields)
        {
            DeclarationOutcome declared = Declaration(declarations, AssessmentDeclarationCodes.TimeModel);

            List<FieldRef> timeFields = fields
                .Where(f => RoleIs(f.Field, IntakeFieldRoles.Time))
                .ToList();

            var evidence = new List<AssessmentEvidence>();
            if (declared.Kind == DeclarationOutcomeKind.Answered)
            {
                evidence.Add(DeclarationEvidence(AssessmentDeclarationCodes.TimeModel, declared.Answer));
            }

            foreach (FieldRef field in timeFields)
            {
                evidence.Add(new AssessmentEvidence
                {
                    EvidenceCode = "TIME_FIELD_DECLARED",
                    IntakeRef = field.Ref,
                    Statement = string.Format(CultureInfo.InvariantCulture, "{0} carries the time role.", field.Ref)
                });
            }

            var candidates = timeFields
                .Select(f => new AssessmentCandidate
                {
                    CandidateKind = "time",
                    CandidateCode = f.Ref,
                    IntakeRef = f.Ref,
                    Rationale = "Declared with the time role. A candidate only."
                })
                .ToList();

            AssessmentStatus status = declared.Status;
            string statement = status == AssessmentStatus.Known
                ? "A time model is declared."
                : status == AssessmentStatus.Missing
                    ? "The intake states that no time model exists."
                    : status == AssessmentStatus.NotApplicable
                        ? "An explicit declaration states that a time model does not apply."
                        : status == AssessmentStatus.Blocked
                            ? "Conflicting time model declarations were supplied."
                            : string.Format(
                                CultureInfo.InvariantCulture,
                                "No time model was declared. {0} time-role field(s) are carried as candidates.",
                                timeFields.Count);

            return Section(AssessmentSectionCodes.TimeModel, status, statement, evidence, candidates);
        }

        private static AssessmentSection EvaluateTimeAvailability(
            IReadOnlyList<FieldRef> fields,
            AssessmentStatus timeModelStatus)
        {
            List<FieldRef> timeFields = fields
                .Where(f => RoleIs(f.Field, IntakeFieldRoles.Time))
                .ToList();

            if (timeFields.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.TimeAvailability,
                    AssessmentStatus.Blocked,
                    "Source, server and ingest time availability cannot be assessed without a declared time field.",
                    Array.Empty<AssessmentEvidence>(),
                    Array.Empty<AssessmentCandidate>(),
                    new List<AssessmentBlocker>
                    {
                        new AssessmentBlocker
                        {
                            BlockerCode = "SECTION_PREREQUISITE",
                            RequiredInput = AssessmentSectionCodes.TimeModel,
                            Statement = "At least one field carrying the time role is required."
                        }
                    });
            }

            var declaredSemantics = new HashSet<string>(StringComparer.Ordinal);
            var evidence = new List<AssessmentEvidence>();

            foreach (FieldRef field in timeFields)
            {
                string semantics = CustomerAssessmentNormalization.TrimOnly(field.Field.TimeSemantics);
                if (semantics.Length == 0)
                {
                    continue;
                }

                declaredSemantics.Add(semantics);
                evidence.Add(new AssessmentEvidence
                {
                    EvidenceCode = "TIME_SEMANTICS_DECLARED",
                    IntakeRef = field.Ref,
                    Statement = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} declares {1} time semantics.",
                        field.Ref, semantics)
                });
            }

            string[] expected =
            {
                IntakeTimeSemantics.Source,
                IntakeTimeSemantics.Server,
                IntakeTimeSemantics.Ingest
            };

            var blockers = new List<AssessmentBlocker>();
            foreach (string semantics in expected)
            {
                if (!declaredSemantics.Contains(semantics))
                {
                    blockers.Add(new AssessmentBlocker
                    {
                        BlockerCode = "TIME_SEMANTICS_UNDECLARED",
                        RequiredInput = semantics,
                        Statement = string.Format(
                            CultureInfo.InvariantCulture,
                            "No declared time field carries {0} time semantics.",
                            semantics)
                    });
                }
            }

            if (declaredSemantics.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.TimeAvailability,
                    AssessmentStatus.Unknown,
                    "Time fields are declared but none states whether it carries source, server or ingest time.",
                    evidence,
                    Array.Empty<AssessmentCandidate>(),
                    blockers);
            }

            return Section(
                AssessmentSectionCodes.TimeAvailability,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} of 3 time semantics are declared across {1} time field(s).",
                    declaredSemantics.Count, timeFields.Count),
                evidence,
                Array.Empty<AssessmentCandidate>(),
                blockers);
        }

        private static AssessmentSection EvaluateParameterCatalogue(IReadOnlyList<FieldRef> fields)
        {
            List<FieldRef> catalogued = fields
                .Where(f => RoleIs(f.Field, IntakeFieldRoles.Measure) || RoleIs(f.Field, IntakeFieldRoles.Attribute))
                .ToList();

            if (fields.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.ParameterCatalogue,
                    AssessmentStatus.Unknown,
                    "No field inventory was supplied, so no parameter catalogue can be derived.");
            }

            if (catalogued.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.ParameterCatalogue,
                    AssessmentStatus.Unknown,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} field(s) are declared but none carries a measure or attribute role.",
                        fields.Count));
            }

            var evidence = catalogued
                .Select(f => new AssessmentEvidence
                {
                    EvidenceCode = "PARAMETER_DECLARED",
                    IntakeRef = f.Ref,
                    Statement = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is catalogued with role {1} and unit {2}.",
                        f.Ref,
                        CustomerAssessmentNormalization.TrimOnly(f.Field.Role),
                        CustomerAssessmentNormalization.TrimOnly(f.Field.UnitCode).Length == 0
                            ? "undeclared"
                            : CustomerAssessmentNormalization.TrimOnly(f.Field.UnitCode))
                })
                .ToList();

            return Section(
                AssessmentSectionCodes.ParameterCatalogue,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} parameter(s) are catalogued from {1} declared field(s).",
                    catalogued.Count, fields.Count),
                evidence);
        }

        private static AssessmentSection EvaluateCandidateDimensions(IReadOnlyList<FieldRef> fields)
        {
            List<FieldRef> attributes = fields
                .Where(f => RoleIs(f.Field, IntakeFieldRoles.Attribute))
                .ToList();

            if (attributes.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.CandidateDimensions,
                    AssessmentStatus.Unknown,
                    "No attribute-role field is declared, so no dimension candidate can be derived.");
            }

            var candidates = new List<AssessmentCandidate>();
            var evidence = new List<AssessmentEvidence>();

            foreach (FieldRef field in attributes)
            {
                long? distinct = field.Field.DistinctCount;
                string rationale = distinct.HasValue
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "Attribute role with {0} declared distinct value(s).",
                        distinct.Value)
                    : "Attribute role with an undeclared distinct count.";

                candidates.Add(new AssessmentCandidate
                {
                    CandidateKind = "dimension",
                    CandidateCode = field.Ref,
                    IntakeRef = field.Ref,
                    Rationale = rationale + " A candidate only; no canonical dimension is registered."
                });

                if (distinct.HasValue && distinct.Value <= 1)
                {
                    evidence.Add(new AssessmentEvidence
                    {
                        EvidenceCode = "DIMENSION_NON_DISCRIMINATING",
                        IntakeRef = field.Ref,
                        Statement = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} declares {1} distinct value(s) and would not discriminate.",
                            field.Ref, distinct.Value)
                    });
                }
            }

            return Section(
                AssessmentSectionCodes.CandidateDimensions,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} dimension candidate(s) are carried in this report and none is promoted.",
                    candidates.Count),
                evidence,
                candidates);
        }

        private static AssessmentSection EvaluateCandidateMeasures(IReadOnlyList<FieldRef> fields)
        {
            List<FieldRef> measures = fields
                .Where(f => RoleIs(f.Field, IntakeFieldRoles.Measure))
                .ToList();

            if (measures.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.CandidateMeasures,
                    AssessmentStatus.Unknown,
                    "No measure-role field is declared, so no measure candidate can be derived.");
            }

            var candidates = measures
                .Select(f => new AssessmentCandidate
                {
                    CandidateKind = "measure",
                    CandidateCode = f.Ref,
                    IntakeRef = f.Ref,
                    Rationale = "Measure role. A candidate only; no canonical measure is registered."
                })
                .ToList();

            return Section(
                AssessmentSectionCodes.CandidateMeasures,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} measure candidate(s) are carried in this report and none is promoted.",
                    candidates.Count),
                Array.Empty<AssessmentEvidence>(),
                candidates);
        }

        private static AssessmentSection EvaluateAggregationGaps(
            Dictionary<string, DeclarationOutcome> declarations,
            IReadOnlyList<FieldRef> fields,
            AssessmentStatus measureStatus)
        {
            if (measureStatus != AssessmentStatus.Known)
            {
                return Section(
                    AssessmentSectionCodes.AggregationGaps,
                    AssessmentStatus.Blocked,
                    "Aggregation gaps cannot be enumerated before measure candidates exist.",
                    Array.Empty<AssessmentEvidence>(),
                    Array.Empty<AssessmentCandidate>(),
                    new List<AssessmentBlocker>
                    {
                        new AssessmentBlocker
                        {
                            BlockerCode = "SECTION_PREREQUISITE",
                            RequiredInput = AssessmentSectionCodes.CandidateMeasures,
                            Statement = "At least one measure candidate is required."
                        }
                    });
            }

            List<FieldRef> measures = fields
                .Where(f => RoleIs(f.Field, IntakeFieldRoles.Measure))
                .ToList();

            var evidence = new List<AssessmentEvidence>();
            var blockers = new List<AssessmentBlocker>();

            foreach (FieldRef field in measures)
            {
                string semantics = CustomerAssessmentNormalization.TrimOnly(field.Field.AggregationSemantics);
                if (semantics.Length == 0)
                {
                    blockers.Add(new AssessmentBlocker
                    {
                        BlockerCode = "AGGREGATION_SEMANTICS_UNDECLARED",
                        RequiredInput = field.Ref,
                        Statement = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} declares no aggregation semantics, so no aggregate over it can be defended.",
                            field.Ref)
                    });
                }
                else
                {
                    evidence.Add(new AssessmentEvidence
                    {
                        EvidenceCode = "AGGREGATION_SEMANTICS_DECLARED",
                        IntakeRef = field.Ref,
                        Statement = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} declares {1} aggregation semantics.",
                            field.Ref, semantics)
                    });
                }
            }

            DeclarationOutcome declared = Declaration(declarations, AssessmentDeclarationCodes.AggregationSemantics);
            if (declared.Kind == DeclarationOutcomeKind.Answered)
            {
                evidence.Add(DeclarationEvidence(AssessmentDeclarationCodes.AggregationSemantics, declared.Answer));
            }

            return Section(
                AssessmentSectionCodes.AggregationGaps,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} of {1} measure candidate(s) declare aggregation semantics; {2} gap(s) are named.",
                    measures.Count - blockers.Count, measures.Count, blockers.Count),
                evidence,
                Array.Empty<AssessmentCandidate>(),
                blockers);
        }

        private static AssessmentSection EvaluateDataQuality(
            Dictionary<string, DeclarationOutcome> declarations,
            IReadOnlyList<FieldRef> fields)
        {
            if (fields.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.DataQuality,
                    AssessmentStatus.Unknown,
                    "No field inventory was supplied, so no data quality statement can be made.");
            }

            List<FieldRef> measured = fields
                .Where(f => f.Field.NullFraction.HasValue)
                .ToList();

            DeclarationOutcome declared = Declaration(declarations, AssessmentDeclarationCodes.DataQualityProgramme);

            var evidence = new List<AssessmentEvidence>();
            if (declared.Kind == DeclarationOutcomeKind.Answered)
            {
                evidence.Add(DeclarationEvidence(AssessmentDeclarationCodes.DataQualityProgramme, declared.Answer));
            }

            foreach (FieldRef field in measured)
            {
                evidence.Add(new AssessmentEvidence
                {
                    EvidenceCode = "NULL_FRACTION_DECLARED",
                    IntakeRef = field.Ref,
                    Statement = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} declares a null fraction of {1}.",
                        field.Ref,
                        field.Field.NullFraction!.Value.ToString("0.####", CultureInfo.InvariantCulture))
                });
            }

            if (measured.Count == 0 && declared.Kind != DeclarationOutcomeKind.Answered)
            {
                return Section(
                    AssessmentSectionCodes.DataQuality,
                    AssessmentStatus.Unknown,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "None of the {0} declared field(s) carries a measured null fraction and no quality programme is declared.",
                        fields.Count));
            }

            return Section(
                AssessmentSectionCodes.DataQuality,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} of {1} declared field(s) carry a measured null fraction.",
                    measured.Count, fields.Count),
                evidence);
        }

        private static AssessmentSection EvaluateHistoricalCoverage(
            IReadOnlyList<CustomerIntakeSource> sources)
        {
            List<CustomerIntakeSource> windowed = sources
                .Where(s => s.EarliestObservationUtc.HasValue && s.LatestObservationUtc.HasValue)
                .ToList();

            if (windowed.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.HistoricalCoverage,
                    AssessmentStatus.Unknown,
                    "No source declares an observation window, so historical coverage cannot be concluded.");
            }

            var evidence = windowed
                .Select(s => new AssessmentEvidence
                {
                    EvidenceCode = "OBSERVATION_WINDOW_DECLARED",
                    IntakeRef = CustomerAssessmentNormalization.TrimOnly(s.SourceCode),
                    Statement = string.Format(
                        CultureInfo.InvariantCulture,
                        "Source {0} declares coverage from {1} to {2}.",
                        CustomerAssessmentNormalization.TrimOnly(s.SourceCode),
                        s.EarliestObservationUtc!.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                        s.LatestObservationUtc!.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
                })
                .ToList();

            return Section(
                AssessmentSectionCodes.HistoricalCoverage,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} of {1} source(s) declare an observation window.",
                    windowed.Count, sources.Count),
                evidence);
        }

        private static AssessmentSection EvaluateManualHistory(
            Dictionary<string, DeclarationOutcome> declarations,
            IReadOnlyList<CustomerIntakeSource> sources)
        {
            return EvaluateHistoryClass(
                AssessmentSectionCodes.ManualHistory,
                AssessmentDeclarationCodes.ManualHistoryAvailable,
                declarations,
                sources,
                true,
                "manual");
        }

        private static AssessmentSection EvaluateMachineHistory(
            Dictionary<string, DeclarationOutcome> declarations,
            IReadOnlyList<CustomerIntakeSource> sources)
        {
            return EvaluateHistoryClass(
                AssessmentSectionCodes.MachineHistory,
                AssessmentDeclarationCodes.MachineHistoryAvailable,
                declarations,
                sources,
                false,
                "machine");
        }

        private static AssessmentSection EvaluateHistoryClass(
            string sectionCode,
            string declarationCode,
            Dictionary<string, DeclarationOutcome> declarations,
            IReadOnlyList<CustomerIntakeSource> sources,
            bool manual,
            string label)
        {
            List<CustomerIntakeSource> matching = sources
                .Where(s => s.IsManualRecord.HasValue && s.IsManualRecord.Value == manual)
                .ToList();

            DeclarationOutcome declared = Declaration(declarations, declarationCode);

            var evidence = new List<AssessmentEvidence>();
            if (declared.Kind == DeclarationOutcomeKind.Answered)
            {
                evidence.Add(DeclarationEvidence(declarationCode, declared.Answer));
            }

            foreach (CustomerIntakeSource source in matching)
            {
                evidence.Add(new AssessmentEvidence
                {
                    EvidenceCode = "HISTORY_CLASS_DECLARED",
                    IntakeRef = CustomerAssessmentNormalization.TrimOnly(source.SourceCode),
                    Statement = string.Format(
                        CultureInfo.InvariantCulture,
                        "Source {0} is declared a {1} record.",
                        CustomerAssessmentNormalization.TrimOnly(source.SourceCode), label)
                });
            }

            if (declared.Status == AssessmentStatus.Known || matching.Count > 0)
            {
                return Section(
                    sectionCode,
                    AssessmentStatus.Known,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} {1} history source(s) are identified.",
                        matching.Count, label),
                    evidence);
            }

            if (declared.Status == AssessmentStatus.Missing || declared.Status == AssessmentStatus.NotApplicable)
            {
                return Section(
                    sectionCode,
                    declared.Status,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The intake answers the {0} history question with a reserved answer.",
                        label),
                    evidence);
            }

            if (declared.Status == AssessmentStatus.Blocked)
            {
                return Section(
                    sectionCode,
                    AssessmentStatus.Blocked,
                    "Conflicting declarations were supplied.",
                    evidence,
                    Array.Empty<AssessmentCandidate>(),
                    new List<AssessmentBlocker>
                    {
                        new AssessmentBlocker
                        {
                            BlockerCode = "DECLARATION_CONFLICT",
                            RequiredInput = declarationCode,
                            Statement = "Two or more different answers were supplied for the same declaration."
                        }
                    });
            }

            return Section(
                sectionCode,
                AssessmentStatus.Unknown,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "No source is declared a {0} record and the question was not answered.",
                    label));
        }

        private static AssessmentSection EvaluateSameWindowReconciliation(
            IReadOnlyList<CustomerIntakeSource> sources,
            AssessmentStatus manualStatus,
            AssessmentStatus machineStatus,
            AssessmentStatus coverageStatus)
        {
            var blockers = new List<AssessmentBlocker>();

            if (manualStatus != AssessmentStatus.Known)
            {
                blockers.Add(Prerequisite(AssessmentSectionCodes.ManualHistory));
            }

            if (machineStatus != AssessmentStatus.Known)
            {
                blockers.Add(Prerequisite(AssessmentSectionCodes.MachineHistory));
            }

            if (coverageStatus != AssessmentStatus.Known)
            {
                blockers.Add(Prerequisite(AssessmentSectionCodes.HistoricalCoverage));
            }

            if (blockers.Count > 0)
            {
                return Section(
                    AssessmentSectionCodes.SameWindowReconciliation,
                    AssessmentStatus.Blocked,
                    "Same-calendar-window reconciliation readiness cannot be concluded while a prerequisite is unresolved.",
                    Array.Empty<AssessmentEvidence>(),
                    Array.Empty<AssessmentCandidate>(),
                    blockers);
            }

            List<CustomerIntakeSource> manualSources = sources
                .Where(s => s.IsManualRecord.HasValue && s.IsManualRecord.Value
                            && s.EarliestObservationUtc.HasValue && s.LatestObservationUtc.HasValue)
                .ToList();

            List<CustomerIntakeSource> machineSources = sources
                .Where(s => s.IsManualRecord.HasValue && !s.IsManualRecord.Value
                            && s.EarliestObservationUtc.HasValue && s.LatestObservationUtc.HasValue)
                .ToList();

            if (manualSources.Count == 0 || machineSources.Count == 0)
            {
                return Section(
                    AssessmentSectionCodes.SameWindowReconciliation,
                    AssessmentStatus.Blocked,
                    "A manual and a machine source must both declare an observation window before an overlap can be computed.",
                    Array.Empty<AssessmentEvidence>(),
                    Array.Empty<AssessmentCandidate>(),
                    new List<AssessmentBlocker> { Prerequisite(AssessmentSectionCodes.HistoricalCoverage) });
            }

            DateTimeOffset manualStart = manualSources.Min(s => s.EarliestObservationUtc!.Value);
            DateTimeOffset manualEnd = manualSources.Max(s => s.LatestObservationUtc!.Value);
            DateTimeOffset machineStart = machineSources.Min(s => s.EarliestObservationUtc!.Value);
            DateTimeOffset machineEnd = machineSources.Max(s => s.LatestObservationUtc!.Value);

            DateTimeOffset overlapStart = manualStart > machineStart ? manualStart : machineStart;
            DateTimeOffset overlapEnd = manualEnd < machineEnd ? manualEnd : machineEnd;

            if (overlapEnd <= overlapStart)
            {
                return Section(
                    AssessmentSectionCodes.SameWindowReconciliation,
                    AssessmentStatus.Missing,
                    "The declared manual and machine windows do not overlap, so no same-calendar-window comparison exists.",
                    new List<AssessmentEvidence>
                    {
                        WindowEvidence("MANUAL_WINDOW", manualStart, manualEnd),
                        WindowEvidence("MACHINE_WINDOW", machineStart, machineEnd)
                    });
            }

            return Section(
                AssessmentSectionCodes.SameWindowReconciliation,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Manual and machine records overlap over {0} day(s) of calendar time.",
                    Math.Floor((overlapEnd - overlapStart).TotalDays).ToString(CultureInfo.InvariantCulture)),
                new List<AssessmentEvidence>
                {
                    WindowEvidence("MANUAL_WINDOW", manualStart, manualEnd),
                    WindowEvidence("MACHINE_WINDOW", machineStart, machineEnd),
                    WindowEvidence("OVERLAP_WINDOW", overlapStart, overlapEnd)
                });
        }

        private static AssessmentSection EvaluateStabilisationRules(
            Dictionary<string, DeclarationOutcome> declarations,
            AssessmentStatus transitionStatus)
        {
            if (transitionStatus != AssessmentStatus.Known)
            {
                return Section(
                    AssessmentSectionCodes.StabilisationRules,
                    AssessmentStatus.Blocked,
                    "A stabilisation rule has no meaning until a transition is defined.",
                    Array.Empty<AssessmentEvidence>(),
                    Array.Empty<AssessmentCandidate>(),
                    new List<AssessmentBlocker> { Prerequisite(AssessmentSectionCodes.TransitionDefinition) });
            }

            return EvaluateDeclarationOnly(
                AssessmentSectionCodes.StabilisationRules,
                AssessmentDeclarationCodes.StabilisationRule,
                declarations,
                "Declared rule for when a process is considered stabilised after a transition.");
        }

        private static AssessmentSection EvaluateMultiObjectiveReadiness(
            Dictionary<string, DeclarationOutcome> declarations,
            AssessmentStatus objectiveStatus,
            AssessmentStatus measureStatus)
        {
            var blockers = new List<AssessmentBlocker>();

            if (objectiveStatus != AssessmentStatus.Known)
            {
                blockers.Add(Prerequisite(AssessmentSectionCodes.ObjectiveSets));
            }

            if (measureStatus != AssessmentStatus.Known)
            {
                blockers.Add(Prerequisite(AssessmentSectionCodes.CandidateMeasures));
            }

            if (blockers.Count > 0)
            {
                return Section(
                    AssessmentSectionCodes.MultiObjectiveReadiness,
                    AssessmentStatus.Blocked,
                    "Trade-off readiness cannot be concluded while a prerequisite is unresolved.",
                    Array.Empty<AssessmentEvidence>(),
                    Array.Empty<AssessmentCandidate>(),
                    blockers);
            }

            return EvaluateDeclarationOnly(
                AssessmentSectionCodes.MultiObjectiveReadiness,
                AssessmentDeclarationCodes.ObjectiveTradeOff,
                declarations,
                "Declared preference for resolving competing objectives against one another.");
        }

        private static AssessmentSection EvaluateDependentReadiness(
            string sectionCode,
            string purpose,
            IReadOnlyList<string> prerequisites,
            IReadOnlyDictionary<string, AssessmentSection> byCode)
        {
            var blockers = new List<AssessmentBlocker>();
            var evidence = new List<AssessmentEvidence>();

            foreach (string prerequisite in prerequisites)
            {
                AssessmentStatus status = byCode.TryGetValue(prerequisite, out AssessmentSection? found)
                    ? found.Status
                    : AssessmentStatus.Unknown;

                if (status == AssessmentStatus.Known)
                {
                    evidence.Add(new AssessmentEvidence
                    {
                        EvidenceCode = "PREREQUISITE_SATISFIED",
                        IntakeRef = prerequisite,
                        Statement = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} is concluded.",
                            prerequisite)
                    });
                }
                else
                {
                    blockers.Add(Prerequisite(prerequisite));
                }
            }

            if (blockers.Count > 0)
            {
                return Section(
                    sectionCode,
                    AssessmentStatus.Blocked,
                    purpose + " " + string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} prerequisite section(s) are unresolved.",
                        blockers.Count),
                    evidence,
                    Array.Empty<AssessmentCandidate>(),
                    blockers);
            }

            return Section(sectionCode, AssessmentStatus.Known, purpose + " Every prerequisite is concluded.", evidence);
        }

        private static AssessmentSection EvaluateDeclarationOnly(
            string sectionCode,
            string declarationCode,
            Dictionary<string, DeclarationOutcome> declarations,
            string purpose)
        {
            DeclarationOutcome declared = Declaration(declarations, declarationCode);

            var evidence = new List<AssessmentEvidence>();
            if (declared.Kind == DeclarationOutcomeKind.Answered)
            {
                evidence.Add(DeclarationEvidence(declarationCode, declared.Answer));
            }

            var blockers = new List<AssessmentBlocker>();
            string statement;

            switch (declared.Kind)
            {
                case DeclarationOutcomeKind.Answered:
                    statement = purpose + " The intake answers this question.";
                    break;
                case DeclarationOutcomeKind.Absence:
                    statement = purpose + " The intake positively establishes that it does not exist.";
                    break;
                case DeclarationOutcomeKind.NotApplicable:
                    statement = purpose + " An explicit declaration states that it does not apply.";
                    break;
                case DeclarationOutcomeKind.Conflicting:
                    statement = purpose + " Conflicting answers were supplied.";
                    blockers.Add(new AssessmentBlocker
                    {
                        BlockerCode = "DECLARATION_CONFLICT",
                        RequiredInput = declarationCode,
                        Statement = "Two or more different answers were supplied for the same declaration."
                    });
                    break;
                case DeclarationOutcomeKind.Unanswered:
                    statement = purpose + " The question was presented and not answered.";
                    blockers.Add(new AssessmentBlocker
                    {
                        BlockerCode = "DECLARATION_REQUIRED",
                        RequiredInput = declarationCode,
                        Statement = "An answer to this declaration is required before a conclusion can be drawn."
                    });
                    break;
                default:
                    statement = purpose + " No declaration was supplied.";
                    blockers.Add(new AssessmentBlocker
                    {
                        BlockerCode = "DECLARATION_REQUIRED",
                        RequiredInput = declarationCode,
                        Statement = "An answer to this declaration is required before a conclusion can be drawn."
                    });
                    break;
            }

            return Section(
                sectionCode,
                declared.Status,
                statement,
                evidence,
                Array.Empty<AssessmentCandidate>(),
                blockers);
        }

        private static AssessmentSection EvaluateMissingInformation(IReadOnlyList<AssessmentSection> priorSections)
        {
            var blockers = new List<AssessmentBlocker>();

            foreach (AssessmentSection section in priorSections)
            {
                if (section.Status == AssessmentStatus.Known || section.Status == AssessmentStatus.NotApplicable)
                {
                    continue;
                }

                blockers.Add(new AssessmentBlocker
                {
                    BlockerCode = "SECTION_" + AssessmentStatusCodes.ToWire(section.Status),
                    RequiredInput = section.SectionCode,
                    Statement = section.Statement
                });
            }

            foreach (AssessmentSection section in priorSections)
            {
                foreach (AssessmentBlocker blocker in section.Blockers)
                {
                    blockers.Add(new AssessmentBlocker
                    {
                        BlockerCode = blocker.BlockerCode,
                        RequiredInput = blocker.RequiredInput,
                        Statement = section.SectionCode + ": " + blocker.Statement
                    });
                }
            }

            int unknown = priorSections.Count(s => s.Status == AssessmentStatus.Unknown);
            int missing = priorSections.Count(s => s.Status == AssessmentStatus.Missing);
            int blocked = priorSections.Count(s => s.Status == AssessmentStatus.Blocked);

            return Section(
                AssessmentSectionCodes.MissingInformation,
                AssessmentStatus.Known,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} unknown, {1} missing and {2} blocked section(s) are outstanding across the assessment.",
                    unknown, missing, blocked),
                Array.Empty<AssessmentEvidence>(),
                Array.Empty<AssessmentCandidate>(),
                blockers);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static AssessmentBlocker Prerequisite(string sectionCode)
        {
            return new AssessmentBlocker
            {
                BlockerCode = "SECTION_PREREQUISITE",
                RequiredInput = sectionCode,
                Statement = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} must be concluded first.",
                    sectionCode)
            };
        }

        private static AssessmentEvidence DeclarationEvidence(string declarationCode, string? answer)
        {
            return new AssessmentEvidence
            {
                EvidenceCode = "DECLARATION_ANSWERED",
                IntakeRef = declarationCode,
                Statement = string.Format(
                    CultureInfo.InvariantCulture,
                    "Declaration {0} is answered: {1}",
                    declarationCode,
                    answer ?? string.Empty)
            };
        }

        private static AssessmentEvidence WindowEvidence(string code, DateTimeOffset start, DateTimeOffset end)
        {
            return new AssessmentEvidence
            {
                EvidenceCode = code,
                Statement = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} runs from {1} to {2}.",
                    code,
                    start.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    end.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
            };
        }

        private static AssessmentSection Section(
            string sectionCode,
            AssessmentStatus status,
            string statement,
            IReadOnlyList<AssessmentEvidence>? evidence = null,
            IReadOnlyList<AssessmentCandidate>? candidates = null,
            IReadOnlyList<AssessmentBlocker>? blockers = null)
        {
            return new AssessmentSection
            {
                SectionCode = sectionCode,
                Status = status,
                Statement = statement,
                Evidence = evidence ?? Array.Empty<AssessmentEvidence>(),
                Candidates = candidates ?? Array.Empty<AssessmentCandidate>(),
                Blockers = blockers ?? Array.Empty<AssessmentBlocker>()
            };
        }

        private static void AssertSectionCompleteness(IReadOnlyList<AssessmentSection> sections)
        {
            if (sections.Count != AssessmentSectionCodes.Ordered.Count)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The assessment produced {0} section(s); the contract requires exactly {1}.",
                        sections.Count,
                        AssessmentSectionCodes.Ordered.Count));
            }

            for (int i = 0; i < sections.Count; i++)
            {
                if (!string.Equals(sections[i].SectionCode, AssessmentSectionCodes.Ordered[i], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Section {0} is {1} but the contract requires {2}.",
                            i,
                            sections[i].SectionCode,
                            AssessmentSectionCodes.Ordered[i]));
                }
            }
        }
    }
}
