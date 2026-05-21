namespace PlantProcess.Application.Licensing.Contracts;

public sealed record LicenseLimits(
    LicenseTier Tier,
    int? MaxUsers,
    int? MaxDataSources,
    int? MaxScheduledJobs,
    int? MaxDashboards,
    int? MinRefreshIntervalMinutes,
    int? MaxPreviewRows,
    bool AllowsSqlEditor,
    bool AllowsKpiBuilder,
    bool AllowsWidgetScriptLayer,
    bool AllowsScheduledCorrelation,
    bool AllowsMlLearningJobs,
    bool AllowsBrandedReports)
{
    public bool HasUnlimitedDataSources => MaxDataSources is null;
    public bool HasUnlimitedScheduledJobs => MaxScheduledJobs is null;
    public bool HasUnlimitedDashboards => MaxDashboards is null;
}