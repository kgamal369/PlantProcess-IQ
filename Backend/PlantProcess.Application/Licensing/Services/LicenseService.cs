using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;
using PlantProcess.Application.Licensing.Options;

namespace PlantProcess.Application.Licensing.Services;

public sealed class LicenseService : ILicenseService
{
    private static readonly IReadOnlyDictionary<LicenseFeature, LicenseTier> RequiredTierByFeature =
        new Dictionary<LicenseFeature, LicenseTier>
        {
            [LicenseFeature.ReadOnlySourceRegistry] = LicenseTier.Light,

            [LicenseFeature.CsvImport] = LicenseTier.Light,
            [LicenseFeature.ExcelImport] = LicenseTier.Light,
            [LicenseFeature.PostgreSqlConnector] = LicenseTier.Pro,
            [LicenseFeature.SqlServerConnector] = LicenseTier.Enterprise,
            [LicenseFeature.OracleConnector] = LicenseTier.Enterprise,
            [LicenseFeature.MySqlConnector] = LicenseTier.Enterprise,
            [LicenseFeature.RestApiConnector] = LicenseTier.Enterprise,
            [LicenseFeature.OpcUaHistorianConnector] = LicenseTier.Enterprise,

            [LicenseFeature.DbLinkConfiguration] = LicenseTier.Light,
            [LicenseFeature.DumpStagingRetention] = LicenseTier.Light,
            [LicenseFeature.SourceSnapshotImport] = LicenseTier.Light,
            [LicenseFeature.IncrementalImport] = LicenseTier.Pro,

            [LicenseFeature.SchemaSqlViewBuilder] = LicenseTier.Pro,
            [LicenseFeature.CrossSourceJoinExecution] = LicenseTier.Pro,
            [LicenseFeature.KpiViewBuilder] = LicenseTier.ProPlus,
            [LicenseFeature.SchemaPreviewExecution] = LicenseTier.Pro,
            [LicenseFeature.MappingExecution] = LicenseTier.Pro,

            [LicenseFeature.WidgetScriptLayer] = LicenseTier.ProPlus,
            [LicenseFeature.DashboardPageBuilder] = LicenseTier.Light,
            [LicenseFeature.DashboardWidgetBuilder] = LicenseTier.Light,
            [LicenseFeature.DashboardLayoutPersistence] = LicenseTier.Light,

            [LicenseFeature.DataQualityBasicScan] = LicenseTier.Light,
            [LicenseFeature.DataQualityFullScan] = LicenseTier.Pro,

            [LicenseFeature.RiskDashboardView] = LicenseTier.Pro,
            [LicenseFeature.RiskDashboardContributors] = LicenseTier.ProPlus,

            [LicenseFeature.CorrelationManualRun] = LicenseTier.Pro,
            [LicenseFeature.CorrelationScheduledRun] = LicenseTier.ProPlus,

            [LicenseFeature.InvestigationWorkflow] = LicenseTier.ProPlus,

            [LicenseFeature.MlWorkspacePreview] = LicenseTier.ProPlus,
            [LicenseFeature.MlLearningJobs] = LicenseTier.ProPlus,
            [LicenseFeature.SuggestionRecommendation] = LicenseTier.ProPlus,

            [LicenseFeature.BasicInvestigationReportPdf] = LicenseTier.Pro,
            [LicenseFeature.FullGenealogyReportPdf] = LicenseTier.ProPlus,
            [LicenseFeature.BrandedReportPdf] = LicenseTier.Enterprise
        };

