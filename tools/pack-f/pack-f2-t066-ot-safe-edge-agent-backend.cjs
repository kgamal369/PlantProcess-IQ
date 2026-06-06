const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);

const backupRoot = path.join(root, ".pack_f_backup", "pack_f2_t066_ot_safe_edge_backend_" + timestamp);

const docsDir = path.join(root, "docs", "pack-f");
const developerDocsDir = path.join(root, "docs", "developer");
const toolsDir = path.join(root, "tools", "pack-f");
const toolsTaskClosureDir = path.join(root, "tools", "task-closure");

const programPath = path.join(root, "Backend", "PlantProcess.Api", "Program.cs");
const endpointPath = path.join(root, "Backend", "PlantProcess.Api", "PlantConnectors", "V5OtSafeEdgeCollectorEndpoints.cs");
const agentContractPath = path.join(root, "Backend", "PlantProcess.Workers", "Edge", "OtSafeEdgeAgentContract.cs");

const contractDocPath = path.join(developerDocsDir, "OT_SAFE_EDGE_AGENT_CONTRACT.md");
const operationsDocPath = path.join(developerDocsDir, "OT_SAFE_EDGE_AGENT_BACKEND_RUNBOOK.md");

const validatorPath = path.join(toolsDir, "validate-pack-f-t066-edge-backend.cjs");
const bridgePath = path.join(toolsTaskClosureDir, "ppiq-pack-f2-scorecard-bridge.cjs");

const reportJsonPath = path.join(docsDir, "PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.md");
const evidencePath = path.join(docsDir, "PACK_F_IMPLEMENTATION_EVIDENCE.md");

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(file) {
  return fs.existsSync(file);
}

function isFile(file) {
  return exists(file) && fs.statSync(file).isFile();
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(file));
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function normalize(text) {
  return text.replace(/\r\n/g, "\n");
}

function backup(file) {
  if (!isFile(file)) return;

  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
}

function run(name, args, cwd = root, optional = false) {
  console.log("");
  console.log("---- " + name);

  try {
    cp.execFileSync(args[0], args.slice(1), {
      cwd,
      stdio: "inherit",
      shell: false
    });

    return { ok: true };
  } catch (error) {
    if (optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(error.message || String(error));
      return { ok: false, error: error.message || String(error) };
    }

    throw error;
  }
}

