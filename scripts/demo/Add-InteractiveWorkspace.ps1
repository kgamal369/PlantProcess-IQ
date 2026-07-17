# ============================================================================
# Add-InteractiveWorkspace.ps1  -  THE RESURRECTION PACK
# Recomposes the May Qlik-style workspace from the organs that survived in
# your tree (nothing is invented):
#     DashboardGridLayout        drag + resize grid (react-grid-layout)
#     SavedDashboardWidget       self-querying widget with chart rendering
#     DashboardWidgetCard        frame with actions (min/max/remove live here)
#     DashboardFilterBar         global interactive filters (context-driven)
#     SelectionBreadcrumb        click-to-filter visual selections
#     useDashboardLayoutPersistence   save/reload layout to the definition
# ...and binds them to the SEVEN dashboards seeded earlier today.
#
# WHAT IT DOES (3 files):
#   1. NEW  src/pages/Dashboard/InteractiveWorkspacePage.tsx  (the composition)
#   2. EDIT src/pages/Dashboard/DashboardPageContent.tsx      (repoint the shim:
#           /dashboard becomes the PRODUCTION_OVERVIEW workspace again)
#   3. EDIT src/App.tsx  (adds route /workspace/:dashboardCode -> ALL seeded
#           dashboards become navigable pages)
#
# YOUR THREE PAGES AFTER THIS:
#   Type 1 (raw plant data)      /dashboard                     (Production Overview)
#   Type 2 (correlation)         /workspace/CORRELATION_FINDINGS_BOARD
#   Type 3 (AI+ML)               /workspace/RISK_INTELLIGENCE
#   (...and /workspace/<any other seeded code> as bonus pages)
#
# HONEST RISK NOTE: tsc proves the contracts compile; two things only the
# browser can prove - (a) the saved layout JSON applying to the grid,
# (b) each widget's query returning chartable rows. Both degrade gracefully
# (default grid positions / per-widget empty state) rather than breaking.
# Contract: unique anchors -> byte backups -> edits -> tsc -b gate -> full
# auto-revert of ALL files on any failure.
# Run from repo root (presentation branch):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Add-InteractiveWorkspace.ps1
# ============================================================================
[CmdletBinding()]
param(
    [switch]$SkipGate
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Get-Location).Path
$Web = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$NewPage = Join-Path $Web 'src\pages\Dashboard\InteractiveWorkspacePage.tsx'
$Shim = Join-Path $Web 'src\pages\Dashboard\DashboardPageContent.tsx'
$AppTsx = Join-Path $Web 'src\App.tsx'
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\interactive-workspace-" + $Stamp)

foreach ($f in @($Shim, $AppTsx)) {
    if (-not (Test-Path $f)) { Write-Host ("[FAIL] missing: " + $f) -ForegroundColor Red; exit 1 }
}
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
Copy-Item $Shim (Join-Path $BackupDir 'DashboardPageContent.tsx') -Force
Copy-Item $AppTsx (Join-Path $BackupDir 'App.tsx') -Force
$newPageExisted = Test-Path $NewPage
if ($newPageExisted) { Copy-Item $NewPage (Join-Path $BackupDir 'InteractiveWorkspacePage.tsx') -Force }

