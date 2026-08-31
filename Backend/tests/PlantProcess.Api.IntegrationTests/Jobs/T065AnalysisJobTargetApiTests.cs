using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Jobs;

/// <summary>
/// T-065 Pack B. The analysis-job target contract, proven through the real HTTP
/// surface against ppiq_presentation.
///
/// The distinction this file exists to hold: a REQUESTED target is what the
/// definition stores, and an EXECUTED target is what a run actually resolved.
/// The run response must never present the first as the second, because a
/// selector that was never resolved is not evidence that anything ran.
/// </summary>
public sealed class T065AnalysisJobTargetApiTests : AuthenticatedApiTestBase
{
    public T065AnalysisJobTargetApiTests(WebApplicationFactory<Program> factory) : base(factory) { }

    /// <summary>
    /// The GUID suffix is lower case and the create endpoint canonicalises codes
    /// through NormalizeCode - Trim, ToUpperInvariant, spaces and hyphens to
    /// underscores. So the code this fixture sends and the code the table holds
    /// are not the same string, and every direct assertion below compares the two
    /// case-insensitively, exactly as LoadDefinitionAsync already does. A second
    /// hand-written normaliser here would be a second rule that disagrees the
    /// first time the canonical one changes.
    /// </summary>
    private static readonly string Marker = "T065B_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    private static string Code(string suffix) => Marker + "_" + suffix;

    private static async Task<NpgsqlDataSource> DataSourceAsync()
    {
        Skip.IfNot(IsIntegrationDbReachable(), "Integration Postgres not reachable; runs in CI.");

        var dataSource = NpgsqlDataSource.Create(ResolveIntegrationTestConnectionString());
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            Assert.Equal("ppiq_presentation", conn.Database);
        }

