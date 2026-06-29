using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PlantProcess.Application.Analytics.Advanced;
using PlantProcess.Infrastructure.Analytics;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Analytics;

/// <summary>
/// P3-05 persistence proof against a live PostgreSQL feature store. Requires
/// PPIQ_TEST_PG_CONNSTRING and a seeded demo outcome (PPIQ_TEST_OUTCOME_KEY,
/// default 'defect.edge_crack_rate'); skips cleanly otherwise.
/// </summary>
public sealed class AdvancedResultPersistenceTests
{
    private static string? Conn => Environment.GetEnvironmentVariable("PPIQ_TEST_PG_CONNSTRING");
    private static string OutcomeKey => Environment.GetEnvironmentVariable("PPIQ_TEST_OUTCOME_KEY") ?? "defect.edge_crack_rate";
    private static string Grain => Environment.GetEnvironmentVariable("PPIQ_TEST_GRAIN") ?? "coil";

    [SkippableFact]
    public async Task Persists_run_and_findings_tenant_scoped_with_provenance()
    {
        Skip.If(string.IsNullOrWhiteSpace(Conn), "Set PPIQ_TEST_PG_CONNSTRING to run this integration test.");

        await using var ds = NpgsqlDataSource.Create(Conn!);
        var tenant = Guid.NewGuid(); // unique so we can assert isolation of just our rows
        var loader = new NpgsqlFeatureVectorLoader(ds);
        var writer = new NpgsqlAdvancedResultWriter(ds);
        var service = new AdvancedCorrelationComputeService(loader, writer, NullLogger<AdvancedCorrelationComputeService>.Instance);

        var result = await service.ComputeAsync(
            new AdvancedAnalysisRequest(OutcomeKey, Grain, 3650, tenant, PermutationIterations: 80, BootstrapIterations: 200),
            CancellationToken.None);

        // run row persisted, tenant-scoped
        await using (var c = new NpgsqlCommand(
            "SELECT count(*) FROM public.ml_correlation_compute_runs WHERE id=@id AND tenant_id=@t", ds.CreateConnection()))
        {
            await c.Connection!.OpenAsync();
            await using (var _tctx = new NpgsqlCommand("SELECT set_config('app.current_tenant', @tg, false)", c.Connection))
            {
                _tctx.Parameters.AddWithValue("tg", tenant.ToString());
                await _tctx.ExecuteNonQueryAsync();
            }
            c.Parameters.AddWithValue("id", result.RunId);
            c.Parameters.AddWithValue("t", tenant);
            Assert.Equal(1L, (long)(await c.ExecuteScalarAsync())!);
        }

        if (result.CanRun && result.Findings.Count > 0)
        {
            await using var c = new NpgsqlCommand(
                @"SELECT count(*) FROM public.ml_correlation_results_v2
                  WHERE compute_run_id=@id AND tenant_id=@t AND evidence_json ? 'provenanceHandle'", ds.CreateConnection());
            await c.Connection!.OpenAsync();
            await using (var _tctx = new NpgsqlCommand("SELECT set_config('app.current_tenant', @tg, false)", c.Connection))
            {
                _tctx.Parameters.AddWithValue("tg", tenant.ToString());
                await _tctx.ExecuteNonQueryAsync();
            }
            c.Parameters.AddWithValue("id", result.RunId);
            c.Parameters.AddWithValue("t", tenant);
            var rows = (long)(await c.ExecuteScalarAsync())!;
            Assert.Equal(result.Findings.Count, (int)rows);
        }
    }
}
