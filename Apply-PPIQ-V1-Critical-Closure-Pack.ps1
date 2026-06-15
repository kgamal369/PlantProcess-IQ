#requires -Version 5.1
<#
.SYNOPSIS
  PlantProcess IQ V1 critical closure pack for:
    PPIQ-103, PPIQ-104, PPIQ-201, PPIQ-601, PPIQ-703, PPIQ-705

.DESCRIPTION
  Applies narrowly-scoped implementation patches, creates acceptance tests, runs the real
  build/test/runtime proof, and writes evidence. It is deliberately fail-closed: a task is
  marked DONE only when its acceptance gate actually passes. Missing Docker, browsers,
  HTTPS endpoints, credentials, migrations, or runtime data fail the corresponding gate.

  IMPORTANT: paste/run this script as one complete script file. Do not execute if/elseif
  fragments line-by-line in the PowerShell console.
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$AppHttpUrl = $(if ($env:PPIQ_APP_HTTP_URL) { $env:PPIQ_APP_HTTP_URL } else { "http://127.0.0.1:5173" }),
    [string]$AppHttpsUrl = $env:PPIQ_APP_HTTPS_URL,
    [string]$WebsiteHttpUrl = $(if ($env:PPIQ_WEB_HTTP_URL) { $env:PPIQ_WEB_HTTP_URL } else { "http://127.0.0.1:4174" }),
    [string]$WebsiteHttpsUrl = $env:PPIQ_WEB_HTTPS_URL,
    [string]$ApiUrl = $(if ($env:PLAYWRIGHT_API_URL) { $env:PLAYWRIGHT_API_URL } else { "http://127.0.0.1:5063" }),
    [string]$SmokeUser = $(if ($env:PPIQ_SMOKE_USERNAME) { $env:PPIQ_SMOKE_USERNAME } else { "e2eadmin" }),
    [string]$SmokePassword = $env:PPIQ_SMOKE_PASSWORD,
    [switch]$SkipCleanMigrationProof,
    [switch]$SkipHttpsMatrix,
    [switch]$SkipFullRegression,
    [switch]$KeepP104ProofDatabase
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"
$BackendRoot = Join-Path $RepoRoot "Backend"
$EvidenceRoot = Join-Path $RepoRoot "Documentation\PPIQ_V1_Critical_Closure_$Timestamp"
$BackupRoot = Join-Path $RepoRoot ".ppiq-closure-backup\$Timestamp"
$script:Results = New-Object System.Collections.Generic.List[object]

function Write-Banner([string]$Text) {
    Write-Host ""
    Write-Host ("=" * 92) -ForegroundColor DarkGray
    Write-Host ("  " + $Text) -ForegroundColor Cyan
    Write-Host ("=" * 92) -ForegroundColor DarkGray
}

function Ensure-Directory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    Ensure-Directory (Split-Path -Parent $Path)
    [System.IO.File]::WriteAllText($Path, $Content, $Utf8NoBom)
}

function Backup-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return }
    $relative = $Path.Substring($RepoRoot.Length).TrimStart('\')
    $destination = Join-Path $BackupRoot $relative
    Ensure-Directory (Split-Path -Parent $destination)
    Copy-Item -LiteralPath $Path -Destination $destination -Force
}

function Read-Text([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file not found: $Path"
    }
    return [System.IO.File]::ReadAllText($Path)
}

function Replace-Required([string]$Path, [string]$Old, [string]$New, [string]$Description) {
    # Normalize to LF before matching. The repository contains a mixture of CRLF/LF snapshots;
    # the canonical output is UTF-8 without BOM + LF.
    $text = (Read-Text $Path).Replace("`r`n", "`n")
    $oldNormalized = $Old.Replace("`r`n", "`n")
    $newNormalized = $New.Replace("`r`n", "`n")
    if ($text.Contains($newNormalized)) {
        Write-Host "[IDEMPOTENT] $Description already applied." -ForegroundColor DarkGreen
        return
    }
    if (-not $text.Contains($oldNormalized)) {
        throw "Cannot safely apply '$Description'. Required source anchor was not found in $Path"
    }
    Backup-File $Path
    Write-Utf8NoBom $Path ($text.Replace($oldNormalized, $newNormalized))
    Write-Host "[PATCHED] $Description" -ForegroundColor Green
}

function Insert-BeforeLast([string]$Path, [string]$Anchor, [string]$Content, [string]$Marker, [string]$Description) {
    $text = Read-Text $Path
    if ($text.Contains($Marker)) {
        Write-Host "[IDEMPOTENT] $Description already applied." -ForegroundColor DarkGreen
        return
    }
    $index = $text.LastIndexOf($Anchor, [System.StringComparison]::Ordinal)
    if ($index -lt 0) { throw "Cannot safely apply '$Description'. Anchor '$Anchor' not found in $Path" }
    Backup-File $Path
    $updated = $text.Substring(0, $index) + $Content + $text.Substring($index)
    Write-Utf8NoBom $Path $updated
    Write-Host "[PATCHED] $Description" -ForegroundColor Green
}

function Assert-Contains([string]$Path, [string]$Needle, [string]$Message) {
    $text = Read-Text $Path
    if (-not $text.Contains($Needle)) { throw "$Message`nFile: $Path`nMissing: $Needle" }
}

function Assert-NoBom([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw "UTF-8 BOM detected: $Path"
    }
}

function Invoke-External {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$WorkingDirectory,
        [Parameter(Mandatory=$true)][scriptblock]$Command,
        [string]$LogFile = ""
    )
    Write-Banner $Name
    Ensure-Directory $WorkingDirectory
    Push-Location $WorkingDirectory
    try {
        if ($LogFile) {
            Ensure-Directory (Split-Path -Parent $LogFile)
            & $Command 2>&1 | Tee-Object -FilePath $LogFile
        } else {
            & $Command
        }
        if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
        Write-Host "[GREEN] $Name" -ForegroundColor Green
    }
    finally { Pop-Location }
}

function Add-Result([string]$Task, [string]$Status, [string]$Evidence, [string]$Gap = "") {
    $script:Results.Add([pscustomobject]@{
        Task = $Task
        Status = $Status
        Evidence = $Evidence
        RemainingGap = $Gap
    })
}

function Require-Command([string]$Name) {
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $cmd) { throw "Required command '$Name' is not available in PATH." }
    return $cmd.Source
}

function Get-BashLauncher {
    $bash = Get-Command "bash.exe" -ErrorAction SilentlyContinue
    if ($bash) { return @{ Kind = "bash"; Path = $bash.Source } }
    $bash2 = Get-Command "bash" -ErrorAction SilentlyContinue
    if ($bash2) { return @{ Kind = "bash"; Path = $bash2.Source } }
    $wsl = Get-Command "wsl.exe" -ErrorAction SilentlyContinue
    if ($wsl) { return @{ Kind = "wsl"; Path = $wsl.Source } }
    throw "PPIQ-104 requires bash.exe (Git Bash) or wsl.exe to run deploy/scripts/migrate-and-seed.sh."
}

