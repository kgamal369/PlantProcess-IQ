using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Materials;

namespace PlantProcess.Infrastructure.IntegrationTests.Quarantine;

/// <summary>
/// PPIQ T-099 live gates E, F, H and I.
///
/// EVERY CODE IS REACHED THROUGH THE PRODUCT. No quarantine row here is
/// inserted by hand: each one is what the real projector decided about a real
/// staged payload against a real database. A taxonomy proven by INSERT proves
/// only that the table accepts strings.
/// </summary>
[Collection(ProjectionQuarantineLiveCollection.Name)]
public class ProjectionQuarantineClassificationTests : IAsyncLifetime
{
    private QuarantineWorld _world = null!;

    public async Task InitializeAsync() => _world = await QuarantineWorld.CreateAsync("classify");

    public Task DisposeAsync() => _world.DisposeAsync().AsTask();

    private async Task<(string Status, string? Code)> RunOneAsync(
        string code, string target, string mappingJson, string rawJson)
    {
        var created = await _world.NewMappingAsync(code, code + "_rows", target, mappingJson);
        await _world.AddRowsAsync(created.BatchId, code + "_rows", rawJson);

        await using var db = _world.NewContext();
        var service = _world.NewExecutionService(db);
        var result = await service.ExecuteAsync(created.MappingId, created.BatchId, 50, false, CancellationToken.None);

        Assert.False(result.IsFailure);
        var row = Assert.Single(result.Value!.Rows);
        return (row.Status, row.ValidationCode);
    }

    // ---------------------------------------------------------------- Gate E
    [Fact]
    public async Task PV01_a_declared_source_field_absent_from_the_payload()
    {
        var outcome = await RunOneAsync(
            "T099PV01", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}",
            "{\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        Assert.Equal("Quarantined", outcome.Status);
        Assert.Equal("PV01", outcome.Code);
    }

    [Fact]
    public async Task PV02_a_supplied_value_that_cannot_become_the_target_type()
    {
        // The formerly silent parser case: this used to become a lawful null.
        var outcome = await RunOneAsync(
            "T099PV02", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\",\"ProductionStartUtc\":\"start\"}",
            "{\"code\":\"PV02M\",\"type\":\"COIL\",\"site\":\"T099SITE\",\"start\":\"not-a-date\"}");

        Assert.Equal("Quarantined", outcome.Status);
        Assert.Equal("PV02", outcome.Code);
    }

    [Fact]
    public async Task PV02_a_malformed_optional_integer_is_no_longer_a_silent_null()
    {
        // THE ORIGINAL DEFECT, PROVEN GONE. OptionalInt used to collapse
        // unparseable supplied input to null, which is indistinguishable from an
        // omitted optional field, so corrupt data entered canonical truth as a
        // lawful default. A supplied malformed value is now PV02.
        var outcome = await RunOneAsync(
            "T099PV02B", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\",\"ProductionStartUtc\":\"start\",\"PlantUtcOffsetMinutes\":\"offset\"}",
            "{\"code\":\"PV02B\",\"type\":\"COIL\",\"site\":\"T099SITE\",\"start\":\"2026-09-12T10:00:00Z\",\"offset\":\"not-an-int\"}");

        Assert.Equal("Quarantined", outcome.Status);
        Assert.Equal("PV02", outcome.Code);
    }

    [Fact]
    public async Task PV03_a_required_field_present_but_empty()
    {
        var outcome = await RunOneAsync(
            "T099PV03", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}",
            "{\"code\":\"\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        Assert.Equal("Quarantined", outcome.Status);
        Assert.Equal("PV03", outcome.Code);
    }

    [Fact]
    public async Task PV04_conflict_with_persisted_canonical_truth()
    {
        var parent = await _world.AddMaterialAsync("PV04P");
        var child = await _world.AddMaterialAsync("PV04C");

        await using (var seed = _world.NewContext())
        {
            seed.GenealogyEdges.Add(new GenealogyEdge(parent.Id, child.Id, "Split", isSynthetic: true));
            await seed.SaveChangesAsync();
        }

        var outcome = await RunOneAsync(
            "T099PV04", "GenealogyEdge",
            "{\"ParentMaterialCode\":\"p\",\"ChildMaterialCode\":\"c\",\"RelationshipType\":\"r\"}",
            "{\"p\":\"PV04P\",\"c\":\"PV04C\",\"r\":\"Split\"}");

        Assert.Equal("Quarantined", outcome.Status);
        Assert.Equal("PV04", outcome.Code);
    }

