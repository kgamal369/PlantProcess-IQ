using System.Security.Cryptography.X509Certificates;
using Opc.Ua;

namespace PlantProcess.Collector.OpcUa;

/// <summary>Trust state of the collector at one moment.</summary>
public sealed record OpcUaCollectorTrustReport(
    DateTimeOffset ObservedAtUtc,
    OpcUaCollectorCertificateFact? ApplicationCertificate,
    IReadOnlyList<OpcUaCollectorCertificateFact> TrustedPeers,
    IReadOnlyList<OpcUaCollectorCertificateFact> RejectedPeers);

/// <summary>
/// Operator trust management for server certificates. Trust is only ever granted by an
/// explicit call; nothing here accepts a certificate automatically.
/// </summary>
public sealed class OpcUaCollectorTrustStore
{
    private readonly OpcUaCollectorApplication _application;

    internal OpcUaCollectorTrustStore(OpcUaCollectorApplication application)
    {
        _application = application;
    }

    public Task<IReadOnlyList<OpcUaCollectorCertificateFact>> ListTrustedAsync(CancellationToken cancellationToken)
        => ListAsync(Security.TrustedPeerCertificates, cancellationToken);

    public Task<IReadOnlyList<OpcUaCollectorCertificateFact>> ListRejectedAsync(CancellationToken cancellationToken)
        => ListAsync(Security.RejectedCertificateStore, cancellationToken);

    public async Task<OpcUaCollectorTrustReport> InspectAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IReadOnlyList<OpcUaCollectorCertificateFact> trusted = await ListTrustedAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<OpcUaCollectorCertificateFact> rejected = await ListRejectedAsync(cancellationToken).ConfigureAwait(false);
        return new OpcUaCollectorTrustReport(now, _application.ApplicationCertificate(now), trusted, rejected);
    }

    /// <summary>Adds the public part of an operator-supplied server certificate to the trusted peers.</summary>
    public async Task TrustCertificateAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        using X509Certificate2 publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
        using (ICertificateStore trusted = Open(Security.TrustedPeerCertificates))
        {
            X509Certificate2Collection existing = await trusted
                .FindByThumbprintAsync(publicOnly.Thumbprint, cancellationToken)
                .ConfigureAwait(false);
            if (existing.Count == 0)
            {
                await trusted.AddAsync(publicOnly, ct: cancellationToken).ConfigureAwait(false);
            }
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves a previously rejected server certificate into the trusted peers.
    /// Returns false when no rejected certificate carries the thumbprint.
    /// </summary>
    public async Task<bool> TrustRejectedAsync(string thumbprint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

        X509Certificate2? candidate = null;
        using (ICertificateStore rejected = Open(Security.RejectedCertificateStore))
        {
            X509Certificate2Collection found = await rejected
                .FindByThumbprintAsync(thumbprint, cancellationToken)
                .ConfigureAwait(false);
            if (found.Count > 0)
            {
                candidate = X509CertificateLoader.LoadCertificate(found[0].RawData);
            }
        }

        if (candidate is null)
        {
            return false;
        }

        using (candidate)
        {
            await TrustCertificateAsync(candidate, cancellationToken).ConfigureAwait(false);
        }

        using (ICertificateStore rejected = Open(Security.RejectedCertificateStore))
        {
            await rejected.DeleteAsync(thumbprint, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Removes trust for a server certificate. Returns false when it was not trusted.
    /// New sessions to that server are refused afterwards.
    /// </summary>
    public async Task<bool> RevokeTrustAsync(string thumbprint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

        bool removed;
        using (ICertificateStore trusted = Open(Security.TrustedPeerCertificates))
        {
            removed = await trusted.DeleteAsync(thumbprint, cancellationToken).ConfigureAwait(false);
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        return removed;
    }

    private SecurityConfiguration Security => _application.Configuration.SecurityConfiguration;

    private async Task<IReadOnlyList<OpcUaCollectorCertificateFact>> ListAsync(
        CertificateStoreIdentifier identifier,
        CancellationToken cancellationToken)
    {
        if (!StoreExists(identifier))
        {
            return Array.Empty<OpcUaCollectorCertificateFact>();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var facts = new List<OpcUaCollectorCertificateFact>();
        using ICertificateStore store = Open(identifier);
        X509Certificate2Collection certificates = await store.EnumerateAsync(cancellationToken).ConfigureAwait(false);
        foreach (X509Certificate2 certificate in certificates)
        {
            facts.Add(OpcUaCollectorCertificateFact.From(certificate, now, OpcUaCollectorCertificateFact.DefaultExpiringWindow));
        }

        return facts;
    }

    private static bool StoreExists(CertificateStoreIdentifier identifier)
    {
        if (!string.Equals(identifier.StoreType, CertificateStoreType.Directory, StringComparison.Ordinal))
        {
            return true;
        }

        return Directory.Exists(identifier.StorePath);
    }

    private ICertificateStore Open(CertificateStoreIdentifier identifier)
    {
        if (identifier is null)
        {
            throw new InvalidOperationException("The collector certificate store is not configured.");
        }

        if (string.Equals(identifier.StoreType, CertificateStoreType.Directory, StringComparison.Ordinal))
        {
            Directory.CreateDirectory(identifier.StorePath);
        }

        return identifier.OpenStore(_application.Telemetry)
            ?? throw new InvalidOperationException("The collector certificate store could not be opened.");
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        CertificateValidator validator = _application.Configuration.CertificateValidator;
        validator.ResetValidatedCertificates();
        await validator.UpdateAsync(_application.Configuration, cancellationToken).ConfigureAwait(false);
    }
}