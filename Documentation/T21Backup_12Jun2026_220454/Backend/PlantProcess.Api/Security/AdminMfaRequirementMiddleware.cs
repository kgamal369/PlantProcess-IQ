namespace PlantProcess.Api.Security;

/// <summary>
/// PPIQ_REALIZATION_T009_ADMIN_MFA_REQUIRED
/// Enforces MFA for privileged/admin API surfaces.
///
/// MFA proof is accepted from:
/// - a verified MFA claim: mfa=true / mfa_verified=true / amr containing "mfa"
/// - a transitional integration-test header: X-PPIQ-MFA-Verified=true
///   (honored ONLY outside Production; hard-blocked in Production so it can never
///   be replayed against a live deployment)
/// </summary>
public sealed class AdminMfaRequirementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public AdminMfaRequirementMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresAdminMfa(context) && !HasMfaProof(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "admin_mfa_required",
                message = "PPIQ-T009: Admin endpoints require verified MFA."
            });
            return;
        }

        await _next(context);
    }

    private static bool RequiresAdminMfa(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.Contains("/auth/", StringComparison.OrdinalIgnoreCase))
            return false;

        return path.Contains("/admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v5/enterprise-identity", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v5/enterprise-sso-scim", StringComparison.OrdinalIgnoreCase);
    }

    private bool HasMfaProof(HttpContext context)
    {
        static bool IsTrue(string? value) =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "mfa", StringComparison.OrdinalIgnoreCase);

        // PPIQ-T009 transitional integration-test bypass.
        // Never honored in Production deployments.
        if (!_environment.IsProduction()
            && IsTrue(context.Request.Headers["X-PPIQ-MFA-Verified"].FirstOrDefault()))
            return true;

        foreach (var claim in context.User.Claims)
        {
            if (string.Equals(claim.Type, "mfa", StringComparison.OrdinalIgnoreCase) && IsTrue(claim.Value))
                return true;

            if (string.Equals(claim.Type, "mfa_verified", StringComparison.OrdinalIgnoreCase) && IsTrue(claim.Value))
                return true;

            if (string.Equals(claim.Type, "amr", StringComparison.OrdinalIgnoreCase)
                && claim.Value.Contains("mfa", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
