using Opc.Ua;
using PlantProcess.Collector.OpcUa;
using PlantProcess.Collector.Tests.TestSupport;

namespace PlantProcess.Collector.Tests;

/// <summary>
/// Browse, bounded read and subscription acceptance against a real SDK server started in
/// process. Each operation is proved on its own: none of them is inferred from another,
/// and none of this is site certification for a customer plant.
/// </summary>
[Trait("Gate", "CollectorFieldAcquisition")]
public sealed class OpcUaCollectorFieldAcquisitionTests
{
    private static readonly TimeSpan SampleWait = TimeSpan.FromSeconds(30);

    private static OpcUaProviderFieldIdentity Temperature()
        => new(
            PpiqSourceNodeManagerFactory.SourceNamespaceUri,
            OpcUaIdentifierType.String,
            PpiqSourceNodeManager.TemperatureIdentifier,
            OpcUaProviderFieldIdentity.DefaultAttributeId,
            null);

    private static OpcUaProviderFieldIdentity Unreadable()
        => new(
            PpiqSourceNodeManagerFactory.SourceNamespaceUri,
            OpcUaIdentifierType.String,
            PpiqSourceNodeManager.UnreadableIdentifier,
            OpcUaProviderFieldIdentity.DefaultAttributeId,
            null);

    private static async Task<(CollectorTestHarness Harness, OpcUaCollectorSessionRuntime Runtime)> ConnectedAsync(
        bool withFillerNamespace = false,
        int? port = null)
    {
        CollectorTestHarness harness = await CollectorTestHarness.StartAsync(true, port, withFillerNamespace);
        OpcUaCollectorSessionRuntime runtime = harness.Runtime(harness.Profile());
        OpcUaCollectorSessionReceipt receipt = await runtime.ConnectAsync(CancellationToken.None);
        Assert.Equal(OpcUaCollectorOutcomeCode.SessionEstablished, receipt.Outcome);
        return (harness, runtime);
    }

    [Fact]
    public async Task Browse_returns_bounded_nodes_with_identity_and_governance_metadata()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        var browse = new OpcUaCollectorBrowseService(runtime, harness.Telemetry);
        OpcUaBrowseReceipt receipt = await browse.BrowseAsync(null, new OpcUaBrowseBounds(200, 3, 50, 10), CancellationToken.None);

        Assert.Equal(OpcUaCollectorOperationStatus.Executed, receipt.Operation.Status);

        OpcUaBrowseNodeFact temperature = Assert.Single(
            receipt.Nodes.Where(node => node.BrowseName == "Temperature"));

