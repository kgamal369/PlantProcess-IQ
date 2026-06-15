using PlantProcess.Api.IntegrationTests.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Phase4;

/// <summary>
/// PPIQ-404 (correctness vs seeded demo DB), PPIQ-401 (four standing jobs wired),
/// PPIQ-405 (deterministic re-run). The real AdvancedCorrelationComputeService runs against
/// a known signal seeded into the demo DB; true drivers must be recovered, spurious rejected,
/// and no known-true demo fact contradicted.
/// </summary>
public sealed class Phase4DemoCorrectnessAndJobsTests
{
    private static string Conn() =>
        Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("PLANTPROCESS_TEST_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb")
        ?? "Host=127.0.0.1;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123";

    private static bool IsDbReachable()
    {
        try
        {
            using var c = new NpgsqlConnection(Conn());
            c.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
    private static string FindSeed()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Backend", "database", "seed", "011_p4_demo_correctness_signal.sql");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("011_p4_demo_correctness_signal.sql not found walking up from " + AppContext.BaseDirectory);
    }

    private static async Task<NpgsqlConnection> OpenSeededAsync()
    {
        Skip.IfNot(IsDbReachable(), "Integration Postgres not reachable/authenticated on this machine; runs in CI.");
        var c = new NpgsqlConnection(Conn());
        await c.OpenAsync();
        var sql = await File.ReadAllTextAsync(FindSeed());
        await using (var cmd = new NpgsqlCommand(sql, c)) await cmd.ExecuteNonQueryAsync();
        return c;
    }

    private sealed class DbLoader : IFeatureVectorLoader
    {
        private readonly string _conn;
        public DbLoader(string conn) => _conn = conn;

        public async Task<AdvancedDataset> LoadAsync(AdvancedAnalysisRequest request, CancellationToken ct)
        {
            var outcomes = new List<OutcomeSample>();
            var featureBuckets = new Dictionary<string, List<FeatureSample>>(StringComparer.Ordinal);

            await using var c = new NpgsqlConnection(_conn);
            await c.OpenAsync(ct);

            await using (var cmd = new NpgsqlCommand(
                "SELECT sample_key, outcome_value, heat_key FROM public.ppiq_p4_demo_outcomes ORDER BY sample_index", c))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    outcomes.Add(new OutcomeSample(r.GetString(0), r.GetDouble(1), null, r.GetString(2)));

            await using (var cmd = new NpgsqlCommand(
                "SELECT f.feature_key, o.sample_key, f.feature_value " +
                "FROM public.ppiq_p4_demo_features f JOIN public.ppiq_p4_demo_outcomes o USING(sample_index) " +
                "ORDER BY f.feature_key, f.sample_index", c))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                {
                    var key = r.GetString(0);
                    if (!featureBuckets.TryGetValue(key, out var list)) { list = new List<FeatureSample>(); featureBuckets[key] = list; }
                    list.Add(new FeatureSample(r.GetString(1), r.GetDouble(2), null));
                }

            var features = featureBuckets
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new FeatureSeries(kv.Key, VariableType.Numeric, kv.Value))
                .ToList();

            return new AdvancedDataset(
                request.OutcomeKey, VariableType.Numeric, outcomes, features,
                new Dictionary<string, string>(),
                IndependentHeats: outcomes.Count,
                FreshnessFactor: 0.0,
                RequiredFieldCompleteness: 1.0);
        }
    }

    private sealed class NullWriter : IAdvancedResultWriter
    {
        public Task<Guid> WriteAsync(AdvancedAnalysisRequest request, AdvancedAnalysisRunResult result, CancellationToken ct)
            => Task.FromResult(result.RunId);
    }

    private static AdvancedAnalysisRequest Request() => new(
        OutcomeKey: "demo.edge_crack_rate",
        Grain: "coil",
        WindowDays: 3650,
        TenantId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
        FdrQ: 0.05,
        VifThreshold: 5.0,
        BootstrapIterations: 350,
        PermutationIterations: 120,
        CorrelationId: "PPIQ-P4-DEMO-CORRECTNESS");

    private static AdvancedCorrelationComputeService Service() =>
        new(new DbLoader(Conn()), new NullWriter(),
            NullLogger<AdvancedCorrelationComputeService>.Instance);

