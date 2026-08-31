// ============================================================================
// Canonical intake normalisation and semantic fingerprint.
//
// The fingerprint answers exactly one question: has the assessment truth
// changed? It must therefore be stable under everything that is not assessment
// truth, and sensitive to everything that is.
//
// INCLUDED
//   lineage code, every source / table / field the customer declared, every
//   declared field property that can alter a conclusion, declared entity
//   structure, every declaration code and its answer, the contract version and
//   the rule version.
//
// EXCLUDED
//   request timestamp, request identifier, generated identifiers, the display
//   name a user typed, and the order in which collections happened to arrive.
//
// Declared observation windows ARE included. They are customer data that
// changes the historical coverage conclusion; they are not request metadata.
//
// NORMALISATION RULE
//   Trim only. A change of case inside a field code is a real structural
//   change and must produce a new assessment version. Reserved declaration
//   answers are compared case-insensitively because they are product
//   vocabulary, not customer identifiers.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PlantProcess.Application.CustomerAssessment
{
    public static class CustomerAssessmentNormalization
    {
        private const string FieldSeparator = "\u001f";
        private const string RecordSeparator = "\u001e";

        public static string TrimOnly(string? value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        /// <summary>
        /// Reserved product answers are matched case-insensitively; customer
        /// content is returned trimmed and otherwise untouched.
        /// </summary>
        public static string? NormaliseDeclarationAnswer(string? value)
        {
            if (value == null)
            {
                return null;
            }

            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            string lowered = trimmed.ToLowerInvariant();
            if (AssessmentDeclarationAnswers.EstablishesAbsence(lowered)
                || AssessmentDeclarationAnswers.EstablishesNotApplicable(lowered))
            {
                return lowered;
            }

            return trimmed;
        }

        /// <summary>
        /// Deterministic canonical text for one intake. Ordering is imposed, so
        /// the same intake delivered in a different collection order produces
        /// byte-identical text.
        /// </summary>
        public static string CanonicalText(CustomerIntake intake)
        {
            if (intake == null)
            {
                throw new ArgumentNullException(nameof(intake));
            }

            var builder = new StringBuilder();

            builder.Append("lineage").Append(FieldSeparator)
                   .Append(TrimOnly(intake.LineageCode)).Append(RecordSeparator);

            IEnumerable<CustomerIntakeSource> sources =
                (intake.Sources ?? Array.Empty<CustomerIntakeSource>())
                .OrderBy(s => TrimOnly(s.SourceCode), StringComparer.Ordinal);

            foreach (CustomerIntakeSource source in sources)
            {
                builder.Append("source").Append(FieldSeparator)
                       .Append(TrimOnly(source.SourceCode)).Append(FieldSeparator)
                       .Append(TrimOnly(source.SourceKind)).Append(FieldSeparator)
                       .Append(Nullable(source.IsManualRecord)).Append(FieldSeparator)
                       .Append(Nullable(source.EarliestObservationUtc)).Append(FieldSeparator)
                       .Append(Nullable(source.LatestObservationUtc)).Append(RecordSeparator);

                IEnumerable<CustomerIntakeTable> tables =
                    (source.Tables ?? Array.Empty<CustomerIntakeTable>())
                    .OrderBy(t => TrimOnly(t.TableCode), StringComparer.Ordinal);

                foreach (CustomerIntakeTable table in tables)
                {
                    builder.Append("table").Append(FieldSeparator)
                           .Append(TrimOnly(source.SourceCode)).Append(FieldSeparator)
                           .Append(TrimOnly(table.TableCode)).Append(FieldSeparator)
                           .Append(Nullable(table.DeclaredRowCount)).Append(RecordSeparator);

                    IEnumerable<CustomerIntakeField> fields =
                        (table.Fields ?? Array.Empty<CustomerIntakeField>())
                        .OrderBy(f => TrimOnly(f.FieldCode), StringComparer.Ordinal);

                    foreach (CustomerIntakeField field in fields)
                    {
                        builder.Append("field").Append(FieldSeparator)
                               .Append(TrimOnly(source.SourceCode)).Append(FieldSeparator)
                               .Append(TrimOnly(table.TableCode)).Append(FieldSeparator)
                               .Append(TrimOnly(field.FieldCode)).Append(FieldSeparator)
                               .Append(TrimOnly(field.DeclaredType)).Append(FieldSeparator)
                               .Append(TrimOnly(field.Role)).Append(FieldSeparator)
                               .Append(TrimOnly(field.UnitCode)).Append(FieldSeparator)
                               .Append(TrimOnly(field.TimeSemantics)).Append(FieldSeparator)
                               .Append(TrimOnly(field.AggregationSemantics)).Append(FieldSeparator)
                               .Append(Nullable(field.IsNullableDeclared)).Append(FieldSeparator)
                               .Append(Nullable(field.NullFraction)).Append(FieldSeparator)
                               .Append(Nullable(field.DistinctCount)).Append(RecordSeparator);
                    }
                }
            }

            IEnumerable<CustomerIntakeEntity> entities =
                (intake.Entities ?? Array.Empty<CustomerIntakeEntity>())
                .OrderBy(e => TrimOnly(e.EntityCode), StringComparer.Ordinal);

            foreach (CustomerIntakeEntity entity in entities)
            {
                builder.Append("entity").Append(FieldSeparator)
                       .Append(TrimOnly(entity.EntityCode)).Append(FieldSeparator)
                       .Append(TrimOnly(entity.ParentEntityCode)).Append(FieldSeparator)
                       .Append(TrimOnly(entity.IdentityFieldRef)).Append(RecordSeparator);
            }

            IEnumerable<CustomerIntakeDeclaration> declarations =
                (intake.Declarations ?? Array.Empty<CustomerIntakeDeclaration>())
                .OrderBy(d => TrimOnly(d.DeclarationCode), StringComparer.Ordinal)
                .ThenBy(d => NormaliseDeclarationAnswer(d.Value) ?? string.Empty, StringComparer.Ordinal);

            foreach (CustomerIntakeDeclaration declaration in declarations)
            {
                builder.Append("declaration").Append(FieldSeparator)
                       .Append(TrimOnly(declaration.DeclarationCode)).Append(FieldSeparator)
                       .Append(NormaliseDeclarationAnswer(declaration.Value) ?? "\u0000")
                       .Append(RecordSeparator);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Lowercase hexadecimal SHA-256 of the canonical intake text bound to
        /// both semantic versions. Sixty-four characters, matching the
        /// char(64) column in 833.
        /// </summary>
        public static string ComputeFingerprint(
            CustomerIntake intake,
            string contractVersion,
            string ruleVersion)
        {
            if (intake == null)
            {
                throw new ArgumentNullException(nameof(intake));
            }

            var builder = new StringBuilder();
            builder.Append("contract").Append(FieldSeparator)
                   .Append(TrimOnly(contractVersion)).Append(RecordSeparator);
            builder.Append("rule").Append(FieldSeparator)
                   .Append(TrimOnly(ruleVersion)).Append(RecordSeparator);
            builder.Append(CanonicalText(intake));

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));

            var hex = new StringBuilder(64);
            for (int i = 0; i < hash.Length; i++)
            {
                hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        private static string Nullable(bool? value)
        {
            return value.HasValue
                ? (value.Value ? "true" : "false")
                : "\u0000";
        }

        private static string Nullable(long? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "\u0000";
        }

        private static string Nullable(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("R", CultureInfo.InvariantCulture)
                : "\u0000";
        }

        private static string Nullable(DateTimeOffset? value)
        {
            return value.HasValue
                ? value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : "\u0000";
        }
    }
}
