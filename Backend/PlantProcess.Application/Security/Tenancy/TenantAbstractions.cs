using System;
using System.Security.Claims;

namespace PlantProcess.Application.Security.Tenancy;

/// <summary>
/// P2-01. Thrown when the caller has no valid tenant_id claim. Derives from
/// ArgumentException so the Phase-1 global exception handler maps it to a
/// 400 application/problem+json (matching the existing 'no_tenant' contract).
/// </summary>
public sealed class TenantResolutionException : ArgumentException
{
    public TenantResolutionException(string message = "No valid tenant_id claim on the caller.") : base(message) { }
}

/// <summary>The single authority for resolving the current tenant. Fail-closed.</summary>
public interface ITenantAccessor
{
    /// <summary>Current tenant; throws TenantResolutionException if absent/malformed.</summary>
    Guid TenantId { get; }

    bool TryGetTenantId(out Guid tenantId);
}

/// <summary>
/// P2-01 canonical claim reader. The four scattered TryTenant helpers and the
/// FindFirstValue("tenant_id") ?? default fallbacks should ALL converge here.
/// </summary>
public static class TenantClaims
{
    public const string ClaimType = "tenant_id";

    public static bool TryResolve(ClaimsPrincipal? user, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        if (user is null) return false;
        var raw = user.FindFirst(ClaimType)?.Value ?? user.FindFirst("tenant")?.Value;
        return Guid.TryParse(raw, out tenantId);
    }

    public static Guid Resolve(ClaimsPrincipal? user)
    {
        if (!TryResolve(user, out var t))
            throw new TenantResolutionException();
        return t;
    }
}

/// <summary>
/// P2-03 defense-in-depth for hand-written tenant-scoped SQL. Call AssertScoped on the
/// SQL text before executing a tenant query; it fails loudly if the predicate is missing.
/// </summary>
public static class TenantSql
{
    public static string AssertScoped(string sql)
    {
        if (sql is null || sql.IndexOf("tenant_id", StringComparison.OrdinalIgnoreCase) < 0)
            throw new InvalidOperationException("Tenant-scoped query is missing a tenant_id predicate.");
        return sql;
    }
}
