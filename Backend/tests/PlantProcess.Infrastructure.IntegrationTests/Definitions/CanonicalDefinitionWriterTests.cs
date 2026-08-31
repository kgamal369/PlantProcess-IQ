using System.Data;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Definitions;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-090 Layer A and B execution gates.
///
/// Every assertion here expects success OR refusal explicitly. A guard that
/// silently does nothing must fail this run rather than pass it - the T-089
/// behaviour proof was built the same way, and it is the reason a trigger that
/// quietly declined to fire could not have shipped.
///
/// State assertions read the database. Asserting that a call threw proves the
/// caller was told; only counting rows proves nothing was written.
/// </summary>
[Collection("CanonicalDefinitionStore")]
public sealed class CanonicalDefinitionWriterTests : IAsyncLifetime
{
    private readonly DefinitionStoreFixture _fixture;

    public CanonicalDefinitionWriterTests(DefinitionStoreFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ------------------------------------------------------- AMBIENT_TX_REQUIRED

    [Fact]
    [Trait("Gate", "AMBIENT_TX_REQUIRED")]
    public async Task A_mutation_without_the_callers_transaction_is_refused_and_writes_nothing()
    {
        await using var db = _fixture.NewContext();
        var writer = new CanonicalDefinitionWriter(db);
        var before = await _fixture.CountVersionsAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteVersionAsync(_fixture.SampleWrite("no_tx_write"), CancellationToken.None));

        Assert.Equal(before, await _fixture.CountVersionsAsync());
    }

    [Fact]
    [Trait("Gate", "AMBIENT_TX_REQUIRED")]
    public async Task Publishing_without_the_callers_transaction_is_refused()
    {
        await using var db = _fixture.NewContext();
        var writer = new CanonicalDefinitionWriter(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.PublishAsync(Guid.NewGuid(), 1, CancellationToken.None));
    }

    // ------------------------------------------------------- AMBIENT_TX_ROLLBACK

    /// <summary>
    /// Proves enlistment, not merely checking. The writer succeeds inside the
    /// caller's transaction; rolling that transaction back must remove
    /// everything it wrote, including the parent it created.
    /// </summary>
    [Fact]
    [Trait("Gate", "AMBIENT_TX_ROLLBACK")]
    public async Task Canonical_writes_disappear_when_the_caller_rolls_back()
    {
        await using var db = _fixture.NewContext();
        var writer = new CanonicalDefinitionWriter(db);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var written = await writer.WriteVersionAsync(
                _fixture.SampleWrite("rolled_back_definition"), CancellationToken.None);

            Assert.True(written.IsSuccess, written.Error?.Message);
            await transaction.RollbackAsync();
        }

