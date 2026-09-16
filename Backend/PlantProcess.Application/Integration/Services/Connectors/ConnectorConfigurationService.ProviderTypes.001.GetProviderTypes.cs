using PlantProcess.Application.Integration.Connectors;
using PlantProcess.Application.Integration.Contracts.Dtos;

namespace PlantProcess.Application.Integration.Services.Connectors;

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_PROVIDERTYPES_SPLIT
//
// T-207 corrective. This file used to carry a second hand-written provider list. It
// disagreed with ConnectorProviderCatalog - which the /provider-types route serves - about
// whether the historian supports schema discovery, and about provider descriptions, while
// being the list profile creation actually validated against. Two lists, one of them
// invisible to the customer, both claiming to be the truth.
//
// There is now one producer. This delegates to it and adds nothing.
public sealed partial class ConnectorConfigurationService
{
    public IReadOnlyList<ProviderTypeDto> GetProviderTypes()
        => ConnectorProviderCatalog.GetProviderTypes();
}
