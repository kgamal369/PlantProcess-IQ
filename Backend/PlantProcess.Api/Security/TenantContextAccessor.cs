using Microsoft.AspNetCore.Http;

namespace PlantProcess.Api.Security;

/// <summary>
/// PPIQ_REALIZATION_T021_TENANT_CONTEXT_ACCESSOR
/// Central tenant context accessor for Phase 04 multi-tenancy hardening.
/// </summary>
public interface ITenantContextAccessor
{
    Guid TenantId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid tenantId);
}

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private Guid? _tenantId;

    public Guid TenantId =>
        _tenantId ?? throw new UnauthorizedAccessException(TenantClaimReader.MissingTenantMessage);

    public bool HasTenant => _tenantId.HasValue && _tenantId.Value != Guid.Empty;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException(TenantClaimReader.MissingTenantMessage);

        _tenantId = tenantId;
    }
}

/// <summary>
/// PPIQ_REALIZATION_T021_TENANT_CONTEXT_MIDDLEWARE
/// Resolves tenant once per request for tenant-scoped API surfaces.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext http, ITenantContextAccessor tenantContext)
    {
        if (RequiresTenant(http))
        {
            var tenantId = TenantClaimReader.ResolveRequiredTenantId(http);
            tenantContext.SetTenant(tenantId);
            http.Items["PPIQ_TENANT_ID"] = tenantId;
        }

        await _next(http);
    }

    private static bool RequiresTenant(HttpContext http)
    {
        var path = http.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.Contains("/auth", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.Contains("/health", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.Contains("/swagger", StringComparison.OrdinalIgnoreCase))
            return false;

        return path.Contains("/admin", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/v5", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/tenant", StringComparison.OrdinalIgnoreCase);
    }
}
