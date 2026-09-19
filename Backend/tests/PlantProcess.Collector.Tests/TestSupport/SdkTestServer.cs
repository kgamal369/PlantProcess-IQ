using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace PlantProcess.Collector.Tests.TestSupport;

/// <summary>
/// A real OPC UA server from the SDK, started in process for acceptance tests. It is a test
/// server, not a certified plant source, and nothing here is evidence of site certification.
/// </summary>
internal sealed class CredentialCheckingTestServer : StandardServer
{
    internal const string AcceptedUser = "collector-reader";
    internal const string AcceptedPassword = "reader-secret-8431";

    internal CredentialCheckingTestServer(bool withFillerNamespace = false)
    {
        SourceNodes = new PpiqSourceNodeManagerFactory(withFillerNamespace);
        AddNodeManager(SourceNodes);
    }

    /// <summary>The source address space of this test server.</summary>
    internal PpiqSourceNodeManagerFactory SourceNodes { get; }

    protected override void OnServerStarted(IServerInternal server)
    {
        base.OnServerStarted(server);
        server.SessionManager.ImpersonateUser += OnImpersonateUser;
    }

    private static void OnImpersonateUser(ISession session, ImpersonateEventArgs args)
    {
        if (args.NewIdentity is UserNameIdentityToken token)
        {
            byte[] supplied = token.DecryptedPassword ?? Array.Empty<byte>();
            byte[] expected = System.Text.Encoding.UTF8.GetBytes(AcceptedPassword);

            if (string.Equals(token.UserName, AcceptedUser, StringComparison.Ordinal) &&
                supplied.AsSpan().SequenceEqual(expected))
            {
                args.Identity = new RoleBasedIdentity(new UserIdentity(token), [Role.AuthenticatedUser]);
                return;
            }

            throw new ServiceResultException(StatusCodes.BadUserAccessDenied, "Invalid user name or password.");
        }

        if (args.NewIdentity is AnonymousIdentityToken or null)
        {
            args.Identity = new RoleBasedIdentity(new UserIdentity(), [Role.Anonymous]);
            return;
        }

        throw new ServiceResultException(StatusCodes.BadIdentityTokenInvalid, "Unsupported user token type.");
    }
}

internal sealed class SdkTestServerHost : IAsyncDisposable
{
    private const string ServerName = "PpiqCollectorSdkTestServer";

    private readonly ApplicationInstance _instance;
    private CredentialCheckingTestServer _server;
    private bool _running;

    private SdkTestServerHost(
        ApplicationInstance instance,
        CredentialCheckingTestServer server,
        ApplicationConfiguration configuration,
        int port)
    {
        _instance = instance;
        _server = server;
        Configuration = configuration;
        Port = port;
        _running = true;
    }

    internal ApplicationConfiguration Configuration { get; }

    internal int Port { get; }

    internal string EndpointUrl => "opc.tcp://localhost:" + Port + "/" + ServerName;

    internal string ApplicationUri => "urn:localhost:" + ServerName;

    internal X509Certificate2 Certificate
        => Configuration.SecurityConfiguration.ApplicationCertificates[0].Certificate
           ?? throw new InvalidOperationException("The test server certificate is not loaded.");

    internal int SessionCount => _server.CurrentInstance.SessionManager.GetSessions().Count;

    private bool _withFillerNamespace;

    /// <summary>The node manager of the test source, once the server has started.</summary>
    internal PpiqSourceNodeManager? SourceNodes => _server.SourceNodes.Instance;

    internal static async Task<SdkTestServerHost> StartAsync(
        string pkiRoot,
        int port,
        ITelemetryContext telemetry,
        bool withFillerNamespace = false)
    {
        Directory.CreateDirectory(pkiRoot);

        var instance = new ApplicationInstance(telemetry)
        {
            ApplicationName = ServerName,
            ApplicationType = ApplicationType.Server
        };

        string url = "opc.tcp://localhost:" + port + "/" + ServerName;

        var certificates = new CertificateIdentifierCollection
        {
            new CertificateIdentifier
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = Path.Combine(pkiRoot, "own"),
                SubjectName = "CN=" + ServerName + ", O=PlantProcess IQ Test, DC=localhost",
                CertificateType = ObjectTypeIds.RsaSha256ApplicationCertificateType
            }
        };

        ApplicationConfiguration configuration = await instance
            .Build("urn:localhost:" + ServerName, "uri:plantprocessiq:test:" + ServerName)
            .AsServer([url])
            .AddPolicy(MessageSecurityMode.SignAndEncrypt, SecurityPolicies.Basic256Sha256)
            .AddUserTokenPolicy(UserTokenType.Anonymous)
            .AddUserTokenPolicy(UserTokenType.UserName)
            .SetDiagnosticsEnabled(true)
            .AddSecurityConfiguration(certificates, pkiRoot)
            .SetAutoAcceptUntrustedCertificates(true)
            .CreateAsync()
            .ConfigureAwait(false);

        configuration.ServerConfiguration.BaseAddresses = new StringCollection { url };

        bool haveCertificate = await instance
            .CheckApplicationInstanceCertificatesAsync(true)
            .ConfigureAwait(false);

        if (!haveCertificate)
        {
            throw new InvalidOperationException("The test server certificate could not be created.");
        }

        var server = new CredentialCheckingTestServer(withFillerNamespace);
        await instance.StartAsync(server).ConfigureAwait(false);

        var host = new SdkTestServerHost(instance, server, configuration, port);
        host._withFillerNamespace = withFillerNamespace;
        return host;
    }

    internal async Task StopAsync()
    {
        if (!_running)
        {
            return;
        }

        await _server.StopAsync().ConfigureAwait(false);
        _running = false;
    }

    internal Task RestartAsync() => RestartAsync(_withFillerNamespace);

    /// <summary>
    /// Restarts the server, optionally with an extra namespace in front of the source
    /// namespace, so the source namespace lands on a different index.
    /// </summary>
    internal async Task RestartAsync(bool withFillerNamespace)
    {
        if (_running)
        {
            await StopAsync().ConfigureAwait(false);
        }

        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        _withFillerNamespace = withFillerNamespace;
        _server = new CredentialCheckingTestServer(withFillerNamespace);
        await _instance.StartAsync(_server).ConfigureAwait(false);
        _running = true;
    }

    internal static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        catch (Exception stopError) when (stopError is not OutOfMemoryException)
        {
            // A failed test-server stop never changes a test verdict.
        }
    }
}