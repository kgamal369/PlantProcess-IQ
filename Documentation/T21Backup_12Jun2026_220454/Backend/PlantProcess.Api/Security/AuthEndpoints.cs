using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using PlantProcess.Api.ErrorHandling;
namespace PlantProcess.Api.Security;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication")
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .WithSummary("Login and receive an in-memory access token plus httpOnly refresh cookie.");

        group.MapPost("/refresh", RefreshAsync)
            .WithSummary("Refresh access token from httpOnly refresh cookie.");

        group.MapPost("/logout", LogoutAsync)
            .WithSummary("Revoke refresh cookie and clear browser session.");

        group.MapGet("/session", GetSession)
            .RequireAuthorization()
            .WithSummary("Return authenticated session and entitlements.");

        group.MapPost("/mfa/step-up", StepUpAsync)
            .RequireAuthorization()
            .WithSummary("PPIQ-T021: re-issue the access token with mfa=true after a recent successful /mfa/verify.");

        group.MapGet("/provisioning/status", ProvisioningStatusAsync)
            .WithSummary("Return whether first-run provisioning is required.");

        group.MapPost("/provisioning/claim", ClaimOwnerAsync)
            .WithSummary("Create the first tenant owner using the one-time server-side token.");

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] AuthStore authStore,
        IOptions<AuthOptions> options,
        IWebHostEnvironment environment,
        HttpContext httpContext,
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] IPlantEntitlementResolver entitlementResolver,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("PlantProcess.Auth");
        var auth = options.Value;

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return ApplicationProblems.Validation("User name and password are required.");

        AppUserRecord? user = await authStore.ValidateUserAsync(
            request.UserName,
            request.Password,
            cancellationToken);

        if (user is null && environment.IsDevelopment())
        {
            user = ResolveDevelopmentUser(request, auth);
        }

        if (user is null)
        {
            logger.LogWarning("Failed login attempt for {UserName}.", request.UserName);
            return Results.Unauthorized();
        }

        var token = CreateAccessToken(user, auth, out var expires);
        await IssueRefreshCookieAsync(user, authStore, options.Value, httpContext, cancellationToken);

        logger.LogInformation(
            "Login succeeded for {UserName}. Role={Role}, Tenant={TenantCode}, ExpiresAtUtc={ExpiresAtUtc}",
            user.UserName,
            user.PlantRole,
            user.TenantCode,
            expires);

        var principal = BuildPrincipalForResponse(user, auth);
        httpContext.User = principal;
        var entitlements = entitlementResolver.Resolve(principal);

        return Results.Ok(new LoginResponse(
            AccessToken: token,
            TokenType: "Bearer",
            ExpiresAtUtc: expires,
            UserName: user.UserName,
            DisplayName: user.DisplayName,
            Role: user.CompatibilityRole,
            PlantRole: user.PlantRole,
            TenantId: user.TenantId,
            TenantCode: user.TenantCode,
            Scopes: entitlements.Permissions.ToArray(),
            ForcePasswordChangeRequired: user.ForcePasswordChange,
            IsBootstrapAdmin: false,
            Entitlements: entitlements));
    }

    private static async Task<IResult> RefreshAsync(
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] AuthStore authStore,
        IOptions<AuthOptions> options,
        HttpContext httpContext,
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] IPlantEntitlementResolver entitlementResolver,
        CancellationToken cancellationToken)
    {
        var auth = options.Value;

        if (!httpContext.Request.Cookies.TryGetValue(auth.RefreshCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return Results.Unauthorized();
        }

        var user = await authStore.ValidateRefreshTokenAsync(refreshToken, cancellationToken);
        if (user is null)
            return Results.Unauthorized();

        await authStore.RevokeRefreshTokenAsync(refreshToken, cancellationToken);

        var accessToken = CreateAccessToken(user, auth, out var expires);
        await IssueRefreshCookieAsync(user, authStore, auth, httpContext, cancellationToken);

        var principal = BuildPrincipalForResponse(user, auth);
        httpContext.User = principal;
        var entitlements = entitlementResolver.Resolve(principal);

        return Results.Ok(new LoginResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresAtUtc: expires,
            UserName: user.UserName,
            DisplayName: user.DisplayName,
            Role: user.CompatibilityRole,
            PlantRole: user.PlantRole,
            TenantId: user.TenantId,
            TenantCode: user.TenantCode,
            Scopes: entitlements.Permissions.ToArray(),
            ForcePasswordChangeRequired: user.ForcePasswordChange,
            IsBootstrapAdmin: false,
            Entitlements: entitlements));
    }

    private static async Task<IResult> LogoutAsync(
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] AuthStore authStore,
        IOptions<AuthOptions> options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var auth = options.Value;

        if (httpContext.Request.Cookies.TryGetValue(auth.RefreshCookieName, out var refreshToken))
            await authStore.RevokeRefreshTokenAsync(refreshToken, cancellationToken);

        httpContext.Response.Cookies.Delete(auth.RefreshCookieName, BuildCookieOptions(auth, DateTimeOffset.UtcNow.AddDays(-1)));
        return Results.NoContent();
    }

    private static IResult GetSession(
        ClaimsPrincipal user,
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] IPlantEntitlementResolver entitlementResolver)
    {
        return Results.Ok(entitlementResolver.Resolve(user));
    }

    private static async Task<IResult> ProvisioningStatusAsync(
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] AuthStore authStore,
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] FirstRunProvisioningState state,
        CancellationToken cancellationToken)
    {
        var hasUser = await authStore.HasAnyUserAsync(cancellationToken);
        return Results.Ok(new
        {
            provisioningRequired = !hasUser,
            tokenGenerated = !string.IsNullOrWhiteSpace(state.TokenHash),
            generatedAtUtc = state.GeneratedAtUtc
        });
    }

    private static async Task<IResult> ClaimOwnerAsync(
        ProvisionOwnerRequest request,
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] AuthStore authStore,
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] FirstRunProvisioningState state,
        [Microsoft.AspNetCore.Mvc.FromServicesAttribute] IAuditLogger<P01P02OwnerProvisionedAudit> auditLogger,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("PlantProcess.Provisioning");

        if (await authStore.HasAnyUserAsync(cancellationToken))
            return ApplicationProblems.Conflict("First-run provisioning is already closed because a user exists.");

        if (!state.Validate(request.ProvisioningToken))
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 12)
        {
            return ApplicationProblems.Validation("User name is required and password must be at least 12 characters.");
        }

        var owner = await authStore.CreateOwnerAsync(
            request.UserName,
            request.Password,
            request.DisplayName ?? "Tenant Owner",
            cancellationToken);

        state.Clear();

        logger.LogWarning(
            "First-run tenant owner created. UserName={UserName}, Tenant={TenantCode}",
            owner.UserName,
            owner.TenantCode);

        return Results.Ok(new
        {
            created = true,
            userName = owner.UserName,
            tenantId = owner.TenantId,
            tenantCode = owner.TenantCode,
            role = owner.PlantRole,
            forcePasswordChange = owner.ForcePasswordChange
        });
    }

    private static AppUserRecord? ResolveDevelopmentUser(LoginRequest request, AuthOptions auth)
    {
        var configured = auth.Users.FirstOrDefault(x =>
            string.Equals(x.UserName, request.UserName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Password, request.Password, StringComparison.Ordinal));

        if (configured is null)
            return null;

        var plantRole = PlantRoles.NormalizePlantRole(configured.Role);
        return new AppUserRecord(
            Id: Guid.Parse("00000000-0000-0000-0000-000000000101"),
            TenantId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            TenantCode: "default-demo",
            UserName: configured.UserName,
            DisplayName: configured.DisplayName ?? configured.UserName,
            PlantRole: plantRole,
            CompatibilityRole: PlantRoles.ToCompatibilityRole(plantRole),
            ForcePasswordChange: configured.ForcePasswordChangeOnFirstLogin,
            IsOwner: string.Equals(plantRole, PlantRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateAccessToken(AppUserRecord user, AuthOptions auth, out DateTime expires)
    {
        if (string.IsNullOrWhiteSpace(auth.SigningKey) || auth.SigningKey.Length < 32)
            throw new InvalidOperationException("JWT signing key is missing or too short.");

        var now = DateTime.UtcNow;
        expires = now.AddMinutes(Math.Clamp(auth.AccessTokenMinutes, 5, 240));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("display_name", user.DisplayName),
            new(ClaimTypes.Role, user.CompatibilityRole),
            new("role", user.CompatibilityRole),
            new("ppiq_role", user.PlantRole),
            new("tenant_id", user.TenantId.ToString()),
            new("tenant_code", user.TenantCode)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: auth.Issuer,
            audience: auth.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static ClaimsPrincipal BuildPrincipalForResponse(AppUserRecord user, AuthOptions auth)
    {
        var identity = new ClaimsIdentity("PPIQ");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.CompatibilityRole));
        identity.AddClaim(new Claim("role", user.CompatibilityRole));
        identity.AddClaim(new Claim("ppiq_role", user.PlantRole));
        identity.AddClaim(new Claim("tenant_id", user.TenantId.ToString()));
        identity.AddClaim(new Claim("tenant_code", user.TenantCode));
        return new ClaimsPrincipal(identity);
    }

    private static async Task IssueRefreshCookieAsync(
        AppUserRecord user,
        [Microsoft.AspNetCore.Mvc.FromServices] AuthStore store,
        AuthOptions auth,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var rawRefreshToken = PasswordHasher.CreateSecureToken();
        var expires = DateTime.UtcNow.AddDays(Math.Clamp(auth.RefreshTokenDays, 1, 30));

        await store.StoreRefreshTokenAsync(
            user.TenantId,
            user.Id,
            rawRefreshToken,
            expires,
            httpContext,
            cancellationToken);

        httpContext.Response.Cookies.Append(
            auth.RefreshCookieName,
            rawRefreshToken,
            BuildCookieOptions(auth, expires));
    }

    private static CookieOptions BuildCookieOptions(AuthOptions auth, DateTimeOffset expires)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = auth.RefreshCookieSecure,
            SameSite = auth.RefreshCookieSameSite,
            Expires = expires,
            Path = "/"
        };
    }

    public sealed record LoginRequest(string UserName, string Password, string? RequestedRole = null);

    public sealed record ProvisionOwnerRequest(
        string ProvisioningToken,
        string UserName,
        string Password,
        string? DisplayName = null);

    public sealed record LoginResponse(
        string AccessToken,
        string TokenType,
        DateTime ExpiresAtUtc,
        string UserName,
        string DisplayName,
        string Role,
        string PlantRole,
        Guid TenantId,
        string TenantCode,
        IReadOnlyList<string> Scopes,
        bool ForcePasswordChangeRequired,
        bool IsBootstrapAdmin,
        EffectiveEntitlementDto Entitlements);

    public sealed record P01P02OwnerProvisionedAudit;
}

public interface IAuditLogger<T>
{
}

public sealed class NoopAuditLogger<T> : IAuditLogger<T>
{
}

