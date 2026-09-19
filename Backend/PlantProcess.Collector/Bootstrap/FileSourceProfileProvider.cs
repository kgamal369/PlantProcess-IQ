using System.Text.Json;
using System.Text.Json.Serialization;
using PlantProcess.Collector.OpcUa;

namespace PlantProcess.Collector.Bootstrap;

/// <summary>
/// Collector-local bootstrap profile source: one published profile version delivered as a
/// file inside the customer boundary. It publishes nothing of its own; the exact id and
/// version must match or nothing is returned.
/// </summary>
public sealed class FileSourceProfileProvider : IOpcUaCollectorSourceProfileProvider
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public FileSourceProfileProvider(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public async ValueTask<OpcUaCollectorSourceProfile?> GetAsync(
        string sourceProfileId,
        string configurationVersion,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(_path);
        OpcUaCollectorSourceProfile? profile = await JsonSerializer
            .DeserializeAsync<OpcUaCollectorSourceProfile>(stream, Options, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null ||
            !string.Equals(profile.SourceProfileId, sourceProfileId, StringComparison.Ordinal) ||
            !string.Equals(profile.ConfigurationVersion, configurationVersion, StringComparison.Ordinal))
        {
            return null;
        }

        return profile;
    }
}