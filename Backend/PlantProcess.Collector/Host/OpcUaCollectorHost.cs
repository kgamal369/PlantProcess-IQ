using Microsoft.Extensions.Logging;
using Opc.Ua;
using PlantProcess.Collector.OpcUa;
using PlantProcess.Collector.Secrets;

namespace PlantProcess.Collector.Host;

/// <summary>What one collector host run did.</summary>
public sealed record OpcUaCollectorHostRunResult(
    bool SessionEstablished,
    OpcUaCollectorSessionReceipt Receipt,
    int ReconnectCount,
    OpcUaCollectorSessionState FinalState);

/// <summary>
/// The minimal executable collector boundary: it starts, binds one published source
/// profile version, resolves its secret through the collector abstractions, establishes
/// or refuses a session, keeps the session alive with reconnect, and shuts down.
///
/// It owns no scheduler, no continuous acquisition loop, no lease and no core
/// dependency. Session ownership, fencing and microbatching belong to the task that
/// owns continuous acquisition.
/// </summary>
public sealed class OpcUaCollectorHost : IAsyncDisposable
{
    private readonly OpcUaCollectorApplication _application;
    private readonly OpcUaCollectorSessionRuntime _runtime;
    private readonly ILogger _logger;
    private bool _disposed;

    public OpcUaCollectorHost(
        OpcUaCollectorApplication application,
        IOpcUaCollectorSourceProfileProvider profiles,
        ICollectorSecretResolver secrets,
        string sourceProfileId,
        string configurationVersion)
    {
        ArgumentNullException.ThrowIfNull(application);
        _application = application;
        _runtime = new OpcUaCollectorSessionRuntime(application, profiles, secrets, sourceProfileId, configurationVersion);
        _logger = application.Telemetry.LoggerFactory.CreateLogger("PlantProcess.Collector.Host");
    }

    public OpcUaCollectorSessionState State => _runtime.State;

    public int ReconnectCount => _runtime.ReconnectCount;

    public OpcUaCollectorSessionReceipt? LastReceipt => _runtime.LastReceipt;

    /// <summary>Establishes the session once and returns the receipt. Never throws on refusal.</summary>
    public async Task<OpcUaCollectorHostRunResult> StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        OpcUaCollectorSessionReceipt receipt = await _runtime.ConnectAsync(cancellationToken).ConfigureAwait(false);
        bool established =
            receipt.Outcome is OpcUaCollectorOutcomeCode.SessionEstablished or OpcUaCollectorOutcomeCode.AlreadyConnected;

        _logger.LogInformation(
            "Collector host start reported {Outcome} in state {State}.",
            receipt.Outcome,
            _runtime.State);

        return new OpcUaCollectorHostRunResult(established, receipt, _runtime.ReconnectCount, _runtime.State);
    }

    /// <summary>
    /// Keeps the established session alive until the token is cancelled. Reconnect is handled
    /// by the session runtime; this loop only observes.
    /// </summary>
    public async Task RunUntilCancelledAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown is a normal outcome, not a fault.
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await _runtime.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Collector host stopped in state {State}.", _runtime.State);
    }

    /// <summary>The collector trust store, so an operator tool can inspect or update trust.</summary>
    public OpcUaCollectorTrustStore TrustStore => _application.TrustStore;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _runtime.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }

    /// <summary>Builds a host from collector options. Used by the executable entry point and by tests.</summary>
    public static async Task<OpcUaCollectorHost> CreateAsync(
        OpcUaCollectorApplicationOptions options,
        IOpcUaCollectorSourceProfileProvider profiles,
        ICollectorSecretResolver secrets,
        string sourceProfileId,
        string configurationVersion,
        ITelemetryContext telemetry,
        CancellationToken cancellationToken)
    {
        OpcUaCollectorApplication application = await OpcUaCollectorApplicationFactory
            .CreateAsync(options, telemetry, cancellationToken)
            .ConfigureAwait(false);

        return new OpcUaCollectorHost(application, profiles, secrets, sourceProfileId, configurationVersion);
    }
}