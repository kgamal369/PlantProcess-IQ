namespace PlantProcess.Application.Licensing.Contracts;

public sealed record LicensePlan(
    LicenseTier Tier,
    string DisplayName,
    string Description,
    IReadOnlyCollection<LicenseFeature> EnabledFeatures,
    LicenseLimits Limits);