function Restore-All {
    Copy-Item (Join-Path $BackupDir 'DashboardPageContent.tsx') $Shim -Force
    Copy-Item (Join-Path $BackupDir 'App.tsx') $AppTsx -Force
    if ($newPageExisted) { Copy-Item (Join-Path $BackupDir 'InteractiveWorkspacePage.tsx') $NewPage -Force }
    elseif (Test-Path $NewPage) { Remove-Item $NewPage -Force }
    Write-Host ("[REVERT] all files restored. Backup: " + $BackupDir) -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# 1. the composition (new file; single-quoted here-string = no interpolation)
# ---------------------------------------------------------------------------
$pageSource = @'
import { useCallback, useEffect, useState, type ComponentProps } from "react";
import { useParams } from "react-router-dom";
import { DashboardFilterBar } from "@/components/DashboardFilterBar";
import { DashboardGridLayout } from "@/components/dashboard/DashboardGridLayout";
import { SavedDashboardWidget } from "@/components/dashboard/SavedDashboardWidget";
import { SelectionBreadcrumb } from "@/components/dashboard/SelectionBreadcrumb";
import { useDashboardLayoutPersistence } from "@/hooks/useDashboardLayoutPersistence";
import { dashboardingApi } from "@/api/dashboarding/dashboarding.api";
import { StandardButton } from "@/components/standard";

type WidgetRecord = ComponentProps<typeof SavedDashboardWidget>["widget"];

type LoadedDashboard = {
  id: string;
  name: string;
  description: string;
  widgets: WidgetRecord[];
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" ? (value as Record<string, unknown>) : {};
}

function pickArray(source: Record<string, unknown>, keys: string[]): unknown[] {
  for (const key of keys) {
    const v = source[key];
    if (Array.isArray(v)) return v;
  }
  return [];
}

async function loadDashboardByCode(code: string): Promise<LoadedDashboard | null> {
  const listRaw = await dashboardingApi.getDashboardDefinitions();
  const list = Array.isArray(listRaw)
    ? listRaw
    : pickArray(asRecord(listRaw), ["items", "definitions", "rows", "data"]);
  const match = list
    .map(asRecord)
    .find(
      (d) =>
        String(d["dashboardCode"] ?? d["dashboard_code"] ?? "").toUpperCase() ===
        code.toUpperCase()
    );
  if (!match) return null;
  const id = String(match["id"] ?? "");
  if (!id) return null;
  const fullRaw = await dashboardingApi.getDashboardDefinition(id);
  const full = asRecord(fullRaw);
  const container = asRecord(full["definition"]) ?? full;
  const widgetsRaw = [
    ...pickArray(full, ["widgets", "widgetDefinitions", "dashboardWidgetDefinitions"]),
    ...pickArray(container, ["widgets", "widgetDefinitions", "dashboardWidgetDefinitions"]),
  ];
  return {
    id,
    name: String(full["name"] ?? container["name"] ?? match["name"] ?? code),
    description: String(full["description"] ?? container["description"] ?? match["description"] ?? ""),
    widgets: widgetsRaw as WidgetRecord[],
  };
}

export function InteractiveWorkspacePage({ dashboardCode }: { dashboardCode: string }) {
  const [dashboard, setDashboard] = useState<LoadedDashboard | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

  const persistence = useDashboardLayoutPersistence(dashboard?.id);

  const refresh = useCallback(() => {
    setReloadNonce((n) => n + 1);
  }, []);

  useEffect(() => {
    let alive = true;
    setFailed(null);
    loadDashboardByCode(dashboardCode)
      .then((loaded) => {
        if (!alive) return;
        if (!loaded) {
          setFailed("No dashboard found with code " + dashboardCode + ".");
          return;
        }
        setDashboard(loaded);
      })
      .catch(() => {
        if (!alive) return;
        setFailed("The dashboard service is not reachable.");
      });
    return () => {
      alive = false;
    };
  }, [dashboardCode, reloadNonce]);

  useEffect(() => {
    if (dashboard?.id) {
      void persistence.reloadLayout();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dashboard?.id]);

  if (failed) {
    return (
      <section className="ppiq-std-card">
        <header className="ppiq-std-card__header">
          <h2>Interactive workspace</h2>
        </header>
        <p>{failed}</p>
      </section>
    );
  }
  if (!dashboard) {
    return (
      <section className="ppiq-std-card">
        <header className="ppiq-std-card__header">
          <h2>Interactive workspace</h2>
        </header>
        <p>Loading dashboard...</p>
      </section>
    );
  }

  return (
    <section aria-label={dashboard.name}>
      <header className="ppiq-std-card__header">
        <div>
          <h2>{dashboard.name}</h2>
          <p>{dashboard.description}</p>
        </div>
        <div className="ppiq-journey-actions">
          <StandardButton
            variant="ghost"
            onClick={() => void persistence.saveLayout()}
            disabled={persistence.isSavingLayout}
          >
            {persistence.isSavingLayout ? "Saving layout..." : "Save layout"}
          </StandardButton>
          <StandardButton variant="ghost" onClick={refresh}>
            Refresh widgets
          </StandardButton>
        </div>
      </header>
      <DashboardFilterBar />
      <SelectionBreadcrumb />
      <DashboardGridLayout>
        {dashboard.widgets.map((widget) => (
          <div key={String((widget as { id?: unknown }).id ?? Math.random())}>
            <SavedDashboardWidget
              dashboardDefinitionId={dashboard.id}
              widget={widget}
              onEdit={() => undefined}
              onRemoved={refresh}
              onCloned={refresh}
              onHidden={refresh}
            />
          </div>
        ))}
      </DashboardGridLayout>
    </section>
  );
}

export function RoutedInteractiveWorkspacePage() {
  const params = useParams<{ dashboardCode: string }>();
  return (
    <InteractiveWorkspacePage dashboardCode={params.dashboardCode ?? "PRODUCTION_OVERVIEW"} />
  );
}
'@
[System.IO.File]::WriteAllText($NewPage, ($pageSource -replace "`r`n", "`n" -replace "`n", "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "      CREATED src\pages\Dashboard\InteractiveWorkspacePage.tsx"

# ---------------------------------------------------------------------------
# 2. repoint the shim
# ---------------------------------------------------------------------------
$shimSource = @'
import { InteractiveWorkspacePage } from "./InteractiveWorkspacePage";

export function DashboardPageContent() {
  return <InteractiveWorkspacePage dashboardCode="PRODUCTION_OVERVIEW" />;
}
'@
[System.IO.File]::WriteAllText($Shim, ($shimSource -replace "`r`n", "`n" -replace "`n", "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "      REPOINTED DashboardPageContent -> InteractiveWorkspacePage (PRODUCTION_OVERVIEW)"

# ---------------------------------------------------------------------------
# 3. App.tsx: import + /workspace/:dashboardCode route (anchored)
# ---------------------------------------------------------------------------
$app = [System.IO.File]::ReadAllText($AppTsx, [System.Text.Encoding]::UTF8)

$importAnchor = 'import { DashboardGridLayoutProvider } from "./state/DashboardGridLayoutContext";'
$importAdd = $importAnchor + "`r`n" + 'import { RoutedInteractiveWorkspacePage } from "./pages/Dashboard/InteractiveWorkspacePage";'
$routeAnchor = '<Route path="/data-integration" element={<DataIntegrationLayout />}>'
$routeAdd = '<Route path="/workspace/:dashboardCode" element={<RoutedInteractiveWorkspacePage />} />' + "`r`n                    " + $routeAnchor

$ok = $true
foreach ($pair in @(
        @{ Id = 'APP-IMPORT'; A = $importAnchor; R = $importAdd },
        @{ Id = 'APP-ROUTE'; A = $routeAnchor; R = $routeAdd })) {
    $count = 0; $idx = 0
    while (($idx = $app.IndexOf([string]$pair.A, $idx, [System.StringComparison]::Ordinal)) -ge 0) { $count++; $idx += ([string]$pair.A).Length }
    if ($count -ne 1) {
        Write-Host ("[ABORT] " + $pair.Id + " anchor count=" + $count) -ForegroundColor Red
        $ok = $false
        break
    }
    $app = $app.Replace([string]$pair.A, [string]$pair.R)
    Write-Host ("      APPLIED " + $pair.Id)
}
if (-not $ok) { Restore-All; exit 1 }
[System.IO.File]::WriteAllText($AppTsx, $app, (New-Object System.Text.UTF8Encoding($false)))

# ---------------------------------------------------------------------------
# 4. gate
# ---------------------------------------------------------------------------
if ($SkipGate) {
    Write-Host "[GATE SKIPPED] run: npx tsc -b"
} else {
    Write-Host "[GATE] npx tsc -b ..."
    Push-Location $Web
    try { & npx tsc -b 2>&1 | Select-Object -Last 15 | ForEach-Object { Write-Host ("    " + $_) }; $code = $LASTEXITCODE } finally { Pop-Location }
    if ($code -ne 0) {
        Write-Host "[GATE RED] contracts drifted from my extraction - reverting everything." -ForegroundColor Red
        Write-Host "           Paste the tsc errors above and the corrected pack comes back." -ForegroundColor Red
        Restore-All
        exit 1
    }
    Write-Host "      tsc -b green." -ForegroundColor Green
}

Write-Host ""
Write-Host ("[DONE] Backup: " + $BackupDir)
Write-Host ""
Write-Host "YOUR THREE PAGES:"
Write-Host "  Type 1  http://localhost:5173/dashboard                                (Production Overview)"
Write-Host "  Type 2  http://localhost:5173/workspace/CORRELATION_FINDINGS_BOARD"
Write-Host "  Type 3  http://localhost:5173/workspace/RISK_INTELLIGENCE"
Write-Host "  Bonus:  /workspace/QUALITY_MONITORING  /workspace/EQUIPMENT_OPERATIONS"
Write-Host "          /workspace/PARAMETER_DEEP_ANALYSIS  /workspace/MODEL_INSIGHTS"
Write-Host ""
Write-Host "Hard-refresh, drag a widget, resize it, save the layout, click the"
Write-Host "filter bar. Anything that renders an empty state: name the widget."
exit 0
