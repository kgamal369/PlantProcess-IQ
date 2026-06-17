[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

function Get-RelativePath([string]$FullPath) {
    return $FullPath.Substring($ProjectRoot.Length).TrimStart("\", "/").Replace("/", "\")
}

function Should-SkipPath([string]$FullPath) {
    $relative = Get-RelativePath $FullPath

    # Runtime/build/generated/backup folders.
    if ($relative -match '(^|\\)\.git\\') { return $true }
    if ($relative -match '(^|\\)node_modules\\') { return $true }
    if ($relative -match '(^|\\)bin\\') { return $true }
    if ($relative -match '(^|\\)obj\\') { return $true }
    if ($relative -match '(^|\\)dist\\') { return $true }
    if ($relative -match '(^|\\)coverage\\') { return $true }
    if ($relative -match '(^|\\)playwright-report\\') { return $true }
    if ($relative -match '(^|\\)test-results\\') { return $true }
    if ($relative -match '(^|\\)\.phase1_phase2_backup\\') { return $true }

    # Documentation, audit outputs, and generated evidence.
    if ($relative -match '^Documentation\\') { return $true }
    if ($relative -match '^docs\\phase1-phase2\\') { return $true }

    # Test projects and E2E fixtures intentionally contain fake credentials.
    if ($relative -match '^Backend\\tests\\') { return $true }
    if ($relative -match '^Frontend\\PlantProcess\.Web\\e2e\\') { return $true }

    # Local-only runtime files.
    if ($relative -eq 'Frontend\PlantProcess.Web\.env.local') { return $true }
    if ($relative -eq 'env\profiles\local.env') { return $true }
    if ($relative -eq 'deploy\.env') { return $true }
    if ($relative -match '(^|\\)\.env\.local$') { return $true }

    # Legacy/archived snapshots.
    if ($relative -match '^deploy\\_legacy_consolidated_') { return $true }
    if ($relative -match '^tools\\archive\\') { return $true }

    # Dev/demo/reference fixtures.
    # These are intentionally local/demo-only and must not be treated as production runtime templates.
    if ($relative -eq 'Backend\PlantProcess.Api\appsettings.Development.json') { return $true }
    if ($relative -eq 'Backend\PlantProcess.Api\Properties\launchSettings.json') { return $true }
    if ($relative -eq 'Backend\PlantProcess.Workers\Properties\launchSettings.json') { return $true }
    if ($relative -match '^deploy\\demo-sources\\') { return $true }
    if ($relative -eq 'deploy\identity\keycloak-reference-compose.yml') { return $true }
    if ($relative -eq 'deploy\server\docker-compose.yml') { return $true }

    # Do not scan this scanner. It contains secret-detection regex literals by design.
    if ($relative -eq 'tools\phase1-phase2\Invoke-SecretScan.ps1') { return $true }

    return $false
}

function Is-ConfigOrRuntimeFile([string]$FullPath) {
    $relative = Get-RelativePath $FullPath
    $name = [System.IO.Path]::GetFileName($relative).ToLowerInvariant()
    $extension = [System.IO.Path]::GetExtension($relative).ToLowerInvariant()

    # Production/default app config.
    if ($relative -eq 'Backend\PlantProcess.Api\appsettings.json') { return $true }
    if ($relative -match '^Backend\\PlantProcess\.Api\\appsettings\.(Production|Staging)\.json$') { return $true }
    if ($relative -match '^Backend\\PlantProcess\.Workers\\appsettings(\.(Production|Staging))?\.json$') { return $true }

    # Deployment templates and production-like runtime examples.
    if ($relative -eq 'deploy\.env.example') { return $true }
    if ($relative -eq 'deploy\server\.env.example') { return $true }
    if ($relative -eq 'Frontend\PlantProcess.Web\.env.example') { return $true }

    if ($relative -match '^deploy\\airgap\\') {
        return $extension -in @(".yml", ".yaml", ".ps1", ".env")
    }

    if ($relative -match '^deploy\\dr\\') {
        return $extension -in @(".yml", ".yaml", ".ps1", ".env")
    }

    if ($relative -eq 'scripts\env\use-profile.ps1') { return $true }
    if ($relative -eq 'tools\dev-bootstrap.ps1') { return $true }
    if ($relative -match '^tools\\ml\\.*\.ps1$') { return $true }

    # CI/runtime YAML.
    if ($relative -match '^\.github\\workflows\\') {
        return $extension -in @(".yml", ".yaml")
    }

    return $false
}

function Is-AllowedSecretValue([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $true }

    $v = $Value.Trim().Trim('"').Trim("'").Trim()

    if ([string]::IsNullOrWhiteSpace($v)) { return $true }

    # Environment/runtime injection.
    if ($v -match '^\$') { return $true }
    if ($v -match '\$env:') { return $true }
    if ($v -match '\$\{[A-Z0-9_:-]+\}') { return $true }
    if ($v -match '^%[A-Z0-9_]+%$') { return $true }
    if ($v -match '^<[^>]+>$') { return $true }
    if ($v -match '^\[[A-Z0-9_ -]+\]$') { return $true }

    # Disabled/masked.
    if ($v -match '(?i)^__DISABLED__$') { return $true }
    if ($v -match '(?i)^DISABLED-') { return $true }
    if ($v -match '(?i)masked|redacted|\*{4,}') { return $true }

    # Safe placeholders/examples/dev sentinels.
    if ($v -match '(?i)change.?me') { return $true }
    if ($v -match '(?i)example') { return $true }
    if ($v -match '(?i)placeholder') { return $true }
    if ($v -match '(?i)dummy') { return $true }
    if ($v -match '(?i)fake') { return $true }
    if ($v -match '(?i)test') { return $true }
    if ($v -match '(?i)sample') { return $true }
    if ($v -match '(?i)your[-_ ]') { return $true }
    if ($v -match '(?i)set_') { return $true }
    if ($v -match '(?i)replace_with') { return $true }
    if ($v -match '(?i)dev_only') { return $true }

    # Common non-secret literals.
    if ($v -in @("true", "false", "null", "none", "localhost", "127.0.0.1", "0.0.0.0")) { return $true }

    return $false
}

function Add-Offender(
    [System.Collections.Generic.List[string]]$Offenders,
    [string]$File,
    [int]$LineNumber,
    [string]$Reason,
    [string]$MaskedValue
) {
    $relative = Get-RelativePath $File
    $Offenders.Add(("{0}:{1} -> {2} [{3}]" -f $relative, $LineNumber, $Reason, $MaskedValue))
}

function Mask-Value([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "[empty]" }

    $v = $Value.Trim()

    if ($v.Length -le 4) { return "****" }

    return ($v.Substring(0, [Math]::Min(2, $v.Length)) + "****" + $v.Substring($v.Length - 2))
}

Push-Location $ProjectRoot
try {
    $gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue

    if ($gitleaks) {
        gitleaks detect --source . --config .gitleaks.toml --redact --no-banner

        if ($LASTEXITCODE -ne 0) {
            throw "gitleaks found possible secrets."
        }

        Write-Host "gitleaks passed." -ForegroundColor Green
        exit 0
    }

    Write-Host "gitleaks not installed. Running Phase 1/2 fallback production-runtime secret scan." -ForegroundColor Yellow
    Write-Host "Fallback scope: production/default app config, deploy templates, airgap/DR templates, env examples, and runtime scripts." -ForegroundColor DarkYellow
    Write-Host "Skipped by policy: Development config, demo-source compose files, reference Keycloak compose, local env files, tests, generated docs, and backups." -ForegroundColor DarkYellow

    $offenders = New-Object System.Collections.Generic.List[string]

    $files = Get-ChildItem -Path $ProjectRoot -Recurse -File |
        Where-Object {
            -not (Should-SkipPath $_.FullName) -and
            (Is-ConfigOrRuntimeFile $_.FullName)
        }

    foreach ($file in $files) {
        $lines = [IO.File]::ReadAllLines($file.FullName)

        for ($i = 0; $i -lt $lines.Length; $i++) {
            $line = $lines[$i]
            $lineNo = $i + 1
            $trimmed = $line.Trim()

            if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }

            if ($trimmed.StartsWith("#") -or $trimmed.StartsWith("//") -or $trimmed.StartsWith("*")) {
                if ($trimmed -notmatch '(?i)(?<![A-Za-z0-9_])(Password|Pwd)\s*=') {
                    continue
                }
            }

            # Connection-string Password/Pwd segment only.
            # Does not match env keys like PPIQ_DB_PASSWORD=...
            $connMatches = [regex]::Matches(
                $line,
                '(?i)(?<![A-Za-z0-9_])(Password|Pwd)\s*=\s*([^;''"`r`n]+)'
            )

            foreach ($m in $connMatches) {
                $value = $m.Groups[2].Value.Trim()

                if (-not (Is-AllowedSecretValue $value)) {
                    Add-Offender $offenders $file.FullName $lineNo "hardcoded connection-string password" (Mask-Value $value)
                }
            }

            # Direct sensitive config/env keys anchored at line start.
            $kvMatches = [regex]::Matches(
                $line,
                '(?i)^\s*["'']?([A-Z0-9_]*(PASSWORD|PASSWD|PWD|SECRET|SIGNINGKEY|SIGNING_KEY|CLIENTSECRET|CLIENT_SECRET|API_KEY|APIKEY|ACCESS_TOKEN|REFRESH_TOKEN|PRIVATE_KEY)[A-Z0-9_]*)["'']?\s*[:=]\s*["'']?([^"''#;,\r\n]+)'
            )

            foreach ($m in $kvMatches) {
                $key = $m.Groups[1].Value
                $value = $m.Groups[3].Value.Trim()

                if (-not (Is-AllowedSecretValue $value)) {
                    Add-Offender $offenders $file.FullName $lineNo "possible hardcoded $key" (Mask-Value $value)
                }
            }
        }
    }

    $reportDir = Join-Path $ProjectRoot "docs\phase1-phase2"

    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
    }

    $reportPath = Join-Path $reportDir ("PHASE1_PHASE2_SECRET_SCAN_FALLBACK_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".md")

    $report = New-Object System.Collections.Generic.List[string]
    $report.Add("# Phase 1/2 fallback secret scan report")
    $report.Add("")
    $report.Add(("Generated: {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')))
    $report.Add("")
    $report.Add("Fallback scanner scope:")
    $report.Add("")
    $report.Add("- Scans production/default app config, deploy templates, airgap/DR templates, env examples, and runtime scripts.")
    $report.Add("- Skips Development config, demo-source compose files, reference Keycloak compose, local env files, tests, generated docs, and backups.")
    $report.Add("- Skips source-code implementation files to avoid false positives on property/variable names.")
    $report.Add("- Uses gitleaks automatically when installed.")
    $report.Add("")
    $report.Add(("Scanned files: {0}" -f $files.Count))
    $report.Add(("Findings: {0}" -f $offenders.Count))
    $report.Add("")

    if ($offenders.Count -gt 0) {
        $report.Add("## Findings")
        foreach ($o in ($offenders | Sort-Object -Unique)) {
            $report.Add("- $o")
        }
    } else {
        $report.Add("No production-runtime hardcoded secrets found by fallback scanner.")
    }

    [IO.File]::WriteAllText($reportPath, ($report -join "`r`n") + "`r`n", (New-Object Text.UTF8Encoding($false)))

    if ($offenders.Count -gt 0) {
        $offenders | Sort-Object -Unique | ForEach-Object {
            Write-Host "Possible secret: $_" -ForegroundColor Red
        }

        Write-Host "Fallback report: $reportPath" -ForegroundColor Yellow
        throw "Fallback secret scan found possible production-runtime hardcoded secrets."
    }

    Write-Host "Fallback secret scan passed." -ForegroundColor Green
    Write-Host "Fallback report: $reportPath" -ForegroundColor Green
}
finally {
    Pop-Location
}