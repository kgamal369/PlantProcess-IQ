using System.Text.Json;

namespace PlantProcess.Infrastructure.Connectors;

/// <summary>
/// M1-01: the single credential resolver for every read-only DB connector.
/// Order (first hit wins): (1) connection_options_json {username,password};
/// (2) secret_reference -> env vars REF and REF_PASSWORD; (3) TYPED HARD FAILURE.
/// Never falls through to an empty username (which makes the driver inherit the OS/process
/// identity - the 'ELKA01' defect). A credential-less profile fails loudly, by name.
/// </summary>
public static class ConnectorCredentialResolver
{
    public sealed record Credentials(string Username, string Password);

    public static Credentials Resolve(string? connectionOptionsJson, string? secretReference, string providerLabel)
    {
        if (!string.IsNullOrWhiteSpace(connectionOptionsJson) && connectionOptionsJson.Trim() != "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(connectionOptionsJson);
                var root = doc.RootElement;
                var u = TryGet(root, "username") ?? TryGet(root, "user");
                var p = TryGet(root, "password");
                if (!string.IsNullOrWhiteSpace(u) && p is not null)
                {
                    return new Credentials(u!, p!);
                }
            }
            catch (JsonException)
            {
                throw new InvalidOperationException(
                    providerLabel + " connection_options_json is not valid JSON; cannot resolve credentials.");
            }
        }

        if (!string.IsNullOrWhiteSpace(secretReference))
        {
            var u = Environment.GetEnvironmentVariable(secretReference!);
            var p = Environment.GetEnvironmentVariable(secretReference + "_PASSWORD");
            if (!string.IsNullOrWhiteSpace(u) && p is not null)
            {
                return new Credentials(u!, p!);
            }
        }

        throw new InvalidOperationException(
            providerLabel + " connection failed: no credentials resolved. Provide connection_options_json " +
            "with username and password, or a secret_reference resolving to REF and REF_PASSWORD " +
            "environment variables. The connector will not inherit the host account identity.");
    }

    private static string? TryGet(JsonElement root, string name)
    {
        return root.ValueKind == JsonValueKind.Object
               && root.TryGetProperty(name, out var v)
               && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }
}
