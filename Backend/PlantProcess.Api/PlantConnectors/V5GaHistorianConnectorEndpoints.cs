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

/// <summary>
/// One declared connector operation and whether the build actually executes it.
/// </summary>
public sealed record ConnectorCapability(string Name, bool Executable, string Evidence);

/// <summary>
/// T-207. The single truth source for what this connector can do. Every
/// advertised flag on every route is bound to this registry; no route declares
/// a capability as a literal. A capability may be flipped to Executable = true
/// only together with an implementation that the contract test can prove.
/// The real OPC UA acquisition work earns the currently-false ones.
/// </summary>
public static class HistorianConnectorCapabilities
{
    public const string NotExecutableCode = "OT01";

    public const string NotExecutableMessage =
        "This connector operation is declared but not executable in this build. " +
        "It returns no value rather than a placeholder.";

    public const string ConfigurationValidation = "configurationValidation";
    public const string MappingHintsFromSuppliedTagPaths = "mappingHintsFromSuppliedTagPaths";
    public const string TagBrowse = "tagBrowse";
    public const string BoundedRead = "boundedRead";
    public const string Subscription = "subscription";
    public const string LiveVendorHandshake = "liveVendorHandshake";

    public static readonly IReadOnlyList<ConnectorCapability> All = new[]
    {
        new ConnectorCapability(
            ConfigurationValidation, true,
            "The /test-connection route validates the supplied endpoint and read-only posture and returns no measurement."),
        new ConnectorCapability(
            MappingHintsFromSuppliedTagPaths, true,
            "The /mapping-hints route classifies tag paths the caller supplied. It never supplies tag paths of its own."),
        new ConnectorCapability(
            TagBrowse, false,
            "Real OPC UA namespace browse is not yet implemented."),
        new ConnectorCapability(
            BoundedRead, false,
            "Real OPC UA value acquisition is not yet implemented."),
        new ConnectorCapability(
            Subscription, false,
            "Real OPC UA monitored items and subscriptions are not yet implemented."),
        new ConnectorCapability(
            LiveVendorHandshake, false,
            "Real OPC UA session security, certificate and trust handling is not yet implemented.")
    };

    public static bool IsExecutable(string name)
    {
        foreach (var capability in All)
        {
            if (string.Equals(capability.Name, name, StringComparison.Ordinal))
            {
                return capability.Executable;
            }
        }

        return false;
    }

    public static ConnectorCapability Get(string name)
    {
        foreach (var capability in All)
        {
            if (string.Equals(capability.Name, name, StringComparison.Ordinal))
            {
                return capability;
            }
        }

        throw new InvalidOperationException(
            "Connector capability '" + name + "' is not registered. Register it before any route may advertise it.");
    }

    /// <summary>
    /// The single failure shape for a declared-but-not-executable operation.
    /// 501 Not Implemented, typed, and carrying no data field of any kind.
    /// </summary>
    public static IResult NotExecutable(string capabilityName)
    {
        var capability = Get(capabilityName);

        return Results.Json(
            new
            {
                errorCode = NotExecutableCode,
                capability = capability.Name,
                executable = capability.Executable,
                message = NotExecutableMessage,
                evidence = capability.Evidence,
                providerType = "OpcUaHistorian"
            },
            statusCode: StatusCodes.Status501NotImplemented);
    }
}

public static class V5GaHistorianConnectorEndpoints
{
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
            mode = "configuration-and-mapping-only",
            supportsConnectionTest =
                HistorianConnectorCapabilities.IsExecutable(HistorianConnectorCapabilities.ConfigurationValidation),
            supportsTagBrowse =
                HistorianConnectorCapabilities.IsExecutable(HistorianConnectorCapabilities.TagBrowse),
            supportsBoundedRead =
                HistorianConnectorCapabilities.IsExecutable(HistorianConnectorCapabilities.BoundedRead),
            supportsSubscription =
                HistorianConnectorCapabilities.IsExecutable(HistorianConnectorCapabilities.Subscription),
            supportsMappingHints =
                HistorianConnectorCapabilities.IsExecutable(HistorianConnectorCapabilities.MappingHintsFromSuppliedTagPaths),
            liveVendorHandshake =
                HistorianConnectorCapabilities.IsExecutable(HistorianConnectorCapabilities.LiveVendorHandshake),
            capabilities = HistorianConnectorCapabilities.All
        }));

        group.MapGet("/provider", () => Results.Ok(new
        {
            providerType = "OpcUaHistorian",
            displayName = "OPC-UA / Historian Gateway",
            availability = "configuration-and-mapping-only",
            readOnly = true,
            writeMethodsExposed = false,
            description =
                "Validates read-only historian gateway configuration and classifies tag paths the customer supplies. " +
                "Address-space browse, value acquisition and subscriptions are not executable in this build and " +
                "return " + HistorianConnectorCapabilities.NotExecutableCode + ".",
            aliases = new[] { "historian", "opcua", "opc-ua", "piwebapi", "pi web api" },
            executableRoutes = new[] { "test-connection", "mapping-hints" },
            notExecutableRoutes = new[] { "browse-tags", "read-window" },
            capabilities = HistorianConnectorCapabilities.All
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
                return HistorianConnectorCapabilities.NotExecutable(
                    HistorianConnectorCapabilities.LiveVendorHandshake);
            }

            return Results.Ok(new
            {
                isSuccess = true,
                message = "Historian gateway configuration is well formed. No endpoint session was opened.",
                mode = "configuration-validation-only",
                providerType = NormalizeProvider(request.ProviderType),
                endpointUrl,
                namespaceUri = request.NamespaceUri,
                securityMode = string.IsNullOrWhiteSpace(request.SecurityMode) ? "configured-by-gateway" : request.SecurityMode,
                readOnly = true,
                testedAtUtc = DateTimeOffset.UtcNow,
                seedTagsEchoed = CallerSuppliedTags(request.SeedTags),
                liveHandshakeExecuted = false
            });
        });

        group.MapPost("/browse-tags", ([FromBody] HistorianBrowseTagsRequest request) =>
            HistorianConnectorCapabilities.NotExecutable(HistorianConnectorCapabilities.TagBrowse));

        group.MapPost("/read-window", ([FromBody] HistorianReadWindowRequest request) =>
            HistorianConnectorCapabilities.NotExecutable(HistorianConnectorCapabilities.BoundedRead));

        group.MapPost("/mapping-hints", ([FromBody] HistorianMappingHintsRequest request) =>
        {
            var tags = CallerSuppliedTags(request.TagPaths);

            if (tags.Length == 0)
            {
                return Results.BadRequest(new
                {
                    errorCode = HistorianConnectorCapabilities.NotExecutableCode,
                    message =
                        "TagPaths is required. This route classifies tag paths supplied by the caller and has no " +
                        "tag paths of its own to fall back to.",
                    providerType = "OpcUaHistorian"
                });
            }

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

    /// <summary>
    /// Returns only what the caller supplied. There is no fallback list: a route
    /// that has nothing to work on must say so rather than invent an input.
    /// </summary>
    private static string[] CallerSuppliedTags(string[]? tagPaths)
    {
        if (tagPaths is null || tagPaths.Length == 0)
        {
            return Array.Empty<string>();
        }

        return tagPaths
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToArray();
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
}

