using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Infrastructure.Jobs;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Jobs;

/// <summary>
/// T-065 bridge, proven against ppiq_presentation.
///
/// The retirement guard is the reason this task exists in its current shape. It
/// is proved here against BOTH stores at once, because the failure this design
/// prevents - a definition retiring while an analysis job still points at it -
/// only appears when the two stores disagree.
/// </summary>
public sealed class T065AnalysisJobTargetBridgeTests : AuthenticatedApiTestBase
{
    public T065AnalysisJobTargetBridgeTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private const string Kind = "Analysis";
    private static readonly string Marker = "T065_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    private static async Task<NpgsqlDataSource> DataSourceAsync()
    {
        Skip.IfNot(IsIntegrationDbReachable(), "Integration Postgres not reachable; runs in CI.");

        var dataSource = NpgsqlDataSource.Create(ResolveIntegrationTestConnectionString());
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            Assert.Equal("ppiq_presentation", conn.Database);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT count(*) FROM information_schema.columns " +
                "WHERE table_schema='public' AND table_name='inspection_jobs' " +
                "  AND column_name IN ('target_definition_kind','target_definition_id'," +
                "                      'target_definition_version','target_version_policy','target_parameters')";
            Assert.Equal(5L, (long)(await cmd.ExecuteScalarAsync())!);
        }

