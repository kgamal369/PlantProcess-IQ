using PlantProcess.Application.Security.Tenancy;

namespace PlantProcess.Api.Security;

/// <summary>P2-01. Resolves the current tenant from the authenticated principal's tenant_id claim.</summary>
public sealed class HttpTenantAccessor : ITenantAccessor
{
    private readonly IHttpContextAccessor _http;
    public HttpTenantAccessor(IHttpContextAccessor http) => _http = http;

    public bool TryGetTenantId(out System.Guid tenantId) =>
        TenantClaims.TryResolve(_http.HttpContext?.User, out tenantId);

    public System.Guid TenantId => TenantClaims.Resolve(_http.HttpContext?.User);
}
