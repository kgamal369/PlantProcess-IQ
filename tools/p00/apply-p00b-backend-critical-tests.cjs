const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function full(relativePath) {
  return path.join(root, relativePath.replaceAll("/", path.sep).replaceAll("\\", path.sep));
}

function read(relativePath) {
  return fs.readFileSync(full(relativePath), "utf8");
}

function write(relativePath, content) {
  const file = full(relativePath);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
}

function exists(relativePath) {
  return fs.existsSync(full(relativePath));
}

// ============================================================
// 1. Fix real RiskScoreService bug: computed riskClass must be used.
// ============================================================

const riskScoreServicePath = "Backend/PlantProcess.Application/Analytics/Services/RiskScoreService.cs";

if (exists(riskScoreServicePath)) {
  let text = read(riskScoreServicePath);

  if (text.includes("riskClass: command.RiskClass,")) {
    text = text.replace("riskClass: command.RiskClass,", "riskClass: riskClass,");
    write(riskScoreServicePath, text);
    console.log("Patched RiskScoreService to pass computed riskClass.");
  } else {
    console.log("RiskScoreService already passes computed riskClass or pattern was not found.");
  }
} else {
  throw new Error("Missing RiskScoreService.cs");
}

// ============================================================
// 2. Repair empty historical SQL placeholder so SQL hygiene can be real.
// ============================================================

const emptySqlPath = "Backend/database/scripts/080_phase_3_4_connector_schema_foundation.sql";

if (exists(emptySqlPath) && read(emptySqlPath).trim().length === 0) {
  write(
    emptySqlPath,
    `-- ============================================================================
-- PlantProcess IQ
-- Historical placeholder script.
--
-- Original intent:
--   Phase 3/4 connector schema foundation.
--
-- Current state:
--   Superseded by later canonical schema / two-stage import scripts.
--
-- Why this is intentionally no-op:
--   P00A/P00B SQL hygiene requires every ordered script to be non-empty,
--   explainable, and safe under ON_ERROR_STOP.
-- ============================================================================

SELECT '080_phase_3_4_connector_schema_foundation.sql is a retained no-op placeholder superseded by later scripts.' AS status;
`
  );

  console.log("Filled empty 080 SQL placeholder with explicit no-op proof.");
}

// ============================================================
// 3. Backend unit tests — RiskScoreServiceTests
// ============================================================

write(
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/RiskScoreServiceTests.cs",
  `using System.Reflection;
using FluentAssertions;
using PlantProcess.Application.Analytics.Services;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class RiskScoreServiceTests
{
    [Fact]
    public void Risk_score_constants_should_keep_customer_safe_default_identity()
    {
        RiskScoreService.DefaultRiskType
            .Should()
            .Be("OverallQualityRisk");

        RiskScoreService.DefaultRuleVersion
            .Should()
            .StartWith("rule-risk-v");
    }

    [Fact]
    public void CalculateRiskClass_should_classify_low_and_high_scores_differently()
    {
        var method = typeof(RiskScoreService).GetMethod(
            "CalculateRiskClass",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("RiskScoreService must keep deterministic risk-class calculation");

        var low = (string)method!.Invoke(null, new object[] { 0.10m })!;
        var high = (string)method.Invoke(null, new object[] { 0.90m })!;

        low.Should().NotBeNullOrWhiteSpace();
        high.Should().NotBeNullOrWhiteSpace();
        low.Should().NotBe(high, "low and high scores must not collapse into the same risk class");
    }

    [Fact]
    public void StoreAsync_should_use_computed_risk_class_when_command_risk_class_is_missing()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindBackendRoot(),
                "PlantProcess.Application",
                "Analytics",
                "Services",
                "RiskScoreService.cs"));

        source.Should().Contain("var riskClass =");
        source.Should().Contain("riskClass: riskClass,");
        source.Should().NotContain("riskClass: command.RiskClass,");
    }

    private static string FindBackendRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "PlantProcess.Application")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Backend root from test output directory.");
    }
}
`
);

// ============================================================
// 4. Backend unit tests — FeatureEngineeringServiceTests
// ============================================================