    [Fact]
    public async Task PV05_two_staged_rows_declare_the_same_governed_business_key()
    {
        var created = await _world.NewMappingAsync(
            "T099PV05", "pv05_rows", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}");

        await _world.AddRowsAsync(created.BatchId, "pv05_rows",
            "{\"code\":\"PV05M\",\"type\":\"COIL\",\"site\":\"T099SITE\"}",
            "{\"code\":\"PV05M\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        await using var db = _world.NewContext();
        var service = _world.NewExecutionService(db);
        var result = await service.ExecuteAsync(created.MappingId, created.BatchId, 50, false, CancellationToken.None);

        Assert.False(result.IsFailure);
        var rows = result.Value!.Rows.OrderBy(x => x.RowNumber).ToList();

        // Detected in memory, before PostgreSQL had any chance to refuse it.
        Assert.Equal("Mapped", rows[0].Status);
        Assert.Equal("Quarantined", rows[1].Status);
        Assert.Equal("PV05", rows[1].ValidationCode);
    }

    [Fact]
    public async Task PV06_a_supplied_governed_reference_cannot_resolve()
    {
        var outcome = await RunOneAsync(
            "T099PV06", "MaterialAlias",
            "{\"MaterialCode\":\"mat\",\"AliasCode\":\"alias\"}",
            "{\"mat\":\"PV06-DOES-NOT-EXIST\",\"alias\":\"PV06A\"}");

        Assert.Equal("Quarantined", outcome.Status);
        Assert.Equal("PV06", outcome.Code);
    }

    [Fact]
    public async Task PV07_a_taxonomy_value_absent_from_the_governed_catalogue()
    {
        var outcome = await RunOneAsync(
            "T099PV07", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}",
            "{\"code\":\"PV07M\",\"type\":\"NOT-IN-CATALOGUE\",\"site\":\"T099SITE\"}");

        Assert.Equal("Quarantined", outcome.Status);
        Assert.Equal("PV07", outcome.Code);
    }

    [Fact]
    public async Task PV08_a_unit_that_is_not_the_governed_parameter_unit()
    {
        await _world.AddMaterialAsync("PV08M");

        var outcome = await RunOneAsync(
            "T099PV08", "ParameterObservation",
            "{\"MaterialCode\":\"mat\",\"ParameterCode\":\"param\",\"ObservedAtUtc\":\"at\",\"UnitOfMeasure\":\"uom\",\"NumericValue\":\"val\"}",
            "{\"mat\":\"PV08M\",\"param\":\"T099TEMP\",\"at\":\"2026-09-12T10:00:00Z\",\"uom\":\"inch\",\"val\":\"900\"}");

        Assert.Equal("Quarantined", outcome.Status);
        Assert.Equal("PV08", outcome.Code);
    }

