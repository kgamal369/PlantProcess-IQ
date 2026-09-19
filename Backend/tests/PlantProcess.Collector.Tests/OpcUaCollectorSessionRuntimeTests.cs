using Opc.Ua;
using PlantProcess.Collector.OpcUa;
using PlantProcess.Collector.Tests.TestSupport;

namespace PlantProcess.Collector.Tests;

/// <summary>
/// Session acceptance against a real SDK server started in process. Every fact here is a
/// test-server fact. None of it is site certification for a customer plant.
/// </summary>
[Trait("Gate", "CollectorSessionAcceptance")]
public sealed class OpcUaCollectorSessionRuntimeTests
{
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ReconnectWait = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task Trusted_server_yields_a_session_and_a_receipt_carrying_requested_and_effective_facts()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        OpcUaCollectorSourceProfile profile = harness.Profile();
        OpcUaCollectorSessionRuntime runtime = harness.Runtime(profile);

        OpcUaCollectorSessionReceipt receipt = await runtime.ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, receipt.Outcome);
        Assert.Equal(OpcUaCollectorSessionState.Connected, runtime.State);
        Assert.False(string.IsNullOrWhiteSpace(receipt.SessionId));

        Assert.Equal(profile.SecurityPolicyUri, receipt.RequestedSecurityPolicyUri);
        Assert.Equal(profile.SecurityPolicyUri, receipt.EffectiveSecurityPolicyUri);
        Assert.Equal(MessageSecurityMode.SignAndEncrypt.ToString(), receipt.EffectiveSecurityMode);
        Assert.Equal(harness.Server.ApplicationUri, receipt.ServerApplicationUri);
        Assert.Equal(UserTokenType.Anonymous.ToString(), receipt.EffectiveUserTokenType);
        Assert.NotNull(receipt.RevisedSessionTimeoutMs);
        Assert.True(receipt.RevisedSessionTimeoutMs > 0);
        Assert.Equal(profile.KeepAliveIntervalMs, receipt.KeepAliveIntervalMs);

        Assert.NotNull(receipt.ServerCertificate);
        Assert.Equal(harness.Server.Certificate.Thumbprint, receipt.ServerCertificate!.Thumbprint);
        Assert.NotNull(receipt.ClientCertificate);
        Assert.Equal(1, harness.Server.SessionCount);
    }

    [Fact]
    public async Task Session_success_never_marks_browse_read_or_subscribe_as_executed()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        OpcUaCollectorSessionRuntime runtime = harness.Runtime(harness.Profile());

        OpcUaCollectorSessionReceipt receipt = await runtime.ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, receipt.Outcome);
        Assert.Equal(
            OpcUaCollectorOperationStatus.Executed,
            receipt.Operations.Single(fact => fact.Operation == OpcUaCollectorOperations.Session).Status);

        foreach (string operation in new[]
        {
            OpcUaCollectorOperations.Browse,
            OpcUaCollectorOperations.BoundedRead,
            OpcUaCollectorOperations.Subscribe
        })
        {
            Assert.Equal(
                OpcUaCollectorOperationStatus.NotExecuted,
                receipt.Operations.Single(fact => fact.Operation == operation).Status);
        }
    }

    [Fact]
    public async Task A_second_connect_never_opens_a_second_session()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        OpcUaCollectorSessionRuntime runtime = harness.Runtime(harness.Profile());

        OpcUaCollectorSessionReceipt first = await runtime.ConnectAsync(CancellationToken.None);
        OpcUaCollectorSessionReceipt second = await runtime.ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, first.Outcome);
        Assert.Equal(OpcUaCollectorOutcomeCode.AlreadyConnected, second.Outcome);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(1, harness.Server.SessionCount);
    }

    [Fact]
    public async Task Untrusted_server_certificate_is_refused_and_lands_in_the_rejected_store()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(false);
        OpcUaCollectorSessionRuntime runtime = harness.Runtime(harness.Profile());

        OpcUaCollectorSessionReceipt receipt = await runtime.ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.ServerCertificateUntrusted, receipt.Outcome);
        Assert.Equal(OpcUaCollectorSessionState.Refused, runtime.State);
        Assert.Null(receipt.SessionId);
        Assert.Equal(0, harness.Server.SessionCount);

        string thumbprint = harness.Server.Certificate.Thumbprint;
        bool rejectedRecorded = await CollectorTestHarness.WaitUntilAsync(
            async () =>
            {
                IReadOnlyList<OpcUaCollectorCertificateFact> rejected =
                    await harness.Application.TrustStore.ListRejectedAsync(CancellationToken.None);
                return rejected.Any(fact => string.Equals(fact.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));
            },
            ShortWait);

        Assert.True(rejectedRecorded, "The refused server certificate must be recorded in the rejected store.");
    }

    [Fact]
    public async Task Trusting_a_rejected_certificate_recovers_the_session_and_revoking_it_refuses_again()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(false);
        string thumbprint = harness.Server.Certificate.Thumbprint;

        OpcUaCollectorSessionReceipt refused = await harness.Runtime(harness.Profile()).ConnectAsync(CancellationToken.None);
        Assert.Equal(OpcUaCollectorOutcomeCode.ServerCertificateUntrusted, refused.Outcome);

        await CollectorTestHarness.WaitUntilAsync(
            async () =>
            {
                IReadOnlyList<OpcUaCollectorCertificateFact> rejected =
                    await harness.Application.TrustStore.ListRejectedAsync(CancellationToken.None);
                return rejected.Any(fact => string.Equals(fact.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));
            },
            ShortWait);

        bool trusted = await harness.Application.TrustStore.TrustRejectedAsync(thumbprint, CancellationToken.None);
        Assert.True(trusted, "The rejected certificate must be available for an explicit trust decision.");

        OpcUaCollectorSessionRuntime afterTrust = harness.Runtime(harness.Profile());
        OpcUaCollectorSessionReceipt established = await afterTrust.ConnectAsync(CancellationToken.None);
        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, established.Outcome);
        await afterTrust.DisconnectAsync(CancellationToken.None);

        bool revoked = await harness.Application.TrustStore.RevokeTrustAsync(thumbprint, CancellationToken.None);
        Assert.True(revoked, "Trust must be revocable.");

        OpcUaCollectorSessionReceipt afterRevoke = await harness.Runtime(harness.Profile()).ConnectAsync(CancellationToken.None);
        Assert.Equal(OpcUaCollectorOutcomeCode.ServerCertificateUntrusted, afterRevoke.Outcome);
    }

    [Fact]
    public async Task A_rotated_server_certificate_is_refused_until_the_new_one_is_trusted()
    {
        int port = SdkTestServerHost.GetFreePort();
        await using CollectorTestHarness first = await CollectorTestHarness.StartAsync(true, port);

        OpcUaCollectorSessionRuntime before = first.Runtime(first.Profile());
        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, (await before.ConnectAsync(CancellationToken.None)).Outcome);
        await before.DisconnectAsync(CancellationToken.None);
        await first.Server.StopAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));

        string rotatedPki = Path.Combine(first.Root, "rotated-server-pki");
        await using SdkTestServerHost rotated = await SdkTestServerHost.StartAsync(rotatedPki, port, first.Telemetry);

        Assert.NotEqual(first.Server.Certificate.Thumbprint, rotated.Certificate.Thumbprint);

        OpcUaCollectorSessionRuntime afterRotation = first.Runtime(first.Profile("v1"));
        OpcUaCollectorSessionReceipt refused = await afterRotation.ConnectAsync(CancellationToken.None);
        Assert.Equal(OpcUaCollectorOutcomeCode.ServerCertificateUntrusted, refused.Outcome);

        await first.Application.TrustStore.TrustCertificateAsync(rotated.Certificate, CancellationToken.None);

        OpcUaCollectorSessionRuntime afterTrust = first.Runtime(first.Profile("v1"));
        OpcUaCollectorSessionReceipt established = await afterTrust.ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, established.Outcome);
        Assert.Equal(rotated.Certificate.Thumbprint, established.ServerCertificate!.Thumbprint);
    }

    [Fact]
    public async Task A_server_restart_is_recovered_by_reconnect()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        OpcUaCollectorSessionRuntime runtime = harness.Runtime(harness.Profile());

        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, (await runtime.ConnectAsync(CancellationToken.None)).Outcome);

        await harness.Server.StopAsync();

        bool reconnecting = await CollectorTestHarness.WaitUntilAsync(
            () => runtime.State == OpcUaCollectorSessionState.Reconnecting,
            ShortWait);
        Assert.True(reconnecting, "A stopped server must move the runtime into the reconnecting state.");

        await harness.Server.RestartAsync();

        bool recovered = await CollectorTestHarness.WaitUntilAsync(
            () => runtime.State == OpcUaCollectorSessionState.Connected && runtime.ReconnectCount > 0,
            ReconnectWait);

        Assert.True(recovered, "The runtime must recover its session after the server returns.");
        Assert.False(string.IsNullOrWhiteSpace(runtime.SessionId));
    }

    [Fact]
    public async Task Disconnect_closes_the_session_and_the_runtime_can_connect_again()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        OpcUaCollectorSessionRuntime runtime = harness.Runtime(harness.Profile());

        await runtime.ConnectAsync(CancellationToken.None);
        await runtime.DisconnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorSessionState.Closed, runtime.State);
        Assert.Null(runtime.SessionId);

        bool closed = await CollectorTestHarness.WaitUntilAsync(() => harness.Server.SessionCount == 0, ShortWait);
        Assert.True(closed, "A disconnect must close the server-side session.");

        OpcUaCollectorSessionReceipt again = await runtime.ConnectAsync(CancellationToken.None);
        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, again.Outcome);
    }

    [Fact]
    public async Task A_user_name_identity_is_accepted_and_the_credential_never_reaches_a_log_or_a_receipt()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        harness.Secrets.Register(
            "collector/plant-historian-1",
            CredentialCheckingTestServer.AcceptedUser,
            CredentialCheckingTestServer.AcceptedPassword);

        OpcUaCollectorSourceProfile profile = harness.Profile(
            identityMode: OpcUaCollectorIdentityMode.UserName,
            secretReference: "collector/plant-historian-1");

        OpcUaCollectorSessionReceipt receipt = await harness.Runtime(profile).ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, receipt.Outcome);
        Assert.Equal(UserTokenType.UserName.ToString(), receipt.EffectiveUserTokenType);

        string rendered = receipt.ToString();
        Assert.DoesNotContain(CredentialCheckingTestServer.AcceptedPassword, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(CredentialCheckingTestServer.AcceptedUser, rendered, StringComparison.Ordinal);

        foreach (string line in harness.Logs.Lines)
        {
            Assert.DoesNotContain(CredentialCheckingTestServer.AcceptedPassword, line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_wrong_user_name_credential_is_rejected_by_the_server()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        harness.Secrets.Register("collector/plant-historian-1", CredentialCheckingTestServer.AcceptedUser, "not-the-password");

        OpcUaCollectorSourceProfile profile = harness.Profile(
            identityMode: OpcUaCollectorIdentityMode.UserName,
            secretReference: "collector/plant-historian-1");

        OpcUaCollectorSessionReceipt receipt = await harness.Runtime(profile).ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.IdentityRejected, receipt.Outcome);
        Assert.Null(receipt.SessionId);
    }

    [Fact]
    public async Task An_unresolvable_secret_reference_is_refused_before_any_session_is_attempted()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);

        OpcUaCollectorSourceProfile profile = harness.Profile(
            identityMode: OpcUaCollectorIdentityMode.UserName,
            secretReference: "collector/missing");

        OpcUaCollectorSessionReceipt receipt = await harness.Runtime(profile).ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.SecretUnavailable, receipt.Outcome);
        Assert.Equal(0, harness.Server.SessionCount);
    }

    [Fact]
    public async Task An_endpoint_the_server_does_not_offer_is_refused_without_any_downgrade()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);

        OpcUaCollectorSourceProfile profile = harness.Profile(
            securityPolicyUri: SecurityPolicies.Aes256_Sha256_RsaPss);

        OpcUaCollectorSessionReceipt receipt = await harness.Runtime(profile).ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.EndpointNotOffered, receipt.Outcome);
        Assert.Null(receipt.EffectiveSecurityPolicyUri);
        Assert.Equal(0, harness.Server.SessionCount);
        Assert.NotEmpty(receipt.OfferedEndpoints);
        Assert.All(
            receipt.OfferedEndpoints,
            offered => Assert.NotEqual(SecurityPolicies.Aes256_Sha256_RsaPss, offered.SecurityPolicyUri));
    }

    [Fact]
    public async Task An_unsecured_endpoint_is_refused_when_the_server_offers_none()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);

        OpcUaCollectorSourceProfile profile = harness.Profile(
            securityPolicyUri: SecurityPolicies.None,
            securityMode: OpcUaCollectorSecurityMode.None);

        OpcUaCollectorSessionReceipt receipt = await harness.Runtime(profile).ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.EndpointNotOffered, receipt.Outcome);
        Assert.Equal(0, harness.Server.SessionCount);
    }

    [Fact]
    public async Task An_unpublished_configuration_version_is_refused()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        harness.Profile("v1");

        var runtime = new OpcUaCollectorSessionRuntime(
            harness.Application,
            harness.Profiles,
            harness.Secrets,
            "plant-historian-1",
            "v2");
        harness.Track(runtime);

        OpcUaCollectorSessionReceipt receipt = await runtime.ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.ProfileNotPublished, receipt.Outcome);
        Assert.Equal(0, harness.Server.SessionCount);
    }

    [Fact]
    public async Task An_unreachable_endpoint_is_reported_as_unreachable()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        int closedPort = SdkTestServerHost.GetFreePort();

        OpcUaCollectorSourceProfile profile = harness.Profile(
            endpointUrl: "opc.tcp://localhost:" + closedPort + "/PpiqCollectorSdkTestServer");

        OpcUaCollectorSessionRuntime runtime = harness.Runtime(profile);
        OpcUaCollectorSessionReceipt receipt = await runtime.ConnectAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorOutcomeCode.EndpointUnreachable, receipt.Outcome);
        Assert.Equal(OpcUaCollectorSessionState.Faulted, runtime.State);
        Assert.Null(receipt.SessionId);
    }
}