param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
    [switch]$StaticOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Off

Set-Location $RepoRoot

$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$EvidenceRoot = Join-Path $RepoRoot "Documentation\T03_DotnetTestGreenMeaningful_$Stamp"
$LatestRoot = Join-Path $RepoRoot "Documentation\T03_DotnetTestGreenMeaningful_Latest"
$ResultsRoot = Join-Path $EvidenceRoot "trx"

New-Item -ItemType Directory -Force -Path $EvidenceRoot, $LatestRoot, $ResultsRoot | Out-Null

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Content
    )

    $Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    $dir = Split-Path $Path -Parent
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Content, $Utf8NoBom)
}

function Add-Finding {
    param(
        [System.Collections.Generic.List[object]]$Findings,
        [string]$Kind,
        [string]$File,
        [int]$Line,
        [string]$Message,
        [string]$Snippet = ""
    )

    $Findings.Add([pscustomobject]@{
        kind = $Kind
        file = $File
        line = $Line
        message = $Message
        snippet = $Snippet
    }) | Out-Null
}

function Get-RelativePath {
    param([string]$Path)

    return [System.IO.Path]::GetRelativePath($RepoRoot, $Path).Replace("\", "/")
}

function Get-TestProjects {
    $backendRoot = Join-Path $RepoRoot "Backend"

    if (-not (Test-Path $backendRoot)) {
        throw "Backend folder not found: $backendRoot"
    }

    $projects =
        Get-ChildItem -Path $backendRoot -Recurse -Filter "*.csproj" |
        Where-Object {
            $_.FullName -notmatch '\\bin\\|\\obj\\' -and
            (
                $_.FullName -match '\\tests\\' -or
                $_.BaseName -match 'Tests?$|\.Tests?$|IntegrationTests$|UnitTests$'
            )
        } |
        Sort-Object FullName

    return @($projects)
}

function Test-NoSkippedTestsInSource {
    $findings = [System.Collections.Generic.List[object]]::new()

    $backendRoot = Join-Path $RepoRoot "Backend"

    $files =
        Get-ChildItem -Path $backendRoot -Recurse -Include "*.cs","*.csproj","*.props","*.targets" -File |
        Where-Object {
            $_.FullName -notmatch '\\bin\\|\\obj\\|\\.vs\\|TestResults\\|coverage\\'
        }

    $patterns = @(
        @{
            kind = "xunit-skip"
            regex = '\[(Fact|Theory)\s*\([^\)]*\bSkip\s*='
            message = "xUnit [Fact]/[Theory] Skip is not allowed in T03 full-suite gate."
        },
        @{
            kind = "explicit-skip"
            regex = '\bSkip\s*=\s*"'
            message = "Explicit test skip is not allowed in T03 full-suite gate."
        },
        @{
            kind = "conditional-test-disable"
            regex = '#if\s+(false|DISABLE|SKIP|IGNORE)'
            message = "Conditional test disable block is not allowed in T03 full-suite gate."
        },
        @{
            kind = "inconclusive-placeholder"
            regex = '\bAssert\.True\s*\(\s*true\s*\)|\bAssert\.Skip\b|\bNotImplementedException\b'
            message = "Placeholder/inconclusive test behavior is not meaningful for T03."
        }
    )

    foreach ($file in $files) {
        $lines = Get-Content -Path $file.FullName

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = [string]$lines[$i]

            foreach ($pattern in $patterns) {
                if ($line -match $pattern.regex) {
                    Add-Finding `
                        -Findings $findings `
                        -Kind $pattern.kind `
                        -File (Get-RelativePath $file.FullName) `
                        -Line ($i + 1) `
                        -Message $pattern.message `
                        -Snippet ($line.Trim())
                }
            }
        }
    }

    return $findings
}

