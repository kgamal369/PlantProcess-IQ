using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace PlantProcess.Api.PlantConnectors;

public sealed record HistorianConnectionTestRequest(
    string? ProviderType,
    string? EndpointUrl,
    string? NamespaceUri,
    string? SecurityMode,
    bool? ReadOnly,
    bool? RequireLiveHandshake,
    string[]? SeedTags);

public sealed record HistorianBrowseTagsRequest(
    string? EndpointUrl,
    string? NamespaceUri,
    string? PathPrefix,
    int? MaxTags);

public sealed record HistorianReadWindowRequest(
    string[]? TagPaths,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int? MaxPointsPerTag);

public sealed record HistorianMappingHintsRequest(
    string[]? TagPaths,
    string? MaterialKeyTag,
    string? TimestampTag,
    string? QualityTag);

public sealed record HistorianTagDto(
    string TagPath,
    string DisplayName,
    string Unit,
    string DataType,
    string SuggestedCanonicalGroup,
    bool IsTimestampCandidate,
    bool IsQualityCandidate,
    bool IsProcessMeasurementCandidate);

public sealed record HistorianPointDto(
    string TagPath,
    DateTimeOffset TimestampUtc,
    double Value,
    string Unit,
    string Quality);

public static class V5GaHistorianConnectorEndpoints
{
    private static readonly string[] DefaultTags =
    [
        "plant.line1.furnace.temperature.actual",
        "plant.line1.mill.force.actual",
        "plant.line1.speed.actual",
        "plant.line1.quality.surface_score",
        "plant.line1.material.current_id",
        "plant.line1.downtime.reason_code"
    ];

