using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

public static class Phase2PilotReadinessEndpoints
{
    public static IEndpointRouteBuilder MapPhase2PilotReadinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/phase2/pilot")
            .WithTags("Admin - Phase 2 Pilot Readiness")
            .RequireAuthorization("PlantProcessAdmin");

        // PPIQ-WF-023
        group.MapGet("/tenant-isolation-decision", GetTenantIsolationDecisionAsync)
            .WithName("GetTenantIsolationDecision")
            .Produces<TenantIsolationDecisionResponse>();

        // PPIQ-WF-024
        group.MapGet("/audit-log", QueryAuditLogAsync)
            .WithName("QueryAuditLog")
            .Produces<AuditLogQueryResponse>();

        // PPIQ-WEB-001 / PPIQ-DEMO-023
        group.MapGet("/deployment-checklist", GetDeploymentChecklistAsync)
            .WithName("GetDeploymentChecklist")
            .Produces<DeploymentChecklistResponse>();

        // PPIQ-DEMO-017
        group.MapPost("/demo-language/audit", AuditDemoLanguageAsync)
            .WithName("AuditDemoLanguage")
            .Produces<DemoLanguageAuditResponse>();

        group.MapGet("/demo-language/rules", GetDemoLanguageRulesAsync)
            .WithName("GetDemoLanguageRules")
            .Produces<DemoLanguageRulesResponse>();

