using System;
using System.Threading;
using Npgsql;
using PlantProcess.Analytics.Engine;
using PlantProcess.Analytics.Engine.Postgres;
using PlantProcess.Application.Analytics.Contracts;
using Xunit;

namespace PlantProcess.Analytics.Engine.Tests;

public sealed class PostgresAdapterIntegrationTests
{
    // Set PPIQ_ANALYTICS_IT to a Postgres connection string (a DB with the demo ML feature/outcome data)
    // to exercise the adapters end-to-end. Without it, this test is a no-op (passes trivially).
    private static string? ConnectionString => Environment.GetEnvironmentVariable("PPIQ_ANALYTICS_IT");

    [Fact]
    public async System.Threading.Tasks.Task Managed_engine_runs_and_persists_against_real_database_when_configured()
    {
        var cs = ConnectionString;
        if (string.IsNullOrWhiteSpace(cs)) return; // no-op unless a DB is configured

        await using var dataSource = NpgsqlDataSource.Create(cs);
        var engine = new ManagedStatisticalComputeEngine(
            new PostgresCanonicalFeatureSource(dataSource),
            new PostgresAnalysisFindingSink(dataSource));

        var result = await engine.ComputeAsync(
            new CorrelationComputeRequest("defect.rate_per_m2", "coil", 3650),
            CancellationToken.None);

        Assert.Equal("managed-stat-v1", result.EngineKey);
        Assert.Contains(result.Status, new[] { "Ok", "Partial", "NoData", "Blocked" });
        Assert.NotEqual(Guid.Empty, result.ComputeRunId);
    }
}