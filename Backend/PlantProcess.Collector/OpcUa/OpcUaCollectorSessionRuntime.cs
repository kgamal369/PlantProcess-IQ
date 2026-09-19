using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using PlantProcess.Collector.Secrets;

namespace PlantProcess.Collector.OpcUa;

/// <summary>
/// The collector OPC UA session runtime: endpoint selection, certificate trust, user
/// identity, session lifecycle and reconnect for one published source profile version.
///
/// Read-only law: this runtime exposes connect, disconnect and receipts. It holds the
/// stack session privately and publishes no write, method-call or node-management path.
/// Session success is one operation fact; browse, bounded read and subscribe stay
/// NotExecuted until the tasks that own them are delivered.
/// </summary>
public sealed class OpcUaCollectorSessionRuntime : IAsyncDisposable
{
    private readonly OpcUaCollectorApplication _application;
    private readonly IOpcUaCollectorSourceProfileProvider _profiles;
    private readonly ICollectorSecretResolver _secrets;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly object _gate = new();

    private ISession? _session;
    private SessionReconnectHandler? _reconnectHandler;
    private OpcUaCollectorSourceProfile? _boundProfile;
    private CertificateValidationEventHandler? _validationHandler;
    private uint _lastValidationErrorCode;
    private string? _lastRejectedThumbprint;
    private bool _disposed;