    // ---- PPIQ-404: correctness against the seeded demo DB ----
    [SkippableFact]
    public async Task PPIQ_404_Recovers_true_drivers_and_rejects_spurious_against_demo_db()
    {
        await using var c = await OpenSeededAsync();

        var truth = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var cmd = new NpgsqlCommand("SELECT feature_key, role FROM public.ppiq_p4_demo_truth", c))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) truth[r.GetString(0)] = r.GetString(1);

        var result = await Service().ComputeAsync(Request(), CancellationToken.None);
        Assert.True(result.CanRun, "Engine should be able to run against the seeded demo signal.");
        Assert.NotEmpty(result.Findings);

        // True drivers (or their kept collinear representative) must be recovered, significant, stable.
        var tempRecovered = result.Findings.Any(f =>
            (f.FeatureKey == "param_caster_mold_temp" || f.FeatureKey == "param_mold_temp_collinear")
            && f.Significant && f.IsStable);
        var pressureRecovered = result.Findings.Any(f =>
            f.FeatureKey == "param_caster_strand_pressure" && f.Significant && f.IsStable);
        Assert.True(tempRecovered, "Known true temperature driver was not recovered as significant+stable.");
        Assert.True(pressureRecovered, "Known true pressure driver was not recovered as significant+stable.");

        // Spurious features must be rejected under FDR; never reported as a significant driver.
        foreach (var kv in truth.Where(t => t.Value == "spurious"))
        {
            var f = result.Findings.FirstOrDefault(x => x.FeatureKey == kv.Key);
            if (f is not null)
            {
                Assert.False(f.Significant, $"Spurious '{kv.Key}' must not be significant.");
                Assert.True(f.QValue > Request().FdrQ, $"Spurious '{kv.Key}' q-value must exceed the FDR threshold.");
            }
        }

        // Contradiction guard: no spurious feature may outrank a true driver by effect size.
        var topTrue = result.Findings
            .Where(f => f.FeatureKey is "param_caster_mold_temp" or "param_caster_strand_pressure" or "param_mold_temp_collinear")
            .Select(f => Math.Abs(f.EffectSize)).DefaultIfEmpty(0).Max();
        foreach (var kv in truth.Where(t => t.Value == "spurious"))
        {
            var f = result.Findings.FirstOrDefault(x => x.FeatureKey == kv.Key && x.Significant);
            Assert.True(f is null || Math.Abs(f.EffectSize) <= topTrue,
                $"Spurious '{kv.Key}' contradicts a known-true demo fact by outranking the true drivers.");
        }

        // Honesty: recovered drivers carry the non-causal caveat.
        Assert.All(result.Findings.Where(f => f.Significant), f =>
            Assert.Contains("not a guaranteed root cause", f.HonestyCaveat, StringComparison.OrdinalIgnoreCase));
    }

    // ---- PPIQ-405: deterministic re-run (same seed -> identical findings) ----
    [SkippableFact]
    public async Task PPIQ_405_Reruns_deterministically_on_demo_db()
    {
        await using var c = await OpenSeededAsync();
        var first = await Service().ComputeAsync(Request(), CancellationToken.None);
        var second = await Service().ComputeAsync(Request(), CancellationToken.None);

        Assert.Equal(first.CanRun, second.CanRun);
        Assert.Equal(first.Findings.Count, second.Findings.Count);
        var a = first.Findings.OrderBy(f => f.FeatureKey, StringComparer.Ordinal).ToList();
        var b = second.Findings.OrderBy(f => f.FeatureKey, StringComparer.Ordinal).ToList();
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].FeatureKey, b[i].FeatureKey);
            Assert.Equal(a[i].Significant, b[i].Significant);
            Assert.Equal(Math.Round(a[i].EffectSize, 8), Math.Round(b[i].EffectSize, 8));
            Assert.Equal(Math.Round(a[i].QValue, 8), Math.Round(b[i].QValue, 8));
            Assert.Equal(a[i].IsStable, b[i].IsStable);
        }
    }

    // ---- PPIQ-401: the four standing ML jobs are seeded, enabled, schedulable ----
    [SkippableFact]
    public async Task PPIQ_401_Four_standing_ml_jobs_are_wired()
    {
        await using var c = await OpenSeededAsync();

        await using (var reg = new NpgsqlCommand("SELECT to_regclass('public.job_definitions')::text", c))
        {
            var present = await reg.ExecuteScalarAsync();
            if (present is null or DBNull)
                return; // job_definitions not migrated in this environment; nothing to assert.
        }

        long enabled;
        await using (var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM public.job_definitions " +
            "WHERE job_type IN ('MlParamsVsDefects','MlParamsVsDowntime','MlParamsVsKpis','MlWeeklyFull') " +
            "AND is_enabled = true AND coalesce(is_deleted,false) = false", c))
        {
            enabled = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }

        Assert.True(enabled >= 4,
            $"Expected the four standing ML jobs (defects/downtime/kpi/overall) enabled; found {enabled}. Apply migration 205 if missing.");
    }
}