        return dataSource;
    }

    private static async Task ExecAsync(NpgsqlDataSource ds, string sql, params (string, object?)[] args)
    {
        await using var conn = await ds.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SeedAnalysisJobAsync(
        NpgsqlDataSource ds, string code, Guid? targetId, string? policy, int? version, string? parametersJson)
    {
        await ExecAsync(ds,
            "INSERT INTO public.inspection_jobs " +
            "(id, inspection_job_code, inspection_job_name, inspection_type, rule_json, " +
            " schedule_expression, is_enabled, honest_state, source_system, created_at_utc, " +
            " target_definition_kind, target_definition_id, target_version_policy, " +
            " target_definition_version, target_parameters) " +
            "VALUES (@id, @code, @code, 'AnalysisJobDefinition', " +
            "        CAST('{\"engineJobCode\":\"ML_PROCESS_VS_DEFECT\"}' AS jsonb), " +
            "        'Manual', true, 'RuleBasedMonitoring', @src, now(), " +
            "        @kind, @tid, @policy, @version, CAST(@params AS jsonb))",
            ("id", Guid.NewGuid()), ("code", code), ("src", Marker),
            ("kind", targetId is null ? null : Kind),
            ("tid", targetId), ("policy", policy), ("version", version), ("params", parametersJson));
    }


    /// <summary>
    /// Seeds a job_definitions row that targets the same definition, using the
    /// column shape T-064 already proved against PostgreSQL.
    ///
    /// An earlier version discovered NOT NULL columns from the schema and filled
    /// them by data type. That satisfied nullability and then violated a CHECK
    /// constraint, because a placeholder can be the right type and still be a
    /// value the table refuses. Real columns with real defaults are left to the
    /// database.
    /// </summary>
    private static async Task SeedCanonicalJobAsync(NpgsqlDataSource ds, string code, Guid targetId)
    {
        await ExecAsync(ds,
            "INSERT INTO public.job_definitions " +
            "(id, job_code, job_name, job_type, schedule_expression, is_enabled, last_run_status, " +
            " created_at_utc, is_deleted, is_synthetic, " +
            " target_definition_id, target_definition_kind, target_version_policy, " +
            " target_definition_version, target_parameters) " +
            "VALUES (@id, @code, @code, 'Custom', 'Manual', true, 'NeverRun', " +
            "        now(), false, false, " +
            "        @tid, @kind, 'current_published', NULL, NULL)",
            ("id", Guid.NewGuid()), ("code", code), ("tid", targetId), ("kind", Kind));
    }

    private static async Task CleanAsync(NpgsqlDataSource ds)
    {
        await ExecAsync(ds, "DELETE FROM public.inspection_jobs WHERE source_system = @src", ("src", Marker));
        await ExecAsync(ds, "DELETE FROM public.job_definitions WHERE job_code LIKE @p", ("p", Marker + "%"));
    }

    private IJobTargetLookup ResolveLookup(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IJobTargetLookup>();

    // ---- the many-to-one problem this bridge exists to solve ---------------

    [SkippableFact]
    public async Task Three_analysis_jobs_sharing_one_engine_job_keep_independent_targets()
    {
        var ds = await DataSourceAsync();
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();

        try
        {
            await SeedAnalysisJobAsync(ds, Marker + "_A", a, "current_published", null, null);
            await SeedAnalysisJobAsync(ds, Marker + "_B", b, "pinned", 3, "{}");
            await SeedAnalysisJobAsync(ds, Marker + "_C", c, "current_published", null, "{\"k\":1}");

            // All three declare the same engineJobCode. Before this bridge they
            // shared one job_definitions row and could not have held three
            // different targets at once.
            await using var conn = await ds.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT inspection_job_code, target_definition_id::text, target_version_policy, " +
                "       target_definition_version, target_parameters::text " +
                "FROM public.inspection_jobs WHERE source_system = @src ORDER BY inspection_job_code";
            cmd.Parameters.AddWithValue("src", Marker);

            var seen = new List<(string Code, string Id, string Policy, object Version, object Parameters)>();
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    seen.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2),
                        reader.IsDBNull(3) ? "null" : reader.GetInt32(3),
                        reader.IsDBNull(4) ? "null" : reader.GetString(4)));
                }
            }

            Assert.Equal(3, seen.Count);
            Assert.Equal(3, seen.Select(x => x.Id).Distinct().Count());

            Assert.Equal(a.ToString(), seen[0].Id);
            Assert.Equal("null", seen[0].Version.ToString());
            Assert.Equal("null", seen[0].Parameters.ToString());

            Assert.Equal("pinned", seen[1].Policy);
            Assert.Equal(3, seen[1].Version);
            // {} is a deliberate empty set and stays one.
            Assert.Equal("{}", seen[1].Parameters.ToString());

            Assert.Equal("{\"k\": 1}", seen[2].Parameters.ToString());
        }
        finally { await CleanAsync(ds); await ds.DisposeAsync(); }
    }

    [SkippableFact]
    public async Task Changing_one_target_does_not_change_the_others()
    {
        var ds = await DataSourceAsync();
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var moved = Guid.NewGuid();

        try
        {
            await SeedAnalysisJobAsync(ds, Marker + "_A", a, "current_published", null, null);
            await SeedAnalysisJobAsync(ds, Marker + "_B", b, "current_published", null, null);
            await SeedAnalysisJobAsync(ds, Marker + "_C", c, "current_published", null, null);

            await ExecAsync(ds,
                "UPDATE public.inspection_jobs SET target_definition_id = @new WHERE inspection_job_code = @code",
                ("new", moved), ("code", Marker + "_B"));

            await using var conn = await ds.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT inspection_job_code, target_definition_id::text FROM public.inspection_jobs " +
                "WHERE source_system = @src ORDER BY inspection_job_code";
            cmd.Parameters.AddWithValue("src", Marker);

            var byCode = new Dictionary<string, string>();
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) byCode[reader.GetString(0)] = reader.GetString(1);
            }

            Assert.Equal(a.ToString(), byCode[Marker + "_A"]);
            Assert.Equal(moved.ToString(), byCode[Marker + "_B"]);
            Assert.Equal(c.ToString(), byCode[Marker + "_C"]);
        }
        finally { await CleanAsync(ds); await ds.DisposeAsync(); }
    }

    // ---- JB04 across both stores ------------------------------------------

    [SkippableFact]
    public async Task The_guard_sees_a_dependent_that_lives_only_in_the_compatibility_store()
    {
        var ds = await DataSourceAsync();
        var target = Guid.NewGuid();

        try
        {
            await SeedAnalysisJobAsync(ds, Marker + "_ONLY_ANALYSIS", target, "current_published", null, null);

            using var scope = Factory.Services.CreateScope();
            var codes = await ResolveLookup(scope)
                .JobCodesTargetingAsync(Kind, target, CancellationToken.None);

            // Without the composite this returns empty and the definition retires
            // while an analysis job still points at it.
            Assert.Contains(Marker + "_ONLY_ANALYSIS", codes);
        }
        finally { await CleanAsync(ds); await ds.DisposeAsync(); }
    }

    [SkippableFact]
    public async Task A_job_code_present_in_both_stores_is_named_once()
    {
        var ds = await DataSourceAsync();
        var target = Guid.NewGuid();
        var shared = Marker + "_SHARED";

        try
        {
            await SeedAnalysisJobAsync(ds, shared, target, "current_published", null, null);
            await SeedCanonicalJobAsync(ds, shared, target);

            using var scope = Factory.Services.CreateScope();
            var codes = await ResolveLookup(scope)
                .JobCodesTargetingAsync(Kind, target, CancellationToken.None);

            Assert.Equal(1, codes.Count(x => x == shared));
        }
        finally { await CleanAsync(ds); await ds.DisposeAsync(); }
    }

    [SkippableFact]
    public async Task A_definition_nothing_targets_has_no_dependents()
    {
        var ds = await DataSourceAsync();
        try
        {
            using var scope = Factory.Services.CreateScope();
            var codes = await ResolveLookup(scope)
                .JobCodesTargetingAsync(Kind, Guid.NewGuid(), CancellationToken.None);

            Assert.Empty(codes);
        }
        finally { await ds.DisposeAsync(); }
    }

    [SkippableFact]
    public async Task Dependents_are_returned_in_a_stable_order()
    {
        var ds = await DataSourceAsync();
        var target = Guid.NewGuid();

        try
        {
            await SeedAnalysisJobAsync(ds, Marker + "_Z", target, "current_published", null, null);
            await SeedAnalysisJobAsync(ds, Marker + "_A", target, "current_published", null, null);

            using var scope = Factory.Services.CreateScope();
            var first = await ResolveLookup(scope).JobCodesTargetingAsync(Kind, target, CancellationToken.None);
            var second = await ResolveLookup(scope).JobCodesTargetingAsync(Kind, target, CancellationToken.None);

            // A guard whose message reorders between calls reads like the
            // dependency set changed when it did not.
            Assert.Equal(first, second);
            Assert.Equal(first.OrderBy(x => x, StringComparer.Ordinal).ToList(), first);
        }
        finally { await CleanAsync(ds); await ds.DisposeAsync(); }
    }

    // ---- composition -------------------------------------------------------

    [SkippableFact]
    public async Task Both_compatibility_stores_persist_the_same_policy_vocabulary()
    {
        var ds = await DataSourceAsync();
        var definitionId = Guid.NewGuid();
        var analysisCode = Marker + "_VOCAB";
        var canonicalCode = Marker + "_VOCAB_CANON";

        try
        {
            // One concept, one persisted spelling. If these two stores disagree,
            // a guard that compares them matches nothing and reports no
            // dependents while a dependent exists.
            await SeedAnalysisJobAsync(ds, analysisCode, definitionId, "current_published", null, null);
            await SeedAnalysisJobAsync(ds, analysisCode + "_P", definitionId, "pinned", 4, null);
            await SeedCanonicalJobAsync(ds, canonicalCode, definitionId);

            await using var conn = await ds.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT DISTINCT target_version_policy FROM public.inspection_jobs " +
                " WHERE source_system = @src AND target_version_policy IS NOT NULL " +
                "UNION " +
                "SELECT DISTINCT target_version_policy FROM public.job_definitions " +
                " WHERE job_code = @canon AND target_version_policy IS NOT NULL " +
                "ORDER BY 1";
            cmd.Parameters.AddWithValue("src", Marker);
            cmd.Parameters.AddWithValue("canon", canonicalCode);

            var values = new List<string>();
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) values.Add(reader.GetString(0));
            }

            // Exactly the frozen vocabulary, and nothing that only differs by case.
            Assert.Equal(new[] { "current_published", "pinned" }, values);
        }
        finally
        {
            // CleanAsync already removes both inspection_jobs rows owned by this
            // test marker and canonical job_definitions rows whose code starts
            // with the same marker.
            await CleanAsync(ds);
            await ds.DisposeAsync();
        }
    }

    [SkippableFact]
    public void Exactly_one_lookup_authority_resolves_and_it_is_the_composite()
    {
        Skip.IfNot(IsIntegrationDbReachable(), "Integration Postgres not reachable; runs in CI.");

        using var scope = Factory.Services.CreateScope();

        var all = scope.ServiceProvider.GetServices<IJobTargetLookup>().ToList();
        Assert.Single(all);
        Assert.IsType<AnalysisAwareJobTargetLookup>(all[0]);
    }

    [SkippableFact]
    public void The_database_refuses_an_incoherent_target_even_if_the_application_is_bypassed()
    {
        Skip.IfNot(IsIntegrationDbReachable(), "Integration Postgres not reachable; runs in CI.");

        var ds = NpgsqlDataSource.Create(ResolveIntegrationTestConnectionString());
        try
        {
            // Pinned with no version. JobTargetReference.Validate refuses this at
            // the boundary; the CHECK refuses it here too, because one of the two
            // will eventually be bypassed.
            var incoherent = SeedAnalysisJobAsync(ds, Marker + "_BAD", Guid.NewGuid(), "pinned", null, null);
            Assert.ThrowsAsync<PostgresException>(async () => await incoherent).GetAwaiter().GetResult();
        }
        finally { ds.Dispose(); }
    }
}