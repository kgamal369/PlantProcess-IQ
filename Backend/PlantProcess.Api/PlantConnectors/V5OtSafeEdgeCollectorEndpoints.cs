using System.Collections.Concurrent;

namespace PlantProcess.Api.PlantConnectors;

public sealed record EdgeCollectorRegisterRequest(
    string CollectorId,
    string DisplayName,
    string SiteName,
    string NetworkZone,
    string AgentVersion,
    string PushEndpointUrl,
    bool ReadOnlyCollection,
    bool OutboundOnly,
    bool OpensInboundListener,
    string[]? SourceProfiles);

public sealed record EdgeCollectorHeartbeatRequest(
    string CollectorId,
    string AgentVersion,
    DateTimeOffset ObservedAtUtc,
    string Status,
    int LocalQueueDepth,
    int FailedPushCount,
    string? LastSuccessfulPushUtc,
    string? LastError);

public sealed record EdgeCollectorSample(
    string SourceProfile,
    string TagPath,
    DateTimeOffset TimestampUtc,
    double? NumericValue,
    string? TextValue,
    string? Unit,
    string Quality);

public sealed record EdgeCollectorPushBatchRequest(
    string CollectorId,
    string BatchId,
    DateTimeOffset CreatedAtUtc,
    bool ReadOnlyCollection,
    bool OutboundOnly,
    int SequenceNumber,
    EdgeCollectorSample[] Samples);

public sealed record EdgeCollectorQueueStatusRequest(
    string CollectorId,
    int QueueDepth,
    int OldestItemAgeSeconds,
    int FailedPushCount,
    int LastBatchSize,
    string? LastError);

internal sealed record EdgeCollectorState(
    string CollectorId,
    string DisplayName,
    string SiteName,
    string NetworkZone,
    string AgentVersion,
    string PushEndpointUrl,
    bool ReadOnlyCollection,
    bool OutboundOnly,
    bool OpensInboundListener,
    string[] SourceProfiles,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? LastHeartbeatUtc,
    DateTimeOffset? LastPushUtc,
    int LocalQueueDepth,
    int FailedPushCount,
    int AcceptedSamples,
    string Status,
    string? LastError);

public static class V5OtSafeEdgeCollectorEndpoints
{
    private static readonly ConcurrentDictionary<string, EdgeCollectorState> Collectors = new(StringComparer.OrdinalIgnoreCase);