function Test-NoFilteredDotnetTestInCi {
    $findings = [System.Collections.Generic.List[object]]::new()

    $files =
        Get-ChildItem -Path $RepoRoot -Recurse -Include "*.ps1","*.psm1","*.yml","*.yaml","Jenkinsfile","*.cmd","*.bat","*.sh" -File |
        Where-Object {
            $_.FullName -notmatch '\\bin\\|\\obj\\|node_modules\\|dist\\|coverage\\|TestResults\\|\.phase2_backups\\' -and
            $_.FullName -notmatch '\\Documentation\\T03_DotnetTestGreenMeaningful_' -and
            $_.FullName -notmatch '\\tools\\golive\\t03\\'
        }

    foreach ($file in $files) {
        $lines = Get-Content -Path $file.FullName

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = [string]$lines[$i]

            if ($line -match 'dotnet\s+test' -and $line -match '(\-\-filter|TestCaseFilter|VSTestTestCaseFilter)') {
                Add-Finding `
                    -Findings $findings `
                    -Kind "filtered-dotnet-test" `
                    -File (Get-RelativePath $file.FullName) `
                    -Line ($i + 1) `
                    -Message "Backend dotnet test gate must run the full suite; filtered dotnet test is not accepted for T03." `
                    -Snippet ($line.Trim())
            }
        }
    }

    return $findings
}

