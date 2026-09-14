using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Domain.Common;
using PlantProcess.Domain.Entities.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Integration;

/// <summary>
/// PPIQ T-099. The taxonomy, the typed read and the evidence record, proved
/// without a database.
///
/// The completeness ratchet exists because a member that is declared but never
/// implemented lets the taxonomy look finished while a class classifies nothing.
/// So the first test is literally that the enum is exactly PV01..PV08.
/// </summary>
public class ProjectionQuarantineContractTests
{
    [Fact]
    public void Taxonomy_is_exactly_PV01_to_PV08_in_T099()
    {
        var names = Enum.GetNames(typeof(ProjectionValidationCode)).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.Equal(
            new[] { "PV01", "PV02", "PV03", "PV04", "PV05", "PV06", "PV07", "PV08" },
            names);
    }

    [Fact]
    public void Taxonomy_does_not_declare_PV09_before_T100()
    {
        Assert.False(Enum.IsDefined(typeof(ProjectionValidationCode), 9));
        Assert.DoesNotContain("PV09", Enum.GetNames(typeof(ProjectionValidationCode)));
    }

    [Fact]
    public void Every_declared_code_has_a_deterministic_correction()
    {
        foreach (ProjectionValidationCode code in Enum.GetValues(typeof(ProjectionValidationCode)))
        {
            var correction = ProjectionValidationCorrection.For(code);
            Assert.False(string.IsNullOrWhiteSpace(correction));
        }
    }

