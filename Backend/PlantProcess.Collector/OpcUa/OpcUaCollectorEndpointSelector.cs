using Opc.Ua;

namespace PlantProcess.Collector.OpcUa;

/// <summary>Result of endpoint discovery and exact selection.</summary>
internal sealed record OpcUaCollectorEndpointSelection(
    EndpointDescription? Selected,
    bool IdentityModeOffered,
    IReadOnlyList<OpcUaCollectorOfferedEndpoint> Offered);

/// <summary>
/// Discovers the server endpoints and selects exactly the requested policy, mode and
/// transport. There is no fallback: an endpoint that is not offered is refused, never
/// replaced by a weaker one.
/// </summary>
internal static class OpcUaCollectorEndpointSelector
{
    internal static async Task<OpcUaCollectorEndpointSelection> DiscoverAsync(
        ApplicationConfiguration configuration,
        OpcUaCollectorSourceProfile profile,
        CancellationToken cancellationToken)
    {
        var discoveryUri = new Uri(profile.EndpointUrl);
        EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(configuration);

        using DiscoveryClient client = await DiscoveryClient
            .CreateAsync(configuration, discoveryUri, endpointConfiguration, ct: cancellationToken)
            .ConfigureAwait(false);

        EndpointDescriptionCollection endpoints = await client
            .GetEndpointsAsync(null, cancellationToken)
            .ConfigureAwait(false);

        return Select(endpoints, profile, discoveryUri);
    }

    internal static OpcUaCollectorEndpointSelection Select(
        IEnumerable<EndpointDescription> endpoints,
        OpcUaCollectorSourceProfile profile,
        Uri discoveryUri)
    {
        var offered = new List<OpcUaCollectorOfferedEndpoint>();
        var candidates = new List<EndpointDescription>();
        MessageSecurityMode requestedMode = ToStackMode(profile.SecurityMode);
        string requestedPath = discoveryUri.AbsolutePath.TrimEnd('/');

        foreach (EndpointDescription endpoint in endpoints)
        {
            offered.Add(Describe(endpoint));

            if (!string.Equals(endpoint.SecurityPolicyUri, profile.SecurityPolicyUri, StringComparison.Ordinal) ||
                endpoint.SecurityMode != requestedMode ||
                !IsUaTcp(endpoint.TransportProfileUri))
            {
                continue;
            }

            if (!Uri.TryCreate(endpoint.EndpointUrl, UriKind.Absolute, out Uri? endpointUri) ||
                !string.Equals(endpointUri.Scheme, discoveryUri.Scheme, StringComparison.Ordinal) ||
                !string.Equals(endpointUri.AbsolutePath.TrimEnd('/'), requestedPath, StringComparison.Ordinal))
            {
                continue;
            }

            candidates.Add(endpoint);
        }

        if (candidates.Count == 0)
        {
            return new OpcUaCollectorEndpointSelection(null, false, offered);
        }

        EndpointDescription best = candidates
            .OrderByDescending(candidate => candidate.SecurityLevel)
            .First();

        UserTokenType requestedToken = profile.IdentityMode == OpcUaCollectorIdentityMode.UserName
            ? UserTokenType.UserName
            : UserTokenType.Anonymous;

        bool identityOffered = best.UserIdentityTokens is not null &&
            best.UserIdentityTokens.Any(token => token.TokenType == requestedToken);

        var chosen = (EndpointDescription)best.Clone();
        var rewritten = new UriBuilder(new Uri(chosen.EndpointUrl))
        {
            Host = discoveryUri.IdnHost,
            Port = discoveryUri.Port
        };
        chosen.EndpointUrl = rewritten.Uri.ToString();

        return new OpcUaCollectorEndpointSelection(chosen, identityOffered, offered);
    }

    internal static MessageSecurityMode ToStackMode(OpcUaCollectorSecurityMode mode) => mode switch
    {
        OpcUaCollectorSecurityMode.None => MessageSecurityMode.None,
        OpcUaCollectorSecurityMode.Sign => MessageSecurityMode.Sign,
        OpcUaCollectorSecurityMode.SignAndEncrypt => MessageSecurityMode.SignAndEncrypt,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown security mode.")
    };

    private static bool IsUaTcp(string? transportProfileUri)
        => string.IsNullOrEmpty(transportProfileUri) ||
           string.Equals(transportProfileUri, Profiles.UaTcpTransport, StringComparison.Ordinal);

    private static OpcUaCollectorOfferedEndpoint Describe(EndpointDescription endpoint)
    {
        var tokens = new List<string>();
        if (endpoint.UserIdentityTokens is not null)
        {
            foreach (UserTokenPolicy policy in endpoint.UserIdentityTokens)
            {
                tokens.Add(policy.TokenType.ToString());
            }
        }

        return new OpcUaCollectorOfferedEndpoint(
            endpoint.EndpointUrl ?? string.Empty,
            endpoint.SecurityPolicyUri ?? string.Empty,
            endpoint.SecurityMode.ToString(),
            endpoint.TransportProfileUri ?? string.Empty,
            endpoint.SecurityLevel,
            endpoint.Server?.ApplicationUri,
            tokens);
    }
}