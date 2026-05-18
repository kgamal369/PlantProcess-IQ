using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PlantProcess.Api.Security;

namespace PlantProcess.Api.Endpoints.Security;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication")
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .WithSummary("Login and return JWT access token");

        return app;
    }

    private static IResult LoginAsync(
        LoginRequest request,
        IOptions<AuthOptions> options)
    {
        var auth = options.Value;

        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new
            {
                message = "User name and password are required."
            });
        }

        var isValid =
            string.Equals(request.UserName, auth.BootstrapAdminUser, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.Password, auth.BootstrapAdminPassword, StringComparison.Ordinal);

        if (!isValid)
            return Results.Unauthorized();

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(auth.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserName),
            new(JwtRegisteredClaimNames.UniqueName, request.UserName),
            new(ClaimTypes.Name, request.UserName),
            new(ClaimTypes.Role, "Admin"),
            new("scope", "plantprocess.full_access")
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

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return Results.Ok(new LoginResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresAtUtc: expires,
            UserName: request.UserName,
            Role: "Admin"));
    }

    public sealed record LoginRequest(
        string UserName,
        string Password);

    public sealed record LoginResponse(
        string AccessToken,
        string TokenType,
        DateTime ExpiresAtUtc,
        string UserName,
        string Role);
}