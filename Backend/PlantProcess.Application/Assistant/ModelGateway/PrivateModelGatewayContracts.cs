
using System.Text.Json.Serialization;

namespace PlantProcess.Application.Assistant.ModelGateway;

/// <summary>
/// PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES.
/// Canonical serving modes for the V5 private model gateway certification.
/// </summary>
public enum PrivateModelServingMode
{
    SelfHostedNoEgress = 1,
    PrivateZeroRetentionEndpoint = 2,
    BringYourOwnModel = 3
}

public sealed record PrivateModelGatewayTenantPolicy(
    Guid TenantId,
    bool NoEgress,
    bool AllowPrivateEndpoint,
    bool AllowBringYourOwnModel)
{
    public static PrivateModelGatewayTenantPolicy Default(Guid tenantId)
        => new(
            TenantId: tenantId,
            NoEgress: false,
            AllowPrivateEndpoint: true,
            AllowBringYourOwnModel: true);
}

public sealed record PrivateModelGatewayEndpoint(
    string EndpointCode,
    PrivateModelServingMode ServingMode,
    string ProviderType,
    string ModelName,
    string ModelVersion,
    Uri? EndpointUri,
    bool ZeroDataRetentionConfirmed,
    bool CustomerOwnedEndpoint,
    string NetworkBoundary);

public sealed record ScopedEvidenceChunk(
    string Handle,
    string Text,
    string? SourceTable = null,
    string? RawPlantRowJson = null,
    bool IsSynthetic = false);

public sealed record PrivateModelGatewayRequest(
    Guid TenantId,
    string Question,
    IReadOnlyList<ScopedEvidenceChunk> Evidence,
    PrivateModelGatewayEndpoint Endpoint,
    PrivateModelGatewayTenantPolicy TenantPolicy);

public sealed record PrivateModelGatewayPayload(
    string Question,
    IReadOnlyList<PrivateModelGatewayEvidencePayload> ScopedEvidence,
    string ModelName,
    string ModelVersion,
    bool ZeroDataRetention,
    string ServingMode);

public sealed record PrivateModelGatewayEvidencePayload(
    string Handle,
    string Text);

public sealed record PrivateModelGatewayResult(
    bool Allowed,
    bool OutboundCallAttempted,
    string ServingMode,
    string ProviderType,
    string ModelName,
    string ModelVersion,
    string Answer,
    string RefusalReason,
    PrivateModelGatewayPayload? OutboundPayload)
{
    public bool IsRefusal => !Allowed;
}

public interface IPrivateModelGatewayTransport
{
    Task<string> CompleteAsync(Uri endpointUri, PrivateModelGatewayPayload payload, CancellationToken cancellationToken);
}

public sealed class PrivateModelGatewayPolicyViolationException : InvalidOperationException
{
    public PrivateModelGatewayPolicyViolationException(string message) : base(message)
    {
    }
}
