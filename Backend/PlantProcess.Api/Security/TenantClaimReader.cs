namespace PlantProcess.Api.Security;

/// <summary>
/// PPIQ_REALIZATION_T003_STRICT_TENANT_RESOLUTION
/// Central tenant resolver for API endpoints.
///
/// Guardrail:
/// - Missing tenant claim/header is rejected.
/// - No silent demo-tenant fallback is allowed.
/// - This is intentionally strict for production safety.
/// </summary>
public static class TenantClaimReader
{
    public const string MissingTenantMessage =
        "PPIQ-T003: tenant claim/header is required; silent demo-tenant fallback is disabled.";

    public static Guid ResolveRequiredTenantId(HttpContext http)
    {
        var raw =
            http.User.FindFirst("tenant_id")?.Value ??
            http.User.FindFirst("tenantId")?.Value ??
            http.User.FindFirst("tid")?.Value ??
            http.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value ??
            http.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (Guid.TryParse(raw, out var tenantId) && tenantId != Guid.Empty)
            return tenantId;

        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        throw new UnauthorizedAccessException(MissingTenantMessage);
    }
}
