using System.Diagnostics;

namespace PlantProcess.Api.Middleware;

public sealed class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        var requestId = context.TraceIdentifier;
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "";
        var queryString = context.Request.QueryString.HasValue
            ? context.Request.QueryString.Value
            : "";

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = requestId,
            ["TraceIdentifier"] = requestId,
            ["HttpMethod"] = method,
            ["HttpPath"] = path
        });

        try
        {
            _logger.LogTrace(
                "HTTP request trace started. RequestId={RequestId}, Method={Method}, Path={Path}, QueryString={QueryString}",
                requestId,
                method,
                path,
                queryString);

            _logger.LogDebug(
                "HTTP request received. RequestId={RequestId}, Method={Method}, Path={Path}, QueryString={QueryString}, ContentType={ContentType}, ContentLength={ContentLength}",
                requestId,
                method,
                path,
                queryString,
                context.Request.ContentType,
                context.Request.ContentLength);

            await _next(context);

            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;

            if (statusCode >= 500)
            {
                _logger.LogError(
                    "HTTP request completed with server error. RequestId={RequestId}, Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                    requestId,
                    method,
                    path,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            else if (statusCode >= 400)
            {
                _logger.LogWarning(
                    "HTTP request completed with client error. RequestId={RequestId}, Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                    requestId,
                    method,
                    path,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            else if (stopwatch.ElapsedMilliseconds >= 3000)
            {
                _logger.LogWarning(
                    "Slow HTTP request completed. RequestId={RequestId}, Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                    requestId,
                    method,
                    path,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogInformation(
                    "HTTP request completed. RequestId={RequestId}, Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                    requestId,
                    method,
                    path,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);
            }

            _logger.LogTrace(
                "HTTP request trace finished. RequestId={RequestId}, Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                requestId,
                method,
                path,
                statusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Unhandled exception during HTTP request. RequestId={RequestId}, Method={Method}, Path={Path}, ElapsedMs={ElapsedMs}",
                requestId,
                method,
                path,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}