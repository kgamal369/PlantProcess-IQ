namespace PlantProcess.Application.Licensing.Options;

public sealed class LicenseOptions
{
    public const string SectionName = "PlantProcess:License";

    public string Tier { get; set; } = "ProPlus";

    public string? DisplayName { get; set; }

    public bool IsTrial { get; set; } = false;

    public int? EnterpriseMinRefreshIntervalMinutes { get; set; }

    public int? EnterpriseMaxDataSources { get; set; }

    public int? EnterpriseMaxScheduledJobs { get; set; }

    public int? EnterpriseMaxDashboards { get; set; }
}