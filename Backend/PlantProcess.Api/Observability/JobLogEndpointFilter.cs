using System.Diagnostics;
using System.Reflection;

namespace PlantProcess.Api.Observability;

/// <summary>
/// Endpoint filter that turns any job-style endpoint into a job_log event stream:
/// Started before execution, Completed with duration on success, Failed with the
/// error message on exception (rethrown - behavior is never altered).
/// </summary>
public sealed class JobLogEndpointFilter : IEndpointFilter
{
    private readonly string _jobType;

    public JobLogEndpointFilter(string jobType)
    {
        _jobType = jobType;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var jobLog = services.GetService<IJobLogService>();
        var ct = context.HttpContext.RequestAborted;

        var jobName = _jobType;
        foreach (var arg in context.Arguments)
        {
            var prop = arg?.GetType().GetProperty("RequestedBy", BindingFlags.Public | BindingFlags.Instance);
            var requestedBy = prop?.GetValue(arg) as string;
            if (!string.IsNullOrWhiteSpace(requestedBy))
            {
                jobName = _jobType + " (" + requestedBy + ")";
                break;
            }
        }

        if (jobLog is not null)
        {
            await jobLog.WriteAsync(_jobType, jobName, null, "Info", "Started", new { path = context.HttpContext.Request.Path.Value }, ct);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next(context);
            sw.Stop();
            if (jobLog is not null)
            {
                await jobLog.WriteAsync(_jobType, jobName, null, "Info", "Completed in " + sw.ElapsedMilliseconds + " ms", new { durationMs = sw.ElapsedMilliseconds }, ct);
            }
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (jobLog is not null)
            {
                await jobLog.WriteAsync(_jobType, jobName, null, "Error", "Failed after " + sw.ElapsedMilliseconds + " ms: " + ex.Message, new { durationMs = sw.ElapsedMilliseconds, error = ex.GetType().Name }, CancellationToken.None);
            }
            throw;
        }
    }
}
