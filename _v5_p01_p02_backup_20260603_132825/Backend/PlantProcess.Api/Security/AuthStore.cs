using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Security;

public sealed record AppUserRecord(
    Guid Id,
    Guid TenantId,
    string TenantCode,
    string UserName,
    string DisplayName,
    string PlantRole,
    string CompatibilityRole,
    bool ForcePasswordChange,
    bool IsOwner);

public sealed class AuthStore
{
    private readonly PlantProcessDbContext _db;

    public AuthStore(PlantProcessDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasAnyUserAsync(CancellationToken cancellationToken)
    {
        await EnsureOpenAsync(cancellationToken);
        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM app_users WHERE is_enabled = true)";
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is bool b && b;
    }

    public async Task<AppUserRecord?> ValidateUserAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        await EnsureOpenAsync(cancellationToken);

        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
SELECT u.id, u.tenant_id, t.tenant_code, u.user_name, COALESCE(u.display_name, u.user_name) AS display_name,
       u.password_hash, u.password_salt, u.password_iterations,
       u.plant_role, u.compatibility_role, u.force_password_change, u.is_owner
FROM app_users u
JOIN tenants t ON t.id = u.tenant_id
WHERE u.normalized_user_name = lower(@user_name)
  AND u.is_enabled = true
  AND t.is_active = true
LIMIT 1
""";
        Add(cmd, "user_name", userName.Trim().ToLowerInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var passwordHash = reader.GetString(reader.GetOrdinal("password_hash"));
        var salt = reader.GetString(reader.GetOrdinal("password_salt"));
        var iterations = reader.GetInt32(reader.GetOrdinal("password_iterations"));

        if (!PasswordHasher.Verify(password, salt, passwordHash, iterations))
            return null;

        return MapUser(reader);
    }

    public async Task<AppUserRecord> CreateOwnerAsync(
        string userName,
        string password,
        string displayName,
        CancellationToken cancellationToken)
    {
        await EnsureOpenAsync(cancellationToken);

        var salt = PasswordHasher.CreateSalt();
        var hash = PasswordHasher.Hash(password, salt);

        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
INSERT INTO app_users
(tenant_id, user_name, normalized_user_name, display_name, password_hash, password_salt, password_iterations,
 plant_role, compatibility_role, is_owner, is_enabled, force_password_change)
VALUES
('00000000-0000-0000-0000-000000000001', @user_name, lower(@user_name), @display_name, @hash, @salt, @iterations,
 'TenantOwner', 'Admin', true, true, true)
RETURNING id, tenant_id
""";
        Add(cmd, "user_name", userName.Trim());
        Add(cmd, "display_name", string.IsNullOrWhiteSpace(displayName) ? "Tenant Owner" : displayName.Trim());
        Add(cmd, "hash", hash);
        Add(cmd, "salt", salt);
        Add(cmd, "iterations", PasswordHasher.DefaultIterations);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new AppUserRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            "default-demo",
            userName.Trim(),
            displayName.Trim(),
            "TenantOwner",
            "Admin",
            true,
            true);
    }

    public async Task StoreRefreshTokenAsync(
        Guid tenantId,
        Guid userId,
        string rawToken,
        DateTime expiresAtUtc,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await EnsureOpenAsync(cancellationToken);

        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
INSERT INTO auth_refresh_tokens
(tenant_id, user_id, token_hash, expires_at_utc, user_agent, client_ip)
VALUES (@tenant_id, @user_id, @token_hash, @expires_at_utc, @user_agent, @client_ip)
""";
        Add(cmd, "tenant_id", tenantId);
        Add(cmd, "user_id", userId);
        Add(cmd, "token_hash", PasswordHasher.Sha256(rawToken));
        Add(cmd, "expires_at_utc", expiresAtUtc);
        Add(cmd, "user_agent", httpContext.Request.Headers.UserAgent.ToString());
        Add(cmd, "client_ip", httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AppUserRecord?> ValidateRefreshTokenAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        await EnsureOpenAsync(cancellationToken);

        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
SELECT u.id, u.tenant_id, t.tenant_code, u.user_name, COALESCE(u.display_name, u.user_name) AS display_name,
       u.password_hash, u.password_salt, u.password_iterations,
       u.plant_role, u.compatibility_role, u.force_password_change, u.is_owner
FROM auth_refresh_tokens rt
JOIN app_users u ON u.id = rt.user_id
JOIN tenants t ON t.id = u.tenant_id
WHERE rt.token_hash = @token_hash
  AND rt.revoked_at_utc IS NULL
  AND rt.expires_at_utc > now()
  AND u.is_enabled = true
  AND t.is_active = true
LIMIT 1
""";
        Add(cmd, "token_hash", PasswordHasher.Sha256(rawToken));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return MapUser(reader);
    }

    public async Task RevokeRefreshTokenAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;

        await EnsureOpenAsync(cancellationToken);

        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
UPDATE auth_refresh_tokens
SET revoked_at_utc = now()
WHERE token_hash = @token_hash
  AND revoked_at_utc IS NULL
""";
        Add(cmd, "token_hash", PasswordHasher.Sha256(rawToken));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static AppUserRecord MapUser(DbDataReader reader)
    {
        return new AppUserRecord(
            Id: reader.GetGuid(reader.GetOrdinal("id")),
            TenantId: reader.GetGuid(reader.GetOrdinal("tenant_id")),
            TenantCode: reader.GetString(reader.GetOrdinal("tenant_code")),
            UserName: reader.GetString(reader.GetOrdinal("user_name")),
            DisplayName: reader.GetString(reader.GetOrdinal("display_name")),
            PlantRole: reader.GetString(reader.GetOrdinal("plant_role")),
            CompatibilityRole: reader.GetString(reader.GetOrdinal("compatibility_role")),
            ForcePasswordChange: reader.GetBoolean(reader.GetOrdinal("force_password_change")),
            IsOwner: reader.GetBoolean(reader.GetOrdinal("is_owner")));
    }

    private async Task EnsureOpenAsync(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static void Add(DbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        cmd.Parameters.Add(parameter);
    }
}

public static class PasswordHasher
{
    public const int DefaultIterations = 210000;

    public static string CreateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public static string Hash(string password, string saltBase64, int iterations = DefaultIterations)
    {
        var salt = Convert.FromBase64String(saltBase64);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return Convert.ToBase64String(hash);
    }

    public static bool Verify(string password, string saltBase64, string expectedHashBase64, int iterations)
    {
        var actual = Convert.FromBase64String(Hash(password, saltBase64, iterations));
        var expected = Convert.FromBase64String(expectedHashBase64);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static string CreateSecureToken(int bytes = 64)
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    public static string Sha256(string raw)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
