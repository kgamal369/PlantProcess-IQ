using System.Text;
using PlantProcess.Collector.OpcUa;
using PlantProcess.Collector.Secrets;

namespace PlantProcess.Collector.Tests.TestSupport;

/// <summary>Test profile publisher. Nothing is published unless a test publishes it.</summary>
internal sealed class InMemorySourceProfiles : IOpcUaCollectorSourceProfileProvider
{
    private readonly Dictionary<string, OpcUaCollectorSourceProfile> _published = new(StringComparer.Ordinal);

    internal void Publish(OpcUaCollectorSourceProfile profile)
        => _published[Key(profile.SourceProfileId, profile.ConfigurationVersion)] = profile;

    public ValueTask<OpcUaCollectorSourceProfile?> GetAsync(
        string sourceProfileId,
        string configurationVersion,
        CancellationToken cancellationToken)
    {
        _published.TryGetValue(Key(sourceProfileId, configurationVersion), out OpcUaCollectorSourceProfile? profile);
        return ValueTask.FromResult(profile);
    }

    private static string Key(string id, string version) => id + "@" + version;
}

/// <summary>Test secret path. A reference resolves only when a test registered it.</summary>
internal sealed class InMemorySecrets : ICollectorSecretResolver
{
    private readonly Dictionary<string, (string User, string Password)> _secrets = new(StringComparer.Ordinal);

    internal void Register(string secretReference, string user, string password)
        => _secrets[secretReference] = (user, password);

    public ValueTask<CollectorUserNameSecret?> ResolveUserNameAsync(
        string secretReference,
        CancellationToken cancellationToken)
    {
        if (!_secrets.TryGetValue(secretReference, out (string User, string Password) secret))
        {
            return ValueTask.FromResult<CollectorUserNameSecret?>(null);
        }

        return ValueTask.FromResult<CollectorUserNameSecret?>(
            new CollectorUserNameSecret(secret.User, Encoding.UTF8.GetBytes(secret.Password)));
    }
}