namespace PlantProcess.Collector.OpcUa;

/// <summary>
/// One selected source field, offered to the provider-neutral configuration authority.
///
/// This is a candidate, not a governed PPIQ field. The collector computes no field id, no
/// stable field hash and keeps no local tag catalogue. Selection and naming are the
/// operator's; governance is the configuration authority's.
/// </summary>
public sealed record OpcUaProviderFieldRegistration(
    string SourceProfileId,
    string ConfigurationVersion,
    DateTimeOffset ObservedAtUtc,
    OpcUaProviderFieldIdentity Identity,
    string ProviderType,
    string BrowsePath,
    string BrowseName,
    string? DisplayName,
    string? DataType,
    int? ValueRank,
    IReadOnlyList<uint> ArrayDimensions,
    bool? Readable,
    string? EngineeringUnit);

/// <summary>The provider-neutral output of one registration pass.</summary>
public sealed record OpcUaProviderFieldRegistrationSet(
    string SourceProfileId,
    string ConfigurationVersion,
    DateTimeOffset ObservedAtUtc,
    OpcUaCollectorOperationFact Operation,
    IReadOnlyList<OpcUaProviderFieldRegistration> Fields,
    IReadOnlyList<OpcUaBrowseRefusal> Refusals);

/// <summary>
/// Turns browse facts into registration candidates. It selects only variable nodes that
/// carry a stable provider identity and a supported attribute, and it writes nothing
/// anywhere: the output is handed to the configuration authority.
/// </summary>
public static class OpcUaCollectorFieldRegistration
{
    public const string ProviderType = "OpcUaHistorian";

    public static OpcUaProviderFieldRegistrationSet FromBrowse(OpcUaBrowseReceipt browse)
    {
        ArgumentNullException.ThrowIfNull(browse);

        var fields = new List<OpcUaProviderFieldRegistration>();
        var refusals = new List<OpcUaBrowseRefusal>(browse.Refusals);

        foreach (OpcUaBrowseNodeFact node in browse.Nodes)
        {
            if (node.Identity is null)
            {
                continue;
            }

            if (!node.Identity.AttributeIsSupported)
            {
                refusals.Add(new OpcUaBrowseRefusal(
                    node.BrowsePath,
                    OpcUaCollectorRefusalCodes.AttributeNotSupported,
                    null,
                    "The field names an attribute this build does not acquire."));
                continue;
            }

            fields.Add(new OpcUaProviderFieldRegistration(
                browse.SourceProfileId,
                browse.ConfigurationVersion,
                browse.ObservedAtUtc,
                node.Identity,
                ProviderType,
                node.BrowsePath,
                node.BrowseName,
                node.DisplayName,
                node.DataType,
                node.ValueRank,
                node.ArrayDimensions,
                node.Readable,
                node.EngineeringUnit));
        }

        return new OpcUaProviderFieldRegistrationSet(
            browse.SourceProfileId,
            browse.ConfigurationVersion,
            DateTimeOffset.UtcNow,
            new OpcUaCollectorOperationFact(
                OpcUaCollectorOperations.Browse,
                browse.Operation.Status,
                "Registration candidates emitted for the configuration authority. No field id and no local catalogue."),
            fields,
            refusals);
    }
}