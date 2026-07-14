#requires -Version 5.1
<#
================================================================================
 PPIQ FIX  -  Bring 3 new pages into design-system compliance (PPIQ-T09/T11)
================================================================================
 RUN:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-NewPages-DesignSystem.ps1
   ... -Revert    (restore the 3 pages from backup)
   ... -NoTest    (skip re-running the two guard tests)

 Your existing vitest guards caught that AuthorMappingPage / AlertingPage /
 SupervisorReportPage used raw "Failed to load" strings (PPIQ-T09) and raw
 <button>/<table> (PPIQ-T11). This refactors all three to your conventions:
   - load errors -> <DataFetchBoundary title isLoading error isEmpty onRetry>
   - raw <table>  -> <StandardTable columns data getRowKey>
   - raw <button> -> <StandardButton>
 Behavior and API calls are unchanged; only presentation is made compliant.

 GATE: npx tsc --noEmit (must reference no new errors in these files) AND
       npx vitest run src/test/architecture (both guards must pass). Any red -> auto-revert.
================================================================================
#>
param([switch]$Revert,[switch]$NoTest)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Write-Info($m){ Write-Host "[i] $m" -ForegroundColor Cyan }
function Write-Ok  ($m){ Write-Host "[+] $m" -ForegroundColor Green }
function Write-Warn($m){ Write-Host "[!] $m" -ForegroundColor Yellow }
function Write-Err ($m){ Write-Host "[x] $m" -ForegroundColor Red }
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
function Write-TextFile([string]$Path,[string]$Content){ [System.IO.File]::WriteAllText($Path, ($Content -replace "`r`n","`n" -replace "`n","`r`n"), $Utf8NoBom) }
function Read-TextFile([string]$Path){ return [System.IO.File]::ReadAllText($Path) }

$RepoRoot = (Get-Location).Path
$WebRoot  = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$PageDir  = Join-Path $WebRoot 'src\pages\DataIntegration'
$Author   = Join-Path $PageDir 'AuthorMappingPage.tsx'
$Alerting = Join-Path $PageDir 'AlertingPage.tsx'
$Super    = Join-Path $PageDir 'SupervisorReportPage.tsx'
$BackupRoot = Join-Path $RepoRoot 'deploy\.ppiq-backups'
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $BackupRoot ("DSFIX_" + $Stamp)

if ($Revert) {
    $last = Get-ChildItem $BackupRoot -Directory -Filter 'DSFIX_*' -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $last) { Write-Err "No DSFIX backup."; exit 1 }
    Get-ChildItem $last.FullName -Filter '*.bak' | ForEach-Object {
        $orig = ((Get-Content $_.FullName -TotalCount 1) -replace '^// PPIQ-ORIGINAL-PATH: ','')
        $body = (Get-Content $_.FullName -Raw) -replace "^// PPIQ-ORIGINAL-PATH: [^\r\n]*\r?\n",''
        Write-TextFile $orig $body; Write-Ok "restored $orig"
    }
    exit 0
}

foreach ($f in @($Author,$Alerting,$Super)) { if (-not (Test-Path $f)) { Write-Err "Missing $f"; exit 1 } }
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
foreach ($f in @($Author,$Alerting,$Super)) {
    [System.IO.File]::WriteAllText((Join-Path $BackupDir ([System.IO.Path]::GetFileName($f)+'.bak')), "// PPIQ-ORIGINAL-PATH: $f`r`n" + (Read-TextFile $f), $Utf8NoBom)
}
Write-Ok "Backup -> $BackupDir"

# ============================================================ SupervisorReportPage
$superContent = @'
// M1-05 SupervisorReportPage - design-system compliant (PPIQ-T09/T11).
import { useEffect, useState } from "react";
import { StandardPageHeader, StandardButton, DataFetchBoundary } from "@/components/standard";
import {
  listSupervisorReports,
  runSupervisor,
  type SupervisorReport,
} from "@/api/engine/supervisor.api";
import "./SupervisorReportPage.css";

