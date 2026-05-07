using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.PlantLayout;

public class Site : BaseEntity
{
    public string SiteCode { get; private set; } = null!;

    public string SiteName { get; private set; } = null!;

    public string? CompanyName { get; private set; }

    public string? CountryCode { get; private set; }

    public string TimeZoneId { get; private set; } = "Europe/Berlin";

    private Site()
    {
    }

    public Site(
        string siteCode,
        string siteName,
        bool isSynthetic,
        string? companyName = null,
        string? countryCode = null,
        string timeZoneId = "Europe/Berlin",
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(siteCode))
            throw new ArgumentException("Site code is required.", nameof(siteCode));

        if (string.IsNullOrWhiteSpace(siteName))
            throw new ArgumentException("Site name is required.", nameof(siteName));

        SiteCode = siteCode.Trim();
        SiteName = siteName.Trim();
        CompanyName = companyName?.Trim();
        CountryCode = countryCode?.Trim();
        TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId)
            ? "Europe/Berlin"
            : timeZoneId.Trim();

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }
}