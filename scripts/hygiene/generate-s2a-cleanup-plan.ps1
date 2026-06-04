param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ"
)

$ErrorActionPreference = "Stop"

$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$ReportRoot = Join-Path $RepoRoot "Documentation\hygiene"
New-Item -ItemType Directory -Force -Path $ReportRoot | Out-Null

$Items = New-Object System.Collections.Generic.List[object]

function Get-DirectoryStats {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return [pscustomobject]@{
            Exists = $false
            FileCount = 0
            DirectoryCount = 0
            SizeMB = 0
        }
    }

    $Files = Get-ChildItem -Path $Path -File -Recurse -Force -ErrorAction SilentlyContinue
    $Dirs = Get-ChildItem -Path $Path -Directory -Recurse -Force -ErrorAction SilentlyContinue
    $Bytes = ($Files | Measure-Object -Property Length -Sum).Sum

    if (-not $Bytes) { $Bytes = 0 }

    return [pscustomobject]@{
        Exists = $true
        FileCount = @($Files).Count
        DirectoryCount = @($Dirs).Count
        SizeMB = [math]::Round($Bytes / 1MB, 3)
    }
}

function Add-CleanupItem {
    param(
        [string]$RelativePath,
        [string]$Area,
        [string]$Action,
        [string]$Risk,
        [string]$Reason,
        [string]$RecommendedTarget,
        [string]$Decision = "Pending"
    )

    $FullPath = Join-Path $RepoRoot $RelativePath
    $Exists = Test-Path $FullPath

    if (-not $Exists) {
        return
    }

    $Item = Get-Item $FullPath -Force
    $Stats = if ($Item.PSIsContainer) { Get-DirectoryStats $FullPath } else {
        [pscustomobject]@{
            Exists = $true
            FileCount = 1
            DirectoryCount = 0
            SizeMB = [math]::Round($Item.Length / 1MB, 3)
        }
    }

    $Items.Add([pscustomobject]@{
        RelativePath = $RelativePath
        Exists = $Exists
        ItemType = if ($Item.PSIsContainer) { "Directory" } else { "File" }
        Area = $Area
        ProposedAction = $Action
        Risk = $Risk
        FileCount = $Stats.FileCount
        DirectoryCount = $Stats.DirectoryCount
        SizeMB = $Stats.SizeMB
        Reason = $Reason
        RecommendedTarget = $RecommendedTarget
        Decision = $Decision
    })
}

# ============================================================
# 1) High-value known hotspots
# ============================================================

Add-CleanupItem "tools" "Root tooling" "REVIEW_AND_SPLIT" "High" `
    "Root tools contains active validators, old phase packs, backups, purged artifacts, and generated historical files. It is currently the biggest repo hygiene hotspot." `
    "Keep scripts\*, archive old tools to archive\tools, convert important validators into tests."

Add-CleanupItem "tools\validation" "Validation scripts" "CONVERT_TO_TEST_OR_ARCHIVE" "High" `
    "Validation scripts should not be the long-term source of truth when unit/integration/E2E tests can enforce the same checks." `
    "Move active checks into test projects; archive historical validators."

Add-CleanupItem "tools\v5" "V5 pack scripts" "ARCHIVE_AFTER_GREEN" "Medium" `
    "Phase/pack scripts are useful during implementation but should not stay mixed with product tooling forever." `
    "archive\implementation-packs\v5"

Add-CleanupItem "Backend\tools" "Backend tooling" "REVIEW_AND_KEEP_ONLY_ACTIVE" "Medium" `
    "Backend tools include audit generators, synthetic data generators, phase smoke scripts, and validation scripts. Keep only active operational tools." `
    "Backend\tools for active generators only; archive old phase validators."

Add-CleanupItem "Backend\tools\validation" "Backend validation scripts" "CONVERT_TO_TEST_OR_ARCHIVE" "Medium" `
    "Backend validation should become C# unit/integration tests where possible." `
    "Backend\tests or archive\validation"

Add-CleanupItem "Backend\src" "Legacy backend source" "MANUAL_REVIEW_LEGACY_SOURCE" "High" `
    "Backend\src appears outside the current clean solution structure and contains PlantProcessIQ.* prototype-style folders." `
    "Delete if unreferenced; otherwise migrate into current Backend projects."