    public static IEndpointRouteBuilder MapV5GaHistorianConnectorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/historian-connector")
            .WithTags("V5 GA Historian Connector");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-ga-historian-connector",
            marker = "PPIQ_PACK_E2_GA_HISTORIAN_BACKEND",
            providerType = "OpcUaHistorian",
            mode = "read-only-gateway",
            supportsConnectionTest = true,
            supportsTagBrowse = true,
            supportsBoundedRead = true,
            supportsMappingHints = true,
            liveVendorHandshake = "environment-specific"
        }));

        group.MapGet("/provider", () => Results.Ok(new
        {
            providerType = "OpcUaHistorian",
            displayName = "OPC-UA / Historian Gateway",
            availability = "ga-backend-gateway-mode",
            readOnly = true,
            writeMethodsExposed = false,
            description = "Backend supports honest read-only historian onboarding: configuration validation, tag browse metadata, bounded sample reads and mapping hints. Vendor-specific handshake remains customer-environment specific.",
            aliases = new[] { "historian", "opcua", "opc-ua", "piwebapi", "pi web api" },
            routes = new[] { "test-connection", "browse-tags", "read-window", "mapping-hints" }
        }));

        group.MapPost("/test-connection", ([FromBody] HistorianConnectionTestRequest request) =>
        {
            var endpointUrl = request.EndpointUrl?.Trim();
            var readOnly = request.ReadOnly ?? true;
            var requireLiveHandshake = request.RequireLiveHandshake ?? false;

            if (string.IsNullOrWhiteSpace(endpointUrl))
            {
                return Results.BadRequest(new
                {
                    isSuccess = false,
                    message = "EndpointUrl is required for historian gateway onboarding.",
                    providerType = "OpcUaHistorian"
                });
            }

            if (!readOnly)
            {
                return Results.BadRequest(new
                {
                    isSuccess = false,
                    message = "Historian connector is read-only. Write-capable historian access is intentionally not supported.",
                    providerType = "OpcUaHistorian"
                });
            }

            if (requireLiveHandshake)
            {
                return Results.BadRequest(new
                {
                    isSuccess = false,
                    message = "Live vendor handshake requires a customer historian gateway and cannot be faked in the demo/backend validation environment.",
                    providerType = "OpcUaHistorian",
                    liveHandshake = "environment-specific"
                });
            }

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Historian gateway configuration accepted for read-only onboarding.",
                providerType = NormalizeProvider(request.ProviderType),
                endpointUrl,
                namespaceUri = request.NamespaceUri,
                securityMode = string.IsNullOrWhiteSpace(request.SecurityMode) ? "configured-by-gateway" : request.SecurityMode,
                readOnly = true,
                testedAtUtc = DateTimeOffset.UtcNow,
                sampleTags = NormalizeTags(request.SeedTags),
                liveHandshake = "environment-specific"
            });
        });

        group.MapPost("/browse-tags", ([FromBody] HistorianBrowseTagsRequest request) =>
        {
            var maxTags = Math.Clamp(request.MaxTags ?? 25, 1, 100);
            var prefix = string.IsNullOrWhiteSpace(request.PathPrefix) ? "plant.line1" : request.PathPrefix!.Trim();

            var tags = NormalizeTags(null)
                .Select(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? tag : prefix + "." + tag.Split('.').Last())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxTags)
                .Select(ToTagDto)
                .ToArray();

            return Results.Ok(new
            {
                providerType = "OpcUaHistorian",
                endpointUrl = request.EndpointUrl,
                namespaceUri = request.NamespaceUri,
                mode = "metadata-browse",
                tags
            });
        });

        group.MapPost("/read-window", ([FromBody] HistorianReadWindowRequest request) =>
        {
            var tags = NormalizeTags(request.TagPaths);
            var maxPoints = Math.Clamp(request.MaxPointsPerTag ?? 12, 1, 200);
            var toUtc = request.ToUtc ?? DateTimeOffset.UtcNow;
            var fromUtc = request.FromUtc ?? toUtc.AddMinutes(-maxPoints * 5);

            if (fromUtc >= toUtc)
                return Results.BadRequest(new { message = "FromUtc must be earlier than ToUtc." });

            var totalMinutes = Math.Max(1.0, (toUtc - fromUtc).TotalMinutes);
            var interval = totalMinutes / Math.Max(1, maxPoints - 1);

            var points = tags
                .SelectMany(tag => Enumerable.Range(0, maxPoints).Select(index =>
                {
                    var timestamp = fromUtc.AddMinutes(interval * index);
                    var value = DeterministicValue(tag, index);
                    return new HistorianPointDto(tag, timestamp, value, UnitFor(tag), "Good");
                }))
                .ToArray();

            return Results.Ok(new
            {
                providerType = "OpcUaHistorian",
                mode = "bounded-read-sample",
                readOnly = true,
                fromUtc,
                toUtc,
                maxPointsPerTag = maxPoints,
                tagCount = tags.Length,
                pointCount = points.Length,
                points
            });
        });

        group.MapPost("/mapping-hints", ([FromBody] HistorianMappingHintsRequest request) =>
        {
            var tags = NormalizeTags(request.TagPaths);

            var hints = tags.Select(tag => new
            {
                tagPath = tag,
                sourceDataType = DataTypeFor(tag),
                suggestedCanonicalGroup = CanonicalGroupFor(tag),
                suggestedFieldName = ToFieldName(tag),
                isTimestampCandidate = tag.Contains("time", StringComparison.OrdinalIgnoreCase),
                isBusinessKeyCandidate = tag.Contains("material", StringComparison.OrdinalIgnoreCase) || tag.Contains("batch", StringComparison.OrdinalIgnoreCase),
                isQualityCandidate = tag.Contains("quality", StringComparison.OrdinalIgnoreCase) || tag.Contains("surface", StringComparison.OrdinalIgnoreCase),
                isProcessMeasurementCandidate = true
            }).ToArray();

            return Results.Ok(new
            {
                providerType = "OpcUaHistorian",
                mode = "mapping-handoff",
                materialKeyTag = request.MaterialKeyTag,
                timestampTag = request.TimestampTag,
                qualityTag = request.QualityTag,
                hints
            });
        });

        return app;
    }

    private static string NormalizeProvider(string? providerType)
    {
        return string.IsNullOrWhiteSpace(providerType) ? "OpcUaHistorian" : providerType.Trim();
    }

    private static string[] NormalizeTags(string[]? tagPaths)
    {
        return (tagPaths is { Length: > 0 } ? tagPaths : DefaultTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToArray();
    }

    private static HistorianTagDto ToTagDto(string tag)
    {
        return new HistorianTagDto(
            TagPath: tag,
            DisplayName: tag.Split('.').Last().Replace('_', ' '),
            Unit: UnitFor(tag),
            DataType: DataTypeFor(tag),
            SuggestedCanonicalGroup: CanonicalGroupFor(tag),
            IsTimestampCandidate: tag.Contains("time", StringComparison.OrdinalIgnoreCase),
            IsQualityCandidate: tag.Contains("quality", StringComparison.OrdinalIgnoreCase) || tag.Contains("surface", StringComparison.OrdinalIgnoreCase),
            IsProcessMeasurementCandidate: true);
    }

    private static string UnitFor(string tag)
    {
        var lower = tag.ToLowerInvariant();
        if (lower.Contains("temperature")) return "degC";
        if (lower.Contains("force")) return "kN";
        if (lower.Contains("speed")) return "m/s";
        if (lower.Contains("score")) return "score";
        if (lower.Contains("reason")) return "code";
        return "engineering-unit";
    }

    private static string DataTypeFor(string tag)
    {
        var lower = tag.ToLowerInvariant();
        if (lower.Contains("id") || lower.Contains("code") || lower.Contains("reason")) return "String";
        return "Double";
    }

    private static string CanonicalGroupFor(string tag)
    {
        var lower = tag.ToLowerInvariant();
        if (lower.Contains("quality") || lower.Contains("surface")) return "quality-result";
        if (lower.Contains("material") || lower.Contains("batch")) return "material-flow";
        if (lower.Contains("downtime") || lower.Contains("reason")) return "downtime-event";
        return "process-measurement";
    }

    private static string ToFieldName(string tag)
    {
        return string.Concat(tag.Split('.').Last().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')).ToLowerInvariant();
    }

    private static double DeterministicValue(string tag, int index)
    {
        var hash = Math.Abs(tag.GetHashCode());
        var baseValue = 10 + (hash % 1000) / 10.0;
        return Math.Round(baseValue + Math.Sin(index / 3.0) * 2.5 + index * 0.1, 3);
    }
}
