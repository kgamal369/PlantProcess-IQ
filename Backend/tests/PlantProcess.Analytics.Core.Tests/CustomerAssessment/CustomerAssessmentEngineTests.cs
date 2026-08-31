// ============================================================================
// Engine known-answer tests.
//
// These run against the foreign municipal-water fixture and assert the exact
// conclusions the engine must reach. They touch no database and no clock.
//
// ON THE FINGERPRINT
//   The expected fingerprint is deliberately NOT pinned as a hexadecimal
//   literal. The canonical text contains round-trip formatted doubles, and a
//   literal would silently bind the product to one runtime's formatting rather
//   than to the rule it is meant to protect. What is asserted instead is the
//   behaviour that matters: stability under ordering and non-semantic
//   metadata, and change under a rule-version or intake change.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using PlantProcess.Application.CustomerAssessment;
using Xunit;

namespace PlantProcess.Application.Tests.CustomerAssessment
{
    public sealed class CustomerAssessmentEngineTests
    {
        private static readonly CustomerAssessmentEngine Engine = new CustomerAssessmentEngine();

        private static Dictionary<string, AssessmentSection> Index(CustomerAssessmentReport report)
        {
            return report.Sections.ToDictionary(s => s.SectionCode, StringComparer.Ordinal);
        }

