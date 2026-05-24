// ============================================================
// File:    Backend/PlantProcess.Api/Middleware/AuditLogMiddleware.cs
// Task:    BE-ADD-001
//
// Wraps every request matching an audited endpoint prefix and writes
// one audit row after the response has been produced. Audit writes
// happen out-of-band (separate DbContext) so they never affect the
// request's own transaction.
//
// Configurable allow-list of audited prefixes — only sensitive
// endpoints are audited. Public endpoints (/health, /diagnostics, etc.)
// are not.
// ============================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Audit;

namespace PlantProcess.Api.Middleware;

public sealed class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLogMiddleware> _logger;

    /// <summary>
    /// Endpoint prefixes whose requests are audited. Add new prefixes
    /// here as new resource-bearing endpoint groups are introduced.
    /// </summary>
    private static readonly string[] AuditedPrefixes =
    {
        "/admin",
        "/integration",
        "/workflow",
        "/license",
        "/auth",
        "/dashboards",
        "/correlations",
        "/risk",
        "/data-quality",
        "/demo-lifecycle",
        "/connection-profiles",
        "/source-datasets",
        "/schema-views",
        "/kpi-definitions",
    };

    public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider services)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Quick reject — non-audited endpoints continue without overhead
        if (!ShouldAudit(path))
        {
            await _next(context);
            return;
        }

        Exception? thrown = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            thrown = ex;
            throw;
        }
        finally
        {
            // Capture status & user info NOW — they will be wiped when the
            // request scope is disposed.
            var statusCode = context.Response.StatusCode;
            var category = MapActionCategory(context.Request.Method);
            var outcome = MapOutcome(thrown, statusCode);

            var userId = ExtractUserId(context.User);
            var userName = ExtractUserName(context.User);
            var resourceId = ExtractResourceId(context);
            var resourceType = ExtractResourceType(path);

            var auditContext = new AuditLogContext(
                HttpMethod:     context.Request.Method,
                Endpoint:       path,
                ActionCategory: category,
                OutcomeStatus:  outcome,
                UserId:         userId,
                UserName:       userName,
                ResourceType:   resourceType,
                ResourceId:     resourceId,
                ClientIp:       context.Connection.RemoteIpAddress?.ToString(),
                UserAgent:      context.Request.Headers.UserAgent.ToString(),
                CorrelationId:  context.Items.TryGetValue("CorrelationId", out var cid) ? cid?.ToString() : null,
                HttpStatusCode: statusCode,
                MetadataJson:   null
            );

            // Fire-and-forget on a background task using the root provider
            // (NOT the request scope) so the write outlives the request.
            // We do this synchronously-ish but in a try-block so any failure
            // is swallowed.
            try
            {
                var auditService = services.GetRequiredService<IAuditLogService>();
                await auditService.RecordAsync(auditContext, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "AuditLogMiddleware: failed to record audit entry for {Method} {Path}",
                    context.Request.Method, path);
            }
        }
    }

    // ───────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────

    private static bool ShouldAudit(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        foreach (var prefix in AuditedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string MapActionCategory(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET"    => "Read",
            "HEAD"   => "Read",
            "OPTIONS"=> "Read",
            "DELETE" => "Delete",
            _        => "Write",
        };
    }

    private static string MapOutcome(Exception? thrown, int statusCode)
    {
        if (thrown is not null) return "Failed";
        return statusCode switch
        {
            >= 200 and < 300 => "Success",
            401              => "Unauthenticated",
            403              => "Forbidden",
            404              => "NotFound",
            409              => "Conflict",
            >= 400 and < 500 => "ClientError",
            >= 500           => "ServerError",
            _                => "Unknown",
        };
    }

    private static string? ExtractUserId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.Identity.Name;
    }

    private static string? ExtractUserName(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;
        return user.Identity.Name
            ?? user.FindFirstValue(ClaimTypes.Name);
    }

    private static string? ExtractResourceId(HttpContext context)
    {
        // Endpoints commonly use {id} as the resource route value
        if (context.Request.RouteValues.TryGetValue("id", out var id) && id is not null)
            return id.ToString();

        // Some endpoints use entity-specific names
        foreach (var key in new[] { "materialUnitId", "connectionProfileId", "dashboardDefinitionId", "userId" })
        {
            if (context.Request.RouteValues.TryGetValue(key, out var val) && val is not null)
                return val.ToString();
        }

        return null;
    }

    private static string? ExtractResourceType(string path)
    {
        // Take the first non-empty path segment
        if (string.IsNullOrEmpty(path)) return null;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        return parts[0].ToLowerInvariant() switch
        {
            "admin"            => "Admin",
            "integration"      => "Integration",
            "workflow"         => "Workflow",
            "license"          => "License",
            "auth"             => "Auth",
            "dashboards"       => "Dashboard",
            "correlations"     => "Correlation",
            "risk"             => "RiskScore",
            "data-quality"     => "DataQuality",
            "demo-lifecycle"   => "DemoLifecycle",
            "connection-profiles" => "ConnectionProfile",
            "source-datasets"  => "SourceDataset",
            "schema-views"     => "SchemaView",
            "kpi-definitions"  => "KpiDefinition",
            _ => parts[0],
        };
    }
}

// Extension method to register the middleware fluently
public static class AuditLogMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLog(this IApplicationBuilder app)
        => app.UseMiddleware<AuditLogMiddleware>();
}
