using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Results;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.IntegrationTests.Quarantine;

/// <summary>
/// PPIQ T-099 live gate L, in its own disposable child.
///
/// ORDERING IS THE CONTRACT. The batch path resolves ResolveTenantAsync(null),
/// which refuses when two tenants are active - a T-090 safety behaviour, not a
/// test inconvenience. So tenant A's quarantine evidence is produced while A is
/// the only active tenant, and B is activated only afterwards. Inserting B first
/// would make every execution in this fixture refuse admission, and the isolation
/// assertions would then prove nothing about isolation.
/// </summary>
[Collection(ProjectionQuarantineLiveCollection.Name)]
public class ProjectionQuarantineTenantIsolationTests : IAsyncLifetime
{
    private const string MappingJson =
        "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}";

    private QuarantineWorld _world = null!;
    private Guid _quarantineId;
    private Guid _stagingId;
    private Guid _mappingId;
    private Guid _batchId;

    public async Task InitializeAsync()
    {
        _world = await QuarantineWorld.CreateAsync("tenant");

        Assert.Equal(1, await _world.ActiveTenantCountAsync());

        var created = await _world.NewMappingAsync("T099TEN", "ten_rows", "MaterialUnit", MappingJson);
        _mappingId = created.MappingId;
        _batchId = created.BatchId;

        await _world.AddRowsAsync(_batchId, "ten_rows",
            "{\"code\":\"\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        await using (var db = _world.NewContext())
        {
            var service = _world.NewExecutionService(db);
            var result = await service.ExecuteAsync(_mappingId, _batchId, 50, false, CancellationToken.None);
            Assert.False(result.IsFailure);
            Assert.Equal(1, result.Value!.QuarantinedRows);
        }

        await using (var read = _world.NewContext())
        {
            var record = await read.ProjectionQuarantineRecords.SingleAsync(x => x.MappingDefinitionId == _mappingId);
            _quarantineId = record.Id;
            _stagingId = record.StagingRecordId;
        }

        // Only now does a second lawful tenant exist.
        await _world.EnsureTenantAsync(QuarantineWorld.TenantBCode, "T-099 tenant B");
        Assert.Equal(2, await _world.ActiveTenantCountAsync());
    }

    public Task DisposeAsync() => _world.DisposeAsync().AsTask();

    private sealed record IsolationSnapshot(
        string State,
        int AttemptCount,
        DateTime LastAttemptAtUtc,
        DateTime? ResolvedAtUtc,
        Guid? ResolvedCanonicalId,
        string StagingStatus,
        Guid? StagingCanonicalId,
        long Materials,
        long QuarantineRows);

    private async Task<IsolationSnapshot> SnapshotAsync()
    {
        await using var db = _world.NewContext();
        var record = await db.ProjectionQuarantineRecords.SingleAsync(x => x.Id == _quarantineId);
        var staged = await db.StagingRecords.SingleAsync(x => x.Id == _stagingId);

        return new IsolationSnapshot(
            record.State,
            record.AttemptCount,
            record.LastAttemptAtUtc,
            record.ResolvedAtUtc,
            record.ResolvedCanonicalId,
            staged.ProcessingStatus,
            staged.CanonicalEntityId,
            await _world.ScalarAsync("SELECT count(*) FROM ppiq_plant.material_units;"),
            await _world.ScalarAsync("SELECT count(*) FROM ppiq_staging.projection_quarantine;"));
    }

    // --------------------------------------------------------------- Gate L1
    [Fact]
    public async Task Gate_L1_tenant_A_may_acquire_and_reprocess_its_own_record_by_explicit_code()
    {
        await using var db = _world.NewContext();
        var service = _world.NewReprocessService(db);
        var result = await service.ReprocessAsync(_quarantineId, QuarantineWorld.TenantACode, CancellationToken.None);

        // Identity resolved, record acquired, operation permitted. The row is
        // still invalid, so it stays Open - access is what this gate proves.
        Assert.False(result.IsFailure);
        Assert.Equal(_quarantineId, result.Value!.QuarantineId);
        Assert.Equal(ProjectionQuarantineRecord.StateOpen, result.Value!.State);
        Assert.Equal(2, result.Value!.AttemptCount);
    }

    // --------------------------------------------------------------- Gate L2
    [Fact]
    public async Task Gate_L2_tenant_B_receives_a_non_disclosing_not_found_for_tenant_A_s_record()
    {
        await using var db = _world.NewContext();
        var service = _world.NewReprocessService(db);
        var result = await service.ReprocessAsync(_quarantineId, QuarantineWorld.TenantBCode, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);

        // The ruled boundary is NotFound, not Forbidden: a generic failure would
        // not prove the non-disclosure contract.
        Assert.Equal(ApplicationErrorType.NotFound, result.Error!.Type);

        var message = result.Error!.Message ?? string.Empty;

        // Non-disclosure: nothing in the refusal admits the record exists, names
        // the owning tenant, or leaks the row's evidence.
        Assert.DoesNotContain(_quarantineId.ToString(), message);
        Assert.DoesNotContain(QuarantineWorld.TenantACode, message);
        Assert.DoesNotContain("PV03", message);
        Assert.DoesNotContain("COIL", message);
    }

    // --------------------------------------------------------------- Gate L3
    [Fact]
    public async Task Gate_L3_a_denied_caller_changes_nothing_at_all()
    {
        var before = await SnapshotAsync();

        await using (var db = _world.NewContext())
        {
            var service = _world.NewReprocessService(db);
            var result = await service.ReprocessAsync(_quarantineId, QuarantineWorld.TenantBCode, CancellationToken.None);
            Assert.True(result.IsFailure);
        }

        var after = await SnapshotAsync();

        // Authorization precedes mutation. A denied caller does not even get to
        // increment an attempt counter or advance a timestamp.
        Assert.Equal(before, after);
    }

    // --------------------------------------------------------------- Gate L4
    [Fact]
    public async Task Gate_L4_no_code_execution_refuses_admission_while_two_tenants_are_active()
    {
        var created = await _world.NewMappingAsync("T099AMB", "amb_rows", "MaterialUnit", MappingJson);
        await _world.AddRowsAsync(created.BatchId, "amb_rows",
            "{\"code\":\"AMB1\",\"type\":\"COIL\",\"site\":\"T099SITE\"}",
            "{\"code\":\"AMB2\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        var materialsBefore = await _world.ScalarAsync("SELECT count(*) FROM ppiq_plant.material_units;");
        var quarantineBefore = await _world.ScalarAsync("SELECT count(*) FROM ppiq_staging.projection_quarantine;");

        await using (var db = _world.NewContext())
        {
            var service = _world.NewExecutionService(db);
            var execute = await service.ExecuteAsync(created.MappingId, created.BatchId, 50, false, CancellationToken.None);

            // Two active tenants and no tenant code must refuse. That T-090
            // behaviour survived the T-099 refactor.
            Assert.True(execute.IsFailure);
            Assert.Null(execute.Value);

            // The refusal must be the tenant-admission one, not some other
            // execution failure that happens to also return a failure result.
            Assert.Equal(ApplicationErrorType.BusinessRule, execute.Error!.Type);
            Assert.Contains("tenant", execute.Error!.Message, StringComparison.OrdinalIgnoreCase);

            var preview = await service.PreviewAsync(created.MappingId, created.BatchId, 50, CancellationToken.None);
            Assert.True(preview.IsFailure);
            Assert.Equal(ApplicationErrorType.BusinessRule, preview.Error!.Type);
        }

        Assert.Equal(materialsBefore, await _world.ScalarAsync("SELECT count(*) FROM ppiq_plant.material_units;"));
        Assert.Equal(quarantineBefore, await _world.ScalarAsync("SELECT count(*) FROM ppiq_staging.projection_quarantine;"));

        await using var verify = _world.NewContext();
        var untouched = await verify.StagingRecords
            .Where(x => x.ImportBatchId == created.BatchId)
            .ToListAsync();

        Assert.Equal(2, untouched.Count);
        Assert.All(untouched, x => Assert.Equal("Pending", x.ProcessingStatus));
    }
}