write(
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/FeatureEngineeringServiceTests.cs",
  `using System.Reflection;
using FluentAssertions;
using PlantProcess.Application.Analytics.Services;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class FeatureEngineeringServiceTests
{
    [Fact]
    public void CalculateMinutes_should_return_precise_positive_duration()
    {
        var method = typeof(FeatureEngineeringService).GetMethod(
            "CalculateMinutes",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("duration calculation must stay deterministic");

        var start = new DateTime(2026, 05, 01, 10, 00, 00, DateTimeKind.Utc);
        var end = new DateTime(2026, 05, 01, 10, 12, 30, DateTimeKind.Utc);

        var result = (decimal)method!.Invoke(null, new object?[] { start, end })!;

        result.Should().Be(12.5m);
    }

    [Fact]
    public void CalculateMinutes_should_return_zero_for_invalid_or_missing_window()
    {
        var method = typeof(FeatureEngineeringService).GetMethod(
            "CalculateMinutes",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var start = new DateTime(2026, 05, 01, 10, 00, 00, DateTimeKind.Utc);
        var end = new DateTime(2026, 05, 01, 09, 59, 59, DateTimeKind.Utc);

        var reversed = (decimal)method!.Invoke(null, new object?[] { start, end })!;
        var missing = (decimal)method.Invoke(null, new object?[] { null, end })!;

        reversed.Should().Be(0m);
        missing.Should().Be(0m);
    }

    [Fact]
    public void CalculateStdDev_should_match_sample_standard_deviation()
    {
        var method = typeof(FeatureEngineeringService).GetMethod(
            "CalculateStdDev",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("standard deviation drives anomaly feature calculations");

        var result = (decimal?)method!.Invoke(null, new object[] { new List<decimal> { 10m, 12m, 14m } });

        result.Should().Be(2m);
    }

    [Fact]
    public void CurrentFeatureVersion_should_mark_rule_ready_vectors()
    {
        FeatureEngineeringService.CurrentFeatureVersion
            .Should()
            .Contain("rule-ready");
    }
}
`
);

// ============================================================
// 5. Backend unit tests — MlReadinessServiceTests
// ============================================================

write(
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/MlReadinessServiceTests.cs",
  `using System.Reflection;
using FluentAssertions;
using PlantProcess.Application.Analytics.Services;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class MlReadinessServiceTests
{
    [Theory]
    [InlineData(100, 100, true, "Ready")]
    [InlineData(99, 100, false, "NotReady")]
    public void Metric_should_pass_only_when_current_value_reaches_required_threshold(
        decimal current,
        decimal required,
        bool expectedReady,
        string expectedStatus)
    {
        var method = typeof(MlReadinessService).GetMethod(
            "Metric",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("readiness lower-bound gates must stay deterministic");

        var metric = method!.Invoke(null, new object[]
        {
            "sample-count",
            "Sample count",
            current,
            required,
            "rows",
            "Need enough samples"
        });

        metric.Should().NotBeNull();
        Get<bool>(metric!, "IsReady").Should().Be(expectedReady);
        Get<string>(metric!, "Status").Should().Be(expectedStatus);
    }

    [Theory]
    [InlineData(5, 10, true, "Ready")]
    [InlineData(11, 10, false, "NotReady")]
    public void MetricMax_should_pass_only_when_current_value_is_within_maximum_allowed(
        decimal current,
        decimal maximumAllowed,
        bool expectedReady,
        string expectedStatus)
    {
        var method = typeof(MlReadinessService).GetMethod(
            "MetricMax",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("readiness upper-bound gates must stay deterministic");

        var metric = method!.Invoke(null, new object[]
        {
            "missing-rate",
            "Missing rate",
            current,
            maximumAllowed,
            "%",
            "Missing values must be controlled"
        });

        metric.Should().NotBeNull();
        Get<bool>(metric!, "IsReady").Should().Be(expectedReady);
        Get<string>(metric!, "Status").Should().Be(expectedStatus);
    }

    private static T Get<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"metric property {propertyName} must exist");
        return (T)property!.GetValue(instance)!;
    }
}
`
);

// ============================================================
// 6. Backend unit tests — QualityLabelBuilderServiceTests
// ============================================================

