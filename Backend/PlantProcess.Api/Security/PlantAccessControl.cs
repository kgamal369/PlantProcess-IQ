using System.Security.Claims;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;

namespace PlantProcess.Api.Security;

public static class PlantRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantOwner = "TenantOwner";
    public const string PlantAdmin = "PlantAdmin";
    public const string DataEngineer = "DataEngineer";
    public const string ProcessEngineer = "ProcessEngineer";
    public const string QualityEngineer = "QualityEngineer";
    public const string ReliabilityEngineer = "ReliabilityEngineer";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
    public const string CommercialAdmin = "CommercialAdmin";
    public const string Support = "Support";

    public static string NormalizePlantRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return Viewer;

        return role.Trim() switch
        {
            "Admin" => SuperAdmin,
            "DataManager" => DataEngineer,
            "Engineer" => ProcessEngineer,
            "Viewer" => Viewer,
            "SuperAdmin" => SuperAdmin,
            "TenantOwner" => TenantOwner,
            "PlantAdmin" => PlantAdmin,
            "DataEngineer" => DataEngineer,
            "ProcessEngineer" => ProcessEngineer,
            "QualityEngineer" => QualityEngineer,
            "ReliabilityEngineer" => ReliabilityEngineer,
            "Operator" => Operator,
            "CommercialAdmin" => CommercialAdmin,
            "Support" => Support,
            _ => Viewer
        };
    }

    public static string ToCompatibilityRole(string plantRole)
    {
        return plantRole switch
        {
            SuperAdmin or TenantOwner or PlantAdmin or CommercialAdmin => "Admin",
            DataEngineer => "DataManager",
            ProcessEngineer or QualityEngineer or ReliabilityEngineer or Operator => "Engineer",
            _ => "Viewer"
        };
    }
}

public sealed record EffectiveEntitlementDto(
    string TenantId,
    string TenantCode,
    string Role,
    string CompatibilityRole,
    string Tier,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> Features,
    IReadOnlyDictionary<string, string> LockReasons);

public interface IPlantEntitlementResolver
{
    EffectiveEntitlementDto Resolve(ClaimsPrincipal user);
    bool HasPermission(ClaimsPrincipal user, string permissionCode);
}

public sealed class PlantEntitlementResolver : IPlantEntitlementResolver
{
    private readonly ILicenseService _licenseService;