    public static IEndpointRouteBuilder MapV5OtSafeEdgeCollectorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/edge-collector")
            .WithTags("V5 OT-Safe Edge Collector");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-ot-safe-edge-collector",
            marker = "PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND",
            mode = "read-only-outbound-one-way-push",
            noInboundOtAccessRequired = true,
            opensInboundListener = false,
            supportsRegistration = true,
            supportsHeartbeat = true,
            supportsBatchPush = true,
            supportsQueueStatus = true
        }));

        group.MapGet("/contract", () => Results.Ok(new
        {
            contract = "OT-safe edge collector one-way push",
            marker = "PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND",
            safetyRules = new[]
            {
                "Collector reads only from configured source profiles.",
                "Collector never writes to PLC/SCADA/MES/source systems.",
                "Collector does not require inbound access into the OT network.",
                "Collector pushes outbound batches to PlantProcess IQ only.",
                "Collector uses bounded local queue/spool behavior for temporary outages.",
                "Secrets are referenced by configuration and must not be hardcoded."
            },
            routes = new[]
            {
                "GET /api/v5/edge-collector/health",
                "GET /api/v5/edge-collector/contract",
                "GET /api/v5/edge-collector/profiles",
                "POST /api/v5/edge-collector/register",
                "POST /api/v5/edge-collector/heartbeat",
                "POST /api/v5/edge-collector/push-batch",
                "POST /api/v5/edge-collector/queue-status",
                "GET /api/v5/edge-collector/status"
            }
        }));

        group.MapGet("/profiles", () => Results.Ok(new
        {
            profiles = new[]
            {
                new
                {
                    profileCode = "historian-readonly",
                    displayName = "Historian read-only poller",
                    direction = "OT source -> edge queue -> outbound PPIQ push",
                    writesToSource = false,
                    requiresInboundOtFirewallRule = false
                },
                new
                {
                    profileCode = "file-drop-readonly",
                    displayName = "File-drop read-only collector",
                    direction = "Local folder -> edge queue -> outbound PPIQ push",
                    writesToSource = false,
                    requiresInboundOtFirewallRule = false
                },
                new
                {
                    profileCode = "sql-readonly",
                    displayName = "SQL read-only snapshot collector",
                    direction = "Read-only SQL view -> edge queue -> outbound PPIQ push",
                    writesToSource = false,
                    requiresInboundOtFirewallRule = false
                }
            }
        }));

        group.MapPost("/register", (EdgeCollectorRegisterRequest request) =>
        {
            var validation = ValidateRegistration(request);
            if (validation is not null) return validation;

            var now = DateTimeOffset.UtcNow;
            var state = new EdgeCollectorState(
                CollectorId: request.CollectorId.Trim(),
                DisplayName: request.DisplayName.Trim(),
                SiteName: request.SiteName.Trim(),
                NetworkZone: request.NetworkZone.Trim(),
                AgentVersion: request.AgentVersion.Trim(),
                PushEndpointUrl: request.PushEndpointUrl.Trim(),
                ReadOnlyCollection: request.ReadOnlyCollection,
                OutboundOnly: request.OutboundOnly,
                OpensInboundListener: request.OpensInboundListener,
                SourceProfiles: NormalizeProfiles(request.SourceProfiles),
                RegisteredAtUtc: now,
                LastHeartbeatUtc: null,
                LastPushUtc: null,
                LocalQueueDepth: 0,
                FailedPushCount: 0,
                AcceptedSamples: 0,
                Status: "registered",
                LastError: null);

            Collectors.AddOrUpdate(state.CollectorId, state, (_, _) => state);

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Edge collector registered with OT-safe outbound-only contract.",
                collectorId = state.CollectorId,
                registeredAtUtc = now,
                noInboundOtAccessRequired = true,
                readOnlyCollection = state.ReadOnlyCollection,
                outboundOnly = state.OutboundOnly
            });
        });

        group.MapPost("/heartbeat", (EdgeCollectorHeartbeatRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.CollectorId))
                return Results.BadRequest(new { isSuccess = false, message = "CollectorId is required." });

            var state = EnsureCollector(request.CollectorId);
            var updated = state with
            {
                AgentVersion = string.IsNullOrWhiteSpace(request.AgentVersion) ? state.AgentVersion : request.AgentVersion.Trim(),
                LastHeartbeatUtc = DateTimeOffset.UtcNow,
                LocalQueueDepth = Math.Max(0, request.LocalQueueDepth),
                FailedPushCount = Math.Max(0, request.FailedPushCount),
                Status = string.IsNullOrWhiteSpace(request.Status) ? "heartbeat" : request.Status.Trim(),
                LastError = request.LastError
            };

            Collectors.AddOrUpdate(updated.CollectorId, updated, (_, _) => updated);

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Heartbeat accepted.",
                collectorId = updated.CollectorId,
                serverReceivedAtUtc = DateTimeOffset.UtcNow,
                queueDepth = updated.LocalQueueDepth,
                status = updated.Status
            });
        });

        group.MapPost("/push-batch", (EdgeCollectorPushBatchRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.CollectorId))
                return Results.BadRequest(new { isSuccess = false, message = "CollectorId is required." });

            if (!request.ReadOnlyCollection || !request.OutboundOnly)
            {
                return Results.BadRequest(new
                {
                    isSuccess = false,
                    message = "Batch rejected. Edge collector pushes must declare readOnlyCollection=true and outboundOnly=true."
                });
            }

            if (request.Samples is null || request.Samples.Length == 0)
                return Results.BadRequest(new { isSuccess = false, message = "At least one sample is required." });

            if (request.Samples.Length > 5000)
                return Results.BadRequest(new { isSuccess = false, message = "Batch limit exceeded. Maximum 5000 samples per push." });

            var invalidSample = request.Samples.FirstOrDefault(sample => string.IsNullOrWhiteSpace(sample.TagPath) || sample.TimestampUtc == default);
            if (invalidSample is not null)
                return Results.BadRequest(new { isSuccess = false, message = "Each sample requires TagPath and TimestampUtc." });

            var state = EnsureCollector(request.CollectorId);
            var updated = state with
            {
                LastPushUtc = DateTimeOffset.UtcNow,
                LocalQueueDepth = Math.Max(0, state.LocalQueueDepth - request.Samples.Length),
                AcceptedSamples = state.AcceptedSamples + request.Samples.Length,
                Status = "pushing",
                LastError = null
            };

            Collectors.AddOrUpdate(updated.CollectorId, updated, (_, _) => updated);

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Outbound edge batch accepted.",
                collectorId = updated.CollectorId,
                batchId = string.IsNullOrWhiteSpace(request.BatchId) ? "batch-unknown" : request.BatchId.Trim(),
                acceptedSamples = request.Samples.Length,
                acceptedAtUtc = DateTimeOffset.UtcNow,
                readOnlyCollection = true,
                outboundOnly = true
            });
        });

        group.MapPost("/queue-status", (EdgeCollectorQueueStatusRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.CollectorId))
                return Results.BadRequest(new { isSuccess = false, message = "CollectorId is required." });

            var state = EnsureCollector(request.CollectorId);
            var updated = state with
            {
                LocalQueueDepth = Math.Max(0, request.QueueDepth),
                FailedPushCount = Math.Max(0, request.FailedPushCount),
                LastError = request.LastError,
                Status = request.QueueDepth > 0 ? "queued" : "idle"
            };

            Collectors.AddOrUpdate(updated.CollectorId, updated, (_, _) => updated);

            return Results.Ok(new
            {
                isSuccess = true,
                collectorId = updated.CollectorId,
                queueDepth = updated.LocalQueueDepth,
                failedPushCount = updated.FailedPushCount,
                oldestItemAgeSeconds = Math.Max(0, request.OldestItemAgeSeconds),
                lastBatchSize = Math.Max(0, request.LastBatchSize),
                status = updated.Status
            });
        });

        group.MapGet("/status", () => Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            collectorCount = Collectors.Count,
            collectors = Collectors.Values
                .OrderBy(value => value.SiteName)
                .ThenBy(value => value.CollectorId)
                .Select(value => new
                {
                    value.CollectorId,
                    value.DisplayName,
                    value.SiteName,
                    value.NetworkZone,
                    value.AgentVersion,
                    value.ReadOnlyCollection,
                    value.OutboundOnly,
                    value.OpensInboundListener,
                    value.SourceProfiles,
                    value.RegisteredAtUtc,
                    value.LastHeartbeatUtc,
                    value.LastPushUtc,
                    value.LocalQueueDepth,
                    value.FailedPushCount,
                    value.AcceptedSamples,
                    value.Status,
                    value.LastError
                })
                .ToArray()
        }));

        return app;
    }

    private static IResult? ValidateRegistration(EdgeCollectorRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CollectorId))
            return Results.BadRequest(new { isSuccess = false, message = "CollectorId is required." });

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Results.BadRequest(new { isSuccess = false, message = "DisplayName is required." });

        if (string.IsNullOrWhiteSpace(request.SiteName))
            return Results.BadRequest(new { isSuccess = false, message = "SiteName is required." });

        if (string.IsNullOrWhiteSpace(request.PushEndpointUrl))
            return Results.BadRequest(new { isSuccess = false, message = "PushEndpointUrl is required." });

        if (!request.ReadOnlyCollection)
            return Results.BadRequest(new { isSuccess = false, message = "Edge collector must be read-only toward OT/source systems." });

        if (!request.OutboundOnly)
            return Results.BadRequest(new { isSuccess = false, message = "Edge collector must be outbound-only toward PlantProcess IQ." });

        if (request.OpensInboundListener)
            return Results.BadRequest(new { isSuccess = false, message = "Edge collector must not open an inbound listener in the OT network." });

        return null;
    }

    private static EdgeCollectorState EnsureCollector(string collectorId)
    {
        var normalized = collectorId.Trim();
        return Collectors.GetOrAdd(normalized, id => new EdgeCollectorState(
            CollectorId: id,
            DisplayName: id,
            SiteName: "unregistered-site",
            NetworkZone: "dmz-or-edge",
            AgentVersion: "unknown",
            PushEndpointUrl: "/api/v5/edge-collector/push-batch",
            ReadOnlyCollection: true,
            OutboundOnly: true,
            OpensInboundListener: false,
            SourceProfiles: new[] { "unregistered-readonly" },
            RegisteredAtUtc: DateTimeOffset.UtcNow,
            LastHeartbeatUtc: null,
            LastPushUtc: null,
            LocalQueueDepth: 0,
            FailedPushCount: 0,
            AcceptedSamples: 0,
            Status: "auto-created-from-push",
            LastError: null));
    }

    private static string[] NormalizeProfiles(string[]? profiles)
    {
        return (profiles is { Length: > 0 } ? profiles : new[] { "historian-readonly" })
            .Where(profile => !string.IsNullOrWhiteSpace(profile))
            .Select(profile => profile.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }
}
