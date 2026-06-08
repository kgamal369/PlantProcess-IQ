namespace PlantProcess.Application.Assistant.ModelGateway;

/// <summary>
/// PPIQ_API_STARTUP_SERVICE_INFERENCE_FIX.
/// Safe local/default transport for the private model gateway.
/// It intentionally performs no real outbound network call.
/// Production/private endpoint transports can replace this registration.
/// </summary>
public sealed class NoopPrivateModelGatewayTransport : IPrivateModelGatewayTransport
{
    public Task<string> CompleteAsync(
        Uri endpointUri,
        PrivateModelGatewayPayload payload,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            "Private model gateway runtime transport is not configured for outbound egress in this environment. " +
            "Use SelfHostedNoEgress mode or configure an approved private/BYO transport.");
    }
}