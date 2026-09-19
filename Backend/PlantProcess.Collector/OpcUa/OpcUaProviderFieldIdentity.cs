using System.Globalization;
using Opc.Ua;

namespace PlantProcess.Collector.OpcUa;

/// <summary>How the source names the node inside its namespace.</summary>
public enum OpcUaIdentifierType
{
    Numeric,
    String,
    Guid,
    Opaque
}

/// <summary>
/// The stable OPC UA identity of one source field, as the source itself declares it.
///
/// The namespace index is never identity: it is a per-session position in the server's
/// namespace table and it moves when the table is reordered or the server restarts.
/// Identity is the namespace URI plus the identifier, its type, the attribute and the
/// index range. BrowsePath, DisplayName and engineering unit are metadata, never identity.
///
/// This is a candidate only. The provider-neutral configuration authority governs the
/// PPIQ field identity; this collector computes no field id and no stable field hash.
/// </summary>
public sealed record OpcUaProviderFieldIdentity(
    string NamespaceUri,
    OpcUaIdentifierType IdentifierType,
    string IdentifierValue,
    uint AttributeId,
    string? IndexRange)
{
    /// <summary>The only source attribute this build acquires values from.</summary>
    public static readonly IReadOnlyList<uint> SupportedAttributeIds = new[] { Attributes.Value };

    public static uint DefaultAttributeId => Attributes.Value;

    public bool AttributeIsSupported => SupportedAttributeIds.Contains(AttributeId);

    /// <summary>A readable rendering for evidence. It is not a hash and not a field id.</summary>
    public string Canonical()
        => "nsu=" + NamespaceUri +
           ";idType=" + IdentifierType +
           ";id=" + IdentifierValue +
           ";attr=" + AttributeId.ToString(CultureInfo.InvariantCulture) +
           ";range=" + (string.IsNullOrEmpty(IndexRange) ? "-" : IndexRange);

    /// <summary>
    /// Builds the identity for a node as seen in one session. The namespace index is
    /// translated to its URI here and then discarded.
    /// </summary>
    public static OpcUaProviderFieldIdentity? TryCreate(
        NodeId nodeId,
        NamespaceTable namespaceUris,
        uint? attributeId = null,
        string? indexRange = null)
    {
        ArgumentNullException.ThrowIfNull(namespaceUris);

        if (NodeId.IsNull(nodeId))
        {
            return null;
        }

        string? namespaceUri = namespaceUris.GetString(nodeId.NamespaceIndex);
        if (string.IsNullOrEmpty(namespaceUri))
        {
            return null;
        }

        (OpcUaIdentifierType identifierType, string identifierValue) = Describe(nodeId);

        return new OpcUaProviderFieldIdentity(
            namespaceUri,
            identifierType,
            identifierValue,
            attributeId ?? DefaultAttributeId,
            string.IsNullOrWhiteSpace(indexRange) ? null : indexRange);
    }

    /// <summary>
    /// Resolves the identity against the namespace table of the current session. A
    /// namespace URI the server no longer publishes resolves to null, never to a guessed
    /// index.
    /// </summary>
    public NodeId? TryResolve(NamespaceTable namespaceUris)
    {
        ArgumentNullException.ThrowIfNull(namespaceUris);

        int index = namespaceUris.GetIndex(NamespaceUri);
        if (index < 0 || index > ushort.MaxValue)
        {
            return null;
        }

        var namespaceIndex = (ushort)index;

        switch (IdentifierType)
        {
            case OpcUaIdentifierType.Numeric:
                return uint.TryParse(IdentifierValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint numeric)
                    ? new NodeId(numeric, namespaceIndex)
                    : null;

            case OpcUaIdentifierType.String:
                return new NodeId(IdentifierValue, namespaceIndex);

            case OpcUaIdentifierType.Guid:
                return System.Guid.TryParse(IdentifierValue, out Guid guid)
                    ? new NodeId(guid, namespaceIndex)
                    : null;

            case OpcUaIdentifierType.Opaque:
                try
                {
                    return new NodeId(Convert.FromBase64String(IdentifierValue), namespaceIndex);
                }
                catch (FormatException)
                {
                    return null;
                }

            default:
                return null;
        }
    }

    private static (OpcUaIdentifierType, string) Describe(NodeId nodeId)
    {
        switch (nodeId.IdType)
        {
            case IdType.Numeric:
                return (OpcUaIdentifierType.Numeric,
                    Convert.ToUInt32(nodeId.Identifier, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));

            case IdType.String:
                return (OpcUaIdentifierType.String, (string)nodeId.Identifier);

            case IdType.Guid:
                return (OpcUaIdentifierType.Guid, ((Guid)nodeId.Identifier).ToString("D", CultureInfo.InvariantCulture));

            default:
                return (OpcUaIdentifierType.Opaque, Convert.ToBase64String((byte[])nodeId.Identifier));
        }
    }
}

/// <summary>Typed refusal codes for the field-acquisition operations.</summary>
public static class OpcUaCollectorRefusalCodes
{
    public const string NamespaceNotPublished = "SOURCE_NAMESPACE_NOT_PUBLISHED";
    public const string NodeNotResolvable = "SOURCE_NODE_NOT_RESOLVABLE";
    public const string NodeNotAccessible = "SOURCE_NODE_NOT_ACCESSIBLE";
    public const string AttributeNotSupported = "SOURCE_ATTRIBUTE_NOT_SUPPORTED";
    public const string BrowseRefused = "SOURCE_BROWSE_REFUSED";
    public const string ReadRefused = "SOURCE_READ_REFUSED";
    public const string SubscriptionRefused = "SOURCE_SUBSCRIPTION_REFUSED";
    public const string SessionNotConnected = "COLLECTOR_SESSION_NOT_CONNECTED";
    public const string BoundReached = "SOURCE_BOUND_REACHED";
}