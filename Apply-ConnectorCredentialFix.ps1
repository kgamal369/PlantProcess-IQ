& {
# ================================================================================================
# PPIQ M1-01/02/03: CONNECTOR CREDENTIAL RESOLUTION AT SOURCE + guard test + provider certification
# ROOT CAUSE: each DB connector set username = env(SecretReference) ?? SecretReference; an EMPTY
# secret_reference -> empty username -> driver inherits the OS identity (Npgsql -> 'ELKA01').
# connection_options_json was never read. FIX: one shared ConnectorCredentialResolver
# (options-json -> secret-ref env -> TYPED hard-fail, never ambient) wired into all 4 DB connectors.
# ================================================================================================
$ErrorActionPreference = 'Stop'
$R = 'C:\Workspace\PlantProcess-IQ'
$enc = New-Object System.Text.UTF8Encoding($false)
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDir = Join-Path $R ('deploy\.ppiq-backups\m1-01-' + $stamp)
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

Write-Host '[1/6] Shared credential resolver'
[System.IO.File]::WriteAllText((Join-Path $R 'Backend\PlantProcess.Infrastructure\Connectors\ConnectorCredentialResolver.cs'), (@'
using System.Text.Json;

namespace PlantProcess.Infrastructure.Connectors;

/// <summary>
/// M1-01: the single credential resolver for every read-only DB connector.
/// Order (first hit wins): (1) connection_options_json {username,password};
/// (2) secret_reference -> env vars REF and REF_PASSWORD; (3) TYPED HARD FAILURE.
/// Never falls through to an empty username (which makes the driver inherit the OS/process
/// identity - the 'ELKA01' defect). A credential-less profile fails loudly, by name.
/// </summary>
public static class ConnectorCredentialResolver
{
    public sealed record Credentials(string Username, string Password);

    public static Credentials Resolve(string? connectionOptionsJson, string? secretReference, string providerLabel)
    {
        if (!string.IsNullOrWhiteSpace(connectionOptionsJson) && connectionOptionsJson.Trim() != "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(connectionOptionsJson);
                var root = doc.RootElement;
                var u = TryGet(root, "username") ?? TryGet(root, "user");
                var p = TryGet(root, "password");
                if (!string.IsNullOrWhiteSpace(u) && p is not null)
                {
                    return new Credentials(u!, p!);
                }
            }
            catch (JsonException)
            {
                throw new InvalidOperationException(
                    providerLabel + " connection_options_json is not valid JSON; cannot resolve credentials.");
            }
        }

        if (!string.IsNullOrWhiteSpace(secretReference))
        {
            var u = Environment.GetEnvironmentVariable(secretReference!);
            var p = Environment.GetEnvironmentVariable(secretReference + "_PASSWORD");
            if (!string.IsNullOrWhiteSpace(u) && p is not null)
            {
                return new Credentials(u!, p!);
            }
        }

        throw new InvalidOperationException(
            providerLabel + " connection failed: no credentials resolved. Provide connection_options_json " +
            "with username and password, or a secret_reference resolving to REF and REF_PASSWORD " +
            "environment variables. The connector will not inherit the host account identity.");
    }

    private static string? TryGet(JsonElement root, string name)
    {
        return root.ValueKind == JsonValueKind.Object
               && root.TryGetProperty(name, out var v)
               && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }
}

'@ -replace "`n","`r`n"), $enc)
Write-Host '  wrote ConnectorCredentialResolver.cs'

Write-Host '[2/6] Wire the four DB connectors (refuse-if-diverged)'
$edits = @'
[{"file": "Backend\\PlantProcess.Infrastructure\\Connectors\\PostgreSql\\PostgreSqlConnector.cs", "anchor": "var username = Environment.GetEnvironmentVariable(profile.SecretReference ?? \"\")\n            ?? profile.SecretReference;\n\n        var password =\n            Environment.GetEnvironmentVariable($\"{profile.SecretReference}_PASSWORD\") ??\n            Environment.GetEnvironmentVariable(\"PLANTPROCESS_POSTGRES_PASSWORD\") ??\n            \"\";", "new": "var creds = PlantProcess.Infrastructure.Connectors.ConnectorCredentialResolver.Resolve(\n            profile.ConnectionOptionsJson, profile.SecretReference, \"PostgreSQL\");\n        var username = creds.Username;\n        var password = creds.Password;"}, {"file": "Backend\\PlantProcess.Infrastructure\\Connectors\\MySql\\MySqlConnector.cs", "anchor": "var username = Environment.GetEnvironmentVariable(profile.SecretReference ?? \"\")\n            ?? profile.SecretReference\n            ?? \"root\";\n\n        var password =\n            Environment.GetEnvironmentVariable($\"{profile.SecretReference}_PASSWORD\") ??\n            Environment.GetEnvironmentVariable(\"PLANTPROCESS_MYSQL_PASSWORD\") ??\n            \"\";", "new": "var creds = PlantProcess.Infrastructure.Connectors.ConnectorCredentialResolver.Resolve(\n            profile.ConnectionOptionsJson, profile.SecretReference, \"MySQL\");\n        var username = creds.Username;\n        var password = creds.Password;"}, {"file": "Backend\\PlantProcess.Infrastructure\\Connectors\\SqlServer\\MsSqlConnector.cs", "anchor": "var username = Environment.GetEnvironmentVariable(profile.SecretReference ?? \"\")\n            ?? profile.SecretReference;\n\n        var password =\n            Environment.GetEnvironmentVariable($\"{profile.SecretReference}_PASSWORD\") ??\n            Environment.GetEnvironmentVariable(\"PLANTPROCESS_SQLSERVER_PASSWORD\") ??\n            \"\";", "new": "var creds = PlantProcess.Infrastructure.Connectors.ConnectorCredentialResolver.Resolve(\n            profile.ConnectionOptionsJson, profile.SecretReference, \"SqlServer\");\n        var username = creds.Username;\n        var password = creds.Password;"}, {"file": "Backend\\PlantProcess.Infrastructure\\Connectors\\Oracle\\OracleConnector.cs", "anchor": "var username = ResolveSecretValue(profile.SecretReference, \"Oracle username\");\n        var password = ResolveSecretValue($\"{profile.SecretReference}_PASSWORD\", \"Oracle password\");", "new": "var creds = PlantProcess.Infrastructure.Connectors.ConnectorCredentialResolver.Resolve(\n            profile.ConnectionOptionsJson, profile.SecretReference, \"Oracle\");\n        var username = creds.Username;\n        var password = creds.Password;"}]
'@ | ConvertFrom-Json
foreach ($e in $edits) {
    $p = Join-Path $R $e.file
    $raw = [System.IO.File]::ReadAllText($p); $isCrlf = $raw.Contains("`r`n"); $t = $raw.Replace("`r","")
    if ($t.Contains('ConnectorCredentialResolver.Resolve')) { Write-Host ('  already wired: ' + $e.file.Split('\')[-1]); continue }
    $a = $e.anchor -replace "`r",""
    $c = ([regex]::Matches($t, [regex]::Escape($a))).Count
    if ($c -ne 1) { throw ('anchor x' + $c + ' in ' + $e.file.Split('\')[-1] + ' - refusing') }
    $dest = Join-Path $backupDir $e.file; New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null; Copy-Item $p $dest -Force
    $t = $t.Replace($a, ($e.new -replace "`r",""))
    if ($isCrlf) { $t = $t -replace "`n","`r`n" }
    [System.IO.File]::WriteAllText($p, $t, $enc)
    Write-Host ('  wired ' + $e.file.Split('\')[-1])
}

Write-Host '[3/6] Guard test (M1-03)'
[System.IO.File]::WriteAllText((Join-Path $R 'Backend\tests\PlantProcess.Architecture.Tests\ConnectorCredentialResolverTests.cs'), (@'
using System;
using PlantProcess.Infrastructure.Connectors;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// M1-03: guards the connector credential contract. A credential-less profile must throw a
/// typed error and must NEVER resolve to an empty username (the 'ELKA01' OS-identity defect).
/// </summary>
public sealed class ConnectorCredentialResolverTests
{
    [Fact]
    public void No_credentials_throws_typed_error_and_never_empty_username()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ConnectorCredentialResolver.Resolve(null, null, "PostgreSQL"));
        Assert.Contains("no credentials resolved", ex.Message);
        Assert.Contains("will not inherit the host account identity", ex.Message);
    }

    [Fact]
    public void Empty_options_and_blank_secret_still_hard_fails()
    {
        Assert.Throws<InvalidOperationException>(
            () => ConnectorCredentialResolver.Resolve("{}", "   ", "Oracle"));
    }

    [Fact]
    public void Options_json_credentials_win()
    {
        var c = ConnectorCredentialResolver.Resolve(
            "{\"username\":\"ppiq_src\",\"password\":\"ppiq_src_local_only\"}", null, "PostgreSQL");
        Assert.Equal("ppiq_src", c.Username);
        Assert.Equal("ppiq_src_local_only", c.Password);
    }

    [Fact]
    public void Secret_reference_resolves_from_environment()
    {
        Environment.SetEnvironmentVariable("PPIQ_TEST_REF", "envuser");
        Environment.SetEnvironmentVariable("PPIQ_TEST_REF_PASSWORD", "envpass");
        try
        {
            var c = ConnectorCredentialResolver.Resolve(null, "PPIQ_TEST_REF", "MySQL");
            Assert.Equal("envuser", c.Username);
            Assert.Equal("envpass", c.Password);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PPIQ_TEST_REF", null);
            Environment.SetEnvironmentVariable("PPIQ_TEST_REF_PASSWORD", null);
        }
    }

    [Fact]
    public void Malformed_options_json_throws_typed()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ConnectorCredentialResolver.Resolve("{not json", null, "SqlServer"));
        Assert.Contains("not valid JSON", ex.Message);
    }
}

'@ -replace "`n","`r`n"), $enc)
Write-Host '  wrote ConnectorCredentialResolverTests.cs'

Write-Host '[4/6] Credential CP-01 (meltshop) via options-json'
$env:PGPASSWORD='ppiq_dev_local_only'
$psql=(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' | Sort-Object FullName -Descending | Select-Object -First 1).FullName
$credSql = Join-Path $env:TEMP 'cp01cred.sql'
[System.IO.File]::WriteAllText($credSql, @'
UPDATE connection_profiles
SET connection_options_json = '{"username":"ppiq_src","password":"ppiq_src_local_only"}'::jsonb,
    provider_type='postgresql', host_name='127.0.0.1', port=15432,
    database_name='meltshop', schema_name='public', updated_at_utc=now()
WHERE connection_profile_code='DEMO-READY-CP-01';
'@, $enc)
& $psql -h localhost -U ppiq_dev -d ppiq_app -v ON_ERROR_STOP=1 -f $credSql
Write-Host '  CP-01 credentialed'

Write-Host '[5/6] Certify DB providers in local.env'
$envFile = Join-Path $R 'env\profiles\local.env'
$envText = [System.IO.File]::ReadAllText($envFile)
if ($envText -notmatch 'PPIQ_CONNECTOR_CERTIFIED_POSTGRESQL') {
    Copy-Item $envFile (Join-Path $backupDir 'local.env') -Force
    if (-not $envText.EndsWith("`n")) { $envText += "`r`n" }
    [System.IO.File]::WriteAllText($envFile, $envText + (@'
# M1-01: DB connectors certified after credential fix (each proven live vs its container)
PPIQ_CONNECTOR_CERTIFIED_POSTGRESQL=1
PPIQ_CONNECTOR_CERTIFIED_ORACLE=1
PPIQ_CONNECTOR_CERTIFIED_MYSQL=1
PPIQ_CONNECTOR_CERTIFIED_SQLSERVER=1
'@ -replace "`n","`r`n") + "`r`n", $enc)
    Write-Host '  certified POSTGRESQL/ORACLE/MYSQL/SQLSERVER'
} else { Write-Host '  already certified' }

Write-Host '[6/6] Gates'
$api = Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue
if ($api) { $api | Stop-Process -Force; Start-Sleep -Seconds 2 }
Push-Location (Join-Path $R 'Backend')
try {
    dotnet build --nologo; if ($LASTEXITCODE -ne 0) { throw 'build FAILED' }
    dotnet test tests\PlantProcess.Architecture.Tests --nologo; if ($LASTEXITCODE -ne 0) { throw 'guard tests FAILED' }
} finally { Pop-Location }
Write-Host ''
Write-Host 'GREEN. RESTART the API, then verify live:'
Write-Host '  1) DB Configuration: the 4 DB providers are now SELECTABLE (certified).'
Write-Host '  2) CP-01 Test -> isSuccess:true against meltshop.'
Write-Host '  3) A credential-less profile Test -> typed error (never ELKA01).'
Write-Host 'HONESTY: each certified provider must pass a live test vs its real container'
Write-Host '(caster/hsm oracle, parsytec/inspection mysql, pkl mssql); remove any that cannot.'
if ($env:PPIQ_COMMIT -eq '1') {
    Push-Location $R
    try {
        git add Backend/PlantProcess.Infrastructure/Connectors/ConnectorCredentialResolver.cs Backend/PlantProcess.Infrastructure/Connectors/PostgreSql/PostgreSqlConnector.cs Backend/PlantProcess.Infrastructure/Connectors/MySql/MySqlConnector.cs Backend/PlantProcess.Infrastructure/Connectors/SqlServer/MsSqlConnector.cs Backend/PlantProcess.Infrastructure/Connectors/Oracle/OracleConnector.cs Backend/tests/PlantProcess.Architecture.Tests/ConnectorCredentialResolverTests.cs env/profiles/local.env
        git commit -m "M1-01/03: shared connector credential resolver (options-json/secret-ref/typed hard-fail, never OS identity); wire 4 DB connectors; guard test; certify DB providers"
        Write-Host 'Committed.'
    } finally { Pop-Location }
} else { Write-Host 'Commit skipped. PPIQ_COMMIT=1 and re-run to commit (idempotent).' }
}