        [Fact]
        public void Report_carries_all_twenty_six_areas_in_contract_order()
        {
            CustomerAssessmentReport report = Engine.Evaluate(ForeignIntakeFixture.V1());

            Assert.Equal(26, AssessmentSectionCodes.Ordered.Count);
            Assert.Equal(26, report.Sections.Count);
            Assert.Equal(
                AssessmentSectionCodes.Ordered.ToList(),
                report.Sections.Select(s => s.SectionCode).ToList());
            Assert.Equal(26, report.Sections.Select(s => s.SectionCode).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void Every_section_carries_one_of_exactly_five_status_codes()
        {
            CustomerAssessmentReport report = Engine.Evaluate(ForeignIntakeFixture.V1());

            var permitted = new HashSet<string>(StringComparer.Ordinal)
            {
                AssessmentStatusCodes.Known,
                AssessmentStatusCodes.Unknown,
                AssessmentStatusCodes.Missing,
                AssessmentStatusCodes.NotApplicable,
                AssessmentStatusCodes.Blocked
            };

            foreach (AssessmentSection section in report.Sections)
            {
                Assert.Contains(section.StatusCode, permitted);
                Assert.False(string.IsNullOrWhiteSpace(section.Statement));
            }
        }

        [Fact]
        public void V1_reaches_the_expected_conclusion_for_every_area()
        {
            Dictionary<string, AssessmentSection> sections = Index(Engine.Evaluate(ForeignIntakeFixture.V1()));

            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.SourceInventory].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.EntityMap].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.IdentityStrategy].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.JoinKeyStrategy].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.TimeModel].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.TimeAvailability].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.ParameterCatalogue].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.CandidateDimensions].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.CandidateMeasures].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.AggregationGaps].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.DataQuality].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.HistoricalCoverage].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.ManualHistory].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.MachineHistory].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.SameWindowReconciliation].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.ReconciliationEligibility].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.MlEligibility].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.OtTrialRequirements].Status);
            Assert.Equal(AssessmentStatus.Known, sections[AssessmentSectionCodes.MissingInformation].Status);

            // The intake positively states that no reference specification exists.
            Assert.Equal(AssessmentStatus.Missing, sections[AssessmentSectionCodes.ReferenceAvailability].Status);

            // An explicit reserved answer, not an absent field.
            Assert.Equal(AssessmentStatus.NotApplicable, sections[AssessmentSectionCodes.SequenceBoundaries].Status);

            // Presented and not answered, or not supplied at all.
            Assert.Equal(AssessmentStatus.Unknown, sections[AssessmentSectionCodes.TransitionDefinition].Status);
            Assert.Equal(AssessmentStatus.Unknown, sections[AssessmentSectionCodes.SetupEvidence].Status);
            Assert.Equal(AssessmentStatus.Unknown, sections[AssessmentSectionCodes.ObjectiveSets].Status);

            // Named prerequisites, not silent unknowns.
            Assert.Equal(AssessmentStatus.Blocked, sections[AssessmentSectionCodes.StabilisationRules].Status);
            Assert.Equal(AssessmentStatus.Blocked, sections[AssessmentSectionCodes.MultiObjectiveReadiness].Status);
        }

        [Fact]
        public void An_absent_declaration_is_never_reported_as_missing()
        {
            Dictionary<string, AssessmentSection> sections = Index(Engine.Evaluate(ForeignIntakeFixture.V1()));

            // setup.evidence is absent from the intake entirely.
            AssessmentSection setup = sections[AssessmentSectionCodes.SetupEvidence];
            Assert.Equal(AssessmentStatus.Unknown, setup.Status);
            Assert.NotEqual(AssessmentStatus.Missing, setup.Status);
            Assert.Contains(setup.Blockers, b => b.RequiredInput == AssessmentDeclarationCodes.SetupEvidence);

            // reference.specification.available is present and answered "none".
            Assert.Equal(AssessmentStatus.Missing, sections[AssessmentSectionCodes.ReferenceAvailability].Status);
        }

        [Fact]
        public void Not_applicable_is_reached_only_through_an_explicit_rule_or_answer()
        {
            Dictionary<string, AssessmentSection> sections = Index(Engine.Evaluate(ForeignIntakeFixture.V1()));

            foreach (AssessmentSection section in sections.Values)
            {
                if (section.Status != AssessmentStatus.NotApplicable)
                {
                    continue;
                }

                bool explicitRule = section.Evidence.Any(e => e.EvidenceCode == "EXPLICIT_RULE");
                bool explicitAnswer = section.Statement.Contains("explicit declaration", StringComparison.Ordinal);

                Assert.True(
                    explicitRule || explicitAnswer,
                    section.SectionCode + " reached NOT_APPLICABLE without an explicit rule or answer.");
            }
        }

        [Fact]
        public void A_blocked_section_always_names_the_input_that_prevents_a_conclusion()
        {
            CustomerAssessmentReport report = Engine.Evaluate(ForeignIntakeFixture.V1());

            foreach (AssessmentSection section in report.Sections.Where(s => s.Status == AssessmentStatus.Blocked))
            {
                Assert.NotEmpty(section.Blockers);
                Assert.All(section.Blockers, b => Assert.False(string.IsNullOrWhiteSpace(b.RequiredInput)));
            }
        }

        [Fact]
        public void Candidates_are_carried_and_are_never_described_as_canonical()
        {
            Dictionary<string, AssessmentSection> sections = Index(Engine.Evaluate(ForeignIntakeFixture.V1()));

            Assert.Equal(4, sections[AssessmentSectionCodes.CandidateMeasures].Candidates.Count);
            Assert.Equal(3, sections[AssessmentSectionCodes.CandidateDimensions].Candidates.Count);
            Assert.Equal(2, sections[AssessmentSectionCodes.IdentityStrategy].Candidates.Count);
            Assert.Equal(2, sections[AssessmentSectionCodes.JoinKeyStrategy].Candidates.Count);

            Assert.All(
                sections[AssessmentSectionCodes.CandidateMeasures].Candidates,
                c => Assert.Contains("candidate only", c.Rationale ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void The_derived_counts_match_the_declared_structure()
        {
            Dictionary<string, AssessmentSection> sections = Index(Engine.Evaluate(ForeignIntakeFixture.V1()));

            Assert.Contains("2 source(s) and 12 field(s)", sections[AssessmentSectionCodes.SourceInventory].Statement, StringComparison.Ordinal);
            Assert.Contains("7 parameter(s)", sections[AssessmentSectionCodes.ParameterCatalogue].Statement, StringComparison.Ordinal);
            Assert.Contains("3 of 4 measure candidate(s)", sections[AssessmentSectionCodes.AggregationGaps].Statement, StringComparison.Ordinal);
            Assert.Contains("2 of 3 time semantics", sections[AssessmentSectionCodes.TimeAvailability].Statement, StringComparison.Ordinal);
            Assert.Contains("394 day(s)", sections[AssessmentSectionCodes.SameWindowReconciliation].Statement, StringComparison.Ordinal);
        }

        [Fact]
        public void The_single_undeclared_aggregation_semantic_is_named_as_a_gap()
        {
            Dictionary<string, AssessmentSection> sections = Index(Engine.Evaluate(ForeignIntakeFixture.V1()));

            IReadOnlyList<AssessmentBlocker> gaps = sections[AssessmentSectionCodes.AggregationGaps].Blockers;

            Assert.Single(gaps);
            Assert.Equal("AGGREGATION_SEMANTICS_UNDECLARED", gaps[0].BlockerCode);
            Assert.EndsWith("chlorine_residual_mgl", gaps[0].RequiredInput, StringComparison.Ordinal);
        }

        [Fact]
        public void Missing_information_aggregates_every_unresolved_area()
        {
            CustomerAssessmentReport report = Engine.Evaluate(ForeignIntakeFixture.V1());
            Dictionary<string, AssessmentSection> sections = Index(report);

            int unresolved = report.Sections.Count(s =>
                s.Status == AssessmentStatus.Unknown
                || s.Status == AssessmentStatus.Missing
                || s.Status == AssessmentStatus.Blocked);

            Assert.Equal(6, unresolved);
            Assert.Contains(
                "3 unknown, 1 missing and 2 blocked",
                sections[AssessmentSectionCodes.MissingInformation].Statement,
                StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------
        // Determinism and fingerprint
        // ------------------------------------------------------------------

        [Fact]
        public void The_same_intake_produces_an_identical_report()
        {
            CustomerAssessmentReport first = Engine.Evaluate(ForeignIntakeFixture.V1());
            CustomerAssessmentReport second = Engine.Evaluate(ForeignIntakeFixture.V1());

            CustomerAssessmentDiff diff = CustomerAssessmentDiffCalculator.Compute(first, 1, second, 1);
            Assert.Empty(diff.Entries);
        }

        [Fact]
        public void Collection_order_and_display_name_change_nothing()
        {
            CustomerAssessmentReport ordered = Engine.Evaluate(ForeignIntakeFixture.V1());
            CustomerAssessmentReport shuffled = Engine.Evaluate(ForeignIntakeFixture.V1Shuffled());

            CustomerAssessmentDiff diff = CustomerAssessmentDiffCalculator.Compute(ordered, 1, shuffled, 1);
            Assert.Empty(diff.Entries);
        }

        [Fact]
        public void The_fingerprint_is_sixty_four_lowercase_hexadecimal_characters()
        {
            string fingerprint = CustomerAssessmentNormalization.ComputeFingerprint(
                ForeignIntakeFixture.V1(), "1.0.0", "1.0.0");

            Assert.Equal(64, fingerprint.Length);
            Assert.All(fingerprint, c => Assert.True("0123456789abcdef".IndexOf(c) >= 0));
        }

        [Fact]
        public void The_fingerprint_ignores_ordering_and_non_semantic_metadata()
        {
            string ordered = CustomerAssessmentNormalization.ComputeFingerprint(
                ForeignIntakeFixture.V1(), "1.0.0", "1.0.0");

            string shuffled = CustomerAssessmentNormalization.ComputeFingerprint(
                ForeignIntakeFixture.V1Shuffled(), "1.0.0", "1.0.0");

            Assert.Equal(ordered, shuffled);
        }

        [Fact]
        public void A_changed_rule_version_changes_the_fingerprint_with_an_unchanged_structure()
        {
            CustomerIntake intake = ForeignIntakeFixture.V1();

            string atRuleOne = CustomerAssessmentNormalization.ComputeFingerprint(intake, "1.0.0", "1.0.0");
            string atRuleTwo = CustomerAssessmentNormalization.ComputeFingerprint(intake, "1.0.0", "1.0.1");

            Assert.NotEqual(atRuleOne, atRuleTwo);
        }

        [Fact]
        public void A_changed_contract_version_changes_the_fingerprint()
        {
            CustomerIntake intake = ForeignIntakeFixture.V1();

            Assert.NotEqual(
                CustomerAssessmentNormalization.ComputeFingerprint(intake, "1.0.0", "1.0.0"),
                CustomerAssessmentNormalization.ComputeFingerprint(intake, "1.1.0", "1.0.0"));
        }

        [Fact]
        public void A_meaningful_declaration_change_changes_the_fingerprint()
        {
            Assert.NotEqual(
                CustomerAssessmentNormalization.ComputeFingerprint(ForeignIntakeFixture.V1(), "1.0.0", "1.0.0"),
                CustomerAssessmentNormalization.ComputeFingerprint(ForeignIntakeFixture.V2(), "1.0.0", "1.0.0"));
        }

        // ------------------------------------------------------------------
        // V1 to V2
        // ------------------------------------------------------------------

        [Fact]
        public void V2_changes_the_directly_affected_and_the_legitimately_derived_findings_only()
        {
            CustomerAssessmentReport v1 = Engine.Evaluate(ForeignIntakeFixture.V1());
            CustomerAssessmentReport v2 = Engine.Evaluate(ForeignIntakeFixture.V2());

            Dictionary<string, AssessmentSection> before = Index(v1);

            List<string> changed = v2.Sections
                .Where(s => before[s.SectionCode].Status != s.Status)
                .Select(s => s.SectionCode)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();

            // Two directly affected by the new declarations, and two derived
            // from them because their prerequisite resolved. This is a fact
            // about this fixture, not a universal product law.
            Assert.Equal(
                new List<string>
                {
                    AssessmentSectionCodes.MultiObjectiveReadiness,
                    AssessmentSectionCodes.ObjectiveSets,
                    AssessmentSectionCodes.StabilisationRules,
                    AssessmentSectionCodes.TransitionDefinition
                },
                changed);

            Dictionary<string, AssessmentSection> after = Index(v2);

            Assert.Equal(AssessmentStatus.Known, after[AssessmentSectionCodes.TransitionDefinition].Status);
            Assert.Equal(AssessmentStatus.Known, after[AssessmentSectionCodes.ObjectiveSets].Status);

            // The prerequisite resolved, so these stop being BLOCKED and fall
            // back to the honest UNKNOWN their own declaration warrants.
            Assert.Equal(AssessmentStatus.Unknown, after[AssessmentSectionCodes.StabilisationRules].Status);
            Assert.Equal(AssessmentStatus.Unknown, after[AssessmentSectionCodes.MultiObjectiveReadiness].Status);
        }

        [Fact]
        public void Unrelated_findings_are_stable_across_V1_and_V2()
        {
            CustomerAssessmentReport v1 = Engine.Evaluate(ForeignIntakeFixture.V1());
            CustomerAssessmentReport v2 = Engine.Evaluate(ForeignIntakeFixture.V2());

            Dictionary<string, AssessmentSection> before = Index(v1);
            Dictionary<string, AssessmentSection> after = Index(v2);

            string[] unrelated =
            {
                AssessmentSectionCodes.SourceInventory,
                AssessmentSectionCodes.EntityMap,
                AssessmentSectionCodes.IdentityStrategy,
                AssessmentSectionCodes.JoinKeyStrategy,
                AssessmentSectionCodes.TimeModel,
                AssessmentSectionCodes.TimeAvailability,
                AssessmentSectionCodes.ParameterCatalogue,
                AssessmentSectionCodes.CandidateDimensions,
                AssessmentSectionCodes.CandidateMeasures,
                AssessmentSectionCodes.ReferenceAvailability,
                AssessmentSectionCodes.AggregationGaps,
                AssessmentSectionCodes.DataQuality,
                AssessmentSectionCodes.HistoricalCoverage,
                AssessmentSectionCodes.ManualHistory,
                AssessmentSectionCodes.MachineHistory,
                AssessmentSectionCodes.SameWindowReconciliation,
                AssessmentSectionCodes.SequenceBoundaries,
                AssessmentSectionCodes.SetupEvidence,
                AssessmentSectionCodes.ReconciliationEligibility,
                AssessmentSectionCodes.MlEligibility,
                AssessmentSectionCodes.OtTrialRequirements
            };

            foreach (string code in unrelated)
            {
                Assert.Equal(before[code].Status, after[code].Status);
                Assert.Equal(before[code].Statement, after[code].Statement);
            }
        }

        [Fact]
        public void The_diff_identifies_the_semantic_changes_between_two_versions()
        {
            CustomerAssessmentReport v1 = Engine.Evaluate(ForeignIntakeFixture.V1());
            CustomerAssessmentReport v2 = Engine.Evaluate(ForeignIntakeFixture.V2());

            CustomerAssessmentDiff diff = CustomerAssessmentDiffCalculator.Compute(v1, 1, v2, 2);

            Assert.Equal(1, diff.FromVersionNumber);
            Assert.Equal(2, diff.ToVersionNumber);
            Assert.True(diff.ReadinessChanged);

            List<AssessmentDiffEntry> statusChanges = diff.Entries
                .Where(e => e.ChangeKind == AssessmentChangeKinds.SectionStatusChanged)
                .ToList();

            Assert.Equal(4, statusChanges.Count);

            Assert.Contains(diff.Entries, e =>
                e.ChangeKind == AssessmentChangeKinds.BlockerResolved
                && e.SectionCode == AssessmentSectionCodes.StabilisationRules);

            Assert.Contains(diff.Entries, e =>
                e.ChangeKind == AssessmentChangeKinds.EvidenceAdded
                && e.SectionCode == AssessmentSectionCodes.TransitionDefinition);
        }

        // ------------------------------------------------------------------
        // Falsification
        // ------------------------------------------------------------------

        [Fact]
        public void An_empty_intake_concludes_nothing_and_claims_nothing()
        {
            CustomerAssessmentReport report = Engine.Evaluate(new CustomerIntake { LineageCode = "EMPTY" });
            Dictionary<string, AssessmentSection> sections = Index(report);

            Assert.Equal(26, report.Sections.Count);
            Assert.Equal(AssessmentStatus.Unknown, sections[AssessmentSectionCodes.SourceInventory].Status);
            Assert.Equal(AssessmentStatus.Unknown, sections[AssessmentSectionCodes.EntityMap].Status);
            Assert.Equal(AssessmentStatus.Blocked, sections[AssessmentSectionCodes.TimeAvailability].Status);

            // Nothing is MISSING, because nothing established an absence.
            Assert.DoesNotContain(
                report.Sections.Where(s => s.SectionCode != AssessmentSectionCodes.MissingInformation),
                s => s.Status == AssessmentStatus.Missing);
        }

        [Fact]
        public void A_single_source_intake_is_not_applicable_for_a_cross_source_join()
        {
            CustomerIntake intake = ForeignIntakeFixture.V1();

            var single = new CustomerIntake
            {
                LineageCode = intake.LineageCode,
                Sources = new List<CustomerIntakeSource> { intake.Sources[0] },
                Entities = intake.Entities,
                Declarations = intake.Declarations
            };

            AssessmentSection join = Index(Engine.Evaluate(single))[AssessmentSectionCodes.JoinKeyStrategy];

            Assert.Equal(AssessmentStatus.NotApplicable, join.Status);
            Assert.Contains(join.Evidence, e => e.IntakeRef == AssessmentRuleCodes.SingleSourceNoCrossSourceJoin);
        }

        [Fact]
        public void Conflicting_answers_to_one_declaration_block_rather_than_pick_a_winner()
        {
            CustomerIntake intake = ForeignIntakeFixture.V1();

            var declarations = new List<CustomerIntakeDeclaration>(intake.Declarations)
            {
                new CustomerIntakeDeclaration
                {
                    DeclarationCode = AssessmentDeclarationCodes.OtTrialRequirement,
                    Value = "a second and different answer"
                }
            };

            var conflicted = new CustomerIntake
            {
                LineageCode = intake.LineageCode,
                Sources = intake.Sources,
                Entities = intake.Entities,
                Declarations = declarations
            };

            AssessmentSection section = Index(Engine.Evaluate(conflicted))[AssessmentSectionCodes.OtTrialRequirements];

            Assert.Equal(AssessmentStatus.Blocked, section.Status);
            Assert.Contains(section.Blockers, b => b.BlockerCode == "DECLARATION_CONFLICT");
        }

        [Fact]
        public void An_unresolved_entity_parent_blocks_the_entity_map()
        {
            CustomerIntake intake = ForeignIntakeFixture.V1();

            var entities = new List<CustomerIntakeEntity>(intake.Entities)
            {
                new CustomerIntakeEntity { EntityCode = "ORPHAN", ParentEntityCode = "NEVER_DECLARED" }
            };

            var broken = new CustomerIntake
            {
                LineageCode = intake.LineageCode,
                Sources = intake.Sources,
                Entities = entities,
                Declarations = intake.Declarations
            };

            AssessmentSection map = Index(Engine.Evaluate(broken))[AssessmentSectionCodes.EntityMap];

            Assert.Equal(AssessmentStatus.Blocked, map.Status);
            Assert.Contains(map.Blockers, b => b.RequiredInput == "NEVER_DECLARED");
        }

        [Fact]
        public void Non_overlapping_manual_and_machine_windows_are_reported_as_missing_not_known()
        {
            CustomerIntake intake = ForeignIntakeFixture.V1();

            var sources = new List<CustomerIntakeSource>();
            foreach (CustomerIntakeSource source in intake.Sources)
            {
                if (source.IsManualRecord == true)
                {
                    sources.Add(new CustomerIntakeSource
                    {
                        SourceCode = source.SourceCode,
                        SourceKind = source.SourceKind,
                        IsManualRecord = true,
                        EarliestObservationUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                        LatestObservationUtc = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero),
                        Tables = source.Tables
                    });
                }
                else
                {
                    sources.Add(source);
                }
            }

            var disjoint = new CustomerIntake
            {
                LineageCode = intake.LineageCode,
                Sources = sources,
                Entities = intake.Entities,
                Declarations = intake.Declarations
            };

            Dictionary<string, AssessmentSection> sections = Index(Engine.Evaluate(disjoint));

            Assert.Equal(AssessmentStatus.Missing, sections[AssessmentSectionCodes.SameWindowReconciliation].Status);
            Assert.Equal(AssessmentStatus.Blocked, sections[AssessmentSectionCodes.ReconciliationEligibility].Status);
        }

        [Fact]
        public void The_engine_refuses_a_null_intake_rather_than_producing_an_empty_report()
        {
            Assert.Throws<ArgumentNullException>(() => Engine.Evaluate(null!));
        }
    }
}
