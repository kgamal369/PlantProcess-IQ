using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Licensing.Contracts;

namespace PlantProcess.Application.Licensing.Interfaces;

public interface ILicenseService
{
    LicenseTier GetCurrentTier();

    LicenseStatusDto GetStatus();

    LicenseLimits GetLimits();

    bool IsFeatureEnabled(LicenseFeature feature);

    ApplicationResult EnsureFeatureEnabled(LicenseFeature feature);

    ApplicationResult EnsureConnectorAllowed(string providerType);

    ApplicationResult EnsureRefreshIntervalAllowed(int? intervalMinutes);

    ApplicationResult EnsureSourceCountAllowed(int currentActiveSourceCount);

    ApplicationResult EnsureJobCountAllowed(int currentActiveJobCount);

    ApplicationResult EnsureDashboardCountAllowed(int currentActiveDashboardCount);

    string GetRequiredTierForFeature(LicenseFeature feature);

    bool IsConnectorAvailableInCurrentTier(string providerType);

    IReadOnlyCollection<string> GetAllowedConnectorProviderTypes();

    IReadOnlyCollection<string> GetBlockedConnectorProviderTypes();
}