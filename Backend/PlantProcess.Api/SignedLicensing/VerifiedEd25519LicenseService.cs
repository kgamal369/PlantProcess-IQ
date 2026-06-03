using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;

namespace PlantProcess.Api.SignedLicensing;

public sealed class VerifiedEd25519LicenseService : ILicenseService
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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

    private static readonly IReadOnlyDictionary<string, LicenseTier> RequiredTierByConnector =
        new Dictionary<string, LicenseTier>(StringComparer.OrdinalIgnoreCase)
        {
            ["csv"] = LicenseTier.Light,
            ["excel"] = LicenseTier.Light,
            ["postgres"] = LicenseTier.Pro,
            ["postgresql"] = LicenseTier.Pro,
            ["sqlserver"] = LicenseTier.Enterprise,
            ["mssql"] = LicenseTier.Enterprise,
            ["oracle"] = LicenseTier.Enterprise,
            ["mysql"] = LicenseTier.Enterprise,
            ["rest"] = LicenseTier.Enterprise,
            ["api"] = LicenseTier.Enterprise,
            ["opc"] = LicenseTier.Enterprise,
            ["opcua"] = LicenseTier.Enterprise,
            ["historian"] = LicenseTier.Enterprise
        };

    private readonly NpgsqlDataSource _dataSource;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<VerifiedEd25519LicenseService> _logger;

    public VerifiedEd25519LicenseService(
        NpgsqlDataSource dataSource,
        IHttpContextAccessor httpContextAccessor,
        ILogger<VerifiedEd25519LicenseService> logger)
    {
        _dataSource = dataSource;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public LicenseTier GetCurrentTier()
    {
        return LoadCurrentLicense().Tier;
    }

    public LicenseStatusDto GetStatus()
    {
        var current = LoadCurrentLicense();
        var limits = GetLimitsForTier(current.Tier);

        var features = Enum
            .GetValues<LicenseFeature>()
            .Select(feature => new LicenseFeatureStatusDto(
                Feature: feature.ToString(),
                IsEnabled: IsFeatureEnabled(feature),
                RequiredTier: GetRequiredTierForFeature(feature),
                Message: IsFeatureEnabled(feature)
                    ? "Available from the verified Ed25519 license."
                    : $"Requires {GetRequiredTierForFeature(feature)} license tier."))
            .OrderBy(x => x.RequiredTier)
            .ThenBy(x => x.Feature)
            .ToList();

        return new LicenseStatusDto(
            Tier: current.Tier.ToString(),
            DisplayName: current.DisplayName,
            IsTrial: false,
            Environment: Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            Source: current.Source,
            EffectiveFromUtc: current.IssuedAtUtc.UtcDateTime,
            Limits: limits,
            Features: features,
            AllowedConnectorProviderTypes: GetAllowedConnectorProviderTypes(),
            BlockedConnectorProviderTypes: GetBlockedConnectorProviderTypes());
    }

    public LicenseLimits GetLimits()
    {
        return GetLimitsForTier(GetCurrentTier());
    }

    public bool IsFeatureEnabled(LicenseFeature feature)
    {
        if (!RequiredTierByFeature.TryGetValue(feature, out var required))
            return false;

        return GetCurrentTier() >= required;
    }

    public ApplicationResult EnsureFeatureEnabled(LicenseFeature feature)
    {
        if (IsFeatureEnabled(feature))
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Feature '{feature}' requires {GetRequiredTierForFeature(feature)} license tier. Current verified license tier is {GetCurrentTier()}."));
    }

    public ApplicationResult EnsureConnectorAllowed(string providerType)
    {
        var normalized = NormalizeProvider(providerType);

        if (!RequiredTierByConnector.TryGetValue(normalized, out var required))
        {
            required = LicenseTier.Enterprise;
        }

        var current = GetCurrentTier();

        if (current >= required)
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Connector provider '{providerType}' requires {required} license tier. Current verified license tier is {current}."));
    }

    public ApplicationResult EnsureRefreshIntervalAllowed(int? intervalMinutes)
    {
        if (!intervalMinutes.HasValue)
            return ApplicationResult.Success();

        var limits = GetLimits();

        if (!limits.MinRefreshIntervalMinutes.HasValue)
            return ApplicationResult.Success();

        if (intervalMinutes.Value >= limits.MinRefreshIntervalMinutes.Value)
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Refresh interval {intervalMinutes.Value} minute(s) is below the verified license minimum of {limits.MinRefreshIntervalMinutes.Value} minute(s)."));
    }

    public ApplicationResult EnsureSourceCountAllowed(int currentActiveSourceCount)
    {
        var limits = GetLimits();

        if (!limits.MaxDataSources.HasValue)
            return ApplicationResult.Success();

        if (currentActiveSourceCount < limits.MaxDataSources.Value)
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Data-source limit reached. Verified license allows {limits.MaxDataSources.Value} source(s)."));
    }

    public ApplicationResult EnsureJobCountAllowed(int currentActiveJobCount)
    {
        var limits = GetLimits();

        if (!limits.MaxScheduledJobs.HasValue)
            return ApplicationResult.Success();

        if (currentActiveJobCount < limits.MaxScheduledJobs.Value)
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Scheduled-job limit reached. Verified license allows {limits.MaxScheduledJobs.Value} scheduled job(s)."));
    }

    public ApplicationResult EnsureDashboardCountAllowed(int currentActiveDashboardCount)
    {
        var limits = GetLimits();

        if (!limits.MaxDashboards.HasValue)
            return ApplicationResult.Success();

        if (currentActiveDashboardCount < limits.MaxDashboards.Value)
            return ApplicationResult.Success();

        return ApplicationResult.Failure(ApplicationError.Forbidden(
            $"Dashboard limit reached. Verified license allows {limits.MaxDashboards.Value} dashboard(s)."));
    }

    public string GetRequiredTierForFeature(LicenseFeature feature)
    {
        return RequiredTierByFeature.TryGetValue(feature, out var required)
            ? required.ToString()
            : LicenseTier.Enterprise.ToString();
    }

    public bool IsConnectorAvailableInCurrentTier(string providerType)
    {
        var normalized = NormalizeProvider(providerType);
        var current = GetCurrentTier();

        if (!RequiredTierByConnector.TryGetValue(normalized, out var required))
            required = LicenseTier.Enterprise;

        return current >= required;
    }

    public IReadOnlyCollection<string> GetAllowedConnectorProviderTypes()
    {
        var current = GetCurrentTier();

        return RequiredTierByConnector
            .Where(x => current >= x.Value)
            .Select(x => x.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public IReadOnlyCollection<string> GetBlockedConnectorProviderTypes()
    {
        var current = GetCurrentTier();

        return RequiredTierByConnector
            .Where(x => current < x.Value)
            .Select(x => x.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private CurrentLicense LoadCurrentLicense()
    {
        var tenantId = ResolveTenantId();

        try
        {
            using var connection = _dataSource.OpenConnection();

            using (var tenantCmd = connection.CreateCommand())
            {
                tenantCmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
                tenantCmd.Parameters.AddWithValue("tenant_id", tenantId.ToString());
                tenantCmd.ExecuteNonQuery();
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    license_key,
                    tier,
                    issued_at_utc,
                    expires_at_utc,
                    limits_json::text
                FROM public.ppiq_v_ed25519_current_entitlements
                WHERE tenant_id = @tenant_id
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                return CurrentLicense.SafeLight(
                    "VerifiedEd25519License:NoActiveLicense",
                    "No active verified Ed25519 license is activated; falling back to safe Light tier.");
            }

            var licenseKey = reader.GetString(0);
            var tierText = reader.GetString(1);
            var issuedAt = reader.GetDateTime(2);
            var expiresAt = reader.GetDateTime(3);
            var limitsJson = reader.GetString(4);

            if (!Enum.TryParse<LicenseTier>(NormalizeTier(tierText), ignoreCase: true, out var tier))
            {
                return CurrentLicense.SafeLight(
                    "VerifiedEd25519License:InvalidTier",
                    $"Verified license '{licenseKey}' has invalid tier '{tierText}'.");
            }

            return new CurrentLicense(
                Tier: tier,
                LicenseKey: licenseKey,
                Source: "VerifiedEd25519License",
                DisplayName: $"Verified {tier} License",
                IssuedAtUtc: new DateTimeOffset(DateTime.SpecifyKind(issuedAt, DateTimeKind.Utc)),
                ExpiresAtUtc: new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)),
                LimitsJson: limitsJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Verified Ed25519 license resolver could not read current license. Falling back to safe Light tier.");

            return CurrentLicense.SafeLight(
                "VerifiedEd25519License:ReadFailure",
                "License source unavailable; falling back to safe Light tier.");
        }
    }

    private LicenseLimits GetLimitsForTier(LicenseTier tier)
    {
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
                MaxDataSources: null,
                MaxScheduledJobs: null,
                MaxDashboards: null,
                MinRefreshIntervalMinutes: 1,
                MaxPreviewRows: 10000,
                AllowsSqlEditor: true,
                AllowsKpiBuilder: true,
                AllowsWidgetScriptLayer: true,
                AllowsScheduledCorrelation: true,
                AllowsMlLearningJobs: true,
                AllowsBrandedReports: true),

            _ => GetLimitsForTier(LicenseTier.Light)
        };
    }

    private Guid ResolveTenantId()
    {
        var http = _httpContextAccessor.HttpContext;

        var claimValue =
            http?.User.FindFirst("tenant_id")?.Value ??
            http?.User.FindFirst("tenantId")?.Value ??
            DefaultTenantId.ToString();

        return Guid.TryParse(claimValue, out var tenantId)
            ? tenantId
            : DefaultTenantId;
    }

    private static string NormalizeProvider(string providerType)
    {
        var text = (providerType ?? string.Empty).Trim().ToLowerInvariant();

        if (text.Contains("postgres")) return "postgresql";
        if (text.Contains("sqlserver") || text.Contains("mssql")) return "sqlserver";
        if (text.Contains("oracle")) return "oracle";
        if (text.Contains("mysql")) return "mysql";
        if (text.Contains("excel")) return "excel";
        if (text.Contains("csv")) return "csv";
        if (text.Contains("rest") || text.Contains("api")) return "rest";
        if (text.Contains("opc")) return "opcua";
        if (text.Contains("historian")) return "historian";

        return text;
    }

    private static string NormalizeTier(string tier)
    {
        return string.Equals(tier, "Pro Plus", StringComparison.OrdinalIgnoreCase)
            ? "ProPlus"
            : tier;
    }

    private sealed record CurrentLicense(
        LicenseTier Tier,
        string LicenseKey,
        string Source,
        string DisplayName,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        string LimitsJson)
    {
        public static CurrentLicense SafeLight(string source, string message)
        {
            return new CurrentLicense(
                Tier: LicenseTier.Light,
                LicenseKey: "NO-ACTIVE-VERIFIED-LICENSE",
                Source: source,
                DisplayName: $"Safe Light Fallback - {message}",
                IssuedAtUtc: DateTimeOffset.UtcNow,
                ExpiresAtUtc: DateTimeOffset.UtcNow,
                LimitsJson: "{}");
        }
    }
}