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
        isLoading={isLoading}
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