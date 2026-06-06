using System.Globalization;
using System.Text.Json;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Connectors.Historian;

/// <summary>
/// PPIQ_PACK_E2_GA_HISTORIAN_PROVIDER
/// Read-only OPC-UA / historian gateway connector.
///
/// This connector intentionally avoids pretending that a vendor live handshake
/// can be proven without a customer gateway. It validates a production-shaped
/// configuration and returns explicit metadata for UI registration, connection
/// testing, tag browsing, bounded reads and mapping handoff.
/// </summary>
public sealed class OpcUaHistorianConnector : IDataSourceConnector
{
    private string? _lastError;

    public string ProviderType => "OpcUaHistorian";

    public Task<DataSourceConnectionTestResult> TestConnectionAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var endpointUrl = FirstNonEmpty(
                connectionProfile.ApiBaseUrl,
                GetJsonString(connectionProfile.ConnectionOptionsJson, "endpointUrl", "opcEndpointUrl", "historianEndpointUrl"),
                connectionProfile.HostName);

            if (string.IsNullOrWhiteSpace(endpointUrl))
                return Task.FromResult(Failure("Historian connector requires ApiBaseUrl, HostName, or connectionOptionsJson.endpointUrl."));

            var namespaceUri = FirstNonEmpty(
                GetJsonString(connectionProfile.ConnectionOptionsJson, "namespaceUri", "namespace", "tagNamespace"),
                connectionProfile.SchemaName);

            var readOnly = GetJsonBool(connectionProfile.ConnectionOptionsJson, "readOnly") ?? connectionProfile.ReadOnlyEnforced;
            var requireLiveHandshake = GetJsonBool(connectionProfile.ConnectionOptionsJson, "requireLiveHandshake") ?? false;

            if (!readOnly)
                return Task.FromResult(Failure("Historian connector is read-only. Set readOnly=true / ReadOnlyEnforced=true."));

            if (requireLiveHandshake)
                return Task.FromResult(Failure("Live vendor handshake was requested, but no customer historian gateway is attached in this environment. Configuration is not marked as reachable."));

            var metadata = new Dictionary<string, string?>
            {
                ["providerType"] = ProviderType,
                ["mode"] = "read-only-gateway",
                ["endpointUrl"] = endpointUrl,
                ["namespaceUri"] = namespaceUri,
                ["readOnly"] = readOnly.ToString(CultureInfo.InvariantCulture),
                ["supportsTagBrowse"] = "true",
                ["supportsBoundedRead"] = "true",
                ["supportsMappingHints"] = "true",
                ["liveHandshake"] = "environment-specific"
            };

            _lastError = null;

            return Task.FromResult(new DataSourceConnectionTestResult(
                IsSuccess: true,
                Message: "Historian gateway configuration is valid for read-only onboarding. Live vendor handshake remains environment-specific.",
                TestedAtUtc: DateTime.UtcNow,
                Metadata: metadata));
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Task.FromResult(Failure("Historian connector validation failed: " + ex.Message));
        }
    }

    public string? GetLastError() => _lastError;

    private static DataSourceConnectionTestResult Failure(string message)
    {
        return new DataSourceConnectionTestResult(
            IsSuccess: false,
            Message: message,
            TestedAtUtc: DateTime.UtcNow,
            Metadata: new Dictionary<string, string?>
            {
                ["providerType"] = "OpcUaHistorian",
                ["mode"] = "read-only-gateway",
                ["errorCategory"] = "configuration-or-environment"
            });
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static string? GetJsonString(string? json, params string[] propertyNames)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);

            foreach (var propertyName in propertyNames)
            {
                if (document.RootElement.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool? GetJsonBool(string? json, params string[] propertyNames)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);

            foreach (var propertyName in propertyNames)
            {
                if (!document.RootElement.TryGetProperty(propertyName, out var value))
                    continue;

                if (value.ValueKind == JsonValueKind.True)
                    return true;

                if (value.ValueKind == JsonValueKind.False)
                    return false;

                if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
                    return parsed;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
