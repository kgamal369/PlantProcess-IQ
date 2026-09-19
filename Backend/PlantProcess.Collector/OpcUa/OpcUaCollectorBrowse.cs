using System.Globalization;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace PlantProcess.Collector.OpcUa;

/// <summary>Explicit bounds for one browse. There is no unbounded crawl.</summary>
public sealed record OpcUaBrowseBounds(
    int MaxNodes = 200,
    int MaxDepth = 3,
    uint MaxReferencesPerNode = 50,
    int MaxContinuationPages = 10)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (MaxNodes < 1 || MaxNodes > 5000)
        {
            errors.Add("MaxNodes must be between 1 and 5000.");
        }

        if (MaxDepth < 1 || MaxDepth > 10)
        {
            errors.Add("MaxDepth must be between 1 and 10.");
        }

        if (MaxReferencesPerNode < 1 || MaxReferencesPerNode > 1000)
        {
            errors.Add("MaxReferencesPerNode must be between 1 and 1000.");
        }

        if (MaxContinuationPages < 1 || MaxContinuationPages > 100)
        {
            errors.Add("MaxContinuationPages must be between 1 and 100.");
        }

        return errors;
    }
}

/// <summary>One node the browse reached, with the metadata field governance needs.</summary>
public sealed record OpcUaBrowseNodeFact(
    OpcUaProviderFieldIdentity? Identity,
    string NodeClass,
    string BrowseName,
    string? DisplayName,
    string BrowsePath,
    int Depth,
    string? DataType,
    int? ValueRank,
    IReadOnlyList<uint> ArrayDimensions,
    byte? AccessLevel,
    bool? Readable,
    string? EngineeringUnit);

/// <summary>A node or sub-tree the source refused or the bounds cut off. Nothing disappears silently.</summary>
public sealed record OpcUaBrowseRefusal(
    string BrowsePath,
    string RefusalCode,
    string? SourceStatusCode,
    string Detail);

/// <summary>The operation-level receipt of one bounded browse.</summary>
public sealed record OpcUaBrowseReceipt(
    string SourceProfileId,
    string ConfigurationVersion,
    DateTimeOffset ObservedAtUtc,
    OpcUaCollectorOperationFact Operation,
    string RequestedRootNode,
    OpcUaBrowseBounds Bounds,
    bool BoundReached,
    int NodesVisited,
    int ContinuationPagesFollowed,
    IReadOnlyList<OpcUaBrowseNodeFact> Nodes,
    IReadOnlyList<OpcUaBrowseRefusal> Refusals);

/// <summary>
/// Bounded OPC UA browse. It walks only as far as the declared bounds allow, follows
/// continuation points explicitly, records what it could not reach, and emits a provider
/// field identity candidate for every variable node it found.
///
/// It reads node attributes to describe a variable. Reading a value is a different
/// operation with its own capability and its own gate.
/// </summary>
public sealed class OpcUaCollectorBrowseService
{
    private readonly OpcUaCollectorSessionRuntime _runtime;
    private readonly ILogger _logger;