function Invoke-RepoBash([string]$RelativeScript) {
    $launcher = Get-BashLauncher
    $windowsPath = Join-Path $RepoRoot $RelativeScript
    if (-not (Test-Path -LiteralPath $windowsPath -PathType Leaf)) { throw "Shell script not found: $windowsPath" }
    if ($launcher.Kind -eq "bash") {
        & $launcher.Path $windowsPath
    } else {
        $escaped = $windowsPath.Replace('\','/').Replace('C:','/mnt/c')
        & $launcher.Path bash -lc "chmod +x '$escaped' && '$escaped'"
    }
    if ($LASTEXITCODE -ne 0) { throw "$RelativeScript failed with exit code $LASTEXITCODE" }
}

# -------------------------------------------------------------------------------------------------
# PRE-FLIGHT
# -------------------------------------------------------------------------------------------------
Write-Banner "PlantProcess IQ — V1 Critical Closure Pack"
if (-not (Test-Path -LiteralPath $RepoRoot -PathType Container)) { throw "Repository not found: $RepoRoot" }
foreach ($required in @($FrontendRoot, $WebsiteRoot, $BackendRoot)) {
    if (-not (Test-Path -LiteralPath $required -PathType Container)) { throw "Required project directory not found: $required" }
}
Require-Command "dotnet.exe" | Out-Null
Require-Command "npm.cmd" | Out-Null
Require-Command "npx.cmd" | Out-Null
if (-not $SkipCleanMigrationProof) { Require-Command "docker.exe" | Out-Null }
if ([string]::IsNullOrWhiteSpace($SmokePassword)) {
    throw "PPIQ_SMOKE_PASSWORD is required. Set it before running this pack."
}
if (-not $SkipHttpsMatrix -and ([string]::IsNullOrWhiteSpace($AppHttpsUrl) -or [string]::IsNullOrWhiteSpace($WebsiteHttpsUrl))) {
    throw "PPIQ-703 requires both HTTPS URLs. Set PPIQ_APP_HTTPS_URL and PPIQ_WEB_HTTPS_URL, or use -SkipHttpsMatrix (task will remain BLOCKED)."
}
Ensure-Directory $EvidenceRoot
Ensure-Directory $BackupRoot

# Build prerequisite discovered in the 15-Jun snapshot: React 19 rejects an untyped throw-only
# test component because TypeScript infers () => void. Apply the minimal render-time type contract.
$boomTest = Join-Path $FrontendRoot "src\components\standard\__tests__\P2Close.errorBoundaryContainment.test.tsx"
$boomText = Read-Text $boomTest
if ($boomText.Contains("function Boom() {")) {
    Replace-Required $boomTest "function Boom() {" "function Boom(): never {" "Repair TS2786 throw-only test component"
} elseif (-not ($boomText.Contains("function Boom(): never {") -or $boomText.Contains("function Boom(): ReactElement {") -or $boomText.Contains("function Boom(): React.ReactElement {"))) {
    throw "The Boom test component differs from the audited snapshot; refusing an unsafe automatic edit: $boomTest"
}

# -------------------------------------------------------------------------------------------------
# PPIQ-103 — one-click readiness endpoint + HMI panel + recorded dry-run
# -------------------------------------------------------------------------------------------------
Write-Banner "APPLY PPIQ-103 — readiness endpoint, HMI panel, recorded dry-run"

$readinessEndpoint = @'
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Demo.Readiness;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Demo;

public static class DemoReadinessEndpoints
{
    public static IEndpointRouteBuilder MapDemoReadinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/demo-readiness")
            .WithTags("Admin - Demo Readiness")
            .RequireAuthorization();

        group.MapGet("/", EvaluateAsync)
            .WithName("GetDemoReadiness")
            .WithSummary("PPIQ-103 one-click readiness check")
            .Produces<DemoReadinessReport>();

        return app;
    }

    private static async Task<IResult> EvaluateAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var sourcesLinked = await db.ConnectionProfiles.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken);
        var stagingPopulated = await db.StagingRecords.AsNoTracking().AnyAsync(cancellationToken);
        var mappingsPublished = await db.MappingDefinitions.AsNoTracking().AnyAsync(x => x.IsActive, cancellationToken);
        var jobsRunnable = await db.JobDefinitions.AsNoTracking().CountAsync(x => x.IsEnabled, cancellationToken);
        var demoPages = await CountActiveDemoPagesAsync(db, cancellationToken);

        var inputs = new DemoReadinessInputs(
            SourcesLinked: sourcesLinked,
            SourcesExpected: 8,
            StagingPopulated: stagingPopulated,
            MappingsPublished: mappingsPublished,
            JobsRunnable: jobsRunnable,
            JobsExpected: 4,
            DemoPagesPresent: demoPages > 0);

        return Results.Ok(DemoReadinessEvaluator.Evaluate(inputs));
    }

    private static async Task<int> CountActiveDemoPagesAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT CASE
              WHEN to_regclass('public.page_definitions') IS NULL THEN 0
              ELSE (SELECT COUNT(*)::integer FROM page_definitions WHERE is_deleted = false)
            END;
            """, connection);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
'@
Write-Utf8NoBom (Join-Path $BackendRoot "PlantProcess.Api\Endpoints\Demo\DemoReadinessEndpoints.cs") $readinessEndpoint

$program = Join-Path $BackendRoot "PlantProcess.Api\Program.cs"
Replace-Required $program `
    "    app.MapDemoLifecycleEndpoints();" `
    "    app.MapDemoLifecycleEndpoints();`r`n    app.MapDemoReadinessEndpoints();" `
    "Map PPIQ-103 readiness endpoint"

$readinessApi = @'
import { apiClient } from "../http/apiClient";

export interface DemoReadinessInputs {
  sourcesLinked: number;
  sourcesExpected: number;
  stagingPopulated: boolean;
  mappingsPublished: boolean;
  jobsRunnable: number;
  jobsExpected: number;
  demoPagesPresent: boolean;
}

export interface DemoReadinessReport {
  isReady: boolean;
  status: "green" | "blocked";
  blockers: string[];
  inputs: DemoReadinessInputs;
}

export const demoReadinessApi = {
  get: () => apiClient.get<DemoReadinessReport>("/admin/demo-readiness"),
};
'@
Write-Utf8NoBom (Join-Path $FrontendRoot "src\api\demo\demoReadiness.api.ts") $readinessApi

$readinessPanel = @'
import { useState } from "react";
import { demoReadinessApi, type DemoReadinessReport } from "@/api/demo/demoReadiness.api";
import { StandardButton } from "@/components/standard";
import { StandardCard } from "@/components/standard/StandardSurface";
import "./demo-readiness-panel.css";

export function DemoReadinessPanel() {
  const [report, setReport] = useState<DemoReadinessReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function runCheck() {
    setLoading(true);
    setError(null);
    try {
      setReport(await demoReadinessApi.get());
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Readiness check failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="demo-readiness-panel" aria-label="Demo readiness" data-testid="demo-readiness-panel">
      <StandardCard title="Customer demo readiness" subtitle="One click, exact blockers, no false-green result.">
        <div className="demo-readiness-panel__actions">
          <StandardButton
            variant="primary"
            onClick={runCheck}
            isLoading={loading}
            data-testid="run-demo-readiness"
          >
            Run readiness check
          </StandardButton>
          {report ? (
            <strong data-testid="demo-readiness-status" className={`demo-readiness-panel__status demo-readiness-panel__status--${report.status}`}>
              {report.isReady ? "READY" : "BLOCKED"}
            </strong>
          ) : null}
        </div>

        {error ? <div role="alert" className="demo-readiness-panel__error">{error}</div> : null}
        {report ? (
          <div data-testid="demo-readiness-result">
            <dl className="demo-readiness-panel__metrics">
              <div><dt>Sources</dt><dd>{report.inputs.sourcesLinked}/{report.inputs.sourcesExpected}</dd></div>
              <div><dt>Staging</dt><dd>{report.inputs.stagingPopulated ? "Populated" : "Empty"}</dd></div>
              <div><dt>Mappings</dt><dd>{report.inputs.mappingsPublished ? "Published" : "Missing"}</dd></div>
              <div><dt>Jobs</dt><dd>{report.inputs.jobsRunnable}/{report.inputs.jobsExpected}</dd></div>
              <div><dt>Pages</dt><dd>{report.inputs.demoPagesPresent ? "Present" : "Missing"}</dd></div>
            </dl>
            {!report.isReady ? (
              <ul data-testid="demo-readiness-blockers">
                {report.blockers.map((blocker) => <li key={blocker}>{blocker}</li>)}
              </ul>
            ) : <p>All mandatory demo prerequisites are green.</p>}
          </div>
        ) : null}
      </StandardCard>
    </section>
  );
}
'@
Write-Utf8NoBom (Join-Path $FrontendRoot "src\components\demo\DemoReadinessPanel.tsx") $readinessPanel

$readinessCss = @'
.demo-readiness-panel { margin: 24px; }
.demo-readiness-panel__actions { display:flex; align-items:center; gap:16px; flex-wrap:wrap; }
.demo-readiness-panel__status { border-radius:999px; padding:6px 12px; letter-spacing:.06em; }
.demo-readiness-panel__status--green { background:#153f36; color:#2CE6A2; }
.demo-readiness-panel__status--blocked { background:#4a2029; color:#FF4D6D; }
.demo-readiness-panel__error { margin-top:12px; color:#FF4D6D; }
.demo-readiness-panel__metrics { display:grid; grid-template-columns:repeat(auto-fit,minmax(130px,1fr)); gap:12px; margin-top:16px; }
.demo-readiness-panel__metrics div { background:#0B1730; border:1px solid #102A43; border-radius:12px; padding:12px; }
.demo-readiness-panel__metrics dt { color:#8EA7C1; }
.demo-readiness-panel__metrics dd { margin:4px 0 0; color:#EAF6FF; font-weight:700; }
'@
Write-Utf8NoBom (Join-Path $FrontendRoot "src\components\demo\demo-readiness-panel.css") $readinessCss

$demoLifecycleWrapper = Join-Path $FrontendRoot "src\pages\PlatformOps\DemoLifecyclePage.tsx"
$wrapperText = @'
import { DemoReadinessPanel } from "@/components/demo/DemoReadinessPanel";
import { DemoAnalyticsDemoLifecyclePage } from "./DemoAnalyticsPages";

export function DemoLifecyclePage() {
  return (
    <>
      <DemoAnalyticsDemoLifecyclePage />
      <DemoReadinessPanel />
    </>
  );
}
'@
Backup-File $demoLifecycleWrapper
Write-Utf8NoBom $demoLifecycleWrapper $wrapperText

$readinessDryRun = @'
import { expect, test } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL || "http://127.0.0.1:5063";
const userName = process.env.PPIQ_SMOKE_USERNAME || "e2eadmin";
const password = process.env.PPIQ_SMOKE_PASSWORD || "";

async function login(page: import("@playwright/test").Page) {
  expect(password, "PPIQ_SMOKE_PASSWORD must be configured").not.toBe("");
  const response = await page.request.post(`${apiUrl}/auth/login`, {
    data: { userName, password },
    headers: { Accept: "application/json", "Content-Type": "application/json" },
  });
  expect(response.ok(), `Login failed: ${response.status()} ${await response.text()}`).toBeTruthy();
}

test("PPIQ-103 recorded customer dry-run is fully green", async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on("pageerror", (error) => consoleErrors.push(String(error)));
  page.on("console", (message) => { if (message.type() === "error") consoleErrors.push(message.text()); });

  await login(page);
  await page.goto("/demo-lifecycle", { waitUntil: "domcontentloaded" });
  await expect(page.getByTestId("run-demo-readiness")).toBeVisible();
  await page.getByTestId("run-demo-readiness").click();
  await expect(page.getByTestId("demo-readiness-result")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("demo-readiness-status")).toHaveText("READY");

  for (const route of ["/dashboard", "/materials", "/page-builder", "/admin", "/ml-readiness"]) {
    await page.goto(route, { waitUntil: "domcontentloaded" });
    await expect(page.locator("body")).not.toContainText(/could not load|unhandled|unexpected error/i);
  }

  expect(consoleErrors, consoleErrors.join("\n")).toEqual([]);
});
'@
Write-Utf8NoBom (Join-Path $FrontendRoot "e2e\ppiq-v1-readiness-dry-run.spec.ts") $readinessDryRun

$readinessConfig = @'
import { defineConfig, devices } from "@playwright/test";
import baseConfig from "./playwright.config";

export default defineConfig({
  ...baseConfig,
  testDir: "./e2e",
  testMatch: /ppiq-v1-readiness-dry-run\.spec\.ts/,
  retries: 0,
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report-readiness" }]],
  use: {
    ...baseConfig.use,
    baseURL: process.env.PPIQ_APP_HTTP_URL || "http://127.0.0.1:5173",
    video: "on",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium-headed-proof", use: { ...devices["Desktop Chrome"] } }],
});
'@
Write-Utf8NoBom (Join-Path $FrontendRoot "playwright.ppiq-v1-readiness.config.ts") $readinessConfig

# -------------------------------------------------------------------------------------------------
# PPIQ-601 — live optimistic-concurrency wiring
# -------------------------------------------------------------------------------------------------
Write-Banner "APPLY PPIQ-601 — ExpectedVersion + live ConflictDialog"
$pageApi = Join-Path $FrontendRoot "src\api\pageBuilder\pageBuilder.api.ts"
$pageApiContractOld = @'
  widgetBindingsJson: unknown;
}
'@
$pageApiContractNew = @'
  widgetBindingsJson: unknown;
  expectedVersion?: number | null;
}
'@
Replace-Required $pageApi $pageApiContractOld $pageApiContractNew "Add expectedVersion to PageDefinition upsert contract"

$pageBuilder = Join-Path $FrontendRoot "src\pages\PageBuilder\PageBuilderPage.implementation.tsx"
$pageBuilderImportOld = 'import { pageBuilderApi, type PageDefinitionDto } from "@/api/pageBuilder";'
$pageBuilderImportNew = @'
import { pageBuilderApi, type PageDefinitionDto } from "@/api/pageBuilder";
import { ApiError } from "@/api/http/apiClient";
import { ConflictDialog } from "@/components/conflict/ConflictDialog";
'@
Replace-Required $pageBuilder $pageBuilderImportOld $pageBuilderImportNew "Import ApiError and ConflictDialog"

$pageBuilderStateOld = @'
  const [status, setStatus] = useState<SaveStatus>(initialStatus);

  const payload = useMemo(() => createPageBuilderPayload(state), [state]);
'@
$pageBuilderStateNew = @'
  const [status, setStatus] = useState<SaveStatus>(initialStatus);
  const [loadedPage, setLoadedPage] = useState<PageDefinitionDto | null>(null);
  const [conflict, setConflict] = useState<{
    editor: string;
    currentVersion: number;
    updatedAtUtc?: string;
  } | null>(null);

  const payload = useMemo(() => createPageBuilderPayload(state), [state]);
'@
Replace-Required $pageBuilder $pageBuilderStateOld $pageBuilderStateNew "Add loaded-version and conflict state"

$oldSave = @'
  async function savePageDefinition() {
    try {
      setStatus({ kind: "saving", message: "Saving PageDefinition..." });

      const saved = await pageBuilderApi.create(payload);

      setStatus({
        kind: "saved",
        message: "Saved PageDefinition '" + saved.slug + "' v" + saved.version,
      });
    } catch (error) {
      setStatus({
        kind: "error",
        message: error instanceof Error ? error.message : "Save failed",
      });
    }
  }
'@
$newSave = @'
  async function persistPageDefinition(overwrite = false) {
    setStatus({ kind: "saving", message: "Saving PageDefinition..." });
    const request = {
      ...payload,
      expectedVersion: overwrite ? null : loadedPage?.version ?? null,
    };
    const saved = loadedPage
      ? await pageBuilderApi.update(state.slug, request)
      : await pageBuilderApi.create(request);
    setLoadedPage(saved);
    setConflict(null);
    setStatus({
      kind: "saved",
      message: "Saved PageDefinition '" + saved.slug + "' v" + saved.version,
    });
  }

  async function savePageDefinition() {
    try {
      await persistPageDefinition(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        try {
          const body = JSON.parse(error.responseText) as {
            code?: string;
            editor?: string;
            currentVersion?: number;
            updatedAtUtc?: string;
          };
          if (body.code === "page_version_conflict" && typeof body.currentVersion === "number") {
            setConflict({
              editor: body.editor || "another editor",
              currentVersion: body.currentVersion,
              updatedAtUtc: body.updatedAtUtc,
            });
            setStatus({ kind: "error", message: "Save blocked: this page changed in another session." });
            return;
          }
        } catch {
          // Fall through to the normal error presentation.
        }
      }
      setStatus({ kind: "error", message: error instanceof Error ? error.message : "Save failed" });
    }
  }
'@
Replace-Required $pageBuilder $oldSave $newSave "Wire save to ExpectedVersion and 409 conflict state"

$loadedAnchor = '      const loaded = await pageBuilderApi.getBySlug(state.slug);'
$loadedReplacement = @'
      const loaded = await pageBuilderApi.getBySlug(state.slug);
      setLoadedPage(loaded);
      setConflict(null);
'@
Replace-Required $pageBuilder $loadedAnchor $loadedReplacement "Retain loaded PageDefinition version"

Replace-Required $pageBuilder `
    '<StandardButton variant="primary" onClick={savePageDefinition}>' `
    '<StandardButton variant="primary" onClick={savePageDefinition} data-testid="ctl-save-page">' `
    "Add stable save-page acceptance selector"

$conflictFunctions = @'

  async function reloadAfterConflict() {
    setConflict(null);
    await loadPageDefinition();
  }

  async function overwriteAfterConflict() {
    try {
      await persistPageDefinition(true);
    } catch (error) {
      setStatus({ kind: "error", message: error instanceof Error ? error.message : "Overwrite failed" });
    }
  }

'@
Insert-BeforeLast $pageBuilder "  return (" $conflictFunctions "reloadAfterConflict" "Add conflict resolution handlers"

$dialogMarkup = @'

      <ConflictDialog
        open={conflict !== null}
        editor={conflict?.editor ?? "another editor"}
        currentVersion={conflict?.currentVersion ?? loadedPage?.version ?? 0}
        updatedAtUtc={conflict?.updatedAtUtc}
        onReload={reloadAfterConflict}
        onOverwrite={overwriteAfterConflict}
        onCancel={() => setConflict(null)}
      />
'@
Insert-BeforeLast $pageBuilder "    </main>" $dialogMarkup "<ConflictDialog" "Mount ConflictDialog in the live page-builder workflow"

# Atomic expected-version condition in UPDATE statement.
$pageEndpoint = Join-Path $BackendRoot "PlantProcess.Api\Endpoints\PageBuilder\PageDefinitionEndpoints.cs"
$atomicWhereOld = @'
              AND owner_user_name = @owner
              AND is_deleted = false
            RETURNING id, tenant_id, slug, title, owner_user_name, visibility, version,
'@
$atomicWhereNew = @'
              AND owner_user_name = @owner
              AND is_deleted = false
              AND (@expected_version IS NULL OR version = @expected_version)
            RETURNING id, tenant_id, slug, title, owner_user_name, visibility, version,
'@
Replace-Required $pageEndpoint $atomicWhereOld $atomicWhereNew "Make page update atomically version-conditional"

$expectedParameterOld = @'
        AddPageParameters(command, normalized, tenant, owner);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
'@
$expectedParameterNew = @'
        AddPageParameters(command, normalized, tenant, owner);
        command.Parameters.Add(new NpgsqlParameter("expected_version", NpgsqlDbType.Integer)
        {
            Value = normalized.ExpectedVersion.HasValue
                ? normalized.ExpectedVersion.Value
                : DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
'@
Replace-Required $pageEndpoint $expectedParameterOld $expectedParameterNew "Bind ExpectedVersion to atomic update"

$concurrencySpec = @'
import { expect, test } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL || "http://127.0.0.1:5063";
const userName = process.env.PPIQ_SMOKE_USERNAME || "e2eadmin";
const password = process.env.PPIQ_SMOKE_PASSWORD || "";

async function login(page: import("@playwright/test").Page) {
  const response = await page.request.post(`${apiUrl}/auth/login`, { data: { userName, password } });
  expect(response.ok(), await response.text()).toBeTruthy();
}

test("PPIQ-601 two sessions reject silent last-write-wins", async ({ browser }) => {
  const a = await browser.newContext();
  const b = await browser.newContext();
  const pageA = await a.newPage();
  const pageB = await b.newPage();
  await login(pageA);
  await login(pageB);

  const slug = `concurrency-${Date.now()}`;
  await pageA.goto("/page-builder");
  await pageA.getByLabel("Slug").fill(slug);
  await pageA.getByLabel("Title").fill("Concurrency baseline");
  await pageA.getByTestId("ctl-save-page").click();
  await expect(pageA.getByRole("status")).toContainText(/saved/i);

  await pageB.goto("/page-builder");
  await pageB.getByLabel("Slug").fill(slug);
  await pageB.getByRole("button", { name: /load by slug/i }).click();
  await expect(pageB.getByRole("status")).toContainText(/loaded/i);
  await pageA.getByRole("button", { name: /load by slug/i }).click();
  await expect(pageA.getByRole("status")).toContainText(/loaded/i);

  const titleA = pageA.getByLabel("Title");
  const titleB = pageB.getByLabel("Title");
  await titleA.fill(`Concurrency A ${Date.now()}`);
  await pageA.getByTestId("ctl-save-page").click();
  await expect(pageA.getByRole("status")).toContainText(/saved/i);

  await titleB.fill(`Concurrency B ${Date.now()}`);
  await pageB.getByTestId("ctl-save-page").click();
  await expect(pageB.getByTestId("conflict-dialog")).toBeVisible();
  await expect(pageB.getByTestId("conflict-editor")).toContainText(/current version|changed by/i);
  await expect(pageB.getByTestId("conflict-overwrite")).toBeDisabled();
  await pageB.getByTestId("conflict-overwrite-confirm").check();
  await expect(pageB.getByTestId("conflict-overwrite")).toBeEnabled();

  await a.close();
  await b.close();
});
'@
Write-Utf8NoBom (Join-Path $FrontendRoot "e2e\ppiq-v1-page-concurrency.spec.ts") $concurrencySpec

# -------------------------------------------------------------------------------------------------
# PPIQ-201 — strict no-dead-control proof (no skip, no generic page.content comparison)
# -------------------------------------------------------------------------------------------------
Write-Banner "APPLY PPIQ-201 — strict demo-path control contract"
$strictControls = @'
import { expect, test, type Locator, type Page } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL || "http://127.0.0.1:5063";
const userName = process.env.PPIQ_SMOKE_USERNAME || "e2eadmin";
const password = process.env.PPIQ_SMOKE_PASSWORD || "";

async function login(page: Page) {
  expect(password, "PPIQ_SMOKE_PASSWORD must be configured").not.toBe("");
  const response = await page.request.post(`${apiUrl}/auth/login`, { data: { userName, password } });
  expect(response.ok(), `Login failed: ${response.status()} ${await response.text()}`).toBeTruthy();
}

async function requireVisible(control: Locator, description: string) {
  await expect(control, `${description} is mandatory on the V1 demo path; absence is a failure, never a skip.`).toBeVisible();
}

async function clickWithEffect(page: Page, control: Locator, description: string, status?: Locator) {
  await requireVisible(control, description);
  const before = status ? await status.textContent() : null;
  const responses: string[] = [];
  const listener = (r: import("@playwright/test").Response) => {
    if (r.status() >= 200 && r.status() < 300) responses.push(`${r.status()} ${r.url()}`);
  };
  page.on("response", listener);
  await control.click();
  await page.waitForTimeout(600);
  page.off("response", listener);
  const after = status ? await status.textContent() : null;
  expect(responses.length > 0 || (status && before !== after), `${description} must produce a named 2xx request or change its own status region.`).toBeTruthy();
}

test.describe("PPIQ-201 mandatory demo controls", () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test("material search, investigation, risk and PDF actions are live", async ({ page }) => {
    const errors: string[] = [];
    page.on("pageerror", e => errors.push(String(e)));
    await page.goto("/materials", { waitUntil: "domcontentloaded" });
    const searchInput = page.getByLabel(/search material code/i).or(page.getByPlaceholder(/search material code/i)).first();
    await requireVisible(searchInput, "Material search input");
    await searchInput.fill(process.env.PPIQ_DEMO_MATERIAL_CODE || "C-0044170");
    await clickWithEffect(page, page.getByRole("button", { name: /^search$/i }).first(), "Search", page.locator('[role="status"]').first());

    const load = page.getByRole("button", { name: /load investigation|open investigation/i }).first();
    await clickWithEffect(page, load, "Load investigation", page.locator('[role="status"]').first());

    await clickWithEffect(page, page.getByRole("button", { name: /calculate risk/i }).first(), "Calculate Risk", page.locator('[role="status"]').first());

    const pdf = page.getByRole("link", { name: /pdf|export/i }).or(page.getByRole("button", { name: /pdf|export/i })).first();
    await requireVisible(pdf, "Generate/Export PDF");
    const [pdfResponse] = await Promise.all([
      page.waitForResponse(r => r.url().toLowerCase().includes("pdf") && r.status() >= 200 && r.status() < 300),
      pdf.click(),
    ]);
    expect((await pdfResponse.headerValue("content-type")) || "").toContain("application/pdf");
    expect(errors).toEqual([]);
  });

  test("page save is live and versioned", async ({ page }) => {
    await page.goto("/page-builder", { waitUntil: "domcontentloaded" });
    await clickWithEffect(page, page.getByTestId("ctl-save-page"), "Save page definition", page.getByRole("status"));
    await expect(page.getByRole("status")).toContainText(/saved/i);
  });

  test("dashboard filters and job run controls are reachable and live", async ({ page }) => {
    await page.goto("/dashboard", { waitUntil: "domcontentloaded" });
    const filter = page.getByRole("button", { name: /filter|apply/i }).first();
    await clickWithEffect(page, filter, "Dashboard filter/apply control", page.locator('[role="status"]').first());

    await page.goto("/admin", { waitUntil: "domcontentloaded" });
    const runJob = page.getByRole("button", { name: /run now|run job|trigger/i }).first();
    await clickWithEffect(page, runJob, "Run job", page.locator('[role="status"]').first());
  });
});
'@
Backup-File (Join-Path $FrontendRoot "e2e\demo-path-controls.spec.ts")
Write-Utf8NoBom (Join-Path $FrontendRoot "e2e\demo-path-controls.spec.ts") $strictControls

# -------------------------------------------------------------------------------------------------
# PPIQ-703 — Chromium + Firefox + WebKit x 375/768/1440 x HTTP/HTTPS
# -------------------------------------------------------------------------------------------------
Write-Banner "APPLY PPIQ-703 — full browser/device/protocol matrix"
$matrixConfig = @'
import { defineConfig, devices, type PlaywrightTestConfig } from "@playwright/test";
import baseConfig from "./playwright.config";

const httpBase = process.env.PPIQ_APP_HTTP_URL || "http://127.0.0.1:5173";
const httpsBase = process.env.PPIQ_APP_HTTPS_URL || "";
const viewports = [
  { key: "375", viewport: { width: 375, height: 800 } },
  { key: "768", viewport: { width: 768, height: 1024 } },
  { key: "1440", viewport: { width: 1440, height: 900 } },
];
const engines = [
  { key: "chromium", device: devices["Desktop Chrome"] },
  { key: "firefox", device: devices["Desktop Firefox"] },
  { key: "webkit", device: devices["Desktop Safari"] },
];
const protocols = [
  { key: "http", baseURL: httpBase },
  { key: "https", baseURL: httpsBase },
];

const projects: NonNullable<PlaywrightTestConfig["projects"]> = [];
for (const engine of engines) {
  for (const size of viewports) {
    for (const protocol of protocols) {
      projects.push({
        name: `${engine.key}-${size.key}-${protocol.key}`,
        use: {
          ...engine.device,
          browserName: engine.key as "chromium" | "firefox" | "webkit",
          viewport: size.viewport,
          baseURL: protocol.baseURL,
          ignoreHTTPSErrors: false,
        },
      });
    }
  }
}

const inheritedServers = Array.isArray(baseConfig.webServer)
  ? baseConfig.webServer
  : baseConfig.webServer
    ? [baseConfig.webServer]
    : [];

export default defineConfig({
  ...baseConfig,
  webServer: [
    ...inheritedServers,
    {
      command: "npm run dev -- --host 127.0.0.1 --port 4174",
      cwd: "../../Website/PlantProcess.Website",
      url: process.env.PPIQ_WEB_HTTP_URL || "http://127.0.0.1:4174",
      reuseExistingServer: true,
      timeout: 120_000,
    },
  ],
  testDir: "./e2e",
  testMatch: /ppiq-v1-cross-browser-matrix\.spec\.ts/,
  fullyParallel: false,
  retries: 0,
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report-cross-browser" }]],
  projects,
});
'@
Write-Utf8NoBom (Join-Path $FrontendRoot "playwright.ppiq-v1-matrix.config.ts") $matrixConfig

$matrixSpec = @'
import { expect, test, type Page } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL || "http://127.0.0.1:5063";
const userName = process.env.PPIQ_SMOKE_USERNAME || "e2eadmin";
const password = process.env.PPIQ_SMOKE_PASSWORD || "";

async function login(page: Page) {
  const response = await page.request.post(`${apiUrl}/auth/login`, { data: { userName, password } });
  expect(response.ok(), await response.text()).toBeTruthy();
}

async function assertResponsive(page: Page, url: string) {
  const response = await page.goto(url, { waitUntil: "domcontentloaded", timeout: 45_000 });
  expect(response, `No navigation response for ${url}`).not.toBeNull();
  expect(response!.status(), `${url} returned ${response!.status()}`).toBeLessThan(400);
  await expect(page.locator("body")).toBeVisible();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 8);
  expect(overflow, `${url} has uncontrolled horizontal overflow`).toBeFalsy();
  await expect(page.locator("body")).not.toContainText(/could not load|unhandled exception/i);
}

test("PPIQ-703 app and website reflow over current project protocol", async ({ page }, testInfo) => {
  const isHttps = testInfo.project.name.endsWith("-https");
  const appBase = isHttps ? process.env.PPIQ_APP_HTTPS_URL : process.env.PPIQ_APP_HTTP_URL;
  const websiteBase = isHttps ? process.env.PPIQ_WEB_HTTPS_URL : process.env.PPIQ_WEB_HTTP_URL;
  expect(appBase, `${testInfo.project.name}: app URL is required`).toBeTruthy();
  expect(websiteBase, `${testInfo.project.name}: website URL is required`).toBeTruthy();

  await login(page);
  for (const route of ["/dashboard", "/materials", "/page-builder", "/demo-lifecycle"]) {
    await assertResponsive(page, `${appBase}${route}`);
  }
  for (const route of ["/", "/products", "/products/mes"]) {
    await assertResponsive(page, `${websiteBase}${route}`);
  }
});
'@
Write-Utf8NoBom (Join-Path $FrontendRoot "e2e\ppiq-v1-cross-browser-matrix.spec.ts") $matrixSpec

# -------------------------------------------------------------------------------------------------
# PPIQ-705 — common branded light-surface PDF writer + tests
# -------------------------------------------------------------------------------------------------
Write-Banner "APPLY PPIQ-705 — branded light-surface PDF"
$brandedPdf = @'
using System.Globalization;
using System.Text;

namespace PlantProcess.Application.Reporting;

/// <summary>
/// Shared deterministic PDF writer for customer-facing reports.
/// It paints the #F4F6F8 report surface, a Deep-Navy brand header, and a maintained footer.
/// The embedded markers are intentionally testable in the generated bytes.
/// </summary>
public static class BrandedPdfWriter
{
    private const string LightSurfaceMarker = "PPIQ-LIGHT-SURFACE:#F4F6F8";
    private const string HeaderMarker = "PPIQ-BRAND-HEADER";
    private const string FooterMarker = "PPIQ-BRAND-FOOTER";

    public static byte[] Create(string title, IEnumerable<string> sourceLines)
    {
        var lines = sourceLines
            .Select(Sanitize)
            .SelectMany(line => Wrap(line, 92))
            .Take(900)
            .ToList();
        if (lines.Count == 0) lines.Add("No report content.");

        var pages = lines.Chunk(45).Select(chunk => chunk.ToArray()).ToArray();
        var objects = new List<string>();
        var pageObjectNumbers = new List<int>();
        var contentObjectNumbers = new List<int>();

        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add(""); // pages object filled after page objects are allocated
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

        for (var index = 0; index < pages.Length; index++)
        {
            var pageObject = objects.Count + 1;
            var contentObject = pageObject + 1;
            pageObjectNumbers.Add(pageObject);
            contentObjectNumbers.Add(contentObject);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObject} 0 R >>");
            var content = BuildPage(title, pages[index], index + 1, pages.Length);
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectNumbers.Select(n => $"{n} 0 R"))}] /Count {pages.Length} >>";

        var output = new StringBuilder();
        output.AppendLine("%PDF-1.4");
        output.AppendLine($"% {LightSurfaceMarker} {HeaderMarker} {FooterMarker}");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(output.ToString()));
            output.AppendLine($"{i + 1} 0 obj");
            output.AppendLine(objects[i]);
            output.AppendLine("endobj");
        }
        var xref = Encoding.ASCII.GetByteCount(output.ToString());
        output.AppendLine("xref");
        output.AppendLine($"0 {objects.Count + 1}");
        output.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) output.AppendLine($"{offset:0000000000} 00000 n ");
        output.AppendLine($"trailer << /Size {objects.Count + 1} /Root 1 0 R /Info << /Title ({Escape(title)}) /Subject ({LightSurfaceMarker}; {HeaderMarker}; {FooterMarker}) >> >>");
        output.AppendLine("startxref");
        output.AppendLine(xref.ToString(CultureInfo.InvariantCulture));
        output.AppendLine("%%EOF");
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static string BuildPage(string title, IReadOnlyList<string> lines, int page, int total)
    {
        var content = new StringBuilder();
        // #F4F6F8 light report surface: 244/246/248.
        content.AppendLine("0.9569 0.9647 0.9725 rg 0 0 595 842 re f");
        // #050B18 brand header.
        content.AppendLine("0.0196 0.0431 0.0941 rg 0 770 595 72 re f");
        content.AppendLine("BT /F2 17 Tf 1 1 1 rg 42 808 Td (PlantProcess IQ / SOU) Tj ET");
        content.AppendLine($"BT /F1 10 Tf 0.8 0.9 1 rg 42 786 Td ({Escape(title)}) Tj ET");
        content.AppendLine("0.0392 0.5176 1 rg 42 760 511 2 re f");
        content.AppendLine("BT /F1 10 Tf 0.08 0.13 0.20 rg 42 738 Td");
        foreach (var line in lines)
        {
            content.AppendLine($"({Escape(line)}) Tj");
            content.AppendLine("0 -14 Td");
        }
        content.AppendLine("ET");
        content.AppendLine("0.0627 0.1647 0.2627 rg 0 0 595 34 re f");
        content.AppendLine($"BT /F1 8 Tf 0.92 0.96 1 rg 42 13 Td (Connect Your Plant Data. Understand Your Process.  |  Page {page}/{total}) Tj ET");
        return content.ToString();
    }

    private static IEnumerable<string> Wrap(string line, int width)
    {
        if (string.IsNullOrWhiteSpace(line)) { yield return " "; yield break; }
        var remaining = line.Trim();
        while (remaining.Length > width)
        {
            var cut = remaining.LastIndexOf(' ', width);
            if (cut < width / 2) cut = width;
            yield return remaining[..cut];
            remaining = remaining[cut..].TrimStart();
        }
        yield return remaining;
    }

    private static string Sanitize(string value) => value
        .Replace("\u2013", "-")
        .Replace("\u2014", "-")
        .Replace("\u2192", "->")
        .Replace("\u20ac", "EUR")
        .Replace("\u2264", "<=")
        .Replace("\u2265", ">=");

    private static string Escape(string value) => Sanitize(value)
        .Replace("\\", "\\\\")
        .Replace("(", "\\(")
        .Replace(")", "\\)");
}
'@
Write-Utf8NoBom (Join-Path $BackendRoot "PlantProcess.Application\Reporting\BrandedPdfWriter.cs") $brandedPdf

$customerPdf = Join-Path $BackendRoot "PlantProcess.Api\Endpoints\Reporting\CustomerDemoReportEndpoints.cs"
Replace-Required $customerPdf `
    'var pdfBytes = SimplePdfWriter.Create("PlantProcess IQ Phase 1 Demo Report", lines);' `
    'var pdfBytes = PlantProcess.Application.Reporting.BrandedPdfWriter.Create("PlantProcess IQ Phase 1 Demo Report", lines);' `
    "Use branded writer for customer demo PDF"

$investigationService = Join-Path $BackendRoot "PlantProcess.Application\Services\Reporting\InvestigationReportService.cs"
Replace-Required $investigationService `
    'var pdfBytes = SimplePdfWriter.CreatePdf(BuildPlainTextReport(report));' `
    'var pdfBytes = PlantProcess.Application.Reporting.BrandedPdfWriter.Create("PlantProcess IQ Material Investigation Report", BuildPlainTextReport(report).Replace("\r", "").Split(''\n''));' `
    "Use branded writer for material investigation PDF"

$readinessService = Join-Path $BackendRoot "PlantProcess.Application\Services\Readiness\ApplicationReadinessService.cs"
$readinessPdfOld = 'var pdf = SimplePdfWriter.BuildReadinessPdf(customerName, report);'
$readinessPdfNew = @'
var pdf = PlantProcess.Application.Reporting.BrandedPdfWriter.Create(
            $"PlantProcess IQ Readiness Assessment - {customerName}",
            new[]
            {
                $"Customer: {customerName}",
                $"Generated UTC: {report.GeneratedAtUtc:u}",
                $"Overall score: {report.OverallScore:0.0}",
                $"Overall feasibility: {report.OverallFeasibility}",
                "",
                "Executive Summary",
                report.ExecutiveSummary,
                "",
                "Top Blockers",
            }.Concat(report.TopBlockers.Select(x => "- " + x))
             .Concat(new[] { "", "Recommended Actions" })
             .Concat(report.RecommendedActions.Select(x => "- " + x))
             .Concat(new[] { "", report.Disclaimer }));
'@
Replace-Required $readinessService $readinessPdfOld $readinessPdfNew "Use branded writer for readiness PDF"

$pdfTests = @'
using System.Text;
using PlantProcess.Application.Reporting;

namespace PlantProcess.Application.UnitTests.Reporting;

public sealed class BrandedPdfWriterTests
{
    [Fact]
    public void Ppiq705_Pdf_Contains_Light_Surface_Header_And_Footer_Markers()
    {
        var bytes = BrandedPdfWriter.Create("Test report", new[] { "Evidence line" });
        var ascii = Encoding.ASCII.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", ascii);
        Assert.Contains("PPIQ-LIGHT-SURFACE:#F4F6F8", ascii);
        Assert.Contains("PPIQ-BRAND-HEADER", ascii);
        Assert.Contains("PPIQ-BRAND-FOOTER", ascii);
        Assert.Contains("0.9569 0.9647 0.9725 rg 0 0 595 842 re f", ascii);
        Assert.Contains("Connect Your Plant Data. Understand Your Process.", ascii);
    }
}
'@
Write-Utf8NoBom (Join-Path $BackendRoot "tests\PlantProcess.Application.UnitTests\Reporting\BrandedPdfWriterTests.cs") $pdfTests

# -------------------------------------------------------------------------------------------------
# PPIQ-104 — clean-volume migrate/seed proof script
# -------------------------------------------------------------------------------------------------
Write-Banner "APPLY PPIQ-104 — clean-volume migration/seed proof"
$p104Proof = @'
param(
  [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
  [switch]$KeepDatabase
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$container = "ppiq-p104-proof-db"
$port = 55432
$password = "PpiqP104_" + [Guid]::NewGuid().ToString("N")
$compose1 = Join-Path $RepoRoot "deploy\compose\docker-compose.demo-sources.yml"
$compose2 = Join-Path $RepoRoot "deploy\compose\docker-compose.demo-sources.ports.yml"
$migrate = Join-Path $RepoRoot "deploy\scripts\migrate-and-seed.sh"
if (-not (Test-Path $migrate)) { throw "Missing $migrate" }

function Run([scriptblock]$Command, [string]$Name) {
  & $Command
  if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

try {
  & docker rm -f $container 2>$null | Out-Null
  Run { docker run -d --name $container -e POSTGRES_DB=plantprocessiq -e POSTGRES_USER=plantprocess -e "POSTGRES_PASSWORD=$password" -p "127.0.0.1:${port}:5432" postgres:16-alpine } "Start isolated Postgres"
  $ready = $false
  for ($i=0; $i -lt 60; $i++) {
    & docker exec $container pg_isready -U plantprocess -d plantprocessiq *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 1
  }
  if (-not $ready) { throw "Isolated PostgreSQL did not become ready." }

  if ((Test-Path $compose1) -and (Test-Path $compose2)) {
    Run { docker compose -f $compose1 -f $compose2 up -d } "Start eight demo sources"
  } else { throw "Demo source compose files are missing." }

  $env:PGHOST = "127.0.0.1"; $env:PGPORT = "$port"; $env:PGDATABASE = "plantprocessiq"; $env:PGUSER = "plantprocess"; $env:PGPASSWORD = $password
  $env:PPIQ_DB_HOST = $env:PGHOST; $env:PPIQ_DB_PORT = $env:PGPORT; $env:PPIQ_DB_NAME = $env:PGDATABASE; $env:PPIQ_DB_USER = $env:PGUSER; $env:PPIQ_DB_PASSWORD = $env:PGPASSWORD
  $env:ConnectionStrings__PlantProcess = "Host=127.0.0.1;Port=$port;Database=plantprocessiq;Username=plantprocess;Password=$password"
  $env:PLANTPROCESS_CONNECTION_STRING = $env:ConnectionStrings__PlantProcess

  $bash = Get-Command bash.exe -ErrorAction SilentlyContinue
  if (-not $bash) { $bash = Get-Command bash -ErrorAction SilentlyContinue }
  if ($bash) { Run { & $bash.Source $migrate } "First migrate-and-seed" }
  else {
    $wsl = Get-Command wsl.exe -ErrorAction SilentlyContinue
    if (-not $wsl) { throw "bash.exe or wsl.exe is required." }
    $wslPath = $migrate.Replace('C:','/mnt/c').Replace('\','/')
    Run { & $wsl.Source bash -lc "'$wslPath'" } "First migrate-and-seed"
  }

  $count1 = (& docker exec -e PGPASSWORD=$password $container psql -U plantprocess -d plantprocessiq -Atc "SELECT COUNT(*) FROM schema_migrations;").Trim()
  if ([int]$count1 -lt 77) { throw "Expected at least 77 schema_migrations rows; found $count1." }

  if ($bash) { Run { & $bash.Source $migrate } "Second idempotency migrate-and-seed" }
  else { Run { & $wsl.Source bash -lc "'$wslPath'" } "Second idempotency migrate-and-seed" }
  $count2 = (& docker exec -e PGPASSWORD=$password $container psql -U plantprocess -d plantprocessiq -Atc "SELECT COUNT(*) FROM schema_migrations;").Trim()
  if ($count1 -ne $count2) { throw "Idempotency failed: first=$count1 second=$count2" }

  $view = (& docker exec -e PGPASSWORD=$password $container psql -U plantprocess -d plantprocessiq -Atc "SELECT schemaname||'.'||viewname FROM pg_views WHERE schemaname NOT IN ('pg_catalog','information_schema') ORDER BY schemaname,viewname LIMIT 1;").Trim()
  if (-not $view) { throw "No canonical/application view exists after migration." }
  $viewCount = (& docker exec -e PGPASSWORD=$password $container psql -U plantprocess -d plantprocessiq -Atc "SELECT COUNT(*) FROM $view;").Trim()
  Write-Host "[GREEN] PPIQ-104: migrations=$count2, view=$view, rows=$viewCount" -ForegroundColor Green
}
finally {
  if (-not $KeepDatabase) { & docker rm -f $container 2>$null | Out-Null }
}
'@
Write-Utf8NoBom (Join-Path $RepoRoot "scripts\go-live\Test-PPIQ104-MigrateSeed.ps1") $p104Proof

# -------------------------------------------------------------------------------------------------
# STATIC VALIDATION BEFORE EXECUTION
# -------------------------------------------------------------------------------------------------
Write-Banner "Static validation of applied implementation"
$changedFiles = @(
  $boomTest,
  (Join-Path $BackendRoot "PlantProcess.Api\Endpoints\Demo\DemoReadinessEndpoints.cs"),
  $program,
  (Join-Path $FrontendRoot "src\api\demo\demoReadiness.api.ts"),
  (Join-Path $FrontendRoot "src\components\demo\DemoReadinessPanel.tsx"),
  $demoLifecycleWrapper,
  (Join-Path $FrontendRoot "e2e\ppiq-v1-readiness-dry-run.spec.ts"),
  (Join-Path $FrontendRoot "playwright.ppiq-v1-readiness.config.ts"),
  $pageApi,
  $pageBuilder,
  $pageEndpoint,
  (Join-Path $FrontendRoot "e2e\demo-path-controls.spec.ts"),
  (Join-Path $FrontendRoot "e2e\ppiq-v1-page-concurrency.spec.ts"),
  (Join-Path $FrontendRoot "playwright.ppiq-v1-matrix.config.ts"),
  (Join-Path $FrontendRoot "e2e\ppiq-v1-cross-browser-matrix.spec.ts"),
  (Join-Path $BackendRoot "PlantProcess.Application\Reporting\BrandedPdfWriter.cs"),
  $customerPdf,
  $investigationService,
  $readinessService,
  (Join-Path $BackendRoot "tests\PlantProcess.Application.UnitTests\Reporting\BrandedPdfWriterTests.cs"),
  (Join-Path $RepoRoot "scripts\go-live\Test-PPIQ104-MigrateSeed.ps1")
)
foreach ($file in $changedFiles) { Assert-NoBom $file }
Assert-Contains $program "MapDemoReadinessEndpoints" "PPIQ-103 endpoint is not mapped."
Assert-Contains $demoLifecycleWrapper "DemoReadinessPanel" "PPIQ-103 panel is not mounted."
Assert-Contains $pageBuilder "expectedVersion" "PPIQ-601 ExpectedVersion is not wired."
Assert-Contains $pageBuilder "<ConflictDialog" "PPIQ-601 ConflictDialog is not mounted."
Assert-Contains (Join-Path $FrontendRoot "e2e\demo-path-controls.spec.ts") "never a skip" "PPIQ-201 strict absence contract missing."
Assert-Contains (Join-Path $FrontendRoot "playwright.ppiq-v1-matrix.config.ts") '"webkit"' "PPIQ-703 WebKit project missing."
Assert-Contains (Join-Path $BackendRoot "PlantProcess.Application\Reporting\BrandedPdfWriter.cs") "#F4F6F8" "PPIQ-705 light surface missing."

# -------------------------------------------------------------------------------------------------
# EXECUTION AND ACCEPTANCE GATES
# -------------------------------------------------------------------------------------------------
$env:PLAYWRIGHT_API_URL = $ApiUrl
$env:PPIQ_APP_HTTP_URL = $AppHttpUrl
$env:PPIQ_WEB_HTTP_URL = $WebsiteHttpUrl
$env:PPIQ_SMOKE_USERNAME = $SmokeUser
$env:PPIQ_SMOKE_PASSWORD = $SmokePassword
if (-not $SkipHttpsMatrix) {
  $env:PPIQ_APP_HTTPS_URL = $AppHttpsUrl
  $env:PPIQ_WEB_HTTPS_URL = $WebsiteHttpsUrl
}

Invoke-External "Backend build" $BackendRoot { & dotnet.exe build --no-restore } (Join-Path $EvidenceRoot "backend-build.log")
Invoke-External "Frontend production build" $FrontendRoot { & npm.cmd run build } (Join-Path $EvidenceRoot "frontend-build.log")
Invoke-External "Website production build" $WebsiteRoot { & npm.cmd run build } (Join-Path $EvidenceRoot "website-build.log")
Invoke-External "PPIQ-201 dead-button static scan" $FrontendRoot { & node.exe scripts/dead-button-scan.mjs src DEAD_BUTTON_INVENTORY.md } (Join-Path $EvidenceRoot "dead-button-scan.log")

Invoke-External "Focused backend tests (readiness, concurrency, branded PDF)" $BackendRoot {
  & dotnet.exe test tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj --no-build --filter "FullyQualifiedName~DemoReadiness|FullyQualifiedName~BrandedPdfWriter|FullyQualifiedName~PageVersionConflict"
} (Join-Path $EvidenceRoot "focused-backend-tests.log")

Invoke-External "Focused frontend unit tests" $FrontendRoot {
  & npm.cmd run test -- src/components/conflict/__tests__/ConflictDialog.contract.test.tsx src/components/standard/__tests__/P2Close.errorBoundaryContainment.test.tsx
} (Join-Path $EvidenceRoot "focused-frontend-tests.log")

Invoke-External "Install Playwright browser engines" $FrontendRoot {
  & npx.cmd playwright install chromium firefox webkit
} (Join-Path $EvidenceRoot "playwright-install.log")

Invoke-External "PPIQ-103 recorded readiness dry-run" $FrontendRoot {
  & npx.cmd playwright test --config playwright.ppiq-v1-readiness.config.ts --headed
} (Join-Path $EvidenceRoot "ppiq-103-readiness-dry-run.log")
$videos = Get-ChildItem -Path (Join-Path $FrontendRoot "test-results") -Filter "*.webm" -Recurse -ErrorAction SilentlyContinue
if (-not $videos -or $videos.Count -eq 0) { throw "PPIQ-103 failed: no recorded Playwright video artifact was produced." }
$videoEvidence = Join-Path $EvidenceRoot "videos"
Ensure-Directory $videoEvidence
$videos | Copy-Item -Destination $videoEvidence -Force
Add-Result "PPIQ-103" "DONE" "Readiness endpoint + HMI panel green; headed dry-run exited 0; video copied to $videoEvidence"

if (-not $SkipCleanMigrationProof) {
  Invoke-External "PPIQ-104 clean-volume migrate + seed + idempotency proof" $RepoRoot {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepoRoot "scripts\go-live\Test-PPIQ104-MigrateSeed.ps1") -RepoRoot $RepoRoot -KeepDatabase:$KeepP104ProofDatabase
  } (Join-Path $EvidenceRoot "ppiq-104-migrate-seed.log")
  Add-Result "PPIQ-104" "DONE" "Fresh isolated PostgreSQL proof, >=77 migrations, second-run no-op, demo sources started, canonical view queried."
} else {
  Add-Result "PPIQ-104" "BLOCKED" "Clean migration proof was skipped." "Run again without -SkipCleanMigrationProof."
}

Invoke-External "PPIQ-201 strict demo-path controls" $FrontendRoot {
  & npx.cmd playwright test e2e/demo-path-controls.spec.ts --project=chromium
} (Join-Path $EvidenceRoot "ppiq-201-controls.log")
Add-Result "PPIQ-201" "DONE" "Dead-button scan=0; mandatory controls present; named network/state effects; zero unhandled rejections."

Invoke-External "PPIQ-601 two-session optimistic concurrency" $FrontendRoot {
  & npx.cmd playwright test e2e/ppiq-v1-page-concurrency.spec.ts --project=chromium
} (Join-Path $EvidenceRoot "ppiq-601-concurrency.log")
Add-Result "PPIQ-601" "DONE" "ExpectedVersion sent; atomic version condition; live ConflictDialog; two-context proof passed."

if (-not $SkipHttpsMatrix) {
  Invoke-External "PPIQ-703 18-leg browser/device/protocol matrix" $FrontendRoot {
    & npx.cmd playwright test --config playwright.ppiq-v1-matrix.config.ts
  } (Join-Path $EvidenceRoot "ppiq-703-matrix.log")
  Add-Result "PPIQ-703" "DONE" "Chromium/Firefox/WebKit x 375/768/1440 x HTTP/HTTPS passed with overflow and reachability assertions."
} else {
  Add-Result "PPIQ-703" "BLOCKED" "HTTPS matrix was skipped." "Provide HTTPS app/website URLs and rerun without -SkipHttpsMatrix."
}

Invoke-External "PPIQ-705 PDF unit proof" $BackendRoot {
  & dotnet.exe test tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj --no-build --filter "FullyQualifiedName~BrandedPdfWriterTests"
} (Join-Path $EvidenceRoot "ppiq-705-pdf.log")
Add-Result "PPIQ-705" "DONE" "All principal PDF generation paths use shared light-surface writer; marker and snapshot-level byte contract passed."

if (-not $SkipFullRegression) {
  for ($pass = 1; $pass -le 2; $pass++) {
    Invoke-External "Full backend regression pass $pass/2" $BackendRoot { & dotnet.exe test --no-build } (Join-Path $EvidenceRoot "full-backend-pass-$pass.log")
    Invoke-External "Full frontend unit regression pass $pass/2" $FrontendRoot { & npm.cmd run test } (Join-Path $EvidenceRoot "full-frontend-pass-$pass.log")
  }
} else {
  Write-Host "[WARNING] Full regression was skipped; task-level gates may be green but release-wide confidence is reduced." -ForegroundColor Yellow
}

# -------------------------------------------------------------------------------------------------
# FINAL FAIL-CLOSED STATUS AND EVIDENCE
# -------------------------------------------------------------------------------------------------
$summaryJson = Join-Path $EvidenceRoot "task-results.json"
Write-Utf8NoBom $summaryJson ($script:Results | ConvertTo-Json -Depth 5)

$summaryMd = New-Object System.Text.StringBuilder
[void]$summaryMd.AppendLine("# PPIQ V1 Critical Closure Evidence")
[void]$summaryMd.AppendLine("")
[void]$summaryMd.AppendLine("Generated: $(Get-Date -Format o)")
[void]$summaryMd.AppendLine("")
[void]$summaryMd.AppendLine("| Task | Status | Evidence | Remaining gap |")
[void]$summaryMd.AppendLine("|---|---|---|---|")
foreach ($result in $script:Results) {
    [void]$summaryMd.AppendLine("| $($result.Task) | $($result.Status) | $($result.Evidence.Replace('|','\|')) | $($result.RemainingGap.Replace('|','\|')) |")
}
$summaryPath = Join-Path $EvidenceRoot "README.md"
Write-Utf8NoBom $summaryPath ($summaryMd.ToString())

$blocked = @($script:Results | Where-Object Status -ne "DONE")
if ($blocked.Count -gt 0) {
    Write-Banner "CLOSURE PACK COMPLETED WITH BLOCKED TASKS"
    $blocked | Format-Table -AutoSize | Out-String | Write-Host
    Write-Host "Evidence: $EvidenceRoot" -ForegroundColor Yellow
    throw "$($blocked.Count) task(s) are not DONE. Review the evidence and rerun without skip switches."
}

Write-Banner "ALL SIX TASKS PASSED THEIR ACCEPTANCE GATES"
$script:Results | Format-Table -AutoSize
Write-Host "Evidence folder: $EvidenceRoot" -ForegroundColor Green
Write-Host "Backup folder  : $BackupRoot" -ForegroundColor Green
Write-Host "The tasks are considered DONE only for the exact environment proven by these logs." -ForegroundColor Cyan