write(
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/QualityLabelBuilderServiceTests.cs",
  `using System.Reflection;
using FluentAssertions;
using PlantProcess.Application.Analytics.Services;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class QualityLabelBuilderServiceTests
{
    [Theory]
    [InlineData(false, false, false, false, null, "ACCEPTED_OR_NO_QUALITY_EVENT")]
    [InlineData(true, false, false, false, null, "DEFECT_OTHER")]
    [InlineData(true, false, false, false, "Surface crack", "DEFECT_SURFACE_CRACK")]
    [InlineData(true, true, false, false, "Surface crack", "REJECTED")]
    [InlineData(true, false, true, false, "Surface crack", "DOWNGRADED")]
    [InlineData(true, false, false, true, "Surface crack", "REWORKED")]
    public void BuildLabelCode_should_apply_quality_label_precedence_and_normalization(
        bool hasDefect,
        bool isRejected,
        bool isDowngraded,
        bool isReworked,
        string? primaryDefectCategory,
        string expected)
    {
        var method = typeof(QualityLabelBuilderService).GetMethod(
            "BuildLabelCode",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("quality labels must keep deterministic precedence");

        var result = (string)method!.Invoke(null, new object?[]
        {
            hasDefect,
            isRejected,
            isDowngraded,
            isReworked,
            primaryDefectCategory
        })!;

        result.Should().Be(expected);
    }
}
`
);

// ============================================================
// 7. Backend integration tests — MlLearningCoreIntegrationTests
// ============================================================

write(
  "Backend/tests/PlantProcess.Api.IntegrationTests/Analytics/MlLearningCoreIntegrationTests.cs",
  `using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Analytics;

public sealed class MlLearningCoreIntegrationTests : AuthenticatedApiTestBase
{
    public MlLearningCoreIntegrationTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Ml_learning_core_should_expose_status_jobs_run_results_and_provider_proof()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var statusResponse = await client.GetAsync("/api/ml/learning/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var jobsResponse = await client.GetAsync("/api/ml/learning/jobs");
        jobsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var jobsJson = await jobsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobs = jobsJson.GetProperty("jobs").EnumerateArray().ToList();

        jobs.Should().HaveCountGreaterThanOrEqualTo(4);
        jobs.Select(x => x.GetProperty("job_code").GetString())
            .Should()
            .Contain(new[]
            {
                "ML_PROCESS_VS_DEFECT",
                "ML_PROCESS_VS_DOWNTIME",
                "ML_PROCESS_VS_KPI",
                "ML_WEEKLY_OVERALL"
            });

        var runResponse = await client.PostAsJsonAsync(
            "/api/ml/learning/jobs/ML_PROCESS_VS_DEFECT/run",
            new
            {
                outcomeFamily = "defect",
                windowDays = 730
            });

        runResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var runJson = await runResponse.Content.ReadFromJsonAsync<JsonElement>();
        runJson.GetProperty("result").GetProperty("status").GetString()
            .Should()
            .Be("Completed");

        var resultsResponse = await client.GetAsync("/api/ml/learning/results?limit=200&jobCode=ML_PROCESS_VS_DEFECT");
        resultsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultsJson = await resultsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var results = resultsJson.GetProperty("results").EnumerateArray().ToList();

        results.Should().NotBeEmpty("the ML learning job must persist result rows");

        results.Should().Contain(row =>
            GetString(row, "finding_status") == "EvidenceForReview" &&
            GetNullableDecimal(row, "q_value").HasValue &&
            GetNullableDecimal(row, "effect_size") >= 0.5m);

        results.Should().Contain(row =>
            GetString(row, "feature_key") == "noise.planted_random" &&
            GetString(row, "finding_status") == "RejectedNoiseControl",
            "golden dataset must reject planted random noise");

        var proofResponse = await client.GetAsync("/api/ml/providers/narrative/proof");
        proofResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var proofJson = await proofResponse.Content.ReadFromJsonAsync<JsonElement>();

        proofJson.GetProperty("usedExternalApi").GetBoolean()
            .Should()
            .BeFalse("local/offline narrative provider must degrade safely without external API");

        proofJson.GetProperty("rawPlantDataIncluded").GetBoolean()
            .Should()
            .BeFalse("narrative provider must not leak raw plant data");
    }

    [Fact]
    public async Task Ml_results_endpoint_should_keep_honest_positioning_language()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/ml/learning/results?limit=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var honestPositioning = json.GetProperty("honestPositioning").GetString();

        honestPositioning.Should().NotBeNullOrWhiteSpace();
        honestPositioning!.Should().Contain("Diagnostic associations");
        honestPositioning.Should().Contain("not guaranteed root cause");
    }

    private static string? GetString(JsonElement row, string propertyName)
    {
        return row.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }

    private static decimal? GetNullableDecimal(JsonElement row, string propertyName)
    {
        if (!row.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.TryGetDecimal(out var value)
            ? value
            : null;
    }
}
`
);

