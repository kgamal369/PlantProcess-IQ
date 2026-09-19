using System.Text;
using System.Text.Json;
using PlantProcess.Collector.Bootstrap;
using PlantProcess.Collector.Host;
using PlantProcess.Collector.OpcUa;
using PlantProcess.Collector.Tests.TestSupport;

namespace PlantProcess.Collector.Tests;

/// <summary>
/// The executable collector boundary: it starts, resolves a published profile and its
/// secret through the collector abstractions, establishes or refuses a session and shuts
/// down. It carries no scheduler and no continuous acquisition loop.
/// </summary>
[Trait("Gate", "CollectorHostBoundary")]
public sealed class OpcUaCollectorHostTests
{
    [Fact]
    public async Task The_host_starts_establishes_a_session_and_shuts_down()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);
        OpcUaCollectorSourceProfile profile = harness.Profile();

        await using var host = new OpcUaCollectorHost(
            harness.Application,
            harness.Profiles,
            harness.Secrets,
            profile.SourceProfileId,
            profile.ConfigurationVersion);

        OpcUaCollectorHostRunResult result = await host.StartAsync(CancellationToken.None);

        Assert.True(result.SessionEstablished);
        Assert.Equal(OpcUaCollectorSessionState.Connected, host.State);
        Assert.Equal(1, harness.Server.SessionCount);

        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await host.RunUntilCancelledAsync(stopping.Token);
        await host.StopAsync(CancellationToken.None);

        Assert.Equal(OpcUaCollectorSessionState.Closed, host.State);
        bool closed = await CollectorTestHarness.WaitUntilAsync(
            () => harness.Server.SessionCount == 0,
            TimeSpan.FromSeconds(20));
        Assert.True(closed, "The host shutdown must close the server-side session.");
    }

    [Fact]
    public async Task The_host_reports_a_refusal_without_throwing_and_still_shuts_down()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(false);
        OpcUaCollectorSourceProfile profile = harness.Profile();

        await using var host = new OpcUaCollectorHost(
            harness.Application,
            harness.Profiles,
            harness.Secrets,
            profile.SourceProfileId,
            profile.ConfigurationVersion);

        OpcUaCollectorHostRunResult result = await host.StartAsync(CancellationToken.None);

        Assert.False(result.SessionEstablished);
        Assert.Equal(OpcUaCollectorOutcomeCode.ServerCertificateUntrusted, result.Receipt.Outcome);

        await host.StopAsync(CancellationToken.None);
        Assert.Equal(OpcUaCollectorSessionState.Closed, host.State);
    }

    [Fact]
    public async Task The_host_binds_a_profile_and_a_secret_through_the_collector_bootstrap_sources()
    {
        await using CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true);

        var profile = new OpcUaCollectorSourceProfile(
            "plant-historian-1",
            "v7",
            harness.Server.EndpointUrl,
            Opc.Ua.SecurityPolicies.Basic256Sha256,
            OpcUaCollectorSecurityMode.SignAndEncrypt,
            OpcUaCollectorIdentityMode.UserName,
            "collector/plant-historian-1",
            60000,
            1000,
            1000,
            5000);

        string profilePath = Path.Combine(harness.Root, "source-profile.json");
        await File.WriteAllTextAsync(
            profilePath,
            JsonSerializer.Serialize(profile, new JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            }),
            new UTF8Encoding(false));

        string key = EnvironmentSecretResolver.Prefix +
            EnvironmentSecretResolver.NormalizeReference(profile.SecretReference!);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [key + "_USER"] = CredentialCheckingTestServer.AcceptedUser,
            [key + "_PASSWORD"] = CredentialCheckingTestServer.AcceptedPassword
        };

        await using var host = new OpcUaCollectorHost(
            harness.Application,
            new FileSourceProfileProvider(profilePath),
            new EnvironmentSecretResolver(name => environment.TryGetValue(name, out string? value) ? value : null),
            profile.SourceProfileId,
            profile.ConfigurationVersion);

        OpcUaCollectorHostRunResult result = await host.StartAsync(CancellationToken.None);

        Assert.True(result.SessionEstablished);
        Assert.Equal(Opc.Ua.UserTokenType.UserName.ToString(), result.Receipt.EffectiveUserTokenType);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_bootstrap_profile_source_publishes_nothing_for_another_version()
    {
        string root = Path.Combine(Path.GetTempPath(), "ppiq-collector-bootstrap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string profilePath = Path.Combine(root, "source-profile.json");
            var profile = new OpcUaCollectorSourceProfile(
                "plant-historian-1",
                "v1",
                "opc.tcp://localhost:48401/Server",
                Opc.Ua.SecurityPolicies.Basic256Sha256,
                OpcUaCollectorSecurityMode.SignAndEncrypt,
                OpcUaCollectorIdentityMode.Anonymous,
                null);

            await File.WriteAllTextAsync(
                profilePath,
                JsonSerializer.Serialize(profile, new JsonSerializerOptions
                {
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }),
                new UTF8Encoding(false));

            var provider = new FileSourceProfileProvider(profilePath);

            Assert.NotNull(await provider.GetAsync("plant-historian-1", "v1", CancellationToken.None));
            Assert.Null(await provider.GetAsync("plant-historian-1", "v2", CancellationToken.None));
            Assert.Null(await provider.GetAsync("other-source", "v1", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task The_bootstrap_secret_source_returns_nothing_when_the_reference_is_not_injected()
    {
        var resolver = new EnvironmentSecretResolver(_ => null);

        Assert.Null(await resolver.ResolveUserNameAsync("collector/plant-historian-1", CancellationToken.None));
    }
}