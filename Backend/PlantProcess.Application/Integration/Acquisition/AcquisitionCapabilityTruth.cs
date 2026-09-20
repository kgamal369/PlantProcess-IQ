// Required source operations, answered by the existing connector truth.
//
// This is an adapter, not a registry. It declares no capability of its own: every
// answer is read from ConnectorProviderCatalog, ProviderAvailability (through the
// catalogue) and HistorianCapabilityRegistry. An operation none of them declares is
// not executable, which is the honest answer rather than a gap to fill.
using PlantProcess.Application.Integration.Connectors;

namespace PlantProcess.Application.Integration.Acquisition;

/// <summary>One required operation and what connector truth says about it.</summary>
public sealed record AcquisitionOperationTruth(
    string Operation,
    bool DeclaredExecutable,
    bool EnvironmentAvailable,
    string DeclaredEvidence,
    string AvailabilityEvidence)
{
    public bool Executable => DeclaredExecutable && EnvironmentAvailable;

    public string Evidence => !DeclaredExecutable
        ? DeclaredEvidence
        : !EnvironmentAvailable
            ? AvailabilityEvidence
            : DeclaredEvidence + " " + AvailabilityEvidence;
}

public static class AcquisitionCapabilityTruth
{
    public const string BoundedRead = "boundedRead";
    public const string Subscription = "subscription";
    public const string IncrementalImport = "incrementalImport";
    public const string FileArrival = "fileArrival";
    public const string SourceVersionRecord = "sourceVersionRecord";
    public const string SourceLatchedRecord = "sourceLatchedRecord";

    public const string HistorianProviderType = "OpcUaHistorian";

    public static readonly IReadOnlyList<string> Operations = new[]
    {
        BoundedRead, Subscription, IncrementalImport, FileArrival, SourceVersionRecord, SourceLatchedRecord
    };

    /// <summary>
    /// Authored capability and current-environment availability are deliberately separate.
    /// A published definition is portable authored intent; whether this installation can
    /// activate it now is runtime evidence, not part of the immutable authored identity.
    /// </summary>
    public static AcquisitionOperationTruth Evaluate(string? providerType, string operation)
    {
        if (!Operations.Contains(operation, StringComparer.Ordinal))
        {
            return Truth(operation, false, false,
                "The operation is not part of the acquisition contract.",
                "Environment availability is irrelevant because the operation is undeclared.");
        }

        if (!ConnectorProviderCatalog.HasRuntimeImplementation(providerType))
        {
            return Truth(operation, false, false,
                "Provider '" + (providerType ?? string.Empty) + "' has no runtime implementation in this build.",
                "No executable provider exists to certify in this environment.");
        }

        var provider = ConnectorProviderCatalog.GetProviderTypes()
            .FirstOrDefault(p => string.Equals(p.ProviderType, providerType!.Trim(), StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return Truth(operation, false, false,
                "Provider '" + providerType + "' is not listed by the connector catalogue.",
                "No catalogue provider exists to certify in this environment.");
        }

        var declared = DeclaredTruth(provider, operation);
        var available = provider.IsAvailableNow;
        var availabilityEvidence = available
            ? "Provider '" + provider.ProviderType + "' is available in this environment."
            : "Provider '" + provider.ProviderType + "' is not certified in this environment (" +
              ConnectorCertification.BuildEnvironmentKey(provider.ProviderType) + " is not set).";

        return Truth(operation, declared.DeclaredExecutable, available,
            declared.DeclaredEvidence, availabilityEvidence);
    }

    private static AcquisitionOperationTruth DeclaredTruth(
        PlantProcess.Application.Integration.Contracts.Dtos.ProviderTypeDto provider,
        string operation)
    {
        if (string.Equals(provider.ProviderType, HistorianProviderType, StringComparison.Ordinal))
        {
            var registryName = operation switch
            {
                BoundedRead => HistorianCapabilityRegistry.BoundedRead,
                Subscription => HistorianCapabilityRegistry.Subscription,
                _ => null
            };

            if (registryName is null)
            {
                return Truth(operation, false, true,
                    "Connector truth declares no '" + operation + "' operation for this provider.",
                    "Environment availability is evaluated separately.");
            }

            var capability = HistorianCapabilityRegistry.Get(registryName);
            return Truth(operation, capability.Executable, true,
                capability.Evidence, "Environment availability is evaluated separately.");
        }

        return operation switch
        {
            BoundedRead => Truth(operation, provider.SupportsSnapshotImport, true,
                provider.SupportsSnapshotImport
                    ? "The connector catalogue declares snapshot reads for this provider."
                    : "The connector catalogue declares no snapshot read for this provider.",
                "Environment availability is evaluated separately."),
            IncrementalImport => Truth(operation, provider.SupportsIncrementalImport, true,
                provider.SupportsIncrementalImport
                    ? "The connector catalogue declares incremental import for this provider."
                    : "The connector catalogue declares no incremental import for this provider.",
                "Environment availability is evaluated separately."),
            _ => Truth(operation, false, true,
                "Connector truth declares no '" + operation + "' operation for this provider.",
                "Environment availability is evaluated separately.")
        };
    }

    private static AcquisitionOperationTruth Truth(
        string operation,
        bool declaredExecutable,
        bool environmentAvailable,
        string declaredEvidence,
        string availabilityEvidence) =>
        new(operation, declaredExecutable, environmentAvailable, declaredEvidence, availabilityEvidence);
}
