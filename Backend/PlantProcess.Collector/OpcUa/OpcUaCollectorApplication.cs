using System.Security.Cryptography.X509Certificates;
using Opc.Ua;
using Opc.Ua.Configuration;

namespace PlantProcess.Collector.OpcUa;

/// <summary>Identity and PKI location of the collector OPC UA client application.</summary>
public sealed record OpcUaCollectorApplicationOptions(
    string ApplicationName,
    string ApplicationUri,
    string ProductUri,
    string PkiRootPath,
    string CertificateSubjectName,
    int OperationTimeoutMs = 15000,
    ushort MinimumCertificateKeySize = 2048)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ApplicationName))
        {
            errors.Add("ApplicationName is required.");
        }

        if (string.IsNullOrWhiteSpace(ApplicationUri))
        {
            errors.Add("ApplicationUri is required.");
        }

        if (string.IsNullOrWhiteSpace(ProductUri))
        {
            errors.Add("ProductUri is required.");
        }

        if (string.IsNullOrWhiteSpace(PkiRootPath) || !Path.IsPathFullyQualified(PkiRootPath))
        {
            errors.Add("PkiRootPath must be a fully qualified path.");
        }

        if (string.IsNullOrWhiteSpace(CertificateSubjectName))
        {
            errors.Add("CertificateSubjectName is required.");
        }

        if (OperationTimeoutMs < 1000 || OperationTimeoutMs > 600000)
        {
            errors.Add("OperationTimeoutMs must be between 1000 and 600000.");
        }

        if (MinimumCertificateKeySize < 2048)
        {
            errors.Add("MinimumCertificateKeySize must be at least 2048.");
        }

        return errors;
    }
}

/// <summary>
/// The configured collector client application: strict trust (no auto-accept, SHA-1 refused,
/// minimum key size enforced) and its own application instance certificate.
/// </summary>
public sealed class OpcUaCollectorApplication
{
    internal OpcUaCollectorApplication(
        ApplicationConfiguration configuration,
        ITelemetryContext telemetry,
        OpcUaCollectorApplicationOptions options)
    {
        Configuration = configuration;
        Telemetry = telemetry;
        Options = options;
        TrustStore = new OpcUaCollectorTrustStore(this);
    }

    internal ApplicationConfiguration Configuration { get; }

    internal ITelemetryContext Telemetry { get; }

    public OpcUaCollectorApplicationOptions Options { get; }

    public OpcUaCollectorTrustStore TrustStore { get; }

    /// <summary>Facts about the collector's own application instance certificate.</summary>
    public OpcUaCollectorCertificateFact? ApplicationCertificate(DateTimeOffset nowUtc)
    {
        X509Certificate2? certificate = FindApplicationCertificate();
        return certificate is null
            ? null
            : OpcUaCollectorCertificateFact.From(certificate, nowUtc, OpcUaCollectorCertificateFact.DefaultExpiringWindow);
    }

    internal X509Certificate2? FindApplicationCertificate()
    {
        CertificateIdentifierCollection? identifiers = Configuration.SecurityConfiguration?.ApplicationCertificates;
        if (identifiers is null)
        {
            return null;
        }

        foreach (CertificateIdentifier identifier in identifiers)
        {
            if (identifier.Certificate is not null)
            {
                return identifier.Certificate;
            }
        }

        return null;
    }
}

/// <summary>Creates the collector client application inside the customer boundary.</summary>
public static class OpcUaCollectorApplicationFactory
{
    public static async Task<OpcUaCollectorApplication> CreateAsync(
        OpcUaCollectorApplicationOptions options,
        ITelemetryContext telemetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(telemetry);

        IReadOnlyList<string> errors = options.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "Collector application options are invalid: " + string.Join(" ", errors),
                nameof(options));
        }

        Directory.CreateDirectory(options.PkiRootPath);

        var instance = new ApplicationInstance(telemetry)
        {
            ApplicationName = options.ApplicationName,
            ApplicationType = ApplicationType.Client
        };

        var certificates = new CertificateIdentifierCollection
        {
            new CertificateIdentifier
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = Path.Combine(options.PkiRootPath, "own"),
                SubjectName = options.CertificateSubjectName,
                CertificateType = ObjectTypeIds.RsaSha256ApplicationCertificateType
            }
        };

        ApplicationConfiguration configuration = await instance
            .Build(options.ApplicationUri, options.ProductUri)
            .SetOperationTimeout(options.OperationTimeoutMs)
            .AsClient()
            .AddSecurityConfiguration(certificates, options.PkiRootPath)
            .SetAutoAcceptUntrustedCertificates(false)
            .SetRejectSHA1SignedCertificates(true)
            .SetMinimumCertificateKeySize(options.MinimumCertificateKeySize)
            .SetUseValidatedCertificates(false)
            .SetAddAppCertToTrustedStore(false)
            .CreateAsync(cancellationToken)
            .ConfigureAwait(false);

        bool haveCertificate = await instance
            .CheckApplicationInstanceCertificatesAsync(true, null, cancellationToken)
            .ConfigureAwait(false);

        if (!haveCertificate)
        {
            throw new InvalidOperationException(
                "The collector application instance certificate is not available.");
        }

        return new OpcUaCollectorApplication(configuration, telemetry, options);
    }
}