function Parse-Trx {
    param(
        [string]$Path,
        [string]$ProjectName
    )

    [xml]$trx = Get-Content -Raw -Path $Path

    $ns = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
    $ns.AddNamespace("t", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")

    $counters = $trx.SelectSingleNode("//t:ResultSummary/t:Counters", $ns)

    $unitResults = @($trx.SelectNodes("//t:UnitTestResult", $ns))

    $notPassed =
        $unitResults |
        Where-Object {
            $_.outcome -ne "Passed"
        } |
        ForEach-Object {
            [pscustomobject]@{
                testName = $_.testName
                outcome = $_.outcome
                duration = $_.duration
                message = $_.Output.ErrorInfo.Message
            }
        }

    $notExecuted =
        $unitResults |
        Where-Object {
            $_.outcome -eq "NotExecuted" -or $_.outcome -eq "Skipped"
        }

    return [pscustomobject]@{
        project = $ProjectName
        trx = $Path
        total = [int]$counters.total
        executed = [int]$counters.executed
        passed = [int]$counters.passed
        failed = [int]$counters.failed
        error = [int]$counters.error
        timeout = [int]$counters.timeout
        aborted = [int]$counters.aborted
        inconclusive = [int]$counters.inconclusive
        notExecuted = [int]$counters.notExecuted
        notPassedCount = @($notPassed).Count
        skippedCount = @($notExecuted).Count
        notPassed = @($notPassed)
    }
}

Write-Host "========== T03: Backend dotnet test green + meaningful ==========" -ForegroundColor Cyan

$allFindings = [System.Collections.Generic.List[object]]::new()

$testProjects = Get-TestProjects

if ($testProjects.Count -eq 0) {
    Add-Finding `
        -Findings $allFindings `
        -Kind "no-test-projects" `
        -File "Backend" `
        -Line 0 `
        -Message "No backend test projects were discovered."
}

$sourceFindings = Test-NoSkippedTestsInSource
$ciFindings = Test-NoFilteredDotnetTestInCi

foreach ($finding in $sourceFindings) {
    $allFindings.Add($finding) | Out-Null
}

foreach ($finding in $ciFindings) {
    $allFindings.Add($finding) | Out-Null
}

$staticReport = [pscustomobject]@{
    task = "T03"
    marker = "PPIQ_T03_DOTNET_TEST_GREEN_MEANINGFUL"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    testProjectCount = $testProjects.Count
    testProjects = @($testProjects | ForEach-Object { Get-RelativePath $_.FullName })
    sourceSkipFindingCount = $sourceFindings.Count
    ciFilterFindingCount = $ciFindings.Count
    findingCount = $allFindings.Count
    findings = @($allFindings)
}

$staticJson = $staticReport | ConvertTo-Json -Depth 20
Write-Utf8NoBom -Path (Join-Path $EvidenceRoot "T03_STATIC_GUARD.json") -Content $staticJson
Write-Utf8NoBom -Path (Join-Path $LatestRoot "T03_STATIC_GUARD.json") -Content $staticJson

if ($allFindings.Count -gt 0) {
    $md = @()
    $md += "# T03 Static Guard — FAILED"
    $md += ""
    $md += "- Test projects discovered: $($testProjects.Count)"
    $md += "- Findings: $($allFindings.Count)"
    $md += ""
    foreach ($finding in $allFindings) {
        $md += "- $($finding.file):$($finding.line) [$($finding.kind)] $($finding.message)"
        if ($finding.snippet) {
            $md += "  - `$($finding.snippet)`"
        }
    }

    Write-Utf8NoBom -Path (Join-Path $EvidenceRoot "T03_STATIC_GUARD.md") -Content ($md -join "`r`n")
    Write-Utf8NoBom -Path (Join-Path $LatestRoot "T03_STATIC_GUARD.md") -Content ($md -join "`r`n")

    Write-Host "[RED] T03 static guard failed. See $EvidenceRoot\T03_STATIC_GUARD.md" -ForegroundColor Red
    exit 1
}

Write-Host "[GREEN] T03 static guard passed. TestProjects=$($testProjects.Count)" -ForegroundColor Green

if ($StaticOnly) {
    Write-Host "[GREEN] T03 static-only validation passed." -ForegroundColor Green
    exit 0
}

$projectReports = @()
$overallExitCode = 0

foreach ($project in $testProjects) {
    $projectName = $project.BaseName
    $safeName = $projectName -replace '[^a-zA-Z0-9_.-]', '_'
    $projectResultDir = Join-Path $ResultsRoot $safeName
    New-Item -ItemType Directory -Force -Path $projectResultDir | Out-Null

    Write-Host ""
    Write-Host "Running full backend test project: $($project.FullName)" -ForegroundColor Cyan

    $args = @(
        "test",
        $project.FullName,
        "--configuration",
        "Release",
        "--logger",
        "trx;LogFileName=$safeName.trx",
        "--results-directory",
        $projectResultDir
    )

    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $args `
        -NoNewWindow `
        -PassThru `
        -Wait

    if ($process.ExitCode -ne 0) {
        $overallExitCode = $process.ExitCode
    }

    $trxFiles = @(Get-ChildItem -Path $projectResultDir -Recurse -Filter "*.trx" | Sort-Object LastWriteTime -Descending)

    if ($trxFiles.Count -eq 0) {
        $projectReports += [pscustomobject]@{
            project = $projectName
            projectPath = Get-RelativePath $project.FullName
            status = "FAILED"
            reason = "No TRX result file was produced."
            exitCode = $process.ExitCode
            total = 0
            passed = 0
            failed = 0
            skipped = 0
        }

        $overallExitCode = if ($overallExitCode -eq 0) { 1 } else { $overallExitCode }
        continue
    }

    $parsed = Parse-Trx -Path $trxFiles[0].FullName -ProjectName $projectName

    $status = "PASSED"
    $reason = ""

    if ($process.ExitCode -ne 0) {
        $status = "FAILED"
        $reason = "dotnet test process exit code was $($process.ExitCode)."
    } elseif ($parsed.total -le 0) {
        $status = "FAILED"
        $reason = "Project reported zero tests."
        $overallExitCode = 1
    } elseif ($parsed.failed -gt 0 -or $parsed.error -gt 0 -or $parsed.aborted -gt 0 -or $parsed.timeout -gt 0) {
        $status = "FAILED"
        $reason = "Project has failed/error/aborted/timeout tests."
        $overallExitCode = 1
    } elseif ($parsed.notExecuted -gt 0 -or $parsed.skippedCount -gt 0 -or $parsed.inconclusive -gt 0) {
        $status = "FAILED"
        $reason = "Project has skipped/not-executed/inconclusive tests."
        $overallExitCode = 1
    } elseif ($parsed.executed -ne $parsed.total) {
        $status = "FAILED"
        $reason = "Executed count does not equal total count."
        $overallExitCode = 1
    }

    $projectReports += [pscustomobject]@{
        project = $projectName
        projectPath = Get-RelativePath $project.FullName
        status = $status
        reason = $reason
        exitCode = $process.ExitCode
        trx = Get-RelativePath $trxFiles[0].FullName
        total = $parsed.total
        executed = $parsed.executed
        passed = $parsed.passed
        failed = $parsed.failed
        error = $parsed.error
        timeout = $parsed.timeout
        aborted = $parsed.aborted
        inconclusive = $parsed.inconclusive
        notExecuted = $parsed.notExecuted
        skipped = $parsed.skippedCount
        notPassed = $parsed.notPassed
    }
}

$totalTests = ($projectReports | Measure-Object -Property total -Sum).Sum
$totalPassed = ($projectReports | Measure-Object -Property passed -Sum).Sum
$totalFailed = ($projectReports | Measure-Object -Property failed -Sum).Sum
$totalSkipped = ($projectReports | Measure-Object -Property skipped -Sum).Sum
$totalNotExecuted = ($projectReports | Measure-Object -Property notExecuted -Sum).Sum

$summary = [pscustomobject]@{
    task = "T03"
    marker = "PPIQ_T03_DOTNET_TEST_GREEN_MEANINGFUL"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    backendRoot = "Backend"
    mode = "full-backend-test-project-suite"
    noFilterUsed = $true
    testProjectCount = $testProjects.Count
    totalTests = [int]$totalTests
    totalPassed = [int]$totalPassed
    totalFailed = [int]$totalFailed
    totalSkipped = [int]$totalSkipped
    totalNotExecuted = [int]$totalNotExecuted
    status = if ($overallExitCode -eq 0) { "GREEN" } else { "RED" }
    projects = @($projectReports)
}

$summaryJson = $summary | ConvertTo-Json -Depth 50
Write-Utf8NoBom -Path (Join-Path $EvidenceRoot "T03_DOTNET_TEST_SUMMARY.json") -Content $summaryJson
Write-Utf8NoBom -Path (Join-Path $LatestRoot "T03_DOTNET_TEST_SUMMARY.json") -Content $summaryJson

$md = @()
$md += "# T03 — dotnet test Backend green and meaningful"
$md += ""
$md += "Generated: $((Get-Date).ToString("o"))"
$md += ""
$md += "## Gate"
$md += ""
$md += "- Full backend test projects executed: $($testProjects.Count)"
$md += "- Narrow filter used: NO"
$md += "- Source skip findings: $($sourceFindings.Count)"
$md += "- CI filtered-dotnet-test findings: $($ciFindings.Count)"
$md += "- Total tests: $([int]$totalTests)"
$md += "- Passed: $([int]$totalPassed)"
$md += "- Failed: $([int]$totalFailed)"
$md += "- Skipped / not executed: $([int]($totalSkipped + $totalNotExecuted))"
$md += "- Status: $($summary.status)"
$md += ""
$md += "## Projects"
$md += ""

foreach ($projectReport in $projectReports) {
    $md += "### $($projectReport.project)"
    $md += ""
    $md += "- Status: $($projectReport.status)"
    $md += "- Path: $($projectReport.projectPath)"
    $md += "- Tests: $($projectReport.total)"
    $md += "- Passed: $($projectReport.passed)"
    $md += "- Failed: $($projectReport.failed)"
    $md += "- Skipped / not executed: $([int]($projectReport.skipped + $projectReport.notExecuted))"
    if ($projectReport.reason) {
        $md += "- Reason: $($projectReport.reason)"
    }

    if ($projectReport.notPassed -and @($projectReport.notPassed).Count -gt 0) {
        $md += ""
        $md += "Not-passed tests:"
        foreach ($test in @($projectReport.notPassed)) {
            $md += "- $($test.outcome): $($test.testName) — $($test.message)"
        }
    }

    $md += ""
}

Write-Utf8NoBom -Path (Join-Path $EvidenceRoot "T03_DOTNET_TEST_SUMMARY.md") -Content ($md -join "`r`n")
Write-Utf8NoBom -Path (Join-Path $LatestRoot "T03_DOTNET_TEST_SUMMARY.md") -Content ($md -join "`r`n")

if ($overallExitCode -ne 0) {
    Write-Host "[RED] T03 dotnet test gate failed. Evidence: $EvidenceRoot" -ForegroundColor Red
    exit $overallExitCode
}

Write-Host ""
Write-Host "[GREEN] T03 dotnet test Backend is green and meaningful." -ForegroundColor Green
Write-Host "Test projects: $($testProjects.Count)"
Write-Host "Total tests  : $([int]$totalTests)"
Write-Host "Passed       : $([int]$totalPassed)"
Write-Host "Skipped      : $([int]($totalSkipped + $totalNotExecuted))"
Write-Host "Evidence     : $EvidenceRoot"