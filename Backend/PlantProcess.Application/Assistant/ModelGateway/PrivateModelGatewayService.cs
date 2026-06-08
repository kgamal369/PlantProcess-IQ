
namespace PlantProcess.Application.Assistant.ModelGateway;

/// <summary>
/// PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES.
/// Enforces three certified serving modes:
/// 1) self-hosted no-egress,
/// 2) private zero-retention endpoint,
/// 3) BYO/customer-owned model endpoint.
/// </summary>
public sealed class PrivateModelGatewayService
{
    private readonly IPrivateModelGatewayTransport _transport;

    public PrivateModelGatewayService(IPrivateModelGatewayTransport transport)
    {
        _transport = transport;
    }

    public async Task<PrivateModelGatewayResult> AskAsync(
        PrivateModelGatewayRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Question);

        ValidatePolicy(request);

        var scopedEvidence = BuildScopedEvidencePayload(request.Evidence);
        var payload = new PrivateModelGatewayPayload(
            Question: request.Question.Trim(),
            ScopedEvidence: scopedEvidence,
            ModelName: request.Endpoint.ModelName,
            ModelVersion: request.Endpoint.ModelVersion,
            ZeroDataRetention: request.Endpoint.ZeroDataRetentionConfirmed,
            ServingMode: request.Endpoint.ServingMode.ToString());

        if (request.Endpoint.ServingMode == PrivateModelServingMode.SelfHostedNoEgress)
        {
            return new PrivateModelGatewayResult(
                Allowed: true,
                OutboundCallAttempted: false,
                ServingMode: request.Endpoint.ServingMode.ToString(),
                ProviderType: request.Endpoint.ProviderType,
                ModelName: request.Endpoint.ModelName,
                ModelVersion: request.Endpoint.ModelVersion,
                Answer: BuildSelfHostedExtractiveAnswer(request.Question, scopedEvidence),
                RefusalReason: string.Empty,
                OutboundPayload: null);
        }

        if (request.Endpoint.EndpointUri is null)
        {
            return new PrivateModelGatewayResult(
                Allowed: false,
                OutboundCallAttempted: false,
                ServingMode: request.Endpoint.ServingMode.ToString(),
                ProviderType: request.Endpoint.ProviderType,
                ModelName: request.Endpoint.ModelName,
                ModelVersion: request.Endpoint.ModelVersion,
                Answer: string.Empty,
                RefusalReason: "Private/BYO endpoint URI is required.",
                OutboundPayload: null);
        }

        var answer = await _transport.CompleteAsync(request.Endpoint.EndpointUri, payload, cancellationToken);

        return new PrivateModelGatewayResult(
            Allowed: true,
            OutboundCallAttempted: true,
            ServingMode: request.Endpoint.ServingMode.ToString(),
            ProviderType: request.Endpoint.ProviderType,
            ModelName: request.Endpoint.ModelName,
            ModelVersion: request.Endpoint.ModelVersion,
            Answer: answer,
            RefusalReason: string.Empty,
            OutboundPayload: payload);
    }

    public static IReadOnlyList<PrivateModelGatewayEvidencePayload> BuildScopedEvidencePayload(
        IReadOnlyList<ScopedEvidenceChunk> evidence)
    {
        return evidence
            .Where(e => !e.IsSynthetic)
            .Where(e => !string.IsNullOrWhiteSpace(e.Handle))
            .Where(e => !string.IsNullOrWhiteSpace(e.Text))
            .Take(12)
            .Select(e => new PrivateModelGatewayEvidencePayload(
                Handle: e.Handle.Trim(),
                Text: e.Text.Trim()))
            .ToArray();
    }

    private static void ValidatePolicy(PrivateModelGatewayRequest request)
    {
        if (request.TenantPolicy.NoEgress &&
            request.Endpoint.ServingMode != PrivateModelServingMode.SelfHostedNoEgress)
        {
            throw new PrivateModelGatewayPolicyViolationException(
                "Tenant no-egress policy blocks external/private/BYO model calls. Use SelfHostedNoEgress mode.");
        }

        if (request.Endpoint.ServingMode == PrivateModelServingMode.PrivateZeroRetentionEndpoint)
        {
            if (!request.TenantPolicy.AllowPrivateEndpoint)
            {
                throw new PrivateModelGatewayPolicyViolationException(
                    "Private endpoint mode is disabled by tenant policy.");
            }

            if (!request.Endpoint.ZeroDataRetentionConfirmed)
            {
                throw new PrivateModelGatewayPolicyViolationException(
                    "Private zero-retention endpoint requires ZeroDataRetentionConfirmed=true.");
            }

            if (request.Endpoint.NetworkBoundary.Contains("public", StringComparison.OrdinalIgnoreCase))
            {
                throw new PrivateModelGatewayPolicyViolationException(
                    "Public network boundary is not allowed for private zero-retention endpoint.");
            }
        }

        if (request.Endpoint.ServingMode == PrivateModelServingMode.BringYourOwnModel)
        {
            if (!request.TenantPolicy.AllowBringYourOwnModel)
            {
                throw new PrivateModelGatewayPolicyViolationException(
                    "BYO model mode is disabled by tenant policy.");
            }

            if (!request.Endpoint.CustomerOwnedEndpoint)
            {
                throw new PrivateModelGatewayPolicyViolationException(
                    "BYO model mode requires a customer-owned endpoint.");
            }

            if (request.Endpoint.NetworkBoundary.Contains("public", StringComparison.OrdinalIgnoreCase))
            {
                throw new PrivateModelGatewayPolicyViolationException(
                    "Public network boundary is not allowed for BYO model endpoint.");
            }
        }
    }

    private static string BuildSelfHostedExtractiveAnswer(
        string question,
        IReadOnlyList<PrivateModelGatewayEvidencePayload> evidence)
    {
        if (evidence.Count == 0)
        {
            return "I cannot answer from approved scoped evidence. No outbound call was made.";
        }

        var first = evidence[0];
        return $"Self-hosted answer for '{question.Trim()}'. No outbound call was made. Based only on scoped evidence {first.Handle}: {first.Text}";
    }
}