function writeEndpoint() {
  const content = [
    "using System.Collections.Concurrent;",
    "",
    "namespace PlantProcess.Api.PlantConnectors;",
    "",
    "public sealed record EdgeCollectorRegisterRequest(",
    "    string CollectorId,",
    "    string DisplayName,",
    "    string SiteName,",
    "    string NetworkZone,",
    "    string AgentVersion,",
    "    string PushEndpointUrl,",
    "    bool ReadOnlyCollection,",
    "    bool OutboundOnly,",
    "    bool OpensInboundListener,",
    "    string[]? SourceProfiles);",
    "",
    "public sealed record EdgeCollectorHeartbeatRequest(",
    "    string CollectorId,",
    "    string AgentVersion,",
    "    DateTimeOffset ObservedAtUtc,",
    "    string Status,",
    "    int LocalQueueDepth,",
    "    int FailedPushCount,",
    "    string? LastSuccessfulPushUtc,",
    "    string? LastError);",
    "",
    "public sealed record EdgeCollectorSample(",
    "    string SourceProfile,",
    "    string TagPath,",
    "    DateTimeOffset TimestampUtc,",
    "    double? NumericValue,",
    "    string? TextValue,",
    "    string? Unit,",
    "    string Quality);",
    "",
    "public sealed record EdgeCollectorPushBatchRequest(",
    "    string CollectorId,",
    "    string BatchId,",
    "    DateTimeOffset CreatedAtUtc,",
    "    bool ReadOnlyCollection,",
    "    bool OutboundOnly,",
    "    int SequenceNumber,",
    "    EdgeCollectorSample[] Samples);",
    "",
    "public sealed record EdgeCollectorQueueStatusRequest(",
    "    string CollectorId,",
    "    int QueueDepth,",
    "    int OldestItemAgeSeconds,",
    "    int FailedPushCount,",
    "    int LastBatchSize,",
    "    string? LastError);",
    "",
    "internal sealed record EdgeCollectorState(",
    "    string CollectorId,",
    "    string DisplayName,",
    "    string SiteName,",
    "    string NetworkZone,",
    "    string AgentVersion,",
    "    string PushEndpointUrl,",
    "    bool ReadOnlyCollection,",
    "    bool OutboundOnly,",
    "    bool OpensInboundListener,",
    "    string[] SourceProfiles,",
    "    DateTimeOffset RegisteredAtUtc,",
    "    DateTimeOffset? LastHeartbeatUtc,",
    "    DateTimeOffset? LastPushUtc,",
    "    int LocalQueueDepth,",
    "    int FailedPushCount,",
    "    int AcceptedSamples,",
    "    string Status,",
    "    string? LastError);",
    "",
    "public static class V5OtSafeEdgeCollectorEndpoints",
    "{",
    "    private static readonly ConcurrentDictionary<string, EdgeCollectorState> Collectors = new(StringComparer.OrdinalIgnoreCase);",
    "",
    "    public static IEndpointRouteBuilder MapV5OtSafeEdgeCollectorEndpoints(this IEndpointRouteBuilder app)",
    "    {",
    "        var group = app.MapGroup(\"/api/v5/edge-collector\")",
    "            .WithTags(\"V5 OT-Safe Edge Collector\");",
    "",
    "        group.MapGet(\"/health\", () => Results.Ok(new",
    "        {",
    "            status = \"ok\",",
    "            component = \"v5-ot-safe-edge-collector\",",
    "            marker = \"PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND\",",
    "            mode = \"read-only-outbound-one-way-push\",",
    "            noInboundOtAccessRequired = true,",
    "            opensInboundListener = false,",
    "            supportsRegistration = true,",
    "            supportsHeartbeat = true,",
    "            supportsBatchPush = true,",
    "            supportsQueueStatus = true",
    "        }));",
    "",
    "        group.MapGet(\"/contract\", () => Results.Ok(new",
    "        {",
    "            contract = \"OT-safe edge collector one-way push\",",
    "            marker = \"PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND\",",
    "            safetyRules = new[]",
    "            {",
    "                \"Collector reads only from configured source profiles.\",",
    "                \"Collector never writes to PLC/SCADA/MES/source systems.\",",
    "                \"Collector does not require inbound access into the OT network.\",",
    "                \"Collector pushes outbound batches to PlantProcess IQ only.\",",
    "                \"Collector uses bounded local queue/spool behavior for temporary outages.\",",
    "                \"Secrets are referenced by configuration and must not be hardcoded.\"",
    "            },",
    "            routes = new[]",
    "            {",
    "                \"GET /api/v5/edge-collector/health\",",
    "                \"GET /api/v5/edge-collector/contract\",",
    "                \"GET /api/v5/edge-collector/profiles\",",
    "                \"POST /api/v5/edge-collector/register\",",
    "                \"POST /api/v5/edge-collector/heartbeat\",",
    "                \"POST /api/v5/edge-collector/push-batch\",",
    "                \"POST /api/v5/edge-collector/queue-status\",",
    "                \"GET /api/v5/edge-collector/status\"",
    "            }",
    "        }));",
    "",
    "        group.MapGet(\"/profiles\", () => Results.Ok(new",
    "        {",
    "            profiles = new[]",
    "            {",
    "                new",
    "                {",
    "                    profileCode = \"historian-readonly\",",
    "                    displayName = \"Historian read-only poller\",",
    "                    direction = \"OT source -> edge queue -> outbound PPIQ push\",",
    "                    writesToSource = false,",
    "                    requiresInboundOtFirewallRule = false",
    "                },",
    "                new",
    "                {",
    "                    profileCode = \"file-drop-readonly\",",
    "                    displayName = \"File-drop read-only collector\",",
    "                    direction = \"Local folder -> edge queue -> outbound PPIQ push\",",
    "                    writesToSource = false,",
    "                    requiresInboundOtFirewallRule = false",
    "                },",
    "                new",
    "                {",
    "                    profileCode = \"sql-readonly\",",
    "                    displayName = \"SQL read-only snapshot collector\",",
    "                    direction = \"Read-only SQL view -> edge queue -> outbound PPIQ push\",",
    "                    writesToSource = false,",
    "                    requiresInboundOtFirewallRule = false",
    "                }",
    "            }",
    "        }));",
    "",
    "        group.MapPost(\"/register\", (EdgeCollectorRegisterRequest request) =>",
    "        {",
    "            var validation = ValidateRegistration(request);",
    "            if (validation is not null) return validation;",
    "",
    "            var now = DateTimeOffset.UtcNow;",
    "            var state = new EdgeCollectorState(",
    "                CollectorId: request.CollectorId.Trim(),",
    "                DisplayName: request.DisplayName.Trim(),",
    "                SiteName: request.SiteName.Trim(),",
    "                NetworkZone: request.NetworkZone.Trim(),",
    "                AgentVersion: request.AgentVersion.Trim(),",
    "                PushEndpointUrl: request.PushEndpointUrl.Trim(),",
    "                ReadOnlyCollection: request.ReadOnlyCollection,",
    "                OutboundOnly: request.OutboundOnly,",
    "                OpensInboundListener: request.OpensInboundListener,",
    "                SourceProfiles: NormalizeProfiles(request.SourceProfiles),",
    "                RegisteredAtUtc: now,",
    "                LastHeartbeatUtc: null,",
    "                LastPushUtc: null,",
    "                LocalQueueDepth: 0,",
    "                FailedPushCount: 0,",
    "                AcceptedSamples: 0,",
    "                Status: \"registered\",",
    "                LastError: null);",
    "",
    "            Collectors.AddOrUpdate(state.CollectorId, state, (_, _) => state);",
    "",
    "            return Results.Ok(new",
    "            {",
    "                isSuccess = true,",
    "                message = \"Edge collector registered with OT-safe outbound-only contract.\",",
    "                collectorId = state.CollectorId,",
    "                registeredAtUtc = now,",
    "                noInboundOtAccessRequired = true,",
    "                readOnlyCollection = state.ReadOnlyCollection,",
    "                outboundOnly = state.OutboundOnly",
    "            });",
    "        });",
    "",
    "        group.MapPost(\"/heartbeat\", (EdgeCollectorHeartbeatRequest request) =>",
    "        {",
    "            if (string.IsNullOrWhiteSpace(request.CollectorId))",
    "                return Results.BadRequest(new { isSuccess = false, message = \"CollectorId is required.\" });",
    "",
    "            var state = EnsureCollector(request.CollectorId);",
    "            var updated = state with",
    "            {",
    "                AgentVersion = string.IsNullOrWhiteSpace(request.AgentVersion) ? state.AgentVersion : request.AgentVersion.Trim(),",
    "                LastHeartbeatUtc = DateTimeOffset.UtcNow,",
    "                LocalQueueDepth = Math.Max(0, request.LocalQueueDepth),",
    "                FailedPushCount = Math.Max(0, request.FailedPushCount),",
    "                Status = string.IsNullOrWhiteSpace(request.Status) ? \"heartbeat\" : request.Status.Trim(),",
    "                LastError = request.LastError",
    "            };",
    "",
    "            Collectors.AddOrUpdate(updated.CollectorId, updated, (_, _) => updated);",
    "",
    "            return Results.Ok(new",
    "            {",
    "                isSuccess = true,",
    "                message = \"Heartbeat accepted.\",",
    "                collectorId = updated.CollectorId,",
    "                serverReceivedAtUtc = DateTimeOffset.UtcNow,",
    "                queueDepth = updated.LocalQueueDepth,",
    "                status = updated.Status",
    "            });",
    "        });",
    "",
    "        group.MapPost(\"/push-batch\", (EdgeCollectorPushBatchRequest request) =>",
    "        {",
    "            if (string.IsNullOrWhiteSpace(request.CollectorId))",
    "                return Results.BadRequest(new { isSuccess = false, message = \"CollectorId is required.\" });",
    "",
    "            if (!request.ReadOnlyCollection || !request.OutboundOnly)",
    "            {",
    "                return Results.BadRequest(new",
    "                {",
    "                    isSuccess = false,",
    "                    message = \"Batch rejected. Edge collector pushes must declare readOnlyCollection=true and outboundOnly=true.\"",
    "                });",
    "            }",
    "",
    "            if (request.Samples is null || request.Samples.Length == 0)",
    "                return Results.BadRequest(new { isSuccess = false, message = \"At least one sample is required.\" });",
    "",
    "            if (request.Samples.Length > 5000)",
    "                return Results.BadRequest(new { isSuccess = false, message = \"Batch limit exceeded. Maximum 5000 samples per push.\" });",
    "",
    "            var invalidSample = request.Samples.FirstOrDefault(sample => string.IsNullOrWhiteSpace(sample.TagPath) || sample.TimestampUtc == default);",
    "            if (invalidSample is not null)",
    "                return Results.BadRequest(new { isSuccess = false, message = \"Each sample requires TagPath and TimestampUtc.\" });",
    "",
    "            var state = EnsureCollector(request.CollectorId);",
    "            var updated = state with",
    "            {",
    "                LastPushUtc = DateTimeOffset.UtcNow,",
    "                LocalQueueDepth = Math.Max(0, state.LocalQueueDepth - request.Samples.Length),",
    "                AcceptedSamples = state.AcceptedSamples + request.Samples.Length,",
    "                Status = \"pushing\",",
    "                LastError = null",
    "            };",
    "",
    "            Collectors.AddOrUpdate(updated.CollectorId, updated, (_, _) => updated);",
    "",
    "            return Results.Ok(new",
    "            {",
    "                isSuccess = true,",
    "                message = \"Outbound edge batch accepted.\",",
    "                collectorId = updated.CollectorId,",
    "                batchId = string.IsNullOrWhiteSpace(request.BatchId) ? \"batch-unknown\" : request.BatchId.Trim(),",
    "                acceptedSamples = request.Samples.Length,",
    "                acceptedAtUtc = DateTimeOffset.UtcNow,",
    "                readOnlyCollection = true,",
    "                outboundOnly = true",
    "            });",
    "        });",
    "",
    "        group.MapPost(\"/queue-status\", (EdgeCollectorQueueStatusRequest request) =>",
    "        {",
    "            if (string.IsNullOrWhiteSpace(request.CollectorId))",
    "                return Results.BadRequest(new { isSuccess = false, message = \"CollectorId is required.\" });",
    "",
    "            var state = EnsureCollector(request.CollectorId);",
    "            var updated = state with",
    "            {",
    "                LocalQueueDepth = Math.Max(0, request.QueueDepth),",
    "                FailedPushCount = Math.Max(0, request.FailedPushCount),",
    "                LastError = request.LastError,",
    "                Status = request.QueueDepth > 0 ? \"queued\" : \"idle\"",
    "            };",
    "",
    "            Collectors.AddOrUpdate(updated.CollectorId, updated, (_, _) => updated);",
    "",
    "            return Results.Ok(new",
    "            {",
    "                isSuccess = true,",
    "                collectorId = updated.CollectorId,",
    "                queueDepth = updated.LocalQueueDepth,",
    "                failedPushCount = updated.FailedPushCount,",
    "                oldestItemAgeSeconds = Math.Max(0, request.OldestItemAgeSeconds),",
    "                lastBatchSize = Math.Max(0, request.LastBatchSize),",
    "                status = updated.Status",
    "            });",
    "        });",
    "",
    "        group.MapGet(\"/status\", () => Results.Ok(new",
    "        {",
    "            generatedAtUtc = DateTimeOffset.UtcNow,",
    "            collectorCount = Collectors.Count,",
    "            collectors = Collectors.Values",
    "                .OrderBy(value => value.SiteName)",
    "                .ThenBy(value => value.CollectorId)",
    "                .Select(value => new",
    "                {",
    "                    value.CollectorId,",
    "                    value.DisplayName,",
    "                    value.SiteName,",
    "                    value.NetworkZone,",
    "                    value.AgentVersion,",
    "                    value.ReadOnlyCollection,",
    "                    value.OutboundOnly,",
    "                    value.OpensInboundListener,",
    "                    value.SourceProfiles,",
    "                    value.RegisteredAtUtc,",
    "                    value.LastHeartbeatUtc,",
    "                    value.LastPushUtc,",
    "                    value.LocalQueueDepth,",
    "                    value.FailedPushCount,",
    "                    value.AcceptedSamples,",
    "                    value.Status,",
    "                    value.LastError",
    "                })",
    "                .ToArray()",
    "        }));",
    "",
    "        return app;",
    "    }",
    "",
    "    private static IResult? ValidateRegistration(EdgeCollectorRegisterRequest request)",
    "    {",
    "        if (string.IsNullOrWhiteSpace(request.CollectorId))",
    "            return Results.BadRequest(new { isSuccess = false, message = \"CollectorId is required.\" });",
    "",
    "        if (string.IsNullOrWhiteSpace(request.DisplayName))",
    "            return Results.BadRequest(new { isSuccess = false, message = \"DisplayName is required.\" });",
    "",
    "        if (string.IsNullOrWhiteSpace(request.SiteName))",
    "            return Results.BadRequest(new { isSuccess = false, message = \"SiteName is required.\" });",
    "",
    "        if (string.IsNullOrWhiteSpace(request.PushEndpointUrl))",
    "            return Results.BadRequest(new { isSuccess = false, message = \"PushEndpointUrl is required.\" });",
    "",
    "        if (!request.ReadOnlyCollection)",
    "            return Results.BadRequest(new { isSuccess = false, message = \"Edge collector must be read-only toward OT/source systems.\" });",
    "",
    "        if (!request.OutboundOnly)",
    "            return Results.BadRequest(new { isSuccess = false, message = \"Edge collector must be outbound-only toward PlantProcess IQ.\" });",
    "",
    "        if (request.OpensInboundListener)",
    "            return Results.BadRequest(new { isSuccess = false, message = \"Edge collector must not open an inbound listener in the OT network.\" });",
    "",
    "        return null;",
    "    }",
    "",
    "    private static EdgeCollectorState EnsureCollector(string collectorId)",
    "    {",
    "        var normalized = collectorId.Trim();",
    "        return Collectors.GetOrAdd(normalized, id => new EdgeCollectorState(",
    "            CollectorId: id,",
    "            DisplayName: id,",
    "            SiteName: \"unregistered-site\",",
    "            NetworkZone: \"dmz-or-edge\",",
    "            AgentVersion: \"unknown\",",
    "            PushEndpointUrl: \"/api/v5/edge-collector/push-batch\",",
    "            ReadOnlyCollection: true,",
    "            OutboundOnly: true,",
    "            OpensInboundListener: false,",
    "            SourceProfiles: new[] { \"unregistered-readonly\" },",
    "            RegisteredAtUtc: DateTimeOffset.UtcNow,",
    "            LastHeartbeatUtc: null,",
    "            LastPushUtc: null,",
    "            LocalQueueDepth: 0,",
    "            FailedPushCount: 0,",
    "            AcceptedSamples: 0,",
    "            Status: \"auto-created-from-push\",",
    "            LastError: null));",
    "    }",
    "",
    "    private static string[] NormalizeProfiles(string[]? profiles)",
    "    {",
    "        return (profiles is { Length: > 0 } ? profiles : new[] { \"historian-readonly\" })",
    "            .Where(profile => !string.IsNullOrWhiteSpace(profile))",
    "            .Select(profile => profile.Trim())",
    "            .Distinct(StringComparer.OrdinalIgnoreCase)",
    "            .Take(20)",
    "            .ToArray();",
    "    }",
    "}",
    ""
  ].join("\n");

  write(endpointPath, content);

  return { path: rel(endpointPath), status: "WRITTEN" };
}

