// T-207 corrective. The historian capability truth, in a layer every surface can read.
//
// The registry lived in the API project, so the connector in the infrastructure layer could
// not reach it and carried its own literals instead - which is how metadata came to
// advertise tag browse and bounded read while the routes answered 501 for both. A truth
// source that only one layer can see is not a truth source.
//
// The HTTP shaping of a refusal stays in the API layer. Only the facts live here.
using System;
using System.Collections.Generic;

namespace PlantProcess.Application.Integration.Connectors;

/// <summary>One declared connector capability and the evidence for its state.</summary>
public sealed record HistorianCapability(string Name, bool Executable, string Evidence);

public static class HistorianCapabilityRegistry
{
    public const string ConfigurationValidation = "configurationValidation";
    public const string MappingHintsFromSuppliedTagPaths = "mappingHintsFromSuppliedTagPaths";
    public const string TagBrowse = "tagBrowse";
    public const string BoundedRead = "boundedRead";
    public const string Subscription = "subscription";
    public const string LiveVendorHandshake = "liveVendorHandshake";

    /// <summary>
    /// A capability flips to Executable only together with an implementation a contract
    /// test can prove. The live vendor handshake is executable because the customer-side
    /// collector session runtime exists and its acceptance suite runs against a real SDK
    /// server. Tag browse, bounded read and subscription stay false until the tasks that
    /// own those operations deliver them. A session fact never implies them.
    /// </summary>
    public static readonly IReadOnlyList<HistorianCapability> All = new[]
    {
        new HistorianCapability(
            ConfigurationValidation, true,
            "The test-connection route validates the supplied endpoint and read-only posture and returns no measurement."),
        new HistorianCapability(
            MappingHintsFromSuppliedTagPaths, true,
            "The mapping-hints route classifies tag paths the caller supplied. It never supplies tag paths of its own."),
        new HistorianCapability(
            TagBrowse, false,
            "Real OPC UA namespace browse is not yet implemented."),
        new HistorianCapability(
            BoundedRead, false,
            "Real OPC UA value acquisition is not yet implemented."),
        new HistorianCapability(
            Subscription, false,
            "Real OPC UA monitored items and subscriptions are not yet implemented."),
        new HistorianCapability(
            LiveVendorHandshake, true,
            "The customer-side collector establishes a real OPC UA session: endpoint discovery with exact " +
            "security policy and message security mode, application certificate, explicit server trust, " +
            "user identity and reconnect. Proven by the collector session acceptance suite against an SDK " +
            "server. Core opens no session, holds no plant credential and executes this operation nowhere.")
    };

    public static bool IsExecutable(string name)
    {
        foreach (var capability in All)
        {
            if (string.Equals(capability.Name, name, StringComparison.Ordinal))
            {
                return capability.Executable;
            }
        }

        return false;
    }

    /// <summary>The literal a metadata dictionary should carry for a declared capability.</summary>
    public static string DeclaredValue(string name) => IsExecutable(name) ? "true" : "false";

    public static HistorianCapability Get(string name)
    {
        foreach (var capability in All)
        {
            if (string.Equals(capability.Name, name, StringComparison.Ordinal))
            {
                return capability;
            }
        }

        throw new InvalidOperationException(
            "Connector capability '" + name + "' is not registered. Register it before any surface may advertise it.");
    }
}
