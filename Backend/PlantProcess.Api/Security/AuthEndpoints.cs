using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PlantProcess.Api.Security;

public static class AuthEndpoints
{
    private static readonly HashSet<string> AllowedRoles = new(
        new[] { "Admin", "DataManager", "Engineer", "Viewer" },
        StringComparer.OrdinalIgnoreCase);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication")
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .WithSummary("Login and return JWT access token")
            .WithDescription(
                "Development/bootstrap JWT login. Bootstrap admin is automatically disabled once a real configured admin exists.");

        return app;
    }

    private static IResult LoginAsync(
        LoginRequest request,
        IOptions<AuthOptions> options,
        IWebHostEnvironment environment,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("PlantProcess.Auth");
        var auth = options.Value;

        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new
            {
                message = "User name and password are required."
            });
        }

        if (IsBootstrapCredential(request, auth) && HasRealAdmin(auth))
        {
            logger.LogWarning(
                "Bootstrap admin login rejected because at least one real admin exists. Environment={EnvironmentName}",
                environment.EnvironmentName);

            return Results.Forbid();
        }

        var resolvedUser = ResolveUser(request, auth);

        if (resolvedUser is null)
        {
            logger.LogWarning(
                "Failed login attempt. UserName={UserName}, Environment={EnvironmentName}",
                request.UserName,
                environment.EnvironmentName);

            return Results.Unauthorized();
        }

        var normalizedRole = NormalizeRole(resolvedUser.Role);

        if (!AllowedRoles.Contains(normalizedRole))
        {
            logger.LogWarning(
                "Login rejected because configured role is not allowed. UserName={UserName}, Role={Role}",
                resolvedUser.UserName,
                resolvedUser.Role);

            return Results.BadRequest(new
            {
                message = $"Configured role '{resolvedUser.Role}' is not supported."
            });
        }

        if (string.IsNullOrWhiteSpace(auth.SigningKey) || auth.SigningKey.Length < 32)
        {
            logger.LogError(
                "JWT signing key is missing or too short. Environment={EnvironmentName}",
                environment.EnvironmentName);

            return Results.Problem(
                title: "Authentication configuration error",
                detail: "JWT signing key is not configured correctly.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(Math.Clamp(auth.AccessTokenMinutes, 5, 24 * 60));

        var displayName = string.IsNullOrWhiteSpace(resolvedUser.DisplayName)
            ? resolvedUser.UserName
            : resolvedUser.DisplayName.Trim();

        var scopes = BuildScopes(normalizedRole);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, resolvedUser.UserName),
            new(JwtRegisteredClaimNames.UniqueName, resolvedUser.UserName),
            new(ClaimTypes.Name, resolvedUser.UserName),
            new("display_name", displayName),
            new(ClaimTypes.Role, normalizedRole)
        };

        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: auth.Issuer,
            audience: auth.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        logger.LogInformation(
            "Login succeeded. UserName={UserName}, Role={Role}, Bootstrap={IsBootstrapAdmin}, ForcePasswordChange={ForcePasswordChangeRequired}, ExpiresAtUtc={ExpiresAtUtc}",
            resolvedUser.UserName,
            normalizedRole,
            resolvedUser.IsBootstrapAdmin,
            resolvedUser.ForcePasswordChangeRequired,
            expires);

        return Results.Ok(new LoginResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresAtUtc: expires,
            UserName: resolvedUser.UserName,
            DisplayName: displayName,
            Role: normalizedRole,
            Scopes: scopes,
            ForcePasswordChangeRequired: resolvedUser.ForcePasswordChangeRequired,
            IsBootstrapAdmin: resolvedUser.IsBootstrapAdmin));
    }

    private static ResolvedLoginUser? ResolveUser(LoginRequest request, AuthOptions auth)
    {
        var requestedUserName = request.UserName.Trim();

        var configuredUser = auth.Users.FirstOrDefault(x =>
            string.Equals(x.UserName, requestedUserName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Password, request.Password, StringComparison.Ordinal));

        if (configuredUser is not null)
        {
            return new ResolvedLoginUser(
                UserName: configuredUser.UserName.Trim(),
                Role: NormalizeRole(configuredUser.Role),
                DisplayName: configuredUser.DisplayName,
                IsBootstrapAdmin: configuredUser.IsBootstrapAdmin,
                ForcePasswordChangeRequired: configuredUser.ForcePasswordChangeOnFirstLogin);
        }

        if (IsBootstrapCredential(request, auth) && !HasRealAdmin(auth))
        {
            return new ResolvedLoginUser(
                UserName: auth.BootstrapAdminUser!.Trim(),
                Role: "Admin",
                DisplayName: "Bootstrap Admin",
                IsBootstrapAdmin: true,
                ForcePasswordChangeRequired: auth.BootstrapAdminForcePasswordChange);
        }

        return null;
    }

    private static bool IsBootstrapCredential(LoginRequest request, AuthOptions auth)
    {
        return !string.IsNullOrWhiteSpace(auth.BootstrapAdminUser) &&
               !string.IsNullOrWhiteSpace(auth.BootstrapAdminPassword) &&
               string.Equals(request.UserName.Trim(), auth.BootstrapAdminUser, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.Password, auth.BootstrapAdminPassword, StringComparison.Ordinal);
    }

    private static bool HasRealAdmin(AuthOptions options)
    {
        return options.Users.Any(user =>
            !user.IsBootstrapAdmin &&
            string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(user.UserName) &&
            !string.IsNullOrWhiteSpace(user.Password));
    }

    private static string NormalizeRole(string role)
    {
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return "Admin";

        if (role.Equals("DataManager", StringComparison.OrdinalIgnoreCase))
            return "DataManager";

        if (role.Equals("Engineer", StringComparison.OrdinalIgnoreCase))
            return "Engineer";

        return "Viewer";
    }

    private static IReadOnlyList<string> BuildScopes(string role)
    {
        return role switch
        {
            "Admin" => new[]
            {
                "plantprocess.admin",
                "plantprocess.configure",
                "plantprocess.data.manage",
                "plantprocess.analytics.write",
                "plantprocess.dashboard.write",
                "plantprocess.dashboard.read"
            },

            "DataManager" => new[]
            {
                "plantprocess.configure",
                "plantprocess.data.manage",
                "plantprocess.dashboard.read"
            },

            "Engineer" => new[]
            {
                "plantprocess.analytics.write",
                "plantprocess.dashboard.write",
                "plantprocess.dashboard.read"
            },

            _ => new[]
            {
                "plantprocess.dashboard.read"
            }
        };
    }

    public sealed record LoginRequest(
        string UserName,
        string Password,
        string? RequestedRole = null);

    public sealed record LoginResponse(
        string AccessToken,
        string TokenType,
        DateTime ExpiresAtUtc,
        string UserName,
        string DisplayName,
        string Role,
        IReadOnlyList<string> Scopes,
        bool ForcePasswordChangeRequired,
        bool IsBootstrapAdmin);

    private sealed record ResolvedLoginUser(
        string UserName,
        string Role,
        string? DisplayName,
        bool IsBootstrapAdmin,
        bool ForcePasswordChangeRequired);
}