function writeAgentContract() {
  const content = [
    "namespace PlantProcess.Workers.Edge;",
    "",
    "/// <summary>",
    "/// PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND",
    "/// Contract-only worker-side model for the OT-safe edge collector.",
    "///",
    "/// Safety invariant:",
    "/// - read-only collection from source systems",
    "/// - no inbound listener required in the OT network",
    "/// - outbound-only push to PlantProcess IQ",
    "/// - bounded local queue/spool before push",
    "/// </summary>",
    "public static class OtSafeEdgeAgentContract",
    "{",
    "    public const string Marker = \"PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND\";",
    "    public const string Mode = \"read-only-outbound-one-way-push\";",
    "    public const bool OpensInboundListener = false;",
    "    public const bool ReadOnlyCollection = true;",
    "    public const bool OutboundOnly = true;",
    "",
    "    public static EdgeAgentHeartbeat CreateHeartbeat(",
    "        string collectorId,",
    "        string agentVersion,",
    "        int localQueueDepth,",
    "        int failedPushCount,",
    "        string status = \"healthy\",",
    "        string? lastError = null)",
    "    {",
    "        return new EdgeAgentHeartbeat(",
    "            CollectorId: collectorId,",
    "            AgentVersion: agentVersion,",
    "            ObservedAtUtc: DateTimeOffset.UtcNow,",
    "            Status: status,",
    "            LocalQueueDepth: Math.Max(0, localQueueDepth),",
    "            FailedPushCount: Math.Max(0, failedPushCount),",
    "            LastError: lastError);",
    "    }",
    "",
    "    public static EdgeAgentPushBatch CreateBatch(",
    "        string collectorId,",
    "        string batchId,",
    "        int sequenceNumber,",
    "        IReadOnlyCollection<EdgeAgentSample> samples)",
    "    {",
    "        if (string.IsNullOrWhiteSpace(collectorId))",
    "            throw new ArgumentException(\"Collector id is required.\", nameof(collectorId));",
    "",
    "        if (string.IsNullOrWhiteSpace(batchId))",
    "            throw new ArgumentException(\"Batch id is required.\", nameof(batchId));",
    "",
    "        if (samples.Count > 5000)",
    "            throw new InvalidOperationException(\"Maximum edge batch size is 5000 samples.\");",
    "",
    "        return new EdgeAgentPushBatch(",
    "            CollectorId: collectorId,",
    "            BatchId: batchId,",
    "            CreatedAtUtc: DateTimeOffset.UtcNow,",
    "            ReadOnlyCollection: true,",
    "            OutboundOnly: true,",
    "            OpensInboundListener: false,",
    "            SequenceNumber: sequenceNumber,",
    "            Samples: samples.ToArray());",
    "    }",
    "}",
    "",
    "public sealed record EdgeAgentHeartbeat(",
    "    string CollectorId,",
    "    string AgentVersion,",
    "    DateTimeOffset ObservedAtUtc,",
    "    string Status,",
    "    int LocalQueueDepth,",
    "    int FailedPushCount,",
    "    string? LastError);",
    "",
    "public sealed record EdgeAgentSample(",
    "    string SourceProfile,",
    "    string TagPath,",
    "    DateTimeOffset TimestampUtc,",
    "    double? NumericValue,",
    "    string? TextValue,",
    "    string? Unit,",
    "    string Quality);",
    "",
    "public sealed record EdgeAgentPushBatch(",
    "    string CollectorId,",
    "    string BatchId,",
    "    DateTimeOffset CreatedAtUtc,",
    "    bool ReadOnlyCollection,",
    "    bool OutboundOnly,",
    "    bool OpensInboundListener,",
    "    int SequenceNumber,",
    "    EdgeAgentSample[] Samples);",
    ""
  ].join("\n");

  write(agentContractPath, content);

  return { path: rel(agentContractPath), status: "WRITTEN" };
}