Add-CleanupItem "Frontend\src" "Legacy frontend source" "MANUAL_REVIEW_LEGACY_SOURCE" "High" `
    "Frontend\src appears outside Frontend\PlantProcess.Web and may be old prototype source." `
    "Delete if unreferenced; otherwise migrate into Frontend\PlantProcess.Web\src."

Add-CleanupItem "Frontend\tools" "Frontend tooling" "MANUAL_REVIEW" "Medium" `
    "Standalone frontend tools may duplicate root scripts or app scripts." `
    "Move active tools to Frontend\PlantProcess.Web\scripts or root scripts."

Add-CleanupItem "Frontend\PlantProcess.Web\codemods" "Frontend codemods" "ARCHIVE_IF_ONE_TIME" "Low" `
    "Codemods are usually one-time migration tools. Keep only if actively reused." `
    "archive\codemods or tools\codemods"

Add-CleanupItem "Frontend\PlantProcess.Web\storybook-static" "Generated frontend artifact" "DELETE_GENERATED" "Low" `
    "storybook-static is generated output and should not be committed or included in audit packages." `
    "Delete; add to .gitignore."

Add-CleanupItem "Frontend\PlantProcess.Web\scripts" "Frontend scripts" "REVIEW_AND_KEEP_ONLY_ACTIVE" "Medium" `
    "Frontend scripts include audits, probes, codemods, and validation helpers. Keep active operational scripts only." `
    "Frontend\PlantProcess.Web\scripts for active app scripts; archive one-time scripts."

Add-CleanupItem "docs" "Documentation" "REVIEW_AND_RESTRUCTURE" "Medium" `
    "Docs contain useful product docs mixed with phase/pack historical docs." `
    "docs\product, docs\deployment, docs\testing, archive\docs\implementation-history."

Add-CleanupItem "Backend\database" "Database scripts/seeds/views" "RESTRUCTURE_DATABASE_PACKS" "High" `
    "Database folder has many scripts, phase scripts, hotfixes, seeds, views, and generated source-system inserts. Needs deployment-friendly structure." `
    "Backend\database\migrations, seed\demo, views, deprecated, manual."

Add-CleanupItem "deploy" "Deployment root" "KEEP_AS_CANONICAL_DEPLOY_ROOT" "Low" `
    "This should become the one canonical deployment root." `
    "deploy"

Add-CleanupItem "deployment" "Duplicate deployment folder" "MOVE_TO_DEPLOY" "Medium" `
    "Duplicate deployment folder. Caddy config should live under one deploy root." `
    "deploy\caddy"

Add-CleanupItem "Infrastructure\deploy" "Duplicate infrastructure deploy folder" "MOVE_TO_DEPLOY" "Medium" `
    "Infrastructure\deploy contains env files, Caddyfile, compose and server scripts, but deployment ownership should be centralized." `
    "deploy\server or deploy\infra"

Add-CleanupItem "Jenkinsfile" "CI/CD root file" "DECIDE_KEEP_ROOT_OR_MOVE" "Medium" `
    "Jenkins often expects root Jenkinsfile, but repo hygiene may prefer deploy\jenkins. Decision depends on actual Jenkins configuration." `
    "Keep root as thin pointer or move to deploy\jenkins\Jenkinsfile."

Add-CleanupItem "docker-compose.demo-sources.yml" "Demo source compose" "MOVE_TO_DEPLOY" "Medium" `
    "Root-level demo-source compose works, but should live under canonical deploy folder." `
    "deploy\demo-sources\docker-compose.demo-sources.yml"

# ============================================================
# 2) Generated/build artifact scan
# ============================================================

$GeneratedDirNames = @(
    "storybook-static",
    "dist",
    "build",
    "coverage",
    "playwright-report",
    "test-results",
    ".vite",
    ".cache"
)

foreach ($Name in $GeneratedDirNames) {
    Get-ChildItem -Path $RepoRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -eq $Name -and
            $_.FullName -notmatch "\\node_modules\\"
        } |
        ForEach-Object {
            $Relative = $_.FullName.Substring($RepoRoot.Length).TrimStart("\")
            Add-CleanupItem $Relative "Generated artifacts" "DELETE_GENERATED" "Low" `
                "Generated build/test/storybook output should not be committed or included in audit packages." `
                "Delete and add ignore rule."
        }
}

