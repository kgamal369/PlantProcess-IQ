namespace PlantProcess.Application.Licensing.Contracts;

public sealed record LicenseStatusDto(
    string Tier,
    string DisplayName,
    bool IsTrial,
    string Environment,
    string Source,
    DateTime EffectiveFromUtc,
    LicenseLimits Limits,
    IReadOnlyCollection<LicenseFeatureStatusDto> Features,
    IReadOnlyCollection<string> AllowedConnectorProviderTypes,
    IReadOnlyCollection<string> BlockedConnectorProviderTypes);