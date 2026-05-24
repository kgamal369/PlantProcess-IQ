// ============================================================
// File:    Backend/PlantProcess.Api/Endpoints/Diagnostics/DiagnosticsEndpoints.cs
// Task:    BE-HARD-001
// Purpose: Receive client-side error beacons from the React frontend
//          ErrorBoundary (FE-HARD-001) and log them to Serilog under
//          a dedicated category so they appear in operational logs.
//
//          The endpoint is intentionally:
//            - Anonymous (errors may occur before / during auth)
//            - Rate-limited (10 req/sec/IP) to prevent abuse
//            - Best-effort (always returns 204 — failure to log must
//              never propagate back to the client)
// ============================================================

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace PlantProcess.Api.Endpoints.Diagnostics;

public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/diagnostics")
            .WithTags("Diagnostics")
            .AllowAnonymous();

        group.MapPost("/client-error", RecordClientError)
            .RequireRateLimiting("client-error");

        return app;
    }

    /// <summary>
    /// Receive a client-side error beacon and log it.
    /// Always returns 204 — even if the payload is malformed, since
    /// the goal is observation, not validation.
    /// </summary>
    private static IResult RecordClientError(
        ClientErrorReport report,
        HttpContext httpContext,
        ILogger<DiagnosticsMarker> logger)
    {
        // Truncate fields defensively. We don't want a 1 MB stack trace to overwhelm the log.
        var safe = new
        {
            errorId         = Truncate(report.ErrorId,         100),
            message         = Truncate(report.Message,         500),
            stack           = Truncate(report.Stack,           4000),
            componentStack  = Truncate(report.ComponentStack,  4000),
            route           = Truncate(report.Route,           500),
            userAgent       = Truncate(report.UserAgent,       500),
            occurredAtUtc   = report.OccurredAtUtc,
            clientIp        = httpContext.Connection.RemoteIpAddress?.ToString(),
            correlationId   = httpContext.Items["CorrelationId"]?.ToString(),
        };

        logger.LogError(
            "ClientError: {ErrorId} on route {Route} — {Message}",
            safe.errorId, safe.route, safe.message);

        // Also log the full structured payload at Debug level so the
        // stack trace is recoverable when the operational logs are
        // configured to capture Debug.
        logger.LogDebug("ClientErrorDetails: {@Report}", safe);

        return Results.NoContent();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    // Marker type purely so ILogger<T> has a dedicated category name "ClientError"
    // (when configured) without polluting application namespaces.
    internal sealed class DiagnosticsMarker { }
}

public sealed record ClientErrorReport(
    [property: JsonPropertyName("errorId")]        string? ErrorId,
    [property: JsonPropertyName("message")]        string? Message,
    [property: JsonPropertyName("stack")]          string? Stack,
    [property: JsonPropertyName("componentStack")] string? ComponentStack,
    [property: JsonPropertyName("route")]          string? Route,
    [property: JsonPropertyName("userAgent")]      string? UserAgent,
    [property: JsonPropertyName("occurredAtUtc")]  string? OccurredAtUtc);
