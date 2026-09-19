using Opc.Ua;
using PlantProcess.Collector.OpcUa;

namespace PlantProcess.Collector.Tests;

/// <summary>
/// The identity law: namespace URI plus identifier, never namespace index. Every test here
/// is offline, so the rule is provable without a server as well as against one.
/// </summary>
[Trait("Gate", "CollectorFieldIdentity")]
public sealed class CollectorFieldIdentityTests
{
    private const string SourceNamespace = "urn:ppiq:test:source";
    private const string OtherNamespace = "urn:ppiq:test:other";

    private static NamespaceTable Table(params string[] uris)
    {
        var table = new NamespaceTable();
        foreach (string uri in uris)
        {
            table.Append(uri);
        }

        return table;
    }

    [Fact]
    public void An_identity_carries_the_namespace_uri_and_never_the_index()
    {
        NamespaceTable table = Table(OtherNamespace, SourceNamespace);
        var nodeId = new NodeId("Temperature", (ushort)table.GetIndex(SourceNamespace));

        OpcUaProviderFieldIdentity? identity = OpcUaProviderFieldIdentity.TryCreate(nodeId, table);

        Assert.NotNull(identity);
        Assert.Equal(SourceNamespace, identity!.NamespaceUri);
        Assert.Equal(OpcUaIdentifierType.String, identity.IdentifierType);
        Assert.Equal("Temperature", identity.IdentifierValue);
        Assert.DoesNotContain(
            nodeId.NamespaceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";",
            identity.Canonical(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_identity_resolves_to_a_different_index_after_a_namespace_reorder()
    {
        NamespaceTable before = Table(OtherNamespace, SourceNamespace);
        NamespaceTable after = Table(SourceNamespace, OtherNamespace);

        var nodeId = new NodeId("Temperature", (ushort)before.GetIndex(SourceNamespace));
        OpcUaProviderFieldIdentity identity = OpcUaProviderFieldIdentity.TryCreate(nodeId, before)!;

        NodeId? resolvedBefore = identity.TryResolve(before);
        NodeId? resolvedAfter = identity.TryResolve(after);

        Assert.NotNull(resolvedBefore);
        Assert.NotNull(resolvedAfter);
        Assert.NotEqual(resolvedBefore!.NamespaceIndex, resolvedAfter!.NamespaceIndex);
        Assert.Equal(resolvedBefore.Identifier, resolvedAfter.Identifier);
    }

    [Fact]
    public void An_index_based_identity_would_point_at_the_wrong_field_after_a_reorder()
    {
        NamespaceTable before = Table(OtherNamespace, SourceNamespace);
        NamespaceTable after = Table(SourceNamespace, OtherNamespace);

        ushort indexBefore = (ushort)before.GetIndex(SourceNamespace);
        var frozenIndexNodeId = new NodeId("Temperature", indexBefore);

        string namespaceBehindFrozenIndex = after.GetString(frozenIndexNodeId.NamespaceIndex);

        Assert.NotEqual(SourceNamespace, namespaceBehindFrozenIndex);
    }

    [Fact]
    public void A_namespace_the_server_no_longer_publishes_resolves_to_nothing()
    {
        var identity = new OpcUaProviderFieldIdentity(
            "urn:ppiq:test:retired",
            OpcUaIdentifierType.String,
            "Temperature",
            OpcUaProviderFieldIdentity.DefaultAttributeId,
            null);

        Assert.Null(identity.TryResolve(Table(SourceNamespace)));
    }

    [Theory]
    [InlineData(OpcUaIdentifierType.Numeric, "4711")]
    [InlineData(OpcUaIdentifierType.String, "Plant/Line1/Temperature")]
    public void An_identity_round_trips_through_the_namespace_table(OpcUaIdentifierType identifierType, string identifierValue)
    {
        NamespaceTable table = Table(SourceNamespace);
        var identity = new OpcUaProviderFieldIdentity(
            SourceNamespace,
            identifierType,
            identifierValue,
            OpcUaProviderFieldIdentity.DefaultAttributeId,
            null);

        NodeId? resolved = identity.TryResolve(table);
        Assert.NotNull(resolved);

        OpcUaProviderFieldIdentity? roundTripped = OpcUaProviderFieldIdentity.TryCreate(resolved!, table);

        Assert.NotNull(roundTripped);
        Assert.Equal(identity.Canonical(), roundTripped!.Canonical());
    }

    [Fact]
    public void The_value_attribute_is_the_explicit_default_and_the_only_supported_one()
    {
        NamespaceTable table = Table(SourceNamespace);
        OpcUaProviderFieldIdentity identity = OpcUaProviderFieldIdentity
            .TryCreate(new NodeId("Temperature", (ushort)table.GetIndex(SourceNamespace)), table)!;

        Assert.Equal(Attributes.Value, identity.AttributeId);
        Assert.True(identity.AttributeIsSupported);

        OpcUaProviderFieldIdentity displayName = identity with { AttributeId = Attributes.DisplayName };
        Assert.False(displayName.AttributeIsSupported);
    }

    [Fact]
    public void Browse_path_and_display_name_are_metadata_and_never_part_of_the_identity()
    {
        NamespaceTable table = Table(SourceNamespace);
        OpcUaProviderFieldIdentity identity = OpcUaProviderFieldIdentity
            .TryCreate(new NodeId("Temperature", (ushort)table.GetIndex(SourceNamespace)), table)!;

        string canonical = identity.Canonical();

        Assert.DoesNotContain("BrowsePath", canonical, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DisplayName", canonical, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nsu=" + SourceNamespace, canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void An_index_range_is_part_of_the_identity()
    {
        var scalar = new OpcUaProviderFieldIdentity(
            SourceNamespace,
            OpcUaIdentifierType.String,
            "Array",
            OpcUaProviderFieldIdentity.DefaultAttributeId,
            null);

        OpcUaProviderFieldIdentity element = scalar with { IndexRange = "3" };

        Assert.NotEqual(scalar.Canonical(), element.Canonical());
        Assert.Contains("range=3", element.Canonical(), StringComparison.Ordinal);
    }

    [Fact]
    public void Browse_bounds_refuse_an_unbounded_crawl()
    {
        Assert.NotEmpty(new OpcUaBrowseBounds(0, 3, 50, 10).Validate());
        Assert.NotEmpty(new OpcUaBrowseBounds(200, 0, 50, 10).Validate());
        Assert.NotEmpty(new OpcUaBrowseBounds(200, 3, 0, 10).Validate());
        Assert.NotEmpty(new OpcUaBrowseBounds(200, 3, 50, 0).Validate());
        Assert.Empty(new OpcUaBrowseBounds().Validate());
    }

    [Fact]
    public void A_subscription_request_refuses_an_impossible_transport_configuration()
    {
        Assert.NotEmpty(new OpcUaSubscriptionRequest(10).Validate());
        Assert.NotEmpty(new OpcUaSubscriptionRequest(500, 250, 0).Validate());
        Assert.NotEmpty(new OpcUaSubscriptionRequest(500, 250, 10, true, OpcUaDeadbandKind.Absolute, 0).Validate());
        Assert.Empty(new OpcUaSubscriptionRequest().Validate());
    }
}