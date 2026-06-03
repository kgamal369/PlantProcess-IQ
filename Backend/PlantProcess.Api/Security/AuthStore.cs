using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    private readonly AuthOptions _auth;

    public AuthStore(PlantProcessDbContext db, IOptions<AuthOptions> authOptions)
    {
        _db = db;
        _auth = authOptions.Value;
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
SELECT
    u.id,
    u.tenant_id,
    t.tenant_code,
    u.user_name,
    COALESCE(u.display_name, u.user_name) AS display_name,
    u.password_hash,
    u.password_salt,
    u.password_iterations,
    COALESCE(u.password_algorithm, 'pbkdf2-sha256') AS password_algorithm,
    COALESCE(u.password_hash_parameters, '{}'::jsonb)::text AS password_hash_parameters,
    u.plant_role,
    u.compatibility_role,
    u.force_password_change,
    u.is_owner
FROM app_users u
JOIN tenants t ON t.id = u.tenant_id
WHERE u.normalized_user_name = lower(@user_name)
  AND u.is_enabled = true
  AND t.is_active = true
LIMIT 1
""";
        Add(cmd, "user_name", userName.Trim().ToLowerInvariant());

        Guid id;
        Guid tenantId;
        string tenantCode;
        string storedUserName;
        string displayName;
        string passwordHash;
        string salt;
        int iterations;
        string algorithm;
        string parameterJson;
        string plantRole;
        string compatibilityRole;
        bool forcePasswordChange;
        bool isOwner;

        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;

            id = reader.GetGuid(reader.GetOrdinal("id"));
            tenantId = reader.GetGuid(reader.GetOrdinal("tenant_id"));
            tenantCode = reader.GetString(reader.GetOrdinal("tenant_code"));
            storedUserName = reader.GetString(reader.GetOrdinal("user_name"));
            displayName = reader.GetString(reader.GetOrdinal("display_name"));
            passwordHash = reader.GetString(reader.GetOrdinal("password_hash"));
            salt = reader.GetString(reader.GetOrdinal("password_salt"));
            iterations = reader.GetInt32(reader.GetOrdinal("password_iterations"));
            algorithm = reader.GetString(reader.GetOrdinal("password_algorithm"));
            parameterJson = reader.GetString(reader.GetOrdinal("password_hash_parameters"));
            plantRole = reader.GetString(reader.GetOrdinal("plant_role"));
            compatibilityRole = reader.GetString(reader.GetOrdinal("compatibility_role"));
            forcePasswordChange = reader.GetBoolean(reader.GetOrdinal("force_password_change"));
            isOwner = reader.GetBoolean(reader.GetOrdinal("is_owner"));
        }

        var verification = PasswordHasher.Verify(
            password,
            salt,
            passwordHash,
            iterations,
            algorithm,
            parameterJson,
            _auth);

        if (!verification.Succeeded)
            return null;

        if (verification.NeedsRehash)
            await UpdatePasswordHashAsync(id, password, cancellationToken);

        return new AppUserRecord(
            id,
            tenantId,
            tenantCode,
            storedUserName,
            displayName,
            plantRole,
            compatibilityRole,
            forcePasswordChange,
            isOwner);
    }

    public async Task<AppUserRecord> CreateOwnerAsync(
        string userName,
        string password,
        string displayName,
        CancellationToken cancellationToken)
    {
        await EnsureOpenAsync(cancellationToken);

        var salt = PasswordHasher.CreateSalt();
        var hash = PasswordHasher.HashArgon2id(password, salt, _auth);
        var parameters = PasswordHasher.BuildArgon2idParameterJson(_auth);

        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
INSERT INTO app_users
(
    tenant_id,
    user_name,
    normalized_user_name,
    display_name,
    password_hash,
    password_salt,
    password_iterations,
    password_algorithm,
    password_hash_parameters,
    plant_role,
    compatibility_role,
    is_owner,
    is_enabled,
    force_password_change
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    @user_name,
    lower(@user_name),
    @display_name,
    @hash,
    @salt,
    @iterations,
    'argon2id',
    @parameters::jsonb,
    'TenantOwner',
    'Admin',
    true,
    true,
    true
)
RETURNING id, tenant_id
""";
        Add(cmd, "user_name", userName.Trim());
        Add(cmd, "display_name", string.IsNullOrWhiteSpace(displayName) ? "Tenant Owner" : displayName.Trim());
        Add(cmd, "hash", hash);
        Add(cmd, "salt", salt);
        Add(cmd, "iterations", _auth.Argon2Iterations);
        Add(cmd, "parameters", parameters);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new AppUserRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            "default-demo",
            userName.Trim(),
            string.IsNullOrWhiteSpace(displayName) ? "Tenant Owner" : displayName.Trim(),
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
SELECT
    u.id,
    u.tenant_id,
    t.tenant_code,
    u.user_name,
    COALESCE(u.display_name, u.user_name) AS display_name,
    u.plant_role,
    u.compatibility_role,
    u.force_password_change,
    u.is_owner
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

    private async Task UpdatePasswordHashAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken)
    {
        var salt = PasswordHasher.CreateSalt();
        var hash = PasswordHasher.HashArgon2id(password, salt, _auth);
        var parameters = PasswordHasher.BuildArgon2idParameterJson(_auth);

        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
UPDATE app_users
SET
    password_hash = @hash,
    password_salt = @salt,
    password_iterations = @iterations,
    password_algorithm = 'argon2id',
    password_hash_parameters = @parameters::jsonb,
    updated_at_utc = now()
WHERE id = @id
""";
        Add(cmd, "id", userId);
        Add(cmd, "hash", hash);
        Add(cmd, "salt", salt);
        Add(cmd, "iterations", _auth.Argon2Iterations);
        Add(cmd, "parameters", parameters);

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

public sealed record PasswordVerificationResult(bool Succeeded, bool NeedsRehash);

public static class PasswordHasher
{
    public const int LegacyPbkdf2DefaultIterations = 210000;
    public const string AlgorithmArgon2id = "argon2id";
    public const string AlgorithmLegacyPbkdf2 = "pbkdf2-sha256";

    public static string CreateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public static string HashArgon2id(string password, string saltBase64, AuthOptions options)
    {
        var salt = Convert.FromBase64String(saltBase64);
        var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = Math.Max(1, options.Argon2Parallelism),
            Iterations = Math.Max(1, options.Argon2Iterations),
            MemorySize = Math.Max(8192, options.Argon2MemoryKb)
        };

        return Convert.ToBase64String(argon.GetBytes(Math.Clamp(options.Argon2HashBytes, 16, 64)));
    }

    public static string BuildArgon2idParameterJson(AuthOptions options)
    {
        return JsonSerializer.Serialize(new
        {
            algorithm = AlgorithmArgon2id,
            memoryKb = Math.Max(8192, options.Argon2MemoryKb),
            iterations = Math.Max(1, options.Argon2Iterations),
            parallelism = Math.Max(1, options.Argon2Parallelism),
            hashBytes = Math.Clamp(options.Argon2HashBytes, 16, 64)
        });
    }

    public static PasswordVerificationResult Verify(
        string password,
        string saltBase64,
        string expectedHashBase64,
        int iterations,
        string? algorithm,
        string? parameterJson,
        AuthOptions options)
    {
        var normalizedAlgorithm = string.IsNullOrWhiteSpace(algorithm)
            ? AlgorithmLegacyPbkdf2
            : algorithm.Trim().ToLowerInvariant();

        if (normalizedAlgorithm == AlgorithmArgon2id)
        {
            var actual = Convert.FromBase64String(HashArgon2id(password, saltBase64, options));
            var expected = Convert.FromBase64String(expectedHashBase64);
            return new PasswordVerificationResult(
                CryptographicOperations.FixedTimeEquals(actual, expected),
                false);
        }

        var legacyActual = Convert.FromBase64String(HashLegacyPbkdf2(password, saltBase64, iterations));
        var legacyExpected = Convert.FromBase64String(expectedHashBase64);

        return new PasswordVerificationResult(
            CryptographicOperations.FixedTimeEquals(legacyActual, legacyExpected),
            true);
    }

    public static string HashLegacyPbkdf2(
        string password,
        string saltBase64,
        int iterations = LegacyPbkdf2DefaultIterations)
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

    public static string CreateSecureToken(int bytes = 64)
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    public static string Sha256(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}