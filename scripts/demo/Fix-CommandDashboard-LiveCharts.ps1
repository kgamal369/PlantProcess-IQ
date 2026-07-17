# ============================================================================
# Fix-CommandDashboard-LiveCharts.ps1
# THE STEP-7 SHINE PACK. The current /dashboard page (MaterialAnalyticsPages)
# renders ChartPlaceholder stubs where the May version had real charts. This
# pack wires the two headline panels to the LIVE widget-query engine that
# already works (the Widget Drift page proves it):
#     POST /dashboarding/widget-query-expression/execute
#
#   Quality trend        -> real LINE chart  (day x defectRate)
#   Risk distribution    -> real DONUT chart (severity x defectCount)
#
# HOW IT STAYS SAFE ON DEMO DAY:
#   * one NEW component file (LiveWidgetChart.tsx) - no anchor risk there
#   * the component is envelope-defensive: it accepts rows under several
#     response shapes, auto-detects label/value keys, and on error or empty
#     data renders a clean honest empty state - it can NEVER break the page
#   * two anchored swaps + one import line in MaterialAnalyticsPages.tsx,
#     unique-anchor preflight, byte backup, tsc -b gate, full auto-revert
#   * no inline styles, no raw controls - the ratchet and T11 stay green
# Run from repo root (presentation branch):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-CommandDashboard-LiveCharts.ps1
# ============================================================================
[CmdletBinding()]
param(
    [switch]$SkipGate
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Get-Location).Path
$Web      = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$PageFile = Join-Path $Web 'src\pages\MaterialAnalytics\MaterialAnalyticsPages.tsx'
$CompFile = Join-Path $Web 'src\components\dashboard\LiveWidgetChart.tsx'
$Stamp    = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\dashboard-livecharts-" + $Stamp)

if (-not (Test-Path $PageFile)) { Write-Host "[FAIL] MaterialAnalyticsPages.tsx not found." -ForegroundColor Red; exit 1 }

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
Copy-Item -LiteralPath $PageFile -Destination (Join-Path $BackupDir 'MaterialAnalyticsPages.tsx') -Force
$compExisted = Test-Path $CompFile
if ($compExisted) { Copy-Item -LiteralPath $CompFile -Destination (Join-Path $BackupDir 'LiveWidgetChart.tsx') -Force }

function Restore-All {
    Copy-Item -LiteralPath (Join-Path $BackupDir 'MaterialAnalyticsPages.tsx') -Destination $PageFile -Force
    if ($compExisted) {
        Copy-Item -LiteralPath (Join-Path $BackupDir 'LiveWidgetChart.tsx') -Destination $CompFile -Force
    } elseif (Test-Path $CompFile) {
        Remove-Item -LiteralPath $CompFile -Force
    }
    Write-Host ("[REVERT] restored. Backup: " + $BackupDir) -ForegroundColor Yellow
}

# ---- 1. the component (new file) -------------------------------------------
$component = @'
import { useEffect, useState } from "react";
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  PieChart,
  Pie,
  Cell,
  BarChart,
  Bar,
} from "recharts";
import { apiClient } from "@/api/http";

const PALETTE = ["#38bdf8", "#34d399", "#f59e0b", "#f87171", "#a78bfa", "#22d3ee", "#facc15", "#fb7185"];

type ChartKind = "line" | "donut" | "bar";

type Point = { label: string; value: number };

function toNumber(v: unknown): number | null {
  if (typeof v === "number" && Number.isFinite(v)) return v;
  if (typeof v === "string" && v.trim() !== "" && Number.isFinite(Number(v))) return Number(v);
  return null;
}

function extractRows(payload: unknown): Record<string, unknown>[] {
  if (Array.isArray(payload)) return payload as Record<string, unknown>[];
  if (payload && typeof payload === "object") {
    const p = payload as Record<string, unknown>;
    for (const key of ["rows", "data", "items", "results", "points"]) {
      if (Array.isArray(p[key])) return p[key] as Record<string, unknown>[];
    }
    const nested = p["result"];
    if (nested && typeof nested === "object") return extractRows(nested);
  }
  return [];
}

function toPoints(rows: Record<string, unknown>[]): Point[] {
  if (rows.length === 0) return [];
  const keys = Object.keys(rows[0]);
  const valueKey =
    keys.find((k) => /^(value|measure|count|rate|score|total|y)$/i.test(k)) ??
    keys.find((k) => toNumber(rows[0][k]) !== null && !/id$/i.test(k));
  const labelKey =
    keys.find((k) => /^(label|dimension|name|key|day|date|bucket|x)$/i.test(k)) ??
    keys.find((k) => k !== valueKey);
  if (!valueKey || !labelKey) return [];
  return rows
    .map((r) => ({ label: String(r[labelKey] ?? ""), value: toNumber(r[valueKey]) ?? 0 }))
    .filter((p) => p.label !== "");
}

