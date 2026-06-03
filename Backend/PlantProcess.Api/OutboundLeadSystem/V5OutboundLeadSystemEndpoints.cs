using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PlantProcess.Api.OutboundLeadSystem;

public sealed record CaptureLeadRequest(
    string CompanyName,
    string ContactName,
    string Email,
    string? Phone,
    string? JobTitle,
    string? Country,
    string? PlantType,
    string? InterestArea,
    string? PainPoints,
    string? PreferredContact,
    bool ConsentGiven,
    string? Honeypot);

public sealed record CreateNotificationChannelRequest(
    string ChannelCode,
    string ChannelType,
    string DisplayName,
    string Destination,
    bool IsEnabled = true);

public sealed record TriggerNotificationRequest(
    string EventType,
    string Severity,
    string Subject,
    object Payload);

public sealed record SuggestionOutcomeRequest(
    string SuggestionId,
    Guid? MaterialUnitId,
    string ActionTaken,
    string? ActedBy,
    DateTime? BeforeWindowStartUtc,
    DateTime? BeforeWindowEndUtc,
    DateTime? AfterWindowStartUtc,
    DateTime? AfterWindowEndUtc,
    string TargetKpi,
    decimal? BeforeValue,
    decimal? AfterValue,
    object? Evidence);

public static class V5OutboundLeadSystemEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IEndpointRouteBuilder MapV5OutboundLeadSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/outbound")
            .WithTags("V5 Outbound Lead System");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-outbound-lead-system",
            backendLeadCapture = true,
            noLocalStorageLeadCapture = true,
            mockSmtp = true,
            webhookDelivery = true,
            preferences = true,
            closedLoopSuggestionOutcome = true
        }));

        group.MapPost("/leads", async (
            [FromBody] CaptureLeadRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var validationErrors = ValidateLead(request);

            if (validationErrors.Count > 0)
                return Results.BadRequest(new { accepted = false, validationErrors });

            var spamScore = ComputeSpamScore(request);
            var fitScore = ComputeFitScore(request);
            var status = spamScore >= 0.75m ? "spam" : "new";

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var tx = await connection.BeginTransactionAsync(cancellationToken);

            var leadId = await InsertLeadAsync(
                connection,
                tx,
                tenantId,
                request,
                spamScore,
                fitScore,
                status,
                Hash(http.Connection.RemoteIpAddress?.ToString() ?? ""),
                Hash(http.Request.Headers.UserAgent.ToString()),
                cancellationToken);

            if (status != "spam")
            {
                await QueueNotificationAsync(
                    connection,
                    tx,
                    tenantId,
                    "lead_created",
                    "info",
                    $"New PlantProcess IQ demo lead: {request.CompanyName}",
                    new
                    {
                        leadId,
                        request.CompanyName,
                        request.ContactName,
                        request.Email,
                        request.PlantType,
                        request.InterestArea,
                        fitScore
                    },
                    cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);

            return Results.Ok(new
            {
                accepted = true,
                leadId,
                status,
                fitScore,
                spamScore,
                storedIn = "backend",
                localStorageUsed = false,
                notificationQueued = status != "spam"
            });
        });

        group.MapGet("/leads", async (
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
                SELECT
                    id,
                    company_name,
                    contact_name,
                    email,
                    plant_type,
                    interest_area,
                    fit_score,
                    status,
                    created_at_utc
                FROM public.ppiq_lead_captures
                WHERE tenant_id = @tenant_id
                ORDER BY created_at_utc DESC
                LIMIT 100
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    id = reader.GetGuid(0),
                    companyName = reader.GetString(1),
                    contactName = reader.GetString(2),
                    email = reader.GetString(3),
                    plantType = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    interestArea = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    fitScore = reader.GetDecimal(6),
                    status = reader.GetString(7),
                    createdAtUtc = reader.GetDateTime(8)
                });
            }

            return Results.Ok(rows);
        });

        group.MapPost("/notification-channels", async (
            [FromBody] CreateNotificationChannelRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_notification_channels
                (
                    tenant_id,
                    channel_code,
                    channel_type,
                    display_name,
                    destination,
                    is_enabled
                )
                VALUES
                (
                    @tenant_id,
                    @channel_code,
                    @channel_type,
                    @display_name,
                    @destination,
                    @is_enabled
                )
                ON CONFLICT (tenant_id, channel_code)
                DO UPDATE SET
                    channel_type = EXCLUDED.channel_type,
                    display_name = EXCLUDED.display_name,
                    destination = EXCLUDED.destination,
                    is_enabled = EXCLUDED.is_enabled
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("channel_code", request.ChannelCode.Trim());
            cmd.Parameters.AddWithValue("channel_type", request.ChannelType.Trim());
            cmd.Parameters.AddWithValue("display_name", request.DisplayName.Trim());
            cmd.Parameters.AddWithValue("destination", request.Destination.Trim());
            cmd.Parameters.AddWithValue("is_enabled", request.IsEnabled);

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new { id, saved = true });
        });

        group.MapPost("/notifications/trigger", async (
            [FromBody] TriggerNotificationRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var tx = await connection.BeginTransactionAsync(cancellationToken);

            var queued = await QueueNotificationAsync(
                connection,
                tx,
                tenantId,
                Normalize(request.EventType, "manual_notification"),
                NormalizeSeverity(request.Severity),
                Normalize(request.Subject, "PlantProcess IQ notification"),
                request.Payload,
                cancellationToken);

            await tx.CommitAsync(cancellationToken);

            return Results.Ok(new
            {
                queued,
                eventType = request.EventType,
                severity = NormalizeSeverity(request.Severity),
                deliveryMode = "mock_smtp_or_webhook"
            });
        });

        group.MapPost("/notifications/process-mock", async (
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
                UPDATE public.ppiq_notification_deliveries
                SET status = 'delivered',
                    attempt_count = attempt_count + 1,
                    last_attempt_at_utc = now(),
                    delivered_at_utc = now()
                WHERE tenant_id = @tenant_id
                  AND status IN ('queued', 'retrying')
                RETURNING id, subject
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            var delivered = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                delivered.Add(new
                {
                    id = reader.GetGuid(0),
                    subject = reader.GetString(1)
                });
            }

            return Results.Ok(new
            {
                deliveredCount = delivered.Count,
                delivered
            });
        });

        group.MapPost("/suggestions/outcomes", async (
            [FromBody] SuggestionOutcomeRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var direction = DetermineDirection(request.BeforeValue, request.AfterValue);
            var confidence = DetermineConfidence(request.BeforeValue, request.AfterValue, request.BeforeWindowStartUtc, request.AfterWindowEndUtc);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_suggestion_action_outcomes
                (
                    tenant_id,
                    suggestion_id,
                    material_unit_id,
                    action_taken,
                    acted_by,
                    before_window_start_utc,
                    before_window_end_utc,
                    after_window_start_utc,
                    after_window_end_utc,
                    target_kpi,
                    before_value,
                    after_value,
                    outcome_direction,
                    confidence,
                    evidence_json
                )
                VALUES
                (
                    @tenant_id,
                    @suggestion_id,
                    @material_unit_id,
                    @action_taken,
                    @acted_by,
                    @before_window_start_utc,
                    @before_window_end_utc,
                    @after_window_start_utc,
                    @after_window_end_utc,
                    @target_kpi,
                    @before_value,
                    @after_value,
                    @outcome_direction,
                    @confidence,
                    @evidence_json::jsonb
                )
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("suggestion_id", request.SuggestionId.Trim());
            cmd.Parameters.AddWithValue("material_unit_id", request.MaterialUnitId.HasValue ? request.MaterialUnitId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("action_taken", request.ActionTaken.Trim());
            cmd.Parameters.AddWithValue("acted_by", request.ActedBy is null ? DBNull.Value : request.ActedBy);
            cmd.Parameters.AddWithValue("before_window_start_utc", request.BeforeWindowStartUtc.HasValue ? EnsureUtc(request.BeforeWindowStartUtc.Value) : DBNull.Value);
            cmd.Parameters.AddWithValue("before_window_end_utc", request.BeforeWindowEndUtc.HasValue ? EnsureUtc(request.BeforeWindowEndUtc.Value) : DBNull.Value);
            cmd.Parameters.AddWithValue("after_window_start_utc", request.AfterWindowStartUtc.HasValue ? EnsureUtc(request.AfterWindowStartUtc.Value) : DBNull.Value);
            cmd.Parameters.AddWithValue("after_window_end_utc", request.AfterWindowEndUtc.HasValue ? EnsureUtc(request.AfterWindowEndUtc.Value) : DBNull.Value);
            cmd.Parameters.AddWithValue("target_kpi", Normalize(request.TargetKpi, "quality_risk"));
            cmd.Parameters.AddWithValue("before_value", request.BeforeValue.HasValue ? request.BeforeValue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("after_value", request.AfterValue.HasValue ? request.AfterValue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("outcome_direction", direction);
            cmd.Parameters.AddWithValue("confidence", confidence);
            cmd.Parameters.AddWithValue("evidence_json", JsonSerializer.Serialize(request.Evidence ?? new { }));

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                request.SuggestionId,
                outcomeDirection = direction,
                confidence,
                honestyNote = "This measurement tracks association after action and does not claim causation."
            });
        });

        group.MapGet("/suggestions/outcomes/kpi", async (
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
                SELECT
                    target_kpi,
                    outcome_direction,
                    confidence,
                    count(*) AS count,
                    avg(delta_value) AS avg_delta
                FROM public.ppiq_suggestion_action_outcomes
                WHERE tenant_id = @tenant_id
                GROUP BY target_kpi, outcome_direction, confidence
                ORDER BY target_kpi, outcome_direction, confidence
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    targetKpi = reader.GetString(0),
                    outcomeDirection = reader.GetString(1),
                    confidence = reader.GetString(2),
                    count = reader.GetInt64(3),
                    avgDelta = reader.IsDBNull(4) ? null : reader.GetDecimal(4).ToString()
                });
            }

            return Results.Ok(rows);
        });

        return app;
    }

    private static async Task<Guid> InsertLeadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        Guid tenantId,
        CaptureLeadRequest request,
        decimal spamScore,
        decimal fitScore,
        string status,
        string ipHash,
        string userAgentHash,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_lead_captures
            (
                tenant_id,
                source,
                company_name,
                contact_name,
                email,
                phone,
                job_title,
                country,
                plant_type,
                interest_area,
                pain_points,
                preferred_contact,
                consent_given,
                gdpr_consent_text,
                honeypot,
                spam_score,
                fit_score,
                status,
                client_ip_hash,
                user_agent_hash
            )
            VALUES
            (
                @tenant_id,
                'website',
                @company_name,
                @contact_name,
                @email,
                @phone,
                @job_title,
                @country,
                @plant_type,
                @interest_area,
                @pain_points,
                @preferred_contact,
                @consent_given,
                @gdpr_consent_text,
                @honeypot,
                @spam_score,
                @fit_score,
                @status,
                @client_ip_hash,
                @user_agent_hash
            )
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("company_name", request.CompanyName.Trim());
        cmd.Parameters.AddWithValue("contact_name", request.ContactName.Trim());
        cmd.Parameters.AddWithValue("email", request.Email.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue("phone", DbText(request.Phone));
        cmd.Parameters.AddWithValue("job_title", DbText(request.JobTitle));
        cmd.Parameters.AddWithValue("country", DbText(request.Country));
        cmd.Parameters.AddWithValue("plant_type", DbText(request.PlantType));
        cmd.Parameters.AddWithValue("interest_area", DbText(request.InterestArea));
        cmd.Parameters.AddWithValue("pain_points", DbText(request.PainPoints));
        cmd.Parameters.AddWithValue("preferred_contact", DbText(request.PreferredContact));
        cmd.Parameters.AddWithValue("consent_given", request.ConsentGiven);
        cmd.Parameters.AddWithValue("gdpr_consent_text", "User consented to be contacted about PlantProcess IQ demo/commercial inquiry.");
        cmd.Parameters.AddWithValue("honeypot", DbText(request.Honeypot));
        cmd.Parameters.AddWithValue("spam_score", spamScore);
        cmd.Parameters.AddWithValue("fit_score", fitScore);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("client_ip_hash", ipHash);
        cmd.Parameters.AddWithValue("user_agent_hash", userAgentHash);

        return (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
    }

    private static async Task<int> QueueNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        Guid tenantId,
        string eventType,
        string severity,
        string subject,
        object payload,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_notification_deliveries
            (
                tenant_id,
                channel_id,
                event_type,
                severity,
                subject,
                payload_json,
                status,
                next_attempt_at_utc
            )
            SELECT
                @tenant_id,
                c.id,
                @event_type,
                @severity,
                @subject,
                @payload_json::jsonb,
                'queued',
                now()
            FROM public.ppiq_notification_channels c
            WHERE c.tenant_id = @tenant_id
              AND c.is_enabled = true
              AND (
                  c.channel_code IN (
                      SELECT p.channel_code
                      FROM public.ppiq_notification_preferences p
                      WHERE p.tenant_id = @tenant_id
                        AND p.event_type = @event_type
                        AND p.is_enabled = true
                  )
                  OR @event_type = 'manual_notification'
              )
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("event_type", eventType);
        cmd.Parameters.AddWithValue("severity", severity);
        cmd.Parameters.AddWithValue("subject", subject);
        cmd.Parameters.AddWithValue("payload_json", JsonSerializer.Serialize(payload));

        var queued = 0;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            queued += 1;

        return queued;
    }

    private static List<string> ValidateLead(CaptureLeadRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.CompanyName))
            errors.Add("Company name is required.");

        if (string.IsNullOrWhiteSpace(request.ContactName))
            errors.Add("Contact name is required.");

        if (string.IsNullOrWhiteSpace(request.Email) || !EmailRegex.IsMatch(request.Email))
            errors.Add("Valid email is required.");

        if (!request.ConsentGiven)
            errors.Add("GDPR/contact consent is required.");

        if (!string.IsNullOrWhiteSpace(request.Honeypot))
            errors.Add("Spam protection rejected the submission.");

        return errors;
    }

    private static decimal ComputeSpamScore(CaptureLeadRequest request)
    {
        var score = 0m;

        if (!string.IsNullOrWhiteSpace(request.Honeypot))
            score += 1m;

        if ((request.PainPoints ?? "").Contains("http", StringComparison.OrdinalIgnoreCase))
            score += 0.25m;

        if ((request.CompanyName ?? "").Length < 2)
            score += 0.2m;

        return Math.Min(score, 1m);
    }

    private static decimal ComputeFitScore(CaptureLeadRequest request)
    {
        var score = 0.25m;

        var text = $"{request.PlantType} {request.InterestArea} {request.PainPoints}".ToLowerInvariant();

        if (text.Contains("steel") || text.Contains("manufacturing") || text.Contains("plant"))
            score += 0.25m;

        if (text.Contains("quality") || text.Contains("defect") || text.Contains("genealogy"))
            score += 0.25m;

        if (text.Contains("ai") || text.Contains("assistant") || text.Contains("prediction"))
            score += 0.15m;

        if (!string.IsNullOrWhiteSpace(request.Phone))
            score += 0.10m;

        return Math.Min(score, 1m);
    }

    private static string DetermineDirection(decimal? before, decimal? after)
    {
        if (!before.HasValue || !after.HasValue)
            return "unknown";

        var delta = after.Value - before.Value;

        if (Math.Abs(delta) < 0.000001m)
            return "no_change";

        return delta < 0 ? "improved" : "worsened";
    }

    private static string DetermineConfidence(decimal? before, decimal? after, DateTime? beforeStart, DateTime? afterEnd)
    {
        if (!before.HasValue || !after.HasValue || !beforeStart.HasValue || !afterEnd.HasValue)
            return "insufficient_data";

        return "medium";
    }

    private static string NormalizeSeverity(string value)
    {
        var x = Normalize(value, "info").ToLowerInvariant();
        return x is "info" or "warning" or "high" or "critical" ? x : "info";
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static object DbText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static Guid ResolveTenantId(HttpContext http)
    {
        var claimValue =
            http.User.FindFirst("tenant_id")?.Value ??
            http.User.FindFirst("tenantId")?.Value ??
            DefaultTenantId.ToString();

        return Guid.TryParse(claimValue, out var tenantId) ? tenantId : DefaultTenantId;
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