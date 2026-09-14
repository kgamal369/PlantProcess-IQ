using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PlantProcess.Application.Integration.Services.Import;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.IntegrationTests.Quarantine;

/// <summary>
/// PPIQ T-099 live gates G, J and K.
///
/// The import consumer is exercised for real. Only IDataQualityService is
/// substituted, and the scan flag is false so it is never called: the collaborator
/// under proof is the batch status decision, not a quality scan.
/// </summary>
[Collection(ProjectionQuarantineLiveCollection.Name)]
public class ProjectionQuarantineReprocessTests : IAsyncLifetime
{
    private const string MappingJson =
        "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}";

    private QuarantineWorld _world = null!;

    public async Task InitializeAsync() => _world = await QuarantineWorld.CreateAsync("reprocess");

    public Task DisposeAsync() => _world.DisposeAsync().AsTask();

    // ---------------------------------------------------------------- Gate G
    [Fact]
    public async Task Gate_G_a_quarantined_row_does_not_fail_the_parent_import_batch()
    {
        var created = await _world.NewMappingAsync("T099IMP", "imp_rows", "MaterialUnit", MappingJson);
        await _world.AddRowsAsync(created.BatchId, "imp_rows",
            "{\"code\":\"IMP1\",\"type\":\"COIL\",\"site\":\"T099SITE\"}",
            "{\"code\":\"\",\"type\":\"COIL\",\"site\":\"T099SITE\"}",
            "{\"code\":\"IMP3\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        await using (var db = _world.NewContext())
        {
            var processor = new ImportBatchQueueProcessorService(
                db,
                _world.NewExecutionService(db),
                Substitute.For<IDataQualityService>(),
                NullLogger<ImportBatchQueueProcessorService>.Instance);

            var result = await processor.ProcessPendingBatchesAsync(5, 500, false, false, CancellationToken.None);
            Assert.False(result.IsFailure);

            var item = Assert.Single(result.Value!.Items.Where(x => x.ImportBatchId == created.BatchId));

            // The count is exposed distinctly, and it is not a failure count.
            Assert.Equal(1, item.QuarantinedRows);
            Assert.Equal(0, item.FailedRows);
            Assert.Equal(2, item.MappedRows);
            Assert.Equal(3, item.ProcessedRows);
        }

        await using var verify = _world.NewContext();
        var batch = await verify.ImportBatches.SingleAsync(x => x.Id == created.BatchId);

        // A completed import carrying typed evidence is a SUCCESSFUL import,
        // and the assertion says so positively rather than merely "not Failed".
        Assert.Equal("Completed", batch.Status);
        Assert.Equal(3, batch.RowCount);
        Assert.Null(batch.ErrorMessage);
    }

    [Fact]
    public async Task Gate_G_a_genuine_definition_failure_still_fails_the_batch_and_creates_no_quarantine()
    {
        // Quarantine must not swallow real failure. mapping_json is jsonb NOT NULL,
        // so malformed text can never reach the executor; the storage layer refuses
        // it first. The lawful definition failure is valid JSON that declares no
        // field at all: admission fails before the row loop and no staged row is
        // ever blamed for it.
        var created = await _world.NewMappingAsync("T099BAD", "bad_rows", "MaterialUnit", "{}");
        await _world.AddRowsAsync(created.BatchId, "bad_rows",
            "{\"code\":\"BAD1\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        await using (var db = _world.NewContext())
        {
            var processor = new ImportBatchQueueProcessorService(
                db,
                _world.NewExecutionService(db),
                Substitute.For<IDataQualityService>(),
                NullLogger<ImportBatchQueueProcessorService>.Instance);

            var result = await processor.ProcessPendingBatchesAsync(5, 500, false, false, CancellationToken.None);
            Assert.False(result.IsFailure);

            var item = Assert.Single(result.Value!.Items.Where(x => x.ImportBatchId == created.BatchId));
            Assert.Equal("Failed", item.Status);
        }

        await using var verify = _world.NewContext();
        var batch = await verify.ImportBatches.SingleAsync(x => x.Id == created.BatchId);
        Assert.Equal("Failed", batch.Status);
        Assert.NotNull(batch.ErrorMessage);

        Assert.False(await verify.ProjectionQuarantineRecords
            .AnyAsync(x => x.MappingDefinitionId == created.MappingId));
    }

    // ------------------------------------------------------------ Gates J, K
    // mapping_code and import_batch_code carry UNIQUE indexes, so every caller
    // owns a distinct code; two tests sharing one would collide on the second run.
    private async Task<(Guid QuarantineId, Guid StagingId, Guid MappingId)> OpenOneAsync(string code)
    {
        var rows = code.ToLowerInvariant() + "_rows";
        var created = await _world.NewMappingAsync(code, rows, "MaterialUnit", MappingJson);
        await _world.AddRowsAsync(created.BatchId, rows,
            "{\"code\":\"\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        await using var db = _world.NewContext();
        var service = _world.NewExecutionService(db);
        var result = await service.ExecuteAsync(created.MappingId, created.BatchId, 50, false, CancellationToken.None);
        Assert.False(result.IsFailure);
        Assert.Equal(1, result.Value!.QuarantinedRows);

        await using var read = _world.NewContext();
        var record = await read.ProjectionQuarantineRecords
            .SingleAsync(x => x.MappingDefinitionId == created.MappingId);

        return (record.Id, record.StagingRecordId, created.MappingId);
    }

    [Fact]
    public async Task Gate_J_a_still_invalid_row_stays_open_counts_the_attempt_and_touches_nothing_else()
    {
        var opened = await OpenOneAsync("T099RPJ");

        // An unrelated pending row in its own batch. Without a sibling, the test
        // cannot tell an exact-row service from one that quietly ran a batch.
        var sibling = await _world.NewMappingAsync("T099SIB", "sib_rows", "MaterialUnit", MappingJson);
        await _world.AddRowsAsync(sibling.BatchId, "sib_rows",
            "{\"code\":\"SIB1\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        DateTime lastAttemptBefore;
        await using (var before = _world.NewContext())
        {
            lastAttemptBefore = (await before.ProjectionQuarantineRecords
                .SingleAsync(x => x.Id == opened.QuarantineId)).LastAttemptAtUtc;
        }

        await using (var db = _world.NewContext())
        {
            var service = _world.NewReprocessService(db);
            var result = await service.ReprocessAsync(opened.QuarantineId, QuarantineWorld.TenantACode, CancellationToken.None);

            Assert.False(result.IsFailure);
            Assert.Equal(ProjectionQuarantineRecord.StateOpen, result.Value!.State);
            Assert.Equal(2, result.Value!.AttemptCount);
            Assert.Null(result.Value!.ResolvedCanonicalId);
        }

        await using var verify = _world.NewContext();
        var record = await verify.ProjectionQuarantineRecords.SingleAsync(x => x.Id == opened.QuarantineId);

        Assert.Equal(ProjectionQuarantineRecord.StateOpen, record.State);
        Assert.Equal(2, record.AttemptCount);
        Assert.True(record.LastAttemptAtUtc > lastAttemptBefore);
        Assert.Null(record.ResolvedAtUtc);
        Assert.Null(record.ResolvedCanonicalId);

        var untouched = await verify.StagingRecords
            .SingleAsync(x => x.ImportBatchId == sibling.BatchId && x.RowNumber == 1);

        Assert.Equal("Pending", untouched.ProcessingStatus);
        Assert.False(untouched.IsProcessed);
        Assert.Null(untouched.ProcessedAtUtc);
        Assert.Null(untouched.CanonicalEntityId);
        Assert.False(await verify.ProjectionQuarantineRecords
            .AnyAsync(x => x.MappingDefinitionId == sibling.MappingId));
    }

    [Fact]
    public async Task Gate_K_a_corrected_row_resolves_once_and_stays_resolved()
    {
        var opened = await OpenOneAsync("T099RPK");

        // Correct only the selected staged input, through the lawful surface.
        await using (var fix = _world.NewContext())
        {
            var staged = await fix.StagingRecords.SingleAsync(x => x.Id == opened.StagingId);
            fix.Entry(staged).Property("RawJson")
                .CurrentValue = "{\"code\":\"RP-FIXED\",\"type\":\"COIL\",\"site\":\"T099SITE\"}";
            staged.ResetProcessing();
            await fix.SaveChangesAsync();
        }

        Guid canonicalId;
        await using (var db = _world.NewContext())
        {
            var service = _world.NewReprocessService(db);
            var result = await service.ReprocessAsync(opened.QuarantineId, QuarantineWorld.TenantACode, CancellationToken.None);

            Assert.False(result.IsFailure);
            Assert.Equal(ProjectionQuarantineRecord.StateResolved, result.Value!.State);
            Assert.NotNull(result.Value!.ResolvedCanonicalId);
            canonicalId = result.Value!.ResolvedCanonicalId!.Value;
        }

        await using (var verify = _world.NewContext())
        {
            var record = await verify.ProjectionQuarantineRecords.SingleAsync(x => x.Id == opened.QuarantineId);
            Assert.Equal(ProjectionQuarantineRecord.StateResolved, record.State);
            Assert.NotNull(record.ResolvedAtUtc);
            Assert.Equal(canonicalId, record.ResolvedCanonicalId);

            var staged = await verify.StagingRecords.SingleAsync(x => x.Id == opened.StagingId);
            Assert.Equal("Mapped", staged.ProcessingStatus);
            Assert.Equal(canonicalId, staged.CanonicalEntityId);

            Assert.True(await verify.MaterialUnits.AnyAsync(x => x.Id == canonicalId));
        }

        var materialsAfterFirst = await _world.ScalarAsync("SELECT count(*) FROM ppiq_plant.material_units;");
        var quarantineAfterFirst = await _world.ScalarAsync("SELECT count(*) FROM ppiq_staging.projection_quarantine;");

        // A repeated call creates no second canonical truth and no second queue entry.
        await using (var again = _world.NewContext())
        {
            var service = _world.NewReprocessService(again);
            var repeat = await service.ReprocessAsync(opened.QuarantineId, QuarantineWorld.TenantACode, CancellationToken.None);
            Assert.True(repeat.IsFailure);
        }

        Assert.Equal(materialsAfterFirst, await _world.ScalarAsync("SELECT count(*) FROM ppiq_plant.material_units;"));
        Assert.Equal(quarantineAfterFirst, await _world.ScalarAsync("SELECT count(*) FROM ppiq_staging.projection_quarantine;"));
    }
}
