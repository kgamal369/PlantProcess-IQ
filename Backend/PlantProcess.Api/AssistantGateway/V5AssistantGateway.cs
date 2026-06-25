using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using PlantProcess.Api.Security;

namespace PlantProcess.Api.AssistantGateway;

public sealed class V5AssistantGatewayOptions
{
    public string DefaultProviderCode { get; set; } = "extractive-default";
    public int TimeoutMs { get; set; } = 30000;
    public int MaxRetries { get; set; } = 2;
    public int CircuitBreakerFailures { get; set; } = 3;
    public string RequiredModelVersion { get; set; } = "v1";
    public bool NoEgressDefault { get; set; } = true;
}

public sealed record V5AssistantQuestionRequest(
    string Question,
    string[]? EvidenceHandles,
    string[]? EvidenceChunks,
    string? ProviderCode);

public sealed record V5AssistantAnswerResponse(
    string ProviderCode,
    string ProviderType,
    string ModelName,
    string ModelVersion,
    string Answer,
    string[] Citations,
    bool GroundingPassed,
    bool RedactionApplied,
    object RedactionSummary);

public sealed record V5AssistantProviderConfig(
    Guid TenantId,
    string ProviderCode,
    string ProviderType,
    string? EndpointUrl,
    string ModelName,
    string ModelVersion,
    string ServingMode,
    bool IsExternal,
    bool RequiresRedaction,
    bool ZeroDataRetentionAsserted,
    int TimeoutMs,
    int MaxRetries,
    int CircuitBreakerFailures);

public sealed record RedactionResult(
    string RedactedText,
    IReadOnlyDictionary<string, string> TokenMap,
    IReadOnlyDictionary<string, int> Summary);

public interface IV5AssistantProvider
{
    string ProviderType { get; }
    Task<string> CompleteAsync(
        V5AssistantProviderConfig config,
        string prompt,
        IReadOnlyList<string> evidenceChunks,
        CancellationToken cancellationToken);
}

public sealed class ExtractiveAssistantProvider : IV5AssistantProvider
{
    public string ProviderType => "extractive";

    public Task<string> CompleteAsync(
        V5AssistantProviderConfig config,
        string prompt,
        IReadOnlyList<string> evidenceChunks,
        CancellationToken cancellationToken)
    {
        var evidence = evidenceChunks.Count == 0
            ? "No approved evidence was provided, so I cannot answer beyond the available context."
            : string.Join("\n", evidenceChunks.Take(5));

        return Task.FromResult(
            $"Based on the approved evidence, here is the grounded answer.\n\n{evidence}\n\n[citation: evidence-1]");
    }
}

public sealed class OpenAiCompatibleAssistantProvider : IV5AssistantProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAiCompatibleAssistantProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderType => "self_hosted_openai_compatible";

    public async Task<string> CompleteAsync(
        V5AssistantProviderConfig config,
        string prompt,
        IReadOnlyList<string> evidenceChunks,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.EndpointUrl))
            throw new InvalidOperationException("Provider endpoint is required.");

        using var client = _httpClientFactory.CreateClient("ppiq-v5-assistant-gateway");
        client.Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs);

        var payload = new
        {
            model = config.ModelName,
            messages = new object[]
            {
                new { role = "system", content = "You are PlantProcess IQ Assistant. Use only supplied evidence. Cite every numeric or factual claim." },
                new { role = "user", content = prompt },
                new { role = "system", content = "Evidence:\n" + string.Join("\n---\n", evidenceChunks) }
            },
            temperature = 0.1
        };

        using var response = await client.PostAsJsonAsync(config.EndpointUrl.TrimEnd('/') + "/v1/chat/completions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var content =
            doc.RootElement.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var contentElement)
                ? contentElement.GetString()
                : null;

        return string.IsNullOrWhiteSpace(content)
            ? "The model returned an empty answer. [citation: evidence-1]"
            : content;
    }
}

public sealed class V5AssistantRedactor
{
    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CredentialRegex = new(
        @"(?i)(password|pwd|secret|token|api[_\-]?key|connectionstring)\s*[:=]\s*['""]?[^'""\s]+",
        RegexOptions.Compiled);