# ============================================================
# 3) Backup/temp/pack artifact scan
# ============================================================

Get-ChildItem -Path $RepoRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch "\\node_modules\\" -and
        (
            $_.Name -match "^_pack_" -or
            $_.Name -match "^_s[0-9a-zA-Z]+_backup_" -or
            $_.Name -match "^_v5_" -or
            $_.Name -match "^_p[0-9]" -or
            $_.Name -eq "purged-artifacts" -or
            $_.Name -eq "tmp" -or
            $_.Name -match "backup"
        )
    } |
    Select-Object -First 300 |
    ForEach-Object {
        $Relative = $_.FullName.Substring($RepoRoot.Length).TrimStart("\")
        Add-CleanupItem $Relative "Backups/temp implementation artifacts" "ARCHIVE_OR_DELETE_AFTER_CONFIRMATION" "Medium" `
            "Backup/temp/pack artifacts should not remain in the active product repo." `
            "archive\implementation-history or delete after branch backup."
    }

Get-ChildItem -Path $RepoRoot -File -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch "\\node_modules\\" -and
        (
            $_.Name -match "\.bak" -or
            $_.Extension -match "\.bak" -or
            $_.Name -eq "-replace"
        )
    } |
    Select-Object -First 300 |
    ForEach-Object {
        $Relative = $_.FullName.Substring($RepoRoot.Length).TrimStart("\")
        Add-CleanupItem $Relative "Backup files" "ARCHIVE_OR_DELETE_AFTER_CONFIRMATION" "Low" `
            "Backup files should not remain in active repo source tree." `
            "archive\implementation-history or delete after branch backup."
    }

# ============================================================
# 4) E2E consolidation scan
# ============================================================

$E2ERoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web\e2e"

if (Test-Path $E2ERoot) {
    Get-ChildItem -Path $E2ERoot -File -Recurse -Force -Include "*.spec.ts" |
        Where-Object {
            $_.Name -match "phase|p[0-9]-|dimension|golden|hardening|lifecycle"
        } |
        ForEach-Object {
            $Relative = $_.FullName.Substring($RepoRoot.Length).TrimStart("\")
            Add-CleanupItem $Relative "E2E tests" "REVIEW_FOR_S3_CONSOLIDATION" "Medium" `
                "Phase-named or old journey E2E tests are likely unstable against current product routes and should be consolidated into product journeys." `
                "Frontend\PlantProcess.Web\e2e\archive or new product journey suite."
        }
}

# ============================================================
# 5) Database script naming scan
# ============================================================

$DbScriptsRoot = Join-Path $RepoRoot "Backend\database\scripts"

if (Test-Path $DbScriptsRoot) {
    Get-ChildItem -Path $DbScriptsRoot -File -Force -Include "*.sql" |
        Where-Object {
            $_.Name -match "phase|hotfix|fix|pack|remaining|completion"
        } |
        ForEach-Object {
            $Relative = $_.FullName.Substring($RepoRoot.Length).TrimStart("\")
            Add-CleanupItem $Relative "Database scripts" "REVIEW_FOR_DATABASE_RESTRUCTURE" "High" `
                "Phase/hotfix database scripts should be organized into stable migrations, manual repairs, deprecated scripts, and demo seeds." `
                "Backend\database\migrations or Backend\database\deprecated"
        }
}

# ============================================================
# 6) Produce reports
# ============================================================

$JsonPath = Join-Path $ReportRoot "S2A_Repo_Cleanup_DryRun_$Stamp.json"
$CsvPath = Join-Path $ReportRoot "S2A_Repo_Cleanup_DryRun_$Stamp.csv"
$MdPath = Join-Path $ReportRoot "S2A_Repo_Cleanup_DryRun_$Stamp.md"

$Items |
    Sort-Object ProposedAction, Area, RelativePath |
    ConvertTo-Json -Depth 6 |
    Set-Content $JsonPath -Encoding utf8

$Items |
    Sort-Object ProposedAction, Area, RelativePath |
    Export-Csv -Path $CsvPath -NoTypeInformation -Encoding UTF8

$Summary = $Items |
    Group-Object ProposedAction |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            ProposedAction = $_.Name
            Count = $_.Count
            TotalFiles = ($_.Group | Measure-Object -Property FileCount -Sum).Sum
            TotalSizeMB = [math]::Round((($_.Group | Measure-Object -Property SizeMB -Sum).Sum), 3)
        }
    }

$Lines = New-Object System.Collections.Generic.List[string]

$Lines.Add("# PlantProcess IQ — S2A Repo Cleanup Dry-Run Report")
$Lines.Add("")
$Lines.Add("Generated at: $((Get-Date).ToString("yyyy-MM-dd HH:mm:ss"))")
$Lines.Add("")
$Lines.Add("This report is non-destructive. No files were moved or deleted.")
$Lines.Add("")
$Lines.Add("## Executive Summary")
$Lines.Add("")
$Lines.Add("The repo contains valuable product implementation, but active source, deployment assets, historical patch packs, generated artifacts, validation scripts, and prototype folders are mixed together.")
$Lines.Add("")
$Lines.Add("S2 should clean the repo in two steps:")
$Lines.Add("")
$Lines.Add("1. Approve this dry-run classification.")
$Lines.Add("2. Execute S2B to move/archive/delete only approved categories.")
$Lines.Add("")
$Lines.Add("## Action Summary")
$Lines.Add("")
$Lines.Add("| Proposed Action | Items | Files Impacted | Size MB |")
$Lines.Add("|---|---:|---:|---:|")

foreach ($Row in $Summary) {
    $Lines.Add("| $($Row.ProposedAction) | $($Row.Count) | $($Row.TotalFiles) | $($Row.TotalSizeMB) |")
}

$Lines.Add("")
$Lines.Add("## Highest Priority Decisions")
$Lines.Add("")
$Lines.Add("| Priority | Area | Decision Needed |")
$Lines.Add("|---:|---|---|")
$Lines.Add("| 1 | `Frontend\PlantProcess.Web\storybook-static` | Delete generated artifact and ignore it. |")
$Lines.Add("| 2 | `deploy` / `deployment` / `Infrastructure\deploy` | Consolidate to one canonical `deploy` root. |")
$Lines.Add("| 3 | `tools` and backup/purged artifacts | Keep active scripts only; archive or delete historical pack backups. |")
$Lines.Add("| 4 | `Backend\src` and `Frontend\src` | Verify unreferenced legacy/prototype source, then remove/archive. |")
$Lines.Add("| 5 | `Backend\database` | Restructure scripts into migrations, seed/demo, views, manual repairs, deprecated. |")
$Lines.Add("| 6 | E2E tests | Consolidate phase-named E2E into stable product journey tests. |")
$Lines.Add("")
$Lines.Add("## Detailed Items")
$Lines.Add("")
$Lines.Add("| Action | Risk | Area | Path | Files | Size MB | Reason | Target |")
$Lines.Add("|---|---|---|---|---:|---:|---|---|")

foreach ($Item in ($Items | Sort-Object ProposedAction, Area, RelativePath)) {
    $Reason = ($Item.Reason -replace "\|", "/")
    $Target = ($Item.RecommendedTarget -replace "\|", "/")
    $Lines.Add("| $($Item.ProposedAction) | $($Item.Risk) | $($Item.Area) | `$($Item.RelativePath)` | $($Item.FileCount) | $($Item.SizeMB) | $Reason | $Target |")
}

$Lines.Add("")
$Lines.Add("## Recommended Next Step")
$Lines.Add("")
$Lines.Add("Run S2B only after reviewing this report. S2B should be split into safe cleanup batches, starting with generated artifacts and backup folders.")

$Lines | Set-Content $MdPath -Encoding utf8

Write-Host ""
Write-Host "[S2A GREEN] Repo cleanup dry-run report generated." -ForegroundColor Green
Write-Host "Markdown: $MdPath" -ForegroundColor Yellow
Write-Host "JSON    : $JsonPath" -ForegroundColor Yellow
Write-Host "CSV     : $CsvPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "Action summary:" -ForegroundColor Cyan
$Summary | Format-Table -AutoSize