export function SupervisorReportPage() {
  const [reports, setReports] = useState<SupervisorReport[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const r = await listSupervisorReports();
      setReports(Array.isArray(r) ? r : []);
    } catch (e: unknown) {
      setError(e);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    listSupervisorReports()
      .then((r) => { if (!cancelled) setReports(Array.isArray(r) ? r : []); })
      .catch((e: unknown) => { if (!cancelled) setError(e); })
      .finally(() => { if (!cancelled) setIsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  async function onRun() {
    setBusy(true);
    setError(null);
    try {
      await runSupervisor();
      await load();
    } catch (e: unknown) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="ppiq-supervisor">
      <StandardPageHeader
        title="Engine Supervisor"
        subtitle="A weekly review of the engine's findings (journey step 14). Read-only: it never changes a job automatically."
        actions={
          <StandardButton onClick={onRun} isDisabled={busy} isLoading={busy}>
            Run review now
          </StandardButton>
        }
      />
      <DataFetchBoundary
        title="Supervisor reports"
        isLoading={isLoading}
        error={error}
        isEmpty={reports.length === 0}
        emptyMessage={'No supervisor reports yet. Click "Run review now" to generate the first one.'}
        onRetry={() => void load()}
      >
        <ol className="ppiq-sup-list">
          {reports.map((r) => (
            <li key={r.id} className="ppiq-sup-item">
              <div className="ppiq-sup-item-head">
                <span className="ppiq-sup-title">{r.title}</span>
                <span className="ppiq-sup-date">{r.createdAtUtc}</span>
              </div>
              <pre className="ppiq-sup-body">{r.body}</pre>
            </li>
          ))}
        </ol>
      </DataFetchBoundary>
    </div>
  );
}

export default SupervisorReportPage;
'@
Write-TextFile $Super $superContent
Write-Ok "rewrote SupervisorReportPage.tsx"

# ============================================================ AlertingPage
$alertingContent = @'
// M1-06 AlertingPage - design-system compliant (PPIQ-T09/T11).
import { useEffect, useState } from "react";
import {
  StandardPageHeader,
  StandardButton,
  StandardTable,
  DataFetchBoundary,
  type StandardTableColumn,
} from "@/components/standard";
import {
  createRule,
  evaluateAlerts,
  listLog,
  listRules,
  type AlertRule,
  type PlantDataLogRow,
} from "@/api/engine/alerts.api";
import "./AlertingPage.css";

const COMPARATORS = [">", ">=", "<", "<=", "="];
const SEVERITIES = ["Info", "Warning", "Critical"];

const ruleColumns: ReadonlyArray<StandardTableColumn<AlertRule>> = [
  { key: "name", header: "Name", cell: (r) => r.ruleName },
  { key: "param", header: "Parameter", cell: (r) => r.parameterCode },
  { key: "cond", header: "Condition", cell: (r) => `${r.comparator} ${r.limitValue}` },
  { key: "sev", header: "Severity", cell: (r) => r.severity },
];

const logColumns: ReadonlyArray<StandardTableColumn<PlantDataLogRow>> = [
  { key: "time", header: "Time", cell: (r) => r.loggedAtUtc },
  { key: "rule", header: "Rule", cell: (r) => r.ruleName },
  { key: "mat", header: "Material", cell: (r) => r.materialCode ?? "-" },
  { key: "param", header: "Parameter", cell: (r) => r.parameterCode },
  { key: "val", header: "Value", cell: (r) => (r.observedValue ?? "-") as React.ReactNode },
  { key: "cond", header: "Condition", cell: (r) => `${r.comparator} ${r.limitValue}` },
  { key: "sev", header: "Severity", cell: (r) => r.severity },
];

export function AlertingPage() {
  const [rules, setRules] = useState<AlertRule[]>([]);
  const [log, setLog] = useState<PlantDataLogRow[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const [ruleName, setRuleName] = useState("");
  const [parameterCode, setParameterCode] = useState("");
  const [comparator, setComparator] = useState(">");
  const [limitValue, setLimitValue] = useState("");
  const [severity, setSeverity] = useState("Warning");

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const [rs, lg] = await Promise.all([listRules(), listLog()]);
      setRules(Array.isArray(rs) ? rs : []);
      setLog(Array.isArray(lg) ? lg : []);
    } catch (e: unknown) {
      setError(e);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    Promise.all([listRules(), listLog()])
      .then(([rs, lg]) => {
        if (cancelled) return;
        setRules(Array.isArray(rs) ? rs : []);
        setLog(Array.isArray(lg) ? lg : []);
      })
      .catch((e: unknown) => { if (!cancelled) setError(e); })
      .finally(() => { if (!cancelled) setIsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  async function onCreate() {
    setFormError(null);
    setNotice(null);
    if (!ruleName.trim() || !parameterCode.trim()) {
      setFormError("Rule name and parameter code are required.");
      return;
    }
    const limit = Number(limitValue);
    if (Number.isNaN(limit)) {
      setFormError("Limit must be a number.");
      return;
    }
    setBusy(true);
    try {
      await createRule({ ruleName: ruleName.trim(), parameterCode: parameterCode.trim(), comparator, limitValue: limit, severity });
      setRuleName(""); setParameterCode(""); setLimitValue("");
      setNotice("Rule created. Click 'Run evaluation' to scan observations.");
      await load();
    } catch (e: unknown) {
      setFormError(e instanceof Error ? e.message : "Could not create the rule.");
    } finally {
      setBusy(false);
    }
  }

  async function onEvaluate() {
    setFormError(null);
    setBusy(true);
    try {
      const res = await evaluateAlerts();
      setNotice(`Evaluation complete: ${res.logged} new log row(s).`);
      await load();
    } catch (e: unknown) {
      setFormError(e instanceof Error ? e.message : "Could not run evaluation.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="ppiq-alerting">
      <StandardPageHeader
        title="Plant Data Log"
        subtitle="Define threshold rules; the evaluator scans imported observations and logs breaches (journey step: alerting)."
        actions={
          <StandardButton onClick={onEvaluate} isDisabled={busy} isLoading={busy}>
            Run evaluation
          </StandardButton>
        }
      />

      <div className="ppiq-al-form">
        <label className="ppiq-al-label">
          Rule name
          <input className="ppiq-al-input" value={ruleName} onChange={(e) => setRuleName(e.target.value)} placeholder="Superheat high" />
        </label>
        <label className="ppiq-al-label">
          Parameter code
          <input className="ppiq-al-input" value={parameterCode} onChange={(e) => setParameterCode(e.target.value)} placeholder="SUPERHEAT_C" />
        </label>
        <label className="ppiq-al-label">
          Comparator
          <select className="ppiq-al-input" value={comparator} onChange={(e) => setComparator(e.target.value)}>
            {COMPARATORS.map((c) => (<option key={c} value={c}>{c}</option>))}
          </select>
        </label>
        <label className="ppiq-al-label">
          Limit
          <input className="ppiq-al-input" value={limitValue} onChange={(e) => setLimitValue(e.target.value)} placeholder="36" inputMode="decimal" />
        </label>
        <label className="ppiq-al-label">
          Severity
          <select className="ppiq-al-input" value={severity} onChange={(e) => setSeverity(e.target.value)}>
            {SEVERITIES.map((s) => (<option key={s} value={s}>{s}</option>))}
          </select>
        </label>
        <StandardButton onClick={onCreate} isDisabled={busy}>Add rule</StandardButton>
      </div>

      {notice && <div className="ppiq-al-notice">{notice}</div>}
      {formError && <div className="ppiq-al-error">{formError}</div>}

      <h3 className="ppiq-al-h3">Rules</h3>
      <StandardTable
        columns={ruleColumns}
        data={rules}
        getRowKey={(r) => r.id}
        loading={isLoading}
        emptyTitle="No rules yet"
        emptyDescription="Add one above."
      />

      <h3 className="ppiq-al-h3">Plant data log</h3>
      <DataFetchBoundary
        title="Plant data log"
        isLoading={isLoading}
        error={error}
        isEmpty={log.length === 0}
        emptyMessage="No breaches logged yet. Create a rule and run evaluation."
        onRetry={() => void load()}
      >
        <StandardTable
          columns={logColumns}
          data={log}
          getRowKey={(r) => r.id}
        />
      </DataFetchBoundary>
    </div>
  );
}

export default AlertingPage;
'@
Write-TextFile $Alerting $alertingContent
Write-Ok "rewrote AlertingPage.tsx"

# ============================================================ AuthorMappingPage
$authorContent = @'
// M1-04 AuthorMappingPage - design-system compliant (PPIQ-T09/T11).
import { useEffect, useMemo, useState } from "react";
import {
  StandardPageHeader,
  StandardButton,
  StandardTable,
  DataFetchBoundary,
  type StandardTableColumn,
} from "@/components/standard";
import {
  createMappingDefinition,
  executeMapping,
  listImportBatches,
  type ExecuteResult,
  type ImportBatch,
} from "@/api/integration/mappingAuthor.api";
import "./AuthorMappingPage.css";

type FieldRow = { idx: number; target: string; source: string };

const TARGET_ENTITIES = [
  "DefectCatalog", "ParameterDefinition", "MaterialUnit", "MaterialAlias",
  "ProcessStepExecution", "ParameterObservation", "QualityEvent", "GenealogyEdge",
];

const SUGGESTED: Record<string, string[]> = {
  DefectCatalog: ["DefectCode", "DefectName", "DefectCategory"],
  ParameterDefinition: ["ParameterCode", "ParameterName", "ValueType", "UnitOfMeasure"],
  MaterialUnit: ["MaterialCode", "MaterialType", "ProducedAtUtc"],
  MaterialAlias: ["MaterialCode", "AliasCode", "AliasType"],
  ProcessStepExecution: ["MaterialCode", "StepCode", "StartedAtUtc"],
  ParameterObservation: ["MaterialCode", "ParameterCode", "ObservedAtUtc", "NumericValue"],
  QualityEvent: ["MaterialCode", "DefectCode", "EventType", "EventAtUtc"],
  GenealogyEdge: ["ParentMaterialCode", "ChildMaterialCode", "RelationshipType"],
};

function readNum(r: ExecuteResult, keys: string[]): number | null {
  for (const k of keys) { const v = r[k]; if (typeof v === "number") return v; }
  return null;
}

export function AuthorMappingPage() {
  const [batches, setBatches] = useState<ImportBatch[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const [batchId, setBatchId] = useState<string>("");
  const [targetEntity, setTargetEntity] = useState<string>("DefectCatalog");
  const [rows, setRows] = useState<FieldRow[]>([{ idx: 0, target: "", source: "" }]);
  const [nextIdx, setNextIdx] = useState(1);

  const [busy, setBusy] = useState(false);
  const [mappingId, setMappingId] = useState<string | null>(null);
  const [result, setResult] = useState<ExecuteResult | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const b = await listImportBatches();
      setBatches(Array.isArray(b) ? b : []);
      const first = Array.isArray(b) ? b[0] : undefined;
      if (first) setBatchId(first.id);
    } catch (e: unknown) {
      setError(e);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    listImportBatches()
      .then((b) => {
        if (cancelled) return;
        setBatches(Array.isArray(b) ? b : []);
        const first = Array.isArray(b) ? b[0] : undefined;
        if (first) setBatchId(first.id);
      })
      .catch((e: unknown) => { if (!cancelled) setError(e); })
      .finally(() => { if (!cancelled) setIsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const selectedBatch = useMemo(() => batches.find((b) => b.id === batchId) ?? null, [batches, batchId]);

  function seedRows(entity: string) {
    const s = SUGGESTED[entity] ?? [];
    if (s.length > 0) {
      setRows(s.map((t, i) => ({ idx: i, target: t, source: "" })));
      setNextIdx(s.length);
    } else {
      setRows([{ idx: 0, target: "", source: "" }]);
      setNextIdx(1);
    }
  }
  function updateRow(idx: number, patch: Partial<FieldRow>) {
    setRows((prev) => prev.map((r) => (r.idx === idx ? { ...r, ...patch } : r)));
  }
  function addRow() {
    setRows((prev) => [...prev, { idx: nextIdx, target: "", source: "" }]);
    setNextIdx((n) => n + 1);
  }
  function removeRow(idx: number) {
    setRows((prev) => prev.filter((r) => r.idx !== idx));
  }

  const fieldColumns: ReadonlyArray<StandardTableColumn<FieldRow>> = [
    {
      key: "target", header: "Target field",
      cell: (r) => (
        <input className="ppiq-am-input" value={r.target} placeholder="e.g. DefectCode"
          onChange={(e) => updateRow(r.idx, { target: e.target.value })} />
      ),
    },
    {
      key: "source", header: "Source (column or const:VALUE)",
      cell: (r) => (
        <input className="ppiq-am-input" value={r.source} placeholder="e.g. defect_code or const:CRACK_LONG"
          onChange={(e) => updateRow(r.idx, { source: e.target.value })} />
      ),
    },
    {
      key: "actions", header: "", align: "right",
      cell: (r) => (
        <StandardButton variant="secondary" size="sm" onClick={() => removeRow(r.idx)}>
          Remove
        </StandardButton>
      ),
    },
  ];

  async function onSave() {
    if (!selectedBatch) { setFormError("Pick an import batch first."); return; }
    const map: Record<string, string> = {};
    rows.forEach((r) => { if (r.target.trim() && r.source.trim()) map[r.target.trim()] = r.source.trim(); });
    if (Object.keys(map).length === 0) {
      setFormError("Add at least one field map (target field + source column or const:VALUE).");
      return;
    }
    setFormError(null); setNotice(null); setBusy(true);
    try {
      const res = await createMappingDefinition({
        sourceSystemDefinitionId: selectedBatch.sourceSystemDefinitionId,
        mappingCode: `UI-${selectedBatch.sourceObjectName}-${targetEntity}-${Date.now()}`,
        mappingName: `${targetEntity} from ${selectedBatch.sourceObjectName}`,
        sourceObjectName: selectedBatch.sourceObjectName,
        targetEntityName: targetEntity,
        mappingJson: JSON.stringify(map),
        mappingVersion: "v1",
        description: "Authored in HMI (M1-04 step-4)",
        isSynthetic: false,
        sourceSystem: selectedBatch.sourceSystem ?? null,
        sourceRecordId: null,
      });
      setMappingId(res.id);
      setNotice(`Mapping saved (id ${res.id}). Now Execute to project this batch.`);
    } catch (e: unknown) {
      setFormError(e instanceof Error ? e.message : "Could not save the mapping.");
    } finally {
      setBusy(false);
    }
  }

  async function onExecute() {
    if (!mappingId || !selectedBatch) return;
    setFormError(null); setBusy(true);
    try {
      const res = await executeMapping(mappingId, selectedBatch.id);
      setResult(res);
      setNotice("Executed. Staged rows for this batch should now be Mapped and canonical rows grew.");
    } catch (e: unknown) {
      setFormError(e instanceof Error ? e.message : "Could not execute the mapping.");
    } finally {
      setBusy(false);
    }
  }

  const mapped = result ? readNum(result, ["mapped", "mappedCount", "mappedRows"]) : null;
  const failed = result ? readNum(result, ["failed", "failedCount", "errors"]) : null;
  const total = result ? readNum(result, ["total", "processed", "rowCount"]) : null;

  return (
    <div className="ppiq-author-mapping">
      <StandardPageHeader
        title="Load to Plant Data"
        subtitle="Author a mapping for a staged object and project it into the canonical plant schema (journey step 4-6)."
      />
      <DataFetchBoundary
        title="Import batches"
        isLoading={isLoading}
        error={error}
        isEmpty={batches.length === 0}
        emptyMessage="No import batches yet. Connect a source and import data first (steps 1-3), then return here."
        onRetry={() => void load()}
      >
        <div className="ppiq-am-row">
          <label className="ppiq-am-label">
            Import batch
            <select className="ppiq-am-select" value={batchId}
              onChange={(e) => { setBatchId(e.target.value); setMappingId(null); setResult(null); }}>
              {batches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.sourceObjectName} - {b.status ?? "unknown"} - {b.startedAtUtc ?? ""}
                </option>
              ))}
            </select>
          </label>
          <label className="ppiq-am-label">
            Target entity
            <select className="ppiq-am-select" value={targetEntity}
              onChange={(e) => { setTargetEntity(e.target.value); seedRows(e.target.value); setMappingId(null); setResult(null); }}>
              {TARGET_ENTITIES.map((t) => (<option key={t} value={t}>{t}</option>))}
            </select>
          </label>
        </div>

        {selectedBatch && (
          <p className="ppiq-am-muted">
            Source object <strong>{selectedBatch.sourceObjectName}</strong> from system{" "}
            <strong>{selectedBatch.sourceSystem ?? "-"}</strong>. Source can be a column name or{" "}
            <code>const:VALUE</code> for a literal.
          </p>
        )}

        <StandardTable
          columns={fieldColumns}
          data={rows}
          getRowKey={(r) => r.idx}
        />

        <div className="ppiq-am-actions">
          <StandardButton onClick={addRow} isDisabled={busy}>Add field</StandardButton>
          <StandardButton onClick={onSave} isDisabled={busy || !selectedBatch}>Save mapping</StandardButton>
          <StandardButton onClick={onExecute} isDisabled={busy || !mappingId}>Execute (project)</StandardButton>
        </div>

        {notice && <div className="ppiq-am-notice">{notice}</div>}
        {formError && <div className="ppiq-am-error">{formError}</div>}

        {result && (
          <div className="ppiq-am-result">
            <strong>Projection result</strong>
            <div className="ppiq-am-result-nums">
              {mapped !== null && <span>Mapped: {mapped}</span>}
              {failed !== null && <span>Failed: {failed}</span>}
              {total !== null && <span>Total: {total}</span>}
            </div>
            <pre className="ppiq-am-json">{JSON.stringify(result, null, 2)}</pre>
          </div>
        )}
      </DataFetchBoundary>
    </div>
  );
}

export default AuthorMappingPage;
'@
Write-TextFile $Author $authorContent
Write-Ok "rewrote AuthorMappingPage.tsx"

# ============================================================ gates
Push-Location $WebRoot
$saveEap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'

Write-Info "tsc --noEmit ..."
$tsc = & npx --no-install tsc --noEmit 2>&1
$tscMine = @($tsc | Select-String -Pattern 'AuthorMappingPage|AlertingPage|SupervisorReportPage')
if ($tscMine.Count -gt 0) {
    $ErrorActionPreference = $saveEap; Pop-Location
    Write-Err "tsc errors in the rewritten pages - auto-reverting:"
    $tscMine | ForEach-Object { Write-Err ("   " + $_.Line) }
    & $PSCommandPath -Revert | Out-Null
    Write-Err "Reverted. Paste the TS error and I will one-pass it."
    exit 1
}
Write-Ok "tsc clean for the three pages."

if (-not $NoTest) {
    Write-Info "Re-running the two architecture guards ..."
    $vt = & npx vitest run src/test/architecture/noRawErrorStrings.test.ts src/test/architecture/noRawStandardElements.test.ts 2>&1
    $vt | Out-Host
    $failed = @($vt | Select-String -Pattern '(\d+) failed').Matches
    if (($vt -join "`n") -match '(\d+)\s+failed' -and [int]$Matches[1] -gt 0) {
        $ErrorActionPreference = $saveEap; Pop-Location
        Write-Err "Guards still RED - auto-reverting. Paste the offender list."
        & $PSCommandPath -Revert | Out-Null
        exit 1
    }
    Write-Ok "PPIQ-T09 + PPIQ-T11 GREEN."
}
$ErrorActionPreference = $saveEap; Pop-Location
Write-Ok "Design-system compliance fix applied and verified."
