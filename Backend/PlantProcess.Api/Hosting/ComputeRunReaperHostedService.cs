using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Hosting;

/// <summary>
/// Stuck-run reaper (minimum viable governance): any analytics run left in 'Running'
/// beyond the configured max runtime is transitioned to Failed(timeout) with an honest
/// message, so the Jobs Monitor and the run ledger never show phantom in-flight work.
/// Config: PlantProcess:Analytics:StuckRunMaxMinutes (default 30),
///         PlantProcess:Analytics:ReaperIntervalMinutes (default 5).
/// </summary>
public sealed class ComputeRunReaperHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComputeRunReaperHostedService> _logger;
    private readonly int _maxMinutes;
    private readonly TimeSpan _interval;

    public ComputeRunReaperHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ComputeRunReaperHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _maxMinutes = Math.Max(1, configuration.GetValue<int?>("PlantProcess:Analytics:StuckRunMaxMinutes") ?? 30);
        var intervalMinutes = Math.Max(1, configuration.GetValue<int?>("PlantProcess:Analytics:ReaperIntervalMinutes") ?? 5);
        _interval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Stuck-run reaper active. MaxRuntime={MaxMinutes}min Interval={IntervalMinutes}min",
            _maxMinutes,
            _interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReapOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stuck-run reaper tick failed; will retry next interval.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReapOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantProcessDbContext>();

        var computeReaped = await db.Database.ExecuteSqlRawAsync(
            "UPDATE public.ml_correlation_compute_runs " +
            "SET status = 'Failed', completed_at_utc = now(), " +
            "    message = left(coalesce(message || ' | ', '') || 'Failed(timeout): exceeded max runtime of ' || {0} || ' minutes (reaper)', 500) " +
            "WHERE status = 'Running' AND started_at_utc < now() - make_interval(mins => {0})",
            new object[] { _maxMinutes },
            ct);

        var learningReaped = await db.Database.ExecuteSqlRawAsync(
            "UPDATE public.ml_learning_runs_v1 " +
            "SET status = 'Failed', finished_at_utc = now(), " +
            "    error_message = 'Failed(timeout): exceeded max runtime of ' || {0} || ' minutes (reaper)' " +
            "WHERE status = 'Running' AND started_at_utc < now() - make_interval(mins => {0})",
            new object[] { _maxMinutes },
            ct);

        if (computeReaped > 0 || learningReaped > 0)
        {
            _logger.LogWarning(
                "Stuck-run reaper transitioned {ComputeReaped} compute run(s) and {LearningReaped} learning run(s) to Failed(timeout).",
            computeReaped,
            learningReaped);
        }
    }
}