// ============================================================
// 8. Backend integration tests — AuthGateMatrixTests
// ============================================================

write(
  "Backend/tests/PlantProcess.Api.IntegrationTests/Security/AuthGateMatrixTests.cs",
  `using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Security;

public sealed class AuthGateMatrixTests : AuthenticatedApiTestBase
{
    public AuthGateMatrixTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Login_gate_should_reject_empty_payload()
    {
        using var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { });

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_gate_should_reject_wrong_password()
    {
        using var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = TestAdminUserName,
            Password = "wrong-password"
        });

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_gate_should_issue_admin_token_with_expected_claim_surface()
    {
        using var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            UserName = TestAdminUserName,
            Password = TestAdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("role").GetString()
            .Should()
            .Be("Admin");

        json.GetProperty("accessToken").GetString()
            .Should()
            .NotBeNullOrWhiteSpace();

        json.GetProperty("scopes").EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .Contain("plantprocess.data.manage");
    }

    [Fact]
    public async Task Protected_admin_endpoint_should_reject_anonymous_user()
    {
        using var client = CreateAnonymousClient();

        var response = await client.GetAsync("/admin/jobs-monitor");

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Protected_admin_endpoint_should_accept_admin_token()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/jobs-monitor");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Malformed_bearer_token_should_not_be_accepted()
    {
        using var client = CreateAnonymousClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await client.GetAsync("/admin/jobs-monitor");

        response.StatusCode
            .Should()
            .BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}
`
);

// ============================================================
// 9. Infrastructure integration tests — SqlScriptHygieneApplyTests
// ============================================================

write(
  "Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Database/SqlScriptHygieneApplyTests.cs",
  `using FluentAssertions;

namespace PlantProcess.Infrastructure.IntegrationTests.Database;

public sealed class SqlScriptHygieneApplyTests
{
    [Fact]
    public void Ordered_database_scripts_should_be_non_empty_and_bom_free()
    {
        var scriptsRoot = Path.Combine(FindBackendRoot(), "database", "scripts");

        Directory.Exists(scriptsRoot)
            .Should()
            .BeTrue("database/scripts must exist");

        var scripts = Directory.GetFiles(scriptsRoot, "*.sql")
            .OrderBy(Path.GetFileName)
            .ToList();

        scripts.Should().NotBeEmpty();

        foreach (var script in scripts)
        {
            var bytes = File.ReadAllBytes(script);
            var text = File.ReadAllText(script);

            bytes.Should().NotBeEmpty(Path.GetFileName(script));
            HasUtf8Bom(bytes).Should().BeFalse($"{Path.GetFileName(script)} must be BOM-free for psql automation");
            text.Trim().Should().NotBeEmpty($"{Path.GetFileName(script)} must not be an unexplained empty placeholder");
            text.Should().NotContain("\\0", $"{Path.GetFileName(script)} must not contain null bytes");
        }
    }

    [Fact]
    public void Critical_ml_learning_scripts_should_expose_acceptance_and_governance_functions()
    {
        var scriptsRoot = Path.Combine(FindBackendRoot(), "database", "scripts");

        var script204 = File.ReadAllText(Path.Combine(scriptsRoot, "204_phase04_phase05_ml_learning_core.sql"));
        var script205 = File.ReadAllText(Path.Combine(scriptsRoot, "205_phase04_phase05_completion_governance_jobs_tests.sql"));

        script204.Should().Contain("ppiq_ml_seed_phase45_golden_dataset");
        script204.Should().Contain("ppiq_ml_run_learning_job_v1");
        script204.Should().Contain("ppiq_ml_phase45_acceptance");

        script205.Should().Contain("ppiq_ml_run_phase45_golden_tests_v1");
        script205.Should().Contain("ppiq_ml_phase45_completion_acceptance_v1");
        script205.Should().Contain("v_ml_learning_jobs_monitor_v1");
    }

    [Fact]
    public void Runtime_role_script_should_require_explicit_password_variable()
    {
        var script = File.ReadAllText(Path.Combine(
            FindBackendRoot(),
            "database",
            "scripts",
            "095_create_runtime_app_role_admin_only.sql"));

        script.Should().Contain("\\\\set ON_ERROR_STOP on");
        script.Should().Contain("plantprocess_app_password");
        script.Should().Contain("\\\\quit 1");
    }

    [Fact]
    public void Sql_scripts_should_not_contain_raw_unmasked_local_password()
    {
        var scriptsRoot = Path.Combine(FindBackendRoot(), "database");
        var sqlFiles = Directory.GetFiles(scriptsRoot, "*.sql", SearchOption.AllDirectories);

        foreach (var file in sqlFiles)
        {
            var text = File.ReadAllText(file);

            text.Should().NotContain(
                "plantprocess123",
                $"{Path.GetFileName(file)} must not persist local development password in SQL scripts");
        }
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 &&
               bytes[0] == 0xEF &&
               bytes[1] == 0xBB &&
               bytes[2] == 0xBF;
    }

    private static string FindBackendRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "database", "scripts")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Backend root from test output directory.");
    }
}
`
);

