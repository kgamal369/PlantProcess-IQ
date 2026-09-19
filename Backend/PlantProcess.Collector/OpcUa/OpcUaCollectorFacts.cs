using System.Security.Cryptography.X509Certificates;

namespace PlantProcess.Collector.OpcUa;

/// <summary>Validity window state of a certificate at the moment it was observed.</summary>
public enum OpcUaCollectorCertificateValidity
{
    Valid,
    ExpiringSoon,
    Expired,
    NotYetValid
}

/// <summary>Public, secret-free facts about one certificate.</summary>
public sealed record OpcUaCollectorCertificateFact(
    string Thumbprint,
    string Subject,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc,
    OpcUaCollectorCertificateValidity Validity)
{
    public static readonly TimeSpan DefaultExpiringWindow = TimeSpan.FromDays(30);

    public static OpcUaCollectorCertificateFact From(
        X509Certificate2 certificate,
        DateTimeOffset nowUtc,
        TimeSpan expiringWindow)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var notBefore = new DateTimeOffset(certificate.NotBefore.ToUniversalTime(), TimeSpan.Zero);
        var notAfter = new DateTimeOffset(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero);

        OpcUaCollectorCertificateValidity validity;
        if (nowUtc < notBefore)
        {
            validity = OpcUaCollectorCertificateValidity.NotYetValid;
        }
        else if (nowUtc > notAfter)
        {
            validity = OpcUaCollectorCertificateValidity.Expired;
        }
        else if (notAfter - nowUtc <= expiringWindow)
        {
            validity = OpcUaCollectorCertificateValidity.ExpiringSoon;
        }
        else
        {
            validity = OpcUaCollectorCertificateValidity.Valid;
        }

        return new OpcUaCollectorCertificateFact(
            certificate.Thumbprint,
            certificate.Subject,
            notBefore,
            notAfter,
            validity);
    }
}

/// <summary>One endpoint the server offered during discovery, as reported by the server.</summary>
public sealed record OpcUaCollectorOfferedEndpoint(
    string EndpointUrl,
    string SecurityPolicyUri,
    string SecurityMode,
    string TransportProfileUri,
    byte SecurityLevel,
    string? ServerApplicationUri,
    IReadOnlyList<string> UserTokenTypes);

/// <summary>Lifecycle state of one collector session runtime.</summary>
public enum OpcUaCollectorSessionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Refused,
    Faulted,
    Closed
}

/// <summary>Typed outcome of one connect attempt.</summary>
public enum OpcUaCollectorOutcomeCode
{
    SessionEstablished,
    AlreadyConnected,
    ProfileNotPublished,
    ProfileInvalid,
    EndpointUnreachable,
    EndpointNotOffered,
    IdentityModeNotOffered,
    ServerCertificateUntrusted,
    ServerCertificateInvalid,
    SecretUnavailable,
    IdentityRejected,
    SessionCreateFailed,
    Cancelled
}

/// <summary>Execution status of one source operation in this build and attempt.</summary>
public enum OpcUaCollectorOperationStatus
{
    Executed,
    Refused,
    NotExecuted
}

/// <summary>One operation-level capability fact. Session success never implies the others.</summary>
public sealed record OpcUaCollectorOperationFact(
    string Operation,
    OpcUaCollectorOperationStatus Status,
    string Evidence);

/// <summary>Operation names reported in every receipt.</summary>
public static class OpcUaCollectorOperations
{
    public const string Session = "session";
    public const string Browse = "browse";
    public const string BoundedRead = "boundedRead";
    public const string Subscribe = "subscribe";
}

/// <summary>
/// Operation-level receipt of one connect attempt: requested facts from the authored
/// profile, effective facts from the established session, and the typed outcome.
/// Carries no credential and no user name.
/// </summary>
public sealed record OpcUaCollectorSessionReceipt(
    string SourceProfileId,
    string ConfigurationVersion,
    DateTimeOffset ObservedAtUtc,
    OpcUaCollectorOutcomeCode Outcome,
    string? SourceStatusCode,
    string Detail,
    string? RequestedEndpointUrl,
    string? RequestedSecurityPolicyUri,
    OpcUaCollectorSecurityMode? RequestedSecurityMode,
    OpcUaCollectorIdentityMode? RequestedIdentityMode,
    uint? RequestedSessionTimeoutMs,
    string? EffectiveEndpointUrl,
    string? EffectiveSecurityPolicyUri,
    string? EffectiveSecurityMode,
    string? EffectiveUserTokenType,
    double? RevisedSessionTimeoutMs,
    int? KeepAliveIntervalMs,
    string? ServerApplicationUri,
    OpcUaCollectorCertificateFact? ServerCertificate,
    OpcUaCollectorCertificateFact? ClientCertificate,
    string? SessionId,
    IReadOnlyList<OpcUaCollectorOfferedEndpoint> OfferedEndpoints,
    IReadOnlyList<OpcUaCollectorOperationFact> Operations);