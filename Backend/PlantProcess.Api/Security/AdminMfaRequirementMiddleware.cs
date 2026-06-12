using Microsoft.Extensions.Options;

namespace PlantProcess.Api.Security;

/// <summary>
/// PPIQ_REALIZATION_T009_ADMIN_MFA_REQUIRED + PPIQ-T021 enforcement flag.
/// Enforces MFA for privileged/admin API surfaces WHEN AuthOptions.RequireAdminMfa is true.
/// MFA proof: claim mfa=true / mfa_verified=true / amr contains "mfa" (minted by
/// POST /auth/mfa/step-up after a recent successful /mfa/verify), or - outside
/// Production only - the transitional X-PPIQ-MFA-Verified test header.
/// </summary>
public sealed class AdminMfaRequirementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly IOptionsMonitor<AuthOptions> _auth;

    public AdminMfaRequirementMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        IOptionsMonitor<AuthOptions> auth)
    {
        _next = next;
        _environment = environment;
        _auth = auth;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_auth.CurrentValue.RequireAdminMfa
            && RequiresAdminMfa(context)
            && !HasMfaProof(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "admin_mfa_required",
                message = "PPIQ-T009: Admin endpoints require verified MFA. Complete /mfa/verify then POST /auth/mfa/step-up."
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

        // Transitional integration-test bypass - NEVER honored in Production.
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
