using Opc.Ua;
using Opc.Ua.Server;

namespace PlantProcess.Collector.Tests.TestSupport;

/// <summary>
/// Publishes the node manager of the test source. The namespace order is deliberately
/// variable: the filler namespace exists only to move the source namespace to a different
/// index, so a test can prove that namespace index is never identity.
/// </summary>
internal sealed class PpiqSourceNodeManagerFactory : INodeManagerFactory
{
    internal const string SourceNamespaceUri = "urn:ppiq:test:source";
    internal const string FillerNamespaceUri = "urn:ppiq:test:filler";

    internal PpiqSourceNodeManagerFactory(bool withFillerNamespace)
    {
        NamespacesUris = withFillerNamespace
            ? new StringCollection { FillerNamespaceUri, SourceNamespaceUri }
            : new StringCollection { SourceNamespaceUri };
    }

    public StringCollection NamespacesUris { get; }

    internal PpiqSourceNodeManager? Instance { get; private set; }

    public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
    {
        Instance = new PpiqSourceNodeManager(server, configuration, NamespacesUris.ToArray());
        return Instance;
    }
}

/// <summary>
/// A small source address space: one folder, one readable analog variable with an
/// engineering unit, and one variable the server refuses to read.
/// </summary>
internal sealed class PpiqSourceNodeManager : CustomNodeManager2
{
    internal const string FolderIdentifier = "PpiqSource";
    internal const string TemperatureIdentifier = "PpiqSource.Temperature";
    internal const string UnreadableIdentifier = "PpiqSource.Unreadable";
    internal const string EngineeringUnitText = "degree Celsius";

    private BaseDataVariableState? _temperature;

    internal PpiqSourceNodeManager(
        IServerInternal server,
        ApplicationConfiguration configuration,
        string[] namespaceUris)
        : base(server, configuration, namespaceUris)
    {
    }

    internal ushort SourceNamespaceIndex
        => (ushort)Server.NamespaceUris.GetIndex(PpiqSourceNodeManagerFactory.SourceNamespaceUri);

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out IList<IReference>? references))
            {
                externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
            }

            ushort namespaceIndex = SourceNamespaceIndex;

            var root = new FolderState(null)
            {
                SymbolicName = FolderIdentifier,
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = ObjectTypeIds.FolderType,
                NodeId = new NodeId(FolderIdentifier, namespaceIndex),
                BrowseName = new QualifiedName(FolderIdentifier, namespaceIndex),
                DisplayName = new LocalizedText("en", "PPIQ Source"),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                EventNotifier = EventNotifiers.None
            };

            root.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
            references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, root.NodeId));

            var temperature = new BaseDataVariableState(root)
            {
                SymbolicName = "Temperature",
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                NodeId = new NodeId(TemperatureIdentifier, namespaceIndex),
                BrowseName = new QualifiedName("Temperature", namespaceIndex),
                DisplayName = new LocalizedText("en", "Temperature"),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                DataType = DataTypeIds.Double,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentRead,
                UserAccessLevel = AccessLevels.CurrentRead,
                Historizing = false,
                MinimumSamplingInterval = 100
            };

            temperature.Value = 21.5d;
            temperature.StatusCode = StatusCodes.Good;
            temperature.Timestamp = DateTime.UtcNow;
            root.AddChild(temperature);

            var engineeringUnits = new PropertyState<EUInformation>(temperature)
            {
                SymbolicName = BrowseNames.EngineeringUnits,
                ReferenceTypeId = ReferenceTypeIds.HasProperty,
                TypeDefinitionId = VariableTypeIds.PropertyType,
                NodeId = new NodeId(TemperatureIdentifier + ".EngineeringUnits", namespaceIndex),
                BrowseName = new QualifiedName(BrowseNames.EngineeringUnits, 0),
                DisplayName = new LocalizedText("en", BrowseNames.EngineeringUnits),
                DataType = DataTypeIds.EUInformation,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentRead,
                UserAccessLevel = AccessLevels.CurrentRead,
                Value = new EUInformation
                {
                    NamespaceUri = "http://www.opcfoundation.org/UA/units/un/cefact",
                    UnitId = 4408652,
                    DisplayName = new LocalizedText("en", EngineeringUnitText),
                    Description = new LocalizedText("en", "degree Celsius")
                }
            };

            temperature.AddChild(engineeringUnits);

            var unreadable = new BaseDataVariableState(root)
            {
                SymbolicName = "Unreadable",
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                NodeId = new NodeId(UnreadableIdentifier, namespaceIndex),
                BrowseName = new QualifiedName("Unreadable", namespaceIndex),
                DisplayName = new LocalizedText("en", "Unreadable"),
                DataType = DataTypeIds.Double,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.None,
                UserAccessLevel = AccessLevels.None,
                Historizing = false
            };

            unreadable.Value = 0d;
            unreadable.StatusCode = StatusCodes.Good;
            root.AddChild(unreadable);

            _temperature = temperature;

            AddPredefinedNode(SystemContext, root);
        }
    }

    /// <summary>Moves the source value so a subscription has something real to deliver.</summary>
    internal void SetTemperature(double value)
    {
        lock (Lock)
        {
            if (_temperature is null)
            {
                return;
            }

            _temperature.Value = value;
            _temperature.Timestamp = DateTime.UtcNow;
            _temperature.ClearChangeMasks(SystemContext, false);
        }
    }
}