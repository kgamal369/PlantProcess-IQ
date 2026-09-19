using Microsoft.Extensions.Logging;
using Opc.Ua;
using PlantProcess.Collector.OpcUa;

namespace PlantProcess.Collector.Tests.TestSupport;

/// <summary>
/// One isolated acceptance environment: its own PKI roots, its own free port, its own
/// in-process SDK test server and its own collector client application.
/// </summary>
internal sealed class CollectorTestHarness : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _disposables = new();

    private CollectorTestHarness(
        string root,
        ITelemetryContext telemetry,
        CapturingLoggerProvider logs,
        SdkTestServerHost server,
        OpcUaCollectorApplication application)
    {
        Root = root;
        Telemetry = telemetry;
        Logs = logs;
        Server = server;
        Application = application;
    }

    internal string Root { get; }

    internal ITelemetryContext Telemetry { get; }

    internal CapturingLoggerProvider Logs { get; }

    internal SdkTestServerHost Server { get; }

    internal OpcUaCollectorApplication Application { get; }

    internal InMemorySourceProfiles Profiles { get; } = new();

    internal InMemorySecrets Secrets { get; } = new();

    internal static async Task<CollectorTestHarness> StartAsync(
        bool trustServerCertificate,
        int? port = null,
        bool withFillerNamespace = false)
    {
        string root = Path.Combine(Path.GetTempPath(), "ppiq-collector-acceptance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var logs = new CapturingLoggerProvider();
        ITelemetryContext telemetry = DefaultTelemetry.Create(builder =>
            builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));

        SdkTestServerHost server = await SdkTestServerHost
            .StartAsync(
                Path.Combine(root, "server-pki"),
                port ?? SdkTestServerHost.GetFreePort(),
                telemetry,
                withFillerNamespace)
            .ConfigureAwait(false);

        OpcUaCollectorApplication application = await OpcUaCollectorApplicationFactory
            .CreateAsync(ClientOptions(root), telemetry, CancellationToken.None)
            .ConfigureAwait(false);

        var harness = new CollectorTestHarness(root, telemetry, logs, server, application);

        if (trustServerCertificate)
        {
            await application.TrustStore
                .TrustCertificateAsync(server.Certificate, CancellationToken.None)
                .ConfigureAwait(false);
        }

        return harness;
    }

    internal static OpcUaCollectorApplicationOptions ClientOptions(string root)
        => new(
            "PpiqCollectorUnderTest",
            "urn:localhost:PpiqCollectorUnderTest",
            "uri:plantprocessiq:test:collector",
            Path.Combine(root, "client-pki"),
            "CN=PpiqCollectorUnderTest, O=PlantProcess IQ Test, DC=localhost");

    internal OpcUaCollectorSourceProfile Profile(
        string version = "v1",
        OpcUaCollectorIdentityMode identityMode = OpcUaCollectorIdentityMode.Anonymous,
        string? secretReference = null,
        string? securityPolicyUri = null,
        OpcUaCollectorSecurityMode securityMode = OpcUaCollectorSecurityMode.SignAndEncrypt,
        string? endpointUrl = null,
        int keepAliveIntervalMs = 1000,
        int reconnectPeriodMs = 1000,
        int maxReconnectPeriodMs = 5000)
    {
        var profile = new OpcUaCollectorSourceProfile(
            "plant-historian-1",
            version,
            endpointUrl ?? Server.EndpointUrl,
            securityPolicyUri ?? SecurityPolicies.Basic256Sha256,
            securityMode,
            identityMode,
            secretReference,
            60000,
            keepAliveIntervalMs,
            reconnectPeriodMs,
            maxReconnectPeriodMs);

        Profiles.Publish(profile);
        return profile;
    }

    internal OpcUaCollectorSessionRuntime Runtime(OpcUaCollectorSourceProfile profile)
    {
        var runtime = new OpcUaCollectorSessionRuntime(
            Application,
            Profiles,
            Secrets,
            profile.SourceProfileId,
            profile.ConfigurationVersion);
        _disposables.Add(runtime);
        return runtime;
    }

    internal void Track(IAsyncDisposable disposable) => _disposables.Add(disposable);

    internal static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
        }

        return condition();
    }

    internal static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition().ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
        }

        return await condition().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (IAsyncDisposable disposable in _disposables)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception disposeError) when (disposeError is not OutOfMemoryException)
            {
                // Cleanup never changes a test verdict.
            }
        }

        await Server.DisposeAsync().ConfigureAwait(false);

        try
        {
            Directory.Delete(Root, true);
        }
        catch (Exception deleteError) when (deleteError is not OutOfMemoryException)
        {
            // A locked temporary directory never changes a test verdict.
        }
    }
}