        Assert.Null(await _fixture.FindDefinitionAsync("rolled_back_definition"));
        Assert.Equal(0, await _fixture.CountVersionsForCodeAsync("rolled_back_definition"));
        Assert.Equal(0, await _fixture.CountDetailRowsAsync("widget_details", "rolled_back_definition"));
    }

    // -------------------------------------------------- CANONICAL_HASH_IDEMPOTENCE

    [Fact]
    [Trait("Gate", "CANONICAL_HASH_IDEMPOTENCE")]
    public async Task The_same_semantics_written_twice_produce_one_version()
    {
        var first = await _fixture.WriteAsync(_fixture.SampleWrite("idempotent_widget"));
        var second = await _fixture.WriteAsync(_fixture.SampleWrite("idempotent_widget"));

        Assert.Equal(first.VersionNumber, second.VersionNumber);
        Assert.Equal(1, await _fixture.CountVersionsForCodeAsync("idempotent_widget"));
    }

    /// <summary>
    /// Whitespace, key order and numeric formatting are request syntax, not
    /// semantics. Two payloads that differ only in those ways describe one
    /// definition and must not fork its history.
    /// </summary>
    [Fact]
    [Trait("Gate", "CANONICAL_HASH_IDEMPOTENCE")]
    public async Task Differently_formatted_but_equal_payloads_produce_one_version()
    {
        var compact = _fixture.SampleWrite("formatting_widget") with
        {
            ContentJson = "{\"b\":2,\"a\":1}"
        };
        var spaced = _fixture.SampleWrite("formatting_widget") with
        {
            ContentJson = "{\n  \"a\" : 1.0,\n  \"b\" : 2\n}"
        };

        var first = await _fixture.WriteAsync(compact);
        var second = await _fixture.WriteAsync(spaced);

        Assert.Equal(first.VersionNumber, second.VersionNumber);
        Assert.Equal(1, await _fixture.CountVersionsForCodeAsync("formatting_widget"));
    }

    [Fact]
    [Trait("Gate", "CANONICAL_HASH_IDEMPOTENCE")]
    public async Task Changed_semantics_produce_the_next_version()
    {
        var first = await _fixture.WriteAsync(_fixture.SampleWrite("changing_widget"));

        var changed = _fixture.SampleWrite("changing_widget") with
        {
            Detail = new Dictionary<string, object?> { ["measure_code"] = "defectCount" }
        };
        var second = await _fixture.WriteAsync(changed);

        Assert.Equal(first.VersionNumber + 1, second.VersionNumber);
    }

    // --------------------------------------------------------- CANONICAL_IDENTITY

    [Fact]
    [Trait("Gate", "CANONICAL_IDENTITY")]
    public async Task A_write_without_a_resolved_tenant_is_refused()
    {
        var result = await _fixture.TryWriteAsync(
            _fixture.SampleWrite("no_tenant") with { TenantId = Guid.Empty });

        Assert.True(result.IsFailure);
        Assert.Contains("tenant", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await _fixture.FindDefinitionAsync("no_tenant"));
    }

    [Fact]
    [Trait("Gate", "CANONICAL_IDENTITY")]
    public async Task A_write_without_a_resolved_owner_is_refused()
    {
        var result = await _fixture.TryWriteAsync(
            _fixture.SampleWrite("no_owner") with { OwnerId = Guid.Empty });

        Assert.True(result.IsFailure);
        Assert.Contains("owner", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kind and surface are identity-level. Reusing an established code under a
    /// different kind must refuse rather than quietly rewrite what that
    /// definition is.
    /// </summary>
    [Fact]
    [Trait("Gate", "KIND_SURFACE_CONFLICT")]
    public async Task An_established_code_cannot_change_kind_or_surface()
    {
        await _fixture.WriteAsync(_fixture.SampleWrite("settled_identity"));

        var asModel = _fixture.SampleWrite("settled_identity") with
        {
            Kind = DefinitionKind.Model,
            Detail = new Dictionary<string, object?> { ["algorithm_code"] = "gbm" }
        };
        var result = await _fixture.TryWriteAsync(asModel);

        Assert.True(result.IsFailure);
        Assert.Contains("already established", result.Error!.Message, StringComparison.Ordinal);
    }

    // --------------------------------------------------------- DETAIL VALIDATION

    [Fact]
    [Trait("Gate", "UNKNOWN_DETAIL_REFUSED")]
    public async Task An_undeclared_detail_field_is_refused_rather_than_dropped()
    {
        var typo = _fixture.SampleWrite("typo_widget") with
        {
            Detail = new Dictionary<string, object?>
            {
                ["measure_code"] = "defectRate",
                ["dimenson_code"] = "day"
            }
        };

        var result = await _fixture.TryWriteAsync(typo);

        Assert.True(result.IsFailure);
        Assert.Contains("dimenson_code", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(await _fixture.FindDefinitionAsync("typo_widget"));
    }

    [Fact]
    [Trait("Gate", "MALFORMED_JSON_REFUSED")]
    public async Task A_json_field_carrying_invalid_json_is_refused_before_mutation()
    {
        var malformed = _fixture.SampleWrite("malformed_widget") with
        {
            Detail = new Dictionary<string, object?> { ["column_roles"] = "{not json" }
        };

        var before = await _fixture.CountVersionsAsync();
        var result = await _fixture.TryWriteAsync(malformed);

        Assert.True(result.IsFailure);
        Assert.Contains("valid JSON", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, await _fixture.CountVersionsAsync());
        Assert.Null(await _fixture.FindDefinitionAsync("malformed_widget"));
    }

    // ------------------------------------------------------ G22_PUBLISHED_RESOLUTION

    /// <summary>
    /// A newer draft raises current_version without becoming truth. Published
    /// resolution must read status, so V1 keeps serving until V2 is published.
    /// </summary>
    [Fact]
    [Trait("Gate", "G22_PUBLISHED_RESOLUTION")]
    public async Task A_newer_draft_does_not_become_the_published_version()
    {
        var v1 = await _fixture.WriteAsync(
            _fixture.SampleWrite("g22_widget") with { Status = CanonicalVersionStatus.Published });

        var v2 = await _fixture.WriteAsync(
            _fixture.SampleWrite("g22_widget") with
            {
                Status = CanonicalVersionStatus.Draft,
                Detail = new Dictionary<string, object?> { ["measure_code"] = "riskScore" }
            });

        Assert.Equal(v1.VersionNumber + 1, v2.VersionNumber);

        var current = await _fixture.ResolvePublishedAsync(v1.DefinitionId);
        Assert.Equal(v1.VersionNumber, current.VersionNumber);

        await _fixture.PublishAsync(v1.DefinitionId, v2.VersionNumber);

        var afterPublish = await _fixture.ResolvePublishedAsync(v1.DefinitionId);
        Assert.Equal(v2.VersionNumber, afterPublish.VersionNumber);
    }

    /// <summary>
    /// V1 must still resolve exactly as written after V2 exists. That is what
    /// makes it a version rather than a backup.
    /// </summary>
    [Fact]
    [Trait("Gate", "OLD_VERSION_REPLAY")]
    public async Task An_earlier_version_still_resolves_unchanged_after_a_later_one()
    {
        var v1 = await _fixture.WriteAsync(
            _fixture.SampleWrite("replay_widget") with { Status = CanonicalVersionStatus.Published });
        var originalContent = v1.ContentJson;

        await _fixture.WriteAsync(_fixture.SampleWrite("replay_widget") with
        {
            Detail = new Dictionary<string, object?> { ["measure_code"] = "materialCount" }
        });

        var replayed = await _fixture.ResolveExactAsync(v1.DefinitionId, v1.VersionNumber);
        Assert.Equal(originalContent, replayed.ContentJson);
        Assert.Equal(v1.DefinitionHash, replayed.DefinitionHash);
    }

    // ------------------------------------------------------------ SM06_PUBLISH_GUARD

    [Fact]
    [Trait("Gate", "SM06_PUBLISH_GUARD")]
    public async Task An_outcome_carrying_the_migration_sentinel_cannot_be_published()
    {
        var v1 = await _fixture.WriteAsync(_fixture.CompleteOutcomeWrite("sm06_contract"));
        await _fixture.PublishAsync(v1.DefinitionId, v1.VersionNumber);

        var incomplete = await _fixture.WriteAsync(
            _fixture.SentinelOutcomeWrite("sm06_contract"));

        var refused = await _fixture.TryPublishAsync(incomplete.DefinitionId, incomplete.VersionNumber);

        Assert.True(refused.IsFailure);
        Assert.Contains("detection_position_code", refused.Error!.Message, StringComparison.Ordinal);

        var stillServing = await _fixture.ResolvePublishedAsync(v1.DefinitionId);
        Assert.Equal(v1.VersionNumber, stillServing.VersionNumber);
    }

    /// <summary>
    /// Negative control for the sentinel rule. A longer opaque identifier that
    /// merely contains the sentinel text is a legitimate value and must not
    /// trigger the unknown-sentinel refusal. It still has to satisfy every
    /// other rule; this proves only that the substring alone does not condemn it.
    /// </summary>
    [Fact]
    [Trait("Gate", "SM06_PUBLISH_GUARD")]
    public async Task A_longer_value_containing_the_sentinel_text_is_not_refused_for_that_reason()
    {
        var written = await _fixture.WriteAsync(
            _fixture.CompleteOutcomeWrite("sm06_opaque", detectionPosition: "legacy_migrated_unknown_mapping_v2"));

        var published = await _fixture.TryPublishAsync(written.DefinitionId, written.VersionNumber);

        Assert.True(published.IsSuccess, published.Error?.Message);
    }

    [Fact]
    [Trait("Gate", "SM06_MULTI_OUTCOME")]
    public async Task One_transformation_version_carries_several_outcomes()
    {
        var written = await _fixture.WriteAsync(_fixture.TwoOutcomeWrite("sm06_multi"));

        Assert.Equal(2, await _fixture.CountOutcomeRowsAsync(written.VersionId));
    }

    [Fact]
    [Trait("Gate", "SM06_PARENT_GUARD")]
    public async Task Outcome_semantics_are_refused_on_a_non_transformation_parent()
    {
        var result = await _fixture.TryWriteAsync(
            _fixture.TwoOutcomeWrite("sm06_wrong_parent") with { Kind = DefinitionKind.Widget });

        Assert.True(result.IsFailure);
        Assert.Contains("S1 transformation", result.Error!.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- IMMUTABILITY

    /// <summary>
    /// The refusal must come from the database, not from this application. An
    /// authority that depends on every caller behaving is not an authority, so
    /// this mutates the row directly and expects PostgreSQL to raise 23514.
    /// </summary>
    [Fact]
    [Trait("Gate", "PUBLISHED_IMMUTABILITY")]
    public async Task A_published_version_refuses_a_direct_content_change()
    {
        var v1 = await _fixture.WriteAsync(
            _fixture.SampleWrite("immutable_widget") with { Status = CanonicalVersionStatus.Published });

        var error = await _fixture.ExpectSqlFailureAsync(
            "UPDATE ppiq_meta.definition_versions SET graph_json = '{\"tampered\":true}'::jsonb WHERE id = @id;",
            v1.VersionId);

        Assert.Contains("23514", error, StringComparison.Ordinal);
    }

    // ---------------------------------------------------- PARENT_FIRST_CREATE_RACE

    /// <summary>
    /// Two independent contexts, independent transactions, one code that does
    /// not exist yet. A row lock cannot serialise this - the unique constraint
    /// decides the winner and both must converge on the surviving parent.
    /// </summary>
    [Fact]
    [Trait("Gate", "PARENT_FIRST_CREATE_RACE")]
    public async Task Two_first_writers_of_one_code_produce_exactly_one_parent()
    {
        using var barrier = new Barrier(2);

        async Task RaceAsync(string measure)
        {
            await using var db = _fixture.NewContext();
            var writer = new CanonicalDefinitionWriter(db);
            barrier.SignalAndWait();

            await using var transaction = await db.Database.BeginTransactionAsync();
            await writer.WriteVersionAsync(
                _fixture.SampleWrite("raced_code") with
                {
                    Detail = new Dictionary<string, object?> { ["measure_code"] = measure }
                },
                CancellationToken.None);
            await transaction.CommitAsync();
        }

        await Task.WhenAll(RaceAsync("defectRate"), RaceAsync("riskScore"));

        Assert.Equal(1, await _fixture.CountDefinitionsForCodeAsync("raced_code"));
        var numbers = await _fixture.VersionNumbersForCodeAsync("raced_code");
        Assert.Equal(numbers.Distinct().Count(), numbers.Count);
        Assert.Equal(Enumerable.Range(1, numbers.Count).ToList(), numbers.OrderBy(n => n).ToList());
    }

    /// <summary>
    /// Control for the lock's scope. Two unrelated definitions must not block
    /// each other, or the implementation has introduced global serialisation
    /// while appearing to pass the race test.
    /// </summary>
    [Fact]
    [Trait("Gate", "VERSION_CONCURRENCY")]
    public async Task Unrelated_definitions_progress_without_serialising_on_each_other()
    {
        async Task WriteAsync(string code)
        {
            await using var db = _fixture.NewContext();
            var writer = new CanonicalDefinitionWriter(db);
            await using var transaction = await db.Database.BeginTransactionAsync();
            await writer.WriteVersionAsync(_fixture.SampleWrite(code), CancellationToken.None);
            await transaction.CommitAsync();
        }

        await Task.WhenAll(WriteAsync("independent_a"), WriteAsync("independent_b"));

        Assert.Equal(1, await _fixture.CountDefinitionsForCodeAsync("independent_a"));
        Assert.Equal(1, await _fixture.CountDefinitionsForCodeAsync("independent_b"));
    }
    // ------------------------------------------------------- T-090 kind gates

    /// <summary>
    /// G1. An S1 transformation definition converges onto the canonical store:
    /// one identity, an immutable version, a typed transformation_details row,
    /// and its SM-06 outcome declaration as child rows of the same version.
    /// </summary>
    [Fact]
    [Trait("Gate", "G1_S1_TRANSFORMATION")]
    public async Task An_s1_transformation_converges_onto_the_canonical_store()
    {
        var written = await _fixture.WriteAsync(_fixture.CompleteOutcomeWrite("g1_transformation"));

        Assert.Equal(1, written.VersionNumber);
        Assert.Equal(1, await _fixture.CountDefinitionsForCodeAsync("g1_transformation"));
        Assert.Equal(1, await _fixture.CountDetailRowsAsync("transformation_details", "g1_transformation"));
        Assert.Equal(1, await _fixture.CountOutcomeRowsAsync(written.VersionId));
    }

    /// <summary>
    /// G3. An S2 widget definition converges: the version resolves back with
    /// its content, and the declared detail fields landed as a typed
    /// widget_details row rather than staying JSON-only.
    /// </summary>
    [Fact]
    [Trait("Gate", "G3_S2_WIDGET")]
    public async Task An_s2_widget_converges_onto_the_canonical_store()
    {
        var written = await _fixture.WriteAsync(_fixture.SampleWrite("g3_widget"));

        var resolved = await _fixture.ResolveExactAsync(written.DefinitionId, written.VersionNumber);
        Assert.Contains("Sample widget", resolved.ContentJson);
        Assert.Equal(1, await _fixture.CountDetailRowsAsync("widget_details", "g3_widget"));
    }

    /// <summary>
    /// G4. An S2 master-item definition has no detail table by declaration, and
    /// that must not cost it versioning: two distinct contents leave two
    /// immutable versions on the one canonical identity.
    /// </summary>
    [Fact]
    [Trait("Gate", "G4_S2_MASTER")]
    public async Task An_s2_master_item_versions_without_a_detail_table()
    {
        var code = "t090test_g4_master_dimension";

        var first = await _fixture.TryWriteAsync(new CanonicalDefinitionWrite(
            DefinitionKind.MasterDimension, _fixture.TenantId, _fixture.OwnerId,
            code, "Shift dimension", "{\"grain\":\"shift\"}",
            CanonicalVersionStatus.Published, null));
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(1, first.Value!.VersionNumber);

        var second = await _fixture.TryWriteAsync(new CanonicalDefinitionWrite(
            DefinitionKind.MasterDimension, _fixture.TenantId, _fixture.OwnerId,
            code, "Shift dimension", "{\"grain\":\"shift\",\"labels\":\"long\"}",
            CanonicalVersionStatus.Published, null));
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(2, second.Value!.VersionNumber);

        Assert.Equal(1, await _fixture.CountDefinitionsForCodeAsync("g4_master_dimension"));
    }

    /// <summary>
    /// G5. An S3 analysis definition converges with its declared detail fields
    /// in analysis_details.
    /// </summary>
    [Fact]
    [Trait("Gate", "G5_S3_ANALYSIS")]
    public async Task An_s3_analysis_converges_onto_the_canonical_store()
    {
        var written = await _fixture.WriteAsync(new CanonicalDefinitionWrite(
            DefinitionKind.Analysis, _fixture.TenantId, _fixture.OwnerId,
            "t090test_g5_analysis", "Defect driver analysis", "{\"question\":\"drivers\"}",
            CanonicalVersionStatus.Published,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome_code"] = "surface_defect",
                ["grain_code"] = "material_unit",
                ["method_code"] = "shap_attribution",
            }));

        Assert.Equal(1, written.VersionNumber);
        Assert.Equal(1, await _fixture.CountDetailRowsAsync("analysis_details", "g5_analysis"));
    }

    /// <summary>
    /// G6. An S4 model definition converges with algorithm and hyperparameters
    /// in model_details.
    /// </summary>
    [Fact]
    [Trait("Gate", "G6_S4_MODEL")]
    public async Task An_s4_model_converges_onto_the_canonical_store()
    {
        var written = await _fixture.WriteAsync(new CanonicalDefinitionWrite(
            DefinitionKind.Model, _fixture.TenantId, _fixture.OwnerId,
            "t090test_g6_model", "Defect model", "{\"purpose\":\"defect\"}",
            CanonicalVersionStatus.Published,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["algorithm_code"] = "lightgbm",
                ["hyperparameters"] = "{\"trees\":200}",
            }));

        Assert.Equal(1, written.VersionNumber);
        Assert.Equal(1, await _fixture.CountDetailRowsAsync("model_details", "g6_model"));
    }

    /// <summary>
    /// G7. An S5 log rule definition converges with its condition, severity and
    /// message template in log_rule_details.
    /// </summary>
    [Fact]
    [Trait("Gate", "G7_S5_LOG_RULE")]
    public async Task An_s5_log_rule_converges_onto_the_canonical_store()
    {
        var written = await _fixture.WriteAsync(new CanonicalDefinitionWrite(
            DefinitionKind.LogRule, _fixture.TenantId, _fixture.OwnerId,
            "t090test_g7_log_rule", "Temperature excursion rule", "{\"rule\":\"excursion\"}",
            CanonicalVersionStatus.Published,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["condition_expression"] = "temperature_c > 900",
                ["severity"] = "warning",
                ["message_template"] = "Zone temperature exceeded {threshold}",
            }));

        Assert.Equal(1, written.VersionNumber);
        Assert.Equal(1, await _fixture.CountDetailRowsAsync("log_rule_details", "g7_log_rule"));
    }

    /// <summary>
    /// G10. SM-06 round trip: a single declared outcome lands as one child row
    /// of its version; two outcomes on another version land as two. outcome_code
    /// is the key WITHIN a version, not across the store.
    /// </summary>
    [Fact]
    [Trait("Gate", "G10_SM06_ROUND_TRIP")]
    public async Task Sm06_outcome_declarations_round_trip_as_version_children()
    {
        var one = await _fixture.WriteAsync(_fixture.CompleteOutcomeWrite("g10_single"));
        var two = await _fixture.WriteAsync(_fixture.TwoOutcomeWrite("g10_double"));

        Assert.Equal(1, await _fixture.CountOutcomeRowsAsync(one.VersionId));
        Assert.Equal(2, await _fixture.CountOutcomeRowsAsync(two.VersionId));
    }

    /// <summary>
    /// G11. SM-06 mutation refusal: once the outcome-bearing version publishes,
    /// a direct rewrite of its content is refused BY THE DATABASE, and the
    /// outcome child rows survive the attempt unchanged.
    /// </summary>
    [Fact]
    [Trait("Gate", "G11_SM06_MUTATION")]
    public async Task A_published_sm06_version_refuses_direct_mutation()
    {
        var written = await _fixture.WriteAsync(_fixture.CompleteOutcomeWrite("g11_mutation"));
        await _fixture.PublishAsync(written.DefinitionId, written.VersionNumber);

        var error = await _fixture.ExpectSqlFailureAsync(
            "UPDATE ppiq_meta.definition_versions SET graph_json = '{}'::jsonb WHERE id = @id;",
            written.VersionId);

        Assert.Contains("23514", error);
        Assert.Equal(1, await _fixture.CountOutcomeRowsAsync(written.VersionId));
    }
}