    [Fact]
    public void An_undeclared_code_has_no_correction_and_is_not_invented()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProjectionValidationCorrection.For((ProjectionValidationCode)9));
    }

    [Fact]
    public void Refusal_derives_its_correction_from_the_code()
    {
        var refusal = new RowValidationRefusal(ProjectionValidationCode.PV02, "bad number", "12x");

        Assert.Equal(ProjectionValidationCorrection.For(ProjectionValidationCode.PV02), refusal.SuggestedCorrection);
        Assert.Equal("12x", refusal.OffendingValue);
    }

    // ------------------------------------------------------------------
    // FIELD READ. Absent, empty and unparseable are three answers, not one.
    // ------------------------------------------------------------------
    [Fact]
    public void NotMapped_is_distinct_from_MappedButSourceAbsent()
    {
        var notMapped = FieldRead<string>.NotMapped();
        var absent = FieldRead<string>.Absent();

        Assert.Equal(FieldReadState.NotMapped, notMapped.State);
        Assert.False(notMapped.IsDeclared);

        Assert.Equal(FieldReadState.MappedButSourceAbsent, absent.State);
        Assert.True(absent.IsDeclared);
        Assert.False(absent.IsPresent);
    }

    [Fact]
    public void PresentEmpty_is_present_but_empty()
    {
        var empty = FieldRead<string>.Empty();

        Assert.True(empty.IsPresent);
        Assert.True(empty.IsEmpty);
        Assert.False(empty.IsRefused);
    }

    [Fact]
    public void A_refused_read_carries_the_code_and_never_a_value()
    {
        var read = FieldRead<int>.Refused(new RowValidationRefusal(ProjectionValidationCode.PV02, "not an integer", "abc"));

        Assert.True(read.IsRefused);
        Assert.Equal(ProjectionValidationCode.PV02, read.Refusal!.Code);
        Assert.Equal(default, read.Value);
    }

    // ------------------------------------------------------------------
    // RESULT MODEL. Quarantine is not failure.
    // ------------------------------------------------------------------
    [Fact]
    public void Quarantined_rows_are_counted_separately_from_failed_rows()
    {
        var result = new MappingExecutionResult(
            Guid.NewGuid(), Guid.NewGuid(), "MAP-1", "MaterialUnit",
            PreviewOnly: false,
            RequestedRows: 500,
            ProcessedRows: 3,
            MappedRows: 2,
            SkippedRows: 0,
            FailedRows: 0,
            QuarantinedRows: 1,
            Rows: Array.Empty<MappingExecutionRowResult>());

        Assert.Equal(1, result.QuarantinedRows);
        Assert.Equal(0, result.FailedRows);
    }

    [Fact]
    public void A_quarantined_row_result_carries_the_code_not_only_a_sentence()
    {
        var row = new MappingExecutionRowResult(
            Guid.NewGuid(), 2, "Quarantined", null, null, "value cannot be read as DateTime",
            ValidationCode: "PV02", OffendingValue: "31/31/2026", SuggestedCorrection: "Correct the value or add an explicit conversion.");

        Assert.Equal("Quarantined", row.Status);
        Assert.Equal("PV02", row.ValidationCode);
    }

    // ------------------------------------------------------------------
    // EVIDENCE RECORD.
    // ------------------------------------------------------------------
    private static ProjectionQuarantineRecord NewRecord(ProjectionValidationCode code = ProjectionValidationCode.PV01) =>
        new(
            validationCode: code,
            detail: "detail",
            offendingValue: "value",
            importBatchId: Guid.NewGuid(),
            stagingRecordId: Guid.NewGuid(),
            stagingRowNumber: 2,
            sourceObjectName: "orders",
            mappingDefinitionId: Guid.NewGuid(),
            mappingVersion: "v1",
            targetEntityName: "MaterialUnit",
            tenantId: Guid.NewGuid(),
            isSynthetic: false);

    [Fact]
    public void A_new_record_opens_at_attempt_one_with_a_truthful_last_attempt()
    {
        var record = NewRecord();

        Assert.Equal(ProjectionQuarantineRecord.StateOpen, record.State);
        Assert.Equal(1, record.AttemptCount);
        Assert.NotEqual(default, record.LastAttemptAtUtc);
        Assert.Null(record.ResolvedAtUtc);
        Assert.Null(record.ResolvedCanonicalId);
    }

    [Fact]
    public void Tenant_identity_is_never_invented()
    {
        Assert.Throws<ArgumentException>(() => new ProjectionQuarantineRecord(
            ProjectionValidationCode.PV01, "detail", null,
            Guid.NewGuid(), Guid.NewGuid(), 1, "orders",
            Guid.NewGuid(), "v1", "MaterialUnit",
            tenantId: Guid.Empty,
            isSynthetic: false));
    }

    [Fact]
    public void A_row_number_must_be_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectionQuarantineRecord(
            ProjectionValidationCode.PV01, "detail", null,
            Guid.NewGuid(), Guid.NewGuid(), 0, "orders",
            Guid.NewGuid(), "v1", "MaterialUnit", Guid.NewGuid(), false));
    }

    [Fact]
    public void A_failed_attempt_stays_open_counts_up_and_refreshes_the_evidence()
    {
        var record = NewRecord(ProjectionValidationCode.PV01);

        record.RecordFailedAttempt(ProjectionValidationCode.PV03, "still empty", "");

        Assert.Equal(ProjectionQuarantineRecord.StateOpen, record.State);
        Assert.Equal(2, record.AttemptCount);
        Assert.Equal("PV03", record.ValidationCode);
        Assert.Equal("still empty", record.Detail);
        Assert.Null(record.ResolvedCanonicalId);
    }

    [Fact]
    public void Resolved_requires_the_canonical_identity_that_resolved_it()
    {
        var record = NewRecord();

        Assert.Throws<ArgumentException>(() => record.MarkResolved(Guid.Empty));
        Assert.Equal(ProjectionQuarantineRecord.StateOpen, record.State);
    }

    [Fact]
    public void Resolving_sets_both_the_time_and_the_canonical_identity()
    {
        var record = NewRecord();
        var canonicalId = Guid.NewGuid();

        record.MarkResolved(canonicalId);

        Assert.Equal(ProjectionQuarantineRecord.StateResolved, record.State);
        Assert.Equal(canonicalId, record.ResolvedCanonicalId);
        Assert.NotNull(record.ResolvedAtUtc);
    }

    [Fact]
    public void A_resolved_record_cannot_record_another_failure()
    {
        var record = NewRecord();
        record.MarkResolved(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            record.RecordFailedAttempt(ProjectionValidationCode.PV02, "late", null));
    }

    // ------------------------------------------------------------------
    // EXECUTION CONTEXT. PV05 is an in-execution collision, nothing else.
    // ------------------------------------------------------------------
    [Fact]
    public void The_first_row_claims_a_business_key_and_the_second_collides()
    {
        var context = new MappingExecutionContext(Guid.NewGuid());

        Assert.Null(context.ClaimBusinessKey("MaterialUnit|site|A1", 1));
        Assert.Equal(1, context.ClaimBusinessKey("MaterialUnit|site|A1", 2));
        Assert.Null(context.ClaimBusinessKey("MaterialUnit|site|A2", 3));
    }

    [Fact]
    public void An_execution_context_refuses_an_invented_tenant()
    {
        Assert.Throws<ArgumentException>(() => new MappingExecutionContext(Guid.Empty));
    }
}