        return dataSource;
    }

    private static async Task<object?> ScalarAsync(NpgsqlDataSource ds, string sql, params (string, object?)[] args)
    {
        await using var conn = await ds.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var value2 = await cmd.ExecuteScalarAsync();
        return value2 is DBNull ? null : value2;
    }

    private static async Task CleanAsync(NpgsqlDataSource ds)
    {
        await using var conn = await ds.OpenConnectionAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM public.inspection_jobs WHERE lower(inspection_job_code) LIKE lower(@p)";
            cmd.Parameters.AddWithValue("p", Marker + "%");
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            // T-090. The M1 compatibility version table is retired. Cleanup now
            // goes through the canonical store, which is where versions live.
            cmd.CommandText =
                "DELETE FROM ppiq_meta.definition_store s " +
                "WHERE s.definition_code LIKE @m || '%'";
            cmd.Parameters.AddWithValue("m", Marker);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Seeds a real version history.
    ///
    /// The kind is Widget and not Analysis on purpose, and the reason is measured
    /// rather than assumed: DefinitionService answers ListVersionsAsync for the
    /// widget kind only in M1. T-090 removed that limitation - every kind now
    /// resolves through the canonical store - but the kind stays Widget here so
    /// this test keeps proving what it always proved rather than quietly
    /// changing subject.
    ///
    /// Exactly one version is published, because the resolver refuses a
    /// definition with two published versions rather than choosing between them.
    /// </summary>
    private static async Task SeedVersionAsync(
        NpgsqlDataSource ds, Guid definitionId, int versionNumber, bool isPublished)
    {
        await using var conn = await ds.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        // T-090. Seeds canonical identity and an immutable version instead of a
        // row in the retired compatibility table. The fixture now speaks in
        // definition and version identity rather than a physical table name,
        // which is what keeps it working when storage moves again.
        cmd.CommandText =
            "WITH parent AS (" +
            "  INSERT INTO ppiq_meta.definition_store " +
            "    (id, tenant_id, definition_code, surface, definition_kind, name, owner_id, current_version) " +
            "  SELECT @did, t.id, @m || @did::text, 'S2', 'widget', 'T065 fixture widget', u.id, @vn " +
            "    FROM ppiq_meta.tenants t CROSS JOIN ppiq_meta.app_users u " +
            "   WHERE t.is_deleted = false AND u.is_deleted = false LIMIT 1 " +
            "  ON CONFLICT (tenant_id, definition_code) DO UPDATE SET current_version = @vn " +
            "  RETURNING id, tenant_id) " +
            "INSERT INTO ppiq_meta.definition_versions " +
            "  (id, tenant_id, definition_id, version_number, status, mode, graph_json, definition_hash) " +
            "SELECT @id, parent.tenant_id, parent.id, @vn, " +
            "       CASE WHEN @pub THEN 'published' ELSE 'superseded' END, 'block', " +
            "       CAST('{}' AS jsonb), md5(@did::text || @vn::text) FROM parent";
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("did", definitionId);
        cmd.Parameters.AddWithValue("vn", versionNumber);
        cmd.Parameters.AddWithValue("m", Marker);
        cmd.Parameters.AddWithValue("pub", isPublished);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Writes a definition whose stored run declaration is exactly what the test
    /// states, including declarations the create endpoint would never author.
    /// Rows reach this table from outside the API - imports, fixtures, earlier
    /// schema versions - and the run path must be honest about all of them.
    /// </summary>
    private static async Task SeedRawDefinitionAsync(NpgsqlDataSource ds, string code, string ruleJson)
    {
        await using var conn = await ds.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO public.inspection_jobs " +
            "(id, inspection_job_code, inspection_job_name, inspection_type, rule_json, " +
            " schedule_expression, is_enabled, honest_state, source_system, created_at_utc) " +
            "VALUES (@id, @code, @code, 'AnalysisJobDefinition', CAST(@rule AS jsonb), " +
            "        'Manual', true, 'RuleBasedMonitoring', @src, now())";
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("code", code);
        cmd.Parameters.AddWithValue("rule", ruleJson);
        cmd.Parameters.AddWithValue("src", Marker);
        await cmd.ExecuteNonQueryAsync();
    }

    private static object CreateBody(
        string code,
        string? engineJobCode = "ML_PROCESS_VS_DEFECT",
        string? targetKind = null,
        Guid? targetId = null,
        int? targetVersion = null,
        string? targetPolicy = null,
        string? targetParameters = null)
    {
        return new
        {
            Code = code,
            Name = code,
            DefectType = "T065B",
            WindowDays = 30,
            EngineJobCode = engineJobCode,
            ScheduleExpression = "Manual",
            IsEnabled = true,
            TargetDefinitionKind = targetKind,
            TargetDefinitionId = targetId,
            TargetDefinitionVersion = targetVersion,
            TargetVersionPolicy = targetPolicy,
            TargetParameters = targetParameters
        };
    }

    // ---- requested target round-trips -------------------------------------

    [SkippableFact]
    public async Task A_current_published_target_round_trips_through_create()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("CP");
        var targetId = Guid.NewGuid();

        try
        {
            var created = await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, targetKind: "Analysis", targetId: targetId, targetPolicy: "CurrentPublished"));
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);

            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Analysis", body.GetProperty("targetDefinitionKind").GetString());
            Assert.Equal(targetId, body.GetProperty("targetDefinitionId").GetGuid());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("targetDefinitionVersion").ValueKind);

            // The wire speaks the semantic value; the column holds the stored one.
            Assert.Equal("current_published",
                (string?)await ScalarAsync(ds,
                    "SELECT target_version_policy FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                    ("c", code)));
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    [SkippableFact]
    public async Task A_pinned_target_round_trips_through_create_with_its_exact_version()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("PIN");
        var targetId = Guid.NewGuid();

        try
        {
            var created = await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, targetKind: "Analysis", targetId: targetId,
                           targetVersion: 7, targetPolicy: "Pinned"));
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);

            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(7, body.GetProperty("targetDefinitionVersion").GetInt32());

            Assert.Equal("pinned",
                (string?)await ScalarAsync(ds,
                    "SELECT target_version_policy FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                    ("c", code)));
            Assert.Equal(7,
                Convert.ToInt32(await ScalarAsync(ds,
                    "SELECT target_definition_version FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                    ("c", code))));
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    /// <summary>
    /// The many-to-one engine linkage is the defect this bridge exists to
    /// prevent, so it is proved at the API surface and not only in the column.
    /// </summary>
    [SkippableFact]
    public async Task Three_definitions_on_one_engine_job_code_keep_independent_targets()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        try
        {
            for (var i = 0; i < 3; i++)
            {
                var response = await client.PostAsJsonAsync("/api/analysis-jobs",
                    CreateBody(Code("SHARED" + i), targetKind: "Analysis",
                               targetId: ids[i], targetPolicy: "CurrentPublished"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            var distinct = Convert.ToInt64(await ScalarAsync(ds,
                "SELECT count(DISTINCT target_definition_id) FROM public.inspection_jobs " +
                "WHERE lower(inspection_job_code) LIKE lower(@p)", ("p", Marker + "_SHARED%")));

            Assert.Equal(3L, distinct);
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    [SkippableFact]
    public async Task An_update_moves_one_target_and_leaves_the_others_alone()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var first = Code("UPD1");
        var second = Code("UPD2");
        var originalA = Guid.NewGuid();
        var originalB = Guid.NewGuid();
        var moved = Guid.NewGuid();

        try
        {
            await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(first, targetKind: "Analysis", targetId: originalA, targetPolicy: "CurrentPublished"));
            await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(second, targetKind: "Analysis", targetId: originalB, targetPolicy: "CurrentPublished"));

            var updated = await client.PutAsJsonAsync("/api/analysis-jobs/" + first, new
            {
                Name = first,
                DefectType = "T065B",
                WindowDays = 30,
                EngineJobCode = "ML_PROCESS_VS_DEFECT",
                ScheduleExpression = "Manual",
                IsEnabled = true,
                TargetDefinitionKind = "Analysis",
                TargetDefinitionId = moved,
                TargetDefinitionVersion = 3,
                TargetVersionPolicy = "Pinned"
            });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

            Assert.Equal(moved, (Guid?)await ScalarAsync(ds,
                "SELECT target_definition_id FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                ("c", first)));
            Assert.Equal(originalB, (Guid?)await ScalarAsync(ds,
                "SELECT target_definition_id FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                ("c", second)));
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    // ---- absence is not emptiness -----------------------------------------

    [SkippableFact]
    public async Task Absent_target_parameters_stay_SQL_NULL()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("PNULL");

        try
        {
            await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, targetKind: "Analysis", targetId: Guid.NewGuid(),
                           targetPolicy: "CurrentPublished", targetParameters: null));

            var isNull = (bool)(await ScalarAsync(ds,
                "SELECT target_parameters IS NULL FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                ("c", code)))!;

            Assert.True(isNull, "absent parameters must remain SQL NULL, never an empty object");
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    [SkippableFact]
    public async Task An_empty_target_parameters_object_stays_an_empty_object()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("PEMPTY");

        try
        {
            await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, targetKind: "Analysis", targetId: Guid.NewGuid(),
                           targetPolicy: "CurrentPublished", targetParameters: "{}"));

            var isNull = (bool)(await ScalarAsync(ds,
                "SELECT target_parameters IS NULL FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                ("c", code)))!;
            Assert.False(isNull, "an empty object is a statement, not an absence");

            Assert.Equal("{}", (string?)await ScalarAsync(ds,
                "SELECT target_parameters::text FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                ("c", code)));
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    // ---- refusals at the boundary -----------------------------------------

    [SkippableFact]
    public async Task An_unrecognised_version_policy_is_refused_before_anything_is_written()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("BADPOL");

        try
        {
            var response = await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, targetKind: "Analysis", targetId: Guid.NewGuid(), targetPolicy: "latest"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var written = Convert.ToInt64(await ScalarAsync(ds,
                "SELECT count(*) FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)", ("c", code)));
            Assert.Equal(0L, written);
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    [SkippableFact]
    public async Task Half_a_target_is_refused()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();

        try
        {
            var noPolicy = await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(Code("HALF1"), targetKind: "Analysis", targetId: Guid.NewGuid()));
            Assert.Equal(HttpStatusCode.BadRequest, noPolicy.StatusCode);

            var pinnedWithoutVersion = await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(Code("HALF2"), targetKind: "Analysis", targetId: Guid.NewGuid(),
                           targetPolicy: "Pinned"));
            Assert.Equal(HttpStatusCode.BadRequest, pinnedWithoutVersion.StatusCode);

            var currentPublishedWithVersion = await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(Code("HALF3"), targetKind: "Analysis", targetId: Guid.NewGuid(),
                           targetVersion: 2, targetPolicy: "CurrentPublished"));
            Assert.Equal(HttpStatusCode.BadRequest, currentPublishedWithVersion.StatusCode);

            var unknownKind = await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(Code("HALF4"), targetKind: "NotAKind", targetId: Guid.NewGuid(),
                           targetPolicy: "CurrentPublished"));
            Assert.Equal(HttpStatusCode.BadRequest, unknownKind.StatusCode);

            // Enum.TryParse alone would have accepted this as a definition surface.
            var numericKind = await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(Code("HALF5"), targetKind: "4", targetId: Guid.NewGuid(),
                           targetPolicy: "CurrentPublished"));
            Assert.Equal(HttpStatusCode.BadRequest, numericKind.StatusCode);
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    // ---- the run path ------------------------------------------------------

    /// <summary>
    /// A definition that declares no target runs exactly as it did before T-065.
    /// This is the regression guard for every analysis job that exists today.
    /// </summary>
    [SkippableFact]
    public async Task A_definition_with_no_target_still_runs_and_reports_no_executed_identity()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("NOTGT");

        try
        {
            await client.PostAsJsonAsync("/api/analysis-jobs", CreateBody(code));

            var run = await client.PostAsJsonAsync("/api/analysis-jobs/" + code + "/run",
                new { WindowDaysOverride = (int?)null });
            Assert.Equal(HttpStatusCode.OK, run.StatusCode);

            var body = await run.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("NoTargetDeclared", body.GetProperty("targetStatus").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("executedTargetDefinitionId").ValueKind);
            Assert.NotEqual("BlockedTarget", body.GetProperty("definitionStatus").GetString());
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    /// <summary>
    /// An engine job code the committed catalogue does not name has no class, and
    /// a class is never guessed, so the engine is not reached at all.
    /// </summary>
    [SkippableFact]
    public async Task An_engine_job_code_absent_from_the_catalogue_blocks_the_run_before_the_engine()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("NOCAT");

        try
        {
            await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, engineJobCode: "ML_" + Marker + "_NOT_IN_CATALOGUE"));

            var run = await client.PostAsJsonAsync("/api/analysis-jobs/" + code + "/run",
                new { WindowDaysOverride = (int?)null });
            Assert.Equal(HttpStatusCode.OK, run.StatusCode);

            var body = await run.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("BlockedTarget", body.GetProperty("definitionStatus").GetString());
            Assert.Equal("Refused", body.GetProperty("targetStatus").GetString());
            Assert.Equal("TargetClassUnmappable", body.GetProperty("targetRefusalCode").GetString());
            Assert.Equal("NotRun", body.GetProperty("learningStatus").GetString());
            Assert.Equal("NotRun", body.GetProperty("computeStatus").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("executedTargetDefinitionId").ValueKind);

            // Nothing was stamped, because nothing ran.
            var stamped = await ScalarAsync(ds,
                "SELECT last_run_status FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                ("c", code));
            Assert.Null(stamped);
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    /// <summary>
    /// The whole point of the task: a run reports the version it actually
    /// resolved, and that version comes from the resolver rather than from the
    /// selector the definition stored.
    /// </summary>
    [SkippableFact]
    public async Task A_current_published_target_resolves_the_published_version()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("RESCP");
        var targetId = Guid.NewGuid();

        try
        {
            await SeedVersionAsync(ds, targetId, 1, true);
            await SeedVersionAsync(ds, targetId, 2, false);

            await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, targetKind: "Widget", targetId: targetId,
                           targetPolicy: "CurrentPublished"));

            var run = await client.PostAsJsonAsync("/api/analysis-jobs/" + code + "/run",
                new { WindowDaysOverride = (int?)null });
            var body = await run.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("Resolved", body.GetProperty("targetStatus").GetString());
            Assert.Equal("Widget", body.GetProperty("executedTargetDefinitionKind").GetString());
            Assert.Equal(targetId, body.GetProperty("executedTargetDefinitionId").GetGuid());
            Assert.Equal(1, body.GetProperty("executedTargetDefinitionVersion").GetInt32());
            Assert.NotEqual("BlockedTarget", body.GetProperty("definitionStatus").GetString());
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    /// <summary>
    /// A pinned version resolves to itself. The definition stores the number 1
    /// and version 2 exists, so an implementation that reported "the latest" or
    /// echoed a policy instead of a number would be visible here.
    /// </summary>
    [SkippableFact]
    public async Task A_pinned_target_resolves_its_exact_version()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("RESPIN");
        var targetId = Guid.NewGuid();

        try
        {
            await SeedVersionAsync(ds, targetId, 1, true);
            await SeedVersionAsync(ds, targetId, 2, false);

            await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, targetKind: "Widget", targetId: targetId,
                           targetVersion: 1, targetPolicy: "Pinned"));

            var run = await client.PostAsJsonAsync("/api/analysis-jobs/" + code + "/run",
                new { WindowDaysOverride = (int?)null });
            var body = await run.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("Resolved", body.GetProperty("targetStatus").GetString());
            Assert.Equal(1, body.GetProperty("executedTargetDefinitionVersion").GetInt32());
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    /// <summary>
    /// JB03, both readings of it: a pinned version that exists but was never
    /// published, and a pinned version that is not in the history at all. Neither
    /// reaches the engine, and neither reports an executed identity.
    /// </summary>
    [SkippableTheory]
    [InlineData(2)]
    [InlineData(9)]
    public async Task A_pinned_version_that_is_not_published_refuses_with_JB03(int pinnedVersion)
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("JB03_" + pinnedVersion);
        var targetId = Guid.NewGuid();

        try
        {
            await SeedVersionAsync(ds, targetId, 1, true);
            await SeedVersionAsync(ds, targetId, 2, false);

            await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, targetKind: "Widget", targetId: targetId,
                           targetVersion: pinnedVersion, targetPolicy: "Pinned"));

            var run = await client.PostAsJsonAsync("/api/analysis-jobs/" + code + "/run",
                new { WindowDaysOverride = (int?)null });
            var body = await run.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("BlockedTarget", body.GetProperty("definitionStatus").GetString());
            Assert.Equal("JB03", body.GetProperty("targetRefusalCode").GetString());
            Assert.Equal("NotRun", body.GetProperty("computeStatus").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("executedTargetDefinitionVersion").ValueKind);

            // The requested selector is in the definition and is deliberately not
            // echoed as evidence that anything executed.
            Assert.Equal(JsonValueKind.Null, body.GetProperty("executedTargetDefinitionId").ValueKind);
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    /// <summary>
    /// A kind with no version adapter is refused honestly. This is the current
    /// state of every kind except Widget, and the refusal says so rather than
    /// reporting a version that was never written.
    /// </summary>
    [SkippableFact]
    public async Task A_target_kind_with_no_version_adapter_blocks_the_run()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("NOADPT");

        try
        {
            await client.PostAsJsonAsync("/api/analysis-jobs",
                CreateBody(code, targetKind: "Analysis", targetId: Guid.NewGuid(),
                           targetPolicy: "CurrentPublished"));

            var run = await client.PostAsJsonAsync("/api/analysis-jobs/" + code + "/run",
                new { WindowDaysOverride = (int?)null });
            var body = await run.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("BlockedTarget", body.GetProperty("definitionStatus").GetString());
            Assert.Equal("NotRun", body.GetProperty("computeStatus").GetString());
            Assert.Contains("version adapter", body.GetProperty("targetRefusalReason").GetString()!,
                StringComparison.Ordinal);
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    /// <summary>
    /// The run response carries the three executed fields in every case, so a
    /// consumer never has to guess whether the contract applies to this run.
    /// </summary>
    [SkippableFact]
    public async Task The_run_response_always_declares_the_executed_identity_fields()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("SHAPE");

        try
        {
            await client.PostAsJsonAsync("/api/analysis-jobs", CreateBody(code));

            var run = await client.PostAsJsonAsync("/api/analysis-jobs/" + code + "/run",
                new { WindowDaysOverride = (int?)null });

            var body = await run.Content.ReadFromJsonAsync<JsonElement>();

            foreach (var name in new[]
            {
                "targetStatus",
                "targetRefusalCode",
                "targetRefusalReason",
                "executedTargetDefinitionKind",
                "executedTargetDefinitionId",
                "executedTargetDefinitionVersion"
            })
            {
                Assert.True(body.TryGetProperty(name, out _), name + " must be present on every run response");
            }
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    /// <summary>
    /// The run path reads the declaration and never manufactures one. Before this,
    /// a definition that named no engine job code silently ran as
    /// ML_PROCESS_VS_DEFECT, took that code's class from the catalogue, and
    /// executed against a target nobody had declared.
    /// </summary>
    [SkippableTheory]
    [InlineData("{\"engineOutcomeKey\":\"defect.rate_per_m2\",\"grain\":\"coil\"}", "engineJobCode")]
    [InlineData("{\"engineJobCode\":\"ML_PROCESS_VS_DEFECT\",\"grain\":\"coil\"}", "engineOutcomeKey")]
    [InlineData("{\"engineJobCode\":\"ML_PROCESS_VS_DEFECT\",\"engineOutcomeKey\":\"defect.rate_per_m2\"}", "grain")]
    [InlineData("{\"engineJobCode\":\"   \",\"engineOutcomeKey\":\"defect.rate_per_m2\",\"grain\":\"coil\"}", "engineJobCode")]
    public async Task An_incomplete_run_declaration_blocks_the_run_before_the_engine(
        string ruleJson, string missingKey)
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("DECL_" + missingKey.ToUpperInvariant());

        try
        {
            await SeedRawDefinitionAsync(ds, code, ruleJson);

            var run = await client.PostAsJsonAsync("/api/analysis-jobs/" + code + "/run",
                new { WindowDaysOverride = (int?)null });
            Assert.Equal(HttpStatusCode.OK, run.StatusCode);

            var body = await run.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("BlockedDefinition", body.GetProperty("definitionStatus").GetString());
            Assert.Equal("RunDeclarationIncomplete", body.GetProperty("targetRefusalCode").GetString());
            Assert.Contains(missingKey, body.GetProperty("targetRefusalReason").GetString()!,
                StringComparison.Ordinal);

            // Neither engine was reached.
            Assert.Equal("NotRun", body.GetProperty("learningStatus").GetString());
            Assert.Equal("NotRun", body.GetProperty("computeStatus").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("executedTargetDefinitionId").ValueKind);

            // And nothing was stamped, because nothing ran.
            Assert.Null(await ScalarAsync(ds,
                "SELECT last_run_status FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                ("c", code)));
            Assert.Null(await ScalarAsync(ds,
                "SELECT source_correlation_run_id FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                ("c", code)));
        }
        finally
        {
            await CleanAsync(ds);
        }
    }

    /// <summary>
    /// A declaration that cannot be read as a set of keys is refused rather than
    /// silently replaced by defaults, which is what the swallowed parse failure
    /// used to do.
    /// </summary>
    [SkippableFact]
    public async Task A_run_declaration_that_is_not_an_object_blocks_the_run()
    {
        var ds = await DataSourceAsync();
        using var client = await CreateAuthenticatedClientAsync();
        var code = Code("DECLARR");

        try
        {
            await SeedRawDefinitionAsync(ds, code, "[]");

            var run = await client.PostAsJsonAsync("/api/analysis-jobs/" + code + "/run",
                new { WindowDaysOverride = (int?)null });
            Assert.Equal(HttpStatusCode.OK, run.StatusCode);

            var body = await run.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("BlockedDefinition", body.GetProperty("definitionStatus").GetString());
            Assert.Equal("RunDeclarationMalformed", body.GetProperty("targetRefusalCode").GetString());
            Assert.Equal("NotRun", body.GetProperty("learningStatus").GetString());
            Assert.Equal("NotRun", body.GetProperty("computeStatus").GetString());

            Assert.Null(await ScalarAsync(ds,
                "SELECT last_run_status FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@c)",
                ("c", code)));
        }
        finally
        {
            await CleanAsync(ds);
        }
    }
}
