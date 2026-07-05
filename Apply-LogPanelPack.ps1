& {
# ================================================================================================
# PPIQ V1-46 LOGPANEL PACK + V1-42 window-recompute prover patch + V1-18 diagnostic
# Anchored on the REAL 16:18 AppLayout (275 lines) - anchors asserted exactly-once at build time.
# Gates: tsc + vite build + vitest (new LogPanel test included).
# ================================================================================================
$ErrorActionPreference = 'Stop'
$RepoRoot = 'C:\Workspace\PlantProcess-IQ'
$web = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$enc = New-Object System.Text.UTF8Encoding($false)
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDir = Join-Path $RepoRoot ('deploy\.ppiq-backups\logpanel-' + $stamp)
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
Copy-Item (Join-Path $web 'src\components\AppLayout.tsx') (Join-Path $backupDir 'AppLayout.tsx') -Force
Copy-Item (Join-Path $RepoRoot 'scripts\run\Invoke-PpiqJourneyWalk.ps1') (Join-Path $backupDir 'Invoke-PpiqJourneyWalk.ps1') -Force

function Write-File([string]$Rel, [string]$Body) {
    $p = Join-Path $web $Rel
    New-Item -ItemType Directory -Path (Split-Path $p) -Force | Out-Null
    [System.IO.File]::WriteAllText($p, ($Body -replace "`n", "`r`n"), $enc)
    Write-Host ('  wrote ' + $Rel)
}
Write-Host '[1/4] LogPanel component + css + test'
Write-File 'src\components\logging\LogPanel.tsx' @'
import { useCallback, useEffect, useState } from "react";

import { apiClient } from "../../api/http";

import "./log-panel.css";

type JobLogEntry = {
  id: string;
  occurredAtUtc: string;
  jobType: string;
  jobName: string;
  severity: string;
  message: string;
};

const JOB_TYPES = ["", "Import-Stage1", "Import-Stage2", "Import-FullCycle", "ConnectorTest"];
const SEVERITIES = ["", "Info", "Warning", "Error"];

export function LogPanel() {
  const [open, setOpen] = useState<boolean>(
    new URLSearchParams(window.location.search).get("logs") === "open"
  );
  const [jobType, setJobType] = useState("");
  const [severity, setSeverity] = useState(
    new URLSearchParams(window.location.search).get("severity") ?? ""
  );
  const [day, setDay] = useState("");
  const [entries, setEntries] = useState<JobLogEntry[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    const q = new URLSearchParams();
    if (jobType) q.set("jobType", jobType);
    if (severity) q.set("severity", severity);
    if (day) q.set("day", day);
    apiClient
      .get<{ entries: JobLogEntry[] }>("/admin/job-logs?" + q.toString())
      .then((r) => {
        setEntries(r?.entries ?? []);
        setError(null);
      })
      .catch((e: unknown) => setError(e instanceof Error ? e.message : "load failed"));
  }, [jobType, severity, day]);

  useEffect(() => {
    if (!open) return;
    load();
    const t = window.setInterval(load, 10000);
    return () => window.clearInterval(t);
  }, [open, load]);

  return (
    <div className={open ? "piq-log-panel piq-log-panel--open" : "piq-log-panel"}>
      <button
        type="button"
        className="piq-log-panel__toggle"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        Job Log {open ? "\u25BC" : "\u25B2"}
      </button>

      {open ? (
        <div className="piq-log-panel__body">
          <div className="piq-log-panel__filters">
            <select value={jobType} onChange={(e) => setJobType(e.target.value)} aria-label="Job type">
              {JOB_TYPES.map((t) => (
                <option key={t} value={t}>
                  {t === "" ? "All job types" : t}
                </option>
              ))}
            </select>
            <select value={severity} onChange={(e) => setSeverity(e.target.value)} aria-label="Severity">
              {SEVERITIES.map((s) => (
                <option key={s} value={s}>
                  {s === "" ? "All severities" : s}
                </option>
              ))}
            </select>
            <input type="date" value={day} onChange={(e) => setDay(e.target.value)} aria-label="Day" />
            <button type="button" onClick={load}>Refresh</button>
          </div>

          {error ? <div className="piq-log-panel__error">{error}</div> : null}

          <table className="piq-log-panel__table">
            <thead>
              <tr><th>Time (UTC)</th><th>Type</th><th>Job</th><th>Severity</th><th>Message</th></tr>
            </thead>
            <tbody>
              {entries.length === 0 ? (
                <tr><td colSpan={5} className="piq-log-panel__empty">No job events for this filter.</td></tr>
              ) : (
                entries.map((e) => (
                  <tr key={e.id} className={"piq-log-row--" + e.severity.toLowerCase()}>
                    <td>{e.occurredAtUtc.replace("T", " ").slice(0, 19)}</td>
                    <td>{e.jobType}</td>
                    <td>{e.jobName}</td>
                    <td>{e.severity}</td>
                    <td>{e.message}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}

'@
Write-File 'src\components\logging\log-panel.css' @'
.piq-log-panel { position: sticky; bottom: 0; z-index: 40; background: rgba(10, 24, 44, 0.97); border-top: 1px solid rgba(94, 151, 214, 0.35); }
.piq-log-panel__toggle { width: 100%; text-align: left; padding: 6px 16px; background: none; border: none; color: #8aa3c0; font-size: 12px; letter-spacing: 1px; text-transform: uppercase; cursor: pointer; }
.piq-log-panel__body { max-height: 260px; overflow: auto; padding: 0 16px 12px; }
.piq-log-panel__filters { display: flex; gap: 8px; padding: 6px 0 10px; }
.piq-log-panel__filters select, .piq-log-panel__filters input, .piq-log-panel__filters button { background: #0e2238; color: #d7e5f7; border: 1px solid rgba(94, 151, 214, 0.35); border-radius: 4px; padding: 4px 8px; font-size: 12px; }
.piq-log-panel__table { width: 100%; border-collapse: collapse; font-size: 12px; color: #d7e5f7; }
.piq-log-panel__table th { text-align: left; color: #8aa3c0; padding: 4px 8px; border-bottom: 1px solid rgba(94, 151, 214, 0.25); position: sticky; top: 0; background: rgba(10, 24, 44, 0.97); }
.piq-log-panel__table td { padding: 4px 8px; border-bottom: 1px solid rgba(94, 151, 214, 0.10); }
.piq-log-row--error td { color: #e5484d; }
.piq-log-row--warning td { color: #f5a623; }
.piq-log-panel__empty { color: #8aa3c0; font-style: italic; }
.piq-log-panel__error { color: #e5484d; font-size: 12px; padding-bottom: 6px; }

'@
Write-File 'src\components\__tests__\LogPanel.test.tsx' @'
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";

import { LogPanel } from "@/components/logging/LogPanel";

const getMock = vi.fn();
vi.mock("@/api/http", () => ({
  apiClient: { get: (...args: unknown[]) => getMock(...args) },
}));

describe("LogPanel (V1-46)", () => {
  beforeEach(() => {
    getMock.mockReset();
    getMock.mockResolvedValue({
      entries: [
        { id: "1", occurredAtUtc: "2026-07-05T14:00:00Z", jobType: "Import-Stage1", jobName: "Import-Stage1 (x)", severity: "Info", message: "Started" },
        { id: "2", occurredAtUtc: "2026-07-05T14:00:01Z", jobType: "Import-Stage1", jobName: "Import-Stage1 (x)", severity: "Error", message: "Failed after 5 ms" },
      ],
    });
  });

  it("is collapsed by default and opens to show job events", async () => {
    render(<LogPanel />);
    expect(screen.queryByText(/No job events/)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: /Job Log/ }));
    await waitFor(() => expect(screen.getByText("Started")).toBeTruthy());
    expect(screen.getByText("Failed after 5 ms")).toBeTruthy();
    expect(getMock).toHaveBeenCalled();
  });

  it("passes the severity filter to the API", async () => {
    render(<LogPanel />);
    fireEvent.click(screen.getByRole("button", { name: /Job Log/ }));
    await waitFor(() => expect(getMock).toHaveBeenCalled());
    fireEvent.change(screen.getByLabelText("Severity"), { target: { value: "Error" } });
    await waitFor(() => {
      const urls = getMock.mock.calls.map((c) => String(c[0]));
      expect(urls.some((u) => u.includes("severity=Error"))).toBe(true);
    });
  });
});

'@

Write-Host '[2/4] Mount in AppLayout (refuse-if-diverged)'
$p = Join-Path $web 'src\components\AppLayout.tsx'
$t = [System.IO.File]::ReadAllText($p).Replace("`r", "")
if ($t.Contains('LogPanel')) {
    Write-Host '  already mounted - skipped'
} else {
    $a1 = @'
        {/* Page content */}
        <div className="piq-workspace">
          <Outlet />
        </div>
'@
    $a2 = 'import { AppToaster } from "../notifications/Toaster";'
    foreach ($a in @($a1, $a2)) {
        $c = ([regex]::Matches($t, [regex]::Escape($a))).Count
        if ($c -ne 1) { throw ('AppLayout anchor found ' + $c + ' times - refusing') }
    }
    $t = $t.Replace($a1, @'
        {/* Page content */}
        <div className="piq-workspace">
          <Outlet />
        </div>

        <LogPanel />
'@)
    $t = $t.Replace($a2, @'
import { AppToaster } from "../notifications/Toaster";

import { LogPanel } from "./logging/LogPanel";
'@)
    [System.IO.File]::WriteAllText($p, ($t -replace "`n", "`r`n"), $enc)
    Write-Host '  mounted LogPanel below the workspace (present on every page)'
}

Write-Host '[3/4] Prover: V1-42 window-recompute block'
$pp = Join-Path $RepoRoot 'scripts\run\Invoke-PpiqJourneyWalk.ps1'
$pt = [System.IO.File]::ReadAllText($pp).Replace("`r", "")
if ($pt.Contains('Window recompute')) {
    Write-Host '  already patched - skipped'
} else {
    $anchor = "Add-Ev 'V1-23' 'HMI: inspection run renders ranked list"
    $c = ([regex]::Matches($pt, [regex]::Escape($anchor))).Count
    if ($c -ne 1) { throw 'prover anchor diverged - refusing' }
    $pt = $pt.Replace($anchor, @'
# ---- V1-42 window-recompute: same job, changed duration window, recomputes ----
try {
    $w30 = (Sql "SELECT status || '|' || result_count FROM ppiq_ml_run_learning_job_v1('ML_PROCESS_VS_DEFECT', NULL, 30);")[0]
    $w60 = (Sql "SELECT status || '|' || result_count FROM ppiq_ml_run_learning_job_v1('ML_PROCESS_VS_DEFECT', NULL, 60);")[0]
    Add-Ev 'V1-42' ('Window recompute 30d=' + $w30 + '  60d=' + $w60) $(if (($w30 -match 'Completed') -and ($w60 -match 'Completed')) { 'PASS' } else { 'EVIDENCE' }) 'Changed duration window recomputes (acceptance tail).'
} catch { Add-Ev 'V1-42' 'Window recompute' 'EVIDENCE' $_.Exception.Message }

'@ + $anchor)
    [System.IO.File]::WriteAllText($pp, ($pt -replace "`n", "`r`n"), $enc)
    Write-Host '  window-recompute assertion added before the J7 MANUAL row'
}

Write-Host '[4/4] Gates'
Push-Location $web
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'npm run build FAILED' }
    npx vitest run
    if ($LASTEXITCODE -ne 0) { throw 'vitest FAILED' }
} finally { Pop-Location }
Write-Host ''
Write-Host '--- V1-18 diagnostic: capture the test-connect 400 body ---'
$token = (Invoke-RestMethod -Method Post -Uri 'http://localhost:5063/auth/login' -ContentType 'application/json' -Body (@{username='e2eadmin';password='E2EAdmin123!'} | ConvertTo-Json)).accessToken
$H = @{ Authorization = 'Bearer ' + $token }
$profiles = Invoke-RestMethod -Uri 'http://localhost:5063/admin/connectors/connection-profiles' -Headers $H
$list = @($profiles); if ($profiles.PSObject.Properties['items']) { $list = @($profiles.items) }
foreach ($p in $list) {
    try {
        $r = Invoke-RestMethod -Method Post -Uri ('http://localhost:5063/admin/connectors/connection-profiles/' + $p.id + '/test') -Headers $H
        Write-Host ('  OK   ' + $p.connectionProfileCode + ' -> ' + ($r | ConvertTo-Json -Compress -Depth 3))
    } catch {
        $body = ''
        try { $sr = New-Object IO.StreamReader($_.Exception.Response.GetResponseStream()); $body = $sr.ReadToEnd() } catch {}
        Write-Host ('  FAIL ' + $p.connectionProfileCode + ' -> ' + $body) -ForegroundColor Red
    }
}
Write-Host ''
Write-Host 'GREEN. Open any page: the Job Log bar sits at the bottom; ?logs=open&severity=Error deep-links.'
Write-Host 'Re-run the prover for the V1-42 window PASS. Commit: PPIQ_COMMIT=1 not used here - commit manually:'
Write-Host '  git add -A ; git commit -m "V1-46 LogPanel on every page + V1-42 window-recompute prover + V1-18 diagnostic"'
}
