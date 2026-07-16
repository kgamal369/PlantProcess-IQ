import { useEffect, useState } from "react";
import {
  DataFetchBoundary,
  StandardButton,
  StandardCard,
  StandardInput,
  StandardPageHeader,
  StandardSelect,
  StandardTable,
  type StandardSelectOption,
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

const comparatorOptions: ReadonlyArray<StandardSelectOption> = [">", ">=", "<", "<=", "="].map((value) => ({ value, label: value }));
const severityOptions: ReadonlyArray<StandardSelectOption> = ["Info", "Warning", "Critical"].map((value) => ({ value, label: value }));

const ruleColumns: ReadonlyArray<StandardTableColumn<AlertRule>> = [
  { key: "name", header: "Rule", cell: (row) => row.ruleName },
  { key: "param", header: "Parameter", cell: (row) => row.parameterCode },
  { key: "cond", header: "Condition", cell: (row) => `${row.comparator} ${row.limitValue}` },
  { key: "sev", header: "Severity", cell: (row) => row.severity },
];

const logColumns: ReadonlyArray<StandardTableColumn<PlantDataLogRow>> = [
  { key: "time", header: "Time", cell: (row) => row.loggedAtUtc },
  { key: "rule", header: "Rule", cell: (row) => row.ruleName },
  { key: "mat", header: "Material", cell: (row) => row.materialCode ?? "-" },
  { key: "param", header: "Parameter", cell: (row) => row.parameterCode },
  { key: "val", header: "Value", align: "right", cell: (row) => (row.observedValue ?? "-") as React.ReactNode },
  { key: "cond", header: "Condition", cell: (row) => `${row.comparator} ${row.limitValue}` },
  { key: "sev", header: "Severity", cell: (row) => row.severity },
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
      const [ruleResponse, logResponse] = await Promise.all([listRules(), listLog()]);
      setRules(Array.isArray(ruleResponse) ? ruleResponse : []);
      setLog(Array.isArray(logResponse) ? logResponse : []);
    } catch (caught: unknown) {
      setError(caught);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    Promise.all([listRules(), listLog()])
      .then(([ruleResponse, logResponse]) => {
        if (cancelled) return;
        setRules(Array.isArray(ruleResponse) ? ruleResponse : []);
        setLog(Array.isArray(logResponse) ? logResponse : []);
      })
      .catch((caught: unknown) => {
        if (!cancelled) setError(caught);
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
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
      await createRule({
        ruleName: ruleName.trim(),
        parameterCode: parameterCode.trim(),
        comparator,
        limitValue: limit,
        severity,
      });
      setRuleName("");
      setParameterCode("");
      setLimitValue("");
      setNotice("Rule created. Run an evaluation to scan new observations.");
      await load();
    } catch (caught: unknown) {
      setFormError(caught instanceof Error ? caught.message : "Could not create the rule.");
    } finally {
      setBusy(false);
    }
  }

  async function onEvaluate() {
    setFormError(null);
    setBusy(true);
    try {
      const response = await evaluateAlerts();
      setNotice(`Evaluation complete: ${response.logged} new log row(s).`);
      await load();
    } catch (caught: unknown) {
      setFormError(caught instanceof Error ? caught.message : "Could not run evaluation.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="ppiq-alerting">
      <StandardPageHeader
        title="Plant Data Log"
        subtitle="Create governed threshold rules and review breaches against imported observations."
        description="Evaluations are read-only toward plant systems and are idempotent for each rule and observation."
        actions={
          <StandardButton onClick={onEvaluate} isDisabled={busy} isLoading={busy}>
            Run evaluation
          </StandardButton>
        }
      />

      <div className="ppiq-al-stack">
        <StandardCard
          eyebrow="Rule builder"
          title="Create a threshold rule"
          subtitle="Choose a canonical parameter, comparator, limit and operational severity."
          elevation="flat"
        >
          <div className="ppiq-al-form">
            <StandardInput
              label="Rule name"
              value={ruleName}
              placeholder="Superheat high"
              required
              onChange={setRuleName}
            />
            <StandardInput
              label="Parameter code"
              value={parameterCode}
              placeholder="SUPERHEAT_C"
              helperText="Use the imported canonical parameter code."
              required
              onChange={setParameterCode}
            />
            <StandardSelect
              label="Comparator"
              value={comparator}
              options={comparatorOptions}
              onChange={(value) => setComparator(String(value))}
            />
            <StandardInput
              label="Limit"
              value={limitValue}
              placeholder="36"
              inputMode="decimal"
              required
              onChange={setLimitValue}
            />
            <StandardSelect
              label="Severity"
              value={severity}
              options={severityOptions}
              onChange={(value) => setSeverity(String(value))}
            />
            <div className="ppiq-al-form-actions">
              <StandardButton onClick={onCreate} isDisabled={busy}>
                Add rule
              </StandardButton>
            </div>
          </div>

          {notice ? <div className="ppiq-al-notice" role="status">{notice}</div> : null}
          {formError ? <div className="ppiq-al-error" role="alert">{formError}</div> : null}
        </StandardCard>

        <StandardCard
          eyebrow="Active configuration"
          title="Rules"
          subtitle="Rules are evaluated against newly imported canonical observations."
          elevation="flat"
        >
          <div className="ppiq-al-table-wrap">
            <StandardTable
              columns={ruleColumns}
              data={rules}
              getRowKey={(row) => row.id}
              isLoading={isLoading}
              emptyTitle="No rules yet"
              emptyDescription="Create the first rule above."
            />
          </div>
        </StandardCard>

        <StandardCard
          eyebrow="Operational evidence"
          title="Breach log"
          subtitle="Every row identifies the rule, material, parameter, observed value and severity."
          elevation="flat"
        >
          <DataFetchBoundary
            title="Plant data log"
            isLoading={isLoading}
            error={error}
            isEmpty={log.length === 0}
            emptyMessage="No breaches logged yet. Create a rule and run an evaluation."
            onRetry={() => void load()}
          >
            <div className="ppiq-al-table-wrap">
              <StandardTable columns={logColumns} data={log} getRowKey={(row) => row.id} />
            </div>
          </DataFetchBoundary>
        </StandardCard>
      </div>
    </div>
  );
}

export default AlertingPage;
