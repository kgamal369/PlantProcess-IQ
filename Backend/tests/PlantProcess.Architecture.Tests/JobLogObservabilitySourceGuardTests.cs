using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// V1-44 / V1-45 source guards: the job-event log service + endpoint filter exist, the
/// import endpoints are wrapped by the filter, the admin job-logs endpoint is present, and
/// Serilog is configured for hourly system + job log files. Locks the observability wiring.
/// </summary>
public sealed class JobLogObservabilitySourceGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
        {
            dir = dir.Parent;
        }
        Assert.True(dir is not null, "Could not locate repo root.");
        return dir!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

    [Fact]
    public void Job_log_service_and_filter_exist()
    {
        var svc = Read("Backend", "PlantProcess.Api", "Observability", "JobLogService.cs");
        Assert.Contains("interface IJobLogService", svc);
        Assert.Contains("INSERT INTO public.job_log", svc);
        Assert.Contains("\"JobLog\"", svc);

        var filter = Read("Backend", "PlantProcess.Api", "Observability", "JobLogEndpointFilter.cs");
        Assert.Contains("IEndpointFilter", filter);
        Assert.Contains("Started", filter);
        Assert.Contains("Completed", filter);
        Assert.Contains("Failed", filter);
    }

    [Fact]
    public void Import_endpoints_are_wrapped_by_the_job_log_filter()
    {
        var two = Read("Backend", "PlantProcess.Api", "Endpoints", "Admin", "TwoStageImportEndpoints.cs");
        Assert.Contains("JobLogEndpointFilter(\"Import-Stage1\")", two);
        Assert.Contains("JobLogEndpointFilter(\"Import-Stage2\")", two);
    }

    [Fact]
    public void Admin_job_logs_endpoint_and_hourly_sinks_are_configured()
    {
        var admin = Read("Backend", "PlantProcess.Api", "Endpoints", "Admin", "AdminEndpoints.cs");
        Assert.Contains("/job-logs", admin);
        Assert.Contains("GetJobLogsAsync", admin);

        var program = Read("Backend", "PlantProcess.Api", "Program.cs");
        Assert.Contains("systemlog_.log", program);
        Assert.Contains("joblog_.log", program);
        Assert.Contains("RollingInterval.Hour", program);
        Assert.Contains("IJobLogService", program);
    }

    [Fact]
    public void Job_log_schema_script_exists_with_indexes()
    {
        var sql = Read("Backend", "database", "scripts", "252_job_event_log.sql");
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.job_log", sql);
        Assert.Contains("ix_job_log_occurred", sql);
        Assert.Contains("ix_job_log_type_severity", sql);
        Assert.Contains("severity IN ('Info', 'Warning', 'Error')", sql);
    }
}
