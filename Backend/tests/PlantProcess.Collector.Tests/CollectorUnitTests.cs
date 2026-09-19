using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Opc.Ua;
using PlantProcess.Collector.OpcUa;
using PlantProcess.Collector.Secrets;

namespace PlantProcess.Collector.Tests;

[Trait("Gate", "CollectorContracts")]
public sealed class CollectorUnitTests
{
    private static OpcUaCollectorSourceProfile Valid(
        OpcUaCollectorIdentityMode identityMode = OpcUaCollectorIdentityMode.Anonymous,
        string? secretReference = null,
        string? endpointUrl = null,
        string? policy = null,
        OpcUaCollectorSecurityMode mode = OpcUaCollectorSecurityMode.SignAndEncrypt,
        int keepAlive = 5000)
        => new(
            "plant-historian-1",
            "v1",
            endpointUrl ?? "opc.tcp://plant-historian:4840/Server",
            policy ?? SecurityPolicies.Basic256Sha256,
            mode,
            identityMode,
            secretReference,
            60000,
            keepAlive);

    [Fact]
    public void A_complete_profile_validates()
    {
        Assert.Empty(Valid().Validate());
    }

    [Fact]
    public void A_user_name_profile_without_a_secret_reference_is_refused()
    {
        IReadOnlyList<string> errors = Valid(OpcUaCollectorIdentityMode.UserName).Validate();

        Assert.Contains(errors, error => error.Contains("SecretReference", StringComparison.Ordinal));
    }

    [Fact]
    public void A_non_opc_tcp_endpoint_is_refused()
    {
        IReadOnlyList<string> errors = Valid(endpointUrl: "https://plant-historian/api").Validate();

        Assert.Contains(errors, error => error.Contains("opc.tcp", StringComparison.Ordinal));
    }

    [Fact]
    public void A_security_policy_and_mode_that_disagree_about_no_security_are_refused()
    {
        IReadOnlyList<string> errors = Valid(policy: SecurityPolicies.None).Validate();

        Assert.Contains(errors, error => error.Contains("None", StringComparison.Ordinal));
    }

    [Fact]
    public void An_out_of_range_keep_alive_interval_is_refused()
    {
        Assert.Contains(
            Valid(keepAlive: 120000).Validate(),
            error => error.Contains("KeepAliveIntervalMs", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-400, -10, OpcUaCollectorCertificateValidity.Expired)]
    [InlineData(10, 400, OpcUaCollectorCertificateValidity.NotYetValid)]
    [InlineData(-10, 10, OpcUaCollectorCertificateValidity.ExpiringSoon)]
    [InlineData(-10, 400, OpcUaCollectorCertificateValidity.Valid)]
    public void Certificate_validity_is_reported_honestly(int notBeforeDays, int notAfterDays, OpcUaCollectorCertificateValidity expected)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PpiqCollectorCertificateFactTest",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using X509Certificate2 certificate = request.CreateSelfSigned(
            now.AddDays(notBeforeDays),
            now.AddDays(notAfterDays));

        OpcUaCollectorCertificateFact fact = OpcUaCollectorCertificateFact.From(
            certificate,
            now,
            OpcUaCollectorCertificateFact.DefaultExpiringWindow);

        Assert.Equal(expected, fact.Validity);
        Assert.Equal(certificate.Thumbprint, fact.Thumbprint);
    }

    [Fact]
    public void A_resolved_secret_is_copied_cleared_on_dispose_and_never_rendered()
    {
        byte[] source = Encoding.UTF8.GetBytes("reader-secret-8431");
        var secret = new CollectorUserNameSecret("collector-reader", source);

        Array.Clear(source);
        Assert.Equal("reader-secret-8431", Encoding.UTF8.GetString(secret.Password));
        Assert.DoesNotContain("reader-secret-8431", secret.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("collector-reader", secret.ToString(), StringComparison.Ordinal);

        secret.Dispose();
        Assert.Throws<ObjectDisposedException>(() => ReadPasswordLength(secret));
    }

    [Fact]
    public void Endpoint_selection_refuses_a_weaker_endpoint_when_the_requested_one_is_absent()
    {
        var discoveryUri = new Uri("opc.tcp://plant-historian:4840/Server");
        var offered = new List<EndpointDescription>
        {
            Endpoint(discoveryUri, SecurityPolicies.None, MessageSecurityMode.None),
            Endpoint(discoveryUri, SecurityPolicies.Basic256Sha256, MessageSecurityMode.Sign)
        };

        OpcUaCollectorSourceProfile profile = Valid();

        OpcUaCollectorEndpointSelection selection = OpcUaCollectorEndpointSelector.Select(offered, profile, discoveryUri);

        Assert.Null(selection.Selected);
        Assert.Equal(2, selection.Offered.Count);
    }

    [Fact]
    public void Endpoint_selection_takes_the_exact_requested_policy_and_mode()
    {
        var discoveryUri = new Uri("opc.tcp://plant-historian:4840/Server");
        var offered = new List<EndpointDescription>
        {
            Endpoint(discoveryUri, SecurityPolicies.Basic256Sha256, MessageSecurityMode.Sign),
            Endpoint(discoveryUri, SecurityPolicies.Basic256Sha256, MessageSecurityMode.SignAndEncrypt)
        };

        OpcUaCollectorEndpointSelection selection = OpcUaCollectorEndpointSelector.Select(offered, Valid(), discoveryUri);

        Assert.NotNull(selection.Selected);
        Assert.Equal(MessageSecurityMode.SignAndEncrypt, selection.Selected!.SecurityMode);
        Assert.True(selection.IdentityModeOffered);
    }

    [Fact]
    public void Endpoint_selection_reports_an_identity_mode_the_endpoint_does_not_offer()
    {
        var discoveryUri = new Uri("opc.tcp://plant-historian:4840/Server");
        EndpointDescription anonymousOnly = Endpoint(
            discoveryUri,
            SecurityPolicies.Basic256Sha256,
            MessageSecurityMode.SignAndEncrypt);

        OpcUaCollectorSourceProfile profile = Valid(
            OpcUaCollectorIdentityMode.UserName,
            "collector/plant-historian-1");

        OpcUaCollectorEndpointSelection selection = OpcUaCollectorEndpointSelector.Select(
            new[] { anonymousOnly },
            profile,
            discoveryUri);

        Assert.NotNull(selection.Selected);
        Assert.False(selection.IdentityModeOffered);
    }

    private static int ReadPasswordLength(CollectorUserNameSecret secret) => secret.Password.Length;

    private static EndpointDescription Endpoint(Uri url, string policyUri, MessageSecurityMode mode)
    {
        var description = new EndpointDescription
        {
            EndpointUrl = url.ToString(),
            SecurityPolicyUri = policyUri,
            SecurityMode = mode,
            TransportProfileUri = Profiles.UaTcpTransport,
            SecurityLevel = mode == MessageSecurityMode.SignAndEncrypt ? (byte)40 : (byte)20,
            Server = new ApplicationDescription
            {
                ApplicationUri = "urn:plant-historian:Server",
                ApplicationType = ApplicationType.Server
            }
        };

        description.UserIdentityTokens.Add(new UserTokenPolicy(UserTokenType.Anonymous));
        return description;
    }
}