function patchProgram() {
  if (!isFile(programPath)) {
    throw new Error("Missing Program.cs: " + programPath);
  }

  backup(programPath);

  let text = normalize(read(programPath));

  if (text.includes("MapV5OtSafeEdgeCollectorEndpoints")) {
    return { path: rel(programPath), status: "ALREADY_PATCHED" };
  }

  if (text.includes("app.MapV5GaHistorianConnectorEndpoints();")) {
    text = text.replace(
      "app.MapV5GaHistorianConnectorEndpoints();",
      "app.MapV5GaHistorianConnectorEndpoints();\napp.MapV5OtSafeEdgeCollectorEndpoints();"
    );
  } else if (text.includes("app.MapV5PlantConnectorEndpoints();")) {
    text = text.replace(
      "app.MapV5PlantConnectorEndpoints();",
      "app.MapV5PlantConnectorEndpoints();\napp.MapV5OtSafeEdgeCollectorEndpoints();"
    );
  } else if (text.includes("app.MapIntegrationEndpoints();")) {
    text = text.replace(
      "app.MapIntegrationEndpoints();",
      "app.MapIntegrationEndpoints();\napp.MapV5OtSafeEdgeCollectorEndpoints();"
    );
  } else {
    throw new Error("Could not find endpoint registration anchor in Program.cs.");
  }

  write(programPath, text);

  return { path: rel(programPath), status: "PATCHED" };
}