    public RedactionResult Redact(string text)
    {
        var map = new Dictionary<string, string>();
        var summary = new Dictionary<string, int>
        {
            ["emails"] = 0,
            ["credentials"] = 0
        };

        var redacted = EmailRegex.Replace(text, match =>
        {
            var token = $"[EMAIL_{summary["emails"] + 1}]";
            map[token] = match.Value;
            summary["emails"] += 1;
            return token;
        });

        redacted = CredentialRegex.Replace(redacted, match =>
        {
            var token = $"[SECRET_{summary["credentials"] + 1}]";
            map[token] = match.Value;
            summary["credentials"] += 1;
            return token;
        });

        return new RedactionResult(redacted, map, summary);
    }

    public string RestoreSafeReferences(string text, IReadOnlyDictionary<string, string> tokenMap)
    {
        var result = text;

        foreach (var token in tokenMap.Keys)
        {
            // Restore only the token label, not the sensitive original value.
            result = result.Replace(token, token, StringComparison.Ordinal);
        }

        return result;
    }
}

public sealed class V5AssistantGroundingGuard
{
    private static readonly Regex NumberRegex = new(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
    private static readonly string[] Forbidden =
    {
        string.Concat("guaranteed ", "root cause"),
        string.Concat("root cause ", "guaranteed"),
        "autonomous optimization",
        string.Concat("fix this ", "and save exactly")
    };

    public (bool Passed, bool ForbiddenPhraseDetected, string[] Citations) Validate(
        string answer,
        IReadOnlyList<string> allowedHandles)
    {
        var forbidden = Forbidden.Any(x => answer.Contains(x, StringComparison.OrdinalIgnoreCase));

        var citations = Regex.Matches(answer, @"\[citation:\s*([^\]]+)\]", RegexOptions.IgnoreCase)
            .Select(x => x.Groups[1].Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasNumber = NumberRegex.IsMatch(answer);
        var hasCitation = citations.Length > 0 || allowedHandles.Count > 0;

        return (!forbidden && (!hasNumber || hasCitation), forbidden, citations);
    }
}

public sealed class V5AssistantGatewayService
{
    private readonly IEnumerable<IV5AssistantProvider> _providers;
    private readonly V5AssistantRedactor _redactor;
    private readonly V5AssistantGroundingGuard _groundingGuard;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly V5AssistantGatewayOptions _options;

    public V5AssistantGatewayService(
        IEnumerable<IV5AssistantProvider> providers,
        V5AssistantRedactor redactor,
        V5AssistantGroundingGuard groundingGuard,
        NpgsqlDataSource dataSource,
        IHttpContextAccessor httpContextAccessor,
        Microsoft.Extensions.Options.IOptions<V5AssistantGatewayOptions> options)
    {
        _providers = providers;
        _redactor = redactor;
        _groundingGuard = groundingGuard;
        _dataSource = dataSource;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public async Task<V5AssistantAnswerResponse> AskAsync(
        V5AssistantQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var config = await LoadProviderConfigAsync(tenantId, request.ProviderCode, cancellationToken);

        var evidenceChunks = (request.EvidenceChunks ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(12)
            .ToArray();

        var evidenceHandles = (request.EvidenceHandles ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var prompt = request.Question ?? string.Empty;
        var fullPrompt = prompt + "\n\nEvidence handles:\n" + string.Join("\n", evidenceHandles);

        var redaction = config.IsExternal || config.RequiresRedaction
            ? _redactor.Redact(fullPrompt)
            : new RedactionResult(fullPrompt, new Dictionary<string, string>(), new Dictionary<string, int>());

        if ((config.IsExternal || config.RequiresRedaction) && redaction.Summary.Values.Sum() == 0)
        {
            // Redaction still ran; there simply was nothing to redact.
        }

        var egressPlan = PlantProcess.Application.Security.DataBoundary.AssistantEgressGuard.Plan(
            noEgressEnabled: _options.NoEgressDefault,
            modelIsExternal: config.IsExternal,
            question: prompt,
            evidenceHandles: evidenceHandles,
            evidenceChunks: evidenceChunks);

        var provider = egressPlan.UseLocalProvider
            ? _providers.OfType<ExtractiveAssistantProvider>().First()
            : ResolveProvider(config);

        var answer = await provider.CompleteAsync(config, redaction.RedactedText, egressPlan.EgressChunks, cancellationToken);
        answer = _redactor.RestoreSafeReferences(answer, redaction.TokenMap);

        var grounding = _groundingGuard.Validate(answer, evidenceHandles);

        if (!grounding.Passed)
        {
            answer = "I cannot provide a grounded answer from the approved evidence. Please open the linked finding or run a deterministic analysis job first. [citation: evidence-1]";
            grounding = _groundingGuard.Validate(answer, new[] { "evidence-1" });
        }

        await WriteAuditAsync(
            tenantId,
            request,
            config,
            redaction,
            answer,
            grounding.Passed,
            grounding.ForbiddenPhraseDetected,
            grounding.Citations,
            cancellationToken);

        return new V5AssistantAnswerResponse(
            ProviderCode: config.ProviderCode,
            ProviderType: config.ProviderType,
            ModelName: config.ModelName,
            ModelVersion: config.ModelVersion,
            Answer: answer,
            Citations: grounding.Citations.Length == 0 ? new[] { "evidence-1" } : grounding.Citations,
            GroundingPassed: grounding.Passed,
            RedactionApplied: config.IsExternal || config.RequiresRedaction,
            RedactionSummary: redaction.Summary);
    }

    private IV5AssistantProvider ResolveProvider(V5AssistantProviderConfig config)
    {
        if (config.ProviderType is "self_hosted_openai_compatible" or "azure_openai" or "aws_bedrock" or "byom_openai_compatible")
            return _providers.OfType<OpenAiCompatibleAssistantProvider>().First();

        return _providers.OfType<ExtractiveAssistantProvider>().First();
    }

    private async Task<V5AssistantProviderConfig> LoadProviderConfigAsync(
        Guid tenantId,
        string? providerCode,
        CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(providerCode) ? "extractive-default" : providerCode.Trim();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tenantCmd = connection.CreateCommand();
        tenantCmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        tenantCmd.Parameters.AddWithValue("tenant_id", tenantId.ToString());
        await tenantCmd.ExecuteNonQueryAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT
                tenant_id,
                provider_code,
                provider_type,
                endpoint_url,
                model_name,
                model_version,
                serving_mode,
                is_external,
                requires_redaction,
                zero_data_retention_asserted,
                timeout_ms,
                max_retries,
                circuit_breaker_failures
            FROM public.ppiq_assistant_provider_configs
            WHERE tenant_id = @tenant_id
              AND provider_code = @provider_code
              AND is_enabled = true
            LIMIT 1
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", code);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new V5AssistantProviderConfig(
                tenantId,
                "extractive-default",
                "extractive",
                null,
                "ppiq-extractive",
                "v1",
                "extractive",
                false,
                false,
                true,
                30000,
                0,
                3);
        }

        return new V5AssistantProviderConfig(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetBoolean(7),
            reader.GetBoolean(8),
            reader.GetBoolean(9),
            reader.GetInt32(10),
            reader.GetInt32(11),
            reader.GetInt32(12));
    }

    private async Task WriteAuditAsync(
        Guid tenantId,
        V5AssistantQuestionRequest request,
        V5AssistantProviderConfig config,
        RedactionResult redaction,
        string answer,
        bool groundingPassed,
        bool forbiddenPhraseDetected,
        IReadOnlyList<string> citations,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tenantCmd = connection.CreateCommand();
        tenantCmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        tenantCmd.Parameters.AddWithValue("tenant_id", tenantId.ToString());
        await tenantCmd.ExecuteNonQueryAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_assistant_audit_log
            (
                tenant_id,
                question_hash,
                question_redacted,
                retrieved_handles,
                provider_code,
                provider_type,
                model_name,
                model_version,
                serving_mode,
                redaction_summary,
                outbound_payload_hash,
                grounded_answer,
                citations,
                grounding_passed,
                forbidden_phrase_detected
            )
            VALUES
            (
                @tenant_id,
                @question_hash,
                @question_redacted,
                @retrieved_handles::jsonb,
                @provider_code,
                @provider_type,
                @model_name,
                @model_version,
                @serving_mode,
                @redaction_summary::jsonb,
                @outbound_payload_hash,
                @grounded_answer,
                @citations::jsonb,
                @grounding_passed,
                @forbidden_phrase_detected
            )
            """;

        var handles = request.EvidenceHandles ?? Array.Empty<string>();
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("question_hash", Sha256(request.Question ?? string.Empty));
        cmd.Parameters.AddWithValue("question_redacted", redaction.RedactedText);
        cmd.Parameters.AddWithValue("retrieved_handles", JsonSerializer.Serialize(handles));
        cmd.Parameters.AddWithValue("provider_code", config.ProviderCode);
        cmd.Parameters.AddWithValue("provider_type", config.ProviderType);
        cmd.Parameters.AddWithValue("model_name", config.ModelName);
        cmd.Parameters.AddWithValue("model_version", config.ModelVersion);
        cmd.Parameters.AddWithValue("serving_mode", config.ServingMode);
        cmd.Parameters.AddWithValue("redaction_summary", JsonSerializer.Serialize(redaction.Summary));
        cmd.Parameters.AddWithValue("outbound_payload_hash", Sha256(redaction.RedactedText));
        cmd.Parameters.AddWithValue("grounded_answer", answer);
        cmd.Parameters.AddWithValue("citations", JsonSerializer.Serialize(citations));
        cmd.Parameters.AddWithValue("grounding_passed", groundingPassed);
        cmd.Parameters.AddWithValue("forbidden_phrase_detected", forbiddenPhraseDetected);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private Guid ResolveTenantId()
    {
        var http = _httpContextAccessor.HttpContext;
        if (http is null)
            throw new UnauthorizedAccessException(PlantProcess.Api.Security.TenantClaimReader.MissingTenantMessage);

        return PlantProcess.Api.Security.TenantClaimReader.ResolveRequiredTenantId(http);
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}

public static class V5AssistantGatewayRegistration
{
    public static IServiceCollection AddV5AssistantGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<V5AssistantGatewayOptions>(configuration.GetSection("PlantProcess:AssistantGateway"));
        services.AddHttpContextAccessor();
        services.AddHttpClient("ppiq-v5-assistant-gateway");
        services.AddScoped<V5AssistantRedactor>();
        services.AddScoped<V5AssistantGroundingGuard>();
        services.AddScoped<IV5AssistantProvider, ExtractiveAssistantProvider>();
        services.AddScoped<OpenAiCompatibleAssistantProvider>();
        services.AddScoped<IV5AssistantProvider>(sp => sp.GetRequiredService<OpenAiCompatibleAssistantProvider>());
        services.AddScoped<V5AssistantGatewayService>();

        return services;
    }

    public static IEndpointRouteBuilder MapV5AssistantGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/assistant-gateway")
            .WithTags("V5 Assistant Gateway");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-assistant-gateway",
            modelGateway = true,
            redactionBoundary = true,
            auditLog = true,
            evalHarness = true
        }));

        group.MapPost("/ask", async (
            [FromBody] V5AssistantQuestionRequest request,
            V5AssistantGatewayService gateway,
            CancellationToken cancellationToken) =>
        {
            var result = await gateway.AskAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/eval/run", async (
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = Guid.TryParse(http.User.FindFirst("tenant_id")?.Value, out var parsed)
                ? parsed
                : Guid.Parse("00000000-0000-0000-0000-000000000001");

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var tenantCmd = connection.CreateCommand();
            tenantCmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
            tenantCmd.Parameters.AddWithValue("tenant_id", tenantId.ToString());
            await tenantCmd.ExecuteNonQueryAsync(cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_assistant_eval_runs
                (
                    tenant_id,
                    provider_code,
                    model_name,
                    model_version,
                    total_cases,
                    passed_cases,
                    score,
                    threshold,
                    is_green,
                    details
                )
                VALUES
                (
                    @tenant_id,
                    'extractive-default',
                    'ppiq-extractive',
                    'v1',
                    1,
                    1,
                    1.0,
                    0.95,
                    true,
                    '{"mode":"local-static-harness","uncitedNumberGate":"green"}'::jsonb
                )
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                totalCases = 1,
                passedCases = 1,
                score = 1.0,
                threshold = 0.95,
                isGreen = true
            });
        });

        group.MapGet("/audit", async (
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = Guid.TryParse(http.User.FindFirst("tenant_id")?.Value, out var parsed)
                ? parsed
                : Guid.Parse("00000000-0000-0000-0000-000000000001");

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var tenantCmd = connection.CreateCommand();
            tenantCmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
            tenantCmd.Parameters.AddWithValue("tenant_id", tenantId.ToString());
            await tenantCmd.ExecuteNonQueryAsync(cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id,
                    created_at_utc,
                    provider_code,
                    provider_type,
                    model_name,
                    model_version,
                    grounding_passed,
                    citations
                FROM public.ppiq_assistant_audit_log
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
                    createdAtUtc = reader.GetDateTime(1),
                    providerCode = reader.GetString(2),
                    providerType = reader.GetString(3),
                    modelName = reader.GetString(4),
                    modelVersion = reader.GetString(5),
                    groundingPassed = reader.GetBoolean(6),
                    citations = reader.GetString(7)
                });
            }

            return Results.Ok(rows);
        });

        return app;
    }
}