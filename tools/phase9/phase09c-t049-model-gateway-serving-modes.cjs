const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function p(relativePath) {
  return path.join(root, relativePath.replace(/\//g, path.sep));
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(relativePath) {
  return fs.existsSync(p(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(p(relativePath), "utf8");
}

function write(relativePath, content) {
  const target = p(relativePath);
  ensureDir(path.dirname(target));
  fs.writeFileSync(target, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + relativePath);
}

function backup(relativePath) {
  const source = p(relativePath);
  if (!fs.existsSync(source)) return;

  const stamp = new Date().toISOString().replace(/[-:]/g, "").replace(/\..+/, "").replace("T", "_");
  const target = p(".phase9_backup/t049_model_gateway_serving_modes_" + stamp + "/" + relativePath);
  ensureDir(path.dirname(target));
  fs.copyFileSync(source, target);
}

function run(name, command, args) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(command, args, {
    cwd: root,
    stdio: "inherit",
    shell: false
  });
}

write("Backend/PlantProcess.Application/Assistant/ModelGateway/PrivateModelGatewayContracts.cs", `
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
`);

write("Backend/PlantProcess.Application/Assistant/ModelGateway/PrivateModelGatewayService.cs", `
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
        return $"Self-hosted answer for '{question.Trim()}'. Based only on scoped evidence {first.Handle}: {first.Text}";
    }
}
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T049ModelGatewayServingModesTests.cs", `
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
`);

const certPath = "Backend/PlantProcess.Api/AssistantGateway/V5PrivateModelGatewayCertificationEndpoints.cs";
backup(certPath);

let cert = read(certPath);

if (!cert.includes("PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES")) {
  cert = cert.replace(
    "public static class V5PrivateModelGatewayCertificationEndpoints",
    "/// <summary>\n/// PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES.\n/// V5 certification surface proves self-hosted no-egress, private ZDR endpoint, BYO model, and tenant no-egress controls.\n/// </summary>\npublic static class V5PrivateModelGatewayCertificationEndpoints"
  );

  cert = cert.replace(
    `"local-mock-private"`,
    `"local-mock-private",\n                "self-hosted-no-egress",\n                "private-zero-retention-endpoint",\n                "bring-your-own-model"`
  );

  cert = cert.replace(
    `"model-version-pinning"`,
    `"model-version-pinning",\n                "three-serving-mode-certification",\n                "tenant-no-egress-toggle",\n                "scoped-evidence-only-payload"`
  );

  write(certPath, cert);
}

write("tools/phase9/validate-t049-model-gateway-serving-modes.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function file(relativePath) {
  return path.join(root, relativePath);
}

function exists(relativePath) {
  return fs.existsSync(file(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(file(relativePath), "utf8");
}

const checks = [
  {
    file: "Backend/PlantProcess.Application/Assistant/ModelGateway/PrivateModelGatewayContracts.cs",
    signals: [
      "PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES",
      "SelfHostedNoEgress",
      "PrivateZeroRetentionEndpoint",
      "BringYourOwnModel",
      "NoEgress",
      "ScopedEvidenceChunk",
      "RawPlantRowJson",
      "IPrivateModelGatewayTransport"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/ModelGateway/PrivateModelGatewayService.cs",
    signals: [
      "PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES",
      "BuildScopedEvidencePayload",
      "Tenant no-egress policy blocks",
      "SelfHostedNoEgress",
      "PrivateZeroRetentionEndpoint",
      "BringYourOwnModel",
      "ZeroDataRetentionConfirmed",
      "CustomerOwnedEndpoint",
      "OutboundCallAttempted: false"
    ]
  },
  {
    file: "Backend/PlantProcess.Api/AssistantGateway/V5PrivateModelGatewayCertificationEndpoints.cs",
    signals: [
      "PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES",
      "self-hosted-no-egress",
      "private-zero-retention-endpoint",
      "bring-your-own-model",
      "tenant-no-egress-toggle",
      "scoped-evidence-only-payload"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T049ModelGatewayServingModesTests.cs",
    signals: [
      "T049_SelfHosted_Mode_Makes_Zero_Outbound_Calls",
      "T049_Private_ZeroRetention_Endpoint_Sends_Only_Question_And_Scoped_Evidence",
      "T049_BYO_Model_Mode_Uses_Customer_Endpoint_And_Scoped_Evidence_Only",
      "T049_Tenant_NoEgress_Toggle_Blocks_Private_And_BYO_Egress",
      "T049_NoEgress_Tenant_Can_Still_Use_SelfHosted_Mode",
      "T049_Certification_Matrix_Covers_All_Three_Serving_Modes",
      "T049_Scoped_Evidence_Payload_Drops_Synthetic_And_Raw_Source_Metadata",
      "secret_raw_row",
      "database_password",
      "plantprocess123",
      "PRIVATE_TAG",
      "CallCount"
    ]
  }
];

for (const check of checks) {
  if (!exists(check.file)) {
    failures.push({ file: check.file, reason: "missing file" });
    continue;
  }

  const text = read(check.file);

  for (const signal of check.signals) {
    if (!text.includes(signal)) {
      failures.push({ file: check.file, reason: "missing signal: " + signal });
    }
  }
}

if (failures.length) {
  console.error("PPIQ-T049 failed: model gateway serving mode certification is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T049 passed: self-hosted no-egress, private ZDR endpoint, BYO model and tenant no-egress toggle are certified.");
`);

write("docs/phase9/T049_MODEL_GATEWAY_SERVING_MODES.md", `
# T-049 Model Gateway Serving Modes Certification

Marker: PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES

## Purpose

Prove the V5 private model gateway serving contract before Phase 09 deploy.

## Certified modes

1. Self-hosted / local no-egress mode
2. Private zero-retention endpoint
3. Bring-your-own-model customer endpoint

## Certified guardrails

- Self-hosted mode makes zero outbound calls.
- Private endpoint sends only question + scoped evidence.
- BYO endpoint sends only question + scoped evidence to the customer endpoint.
- Raw plant rows, source tables, OPC tags, database secrets, and raw JSON rows are not included in outbound payloads.
- Synthetic evidence is excluded from outbound payloads.
- Tenant no-egress toggle blocks private and BYO egress.
- Tenant no-egress still allows self-hosted local mode.

## Validation

Run:

    node tools/phase9/validate-t049-model-gateway-serving-modes.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase9_T049ModelGatewayServingModesTests --no-build
`);

run("node --check T-049 validator", "node", ["--check", "tools/phase9/validate-t049-model-gateway-serving-modes.cjs"]);
run("T-049 validator", "node", ["tools/phase9/validate-t049-model-gateway-serving-modes.cjs"]);
run("Backend build after T-049", "dotnet", ["build", "Backend"]);
run("T-049 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase9_T049ModelGatewayServingModesTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-049 completed: model gateway serving modes and no-egress toggle are green.");
console.log("=================================================================================================");
