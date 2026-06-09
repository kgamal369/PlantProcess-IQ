namespace PlantProcess.Application.Integration.Connectors;

/// <summary>
/// Environment-backed connector certification gate.
///
/// This is intentionally small and dependency-free because it is used by unit tests,
/// local/demo startup checks, and rollout gates before a connector is allowed to be
/// presented as certified/GA.
///
/// Convention:
///   PPIQ_CONNECTOR_CERTIFIED_<PROVIDER>
///
/// Examples:
///   PPIQ_CONNECTOR_CERTIFIED_POSTGRESQL=true
///   PPIQ_CONNECTOR_CERTIFIED_MYSQL=1
///   PPIQ_CONNECTOR_CERTIFIED_ORACLE=false
/// </summary>
public static class ConnectorCertification
{
    public const string EnvironmentPrefix = "PPIQ_CONNECTOR_CERTIFIED_";

    public static bool IsCertified(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            return false;
        }

        var key = BuildEnvironmentKey(providerType);
        var value = Environment.GetEnvironmentVariable(key);

        return IsAffirmative(value);
    }

    public static string BuildEnvironmentKey(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            return EnvironmentPrefix;
        }

        var normalized = providerType
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return EnvironmentPrefix + normalized;
    }

    public static bool IsAffirmative(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim() switch
        {
            "1" => true,
            "true" => true,
            "TRUE" => true,
            "True" => true,
            "yes" => true,
            "YES" => true,
            "Yes" => true,
            "y" => true,
            "Y" => true,
            "on" => true,
            "ON" => true,
            "On" => true,
            _ => false
        };
    }
}