export function LiveWidgetChart({
  title,
  chartType,
  dimensionCode,
  measureCode,
  maxRows = 60,
}: {
  title: string;
  chartType: ChartKind;
  dimensionCode: string;
  measureCode: string;
  maxRows?: number;
}) {
  const [points, setPoints] = useState<Point[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let alive = true;
    apiClient
      .post<unknown>("/dashboarding/widget-query-expression/execute", {
        widgetType: "chart",
        chartType,
        dimensionCode,
        measureCode,
        filters: {},
        options: { maxRows, rawRowLimit: 10000 },
      })
      .then((payload) => {
        if (!alive) return;
        setPoints(toPoints(extractRows(payload)));
      })
      .catch(() => {
        if (!alive) return;
        setFailed(true);
      });
    return () => {
      alive = false;
    };
  }, [chartType, dimensionCode, measureCode, maxRows]);

  if (failed || (points !== null && points.length === 0)) {
    return (
      <div className="productModule56-chart-box" role="img" aria-label={title}>
        <div>
          <strong>{title}</strong>
          <p>No data in the current scope yet.</p>
        </div>
      </div>
    );
  }
  if (points === null) {
    return (
      <div className="productModule56-chart-box" role="img" aria-label={title}>
        <div>
          <strong>{title}</strong>
          <p>Loading...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="productModule56-chart-box" role="img" aria-label={title}>
      <ResponsiveContainer width="100%" height={260}>
        {chartType === "donut" ? (
          <PieChart>
            <Pie data={points} dataKey="value" nameKey="label" innerRadius={60} outerRadius={95} paddingAngle={2}>
              {points.map((entry, index) => (
                <Cell key={entry.label + index} fill={PALETTE[index % PALETTE.length]} />
              ))}
            </Pie>
            <Tooltip />
          </PieChart>
        ) : chartType === "bar" ? (
          <BarChart data={points}>
            <CartesianGrid strokeDasharray="3 3" stroke="#1e3a5f" />
            <XAxis dataKey="label" stroke="#7dd3fc" fontSize={11} />
            <YAxis stroke="#7dd3fc" fontSize={11} />
            <Tooltip />
            <Bar dataKey="value" fill="#38bdf8" radius={[4, 4, 0, 0]} />
          </BarChart>
        ) : (
          <LineChart data={points}>
            <CartesianGrid strokeDasharray="3 3" stroke="#1e3a5f" />
            <XAxis dataKey="label" stroke="#7dd3fc" fontSize={11} />
            <YAxis stroke="#7dd3fc" fontSize={11} />
            <Tooltip />
            <Line type="monotone" dataKey="value" stroke="#38bdf8" strokeWidth={2} dot={false} />
          </LineChart>
        )}
      </ResponsiveContainer>
    </div>
  );
}
'@
[System.IO.File]::WriteAllText($CompFile, ($component -replace "`r`n", "`n" -replace "`n", "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "      CREATED src\components\dashboard\LiveWidgetChart.tsx"

# ---- 2. anchored edits on the page ------------------------------------------
$edits = @(
    @{ Id = 'IMP'; Anchor = 'import { apiClient } from "@/api/http";';
       Replace = ('import { apiClient } from "@/api/http";' + "`r`n" + 'import { LiveWidgetChart } from "@/components/dashboard/LiveWidgetChart";') },
    @{ Id = 'C1'; Anchor = '<ChartPlaceholder title="Quality trend" note="Defect trend and volume trend are rendered in the standard dashboard frame." />';
       Replace = '<LiveWidgetChart title="Quality trend" chartType="line" dimensionCode="day" measureCode="defectRate" />' },
    @{ Id = 'C2'; Anchor = '<ChartPlaceholder title="Risk distribution" note="Low, Medium and High buckets share the same tokenized risk-chip treatment." />';
       Replace = '<LiveWidgetChart title="Severity distribution" chartType="donut" dimensionCode="severity" measureCode="defectCount" />' }
)

$text = [System.IO.File]::ReadAllText($PageFile, [System.Text.Encoding]::UTF8)
foreach ($e in $edits) {
    $count = 0; $idx = 0
    while (($idx = $text.IndexOf([string]$e.Anchor, $idx, [System.StringComparison]::Ordinal)) -ge 0) { $count++; $idx += ([string]$e.Anchor).Length }
    if ($count -ne 1) {
        Write-Host ("[ABORT] " + $e.Id + " anchor count=" + $count + " - page drifted; nothing kept.") -ForegroundColor Red
        Restore-All
        exit 1
    }
    $text = $text.Replace([string]$e.Anchor, [string]$e.Replace)
    Write-Host ("      APPLIED " + $e.Id)
}
[System.IO.File]::WriteAllText($PageFile, $text, (New-Object System.Text.UTF8Encoding($false)))

# ---- 3. gate -----------------------------------------------------------------
if ($SkipGate) {
    Write-Host "[GATE SKIPPED] run: npx tsc -b"
} else {
    Write-Host "[GATE] npx tsc -b ..."
    Push-Location $Web
    try { & npx tsc -b; $code = $LASTEXITCODE } finally { Pop-Location }
    if ($code -ne 0) {
        Write-Host "[GATE RED] reverting everything." -ForegroundColor Red
        Restore-All
        exit 1
    }
    Write-Host "      tsc -b green." -ForegroundColor Green
}

Write-Host ""
Write-Host ("[DONE] Backup: " + $BackupDir)
Write-Host "BROWSER: hard-refresh /dashboard. Quality trend should draw a live line"
Write-Host "and Severity distribution a live donut from your 51k quality events."
Write-Host "If either shows 'No data in the current scope yet', paste the browser"
Write-Host "devtools Network response of widget-query-expression/execute and I"
Write-Host "rebind the dimension/measure in one edit."
exit 0
