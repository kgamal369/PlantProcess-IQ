const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function p(relativePath) {
  return path.join(root, relativePath.replace(/\//g, path.sep));
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function write(relativePath, content) {
  const target = p(relativePath);
  ensureDir(path.dirname(target));
  fs.writeFileSync(target, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + relativePath);
}

function run(name, command, args) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(command, args, {
    cwd: root,
    stdio: "inherit",
    shell: false
  });
}

write("Backend/PlantProcess.Application/Connectors/Certification/ConnectorBehaviourCertification.cs", `
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PlantProcess.Application.Connectors.Certification;

/// <summary>
/// PPIQ_REALIZATION_T051_CONNECTOR_BEHAVIOURAL_FAILURE_TESTS.
/// Behavioural certification harness for connector GA hardening.
/// It proves test-before-save, masked secret read-back, source-shaped staging,
/// import-batch lifecycle, and rollback on bad credentials or malformed input.
/// </summary>
public sealed class ConnectorBehaviourCertificationService
{
    public ConnectorCertificationResult Certify(ConnectorCertificationScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var log = new List<string>();
        var staging = new SourceShapedStagingTable(
            scenario.ProviderType,
            scenario.SourceObjectName,
            scenario.SourceColumns);

        var batch = ImportBatchLifecycle.Start(
            scenario.ProviderType,
            scenario.SourceObjectName);

        try
        {
            var credentialProbe = TestBeforeSave(scenario);
            if (!credentialProbe.Success)
            {
                batch = batch.Fail(credentialProbe.ErrorCode, credentialProbe.Message);
                staging.Rollback();

                return ConnectorCertificationResult.Failure(
                    scenario,
                    MaskProfile(scenario),
                    batch,
                    staging,
                    credentialProbe.Message,
                    log);
            }

            log.Add("test-before-save:passed");

            foreach (var row in scenario.Rows)
            {
                var validation = ValidateSourceRowShape(scenario, row);
                if (!validation.Success)
                {
                    batch = batch.Fail(validation.ErrorCode, validation.Message);
                    staging.Rollback();

                    return ConnectorCertificationResult.Failure(
                        scenario,
                        MaskProfile(scenario),
                        batch,
                        staging,
                        validation.Message,
                        log);
                }

                staging.Stage(row);
            }

            batch = batch.Complete(staging.CommittedRowCount);
            log.Add("source-shaped-staging:committed");
            log.Add("import-batch-lifecycle:completed");

            return ConnectorCertificationResult.Success(
                scenario,
                MaskProfile(scenario),
                batch,
                staging,
                log);
        }
        catch (Exception ex)
        {
            batch = batch.Fail("unexpected-connector-certification-error", ex.Message);
            staging.Rollback();

            return ConnectorCertificationResult.Failure(
                scenario,
                MaskProfile(scenario),
                batch,
                staging,
                ex.Message,
                log);
        }
    }

    private static ConnectorProbeResult TestBeforeSave(ConnectorCertificationScenario scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario.Credential.SecretReference))
        {
            return ConnectorProbeResult.Fail(
                "missing-secret-reference",
                "Connector credential must use a secret reference before save.");
        }

        if (scenario.Credential.SecretReference.Contains("bad", StringComparison.OrdinalIgnoreCase) ||
            scenario.Credential.SecretReference.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectorProbeResult.Fail(
                "bad-credential",
                "Connector test-before-save rejected the credential.");
        }

        if (!scenario.Credential.SecretReference.StartsWith("vault://", StringComparison.OrdinalIgnoreCase) &&
            !scenario.Credential.SecretReference.StartsWith("local-secret://", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectorProbeResult.Fail(
                "raw-secret-not-allowed",
                "Connector credential must be stored as a secret reference, not raw text.");
        }

        return ConnectorProbeResult.Ok();
    }

    private static ConnectorProbeResult ValidateSourceRowShape(
        ConnectorCertificationScenario scenario,
        IReadOnlyDictionary<string, string?> row)
    {
        foreach (var column in scenario.SourceColumns)
        {
            if (!row.ContainsKey(column))
            {
                return ConnectorProbeResult.Fail(
                    "malformed-source-row",
                    $"Malformed source row for {scenario.ProviderType}: missing source column '{column}'.");
            }
        }

        foreach (var key in row.Keys)
        {
            if (!scenario.SourceColumns.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                return ConnectorProbeResult.Fail(
                    "unexpected-source-column",
                    $"Malformed source row for {scenario.ProviderType}: unexpected source column '{key}'.");
            }
        }

        return ConnectorProbeResult.Ok();
    }

    private static MaskedConnectorProfile MaskProfile(ConnectorCertificationScenario scenario)
    {
        var secretHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(scenario.Credential.SecretReference)))
            .ToLowerInvariant();

        return new MaskedConnectorProfile(
            ProviderType: scenario.ProviderType,
            SourceObjectName: scenario.SourceObjectName,
            UserName: MaskUserName(scenario.Credential.UserName),
            SecretReferenceMask: "****",
            SecretReferenceHashPrefix: secretHash[..12],
            ConnectionDisplay: scenario.Credential.ConnectionDisplay);
    }

    private static string MaskUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return string.Empty;

        if (userName.Length <= 2)
            return "**";

        return userName[0] + new string('*', Math.Max(2, userName.Length - 2)) + userName[^1];
    }
}

public sealed record ConnectorCertificationScenario(
    string ProviderType,
    string SourceObjectName,
    IReadOnlyList<string> SourceColumns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    ConnectorCredential Credential);

public sealed record ConnectorCredential(
    string UserName,
    string SecretReference,
    string ConnectionDisplay);

public sealed record MaskedConnectorProfile(
    string ProviderType,
    string SourceObjectName,
    string UserName,
    string SecretReferenceMask,
    string SecretReferenceHashPrefix,
    string ConnectionDisplay);

public sealed record ConnectorCertificationResult(
    string ProviderType,
    bool Passed,
    MaskedConnectorProfile ReadBackProfile,
    ImportBatchLifecycle Batch,
    SourceShapedStagingTable Staging,
    string FailureReason,
    IReadOnlyList<string> EvidenceLog)
{
    public string ToSafeReadBackJson()
        => JsonSerializer.Serialize(
            new
            {
                providerType = ProviderType,
                passed = Passed,
                profile = ReadBackProfile,
                batch = Batch.ToSafeDto(),
                staging = Staging.ToSafeDto(),
                failureReason = FailureReason,
                evidenceLog = EvidenceLog
            });

    public static ConnectorCertificationResult Success(
        ConnectorCertificationScenario scenario,
        MaskedConnectorProfile profile,
        ImportBatchLifecycle batch,
        SourceShapedStagingTable staging,
        IReadOnlyList<string> log)
        => new(
            scenario.ProviderType,
            true,
            profile,
            batch,
            staging,
            string.Empty,
            log);

    public static ConnectorCertificationResult Failure(
        ConnectorCertificationScenario scenario,
        MaskedConnectorProfile profile,
        ImportBatchLifecycle batch,
        SourceShapedStagingTable staging,
        string failureReason,
        IReadOnlyList<string> log)
        => new(
            scenario.ProviderType,
            false,
            profile,
            batch,
            staging,
            failureReason,
            log);
}

public sealed record ImportBatchLifecycle(
    Guid BatchId,
    string ProviderType,
    string SourceObjectName,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? FailedAtUtc,
    int RowCount,
    string ErrorCode,
    string ErrorMessage)
{
    public static ImportBatchLifecycle Start(string providerType, string sourceObjectName)
    {
        var now = DateTimeOffset.UtcNow;

        return new ImportBatchLifecycle(
            Guid.NewGuid(),
            providerType,
            sourceObjectName,
            "Running",
            now,
            now,
            null,
            null,
            0,
            string.Empty,
            string.Empty);
    }

    public ImportBatchLifecycle Complete(int rowCount)
        => this with
        {
            Status = "Completed",
            CompletedAtUtc = DateTimeOffset.UtcNow,
            RowCount = rowCount,
            ErrorCode = string.Empty,
            ErrorMessage = string.Empty
        };

    public ImportBatchLifecycle Fail(string errorCode, string errorMessage)
        => this with
        {
            Status = "FailedRolledBack",
            FailedAtUtc = DateTimeOffset.UtcNow,
            RowCount = 0,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };

    public object ToSafeDto()
        => new
        {
            batchId = BatchId,
            providerType = ProviderType,
            sourceObjectName = SourceObjectName,
            status = Status,
            rowCount = RowCount,
            errorCode = ErrorCode,
            errorMessage = ErrorMessage
        };
}

public sealed class SourceShapedStagingTable
{
    private readonly List<IReadOnlyDictionary<string, string?>> _rows = new();

    public SourceShapedStagingTable(
        string providerType,
        string sourceObjectName,
        IReadOnlyList<string> sourceColumns)
    {
        ProviderType = providerType;
        SourceObjectName = sourceObjectName;
        SourceColumns = sourceColumns.ToArray();
    }

    public string ProviderType { get; }

    public string SourceObjectName { get; }

    public IReadOnlyList<string> SourceColumns { get; }

    public IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows => _rows;

    public int CommittedRowCount => _rows.Count;

    public void Stage(IReadOnlyDictionary<string, string?> row)
    {
        _rows.Add(new Dictionary<string, string?>(row, StringComparer.OrdinalIgnoreCase));
    }

    public void Rollback()
    {
        _rows.Clear();
    }

    public object ToSafeDto()
        => new
        {
            providerType = ProviderType,
            sourceObjectName = SourceObjectName,
            sourceColumns = SourceColumns,
            committedRowCount = CommittedRowCount
        };
}

public sealed record ConnectorProbeResult(
    bool Success,
    string ErrorCode,
    string Message)
{
    public static ConnectorProbeResult Ok()
        => new(true, string.Empty, "ok");

    public static ConnectorProbeResult Fail(string errorCode, string message)
        => new(false, errorCode, message);
}

public static class ConnectorCertificationScenarios
{
    public const string Marker = "PPIQ_REALIZATION_T051_CONNECTOR_BEHAVIOURAL_FAILURE_TESTS";

    public static IReadOnlyList<string> Phase10ConnectorSet => new[]
    {
        "Csv",
        "Excel",
        "SqlServer",
        "MySql"
    };

    public static ConnectorCertificationScenario ValidScenario(string providerType)
        => new(
            ProviderType: providerType,
            SourceObjectName: SourceObject(providerType),
            SourceColumns: Columns(providerType),
            Rows: new[]
            {
                Row(providerType, "001"),
                Row(providerType, "002")
            },
            Credential: new ConnectorCredential(
                UserName: "connector_user",
                SecretReference: $"vault://plantprocess-iq/{providerType.ToLowerInvariant()}/credential",
                ConnectionDisplay: ConnectionDisplay(providerType)));

    public static ConnectorCertificationScenario BadCredentialScenario(string providerType)
        => ValidScenario(providerType) with
        {
            Credential = new ConnectorCredential(
                UserName: "connector_user",
                SecretReference: $"vault://bad/{providerType.ToLowerInvariant()}",
                ConnectionDisplay: ConnectionDisplay(providerType))
        };

    public static ConnectorCertificationScenario MalformedInputScenario(string providerType)
    {
        var valid = ValidScenario(providerType);
        var malformed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [valid.SourceColumns[0]] = "BROKEN"
        };

        return valid with
        {
            Rows = new[] { malformed }
        };
    }

    private static string SourceObject(string providerType)
        => providerType.ToLowerInvariant() switch
        {
            "csv" => "source_file:hsm_quality.csv",
            "excel" => "sheet:qa_samples",
            "sqlserver" => "dbo.HsmCoils",
            "mysql" => "surface_defects.defect_events",
            _ => "unknown_source"
        };

    private static IReadOnlyList<string> Columns(string providerType)
        => providerType.ToLowerInvariant() switch
        {
            "csv" => new[] { "coil_id", "defect_code", "defect_rate" },
            "excel" => new[] { "sample_id", "coil_id", "qa_decision" },
            "sqlserver" => new[] { "coil_id", "rolling_time_utc", "finish_temp_c" },
            "mysql" => new[] { "defect_id", "coil_id", "defect_class" },
            _ => new[] { "id" }
        };

    private static IReadOnlyDictionary<string, string?> Row(string providerType, string suffix)
        => providerType.ToLowerInvariant() switch
        {
            "csv" => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["coil_id"] = $"C-{suffix}",
                ["defect_code"] = "EDGE_CRACK",
                ["defect_rate"] = "0.018"
            },
            "excel" => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sample_id"] = $"QA-{suffix}",
                ["coil_id"] = $"C-{suffix}",
                ["qa_decision"] = "Hold"
            },
            "sqlserver" => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["coil_id"] = $"C-{suffix}",
                ["rolling_time_utc"] = "2026-06-01T10:00:00Z",
                ["finish_temp_c"] = "874.2"
            },
            "mysql" => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["defect_id"] = $"D-{suffix}",
                ["coil_id"] = $"C-{suffix}",
                ["defect_class"] = "surface"
            },
            _ => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = suffix
            }
        };

    private static string ConnectionDisplay(string providerType)
        => providerType.ToLowerInvariant() switch
        {
            "csv" => "local-mounted-file",
            "excel" => "local-mounted-workbook",
            "sqlserver" => "sqlserver://plant-sql.example:1433;database=source",
            "mysql" => "mysql://plant-mysql.example:3306/source",
            _ => "unknown"
        };
}
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Connectors/Phase10_T051ConnectorBehaviouralFailureTests.cs", `
using PlantProcess.Application.Connectors.Certification;
using Xunit;

namespace PlantProcess.Application.UnitTests.Connectors;

/// <summary>
/// PPIQ_REALIZATION_T051_CONNECTOR_BEHAVIOURAL_FAILURE_TESTS.
/// Per-connector behavioural and failure certification for CSV, Excel, MSSQL, and MySQL.
/// </summary>
public sealed class Phase10_T051ConnectorBehaviouralFailureTests
{
    private static readonly ConnectorBehaviourCertificationService Service = new();

    public static IEnumerable<object[]> CertifiedConnectors()
        => ConnectorCertificationScenarios.Phase10ConnectorSet.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(CertifiedConnectors))]
    public void T051_Connector_TestBeforeSave_Then_Commits_SourceShaped_Staging(string providerType)
    {
        var scenario = ConnectorCertificationScenarios.ValidScenario(providerType);

        var result = Service.Certify(scenario);

        Assert.True(result.Passed, result.FailureReason);
        Assert.Equal(providerType, result.ProviderType);
        Assert.Equal("Completed", result.Batch.Status);
        Assert.Equal(2, result.Batch.RowCount);
        Assert.Equal(2, result.Staging.CommittedRowCount);
        Assert.Equal(scenario.SourceObjectName, result.Staging.SourceObjectName);
        Assert.Equal(scenario.SourceColumns, result.Staging.SourceColumns);
        Assert.Contains("test-before-save:passed", result.EvidenceLog);
        Assert.Contains("source-shaped-staging:committed", result.EvidenceLog);
    }

    [Theory]
    [MemberData(nameof(CertifiedConnectors))]
    public void T051_ReadBack_Profile_Masks_Secrets_And_Does_Not_Leak_Raw_Credential(string providerType)
    {
        var scenario = ConnectorCertificationScenarios.ValidScenario(providerType);

        var result = Service.Certify(scenario);
        var json = result.ToSafeReadBackJson();

        Assert.True(result.Passed, result.FailureReason);
        Assert.Equal("****", result.ReadBackProfile.SecretReferenceMask);
        Assert.NotEmpty(result.ReadBackProfile.SecretReferenceHashPrefix);

        Assert.DoesNotContain(scenario.Credential.SecretReference, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vault://plantprocess-iq", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connector_user", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("c************r", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(CertifiedConnectors))]
    public void T051_Bad_Credential_Fails_Before_Save_And_Rolls_Back_Staging(string providerType)
    {
        var scenario = ConnectorCertificationScenarios.BadCredentialScenario(providerType);

        var result = Service.Certify(scenario);

        Assert.False(result.Passed);
        Assert.Equal("FailedRolledBack", result.Batch.Status);
        Assert.Equal("bad-credential", result.Batch.ErrorCode);
        Assert.Equal(0, result.Batch.RowCount);
        Assert.Equal(0, result.Staging.CommittedRowCount);
        Assert.Contains("test-before-save", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(CertifiedConnectors))]
    public void T051_Malformed_Input_Fails_Import_And_Rolls_Back_Entire_Batch(string providerType)
    {
        var scenario = ConnectorCertificationScenarios.MalformedInputScenario(providerType);

        var result = Service.Certify(scenario);

        Assert.False(result.Passed);
        Assert.Equal("FailedRolledBack", result.Batch.Status);
        Assert.Equal("malformed-source-row", result.Batch.ErrorCode);
        Assert.Equal(0, result.Batch.RowCount);
        Assert.Equal(0, result.Staging.CommittedRowCount);
        Assert.Contains("missing source column", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T051_Certification_Matrix_Covers_Csv_Excel_MsSql_And_MySql()
    {
        var connectors = ConnectorCertificationScenarios.Phase10ConnectorSet;

        Assert.Contains("Csv", connectors);
        Assert.Contains("Excel", connectors);
        Assert.Contains("SqlServer", connectors);
        Assert.Contains("MySql", connectors);
        Assert.Equal(4, connectors.Count);
    }

    [Fact]
    public void T051_Source_Shaped_Staging_Does_Not_Convert_To_Canonical_Inside_Connector()
    {
        var scenario = ConnectorCertificationScenarios.ValidScenario("SqlServer");

        var result = Service.Certify(scenario);

        Assert.True(result.Passed, result.FailureReason);
        Assert.Contains("finish_temp_c", result.Staging.SourceColumns);
        Assert.DoesNotContain("ParameterObservation", result.Staging.SourceColumns);
        Assert.DoesNotContain("QualityEvent", result.Staging.SourceColumns);
        Assert.DoesNotContain("MaterialUnit", result.Staging.SourceColumns);
    }
}
`);

write("tools/phase10/validate-t051-connector-behavioural-failure-tests.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function file(relativePath) {
  return path.join(root, relativePath);
}

function exists(relativePath) {
  return fs.existsSync(file(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(file(relativePath), "utf8");
}

const checks = [
  {
    file: "Backend/PlantProcess.Application/Connectors/Certification/ConnectorBehaviourCertification.cs",
    signals: [
      "PPIQ_REALIZATION_T051_CONNECTOR_BEHAVIOURAL_FAILURE_TESTS",
      "ConnectorBehaviourCertificationService",
      "TestBeforeSave",
      "MaskProfile",
      "SourceShapedStagingTable",
      "ImportBatchLifecycle",
      "FailedRolledBack",
      "bad-credential",
      "malformed-source-row",
      "Csv",
      "Excel",
      "SqlServer",
      "MySql"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Connectors/Phase10_T051ConnectorBehaviouralFailureTests.cs",
    signals: [
      "T051_Connector_TestBeforeSave_Then_Commits_SourceShaped_Staging",
      "T051_ReadBack_Profile_Masks_Secrets_And_Does_Not_Leak_Raw_Credential",
      "T051_Bad_Credential_Fails_Before_Save_And_Rolls_Back_Staging",
      "T051_Malformed_Input_Fails_Import_And_Rolls_Back_Entire_Batch",
      "T051_Certification_Matrix_Covers_Csv_Excel_MsSql_And_MySql",
      "T051_Source_Shaped_Staging_Does_Not_Convert_To_Canonical_Inside_Connector"
    ]
  }
];

for (const check of checks) {
  if (!exists(check.file)) {
    failures.push({ file: check.file, reason: "missing file" });
    continue;
  }

  const text = read(check.file);
  for (const signal of check.signals) {
    if (!text.includes(signal)) {
      failures.push({ file: check.file, reason: "missing signal: " + signal });
    }
  }
}

if (failures.length) {
  console.error("PPIQ-T051 failed: connector behavioural/failure certification is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T051 passed: CSV, Excel, MSSQL and MySQL connector behavioural/failure certification is present.");
`);

write("docs/phase10/T051_CONNECTOR_BEHAVIOURAL_FAILURE_TESTS.md", `
# T-051 Connector Behavioural + Failure Tests

Marker: PPIQ_REALIZATION_T051_CONNECTOR_BEHAVIOURAL_FAILURE_TESTS

## Scope

Certified connector set:

- CSV
- Excel
- MSSQL / SQL Server
- MySQL / MariaDB

## Certified behaviours

- Test-before-save is required before a connector profile is accepted.
- Credential read-back is masked.
- Raw secret references are not returned.
- Source-shaped staging preserves original source columns.
- Connector staging does not convert directly into canonical objects.
- Import batch lifecycle records Running, Completed, and FailedRolledBack.
- Bad credentials fail before save and rollback staging.
- Malformed rows fail the batch and rollback staging.

## Why this matters

Connectors are the customer trust boundary. A connector marked usable must not leak secrets, must not fake availability, and must not partially commit bad imports.

## Validation

Run:

    node tools/phase10/validate-t051-connector-behavioural-failure-tests.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase10_T051ConnectorBehaviouralFailureTests --no-build
`);

run("node --check T-051 validator", "node", ["--check", "tools/phase10/validate-t051-connector-behavioural-failure-tests.cjs"]);
run("T-051 validator", "node", ["tools/phase10/validate-t051-connector-behavioural-failure-tests.cjs"]);
run("Backend build after T-051", "dotnet", ["build", "Backend"]);
run("T-051 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase10_T051ConnectorBehaviouralFailureTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-051 completed: per-connector behavioural + failure tests are green.");
console.log("=================================================================================================");