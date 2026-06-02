using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Hosting;

/// <summary>T-028 drop 3: periodically refreshes the dashboard read models and records a JobRunHistory row.</summary>
public sealed class ReadModelRefreshHostedService : BackgroundService
{
    private const string JobCode = "read-model-refresh";
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReadModelRefreshHostedService> _logger;

    public ReadModelRefreshHostedService(IServiceScopeFactory scopeFactory, ILogger<ReadModelRefreshHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay so migrations/seeding settle first.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Scheduled read-model refresh failed."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantProcessDbContext>();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        var def = await db.JobDefinitions.FirstOrDefaultAsync(j => j.JobCode == JobCode, ct);
        if (def is null)
        {
            def = new JobDefinition(
                jobCode: JobCode,
                jobName: "Read Model Refresh",
                jobType: JobDefinitionType.CanonicalRefresh,
                scheduleExpression: "every-15-minutes",
                isSynthetic: false,
                description: "Refreshes mv_dashboard_* materialized views (T-028).");
            db.JobDefinitions.Add(def);
            await db.SaveChangesAsync(ct);
        }

        var history = new JobRunHistory(
            jobDefinitionId: def.Id,
            jobCode: JobCode,
            jobName: "Read Model Refresh",
            jobType: JobDefinitionType.CanonicalRefresh,
            triggerSource: "Scheduler",
            triggeredBy: null,
            correlationId: Guid.NewGuid().ToString("n"),
            isSynthetic: false,
            sourceSystem: null,
            sourceRecordId: null);
        db.JobRunHistories.Add(history);
        await db.SaveChangesAsync(ct);

        try
        {
            await using var cmd = dataSource.CreateCommand(
                "SELECT public.ppiq_refresh_read_models('dashboard', ARRAY[" +
                "'downtime_by_area','equipment_stoppage','daily_production'," +
                "'defect_by_product_family','parameter_by_grade','parameter_trend'])");
            var runId = await cmd.ExecuteScalarAsync(ct);
            history.MarkSucceeded($"Read models refreshed (refresh run {runId}).");
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            history.MarkFailed(ex.Message);
            await db.SaveChangesAsync(ct);
            throw;
        }
    }
}