    // ---------------------------------------------------------------- Gate F
    [Fact]
    public async Task Gate_F_a_refused_row_does_not_end_the_run_and_does_not_fail_it()
    {
        var created = await _world.NewMappingAsync(
            "T099MIX", "mix_rows", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}");

        await _world.AddRowsAsync(created.BatchId, "mix_rows",
            "{\"code\":\"MIX1\",\"type\":\"COIL\",\"site\":\"T099SITE\"}",
            "{\"code\":\"\",\"type\":\"COIL\",\"site\":\"T099SITE\"}",
            "{\"code\":\"MIX3\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        await using var db = _world.NewContext();
        var service = _world.NewExecutionService(db);
        var result = await service.ExecuteAsync(created.MappingId, created.BatchId, 50, false, CancellationToken.None);

        Assert.False(result.IsFailure);
        var value = result.Value!;

        Assert.Equal(3, value.ProcessedRows);
        Assert.Equal(2, value.MappedRows);
        Assert.Equal(1, value.QuarantinedRows);
        Assert.Equal(0, value.FailedRows);

        await using var verify = _world.NewContext();

        // Both lawful siblings survived beside the refusal.
        Assert.True(await verify.MaterialUnits.AnyAsync(x => x.MaterialCode == "MIX1"));
        Assert.True(await verify.MaterialUnits.AnyAsync(x => x.MaterialCode == "MIX3"));

        var open = await verify.ProjectionQuarantineRecords
            .Where(x => x.MappingDefinitionId == created.MappingId)
            .ToListAsync();

        var record = Assert.Single(open);
        Assert.Equal("PV03", record.ValidationCode);
        Assert.Equal(2, record.StagingRowNumber);
        Assert.Null(record.ResolvedCanonicalId);

        // The refused row produced no canonical result of its own.
        var staged = await verify.StagingRecords
            .Where(x => x.ImportBatchId == created.BatchId && x.RowNumber == 2)
            .SingleAsync();
        Assert.Null(staged.CanonicalEntityId);
        Assert.Equal("Failed", staged.ProcessingStatus);
    }

    // ---------------------------------------------------------------- Gate H
    [Fact]
    public async Task Gate_H_stop_on_first_error_stops_after_a_refusal_and_false_continues()
    {
        var stop = await _world.NewMappingAsync(
            "T099STOP", "stop_rows", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}");

        await _world.AddRowsAsync(stop.BatchId, "stop_rows",
            "{\"code\":\"STOP1\",\"type\":\"COIL\",\"site\":\"T099SITE\"}",
            "{\"code\":\"\",\"type\":\"COIL\",\"site\":\"T099SITE\"}",
            "{\"code\":\"STOP3\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        await using (var db = _world.NewContext())
        {
            var service = _world.NewExecutionService(db);
            var result = await service.ExecuteAsync(stop.MappingId, stop.BatchId, 50, true, CancellationToken.None);

            Assert.False(result.IsFailure);
            var value = result.Value!;

            // A quarantined row IS a row-level refusal, so the option means what
            // it says. No system failure is invented to justify stopping.
            Assert.Equal(2, value.ProcessedRows);
            Assert.Equal(1, value.MappedRows);
            Assert.Equal(1, value.QuarantinedRows);
            Assert.Equal(0, value.FailedRows);
        }

        await using var verify = _world.NewContext();
        Assert.False(await verify.MaterialUnits.AnyAsync(x => x.MaterialCode == "STOP3"));

        var third = await verify.StagingRecords
            .Where(x => x.ImportBatchId == stop.BatchId && x.RowNumber == 3)
            .SingleAsync();
        Assert.Equal("Pending", third.ProcessingStatus);
    }

    // ---------------------------------------------------------------- Gate I
    [Fact]
    public async Task Gate_I_preview_classifies_and_persists_nothing()
    {
        var created = await _world.NewMappingAsync(
            "T099PREV", "prev_rows", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}");

        await _world.AddRowsAsync(created.BatchId, "prev_rows",
            "{\"code\":\"\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        var materialsBefore = await _world.ScalarAsync("SELECT count(*) FROM ppiq_plant.material_units;");
        var quarantineBefore = await _world.ScalarAsync("SELECT count(*) FROM ppiq_staging.projection_quarantine;");
        var pendingBefore = await _world.ScalarAsync(
            "SELECT count(*) FROM ppiq_staging.staging_records WHERE processing_status = 'Pending';");

        await using (var db = _world.NewContext())
        {
            var service = _world.NewExecutionService(db);
            var result = await service.PreviewAsync(created.MappingId, created.BatchId, 50, CancellationToken.None);

            Assert.False(result.IsFailure);
            var row = Assert.Single(result.Value!.Rows);
            Assert.Equal("Quarantined", row.Status);
            Assert.Equal("PV03", row.ValidationCode);
            Assert.True(result.Value!.PreviewOnly);
        }

        Assert.Equal(materialsBefore, await _world.ScalarAsync("SELECT count(*) FROM ppiq_plant.material_units;"));
        Assert.Equal(quarantineBefore, await _world.ScalarAsync("SELECT count(*) FROM ppiq_staging.projection_quarantine;"));
        Assert.Equal(pendingBefore, await _world.ScalarAsync(
            "SELECT count(*) FROM ppiq_staging.staging_records WHERE processing_status = 'Pending';"));

        // Counts can stay equal while the previewed row itself was mutated, so
        // the exact row is reloaded and every processing field is asserted.
        await using var verify = _world.NewContext();
        var staged = await verify.StagingRecords
            .SingleAsync(x => x.ImportBatchId == created.BatchId && x.RowNumber == 1);

        Assert.False(staged.IsProcessed);
        Assert.Equal("Pending", staged.ProcessingStatus);
        Assert.Null(staged.ProcessedAtUtc);
        Assert.Null(staged.ProcessingError);
        Assert.Null(staged.CanonicalEntityId);
        Assert.Null(staged.CanonicalEntityName);
    }
}
