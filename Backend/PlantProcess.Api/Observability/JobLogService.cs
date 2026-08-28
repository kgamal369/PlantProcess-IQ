using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;
using Serilog;

namespace PlantProcess.Api.Observability;

/// <summary>
/// Customer-oriented job event log (V1-45): one write lands the event in the job_log
/// table (HMI log panel + admin API) AND emits a Serilog event carrying JobLog=true,
/// which the filtered sub-logger mirrors into logs/joblog_yyyyMMddHH.log.
/// </summary>
public interface IJobLogService
{
    Task WriteAsync(
        string jobType,
        string jobName,
        Guid? runId,
        string severity,
        string message,
        object? context,
        CancellationToken cancellationToken);
}

public sealed class JobLogService : IJobLogService
{
    private readonly PlantProcessDbContext _dbContext;

    public JobLogService(PlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(
        string jobType,
        string jobName,
        Guid? runId,
        string severity,
        string message,
        object? context,
        CancellationToken cancellationToken)
    {
        var contextJson = context is null ? "{}" : JsonSerializer.Serialize(context);

        await _dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO ppiq_meta.job_log (job_type, job_name, run_id, severity, message, context) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}::jsonb)",
            new object?[] { jobType, jobName, runId, severity, message, contextJson },
            cancellationToken);

        var level = severity switch
        {
            "Error" => Serilog.Events.LogEventLevel.Error,
            "Warning" => Serilog.Events.LogEventLevel.Warning,
            _ => Serilog.Events.LogEventLevel.Information,
        };

        Log.ForContext("JobLog", true)
            .ForContext("JobType", jobType)
            .ForContext("JobName", jobName)
            .ForContext("JobRunId", runId)
            .Write(level, "{JobType} {JobName}: {JobMessage}", jobType, jobName, message);
    }
}