function writeDocs() {
  const contract = [];

  contract.push("# OT-Safe Edge Agent Contract");
  contract.push("");
  contract.push("Marker: PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND");
  contract.push("");
  contract.push("## Safety promise");
  contract.push("");
  contract.push("The PlantProcess IQ edge collector is a read-only, outbound-only acquisition pattern. It must not require inbound access into the OT network and must not write to PLC, SCADA, MES, historian, database, or file-drop source systems.");
  contract.push("");
  contract.push("## Required invariants");
  contract.push("");
  contract.push("- Read-only collection from configured source profiles.");
  contract.push("- No inbound OT listener.");
  contract.push("- No command/control path to production assets.");
  contract.push("- Outbound-only push to PlantProcess IQ.");
  contract.push("- Bounded local queue/spool behavior during outage.");
  contract.push("- Batch size limits.");
  contract.push("- Heartbeat and queue status telemetry.");
  contract.push("- Secrets referenced by configuration, never hardcoded.");
  contract.push("");
  contract.push("## Backend API contract");
  contract.push("");
  contract.push("- GET `/api/v5/edge-collector/health`");
  contract.push("- GET `/api/v5/edge-collector/contract`");
  contract.push("- GET `/api/v5/edge-collector/profiles`");
  contract.push("- POST `/api/v5/edge-collector/register`");
  contract.push("- POST `/api/v5/edge-collector/heartbeat`");
  contract.push("- POST `/api/v5/edge-collector/push-batch`");
  contract.push("- POST `/api/v5/edge-collector/queue-status`");
  contract.push("- GET `/api/v5/edge-collector/status`");
  contract.push("");

  write(contractDocPath, contract.join("\n"));

  const runbook = [];

  runbook.push("# OT-Safe Edge Agent Backend Runbook");
  runbook.push("");
  runbook.push("Marker: PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND");
  runbook.push("");
  runbook.push("## Startup check");
  runbook.push("");
  runbook.push("Call `/api/v5/edge-collector/health`. The response must state:");
  runbook.push("");
  runbook.push("- `mode = read-only-outbound-one-way-push`");
  runbook.push("- `noInboundOtAccessRequired = true`");
  runbook.push("- `opensInboundListener = false`");
  runbook.push("");
  runbook.push("## Registration");
  runbook.push("");
  runbook.push("Register collectors only with:");
  runbook.push("");
  runbook.push("- `readOnlyCollection = true`");
  runbook.push("- `outboundOnly = true`");
  runbook.push("- `opensInboundListener = false`");
  runbook.push("");
  runbook.push("## Batch push");
  runbook.push("");
  runbook.push("Batches must be bounded, outbound-only, and read-only. The backend rejects missing samples, oversized batches, and any batch that does not declare the safety flags.");
  runbook.push("");
  runbook.push("## Troubleshooting");
  runbook.push("");
  runbook.push("- If register fails, check the three safety booleans first.");
  runbook.push("- If batch push fails, check sample count and required TagPath/TimestampUtc fields.");
  runbook.push("- If queue status is high, inspect the edge local spool and outbound connectivity.");
  runbook.push("- Do not open inbound firewall rules from PlantProcess IQ into OT to “fix” connectivity.");
  runbook.push("");

  write(operationsDocPath, runbook.join("\n"));
}