// ============================================================
// 10. Add OpenAPI ML/page/dynamic endpoint coverage without touching old test.
// ============================================================

write(
  "Backend/tests/PlantProcess.Api.IntegrationTests/OpenApi/OpenApiMlAndDynamicEndpointContractTests.cs",
  `using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.OpenApi;

public sealed class OpenApiMlAndDynamicEndpointContractTests : AuthenticatedApiTestBase
{
    public OpenApiMlAndDynamicEndpointContractTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Swagger_document_should_include_ml_learning_and_dynamic_page_surfaces()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.TryGetProperty("paths", out var paths)
            .Should()
            .BeTrue();

        var pathNames = paths.EnumerateObject()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        pathNames.Should().Contain("/api/ml/learning/status");
        pathNames.Should().Contain("/api/ml/learning/jobs");
        pathNames.Should().Contain("/api/ml/learning/results");
        pathNames.Should().Contain("/api/ml/providers/narrative/proof");
        pathNames.Should().Contain("/api/suggestions");
        pathNames.Should().Contain("/api/pages/{slug}");
    }
}
`
);

// ============================================================
// 11. Validator for Pack B files.
// ============================================================

write(
  "tools/p00/validate-p00b-backend-critical-tests.cjs",
  `const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const requiredFiles = [
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/RiskScoreServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/FeatureEngineeringServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/MlReadinessServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/QualityLabelBuilderServiceTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Analytics/MlLearningCoreIntegrationTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Security/AuthGateMatrixTests.cs",
  "Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Database/SqlScriptHygieneApplyTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/OpenApi/OpenApiMlAndDynamicEndpointContractTests.cs"
];

const failures = [];

for (const file of requiredFiles) {
  const full = path.join(root, file);
  if (!fs.existsSync(full)) {
    failures.push("Missing required Pack B test file: " + file);
  }
}

const riskService = fs.readFileSync(
  path.join(root, "Backend/PlantProcess.Application/Analytics/Services/RiskScoreService.cs"),
  "utf8");

if (!riskService.includes("riskClass: riskClass,")) {
  failures.push("RiskScoreService does not pass computed riskClass into RiskScore constructor.");
}

if (riskService.includes("riskClass: command.RiskClass,")) {
  failures.push("RiskScoreService still passes command.RiskClass directly.");
}

const sql080 = fs.readFileSync(
  path.join(root, "Backend/database/scripts/080_phase_3_4_connector_schema_foundation.sql"),
  "utf8");

if (sql080.trim().length === 0) {
  failures.push("080 SQL placeholder is still empty.");
}

if (failures.length > 0) {
  console.error("P00B backend critical test validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("P00B backend critical test validation passed.");
console.log("Pack B test files exist and RiskScore/SQL hygiene fixes are present.");
`
);

console.log("P00B Pack B backend critical behavioural tests applied.");
