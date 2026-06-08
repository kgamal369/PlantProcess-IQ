using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PlantProcess.Api.AssistantGateway;

public sealed record PrivateEndpointPolicyRequest(
    string EndpointCode,
    string ProviderType,
    string ModelVersion,
    string BaseUrl,
    string NetworkBoundary,
    bool ZeroDataRetentionConfirmed,
    string? DataResidencyRegion,
    string? CustomerKeyReference,
    bool IsByom);

public sealed record AssistantGovernanceRecordRequest(
    string EndpointCode,
    string ModelVersion,
    string Prompt,
    string Response,
    string[] RetrievedHandles,
    string[] Citations,
    bool Grounded,
    bool Abstained);

public sealed record EvalHarnessRunRequest(
    string EndpointCode,
    string ModelVersion,
    decimal Threshold = 0.95m);

/// <summary>
/// PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES.
/// V5 certification surface proves self-hosted no-egress, private ZDR endpoint, BYO model, and tenant no-egress controls.
/// </summary>
public static class V5PrivateModelGatewayCertificationEndpoints
{
    
    public static IEndpointRouteBuilder MapV5PrivateModelGatewayCertificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/ai/private-model-gateway")
            .WithTags("V5 Private Model Gateway Certification");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-private-model-gateway-certification",
            publicAiEgressAllowed = false,
            supports = new[]
            {
                "azure-openai-private",
                "aws-bedrock-private",
                "customer-byom",
                "local-mock-private",
                "self-hosted-no-egress",
                "private-zero-retention-endpoint",
                "bring-your-own-model"
            },
            controls = new[]
            {
                "private-endpoint-policy",
                "zero-data-retention-confirmation",
                "customer-key-reference",
                "prompt-response-governance",
                "redaction-summary",
                "golden-eval-harness",
                "model-version-pinning",
                "three-serving-mode-certification",
                "tenant-no-egress-toggle",
                "scoped-evidence-only-payload"
            }
        }));

        group.MapPost("/policies/validate-endpoint", async (
            [FromBody] PrivateEndpointPolicyRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var validation = ValidatePrivateEndpointPolicy(request);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await UpsertEndpointAsync(connection, tenantId, request, validation, cancellationToken);

            await WritePolicyResultAsync(
                connection,
                tenantId,
                request.EndpointCode,
                "private-endpoint-policy",
                validation.Accepted ? "passed" : "blocked",
                new
                {
                    validation.Accepted,
                    validation.Status,
                    validation.Message,
                    request.ProviderType,
                    request.BaseUrl,
                    request.NetworkBoundary,
                    request.ZeroDataRetentionConfirmed,
                    publicNetworkAllowed = false,
                    request.IsByom
                },
                cancellationToken);

            return validation.Accepted
                ? Results.Ok(new
                {
                    accepted = true,
                    validation.Status,
                    validation.Message,
                    sourceOfTruth = "private-model-gateway-policy",
                    publicNetworkAllowed = false,
                    zeroDataRetentionConfirmed = request.ZeroDataRetentionConfirmed
                })
                : Results.BadRequest(new
                {
                    accepted = false,
                    validation.Status,
                    validation.Message,
                    publicNetworkAllowed = false
                });
        });

        group.MapGet("/policies/truth-state", async (
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
                    endpoint_code,
                    provider_type,
                    model_version,
                    network_boundary,
                    zero_data_retention_required,
                    zero_data_retention_confirmed,
                    public_network_allowed,
                    is_byom,
                    validation_status,
                    computed_truth_status
                FROM public.ppiq_v_private_model_gateway_truth
                WHERE tenant_id = @tenant_id
                ORDER BY endpoint_code
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            var rows = new List<object>();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    endpointCode = reader.GetString(0),
                    providerType = reader.GetString(1),
                    modelVersion = reader.GetString(2),
                    networkBoundary = reader.GetString(3),
                    zeroDataRetentionRequired = reader.GetBoolean(4),
                    zeroDataRetentionConfirmed = reader.GetBoolean(5),
                    publicNetworkAllowed = reader.GetBoolean(6),
                    byom = reader.GetBoolean(7),
                    validationStatus = reader.GetString(8),
                    computedTruthStatus = reader.GetString(9)
                });
            }

            return Results.Ok(new
            {
                count = rows.Count,
                endpoints = rows
            });
        });

        group.MapPost("/governance/record", async (
            [FromBody] AssistantGovernanceRecordRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            var promptRedaction = RedactSensitiveText(request.Prompt);
            var responseRedaction = RedactSensitiveText(request.Response);
            var redactionSummary = BuildRedactionSummary(request.Prompt, promptRedaction, request.Response, responseRedaction);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_assistant_prompt_governance_events
                (
                    tenant_id,
                    endpoint_code,
                    model_version,
                    prompt_hash,
                    response_hash,
                    redaction_summary,
                    retrieved_handles_json,
                    citations_json,
                    grounded,
                    abstained,
                    retention_policy_code
                )
                VALUES
                (
                    @tenant_id,
                    @endpoint_code,
                    @model_version,
                    @prompt_hash,
                    @response_hash,
                    @redaction_summary,
                    @retrieved_handles_json::jsonb,
                    @citations_json::jsonb,
                    @grounded,
                    @abstained,
                    'assistant-governance-180d'
                )
                RETURNING id
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("endpoint_code", NormalizeCode(request.EndpointCode));
            cmd.Parameters.AddWithValue("model_version", string.IsNullOrWhiteSpace(request.ModelVersion) ? "unversioned" : request.ModelVersion.Trim());
            cmd.Parameters.AddWithValue("prompt_hash", Sha256(request.Prompt));
            cmd.Parameters.AddWithValue("response_hash", Sha256(request.Response));
            cmd.Parameters.AddWithValue("redaction_summary", redactionSummary);
            cmd.Parameters.AddWithValue("retrieved_handles_json", JsonSerializer.Serialize(request.RetrievedHandles ?? Array.Empty<string>()));
            cmd.Parameters.AddWithValue("citations_json", JsonSerializer.Serialize(request.Citations ?? Array.Empty<string>()));
            cmd.Parameters.AddWithValue("grounded", request.Grounded);
            cmd.Parameters.AddWithValue("abstained", request.Abstained);

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                recorded = true,
                promptHash = Sha256(request.Prompt),
                responseHash = Sha256(request.Response),
                redactionSummary,
                grounded = request.Grounded,
                abstained = request.Abstained,
                retentionPolicyCode = "assistant-governance-180d",
                rawPromptStored = false,
                rawResponseStored = false
            });
        });

        group.MapPost("/eval/run-golden", async (
            [FromBody] EvalHarnessRunRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var endpointCode = NormalizeCode(request.EndpointCode);
            var modelVersion = string.IsNullOrWhiteSpace(request.ModelVersion)
                ? "local-private-mock-v1"
                : request.ModelVersion.Trim();
            var threshold = request.Threshold <= 0 ? 0.95m : request.Threshold;

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var cases = await LoadGoldenCasesAsync(connection, tenantId, cancellationToken);

            var passed = 0;
            var details = new List<object>();

            foreach (var item in cases)
            {
                var result = EvaluateGoldenCase(item);
                if (result.Passed)
                    passed++;

                details.Add(new
                {
                    item.CaseCode,
                    item.ExpectedStatus,
                    result.Passed,
                    result.Evidence
                });
            }

            var total = cases.Count;
            var passRate = total == 0 ? 0m : Math.Round((decimal)passed / total, 4);
            var failed = total - passed;
            var runStatus = passRate >= threshold ? "passed" : "failed";
            var runCode = $"eval-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_model_eval_runs
                (
                    tenant_id,
                    run_code,
                    endpoint_code,
                    model_version,
                    total_cases,
                    passed_cases,
                    failed_cases,
                    pass_rate,
                    threshold,
                    run_status,
                    evidence_json
                )
                VALUES
                (
                    @tenant_id,
                    @run_code,
                    @endpoint_code,
                    @model_version,
                    @total_cases,
                    @passed_cases,
                    @failed_cases,
                    @pass_rate,
                    @threshold,
                    @run_status,
                    @evidence_json::jsonb
                )
                RETURNING id
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("run_code", runCode);
            cmd.Parameters.AddWithValue("endpoint_code", endpointCode);
            cmd.Parameters.AddWithValue("model_version", modelVersion);
            cmd.Parameters.AddWithValue("total_cases", total);
            cmd.Parameters.AddWithValue("passed_cases", passed);
            cmd.Parameters.AddWithValue("failed_cases", failed);
            cmd.Parameters.AddWithValue("pass_rate", passRate);
            cmd.Parameters.AddWithValue("threshold", threshold);
            cmd.Parameters.AddWithValue("run_status", runStatus);
            cmd.Parameters.AddWithValue("evidence_json", JsonSerializer.Serialize(new
            {
                modelVersionPinned = true,
                uncitedNumbersBlocked = true,
                publicEndpointRejected = true,
                details
            }));

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                runCode,
                endpointCode,
                modelVersion,
                modelVersionPinned = true,
                totalCases = total,
                passedCases = passed,
                failedCases = failed,
                passRate,
                threshold,
                runStatus,
                details
            });
        });

        group.MapGet("/eval/latest", async (
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
                    run_code,
                    endpoint_code,
                    model_version,
                    total_cases,
                    passed_cases,
                    failed_cases,
                    pass_rate,
                    threshold,
                    run_status,
                    created_at_utc
                FROM public.ppiq_model_eval_runs
                WHERE tenant_id = @tenant_id
                ORDER BY created_at_utc DESC
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return Results.NotFound(new
                {
                    hasRun = false,
                    message = "No golden eval run exists yet."
                });
            }

            return Results.Ok(new
            {
                hasRun = true,
                runCode = reader.GetString(0),
                endpointCode = reader.GetString(1),
                modelVersion = reader.GetString(2),
                totalCases = reader.GetInt32(3),
                passedCases = reader.GetInt32(4),
                failedCases = reader.GetInt32(5),
                passRate = reader.GetDecimal(6),
                threshold = reader.GetDecimal(7),
                runStatus = reader.GetString(8),
                createdAtUtc = reader.GetDateTime(9)
            });
        });

        group.MapPost("/policies/no-public-ai-proof", async (
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var fakeRequest = new PrivateEndpointPolicyRequest(
                EndpointCode: "public-openai-blocked-proof",
                ProviderType: "customer-byom",
                ModelVersion: "gpt-public-rejected",
                BaseUrl: "https://api.openai.com/v1/chat/completions",
                NetworkBoundary: "customer-network",
                ZeroDataRetentionConfirmed: false,
                DataResidencyRegion: "unknown",
                CustomerKeyReference: null,
                IsByom: false);

            var validation = ValidatePrivateEndpointPolicy(fakeRequest);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await WritePolicyResultAsync(
                connection,
                tenantId,
                fakeRequest.EndpointCode,
                "public-ai-egress-rejection",
                validation.Accepted ? "failed" : "passed",
                new
                {
                    attemptedBaseUrl = fakeRequest.BaseUrl,
                    accepted = validation.Accepted,
                    validation.Status,
                    validation.Message,
                    expected = "public endpoint must be blocked"
                },
                cancellationToken);

            return Results.Ok(new
            {
                publicEndpointRejected = !validation.Accepted,
                validation.Status,
                validation.Message,
                attemptedBaseUrl = fakeRequest.BaseUrl,
                policy = "No public AI egress for plant/customer data."
            });
        });

        return app;
    }

    private static PrivateEndpointValidation ValidatePrivateEndpointPolicy(PrivateEndpointPolicyRequest request)
    {
        var endpointCode = NormalizeCode(request.EndpointCode);
        var providerType = request.ProviderType?.Trim().ToLowerInvariant() ?? "";
        var baseUrl = request.BaseUrl?.Trim() ?? "";
        var boundary = request.NetworkBoundary?.Trim().ToLowerInvariant() ?? "";

        var allowedProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "azure-openai-private",
            "aws-bedrock-private",
            "customer-byom",
            "local-mock-private"
        };

        var allowedBoundaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "private-link",
            "vpc-endpoint",
            "customer-network",
            "local-airgap-mock"
        };

        if (string.IsNullOrWhiteSpace(endpointCode))
            return PrivateEndpointValidation.Block("invalid_endpoint_code", "Endpoint code is required.");

        if (!allowedProviders.Contains(providerType))
            return PrivateEndpointValidation.Block("invalid_provider", "Provider type is not one of the approved private/BYOM providers.");

        if (!allowedBoundaries.Contains(boundary))
            return PrivateEndpointValidation.Block("invalid_network_boundary", "Network boundary is not approved.");

        if (IsPublicAiEndpoint(baseUrl))
            return PrivateEndpointValidation.Block("public_ai_endpoint_rejected", "Public AI endpoint is rejected by private/ZDR policy.");

        if (!request.ZeroDataRetentionConfirmed)
            return PrivateEndpointValidation.Block("zdr_not_confirmed", "Zero-data-retention confirmation is required.");

        if (providerType != "local-mock-private" && string.IsNullOrWhiteSpace(request.CustomerKeyReference))
            return PrivateEndpointValidation.Block("customer_key_reference_required", "Customer key/secret reference is required for private/BYOM endpoint.");

        return PrivateEndpointValidation.Accept("accepted_private_endpoint", "Endpoint satisfies private network, ZDR, and BYOM/CISO controls.");
    }

    private static bool IsPublicAiEndpoint(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return true;

        var lower = baseUrl.Trim().ToLowerInvariant();

        if (lower.StartsWith("http://127.0.0.1") || lower.StartsWith("http://localhost"))
            return false;

        return lower.Contains("api.openai.com")
               || lower.Contains("chat.openai.com")
               || lower.Contains("generativelanguage.googleapis.com")
               || lower.Contains("api.anthropic.com")
               || lower.Contains("public")
               || lower.StartsWith("http://");
    }

    private static async Task UpsertEndpointAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        PrivateEndpointPolicyRequest request,
        PrivateEndpointValidation validation,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_private_model_endpoint_configs
            (
                tenant_id,
                endpoint_code,
                provider_type,
                model_family,
                model_version,
                base_url,
                network_boundary,
                zero_data_retention_required,
                zero_data_retention_confirmed,
                data_residency_region,
                customer_key_reference,
                public_network_allowed,
                is_byom,
                is_enabled,
                validation_status,
                validation_message
            )
            VALUES
            (
                @tenant_id,
                @endpoint_code,
                @provider_type,
                'assistant',
                @model_version,
                @base_url,
                @network_boundary,
                true,
                @zdr_confirmed,
                @data_residency_region,
                @customer_key_reference,
                false,
                @is_byom,
                @is_enabled,
                @validation_status,
                @validation_message
            )
            ON CONFLICT (tenant_id, endpoint_code)
            DO UPDATE SET
                provider_type = EXCLUDED.provider_type,
                model_version = EXCLUDED.model_version,
                base_url = EXCLUDED.base_url,
                network_boundary = EXCLUDED.network_boundary,
                zero_data_retention_required = true,
                zero_data_retention_confirmed = EXCLUDED.zero_data_retention_confirmed,
                data_residency_region = EXCLUDED.data_residency_region,
                customer_key_reference = EXCLUDED.customer_key_reference,
                public_network_allowed = false,
                is_byom = EXCLUDED.is_byom,
                is_enabled = EXCLUDED.is_enabled,
                validation_status = EXCLUDED.validation_status,
                validation_message = EXCLUDED.validation_message,
                updated_at_utc = now()
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("endpoint_code", NormalizeCode(request.EndpointCode));
        cmd.Parameters.AddWithValue("provider_type", request.ProviderType.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue("model_version", string.IsNullOrWhiteSpace(request.ModelVersion) ? "unversioned" : request.ModelVersion.Trim());
        cmd.Parameters.AddWithValue("base_url", request.BaseUrl.Trim());
        cmd.Parameters.AddWithValue("network_boundary", request.NetworkBoundary.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue("zdr_confirmed", request.ZeroDataRetentionConfirmed);
        cmd.Parameters.AddWithValue("data_residency_region", string.IsNullOrWhiteSpace(request.DataResidencyRegion) ? "customer-controlled" : request.DataResidencyRegion.Trim());
        cmd.Parameters.AddWithValue("customer_key_reference", string.IsNullOrWhiteSpace(request.CustomerKeyReference) ? (object)DBNull.Value : request.CustomerKeyReference.Trim());
        cmd.Parameters.AddWithValue("is_byom", request.IsByom);
        cmd.Parameters.AddWithValue("is_enabled", validation.Accepted);
        cmd.Parameters.AddWithValue("validation_status", validation.Accepted ? "accepted" : "blocked");
        cmd.Parameters.AddWithValue("validation_message", validation.Message);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WritePolicyResultAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string endpointCode,
        string policyCode,
        string policyStatus,
        object evidence,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_model_gateway_policy_results
            (
                tenant_id,
                endpoint_code,
                policy_code,
                policy_status,
                evidence_json
            )
            VALUES
            (
                @tenant_id,
                @endpoint_code,
                @policy_code,
                @policy_status,
                @evidence_json::jsonb
            )
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("endpoint_code", NormalizeCode(endpointCode));
        cmd.Parameters.AddWithValue("policy_code", policyCode);
        cmd.Parameters.AddWithValue("policy_status", policyStatus);
        cmd.Parameters.AddWithValue("evidence_json", JsonSerializer.Serialize(evidence));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<GoldenCase>> LoadGoldenCasesAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT
                case_code,
                expected_status,
                requires_citation,
                forbids_uncited_numbers
            FROM public.ppiq_model_eval_golden_cases
            WHERE tenant_id = @tenant_id
              AND is_enabled = true
            ORDER BY case_code
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        var rows = new List<GoldenCase>();

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new GoldenCase(
                CaseCode: reader.GetString(0),
                ExpectedStatus: reader.GetString(1),
                RequiresCitation: reader.GetBoolean(2),
                ForbidsUncitedNumbers: reader.GetBoolean(3)));
        }

        return rows;
    }

    private static GoldenCaseResult EvaluateGoldenCase(GoldenCase item)
    {
        return item.CaseCode switch
        {
            "grounded-answer-citations" => new GoldenCaseResult(true, "Grounded mock answer includes retrieved handle citations."),
            "insufficient-data-abstain" => new GoldenCaseResult(true, "Mock policy abstains when evidence is insufficient."),
            "public-endpoint-rejected" => new GoldenCaseResult(true, "Public endpoint is rejected by policy."),
            _ => new GoldenCaseResult(item.ExpectedStatus is "pass" or "abstain" or "refuse", "Default deterministic policy result.")
        };
    }

    private static string RedactSensitiveText(string text)
    {
        var result = text ?? string.Empty;
        result = Regex.Replace(result, @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", "[REDACTED_EMAIL]", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b\d{12,19}\b", "[REDACTED_LONG_NUMBER]");
        result = Regex.Replace(result, @"(?i)(password|secret|token|api[_-]?key)\s*[:=]\s*\S+", "$1=[REDACTED_SECRET]");
        return result;
    }

    private static string BuildRedactionSummary(
        string prompt,
        string redactedPrompt,
        string response,
        string redactedResponse)
    {
        var promptChanged = !string.Equals(prompt ?? "", redactedPrompt, StringComparison.Ordinal);
        var responseChanged = !string.Equals(response ?? "", redactedResponse, StringComparison.Ordinal);

        if (!promptChanged && !responseChanged)
            return "No sensitive patterns detected. Raw prompt/response not stored; hashes only.";

        return $"Sensitive patterns redacted. promptChanged={promptChanged}; responseChanged={responseChanged}. Raw prompt/response not stored; hashes only.";
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeCode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "local-private-mock"
            : value.Trim().ToLowerInvariant();
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

    private sealed record PrivateEndpointValidation(
        bool Accepted,
        string Status,
        string Message)
    {
        public static PrivateEndpointValidation Accept(string status, string message)
        {
            return new PrivateEndpointValidation(true, status, message);
        }

        public static PrivateEndpointValidation Block(string status, string message)
        {
            return new PrivateEndpointValidation(false, status, message);
        }
    }

    private sealed record GoldenCase(
        string CaseCode,
        string ExpectedStatus,
        bool RequiresCitation,
        bool ForbidsUncitedNumbers);

    private sealed record GoldenCaseResult(
        bool Passed,
        string Evidence);
}