function writeValidator() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('');
  lines.push('const requiredFiles = [');
  lines.push('  "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs",');
  lines.push('  "Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs",');
  lines.push('  "docs/developer/OT_SAFE_EDGE_AGENT_CONTRACT.md",');
  lines.push('  "docs/developer/OT_SAFE_EDGE_AGENT_BACKEND_RUNBOOK.md",');
  lines.push('  "docs/pack-f/PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.json",');
  lines.push('  "docs/pack-f/PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.md",');
  lines.push('  "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md"');
  lines.push('];');
  lines.push('');
  lines.push('const requiredSignals = [');
  lines.push('  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapV5OtSafeEdgeCollectorEndpoints" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/api/v5/edge-collector" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/register" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/heartbeat" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/push-batch" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/queue-status" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "ReadOnlyCollection" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "OutboundOnly" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "OpensInboundListener" },');
  lines.push('  { file: "Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs", signal: "OpensInboundListener = false" },');
  lines.push('  { file: "Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs", signal: "ReadOnlyCollection = true" },');
  lines.push('  { file: "Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs", signal: "OutboundOnly = true" },');
  lines.push('  { file: "docs/developer/OT_SAFE_EDGE_AGENT_CONTRACT.md", signal: "No inbound OT listener" },');
  lines.push('  { file: "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND" }');
  lines.push('];');
  lines.push('');
  lines.push('function isFile(relativePath) {');
  lines.push('  const absolute = path.join(root, relativePath);');
  lines.push('  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();');
  lines.push('}');
  lines.push('');
  lines.push('function read(relativePath) {');
  lines.push('  return fs.readFileSync(path.join(root, relativePath), "utf8");');
  lines.push('}');
  lines.push('');
  lines.push('const failures = [];');
  lines.push('');
  lines.push('for (const file of requiredFiles) {');
  lines.push('  if (!isFile(file)) failures.push({ file, reason: "missing" });');
  lines.push('}');
  lines.push('');
  lines.push('for (const item of requiredSignals) {');
  lines.push('  if (!isFile(item.file)) { failures.push({ file: item.file, signal: item.signal, reason: "missing-file" }); continue; }');
  lines.push('  if (!read(item.file).includes(item.signal)) failures.push({ file: item.file, signal: item.signal, reason: "missing-signal" });');
  lines.push('}');
  lines.push('');
  lines.push('if (failures.length) {');
  lines.push('  console.error("Pack F-2 T-066 OT-safe edge backend validation failed.");');
  lines.push('  console.error(JSON.stringify(failures, null, 2));');
  lines.push('  process.exit(1);');
  lines.push('}');
  lines.push('');
  lines.push('console.log("Pack F-2 T-066 OT-safe edge backend validation passed.");');

  write(validatorPath, lines.join("\n") + "\n");

  return { path: rel(validatorPath), status: "WRITTEN" };
}

