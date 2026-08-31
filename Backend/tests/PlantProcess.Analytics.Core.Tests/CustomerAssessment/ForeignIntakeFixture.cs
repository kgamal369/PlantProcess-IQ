// ============================================================================
// A deliberately foreign intake fixture.
//
// Its whole purpose is to prove that the generic engine does not know the
// customer's vocabulary in advance. Every word below that names a plant, an
// instrument or a substance exists ONLY in this file and in the expected test
// evidence. None of it appears in an enum, a runtime default, a product
// constant, a generic mapping or an equipment ontology.
//
// There is no oil vocabulary and no steel vocabulary anywhere in the product,
// and this fixture is not permitted to add a third.
// ============================================================================

using System;
using System.Collections.Generic;
using PlantProcess.Application.CustomerAssessment;

namespace PlantProcess.Application.Tests.CustomerAssessment
{
    public static class ForeignIntakeFixture
    {
        public const string LineageCode = "MUNICIPAL-WATER-INTAKE-A";

        private static readonly DateTimeOffset MachineFrom =
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private static readonly DateTimeOffset MachineTo =
            new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

        private static readonly DateTimeOffset ManualFrom =
            new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        private static readonly DateTimeOffset ManualTo =
            new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

        /// <summary>The first intake the customer supplies.</summary>
        public static CustomerIntake V1()
        {
            return new CustomerIntake
            {
                LineageCode = LineageCode,
                DisplayName = "Municipal water treatment - first structure hand-over",
                Sources = new List<CustomerIntakeSource>
                {
                    new CustomerIntakeSource
                    {
                        SourceCode = "SCADA_HISTORIAN",
                        SourceKind = "historian",
                        IsManualRecord = false,
                        EarliestObservationUtc = MachineFrom,
                        LatestObservationUtc = MachineTo,
                        Tables = new List<CustomerIntakeTable>
                        {
                            new CustomerIntakeTable
                            {
                                TableCode = "tag_reading",
                                DeclaredRowCount = 41_260_000,
                                Fields = new List<CustomerIntakeField>
                                {
                                    Field("reading_ts", "timestamp", IntakeFieldRoles.Time,
                                        timeSemantics: IntakeTimeSemantics.Source, nullFraction: 0.0),
                                    Field("ingest_ts", "timestamp", IntakeFieldRoles.Time,
                                        timeSemantics: IntakeTimeSemantics.Ingest, nullFraction: 0.0),
                                    Field("tag_code", "varchar", IntakeFieldRoles.Identity,
                                        distinctCount: 412, nullFraction: 0.0),
                                    Field("turbidity_ntu", "numeric", IntakeFieldRoles.Measure,
                                        unit: "NTU", aggregation: "Average", nullFraction: 0.0031),
                                    Field("chlorine_residual_mgl", "numeric", IntakeFieldRoles.Measure,
                                        unit: "mg/l", nullFraction: 0.0117),
                                    Field("flow_m3h", "numeric", IntakeFieldRoles.Measure,
                                        unit: "m3/h", aggregation: "TimeWeightedMean", nullFraction: 0.0004),
                                    Field("treatment_train", "varchar", IntakeFieldRoles.Attribute,
                                        distinctCount: 3, nullFraction: 0.0)
                                }
                            }
                        }
                    },
                    new CustomerIntakeSource
                    {
                        SourceCode = "LAB_BENCH_BOOK",
                        SourceKind = "spreadsheet_export",
                        IsManualRecord = true,
                        EarliestObservationUtc = ManualFrom,
                        LatestObservationUtc = ManualTo,
                        Tables = new List<CustomerIntakeTable>
                        {
                            new CustomerIntakeTable
                            {
                                TableCode = "bench_sample",
                                DeclaredRowCount = 18_400,
                                Fields = new List<CustomerIntakeField>
                                {
                                    Field("sample_ts", "timestamp", IntakeFieldRoles.Time,
                                        timeSemantics: IntakeTimeSemantics.Source, nullFraction: 0.0),
                                    Field("tag_code", "varchar", IntakeFieldRoles.Identity,
                                        distinctCount: 96, nullFraction: 0.0),
                                    Field("turbidity_ntu", "numeric", IntakeFieldRoles.Measure,
                                        unit: "NTU", aggregation: "Average", nullFraction: 0.0208),
                                    Field("operator_initials", "varchar", IntakeFieldRoles.Attribute,
                                        distinctCount: 7, nullFraction: 0.0442),
                                    Field("sample_point", "varchar", IntakeFieldRoles.Attribute,
                                        distinctCount: 1, nullFraction: 0.0)
                                }
                            }
                        }
                    }
                },
                Entities = new List<CustomerIntakeEntity>
                {
                    new CustomerIntakeEntity { EntityCode = "NETWORK" },
                    new CustomerIntakeEntity { EntityCode = "TREATMENT_WORKS", ParentEntityCode = "NETWORK" },
                    new CustomerIntakeEntity
                    {
                        EntityCode = "FILTER_TRAIN",
                        ParentEntityCode = "TREATMENT_WORKS",
                        IdentityFieldRef = "SCADA_HISTORIAN.tag_reading.treatment_train"
                    }
                },
                Declarations = new List<CustomerIntakeDeclaration>
                {
                    Declaration(AssessmentDeclarationCodes.IdentityStrategy,
                        "tag_code is unique per instrument within one treatment works"),
                    Declaration(AssessmentDeclarationCodes.JoinStrategy,
                        "bench samples align to historian readings on tag_code and nearest reading_ts"),
                    Declaration(AssessmentDeclarationCodes.TimeModel,
                        "instrument event time is recorded at the tag; arrival time is recorded on ingest"),
                    Declaration(AssessmentDeclarationCodes.ReferenceSpecificationAvailable,
                        AssessmentDeclarationAnswers.None),
                    Declaration(AssessmentDeclarationCodes.SequenceBoundary,
                        AssessmentDeclarationAnswers.NotApplicable),
                    Declaration(AssessmentDeclarationCodes.OtTrialRequirement,
                        "read-only OPC UA against the historian mirror; no write path"),
                    Declaration(AssessmentDeclarationCodes.TransitionDefinition, null)
                }
            };
        }