    public OpcUaCollectorBrowseService(OpcUaCollectorSessionRuntime runtime, ITelemetryContext telemetry)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(telemetry);
        _runtime = runtime;
        _logger = telemetry.LoggerFactory.CreateLogger("PlantProcess.Collector.OpcUa.Browse");
    }

    public Task<OpcUaBrowseReceipt> BrowseAsync(
        OpcUaProviderFieldIdentity? root,
        OpcUaBrowseBounds bounds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bounds);

        IReadOnlyList<string> boundErrors = bounds.Validate();
        if (boundErrors.Count > 0)
        {
            throw new ArgumentException("Browse bounds are invalid: " + string.Join(" ", boundErrors), nameof(bounds));
        }

        return _runtime.WithSessionAsync(
            (session, ct) => ExecuteAsync(session, root, bounds, ct),
            () => Refused(
                OpcUaCollectorRefusalCodes.SessionNotConnected,
                "The collector session is not connected, so nothing was browsed.",
                root,
                bounds),
            cancellationToken);
    }

    private async Task<OpcUaBrowseReceipt> ExecuteAsync(
        ISession session,
        OpcUaProviderFieldIdentity? root,
        OpcUaBrowseBounds bounds,
        CancellationToken cancellationToken)
    {
        NodeId rootNodeId = ObjectIds.ObjectsFolder;
        if (root is not null)
        {
            NodeId? resolved = root.TryResolve(session.MessageContext.NamespaceUris);
            if (resolved is null)
            {
                return Refused(
                    OpcUaCollectorRefusalCodes.NamespaceNotPublished,
                    "The requested browse root names a namespace this server does not publish.",
                    root,
                    bounds);
            }

            rootNodeId = resolved;
        }

        var nodes = new List<OpcUaBrowseNodeFact>();
        var refusals = new List<OpcUaBrowseRefusal>();
        var queue = new Queue<(NodeId NodeId, string Path, int Depth)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int continuationPages = 0;
        bool boundReached = false;

        queue.Enqueue((rootNodeId, root is null ? "/Objects" : "/" + root.IdentifierValue, 0));
        seen.Add(rootNodeId.ToString());

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (nodes.Count >= bounds.MaxNodes)
            {
                boundReached = true;
                refusals.Add(new OpcUaBrowseRefusal(
                    "(remaining queue)",
                    OpcUaCollectorRefusalCodes.BoundReached,
                    null,
                    "The node bound of " + bounds.MaxNodes + " was reached. " + queue.Count + " nodes were left unvisited."));
                break;
            }

            (NodeId nodeId, string path, int depth) = queue.Dequeue();

            var description = new BrowseDescription
            {
                NodeId = nodeId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                ResultMask = (uint)BrowseResultMask.All
            };

            BrowseResponse response;
            try
            {
                response = await session
                    .BrowseAsync(null, null, bounds.MaxReferencesPerNode, new BrowseDescriptionCollection { description }, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ServiceResultException browseError)
            {
                refusals.Add(new OpcUaBrowseRefusal(
                    path,
                    OpcUaCollectorRefusalCodes.BrowseRefused,
                    OpcUaCollectorStatusNames.Describe(browseError.StatusCode),
                    browseError.Message));
                continue;
            }

            BrowseResult result = response.Results[0];
            if (StatusCode.IsBad(result.StatusCode))
            {
                refusals.Add(new OpcUaBrowseRefusal(
                    path,
                    OpcUaCollectorRefusalCodes.NodeNotAccessible,
                    OpcUaCollectorStatusNames.Describe(result.StatusCode.Code),
                    "The server refused the browse of this node."));
                continue;
            }

            var references = new List<ReferenceDescription>(result.References);
            byte[] continuationPoint = result.ContinuationPoint;

            while (continuationPoint is { Length: > 0 })
            {
                if (continuationPages >= bounds.MaxContinuationPages)
                {
                    boundReached = true;
                    refusals.Add(new OpcUaBrowseRefusal(
                        path,
                        OpcUaCollectorRefusalCodes.BoundReached,
                        null,
                        "The continuation page bound of " + bounds.MaxContinuationPages + " was reached."));
                    await ReleaseAsync(session, continuationPoint, cancellationToken).ConfigureAwait(false);
                    break;
                }

                BrowseNextResponse next = await session
                    .BrowseNextAsync(null, false, new ByteStringCollection { continuationPoint }, cancellationToken)
                    .ConfigureAwait(false);

                continuationPages++;
                BrowseResult nextResult = next.Results[0];
                references.AddRange(nextResult.References);
                continuationPoint = nextResult.ContinuationPoint;
            }

            foreach (ReferenceDescription reference in references)
            {
                if (nodes.Count >= bounds.MaxNodes)
                {
                    boundReached = true;
                    break;
                }

                NodeId childId = ExpandedNodeId.ToNodeId(reference.NodeId, session.MessageContext.NamespaceUris);
                if (NodeId.IsNull(childId))
                {
                    refusals.Add(new OpcUaBrowseRefusal(
                        path + "/" + reference.BrowseName,
                        OpcUaCollectorRefusalCodes.NodeNotResolvable,
                        null,
                        "The reference points outside the servers published namespace table."));
                    continue;
                }

                string childPath = path + "/" + reference.BrowseName.Name;
                if (!seen.Add(childId.ToString()))
                {
                    continue;
                }

                OpcUaBrowseNodeFact fact = await DescribeAsync(
                    session,
                    childId,
                    reference,
                    childPath,
                    depth + 1,
                    refusals,
                    cancellationToken).ConfigureAwait(false);

                nodes.Add(fact);

                if (reference.NodeClass == NodeClass.Object && depth + 1 < bounds.MaxDepth)
                {
                    queue.Enqueue((childId, childPath, depth + 1));
                }
            }
        }

        _logger.LogInformation(
            "Bounded browse visited {NodeCount} nodes with {RefusalCount} refusals.",
            nodes.Count,
            refusals.Count);

        return new OpcUaBrowseReceipt(
            _runtime.SourceProfileId,
            _runtime.ConfigurationVersion,
            DateTimeOffset.UtcNow,
            new OpcUaCollectorOperationFact(
                OpcUaCollectorOperations.Browse,
                OpcUaCollectorOperationStatus.Executed,
                "Bounded browse executed against the source within the declared bounds."),
            rootNodeId.ToString(),
            bounds,
            boundReached,
            nodes.Count,
            continuationPages,
            nodes,
            refusals);
    }

    private static async Task ReleaseAsync(ISession session, byte[] continuationPoint, CancellationToken cancellationToken)
    {
        try
        {
            await session
                .BrowseNextAsync(null, true, new ByteStringCollection { continuationPoint }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ServiceResultException)
        {
            // Releasing a continuation point is best effort and never changes the receipt.
        }
    }

    private async Task<OpcUaBrowseNodeFact> DescribeAsync(
        ISession session,
        NodeId nodeId,
        ReferenceDescription reference,
        string browsePath,
        int depth,
        List<OpcUaBrowseRefusal> refusals,
        CancellationToken cancellationToken)
    {
        OpcUaProviderFieldIdentity? identity = reference.NodeClass == NodeClass.Variable
            ? OpcUaProviderFieldIdentity.TryCreate(nodeId, session.MessageContext.NamespaceUris)
            : null;

        if (reference.NodeClass == NodeClass.Variable && identity is null)
        {
            refusals.Add(new OpcUaBrowseRefusal(
                browsePath,
                OpcUaCollectorRefusalCodes.NamespaceNotPublished,
                null,
                "The node carries a namespace index with no published URI, so it has no stable identity."));
        }

        string? dataType = null;
        int? valueRank = null;
        var arrayDimensions = new List<uint>();
        byte? accessLevel = null;
        bool? readable = null;
        string? engineeringUnit = null;

        if (reference.NodeClass == NodeClass.Variable)
        {
            var attributes = new ReadValueIdCollection
            {
                new ReadValueId { NodeId = nodeId, AttributeId = Attributes.DataType },
                new ReadValueId { NodeId = nodeId, AttributeId = Attributes.ValueRank },
                new ReadValueId { NodeId = nodeId, AttributeId = Attributes.ArrayDimensions },
                new ReadValueId { NodeId = nodeId, AttributeId = Attributes.AccessLevel }
            };

            try
            {
                ReadResponse metadata = await session
                    .ReadAsync(null, 0, TimestampsToReturn.Neither, attributes, cancellationToken)
                    .ConfigureAwait(false);

                dataType = DescribeDataType(metadata.Results[0], session.MessageContext.NamespaceUris);
                valueRank = metadata.Results[1].Value as int?;

                if (metadata.Results[2].Value is uint[] dimensions)
                {
                    arrayDimensions.AddRange(dimensions);
                }

                if (metadata.Results[3].Value is byte level)
                {
                    accessLevel = level;
                    readable = (level & AccessLevels.CurrentRead) == AccessLevels.CurrentRead;
                }
            }
            catch (ServiceResultException metadataError)
            {
                refusals.Add(new OpcUaBrowseRefusal(
                    browsePath,
                    OpcUaCollectorRefusalCodes.NodeNotAccessible,
                    OpcUaCollectorStatusNames.Describe(metadataError.StatusCode),
                    "Node metadata could not be read."));
            }

            engineeringUnit = await TryReadEngineeringUnitAsync(session, nodeId, cancellationToken).ConfigureAwait(false);
        }

        return new OpcUaBrowseNodeFact(
            identity,
            reference.NodeClass.ToString(),
            reference.BrowseName?.Name ?? string.Empty,
            reference.DisplayName?.Text,
            browsePath,
            depth,
            dataType,
            valueRank,
            arrayDimensions,
            accessLevel,
            readable,
            engineeringUnit);
    }

    private static string? DescribeDataType(DataValue value, NamespaceTable namespaceUris)
    {
        if (value.Value is not NodeId dataTypeId)
        {
            return null;
        }

        string? builtIn = TypeInfo.GetBuiltInType(dataTypeId).ToString();
        string qualified = dataTypeId.ToString();
        OpcUaProviderFieldIdentity? identity = OpcUaProviderFieldIdentity.TryCreate(dataTypeId, namespaceUris);
        return identity is null ? builtIn + " (" + qualified + ")" : builtIn + " (" + identity.Canonical() + ")";
    }

    private static async Task<string?> TryReadEngineeringUnitAsync(
        ISession session,
        NodeId nodeId,
        CancellationToken cancellationToken)
    {
        var description = new BrowseDescription
        {
            NodeId = nodeId,
            BrowseDirection = BrowseDirection.Forward,
            ReferenceTypeId = ReferenceTypeIds.HasProperty,
            IncludeSubtypes = true,
            NodeClassMask = (uint)NodeClass.Variable,
            ResultMask = (uint)BrowseResultMask.All
        };

        try
        {
            BrowseResponse response = await session
                .BrowseAsync(null, null, 20, new BrowseDescriptionCollection { description }, cancellationToken)
                .ConfigureAwait(false);

            foreach (ReferenceDescription reference in response.Results[0].References)
            {
                if (!string.Equals(reference.BrowseName?.Name, BrowseNames.EngineeringUnits, StringComparison.Ordinal))
                {
                    continue;
                }

                NodeId propertyId = ExpandedNodeId.ToNodeId(reference.NodeId, session.MessageContext.NamespaceUris);
                ReadResponse read = await session
                    .ReadAsync(
                        null,
                        0,
                        TimestampsToReturn.Neither,
                        new ReadValueIdCollection { new ReadValueId { NodeId = propertyId, AttributeId = Attributes.Value } },
                        cancellationToken)
                    .ConfigureAwait(false);

                EUInformation? unit = read.Results[0].Value switch
                {
                    ExtensionObject extension => extension.Body as EUInformation,
                    EUInformation direct => direct,
                    _ => null
                };

                if (unit is not null)
                {
                    return unit.DisplayName?.Text ?? unit.UnitId.ToString(CultureInfo.InvariantCulture);
                }
            }
        }
        catch (ServiceResultException)
        {
            return null;
        }

        return null;
    }

    private OpcUaBrowseReceipt Refused(
        string refusalCode,
        string detail,
        OpcUaProviderFieldIdentity? root,
        OpcUaBrowseBounds bounds)
    {
        return new OpcUaBrowseReceipt(
            _runtime.SourceProfileId,
            _runtime.ConfigurationVersion,
            DateTimeOffset.UtcNow,
            new OpcUaCollectorOperationFact(OpcUaCollectorOperations.Browse, OpcUaCollectorOperationStatus.Refused, detail),
            root is null ? ObjectIds.ObjectsFolder.ToString() : root.Canonical(),
            bounds,
            false,
            0,
            0,
            Array.Empty<OpcUaBrowseNodeFact>(),
            new[] { new OpcUaBrowseRefusal(root is null ? "/Objects" : root.Canonical(), refusalCode, null, detail) });
    }
}