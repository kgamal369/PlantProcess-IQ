namespace PlantProcess.Collector.Secrets;

/// <summary>
/// Collector-side secret resolution. Source credentials are resolved inside the customer
/// boundary from a secret reference; the reference is configuration, the secret never is.
/// The approved collector vault supplies the implementation; this assembly ships none.
/// </summary>
public interface ICollectorSecretResolver
{
    /// <summary>
    /// Resolves a user-name credential for the reference, or returns null when the
    /// reference does not resolve. The caller owns and disposes the returned secret.
    /// </summary>
    ValueTask<CollectorUserNameSecret?> ResolveUserNameAsync(
        string secretReference,
        CancellationToken cancellationToken);
}

/// <summary>
/// A resolved user-name credential. The password is held as UTF-8 bytes, copied on entry,
/// cleared on dispose and never rendered by ToString.
/// </summary>
public sealed class CollectorUserNameSecret : IDisposable
{
    private readonly byte[] _password;
    private bool _disposed;

    public CollectorUserNameSecret(string userName, ReadOnlySpan<byte> passwordUtf8)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException("A user name is required.", nameof(userName));
        }

        UserName = userName;
        _password = passwordUtf8.ToArray();
    }

    public string UserName { get; }

    internal ReadOnlySpan<byte> Password
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _password;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Array.Clear(_password);
        _disposed = true;
    }

    public override string ToString() => "CollectorUserNameSecret(redacted)";
}