namespace PlantProcess.Collector.OpcUa;

/// <summary>Message security mode requested by an authored source profile.</summary>
public enum OpcUaCollectorSecurityMode
{
    None,
    Sign,
    SignAndEncrypt
}

/// <summary>User identity mode requested by an authored source profile.</summary>
public enum OpcUaCollectorIdentityMode
{
    Anonymous,
    UserName
}

/// <summary>
/// The requested (authored) OPC UA session parameters for one exact published source
/// profile version. Nothing here is an effective or negotiated fact; the session receipt
/// records what the server actually accepted.
/// </summary>
public sealed record OpcUaCollectorSourceProfile(
    string SourceProfileId,
    string ConfigurationVersion,
    string EndpointUrl,
    string SecurityPolicyUri,
    OpcUaCollectorSecurityMode SecurityMode,
    OpcUaCollectorIdentityMode IdentityMode,
    string? SecretReference,
    uint RequestedSessionTimeoutMs = 60000,
    int KeepAliveIntervalMs = 5000,
    int ReconnectPeriodMs = 1000,
    int MaxReconnectPeriodMs = 15000)
{
    private const string NoSecurityPolicyUri = "http://opcfoundation.org/UA/SecurityPolicy#None";

    /// <summary>Returns every violation; an empty list means the profile is executable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SourceProfileId))
        {
            errors.Add("SourceProfileId is required.");
        }

        if (string.IsNullOrWhiteSpace(ConfigurationVersion))
        {
            errors.Add("ConfigurationVersion is required.");
        }

        if (!Uri.TryCreate(EndpointUrl, UriKind.Absolute, out Uri? endpoint) ||
            !string.Equals(endpoint.Scheme, "opc.tcp", StringComparison.Ordinal))
        {
            errors.Add("EndpointUrl must be an absolute opc.tcp URL.");
        }

        if (string.IsNullOrWhiteSpace(SecurityPolicyUri))
        {
            errors.Add("SecurityPolicyUri is required.");
        }
        else
        {
            bool policyIsNone = string.Equals(SecurityPolicyUri, NoSecurityPolicyUri, StringComparison.Ordinal);
            bool modeIsNone = SecurityMode == OpcUaCollectorSecurityMode.None;
            if (policyIsNone != modeIsNone)
            {
                errors.Add("SecurityPolicyUri None and SecurityMode None must be requested together.");
            }
        }

        if (IdentityMode == OpcUaCollectorIdentityMode.UserName && string.IsNullOrWhiteSpace(SecretReference))
        {
            errors.Add("UserName identity requires a SecretReference.");
        }

        if (IdentityMode == OpcUaCollectorIdentityMode.Anonymous && !string.IsNullOrWhiteSpace(SecretReference))
        {
            errors.Add("Anonymous identity must not carry a SecretReference.");
        }

        if (RequestedSessionTimeoutMs < 10000 || RequestedSessionTimeoutMs > 3600000)
        {
            errors.Add("RequestedSessionTimeoutMs must be between 10000 and 3600000.");
        }

        if (KeepAliveIntervalMs < 500 || KeepAliveIntervalMs > 60000)
        {
            errors.Add("KeepAliveIntervalMs must be between 500 and 60000.");
        }

        if (ReconnectPeriodMs < 500 || ReconnectPeriodMs > 30000)
        {
            errors.Add("ReconnectPeriodMs must be between 500 and 30000.");
        }

        if (MaxReconnectPeriodMs < ReconnectPeriodMs || MaxReconnectPeriodMs > 30000)
        {
            errors.Add("MaxReconnectPeriodMs must be between ReconnectPeriodMs and 30000.");
        }

        return errors;
    }
}

/// <summary>
/// Seam to the provider-neutral acquisition configuration authority. The collector binds
/// to one exact published profile version and never authors its own configuration.
/// </summary>
public interface IOpcUaCollectorSourceProfileProvider
{
    /// <summary>
    /// Returns the published profile for the exact id and version, or null when that
    /// version is not published to this collector.
    /// </summary>
    ValueTask<OpcUaCollectorSourceProfile?> GetAsync(
        string sourceProfileId,
        string configurationVersion,
        CancellationToken cancellationToken);
}