        return app;
    }

    private static async Task<IResult> GetTenantIsolationDecisionAsync(
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = new List<TenantIsolationDecisionRow>();

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                decision_code,
                decision_title,
                selected_model,
                decision_status,
                decision_reason,
                allowed_scope,
                blocked_scope,
                valid_from_utc,
                valid_until_utc,
                approved_by,
                evidence_json::text
            FROM public.tenant_isolation_decisions
            WHERE is_deleted = false
            ORDER BY created_at_utc DESC
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new TenantIsolationDecisionRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetDateTime(8),
                reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetString(11)));
        }

        return Results.Ok(new TenantIsolationDecisionResponse(
            DateTime.UtcNow,
            "Phase 2 pilot scope is single-tenant by decision. No shared customer data model is allowed in early pilots.",
            rows));
    }

    private static async Task<IResult> QueryAuditLogAsync(
        string? endpoint,
        string? userName,
        string? actionCategory,
        string? outcomeStatus,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? limit,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit ?? 100, 1, 500);

        var rows = new List<AuditLogRow>();

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                http_method,
                endpoint,
                action_category,
                outcome_status,
                user_id,
                user_name,
                resource_type,
                resource_id,
                client_ip,
                correlation_id,
                http_status_code,
                metadata_json::text,
                created_at_utc
            FROM public.audit_log_entries
            WHERE (@endpoint IS NULL OR endpoint ILIKE '%' || @endpoint || '%')
              AND (@user_name IS NULL OR user_name ILIKE '%' || @user_name || '%')
              AND (@action_category IS NULL OR action_category = @action_category)
              AND (@outcome_status IS NULL OR outcome_status = @outcome_status)
              AND (@from_utc IS NULL OR created_at_utc >= @from_utc)
              AND (@to_utc IS NULL OR created_at_utc < @to_utc)
            ORDER BY created_at_utc DESC
            LIMIT @limit
            """;

        command.Parameters.AddWithValue("endpoint", (object?)endpoint ?? DBNull.Value);
        command.Parameters.AddWithValue("user_name", (object?)userName ?? DBNull.Value);
        command.Parameters.AddWithValue("action_category", (object?)actionCategory ?? DBNull.Value);
        command.Parameters.AddWithValue("outcome_status", (object?)outcomeStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("from_utc", (object?)fromUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("to_utc", (object?)toUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", safeLimit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AuditLogRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.GetDateTime(13)));
        }

        return Results.Ok(new AuditLogQueryResponse(DateTime.UtcNow, rows.Count, rows));
    }

    private static IResult GetDeploymentChecklistAsync()
    {
        var rows = new[]
        {
            new DeploymentChecklistRow("PPIQ-WEB-001", "Public DNS points to server", "Manual", false, "Create A records for app, api, and website hostnames."),
            new DeploymentChecklistRow("PPIQ-DEMO-023", "Caddy reverse proxy configured", "File", false, "Deploy Caddyfile and verify HTTPS."),
            new DeploymentChecklistRow("PPIQ-DEMO-023", "Let’s Encrypt certificates issued", "Runtime", false, "Verify https:// hostnames in browser."),
            new DeploymentChecklistRow("PPIQ-WEB-001", "Website build deployed", "Runtime", false, "Website static dist served by Caddy."),
            new DeploymentChecklistRow("PPIQ-WEB-001", "API health reachable", "Runtime", false, "GET /health returns 200 through public API hostname."),
            new DeploymentChecklistRow("PPIQ-WEB-001", "Demo language truth audit passed", "Validation", false, "No AI/root-cause/MES-replacement overclaiming.")
        };

        return Results.Ok(new DeploymentChecklistResponse(
            DateTime.UtcNow,
            "This endpoint is a pilot-readiness checklist. It does not modify DNS or certificates.",
            rows));
    }

    private static async Task<IResult> GetDemoLanguageRulesAsync(
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = new List<DemoLanguageRuleRow>();

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                rule_code,
                forbidden_phrase,
                safer_replacement,
                severity,
                rationale,
                is_active
            FROM public.demo_language_truth_rules
            WHERE is_active = true
            ORDER BY severity, rule_code
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DemoLanguageRuleRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetBoolean(6)));
        }

        return Results.Ok(new DemoLanguageRulesResponse(DateTime.UtcNow, rows));
    }

    private static async Task<IResult> AuditDemoLanguageAsync(
        [FromBody] DemoLanguageAuditRequest request,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return Results.BadRequest(new { message = "Text is required." });

        var rulesResult = await GetDemoLanguageRulesForAuditAsync(dbContext, cancellationToken);

        var findings = rulesResult
            .Where(rule => request.Text.Contains(rule.ForbiddenPhrase, StringComparison.OrdinalIgnoreCase))
            .Select(rule => new DemoLanguageFinding(
                rule.RuleCode,
                rule.ForbiddenPhrase,
                rule.SaferReplacement,
                rule.Severity,
                rule.Rationale))
            .ToList();

        return Results.Ok(new DemoLanguageAuditResponse(
            DateTime.UtcNow,
            findings.Count == 0,
            findings.Count == 0
                ? "Demo language audit passed."
                : "Demo language audit found risky claims. Replace them before customer use.",
            findings));
    }

    private static async Task<IReadOnlyList<DemoLanguageRuleRow>> GetDemoLanguageRulesForAuditAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = new List<DemoLanguageRuleRow>();

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                rule_code,
                forbidden_phrase,
                safer_replacement,
                severity,
                rationale,
                is_active
            FROM public.demo_language_truth_rules
            WHERE is_active = true
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DemoLanguageRuleRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetBoolean(6)));
        }

        return rows;
    }
}

public sealed record TenantIsolationDecisionResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<TenantIsolationDecisionRow> Rows);

public sealed record TenantIsolationDecisionRow(
    Guid Id,
    string DecisionCode,
    string DecisionTitle,
    string SelectedModel,
    string DecisionStatus,
    string DecisionReason,
    string AllowedScope,
    string BlockedScope,
    DateTime ValidFromUtc,
    DateTime? ValidUntilUtc,
    string? ApprovedBy,
    string EvidenceJson);

public sealed record AuditLogQueryResponse(
    DateTime GeneratedAtUtc,
    int Count,
    IReadOnlyList<AuditLogRow> Rows);

public sealed record AuditLogRow(
    Guid Id,
    string HttpMethod,
    string Endpoint,
    string ActionCategory,
    string OutcomeStatus,
    string? UserId,
    string? UserName,
    string? ResourceType,
    string? ResourceId,
    string? ClientIp,
    string? CorrelationId,
    int? HttpStatusCode,
    string? MetadataJson,
    DateTime CreatedAtUtc);

public sealed record DeploymentChecklistResponse(
    DateTime GeneratedAtUtc,
    string Message,
    IReadOnlyList<DeploymentChecklistRow> Rows);

public sealed record DeploymentChecklistRow(
    string TaskId,
    string Name,
    string VerificationType,
    bool IsVerified,
    string EvidenceRequired);

public sealed record DemoLanguageRulesResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<DemoLanguageRuleRow> Rows);

public sealed record DemoLanguageRuleRow(
    Guid Id,
    string RuleCode,
    string ForbiddenPhrase,
    string SaferReplacement,
    string Severity,
    string Rationale,
    bool IsActive);

public sealed record DemoLanguageAuditRequest(string Text);

public sealed record DemoLanguageAuditResponse(
    DateTime GeneratedAtUtc,
    bool IsClean,
    string Message,
    IReadOnlyList<DemoLanguageFinding> Findings);

public sealed record DemoLanguageFinding(
    string RuleCode,
    string ForbiddenPhrase,
    string SaferReplacement,
    string Severity,
    string Rationale);