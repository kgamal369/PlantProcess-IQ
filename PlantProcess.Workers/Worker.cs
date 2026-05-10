using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlantProcess.Workers;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PlantProcess IQ worker started. No ingestion job is active yet. Future Sprint 2 work should add import, mapping, synthetic generation and bulk-ingestion jobs here.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}