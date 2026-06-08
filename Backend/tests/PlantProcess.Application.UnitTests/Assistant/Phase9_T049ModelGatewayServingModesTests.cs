
using System.Text.Json;
using PlantProcess.Application.Assistant.ModelGateway;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES.
/// Certifies self-hosted no-egress, private zero-retention endpoint,
/// BYO model endpoint, and tenant no-egress blocking.
/// </summary>
public sealed class Phase9_T049ModelGatewayServingModesTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly ScopedEvidenceChunk[] Evidence =
    {
        new(
            Handle: "finding:edge-crack-caster-a",
            Text: "Approved finding: edge crack risk is elevated for caster-a. Projected range is 28,000 to 56,000 EUR.",
            SourceTable: "raw_hsm_coil_rows",
            RawPlantRowJson: """{"coil_id":"C-0044170","heat_id":"H-3361","secret_raw_row":"must-not-egress"}"""),
        new(
            Handle: "finding:temperature-band",
            Text: "Approved finding: finishing temperature band drift is associated with defect rate.",
            SourceTable: "raw_process_measurements",
            RawPlantRowJson: """{"database_password":"plantprocess123","opc_tag":"PRIVATE_TAG"}""")
    };

    [Fact]
    public async Task T049_SelfHosted_Mode_Makes_Zero_Outbound_Calls()
    {
        var transport = new CapturingTransport();
        var service = new PrivateModelGatewayService(transport);

        var result = await service.AskAsync(
            new PrivateModelGatewayRequest(
                TenantId,
                "Explain the approved suggestion.",
                Evidence,
                SelfHostedEndpoint(),
                PrivateModelGatewayTenantPolicy.Default(TenantId)),
            CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.False(result.OutboundCallAttempted);
        Assert.Equal(0, transport.CallCount);
        Assert.Null(result.OutboundPayload);
        Assert.Contains("Self-hosted", result.Answer);
        Assert.Contains("finding:edge-crack-caster-a", result.Answer);
    }

    [Fact]
    public async Task T049_Private_ZeroRetention_Endpoint_Sends_Only_Question_And_Scoped_Evidence()
    {
        var transport = new CapturingTransport();
        var service = new PrivateModelGatewayService(transport);

        var result = await service.AskAsync(
            new PrivateModelGatewayRequest(
                TenantId,
                "What is the projected value range?",
                Evidence,
                PrivateEndpoint(),
                PrivateModelGatewayTenantPolicy.Default(TenantId)),
            CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.True(result.OutboundCallAttempted);
        Assert.Equal(1, transport.CallCount);
        Assert.NotNull(result.OutboundPayload);
        Assert.True(result.OutboundPayload!.ZeroDataRetention);
        Assert.Equal("What is the projected value range?", result.OutboundPayload.Question);
        Assert.Equal(2, result.OutboundPayload.ScopedEvidence.Count);

        var serialized = JsonSerializer.Serialize(result.OutboundPayload);

        Assert.Contains("finding:edge-crack-caster-a", serialized);
        Assert.Contains("Projected range is 28,000 to 56,000 EUR", serialized);

        Assert.DoesNotContain("raw_hsm_coil_rows", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw_process_measurements", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret_raw_row", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("database_password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("plantprocess123", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_TAG", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task T049_BYO_Model_Mode_Uses_Customer_Endpoint_And_Scoped_Evidence_Only()
    {
        var transport = new CapturingTransport();
        var service = new PrivateModelGatewayService(transport);

        var result = await service.AskAsync(
            new PrivateModelGatewayRequest(
                TenantId,
                "Summarize the approved association.",
                Evidence,
                ByoEndpoint(),
                PrivateModelGatewayTenantPolicy.Default(TenantId)),
            CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.True(result.OutboundCallAttempted);
        Assert.Single(transport.Calls);
        Assert.Equal(new Uri("https://customer-model.internal/v1/chat/completions"), transport.Calls[0].EndpointUri);
        Assert.Equal("BringYourOwnModel", transport.Calls[0].Payload.ServingMode);
        Assert.Equal("customer-byo-quality-model", transport.Calls[0].Payload.ModelName);

        var serialized = JsonSerializer.Serialize(transport.Calls[0].Payload);
        Assert.Contains("Summarize the approved association.", serialized);
        Assert.Contains("finding:temperature-band", serialized);
        Assert.DoesNotContain("opc_tag", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_TAG", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task T049_Tenant_NoEgress_Toggle_Blocks_Private_And_BYO_Egress()
    {
        var transport = new CapturingTransport();
        var service = new PrivateModelGatewayService(transport);
        var noEgressPolicy = new PrivateModelGatewayTenantPolicy(
            TenantId,
            NoEgress: true,
            AllowPrivateEndpoint: true,
            AllowBringYourOwnModel: true);

        var privateFailure = await Assert.ThrowsAsync<PrivateModelGatewayPolicyViolationException>(() =>
            service.AskAsync(
                new PrivateModelGatewayRequest(
                    TenantId,
                    "Try private endpoint.",
                    Evidence,
                    PrivateEndpoint(),
                    noEgressPolicy),
                CancellationToken.None));

        var byoFailure = await Assert.ThrowsAsync<PrivateModelGatewayPolicyViolationException>(() =>
            service.AskAsync(
                new PrivateModelGatewayRequest(
                    TenantId,
                    "Try BYO endpoint.",
                    Evidence,
                    ByoEndpoint(),
                    noEgressPolicy),
                CancellationToken.None));

        Assert.Contains("no-egress", privateFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-egress", byoFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task T049_NoEgress_Tenant_Can_Still_Use_SelfHosted_Mode()
    {
        var transport = new CapturingTransport();
        var service = new PrivateModelGatewayService(transport);
        var noEgressPolicy = new PrivateModelGatewayTenantPolicy(
            TenantId,
            NoEgress: true,
            AllowPrivateEndpoint: false,
            AllowBringYourOwnModel: false);

        var result = await service.AskAsync(
            new PrivateModelGatewayRequest(
                TenantId,
                "Explain locally.",
                Evidence,
                SelfHostedEndpoint(),
                noEgressPolicy),
            CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.False(result.OutboundCallAttempted);
        Assert.Equal(0, transport.CallCount);
        Assert.Contains("No outbound call was made", result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T049_Certification_Matrix_Covers_All_Three_Serving_Modes()
    {
        var modes = Enum.GetValues<PrivateModelServingMode>();

        Assert.Contains(PrivateModelServingMode.SelfHostedNoEgress, modes);
        Assert.Contains(PrivateModelServingMode.PrivateZeroRetentionEndpoint, modes);
        Assert.Contains(PrivateModelServingMode.BringYourOwnModel, modes);
        Assert.Equal(3, modes.Length);
    }

    [Fact]
    public void T049_Scoped_Evidence_Payload_Drops_Synthetic_And_Raw_Source_Metadata()
    {
        var scoped = PrivateModelGatewayService.BuildScopedEvidencePayload(new[]
        {
            Evidence[0],
            new ScopedEvidenceChunk(
                Handle: "synthetic-seed",
                Text: "Synthetic demo seed should not be sent.",
                SourceTable: "demo_seed",
                RawPlantRowJson: """{"fake":"true"}""",
                IsSynthetic: true)
        });

        Assert.Single(scoped);
        Assert.Equal("finding:edge-crack-caster-a", scoped[0].Handle);

        var serialized = JsonSerializer.Serialize(scoped);
        Assert.DoesNotContain("Synthetic demo seed", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("demo_seed", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fake", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static PrivateModelGatewayEndpoint SelfHostedEndpoint()
        => new(
            EndpointCode: "self-hosted-no-egress",
            ServingMode: PrivateModelServingMode.SelfHostedNoEgress,
            ProviderType: "self-hosted-local",
            ModelName: "ppiq-local-extractive",
            ModelVersion: "phase09-t049",
            EndpointUri: null,
            ZeroDataRetentionConfirmed: true,
            CustomerOwnedEndpoint: true,
            NetworkBoundary: "local-process");

    private static PrivateModelGatewayEndpoint PrivateEndpoint()
        => new(
            EndpointCode: "private-zdr-endpoint",
            ServingMode: PrivateModelServingMode.PrivateZeroRetentionEndpoint,
            ProviderType: "azure-openai-private",
            ModelName: "private-quality-model",
            ModelVersion: "2026-06-private",
            EndpointUri: new Uri("https://private-openai.customer.local/v1/chat/completions"),
            ZeroDataRetentionConfirmed: true,
            CustomerOwnedEndpoint: false,
            NetworkBoundary: "private-link");

    private static PrivateModelGatewayEndpoint ByoEndpoint()
        => new(
            EndpointCode: "customer-byo",
            ServingMode: PrivateModelServingMode.BringYourOwnModel,
            ProviderType: "customer-byom",
            ModelName: "customer-byo-quality-model",
            ModelVersion: "customer-v1",
            EndpointUri: new Uri("https://customer-model.internal/v1/chat/completions"),
            ZeroDataRetentionConfirmed: true,
            CustomerOwnedEndpoint: true,
            NetworkBoundary: "customer-network");

    private sealed class CapturingTransport : IPrivateModelGatewayTransport
    {
        public List<CapturedCall> Calls { get; } = new();

        public int CallCount => Calls.Count;

        public Task<string> CompleteAsync(
            Uri endpointUri,
            PrivateModelGatewayPayload payload,
            CancellationToken cancellationToken)
        {
            Calls.Add(new CapturedCall(endpointUri, payload));
            return Task.FromResult("Private model answer based only on scoped evidence. [citation: finding:edge-crack-caster-a]");
        }
    }

    private sealed record CapturedCall(
        Uri EndpointUri,
        PrivateModelGatewayPayload Payload);
}