        /// <summary>
        /// The same structure after the customer answers two further questions.
        /// Nothing about the sources, tables or fields changes.
        /// </summary>
        public static CustomerIntake V2()
        {
            CustomerIntake v1 = V1();

            var declarations = new List<CustomerIntakeDeclaration>();
            foreach (CustomerIntakeDeclaration declaration in v1.Declarations)
            {
                if (string.Equals(
                        declaration.DeclarationCode,
                        AssessmentDeclarationCodes.TransitionDefinition,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                declarations.Add(declaration);
            }

            declarations.Add(Declaration(AssessmentDeclarationCodes.TransitionDefinition,
                "a filter backwash starts a new operating regime and ends when turbidity returns below the works limit"));
            declarations.Add(Declaration(AssessmentDeclarationCodes.ObjectiveSet,
                "hold treated turbidity below the works limit while reducing chemical dose"));

            return new CustomerIntake
            {
                LineageCode = v1.LineageCode,
                DisplayName = v1.DisplayName,
                Sources = v1.Sources,
                Entities = v1.Entities,
                Declarations = declarations
            };
        }

        /// <summary>
        /// V1 with every collection reversed, one display name changed and all
        /// values otherwise identical. Used to prove the fingerprint is stable
        /// under ordering and under non-semantic metadata.
        /// </summary>
        public static CustomerIntake V1Shuffled()
        {
            CustomerIntake v1 = V1();

            var sources = new List<CustomerIntakeSource>(v1.Sources);
            sources.Reverse();

            var reshaped = new List<CustomerIntakeSource>();
            foreach (CustomerIntakeSource source in sources)
            {
                var tables = new List<CustomerIntakeTable>();
                foreach (CustomerIntakeTable table in source.Tables)
                {
                    var fields = new List<CustomerIntakeField>(table.Fields);
                    fields.Reverse();
                    tables.Add(new CustomerIntakeTable
                    {
                        TableCode = table.TableCode,
                        DeclaredRowCount = table.DeclaredRowCount,
                        Fields = fields
                    });
                }

                tables.Reverse();
                reshaped.Add(new CustomerIntakeSource
                {
                    SourceCode = source.SourceCode,
                    SourceKind = source.SourceKind,
                    IsManualRecord = source.IsManualRecord,
                    EarliestObservationUtc = source.EarliestObservationUtc,
                    LatestObservationUtc = source.LatestObservationUtc,
                    Tables = tables
                });
            }

            var entities = new List<CustomerIntakeEntity>(v1.Entities);
            entities.Reverse();

            var declarations = new List<CustomerIntakeDeclaration>(v1.Declarations);
            declarations.Reverse();

            return new CustomerIntake
            {
                LineageCode = "  " + v1.LineageCode + "  ",
                DisplayName = "a different display name typed by a different user",
                Sources = reshaped,
                Entities = entities,
                Declarations = declarations
            };
        }

        private static CustomerIntakeField Field(
            string fieldCode,
            string declaredType,
            string role,
            string? unit = null,
            string? aggregation = null,
            string? timeSemantics = null,
            long? distinctCount = null,
            double? nullFraction = null)
        {
            return new CustomerIntakeField
            {
                FieldCode = fieldCode,
                DeclaredType = declaredType,
                Role = role,
                UnitCode = unit,
                AggregationSemantics = aggregation,
                TimeSemantics = timeSemantics,
                DistinctCount = distinctCount,
                NullFraction = nullFraction,
                IsNullableDeclared = nullFraction.HasValue ? nullFraction.Value > 0.0 : (bool?)null
            };
        }

        private static CustomerIntakeDeclaration Declaration(string code, string? value)
        {
            return new CustomerIntakeDeclaration
            {
                DeclarationCode = code,
                Value = value
            };
        }
    }
}
