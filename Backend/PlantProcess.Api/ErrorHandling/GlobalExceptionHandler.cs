using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlantProcess.Api.ErrorHandling;

/// <summary>
/// P1-01: global RFC-7807 exception handler. Maps any unhandled exception to an
/// application/problem+json response carrying type/title/status/traceId. In
/// non-Development environments 'detail' and the stack trace are suppressed.
/// Wired via builder.Services.AddExceptionHandler&lt;GlobalExceptionHandler&gt;()
/// + AddProblemDetails() + app.UseExceptionHandler().
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _env;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IHostEnvironment env, ILogger<GlobalExceptionHandler> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        // Client went away mid-request: nothing useful to write.
        if (ex is OperationCanceledException && ctx.RequestAborted.IsCancellationRequested)
            return true;

        var (status, title, code) = Map(ex);

        var correlationId =
            ctx.Items.TryGetValue("CorrelationId", out var v) && v is string s && !string.IsNullOrWhiteSpace(s)
                ? s
                : ctx.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Status   = status,
            Title    = title,
            Type     = $"urn:ppiq:error:{code}",
            Instance = ctx.Request?.Path.Value,
            Detail   = _env.IsDevelopment() ? ex.Message : null
        };
        problem.Extensions["errorCode"] = code;
        problem.Extensions["traceId"]   = correlationId;
        if (_env.IsDevelopment())
            problem.Extensions["exception"] = ex.ToString();

        _logger.LogError(ex,
            "Unhandled exception mapped to {Status} {Code} (traceId {TraceId})",
            status, code, correlationId);

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync<ProblemDetails>(
            problem, (JsonSerializerOptions?)null, "application/problem+json", ct);

        return true;
    }

    private static (int Status, string Title, string Code) Map(Exception ex) => ex switch
    {
        BadHttpRequestException b      => (b.StatusCode, "Bad request", "bad_request"),
        ValidationException            => (400, "Validation failed", "validation"),
        ArgumentException              => (400, "Invalid request", "validation"),
        KeyNotFoundException           => (404, "Resource not found", "not_found"),
        UnauthorizedAccessException    => (403, "Forbidden", "forbidden"),
        TimeoutException               => (504, "Upstream timeout", "timeout"),
        OperationCanceledException     => (504, "Request timed out", "timeout"),
        _                              => (500, "An unexpected error occurred", "internal")
    };
}
