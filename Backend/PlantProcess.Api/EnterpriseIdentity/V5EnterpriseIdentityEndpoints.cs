using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PlantProcess.Api.EnterpriseIdentity;

public sealed record MfaEnrollRequest(Guid? UserId, string? AccountName);
public sealed record MfaVerifyRequest(Guid? UserId, string Code, string? RecoveryCode);
public sealed record CreateSessionRequest(Guid? UserId, string? DeviceLabel);
public sealed record LoginAttemptRequest(string UserName, bool Succeeded);
public sealed record PasswordPolicyCheckRequest(string Password);

public static class V5EnterpriseIdentityEndpoints
{
        private static readonly Guid DefaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    public static IEndpointRouteBuilder MapV5EnterpriseIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/enterprise-identity")
            .WithTags("V5 Enterprise Identity");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-enterprise-identity",
            totpMfa = true,
            recoveryCodesHashOnly = true,
            sessionRegistry = true,
            sessionRevoke = true,
            refreshReuseDetection = true,
            lockout = true,
            passwordPolicy = true
        }));

        group.MapGet("/policy", async (
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);
            await EnsurePolicyAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    mfa_required_for_admins,
                    mfa_required_for_all_users,
                    max_active_sessions,
                    idle_timeout_minutes,
                    absolute_timeout_minutes,
                    max_failed_login_attempts,
                    lockout_minutes,
                    password_min_length,
                    password_requires_upper,
                    password_requires_lower,
                    password_requires_digit,
                    password_requires_symbol
                FROM public.ppiq_identity_policy
                WHERE tenant_id = @tenant_id
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return Results.NotFound();

            return Results.Ok(new
            {
                mfaRequiredForAdmins = reader.GetBoolean(0),
                mfaRequiredForAllUsers = reader.GetBoolean(1),
                maxActiveSessions = reader.GetInt32(2),
                idleTimeoutMinutes = reader.GetInt32(3),
                absoluteTimeoutMinutes = reader.GetInt32(4),
                maxFailedLoginAttempts = reader.GetInt32(5),
                lockoutMinutes = reader.GetInt32(6),
                passwordMinLength = reader.GetInt32(7),
                passwordRequiresUpper = reader.GetBoolean(8),
                passwordRequiresLower = reader.GetBoolean(9),
                passwordRequiresDigit = reader.GetBoolean(10),
                passwordRequiresSymbol = reader.GetBoolean(11)
            });
        });

        group.MapPost("/mfa/enroll", async (
            [FromBody] MfaEnrollRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var userId = request.UserId ?? DefaultUserId;
            var account = string.IsNullOrWhiteSpace(request.AccountName) ? "admin@plantprocessiq.local" : request.AccountName.Trim();

            var secret = TotpUtility.GenerateSecretBase32();
            var secretCiphertext = V5LocalSecretProtector.Protect(secret);
            var recoveryCodes = Enumerable.Range(0, 8)
                .Select(_ => CreateRecoveryCode())
                .ToArray();

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var tx = await connection.BeginTransactionAsync(cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_mfa_totp_enrollments
                (
                    tenant_id,
                    user_id,
                    secret_ciphertext,
                    secret_hint
                )
                VALUES
                (
                    @tenant_id,
                    @user_id,
                    @secret_ciphertext,
                    @secret_hint
                )
                ON CONFLICT (tenant_id, user_id)
                DO UPDATE SET
                    secret_ciphertext = EXCLUDED.secret_ciphertext,
                    secret_hint = EXCLUDED.secret_hint,
                    is_verified = false,
                    enabled_at_utc = NULL,
                    disabled_at_utc = NULL,
                    created_at_utc = now()
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("secret_ciphertext", secretCiphertext);
            cmd.Parameters.AddWithValue("secret_hint", secret[^4..]);

            var enrollmentId = await cmd.ExecuteScalarAsync(cancellationToken);

            await using var deleteCodes = connection.CreateCommand();
            deleteCodes.Transaction = tx;
            deleteCodes.CommandText =
                """
                DELETE FROM public.ppiq_mfa_recovery_codes
                WHERE tenant_id = @tenant_id AND user_id = @user_id
                """;
            deleteCodes.Parameters.AddWithValue("tenant_id", tenantId);
            deleteCodes.Parameters.AddWithValue("user_id", userId);
            await deleteCodes.ExecuteNonQueryAsync(cancellationToken);

            foreach (var recoveryCode in recoveryCodes)
            {
                await using var codeCmd = connection.CreateCommand();
                codeCmd.Transaction = tx;
                codeCmd.CommandText =
                    """
                    INSERT INTO public.ppiq_mfa_recovery_codes
                    (
                        tenant_id,
                        user_id,
                        code_hash
                    )
                    VALUES
                    (
                        @tenant_id,
                        @user_id,
                        @code_hash
                    )
                    """;
                codeCmd.Parameters.AddWithValue("tenant_id", tenantId);
                codeCmd.Parameters.AddWithValue("user_id", userId);
                codeCmd.Parameters.AddWithValue("code_hash", HashRecoveryCode(tenantId, userId, recoveryCode));
                await codeCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await WriteAuthEventAsync(connection, tx, tenantId, userId, account, "mfa_enroll", "info", http, "TOTP enrollment created.", cancellationToken);
            await tx.CommitAsync(cancellationToken);

            var issuer = Uri.EscapeDataString("PlantProcess IQ");
            var escapedAccount = Uri.EscapeDataString(account);
            var otpauthUri = $"otpauth://totp/{issuer}:{escapedAccount}?secret={secret}&issuer={issuer}&digits=6&period=30";

            return Results.Ok(new
            {
                enrollmentId,
                userId,
                secret,
                otpauthUri,
                recoveryCodes,
                recoveryCodesStored = "hashed",
                verificationRequired = true
            });
        });

        group.MapPost("/mfa/verify", async (
            [FromBody] MfaVerifyRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var userId = request.UserId ?? DefaultUserId;

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var verified = false;
            var method = "totp";

            if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
            {
                verified = await ConsumeRecoveryCodeAsync(connection, tenantId, userId, request.RecoveryCode, cancellationToken);
                method = "recovery_code";
            }
            else
            {
                var secret = await LoadTotpSecretAsync(connection, tenantId, userId, cancellationToken);

                if (!string.IsNullOrWhiteSpace(secret))
                    verified = TotpUtility.Verify(secret, request.Code);
            }

            if (verified)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    """
                    UPDATE public.ppiq_mfa_totp_enrollments
                    SET is_verified = true,
                        enabled_at_utc = COALESCE(enabled_at_utc, now()),
                        last_verified_at_utc = now()
                    WHERE tenant_id = @tenant_id
                      AND user_id = @user_id
                    """;
                cmd.Parameters.AddWithValue("tenant_id", tenantId);
                cmd.Parameters.AddWithValue("user_id", userId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                await WriteAuthEventAsync(connection, null, tenantId, userId, null, "mfa_verify", "success", http, method, cancellationToken);
            }
            else
            {
                await WriteAuthEventAsync(connection, null, tenantId, userId, null, "mfa_verify", "failure", http, method, cancellationToken);
            }

            return Results.Ok(new
            {
                verified,
                method,
                secondStepSatisfied = verified
            });
        });

        group.MapPost("/sessions", async (
            [FromBody] CreateSessionRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var userId = request.UserId ?? DefaultUserId;

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);
            await EnsurePolicyAsync(connection, tenantId, cancellationToken);

            var policy = await LoadPolicyAsync(connection, tenantId, cancellationToken);
            var familyId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_user_sessions
                (
                    tenant_id,
                    user_id,
                    refresh_token_family_id,
                    device_label,
                    user_agent,
                    client_ip,
                    idle_expires_at_utc,
                    absolute_expires_at_utc
                )
                VALUES
                (
                    @tenant_id,
                    @user_id,
                    @family_id,
                    @device_label,
                    @user_agent,
                    @client_ip,
                    @idle_expires_at_utc,
                    @absolute_expires_at_utc
                )
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("family_id", familyId);
            cmd.Parameters.AddWithValue("device_label", request.DeviceLabel ?? "unknown-device");
            cmd.Parameters.AddWithValue("user_agent", http.Request.Headers.UserAgent.ToString());
            cmd.Parameters.AddWithValue("client_ip", http.Connection.RemoteIpAddress?.ToString() ?? "");
            cmd.Parameters.AddWithValue("idle_expires_at_utc", now.AddMinutes(policy.IdleTimeoutMinutes));
            cmd.Parameters.AddWithValue("absolute_expires_at_utc", now.AddMinutes(policy.AbsoluteTimeoutMinutes));

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            await WriteAuthEventAsync(connection, null, tenantId, userId, null, "session_created", "success", http, $"family={familyId}", cancellationToken);

            return Results.Ok(new
            {
                id,
                userId,
                refreshTokenFamilyId = familyId,
                idleExpiresAtUtc = now.AddMinutes(policy.IdleTimeoutMinutes),
                absoluteExpiresAtUtc = now.AddMinutes(policy.AbsoluteTimeoutMinutes)
            });
        });

        group.MapGet("/sessions", async (
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var userId = DefaultUserId;

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id,
                    user_id,
                    refresh_token_family_id,
                    device_label,
                    user_agent,
                    client_ip,
                    created_at_utc,
                    last_seen_at_utc,
                    idle_expires_at_utc,
                    absolute_expires_at_utc,
                    revoked_at_utc,
                    revoke_reason
                FROM public.ppiq_user_sessions
                WHERE tenant_id = @tenant_id
                  AND user_id = @user_id
                ORDER BY last_seen_at_utc DESC
                LIMIT 100
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("user_id", userId);

            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    id = reader.GetGuid(0),
                    userId = reader.GetGuid(1),
                    refreshTokenFamilyId = reader.GetGuid(2),
                    deviceLabel = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    userAgent = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    clientIp = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    createdAtUtc = reader.GetDateTime(6),
                    lastSeenAtUtc = reader.GetDateTime(7),
                    idleExpiresAtUtc = reader.GetDateTime(8),
                    absoluteExpiresAtUtc = reader.GetDateTime(9),
                    revokedAtUtc = reader.IsDBNull(10) ? null : reader.GetDateTime(10).ToString("O"),
                    revokeReason = reader.IsDBNull(11) ? null : reader.GetString(11)
                });
            }

            return Results.Ok(rows);
        });

        group.MapPost("/sessions/{sessionId:guid}/revoke", async (
            Guid sessionId,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE public.ppiq_user_sessions
                SET revoked_at_utc = now(),
                    revoke_reason = 'manual_revoke',
                    is_current = false
                WHERE tenant_id = @tenant_id
                  AND id = @session_id
                  AND revoked_at_utc IS NULL
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("session_id", sessionId);

            var changed = await cmd.ExecuteNonQueryAsync(cancellationToken);
            await WriteAuthEventAsync(connection, null, tenantId, null, null, "session_revoked", "success", http, sessionId.ToString(), cancellationToken);

            return Results.Ok(new
            {
                sessionId,
                revoked = changed > 0
            });
        });

        group.MapPost("/account-protection/login-attempt", async (
            [FromBody] LoginAttemptRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var user = request.UserName.Trim().ToLowerInvariant();

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);
            await EnsurePolicyAsync(connection, tenantId, cancellationToken);

            var policy = await LoadPolicyAsync(connection, tenantId, cancellationToken);

            if (request.Succeeded)
            {
                await ResetProtectionAsync(connection, tenantId, user, cancellationToken);
                await WriteAuthEventAsync(connection, null, tenantId, null, user, "login_attempt", "success", http, "protection reset", cancellationToken);
                return Results.Ok(new { allowed = true, locked = false, failedAttempts = 0 });
            }

            var state = await RegisterFailedAttemptAsync(connection, tenantId, user, policy.MaxFailedLoginAttempts, policy.LockoutMinutes, cancellationToken);
            await WriteAuthEventAsync(connection, null, tenantId, null, user, "login_attempt", state.Locked ? "blocked" : "failure", http, $"failedAttempts={state.FailedAttempts}", cancellationToken);

            return Results.Ok(new
            {
                allowed = !state.Locked,
                locked = state.Locked,
                state.FailedAttempts,
                state.LockedUntilUtc
            });
        });

        group.MapPost("/password-policy/check", async (
            [FromBody] PasswordPolicyCheckRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);
            await EnsurePolicyAsync(connection, tenantId, cancellationToken);

            var policy = await LoadPolicyAsync(connection, tenantId, cancellationToken);
            var result = CheckPasswordPolicy(request.Password ?? "", policy);

            return Results.Ok(result);
        });

        return app;
    }

    private sealed record Policy(
        int IdleTimeoutMinutes,
        int AbsoluteTimeoutMinutes,
        int MaxFailedLoginAttempts,
        int LockoutMinutes,
        int PasswordMinLength,
        bool RequiresUpper,
        bool RequiresLower,
        bool RequiresDigit,
        bool RequiresSymbol);

    private sealed record ProtectionState(int FailedAttempts, bool Locked, DateTime? LockedUntilUtc);

    private static async Task<Policy> LoadPolicyAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT
                idle_timeout_minutes,
                absolute_timeout_minutes,
                max_failed_login_attempts,
                lockout_minutes,
                password_min_length,
                password_requires_upper,
                password_requires_lower,
                password_requires_digit,
                password_requires_symbol
            FROM public.ppiq_identity_policy
            WHERE tenant_id = @tenant_id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return new Policy(60, 720, 5, 15, 12, true, true, true, true);

        return new Policy(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetBoolean(5),
            reader.GetBoolean(6),
            reader.GetBoolean(7),
            reader.GetBoolean(8));
    }

    private static object CheckPasswordPolicy(string password, Policy policy)
    {
        var errors = new List<string>();

        if (password.Length < policy.PasswordMinLength)
            errors.Add($"Password must be at least {policy.PasswordMinLength} characters.");

        if (policy.RequiresUpper && !password.Any(char.IsUpper))
            errors.Add("Password must include an uppercase letter.");

        if (policy.RequiresLower && !password.Any(char.IsLower))
            errors.Add("Password must include a lowercase letter.");

        if (policy.RequiresDigit && !password.Any(char.IsDigit))
            errors.Add("Password must include a digit.");

        if (policy.RequiresSymbol && !password.Any(ch => !char.IsLetterOrDigit(ch)))
            errors.Add("Password must include a symbol.");

        return new
        {
            isValid = errors.Count == 0,
            errors
        };
    }

    private static async Task EnsurePolicyAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_identity_policy(tenant_id)
            VALUES (@tenant_id)
            ON CONFLICT (tenant_id) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ProtectionState> RegisterFailedAttemptAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string normalizedUserName,
        int maxFailed,
        int lockoutMinutes,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_account_protection_state
            (
                tenant_id,
                normalized_user_name,
                failed_attempts,
                first_failed_at_utc,
                locked_until_utc
            )
            VALUES
            (
                @tenant_id,
                @normalized_user_name,
                1,
                now(),
                NULL
            )
            ON CONFLICT (tenant_id, normalized_user_name)
            DO UPDATE SET
                failed_attempts = public.ppiq_account_protection_state.failed_attempts + 1,
                locked_until_utc = CASE
                    WHEN public.ppiq_account_protection_state.failed_attempts + 1 >= @max_failed
                    THEN now() + (@lockout_minutes || ' minutes')::interval
                    ELSE public.ppiq_account_protection_state.locked_until_utc
                END,
                updated_at_utc = now()
            RETURNING failed_attempts, locked_until_utc
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("normalized_user_name", normalizedUserName);
        cmd.Parameters.AddWithValue("max_failed", maxFailed);
        cmd.Parameters.AddWithValue("lockout_minutes", lockoutMinutes);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        var failed = reader.GetInt32(0);
        DateTime? lockedUntil = reader.IsDBNull(1) ? null : reader.GetDateTime(1);

        return new ProtectionState(failed, lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow, lockedUntil);
    }

    private static async Task ResetProtectionAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string normalizedUserName,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_account_protection_state
            (
                tenant_id,
                normalized_user_name,
                failed_attempts,
                last_success_at_utc
            )
            VALUES
            (
                @tenant_id,
                @normalized_user_name,
                0,
                now()
            )
            ON CONFLICT (tenant_id, normalized_user_name)
            DO UPDATE SET
                failed_attempts = 0,
                locked_until_utc = NULL,
                last_success_at_utc = now(),
                updated_at_utc = now()
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("normalized_user_name", normalizedUserName);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> LoadTotpSecretAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT secret_ciphertext
            FROM public.ppiq_mfa_totp_enrollments
            WHERE tenant_id = @tenant_id
              AND user_id = @user_id
              AND disabled_at_utc IS NULL
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("user_id", userId);

        var encrypted = await cmd.ExecuteScalarAsync(cancellationToken) as string;
        return string.IsNullOrWhiteSpace(encrypted)
            ? null
            : V5LocalSecretProtector.Unprotect(encrypted);
    }

    private static async Task<bool> ConsumeRecoveryCodeAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE public.ppiq_mfa_recovery_codes
            SET used_at_utc = now()
            WHERE tenant_id = @tenant_id
              AND user_id = @user_id
              AND code_hash = @code_hash
              AND used_at_utc IS NULL
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("code_hash", HashRecoveryCode(tenantId, userId, code));

        return await cmd.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task WriteAuthEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid tenantId,
        Guid? userId,
        string? normalizedUserName,
        string eventType,
        string outcome,
        HttpContext http,
        string details,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();

        if (transaction is not null)
            cmd.Transaction = transaction;

        cmd.CommandText =
            """
            INSERT INTO public.ppiq_auth_audit_events
            (
                tenant_id,
                user_id,
                normalized_user_name,
                event_type,
                outcome,
                client_ip,
                user_agent,
                details
            )
            VALUES
            (
                @tenant_id,
                @user_id,
                @normalized_user_name,
                @event_type,
                @outcome,
                @client_ip,
                @user_agent,
                @details::jsonb
            )
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("user_id", userId.HasValue ? userId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("normalized_user_name", normalizedUserName is null ? DBNull.Value : normalizedUserName);
        cmd.Parameters.AddWithValue("event_type", eventType);
        cmd.Parameters.AddWithValue("outcome", outcome);
        cmd.Parameters.AddWithValue("client_ip", http.Connection.RemoteIpAddress?.ToString() ?? "");
        cmd.Parameters.AddWithValue("user_agent", http.Request.Headers.UserAgent.ToString());
        cmd.Parameters.AddWithValue("details", $$"""{"message":{{System.Text.Json.JsonSerializer.Serialize(details)}}}""");

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string CreateRecoveryCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(9);
        return Convert.ToBase64String(bytes)
            .Replace("+", "", StringComparison.Ordinal)
            .Replace("/", "", StringComparison.Ordinal)
            .Replace("=", "", StringComparison.Ordinal)
            .ToUpperInvariant()[..10];
    }

    private static string HashRecoveryCode(Guid tenantId, Guid userId, string code)
    {
        var normalized = $"{tenantId:N}:{userId:N}:{code.Trim().ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static Guid ResolveTenantId(HttpContext http)
    {
        return PlantProcess.Api.Security.TenantClaimReader.ResolveRequiredTenantId(http);
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        cmd.Parameters.AddWithValue("tenant_id", tenantId.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

public static class TotpUtility
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateSecretBase32(int bytes = 20)
    {
        return Base32Encode(RandomNumberGenerator.GetBytes(bytes));
    }

    public static bool Verify(string secretBase32, string code, int timeStepSeconds = 30, int window = 1)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = new string(code.Where(char.IsDigit).ToArray());

        if (normalized.Length != 6)
            return false;

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var counter = unix / timeStepSeconds;

        for (var offset = -window; offset <= window; offset++)
        {
            var expected = Generate(secretBase32, counter + offset);

            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(normalized)))
            {
                return true;
            }
        }

        return false;
    }

    public static string Generate(string secretBase32, long counter)
    {
        var key = Base32Decode(secretBase32);
        var counterBytes = BitConverter.GetBytes(counter);

        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0f;

        var binary =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);

        return (binary % 1_000_000).ToString("D6");
    }

    private static string Base32Encode(byte[] data)
    {
        var output = new StringBuilder();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                output.Append(Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
            output.Append(Alphabet[(buffer << (5 - bitsLeft)) & 31]);

        return output.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        var clean = input.Trim().Replace("=", "", StringComparison.Ordinal).ToUpperInvariant();
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in clean)
        {
            var value = Alphabet.IndexOf(c);

            if (value < 0)
                continue;

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 255));
                bitsLeft -= 8;
            }
        }

        return bytes.ToArray();
    }
}

public static class V5LocalSecretProtector
{
    public static string Protect(string plaintext)
    {
        var key = ResolveKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        var combined = aes.IV.Concat(cipher).ToArray();

        return Convert.ToBase64String(combined);
    }

    public static string Unprotect(string ciphertext)
    {
        var key = ResolveKey();
        var combined = Convert.FromBase64String(ciphertext);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = combined[..16];

        using var decryptor = aes.CreateDecryptor();
        var cipher = combined[16..];
        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] ResolveKey()
    {
        var raw =
            Environment.GetEnvironmentVariable("PPIQ_SECRET_PROTECTOR_KEY") ??
            "local-dev-only-ppiq-v5-secret-protector-key-change-in-prod";

        return SHA256.HashData(Encoding.UTF8.GetBytes(raw));
    }
}