        Assert.NotNull(temperature.Identity);
        Assert.Equal(PpiqSourceNodeManagerFactory.SourceNamespaceUri, temperature.Identity!.NamespaceUri);
        Assert.Equal(OpcUaIdentifierType.String, temperature.Identity.IdentifierType);
        Assert.Equal(PpiqSourceNodeManager.TemperatureIdentifier, temperature.Identity.IdentifierValue);
        Assert.Equal(Attributes.Value, temperature.Identity.AttributeId);
        Assert.Equal("Variable", temperature.NodeClass);
        Assert.Equal(ValueRanks.Scalar, temperature.ValueRank);
        Assert.True(temperature.Readable);
        Assert.Contains("Double", temperature.DataType!, StringComparison.Ordinal);
        Assert.Equal(PpiqSourceNodeManager.EngineeringUnitText, temperature.EngineeringUnit);
    }

    [Fact]
    public async Task Browse_stops_at_its_declared_bound_and_says_so()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        var browse = new OpcUaCollectorBrowseService(runtime, harness.Telemetry);
        OpcUaBrowseReceipt receipt = await browse.BrowseAsync(null, new OpcUaBrowseBounds(3, 3, 5, 2), CancellationToken.None);

        Assert.True(receipt.NodesVisited <= 3, "The browse must never exceed its node bound.");
        Assert.True(receipt.BoundReached, "Reaching the bound must be recorded, not hidden.");
        Assert.Contains(receipt.Refusals, refusal => refusal.RefusalCode == OpcUaCollectorRefusalCodes.BoundReached);
    }

    [Fact]
    public async Task Browse_records_a_typed_refusal_for_a_node_the_server_does_not_publish()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        var unknown = new OpcUaProviderFieldIdentity(
            "urn:ppiq:test:not-published",
            OpcUaIdentifierType.String,
            "Nothing",
            OpcUaProviderFieldIdentity.DefaultAttributeId,
            null);

        var browse = new OpcUaCollectorBrowseService(runtime, harness.Telemetry);
        OpcUaBrowseReceipt receipt = await browse.BrowseAsync(unknown, new OpcUaBrowseBounds(), CancellationToken.None);

        Assert.Equal(OpcUaCollectorOperationStatus.Refused, receipt.Operation.Status);
        Assert.Empty(receipt.Nodes);
        Assert.Contains(receipt.Refusals, refusal => refusal.RefusalCode == OpcUaCollectorRefusalCodes.NamespaceNotPublished);
    }

    [Fact]
    public async Task Browse_output_becomes_registration_candidates_without_a_field_id()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        var browse = new OpcUaCollectorBrowseService(runtime, harness.Telemetry);
        OpcUaBrowseReceipt receipt = await browse.BrowseAsync(null, new OpcUaBrowseBounds(), CancellationToken.None);

        OpcUaProviderFieldRegistrationSet registrations = OpcUaCollectorFieldRegistration.FromBrowse(receipt);

        Assert.NotEmpty(registrations.Fields);
        Assert.All(registrations.Fields, field => Assert.False(string.IsNullOrWhiteSpace(field.Identity.NamespaceUri)));
        Assert.Contains(
            registrations.Fields,
            field => field.Identity.IdentifierValue == PpiqSourceNodeManager.TemperatureIdentifier);
        Assert.All(registrations.Fields, field => Assert.Equal(OpcUaCollectorFieldRegistration.ProviderType, field.ProviderType));
    }

    [Fact]
    public async Task Bounded_read_returns_value_quality_and_both_timestamps()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        var read = new OpcUaCollectorBoundedReadService(runtime, harness.Telemetry);
        OpcUaBoundedReadReceipt receipt = await read.ReadAsync(new[] { Temperature() }, CancellationToken.None);

        Assert.Equal(OpcUaCollectorOperationStatus.Executed, receipt.Operation.Status);
        Assert.Equal(1, receipt.RequestedScope);
        Assert.Equal(1, receipt.AchievedScope);
        Assert.False(receipt.AtomicSourceSnapshot, "An ordinary multi-node read is never a controller-atomic snapshot.");

        OpcUaObservation observation = Assert.Single(receipt.Observations);
        Assert.True(observation.QualityIsGood);
        Assert.NotNull(observation.Value);
        Assert.NotNull(observation.SourceTimestampUtc);
        Assert.NotNull(observation.ServerTimestampUtc);
        Assert.Equal(PpiqSourceNodeManager.TemperatureIdentifier, observation.Identity.IdentifierValue);
    }

    [Fact]
    public async Task Bounded_read_refuses_an_inaccessible_field_and_substitutes_nothing()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        var read = new OpcUaCollectorBoundedReadService(runtime, harness.Telemetry);
        OpcUaBoundedReadReceipt receipt = await read.ReadAsync(new[] { Unreadable() }, CancellationToken.None);

        Assert.Equal(OpcUaCollectorOperationStatus.Refused, receipt.Operation.Status);
        Assert.Equal(1, receipt.RequestedScope);
        Assert.Equal(0, receipt.AchievedScope);
        Assert.Empty(receipt.Observations);
        OpcUaReadRefusal refusal = Assert.Single(receipt.Refusals);
        Assert.Equal(OpcUaCollectorRefusalCodes.NodeNotAccessible, refusal.RefusalCode);
    }

    [Fact]
    public async Task Bounded_read_reports_requested_and_achieved_scope_separately()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        var read = new OpcUaCollectorBoundedReadService(runtime, harness.Telemetry);
        OpcUaBoundedReadReceipt receipt = await read.ReadAsync(
            new[] { Temperature(), Unreadable() },
            CancellationToken.None);

        Assert.Equal(2, receipt.RequestedScope);
        Assert.Equal(1, receipt.AchievedScope);
        Assert.Single(receipt.Observations);
        Assert.Single(receipt.Refusals);
    }

    [Fact]
    public async Task Subscription_records_requested_and_server_revised_values_separately()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        await using var subscription = new OpcUaCollectorSubscriptionSession(runtime, harness.Telemetry);
        var request = new OpcUaSubscriptionRequest(500, 250, 10, true, OpcUaDeadbandKind.Absolute, 0.25);

        OpcUaSubscriptionReceipt receipt = await subscription.SubscribeAsync(
            new[] { Temperature() },
            request,
            CancellationToken.None);

        Assert.Equal(OpcUaCollectorOperationStatus.Executed, receipt.Operation.Status);
        Assert.Equal(request.PublishingIntervalMs, receipt.RequestedPublishingIntervalMs);
        Assert.NotNull(receipt.RevisedPublishingIntervalMs);

        OpcUaMonitoredItemFact item = Assert.Single(receipt.Items);
        Assert.True(item.Created);
        Assert.Equal(request.SamplingIntervalMs, item.RequestedSamplingIntervalMs);
        Assert.NotNull(item.RevisedSamplingIntervalMs);
        Assert.Equal(request.QueueSize, item.RequestedQueueSize);
        Assert.NotNull(item.RevisedQueueSize);
        Assert.Contains("deadband=Absolute", item.RequestedFilter, StringComparison.Ordinal);
        Assert.NotNull(item.EffectiveFilter);
    }

    [Fact]
    public async Task Subscription_delivers_source_samples_and_fabricates_none()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        await using var subscription = new OpcUaCollectorSubscriptionSession(runtime, harness.Telemetry);
        await subscription.SubscribeAsync(
            new[] { Temperature() },
            new OpcUaSubscriptionRequest(250, 100, 10),
            CancellationToken.None);

        harness.Server.SourceNodes!.SetTemperature(33.75d);

        bool delivered = await CollectorTestHarness.WaitUntilAsync(
            () => subscription.Samples.Any(sample => sample.Value is not null && sample.Value.Contains("33.75", StringComparison.Ordinal)),
            SampleWait);

        Assert.True(delivered, "The source change must arrive as a monitored item notification.");

        OpcUaSubscriptionSample sample = subscription.Samples.Last();
        Assert.True(sample.QualityIsGood);
        Assert.NotNull(sample.SourceTimestampUtc);
        Assert.True(sample.SequenceInSession > 0);
        Assert.All(subscription.Samples, item => Assert.Equal(PpiqSourceNodeManager.TemperatureIdentifier, item.Identity.IdentifierValue));
    }

    [Fact]
    public async Task Subscription_resubscribes_after_an_outage_and_keeps_the_gap_as_evidence()
    {
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync();
        await using CollectorTestHarness _ = harness;

        await using var subscription = new OpcUaCollectorSubscriptionSession(runtime, harness.Telemetry);
        await subscription.SubscribeAsync(
            new[] { Temperature() },
            new OpcUaSubscriptionRequest(250, 100, 10),
            CancellationToken.None);

        await CollectorTestHarness.WaitUntilAsync(() => subscription.Samples.Count > 0, SampleWait);
        int before = subscription.Samples.Count;

        OpcUaSubscriptionReceipt receipt = await subscription.ResubscribeAsync(
            "source connection loss simulated by the acceptance suite",
            CancellationToken.None);

        Assert.Equal(OpcUaCollectorOperationStatus.Executed, receipt.Operation.Status);

        OpcUaSubscriptionGap gap = Assert.Single(subscription.Gaps);
        Assert.True(gap.Resubscribed);
        Assert.NotNull(gap.RecoveredAtUtc);
        Assert.Equal(before, (int)gap.SamplesBeforeGap);
        Assert.All(subscription.Samples, sample => Assert.NotNull(sample.SourceTimestampUtc));
    }

    [Fact]
    public async Task A_namespace_index_change_never_changes_the_field_identity()
    {
        int port = SdkTestServerHost.GetFreePort();
        (CollectorTestHarness harness, OpcUaCollectorSessionRuntime runtime) = await ConnectedAsync(false, port);
        await using CollectorTestHarness _ = harness;

        var read = new OpcUaCollectorBoundedReadService(runtime, harness.Telemetry);
        OpcUaBoundedReadReceipt before = await read.ReadAsync(new[] { Temperature() }, CancellationToken.None);
        Assert.Equal(1, before.AchievedScope);

        ushort indexBefore = harness.Server.SourceNodes!.SourceNamespaceIndex;

        await runtime.DisconnectAsync(CancellationToken.None);
        await harness.Server.StopAsync();
        await harness.Server.RestartAsync(true);

        OpcUaCollectorSessionRuntime reconnected = harness.Runtime(harness.Profile());
        Assert.Equal(
            OpcUaCollectorOutcomeCode.SessionEstablished,
            (await reconnected.ConnectAsync(CancellationToken.None)).Outcome);

        ushort indexAfter = harness.Server.SourceNodes!.SourceNamespaceIndex;
        Assert.NotEqual(indexBefore, indexAfter);

        var readAgain = new OpcUaCollectorBoundedReadService(reconnected, harness.Telemetry);
        OpcUaBoundedReadReceipt after = await readAgain.ReadAsync(new[] { Temperature() }, CancellationToken.None);

        Assert.Equal(1, after.AchievedScope);
        Assert.Equal(
            before.Observations[0].Identity.Canonical(),
            after.Observations[0].Identity.Canonical());
    }
}