    private static readonly IReadOnlyDictionary<string, string[]> PermissionsByRole =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [PlantRoles.SuperAdmin] = new[]
            {
                "system.admin","tenant.admin","license.admin","user.admin","source.configure","mapping.configure",
                "job.manage","page.design","widget.design","analysis.execute","suggestion.workflow",
                "assistant.use","audit.read","report.export"
            },
            [PlantRoles.TenantOwner] = new[]
            {
                "tenant.admin","license.admin","user.admin","source.configure","mapping.configure","job.manage",
                "page.design","widget.design","analysis.execute","suggestion.workflow","assistant.use",
                "audit.read","report.export"
            },
            [PlantRoles.PlantAdmin] = new[]
            {
                "source.configure","mapping.configure","job.manage","page.design","widget.design",
                "analysis.execute","suggestion.workflow","assistant.use","audit.read","report.export"
            },
            [PlantRoles.DataEngineer] = new[]
            {
                "source.configure","mapping.configure","job.manage","page.design","widget.design",
                "analysis.execute","report.export"
            },
            [PlantRoles.ProcessEngineer] = new[]
            {
                "analysis.execute","page.design","widget.design","suggestion.workflow","assistant.use","report.export"
            },
            [PlantRoles.QualityEngineer] = new[]
            {
                "analysis.execute","page.design","widget.design","suggestion.workflow","assistant.use","report.export"
            },
            [PlantRoles.ReliabilityEngineer] = new[]
            {
                "analysis.execute","assistant.use","report.export"
            },
            [PlantRoles.Operator] = new[] { "analysis.execute" },
            [PlantRoles.Viewer] = new[] { "assistant.use" },
            [PlantRoles.CommercialAdmin] = new[] { "license.admin" },
            [PlantRoles.Support] = new[] { "audit.read" }
        };

    public PlantEntitlementResolver(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    public bool HasPermission(ClaimsPrincipal user, string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return false;
        var role = ResolveRole(user);
        return PermissionsByRole.TryGetValue(role, out var permissions) &&
               permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    public EffectiveEntitlementDto Resolve(ClaimsPrincipal user)
    {
        var role = ResolveRole(user);
        var compatibilityRole = PlantRoles.ToCompatibilityRole(role);
        var permissions = PermissionsByRole.TryGetValue(role, out var p)
            ? p.OrderBy(x => x).ToArray()
            : Array.Empty<string>();

        var features = Enum.GetValues<LicenseFeature>()
            .Where(_licenseService.IsFeatureEnabled)
            .Select(x => x.ToString())
            .OrderBy(x => x)
            .ToArray();

        var lockReasons = Enum.GetValues<LicenseFeature>()
            .Where(x => !_licenseService.IsFeatureEnabled(x))
            .ToDictionary(
                x => x.ToString(),
                x => $"Requires {_licenseService.GetRequiredTierForFeature(x)} license tier.");

        return new EffectiveEntitlementDto(
            TenantId: PlantProcess.Application.Security.Tenancy.TenantClaims.Resolve(user).ToString(),
            TenantCode: user.FindFirstValue("tenant_code") ?? "default-demo",
            Role: role,
            CompatibilityRole: compatibilityRole,
            Tier: _licenseService.GetCurrentTier().ToString(),
            Permissions: permissions,
            Features: features,
            LockReasons: lockReasons);
    }

    private static string ResolveRole(ClaimsPrincipal user)
    {
        return PlantRoles.NormalizePlantRole(
            user.FindFirstValue("ppiq_role") ??
            user.FindFirstValue(ClaimTypes.Role) ??
            user.FindFirstValue("role"));
    }
}

public sealed class AccessControlMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AccessControlMiddleware> _logger;

    private static readonly (string Prefix, string[] Methods, string Permission, bool Anonymous)[] Matrix =
    {
        ("/auth/login", new[] { "POST" }, "anonymous", true),
        ("/auth/refresh", new[] { "POST" }, "anonymous", true),
        ("/auth/logout", new[] { "POST" }, "anonymous", true),
        // PPIQ-T021: middleware-anonymous like /auth/refresh - the endpoint itself
        // enforces authentication via RequireAuthorization(); step-up additionally
        // demands a recent successful mfa_verify audit event before minting mfa=true.
        ("/auth/mfa/step-up", new[] { "POST" }, "anonymous", true),
        ("/auth/provisioning", new[] { "GET", "POST" }, "anonymous", true),
        ("/swagger", new[] { "GET" }, "anonymous", true),
        ("/", new[] { "GET" }, "anonymous", true),

        ("/admin/license", All(), "license.admin", false),
            ("/api/v5/licensing/ed25519", All(), "license.admin", false),
        ("/api/v5/licensing", All(), "license.admin", false),
        ("/admin", All(), "tenant.admin", false),
        ("/integration", All(), "source.configure", false),
        ("/workflow", All(), "job.manage", false),
        ("/analytics/correlations", new[] { "GET", "POST" }, "analysis.execute", false),
        ("/analytics/ml", new[] { "GET", "POST" }, "analysis.execute", false),
        ("/analytics/phase2", All(), "analysis.execute", false),
        ("/api/ml/learning", new[] { "GET", "POST" }, "job.manage", false),
        // M1-21: the ML foundation group (feature store + correlation engine).
        // Without this line the middleware denies every POST to
        // /api/ml/foundation/compute/correlation and /feature-store/refresh
        // ("not mapped in the P01/P02 permission matrix"), while the GET
        // readiness/outcomes calls slip through anonymously via ("/", GET).
        // Consequence: the correlation engine cannot be invoked at all and
        // journey step 9 (Run) is undemonstrable. analysis.execute is the
        // permission already carried by /analytics/correlations and
        // /analytics/ml - the sibling engine routes.
        ("/api/ml/foundation", All(), "analysis.execute", false),
        ("/api/analysis-jobs", All(), "job.manage", false),
        ("/api/alerts", All(), "job.manage", false),
        ("/api/supervisor", All(), "job.manage", false),
        // M1-07: the assistant group. Without this line the middleware denies every
        // POST to /api/assistant/ask ("not mapped in the P01/P02 permission matrix"),
        // while GETs slip through anonymously via the ("/", GET, anonymous) entry.
        // assistant.use is an existing permission, already carried by /analytics/dashboard.
        ("/api/assistant", All(), "assistant.use", false),
        // M1-22: the suggestions group (step-13 page). Without this the middleware
        // denies POST/GET to /api/suggestions ("not mapped"), surfacing the red
        // "SUGGESTION RUNTIME NOT REACHABLE: 401" banner. analysis.execute matches
        // the sibling analytics routes; adjust if the page role-gates differently.
        ("/api/suggestions", All(), "analysis.execute", false),
        ("/api/prep/visual-mapper", All(), "analysis.execute", false),
        ("/analytics/dashboard/definitions", All(), "page.design", false),
        ("/analytics/dashboard", new[] { "GET", "POST" }, "assistant.use", false),
        ("/page-definitions", All(), "page.design", false),
        ("/materials", new[] { "GET", "POST" }, "analysis.execute", false),
        ("/process", new[] { "GET", "POST" }, "analysis.execute", false),
        ("/quality", new[] { "GET", "POST" }, "analysis.execute", false),
        ("/data-quality", new[] { "GET", "POST" }, "analysis.execute", false),
        ("/reporting", All(), "report.export", false),
        ("/demo-lifecycle", new[] { "GET" }, "assistant.use", false),
        ("/dynamic-content", new[] { "GET" }, "assistant.use", false),
        ("/health", new[] { "GET" }, "anonymous", true),
        ("/db-health", new[] { "GET" }, "anonymous", true),
        ("/health/ready", new[] { "GET" }, "anonymous", true)
    };

    public AccessControlMiddleware(
        RequestDelegate next,
        ILogger<AccessControlMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IPlantEntitlementResolver resolver)
    {
        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method.ToUpperInvariant();

        var entry = Matrix
            .OrderByDescending(x => x.Prefix.Length)
            .FirstOrDefault(x =>
                path.StartsWith(x.Prefix, StringComparison.OrdinalIgnoreCase) &&
                x.Methods.Contains(method, StringComparer.OrdinalIgnoreCase));

        if (entry.Prefix is null)
        {
            _logger.LogWarning("Deny-by-default RBAC block for unmapped endpoint {Method} {Path}", method, path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Endpoint is not mapped in the P01/P02 permission matrix.",
                method,
                path
            });
            return;
        }

        if (entry.Anonymous)
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var routeTenantId = context.Request.Query["tenantId"].FirstOrDefault()
            ?? context.Request.RouteValues["tenantId"]?.ToString();

        var tokenTenantId = context.User.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(tokenTenantId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "No tenant claim on the authenticated principal.", error = "no_tenant" });
            return;
        }

        if (!string.IsNullOrWhiteSpace(routeTenantId) &&
            !string.Equals(routeTenantId, tokenTenantId, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Cross-tenant access blocked.",
                tenant = tokenTenantId
            });
            return;
        }

        if (!resolver.HasPermission(context.User, entry.Permission))
        {
            var entitlement = resolver.Resolve(context.User);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Permission denied.",
                requiredPermission = entry.Permission,
                role = entitlement.Role,
                tier = entitlement.Tier
            });
            return;
        }

        await _next(context);
    }

    private static string[] All() => new[] { "GET", "POST", "PUT", "PATCH", "DELETE" };
}

public static class AccessControlApplicationBuilderExtensions
{
    public static IApplicationBuilder UsePlantProcessAccessControl(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AccessControlMiddleware>();
    }
}


