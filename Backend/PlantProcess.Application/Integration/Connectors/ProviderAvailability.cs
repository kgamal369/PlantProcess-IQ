namespace PlantProcess.Application.Integration.Connectors;

/// <summary>
/// PPIQ-T10: THE single source for "is this connector provider available now".
/// Before this class, three field-ordered arrays carried independent IsAvailableNow
/// literals (the truth dashboard, the configuration service, and the catalog) - a
/// certification flip had to be edited three times or the surfaces disagreed.
/// Rules: Csv and Excel are always available (file snapshot connectors, demo-certified
/// from day one). Every other provider follows ConnectorCertification, which is gated
/// per provider via PPIQ_CONNECTOR_CERTIFIED_&lt;PROVIDER&gt; - flipping availability is an
/// environment change, never a code edit.
/// </summary>
public static class ProviderAvailability
{
    private static readonly HashSet<string> AlwaysAvailable =
        new(StringComparer.OrdinalIgnoreCase) { "Csv", "Excel" };

    public static bool IsAvailableNow(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
            return false;

        if (AlwaysAvailable.Contains(providerType))
            return true;

        return ConnectorCertification.IsCertified(providerType);
    }
}