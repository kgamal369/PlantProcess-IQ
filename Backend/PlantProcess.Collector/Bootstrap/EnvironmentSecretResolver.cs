using System.Text;
using PlantProcess.Collector.Secrets;

namespace PlantProcess.Collector.Bootstrap;

/// <summary>
/// Collector-local bootstrap secret path: the operator injects the source credential into
/// the collector process environment. The secret never leaves the customer boundary and is
/// never written to a receipt or a log.
///
/// Variable names for reference R: PPIQ_COLLECTOR_SECRET_R_USER and
/// PPIQ_COLLECTOR_SECRET_R_PASSWORD, where R is upper-cased and every character outside
/// A-Z, 0-9 becomes an underscore.
/// </summary>
public sealed class EnvironmentSecretResolver : ICollectorSecretResolver
{
    public const string Prefix = "PPIQ_COLLECTOR_SECRET_";

    private readonly Func<string, string?> _read;

    public EnvironmentSecretResolver()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    public EnvironmentSecretResolver(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        _read = read;
    }

    public static string NormalizeReference(string secretReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);

        var builder = new StringBuilder(secretReference.Length);
        foreach (char character in secretReference.ToUpperInvariant())
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    public ValueTask<CollectorUserNameSecret?> ResolveUserNameAsync(
        string secretReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secretReference))
        {
            return ValueTask.FromResult<CollectorUserNameSecret?>(null);
        }

        string key = Prefix + NormalizeReference(secretReference);
        string? user = _read(key + "_USER");
        string? password = _read(key + "_PASSWORD");

        if (string.IsNullOrWhiteSpace(user) || password is null)
        {
            return ValueTask.FromResult<CollectorUserNameSecret?>(null);
        }

        byte[] encoded = Encoding.UTF8.GetBytes(password);
        try
        {
            return ValueTask.FromResult<CollectorUserNameSecret?>(new CollectorUserNameSecret(user, encoded));
        }
        finally
        {
            Array.Clear(encoded);
        }
    }
}