    private readonly LicenseOptions _options;
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(
        IOptions<LicenseOptions> options,
        ILogger<LicenseService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public LicenseTier GetCurrentTier()
    {
        if (Enum.TryParse<LicenseTier>(_options.Tier, ignoreCase: true, out var tier))
            return tier;

        _logger.LogWarning(
            "Invalid PlantProcess license tier configured: {Tier}. Falling back to ProPlus for controlled demo safety.",
            _options.Tier);

        return LicenseTier.ProPlus;
    }

    public LicenseStatusDto GetStatus()
    {
        var tier = GetCurrentTier();
        var limits = GetLimits();

        var features = Enum
            .GetValues<LicenseFeature>()
            .Select(feature => new LicenseFeatureStatusDto(
                Feature: feature.ToString(),
                IsEnabled: IsFeatureEnabled(feature),
                RequiredTier: GetRequiredTierForFeature(feature),
                Message: IsFeatureEnabled(feature)
                    ? "Available in the current license tier."
                    : $"Requires {GetRequiredTierForFeature(feature)} license tier."))
            .OrderBy(x => x.RequiredTier)
            .ThenBy(x => x.Feature)
            .ToList();

        return new LicenseStatusDto(
            Tier: tier.ToString(),
            DisplayName: GetDisplayName(tier),
            IsTrial: _options.IsTrial,
            Environment: ResolveEnvironmentName(),
            Source: "Configuration",
            EffectiveFromUtc: DateTime.UtcNow.Date,
            Limits: limits,
            Features: features,
            AllowedConnectorProviderTypes: GetAllowedConnectorProviderTypes(),
            BlockedConnectorProviderTypes: GetBlockedConnectorProviderTypes());
    }

    public LicenseLimits GetLimits()
    {
        var tier = GetCurrentTier();

        return tier switch
        {
            LicenseTier.Light => new LicenseLimits(
                Tier: tier,
                MaxUsers: 3,
                MaxDataSources: 1,
                MaxScheduledJobs: 1,
                MaxDashboards: 3,
                MinRefreshIntervalMinutes: 1440,
                MaxPreviewRows: 100,
                AllowsSqlEditor: false,
                AllowsKpiBuilder: false,
                AllowsWidgetScriptLayer: false,
                AllowsScheduledCorrelation: false,
                AllowsMlLearningJobs: false,
                AllowsBrandedReports: false),

            LicenseTier.Pro => new LicenseLimits(
                Tier: tier,
                MaxUsers: 10,
                MaxDataSources: 3,
                MaxScheduledJobs: 5,
                MaxDashboards: 8,
                MinRefreshIntervalMinutes: 15,
                MaxPreviewRows: 1000,
                AllowsSqlEditor: true,
                AllowsKpiBuilder: false,
                AllowsWidgetScriptLayer: false,
                AllowsScheduledCorrelation: false,
                AllowsMlLearningJobs: false,
                AllowsBrandedReports: false),

            LicenseTier.ProPlus => new LicenseLimits(
                Tier: tier,
                MaxUsers: 25,
                MaxDataSources: 8,
                MaxScheduledJobs: 25,
                MaxDashboards: 20,
                MinRefreshIntervalMinutes: 2,
                MaxPreviewRows: 5000,
                AllowsSqlEditor: true,
                AllowsKpiBuilder: true,
                AllowsWidgetScriptLayer: true,
                AllowsScheduledCorrelation: true,
                AllowsMlLearningJobs: true,
                AllowsBrandedReports: false),

            LicenseTier.Enterprise => new LicenseLimits(
                Tier: tier,
                MaxUsers: null,
                MaxDataSources: _options.EnterpriseMaxDataSources,
                MaxScheduledJobs: _options.EnterpriseMaxScheduledJobs,
                MaxDashboards: _options.EnterpriseMaxDashboards,
                MinRefreshIntervalMinutes: _options.EnterpriseMinRefreshIntervalMinutes ?? 1,
                MaxPreviewRows: 10000,
                AllowsSqlEditor: true,
                AllowsKpiBuilder: true,
                AllowsWidgetScriptLayer: true,
                AllowsScheduledCorrelation: true,
                AllowsMlLearningJobs: true,
                AllowsBrandedReports: true),

            _ => throw new InvalidOperationException($"Unsupported license tier: {tier}")
        };
    }

    public bool IsFeatureEnabled(LicenseFeature feature)
    {
        var current = GetCurrentTier();

        if (!RequiredTierByFeature.TryGetValue(feature, out var required))
            return false;

        return current >= required;
    }

    public ApplicationResult EnsureFeatureEnabled(LicenseFeature feature)
    {
        if (IsFeatureEnabled(feature))
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Feature '{feature}' is not available in the current license tier '{GetCurrentTier()}'. Required tier: {GetRequiredTierForFeature(feature)}."));
    }

    public ApplicationResult EnsureConnectorAllowed(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
            return ApplicationResult.Failure(ApplicationError.Validation("ProviderType is required."));

        var feature = ResolveConnectorFeature(providerType);

        if (feature is null)
        {
            return ApplicationResult.Failure(ApplicationError.Forbidden(
                $"Connector provider '{providerType}' is not supported by the current commercial configuration."));
        }

        return EnsureFeatureEnabled(feature.Value);
    }

    public ApplicationResult EnsureRefreshIntervalAllowed(int? intervalMinutes)
    {
        var limits = GetLimits();

        if (intervalMinutes is null)
            return ApplicationResult.Success();

        if (intervalMinutes <= 0)
            return ApplicationResult.Failure(ApplicationError.Validation("Refresh interval must be greater than zero minutes."));

        if (limits.MinRefreshIntervalMinutes is null)
            return ApplicationResult.Success();

        if (intervalMinutes >= limits.MinRefreshIntervalMinutes.Value)
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Refresh interval '{intervalMinutes}' minutes is below the current license limit. Current tier: {limits.Tier}. Minimum allowed interval: {limits.MinRefreshIntervalMinutes} minutes."));
    }

    public ApplicationResult EnsureSourceCountAllowed(int currentActiveSourceCount)
    {
        var limits = GetLimits();

        if (limits.MaxDataSources is null)
            return ApplicationResult.Success();

        if (currentActiveSourceCount < limits.MaxDataSources.Value)
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Data source limit reached for current license tier '{limits.Tier}'. Allowed sources: {limits.MaxDataSources}. Current active sources: {currentActiveSourceCount}."));
    }

    public ApplicationResult EnsureJobCountAllowed(int currentActiveJobCount)
    {
        var limits = GetLimits();

        if (limits.MaxScheduledJobs is null)
            return ApplicationResult.Success();

        if (currentActiveJobCount < limits.MaxScheduledJobs.Value)
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Scheduled job limit reached for current license tier '{limits.Tier}'. Allowed jobs: {limits.MaxScheduledJobs}. Current active jobs: {currentActiveJobCount}."));
    }

    public ApplicationResult EnsureDashboardCountAllowed(int currentActiveDashboardCount)
    {
        var limits = GetLimits();

        if (limits.MaxDashboards is null)
            return ApplicationResult.Success();

        if (currentActiveDashboardCount < limits.MaxDashboards.Value)
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Dashboard/page limit reached for current license tier '{limits.Tier}'. Allowed dashboards/pages: {limits.MaxDashboards}. Current active dashboards/pages: {currentActiveDashboardCount}."));
    }

    public string GetRequiredTierForFeature(LicenseFeature feature)
    {
        return RequiredTierByFeature.TryGetValue(feature, out var tier)
            ? tier.ToString()
            : LicenseTier.Enterprise.ToString();
    }

    public bool IsConnectorAvailableInCurrentTier(string providerType)
    {
        var feature = ResolveConnectorFeature(providerType);
        return feature.HasValue && IsFeatureEnabled(feature.Value);
    }

    public IReadOnlyCollection<string> GetAllowedConnectorProviderTypes()
    {
        var providers = new[]
        {
            "Csv",
            "Excel",
            "PostgreSql",
            "SqlServer",
            "Oracle",
            "MySql",
            "RestApi",
            "OpcUaHistorian"
        };

        return providers
            .Where(IsConnectorAvailableInCurrentTier)
            .OrderBy(x => x)
            .ToList();
    }

    public IReadOnlyCollection<string> GetBlockedConnectorProviderTypes()
    {
        var providers = new[]
        {
            "Csv",
            "Excel",
            "PostgreSql",
            "SqlServer",
            "Oracle",
            "MySql",
            "RestApi",
            "OpcUaHistorian"
        };

        return providers
            .Where(x => !IsConnectorAvailableInCurrentTier(x))
            .OrderBy(x => x)
            .ToList();
    }

    private static LicenseFeature? ResolveConnectorFeature(string providerType)
    {
        return providerType.Trim().ToLowerInvariant() switch
        {
            "csv" => LicenseFeature.CsvImport,
            "excel" => LicenseFeature.ExcelImport,
            "xlsx" => LicenseFeature.ExcelImport,
            "postgresql" => LicenseFeature.PostgreSqlConnector,
            "postgres" => LicenseFeature.PostgreSqlConnector,
            "sqlserver" => LicenseFeature.SqlServerConnector,
            "mssql" => LicenseFeature.SqlServerConnector,
            "oracle" => LicenseFeature.OracleConnector,
            "mysql" => LicenseFeature.MySqlConnector,
            "restapi" => LicenseFeature.RestApiConnector,
            "rest" => LicenseFeature.RestApiConnector,
            "opcuahistorian" => LicenseFeature.OpcUaHistorianConnector,
            "opcua" => LicenseFeature.OpcUaHistorianConnector,
            "historian" => LicenseFeature.OpcUaHistorianConnector,
            _ => null
        };
    }

    private string GetDisplayName(LicenseTier tier)
    {
        if (!string.IsNullOrWhiteSpace(_options.DisplayName))
            return _options.DisplayName.Trim();

        return tier switch
        {
            LicenseTier.Light => "Light License",
            LicenseTier.Pro => "Pro License",
            LicenseTier.ProPlus => "Pro Plus License",
            LicenseTier.Enterprise => "Enterprise License",
            _ => tier.ToString()
        };
    }

    private static string ResolveEnvironmentName()
    {
        return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Unknown";
    }
}