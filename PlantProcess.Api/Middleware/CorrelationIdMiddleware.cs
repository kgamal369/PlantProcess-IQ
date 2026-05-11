using Serilog.Context;

namespace PlantProcess.Api.Middleware;

/// <summary>
/// Middleware that reads or generates an X-Correlation-ID for every HTTP request,
/// propagates it to the Serilog LogContext so every log entry in the request scope
/// carries the same CorrelationId property, and echoes it back in the response header.
/// Must be registered BEFORE RequestResponseLoggingMiddleware in Program.cs.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    // ─── Constants ───────────────────────────────────────────────────────────
    private const string HeaderName = "X-Correlation-ID";
    private const string LogContextKey = "CorrelationId";

    // ─── Dependencies ─────────────────────────────────────────────────────────
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    // ─── Constructor ──────────────────────────────────────────────────────────
    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─── Pipeline ─────────────────────────────────────────────────────────────
    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Read existing header or generate a new compact GUID (no hyphens).
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        var isNew = string.IsNullOrWhiteSpace(correlationId);
        if (isNew)
        {
            correlationId = Guid.NewGuid().ToString("N");

            _logger.LogTrace(
                "No {Header} header found. Generated new CorrelationId={CorrelationId}",
                HeaderName,
                correlationId);
        }
        else
        {
            _logger.LogTrace(
                "Received {Header} header. CorrelationId={CorrelationId}",
                HeaderName,
                correlationId);
        }

        // 2. Echo it back in the response so callers can trace end-to-end.
        context.Response.Headers[HeaderName] = correlationId;

        // 3. Store in HttpContext.Items for downstream access (services, endpoints).
        context.Items[LogContextKey] = correlationId;

        // 4. Push into Serilog LogContext — all log entries within this request scope
        //    will automatically carry CorrelationId in their structured data.
        using (LogContext.PushProperty(LogContextKey, correlationId))
        {
            await _next(context);
        }
    }
}