    public OpcUaCollectorSessionRuntime(
        OpcUaCollectorApplication application,
        IOpcUaCollectorSourceProfileProvider profiles,
        ICollectorSecretResolver secrets,
        string sourceProfileId,
        string configurationVersion)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationVersion);

        _application = application;
        _profiles = profiles;
        _secrets = secrets;
        SourceProfileId = sourceProfileId;
        ConfigurationVersion = configurationVersion;
        _logger = application.Telemetry.LoggerFactory.CreateLogger("PlantProcess.Collector.OpcUa.Session");

        _validationHandler = OnCertificateValidation;
        _application.Configuration.CertificateValidator.CertificateValidation += _validationHandler;
    }

    public string SourceProfileId { get; }

    public string ConfigurationVersion { get; }

    public OpcUaCollectorSessionState State { get; private set; } = OpcUaCollectorSessionState.Disconnected;

    public int ReconnectCount { get; private set; }

    public string? SessionId { get; private set; }

    public OpcUaCollectorSessionReceipt? LastReceipt { get; private set; }

    public async Task<OpcUaCollectorSessionReceipt> ConnectAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is not null && _session.Connected)
            {
                return Publish(BuildEstablishedReceipt(
                    OpcUaCollectorOutcomeCode.AlreadyConnected,
                    "A session for this source profile is already active. A second session is never opened.",
                    _boundProfile!,
                    Array.Empty<OpcUaCollectorOfferedEndpoint>()));
            }

            SetState(OpcUaCollectorSessionState.Connecting);
            ResetValidationObservation();

            OpcUaCollectorSourceProfile? profile = await _profiles
                .GetAsync(SourceProfileId, ConfigurationVersion, cancellationToken)
                .ConfigureAwait(false);

            if (profile is null ||
                !string.Equals(profile.SourceProfileId, SourceProfileId, StringComparison.Ordinal) ||
                !string.Equals(profile.ConfigurationVersion, ConfigurationVersion, StringComparison.Ordinal))
            {
                return Publish(Refusal(
                    OpcUaCollectorOutcomeCode.ProfileNotPublished,
                    "No source profile is published for this exact id and configuration version.",
                    null,
                    null));
            }

            IReadOnlyList<string> profileErrors = profile.Validate();
            if (profileErrors.Count > 0)
            {
                return Publish(Refusal(
                    OpcUaCollectorOutcomeCode.ProfileInvalid,
                    "The published source profile is not executable: " + string.Join(" ", profileErrors),
                    profile,
                    null));
            }

            OpcUaCollectorEndpointSelection selection;
            try
            {
                selection = await OpcUaCollectorEndpointSelector
                    .DiscoverAsync(_application.Configuration, profile, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Publish(Refusal(OpcUaCollectorOutcomeCode.Cancelled, "The connect attempt was cancelled.", profile, null));
            }
            catch (ServiceResultException discoveryError)
            {
                return Publish(Refusal(
                    ClassifyDiscovery(discoveryError.StatusCode),
                    "Endpoint discovery failed: " + discoveryError.Message,
                    profile,
                    OpcUaCollectorStatusNames.Describe(discoveryError.StatusCode)));
            }
            catch (Exception discoveryError) when (discoveryError is not OutOfMemoryException)
            {
                return Publish(Refusal(
                    OpcUaCollectorOutcomeCode.EndpointUnreachable,
                    "Endpoint discovery failed: " + discoveryError.Message,
                    profile,
                    null));
            }

            if (selection.Selected is null)
            {
                return Publish(Refusal(
                    OpcUaCollectorOutcomeCode.EndpointNotOffered,
                    "The server offers no endpoint with the requested security policy, message security mode and transport. " +
                    "No weaker endpoint is substituted.",
                    profile,
                    null,
                    selection.Offered));
            }

            if (!selection.IdentityModeOffered)
            {
                return Publish(Refusal(
                    OpcUaCollectorOutcomeCode.IdentityModeNotOffered,
                    "The selected endpoint does not offer the requested user identity mode.",
                    profile,
                    null,
                    selection.Offered));
            }

            CollectorUserNameSecret? secret = null;
            IUserIdentity identity;
            if (profile.IdentityMode == OpcUaCollectorIdentityMode.UserName)
            {
                secret = await _secrets
                    .ResolveUserNameAsync(profile.SecretReference!, cancellationToken)
                    .ConfigureAwait(false);

                if (secret is null)
                {
                    return Publish(Refusal(
                        OpcUaCollectorOutcomeCode.SecretUnavailable,
                        "The collector secret reference did not resolve. No session was attempted.",
                        profile,
                        null,
                        selection.Offered));
                }

                identity = new UserIdentity(secret.UserName, secret.Password);
            }
            else
            {
                identity = new UserIdentity();
            }

            try
            {
                var endpointConfiguration = EndpointConfiguration.Create(_application.Configuration);
                var endpoint = new ConfiguredEndpoint(null, selection.Selected, endpointConfiguration);
                var factory = new DefaultSessionFactory(_application.Telemetry);

                ISession session = await factory
                    .CreateAsync(
                        _application.Configuration,
                        endpoint,
                        false,
                        SessionName(profile),
                        profile.RequestedSessionTimeoutMs,
                        identity,
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (session is null || !session.Connected)
                {
                    SafeDispose(session);
                    return Publish(Refusal(
                        OpcUaCollectorOutcomeCode.SessionCreateFailed,
                        "The server did not return a connected session.",
                        profile,
                        null,
                        selection.Offered));
                }

                lock (_gate)
                {
                    _session = session;
                    _boundProfile = profile;
                    session.KeepAliveInterval = profile.KeepAliveIntervalMs;
                    session.KeepAlive -= OnKeepAlive;
                    session.KeepAlive += OnKeepAlive;
                    _reconnectHandler?.Dispose();
                    _reconnectHandler = new SessionReconnectHandler(
                        _application.Telemetry,
                        true,
                        profile.MaxReconnectPeriodMs);
                    SessionId = session.SessionId?.ToString();
                }

                SetState(OpcUaCollectorSessionState.Connected);
                _logger.LogInformation(
                    "Collector session established for source profile {SourceProfileId} version {ConfigurationVersion}.",
                    SourceProfileId,
                    ConfigurationVersion);

                return Publish(BuildEstablishedReceipt(
                    OpcUaCollectorOutcomeCode.SessionEstablished,
                    "A secure session was established. Browse, bounded read and subscribe remain not executable in this build.",
                    profile,
                    selection.Offered));
            }
            catch (OperationCanceledException)
            {
                return Publish(Refusal(OpcUaCollectorOutcomeCode.Cancelled, "The connect attempt was cancelled.", profile, null, selection.Offered));
            }
            catch (ServiceResultException sessionError)
            {
                return Publish(Refusal(
                    ClassifySession(sessionError.StatusCode),
                    "The session was refused: " + sessionError.Message,
                    profile,
                    OpcUaCollectorStatusNames.Describe(sessionError.StatusCode),
                    selection.Offered));
            }
            catch (Exception sessionError) when (sessionError is not OutOfMemoryException)
            {
                return Publish(Refusal(
                    OpcUaCollectorOutcomeCode.SessionCreateFailed,
                    "The session could not be created: " + sessionError.Message,
                    profile,
                    null,
                    selection.Offered));
            }
            finally
            {
                secret?.Dispose();
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ISession? session;
            lock (_gate)
            {
                _reconnectHandler?.Dispose();
                _reconnectHandler = null;
                session = _session;
                _session = null;
                SessionId = null;
            }

            if (session is not null)
            {
                session.KeepAlive -= OnKeepAlive;
                try
                {
                    await session.CloseAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception closeError) when (closeError is not OutOfMemoryException)
                {
                    _logger.LogWarning("Collector session close reported {Reason}.", closeError.Message);
                }
                finally
                {
                    SafeDispose(session);
                }
            }

            SetState(OpcUaCollectorSessionState.Closed);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception disconnectError) when (disconnectError is not OutOfMemoryException)
        {
            _logger.LogWarning("Collector session disposal reported {Reason}.", disconnectError.Message);
        }

        if (_validationHandler is not null)
        {
            _application.Configuration.CertificateValidator.CertificateValidation -= _validationHandler;
            _validationHandler = null;
        }

        _sync.Dispose();
        _disposed = true;
    }

    private static string SessionName(OpcUaCollectorSourceProfile profile)
        => "ppiq-collector:" + profile.SourceProfileId + ":" + profile.ConfigurationVersion;

    private void OnCertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        // Trust is granted by the operator through the collector trust store, never here.
        e.Accept = false;
        e.AcceptAll = false;

        lock (_gate)
        {
            _lastValidationErrorCode = e.Error?.StatusCode.Code ?? 0;
            _lastRejectedThumbprint = e.Certificate?.Thumbprint;
        }

        _logger.LogWarning(
            "Server certificate refused. Thumbprint {Thumbprint}, status {Status}.",
            e.Certificate?.Thumbprint,
            OpcUaCollectorStatusNames.Describe(e.Error?.StatusCode.Code ?? 0));
    }

    private void ResetValidationObservation()
    {
        lock (_gate)
        {
            _lastValidationErrorCode = 0;
            _lastRejectedThumbprint = null;
        }
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (!ServiceResult.IsBad(e.Status))
        {
            return;
        }

        SessionReconnectHandler? handler;
        OpcUaCollectorSourceProfile? profile;
        lock (_gate)
        {
            if (!ReferenceEquals(session, _session))
            {
                return;
            }

            handler = _reconnectHandler;
            profile = _boundProfile;
        }

        if (handler is null || profile is null)
        {
            return;
        }

        SessionReconnectHandler.ReconnectState state = handler.BeginReconnect(
            session,
            profile.ReconnectPeriodMs,
            OnReconnectComplete);

        if (state == SessionReconnectHandler.ReconnectState.Triggered)
        {
            SetState(OpcUaCollectorSessionState.Reconnecting);
            _logger.LogWarning(
                "Collector session keep alive reported {Status}; reconnect triggered.",
                OpcUaCollectorStatusNames.Describe(e.Status?.StatusCode.Code ?? 0));
        }

        e.CancelKeepAlive = true;
    }

    private void OnReconnectComplete(object? sender, EventArgs e)
    {
        ISession? discarded = null;

        lock (_gate)
        {
            if (!ReferenceEquals(sender, _reconnectHandler) || _reconnectHandler is null)
            {
                return;
            }

            ISession? recovered = _reconnectHandler.Session;
            if (recovered is not null && !ReferenceEquals(recovered, _session))
            {
                discarded = _session;
                _session = recovered;
                recovered.KeepAlive -= OnKeepAlive;
                recovered.KeepAlive += OnKeepAlive;
            }

            if (_session is null)
            {
                return;
            }

            SessionId = _session.SessionId?.ToString();
            ReconnectCount++;
        }

        if (discarded is not null)
        {
            discarded.KeepAlive -= OnKeepAlive;
            SafeDispose(discarded);
        }

        SetState(OpcUaCollectorSessionState.Connected);
        _logger.LogInformation("Collector session recovered after reconnect {ReconnectCount}.", ReconnectCount);
    }

    private static void SafeDispose(ISession? session)
    {
        try
        {
            (session as IDisposable)?.Dispose();
        }
        catch (Exception disposeError) when (disposeError is not OutOfMemoryException)
        {
            // A failed dispose never changes the recorded outcome.
        }
    }

    private void SetState(OpcUaCollectorSessionState state)
    {
        State = state;
    }

    private OpcUaCollectorOutcomeCode ClassifyDiscovery(uint statusCode)
    {
        OpcUaCollectorOutcomeCode? fromValidation = ClassifyFromValidation();
        if (fromValidation is not null)
        {
            return fromValidation.Value;
        }

        string? name = OpcUaCollectorStatusNames.NameOf(statusCode);
        if (name is not null && name.StartsWith("BadCertificate", StringComparison.Ordinal))
        {
            return OpcUaCollectorOutcomeCode.ServerCertificateInvalid;
        }

        return OpcUaCollectorOutcomeCode.EndpointUnreachable;
    }

    private OpcUaCollectorOutcomeCode ClassifySession(uint statusCode)
    {
        OpcUaCollectorOutcomeCode? fromValidation = ClassifyFromValidation();
        if (fromValidation is not null)
        {
            return fromValidation.Value;
        }

        string? name = OpcUaCollectorStatusNames.NameOf(statusCode);
        if (name is null)
        {
            return OpcUaCollectorOutcomeCode.SessionCreateFailed;
        }

        if (name.StartsWith("BadCertificate", StringComparison.Ordinal))
        {
            return OpcUaCollectorOutcomeCode.ServerCertificateInvalid;
        }

        if (name.StartsWith("BadUser", StringComparison.Ordinal) ||
            name.StartsWith("BadIdentityToken", StringComparison.Ordinal))
        {
            return OpcUaCollectorOutcomeCode.IdentityRejected;
        }

        if (name.StartsWith("BadNotConnected", StringComparison.Ordinal) ||
            name.StartsWith("BadNoCommunication", StringComparison.Ordinal) ||
            name.StartsWith("BadConnection", StringComparison.Ordinal) ||
            name.StartsWith("BadTcp", StringComparison.Ordinal) ||
            name.StartsWith("BadHost", StringComparison.Ordinal) ||
            name.StartsWith("BadTimeout", StringComparison.Ordinal))
        {
            return OpcUaCollectorOutcomeCode.EndpointUnreachable;
        }

        return OpcUaCollectorOutcomeCode.SessionCreateFailed;
    }

    private OpcUaCollectorOutcomeCode? ClassifyFromValidation()
    {
        uint code;
        string? thumbprint;
        lock (_gate)
        {
            code = _lastValidationErrorCode;
            thumbprint = _lastRejectedThumbprint;
        }

        if (thumbprint is null)
        {
            return null;
        }

        if (code == StatusCodes.BadCertificateUntrusted || code == StatusCodes.BadCertificateChainIncomplete)
        {
            return OpcUaCollectorOutcomeCode.ServerCertificateUntrusted;
        }

        return OpcUaCollectorOutcomeCode.ServerCertificateInvalid;
    }

    private static IReadOnlyList<OpcUaCollectorOperationFact> Operations(OpcUaCollectorOperationStatus sessionStatus, string sessionEvidence)
    {
        const string NotExecutableEvidence =
            "Not executable in this collector build. A session fact never implies this operation.";

        return new[]
        {
            new OpcUaCollectorOperationFact(OpcUaCollectorOperations.Session, sessionStatus, sessionEvidence),
            new OpcUaCollectorOperationFact(OpcUaCollectorOperations.Browse, OpcUaCollectorOperationStatus.NotExecuted, NotExecutableEvidence),
            new OpcUaCollectorOperationFact(OpcUaCollectorOperations.BoundedRead, OpcUaCollectorOperationStatus.NotExecuted, NotExecutableEvidence),
            new OpcUaCollectorOperationFact(OpcUaCollectorOperations.Subscribe, OpcUaCollectorOperationStatus.NotExecuted, NotExecutableEvidence)
        };
    }

    private OpcUaCollectorSessionReceipt Refusal(
        OpcUaCollectorOutcomeCode outcome,
        string detail,
        OpcUaCollectorSourceProfile? profile,
        string? sourceStatusCode,
        IReadOnlyList<OpcUaCollectorOfferedEndpoint>? offered = null)
    {
        SetState(outcome == OpcUaCollectorOutcomeCode.EndpointUnreachable ||
                 outcome == OpcUaCollectorOutcomeCode.SessionCreateFailed
            ? OpcUaCollectorSessionState.Faulted
            : OpcUaCollectorSessionState.Refused);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new OpcUaCollectorSessionReceipt(
            SourceProfileId,
            ConfigurationVersion,
            now,
            outcome,
            sourceStatusCode,
            detail,
            profile?.EndpointUrl,
            profile?.SecurityPolicyUri,
            profile?.SecurityMode,
            profile?.IdentityMode,
            profile?.RequestedSessionTimeoutMs,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _application.ApplicationCertificate(now),
            null,
            offered ?? Array.Empty<OpcUaCollectorOfferedEndpoint>(),
            Operations(OpcUaCollectorOperationStatus.Refused, detail));
    }

    private OpcUaCollectorSessionReceipt BuildEstablishedReceipt(
        OpcUaCollectorOutcomeCode outcome,
        string detail,
        OpcUaCollectorSourceProfile profile,
        IReadOnlyList<OpcUaCollectorOfferedEndpoint> offered)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ISession? session;
        lock (_gate)
        {
            session = _session;
        }

        EndpointDescription? description = session?.ConfiguredEndpoint?.Description;
        OpcUaCollectorCertificateFact? serverCertificate = null;

        if (description?.ServerCertificate is { Length: > 0 } blob)
        {
            try
            {
                using X509Certificate2 parsed = Opc.Ua.Utils.ParseCertificateBlob(blob, _application.Telemetry);
                serverCertificate = OpcUaCollectorCertificateFact.From(
                    parsed,
                    now,
                    OpcUaCollectorCertificateFact.DefaultExpiringWindow);
            }
            catch (Exception parseError) when (parseError is not OutOfMemoryException)
            {
                _logger.LogWarning("The server certificate blob could not be parsed: {Reason}.", parseError.Message);
            }
        }

        return new OpcUaCollectorSessionReceipt(
            SourceProfileId,
            ConfigurationVersion,
            now,
            outcome,
            null,
            detail,
            profile.EndpointUrl,
            profile.SecurityPolicyUri,
            profile.SecurityMode,
            profile.IdentityMode,
            profile.RequestedSessionTimeoutMs,
            description?.EndpointUrl,
            description?.SecurityPolicyUri,
            description?.SecurityMode.ToString(),
            session?.Identity?.TokenType.ToString(),
            session?.SessionTimeout,
            session?.KeepAliveInterval,
            description?.Server?.ApplicationUri,
            serverCertificate,
            _application.ApplicationCertificate(now),
            session?.SessionId?.ToString(),
            offered,
            Operations(OpcUaCollectorOperationStatus.Executed, detail));
    }

    private OpcUaCollectorSessionReceipt Publish(OpcUaCollectorSessionReceipt receipt)
    {
        LastReceipt = receipt;
        _logger.LogInformation(
            "Collector connect attempt for {SourceProfileId} version {ConfigurationVersion} reported {Outcome} at {ObservedAtUtc}.",
            receipt.SourceProfileId,
            receipt.ConfigurationVersion,
            receipt.Outcome,
            receipt.ObservedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        return receipt;
    }
}