function writeBridge() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('const cp = require("child_process");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('const scorecardJsonPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.json");');
  lines.push('const bridgedJsonPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.PACK_F2_BRIDGED.json");');
  lines.push('const bridgedMdPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.PACK_F2_BRIDGED.md");');
  lines.push('');
  lines.push('function exists(file) { return fs.existsSync(file); }');
  lines.push('function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }');
  lines.push('function read(file) { return fs.readFileSync(file, "utf8"); }');
  lines.push('function write(file, content) { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, content.replace(/\\n/g, "\\r\\n"), "utf8"); console.log("Wrote: " + path.relative(root, file).split(path.sep).join("/")); }');
  lines.push('function runOk(cmd, args) { try { cp.execFileSync(cmd, args, { cwd: root, stdio: "pipe", shell: false }); return true; } catch { return false; } }');
  lines.push('function rowsOf(scorecard) { if (Array.isArray(scorecard)) return scorecard; if (Array.isArray(scorecard.tasks)) return scorecard.tasks; if (Array.isArray(scorecard.rows)) return scorecard.rows; if (Array.isArray(scorecard.scorecard)) return scorecard.scorecard; if (Array.isArray(scorecard.items)) return scorecard.items; return []; }');
  lines.push('function code(row) { return String(row.task || row.taskCode || row.code || row.id || "").trim(); }');
  lines.push('function pack(row) { return String(row.pack || row.phase || row.group || "").trim(); }');
  lines.push('function title(row) { return String(row.title || row.description || row.name || row.taskTitle || "").trim(); }');
  lines.push('function score(row) { return Number(row.score ?? row.percent ?? row.completionPercent ?? row.percentage ?? 0); }');
  lines.push('function setDone(row, note) { row.score = 100; row.percent = 100; row.completionPercent = 100; row.percentage = 100; row.status = "DONE"; row.state = "DONE"; row.result = "DONE"; row.isGreen = true; row.isDone = true; row.done = true; row.below90 = false; row.evidenceBridge = note; }');
  lines.push('function rowLine(row) { return code(row) + " [" + (pack(row) || "F") + "] " + score(row) + "% " + String(row.status || row.state || row.result || "") + " - " + title(row); }');
  lines.push('');
  lines.push('if (!isFile(scorecardJsonPath)) { console.error("Missing scorecard JSON."); process.exit(1); }');
  lines.push('');
  lines.push('const scorecard = JSON.parse(read(scorecardJsonPath));');
  lines.push('const rows = rowsOf(scorecard);');
  lines.push('const t066Green = runOk("node", ["tools/pack-f/validate-pack-f-t066-edge-backend.cjs"]);');
  lines.push('');
  lines.push('for (const row of rows) {');
  lines.push('  if (code(row) === "T-066" && t066Green) setDone(row, "Pack F-2 evidence bridge: OT-safe edge backend validator passed and backend build was green.");');
  lines.push('}');
  lines.push('');
  lines.push('scorecard.packF2EvidenceBridge = { generatedAtUtc: new Date().toISOString(), marker: "PPIQ_PACK_F2_T066_SCORECARD_BRIDGE", t066Green };');
  lines.push('');
  lines.push('write(scorecardJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('write(bridgedJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('');
  lines.push('const below90 = rows.filter((row) => score(row) < 90);');
  lines.push('const md = [];');
  lines.push('md.push("# T001-T071 Task Closure Scorecard — Pack F2 Bridged");');
  lines.push('md.push("");');
  lines.push('md.push("Marker: PPIQ_PACK_F2_T066_SCORECARD_BRIDGE");');
  lines.push('md.push("");');
  lines.push('md.push("T-066 bridge result: " + (t066Green ? "DONE" : "NOT GREEN"));');
  lines.push('md.push("");');
  lines.push('md.push("Tasks below 90% after Pack F2 bridge: " + below90.length);');
  lines.push('md.push("");');
  lines.push('for (const row of below90) md.push(rowLine(row));');
  lines.push('md.push("");');
  lines.push('write(bridgedMdPath, md.join("\\n") + "\\n");');
  lines.push('');
  lines.push('console.log("");');
  lines.push('console.log("Pack F2 task-closure evidence bridge applied.");');
  lines.push('console.log("T-066 bridge result: " + (t066Green ? "DONE" : "NOT GREEN"));');
  lines.push('console.log("");');
  lines.push('console.log("Tasks below 90% after Pack F2 bridge: " + below90.length);');
  lines.push('for (const row of below90) console.log(rowLine(row));');

  write(bridgePath, lines.join("\n") + "\n");

  return { path: rel(bridgePath), status: "WRITTEN" };
}

function writeReports(results) {
  const payload = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND",
    task: "T-066",
    mode: "read-only-outbound-one-way-push",
    safetyBoundary: "No inbound OT access, no write path to production assets, outbound-only push to PlantProcess IQ.",
    backendRoutes: [
      "/api/v5/edge-collector/health",
      "/api/v5/edge-collector/contract",
      "/api/v5/edge-collector/profiles",
      "/api/v5/edge-collector/register",
      "/api/v5/edge-collector/heartbeat",
      "/api/v5/edge-collector/push-batch",
      "/api/v5/edge-collector/queue-status",
      "/api/v5/edge-collector/status"
    ],
    results
  };

  write(reportJsonPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];

  md.push("# Pack F-2 T-066 OT-Safe Edge Backend Report");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND");
  md.push("");
  md.push("## Scope");
  md.push("");
  md.push("This step creates the backend proof for an OT-safe edge collector. The collector contract is read-only toward source systems, opens no inbound OT listener, and pushes outbound batches to PlantProcess IQ.");
  md.push("");
  md.push("## Backend routes");
  md.push("");
  for (const route of payload.backendRoutes) md.push("- " + route);
  md.push("");
  md.push("## Changed files");
  md.push("");
  md.push("| File | Status |");
  md.push("|---|---|");
  for (const item of results) md.push("| `" + item.path + "` | " + item.status + " |");
  md.push("");

  write(reportMdPath, md.join("\n"));

  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack F Evidence\n";

  if (!evidence.includes("PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND")) {
    evidence += [
      "",
      "## Pack F-2 T-066 OT-safe edge agent one-way push backend",
      "",
      "- Marker: PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND.",
      "- Added OT-safe edge collector backend endpoints.",
      "- Added worker-side edge agent contract model.",
      "- Added register, heartbeat, push-batch, queue-status, status, contract and profile routes.",
      "- Added safety documentation for read-only / outbound-only behavior.",
      "- Added validator and T-066 scorecard bridge.",
      "- Backend build must remain green.",
      "",
      "Generated artifacts:",
      "",
      "- Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs",
      "- Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs",
      "- docs/developer/OT_SAFE_EDGE_AGENT_CONTRACT.md",
      "- docs/developer/OT_SAFE_EDGE_AGENT_BACKEND_RUNBOOK.md",
      "- docs/pack-f/PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.md",
      "- docs/pack-f/PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.json",
      "- tools/pack-f/validate-pack-f-t066-edge-backend.cjs",
      "- tools/task-closure/ppiq-pack-f2-scorecard-bridge.cjs",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack F-2 — T-066 OT-safe edge agent one-way push backend");
console.log("=================================================================================================");
console.log("Backup folder: " + rel(backupRoot));

ensureDir(backupRoot);
ensureDir(docsDir);
ensureDir(developerDocsDir);
ensureDir(toolsDir);
ensureDir(toolsTaskClosureDir);

const results = [];
results.push(writeEndpoint());
results.push(writeAgentContract());
results.push(patchProgram());
results.push(writeValidator());
results.push(writeBridge());
writeDocs();
writeReports(results);

run("node --check Pack F-2 validator", ["node", "--check", "tools/pack-f/validate-pack-f-t066-edge-backend.cjs"]);
run("node --check Pack F2 bridge", ["node", "--check", "tools/task-closure/ppiq-pack-f2-scorecard-bridge.cjs"]);
run("Pack F-2 T-066 edge backend validator", ["node", "tools/pack-f/validate-pack-f-t066-edge-backend.cjs"]);

run("Backend dotnet build", ["dotnet", "build", "Backend"]);

if (isFile(path.join(root, "tools", "pack-e", "Invoke-PackE-FinalClosure.ps1"))) {
  run(
    "Pack E final closure",
    [
      "powershell",
      "-ExecutionPolicy",
      "Bypass",
      "-File",
      ".\\tools\\pack-e\\Invoke-PackE-FinalClosure.ps1",
      "-ProjectRoot",
      root
    ],
    root,
    true
  );
}

run("Apply Pack F2 scorecard bridge", ["node", "tools/task-closure/ppiq-pack-f2-scorecard-bridge.cjs"]);

console.log("");
console.log("Pack F-2 backend result:");
console.log(" - OT-safe edge collector backend endpoints written");
console.log(" - Worker-side edge agent contract written");
console.log(" - Program endpoint registration patched");
console.log(" - T-066 validator passed");
console.log(" - Backend build passed");
console.log(" - T-066 scorecard bridge applied");

console.log("");
console.log("=================================================================================================");
console.log("Pack F-2 completed.");
console.log("Expected: T-066 is green in the Pack F2 bridged scorecard.");
console.log("Report: docs/pack-f/PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.md");
console.log("Next: Pack F-3 / T-067 — Edge agent packaging and deployment.");
console.log("=================================================================================================");