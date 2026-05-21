namespace PlantProcess.Application.Licensing.Contracts;

public sealed record LicenseFeatureStatusDto(
    string Feature,
    bool IsEnabled,
    string RequiredTier,
    string Message);