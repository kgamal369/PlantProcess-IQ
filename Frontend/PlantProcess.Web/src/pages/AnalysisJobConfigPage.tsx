// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/AnalysisJobConfigPage.tsx
// M1-05 Surface-3: analysis-job DEFINITION over LIVE canonical data.
// Define (defect type from live quality events + parameter + window +
// declared-scope population filters) -> SAVE as a definition (tenant data)
// -> RUN (ReadinessGate-governed learning + deterministic compute) ->
// results_v2 rows TIED to the definition + honest readiness/limitation notes.
// 100% generic: every selectable value comes from the live tenant database.
// PPIQ-T11: tables use StandardTable; buttons use StandardPageButton.
// ============================================================
import { useCallback, useEffect, useState, type CSSProperties } from "react";
import { productApi } from "../api/productApiClient";
import type {
  AnalysisJobDefinitionOptions,
  AnalysisJobDefinitionRow,
  AnalysisJobRunResponse,
  AnalysisJobResultsResponse,
  AnalysisJobResultRow,
  RuleCorrelationRunResponse,
  RuleCorrelationBucketRow,
} from "../api/product-core/shared-types";
import { StandardPageButton } from "@/components/standard/StandardPageCompat";
import { StandardTable } from "@/components/standard/StandardTable";

interface FilterRow {
  k: string;
  v: string;
}

function describeError(e: unknown): string {
  if (e instanceof Error) return e.message;
  return String(e);
}

function parseRuleJson(ruleJson: string): {
  windowDays: number;
  populationFilters: FilterRow[];
  engineOutcomeKey: string;
  engineJobCode: string;
} {
  try {
    const parsed = JSON.parse(ruleJson) as Record<string, unknown>;
    const filters: FilterRow[] = [];
    const pf = parsed["populationFilters"];
    if (pf && typeof pf === "object") {
      for (const [k, v] of Object.entries(pf as Record<string, unknown>)) {
        filters.push({ k, v: String(v) });
      }
    }
    return {
      windowDays: typeof parsed["windowDays"] === "number" ? (parsed["windowDays"] as number) : 30,
      populationFilters: filters,
      engineOutcomeKey:
        typeof parsed["engineOutcomeKey"] === "string" ? (parsed["engineOutcomeKey"] as string) : "defect.rate_per_m2",
      engineJobCode:
        typeof parsed["engineJobCode"] === "string" ? (parsed["engineJobCode"] as string) : "ML_PROCESS_VS_DEFECT",
    };
  } catch {
    return {
      windowDays: 30,
      populationFilters: [],
      engineOutcomeKey: "defect.rate_per_m2",
      engineJobCode: "ML_PROCESS_VS_DEFECT",
    };
  }
}

const panelStyle: CSSProperties = {
  border: "1px solid rgba(128,128,128,0.35)",
  borderRadius: 6,
  padding: 14,
  marginBottom: 16,
};

function fmt(n: number | null): string {
  return n === null || n === undefined ? "-" : String(n);
}

export default function AnalysisJobConfigPage() {
  const [options, setOptions] = useState<AnalysisJobDefinitionOptions | null>(null);
  const [definitions, setDefinitions] = useState<AnalysisJobDefinitionRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [loadingResults, setLoadingResults] = useState(false);

  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [defectType, setDefectType] = useState("");
  const [parameterCode, setParameterCode] = useState("");
  const [windowDays, setWindowDays] = useState(30);
  const [filters, setFilters] = useState<FilterRow[]>([]);
  const [engineOutcomeKey, setEngineOutcomeKey] = useState("defect.rate_per_m2");
  const [engineJobCode, setEngineJobCode] = useState("ML_PROCESS_VS_DEFECT");
  const [description, setDescription] = useState("");

  const [runResponse, setRunResponse] = useState<AnalysisJobRunResponse | null>(null);
  const [ruleResponse, setRuleResponse] = useState<RuleCorrelationRunResponse | null>(null);
  const [results, setResults] = useState<AnalysisJobResultsResponse | null>(null);

  const loadDefinitions = useCallback(async () => {
    try {
      const list = await productApi.listAnalysisJobDefinitions();
      setDefinitions(list?.rows ?? []);
    } catch (e: unknown) {
      setError(describeError(e));
    }
  }, []);

  useEffect(() => {
    productApi
      .getAnalysisJobDefinitionOptions()
      .then((o) => setOptions(o))
      .catch((e: unknown) => setError(describeError(e)));
    void loadDefinitions();
  }, [loadDefinitions]);

  const resetForm = useCallback(() => {
    setEditingCode(null);
    setName("");
    setCode("");
    setDefectType("");
    setParameterCode("");
    setWindowDays(30);
    setFilters([]);
    setEngineOutcomeKey("defect.rate_per_m2");
    setEngineJobCode("ML_PROCESS_VS_DEFECT");
    setDescription("");
  }, []);

  const startEdit = useCallback((row: AnalysisJobDefinitionRow) => {
    const rule = parseRuleJson(row.ruleJson);
    setEditingCode(row.code);
    setName(row.name);
    setCode(row.code);
    setDefectType(row.defectType ?? "");
    setParameterCode(row.parameterCode ?? "");
    setWindowDays(rule.windowDays);
    setFilters(rule.populationFilters);
    setEngineOutcomeKey(rule.engineOutcomeKey);
    setEngineJobCode(rule.engineJobCode);
    setDescription(row.description ?? "");
    setInfo("Editing '" + row.code + "'. Save, then Run to recompute on the edited definition.");
  }, []);

  const saveDefinition = useCallback(async () => {
    setError(null);
    setInfo(null);
    setSaving(true);
    try {
      const populationFilters: Record<string, string> = {};
      for (const f of filters) {
        if (f.k.trim().length > 0) populationFilters[f.k.trim()] = f.v;
      }
      if (editingCode) {
        await productApi.updateAnalysisJobDefinition(editingCode, {
          name,
          defectType,
          parameterCode: parameterCode || null,
          windowDays,
          populationFilters,
          engineOutcomeKey,
          engineJobCode,
          description: description || null,
        });
        setInfo("Definition '" + editingCode + "' updated. Run it to recompute.");
      } else {
        const created = await productApi.createAnalysisJobDefinition({
          code,
          name,
          defectType,
          parameterCode: parameterCode || null,
          windowDays,
          populationFilters,
          engineOutcomeKey,
          engineJobCode,
          description: description || null,
        });
        setEditingCode(created.code);
        setCode(created.code);
        setInfo("Definition '" + created.code + "' saved and linked to the engine run. You can Run it now.");
      }
      await loadDefinitions();
    } catch (e: unknown) {
      setError(describeError(e));
    } finally {
      setSaving(false);
    }
  }, [
    editingCode, code, name, defectType, parameterCode, windowDays, filters,
    engineOutcomeKey, engineJobCode, description, loadDefinitions,
  ]);

  const runDefinition = useCallback(
    async (defCode: string) => {
      setError(null);
      setRunning(true);
      setRunResponse(null);
      setRuleResponse(null);
      setResults(null);
      try {
        const run = await productApi.runAnalysisJobDefinition(defCode, {});
        setRunResponse(run);

        const def = await productApi.getAnalysisJobDefinition(defCode);
        if (def.parameterCode && def.defectType) {
          const rule = parseRuleJson(def.ruleJson);
          const fromUtc = new Date(Date.now() - rule.windowDays * 24 * 3600 * 1000).toISOString();
          try {
            const rr = await productApi.runAnalysisRuleCorrelation({
              parameterCode: def.parameterCode,
              defectType: def.defectType,
              fromUtc,
              toUtc: new Date().toISOString(),
            });
            setRuleResponse(rr);
          } catch (ruleErr: unknown) {
            setInfo("Rule-based pass unavailable: " + describeError(ruleErr));
          }
        }

        await loadDefinitions();
      } catch (e: unknown) {
        setError(describeError(e));
      } finally {
        setRunning(false);
      }
    },
    [loadDefinitions],
  );

  const loadResults = useCallback(async (defCode: string) => {
    setError(null);
    setLoadingResults(true);
    try {
      const r = await productApi.getAnalysisJobDefinitionResults(defCode);
      setResults(r);
    } catch (e: unknown) {
      setError(describeError(e));
    } finally {
      setLoadingResults(false);
    }
  }, []);

  const readinessColor =
    runResponse && runResponse.readinessStatus === "Ready" ? "#3fae6a" : "#d9a53f";

  return (
    <div style={{ padding: 20, maxWidth: 1180 }}>
      <h1 style={{ marginBottom: 4 }}>Analysis Job Configuration</h1>
      <p style={{ opacity: 0.8, marginTop: 0 }}>
        Surface 3: define a correlation / ML analysis job over your live data, save it as a
        definition, and run it through the governed engine. Suspected contributors, never
        guaranteed root cause.
      </p>
      {options && (
        <p style={{ opacity: 0.7, fontSize: 13 }}>
          Live data window: {options.dataWindow.minObservedAtUtc ?? "n/a"} to {options.dataWindow.maxObservedAtUtc ?? "n/a"} ({options.dataWindow.observationCount} parameter observations)
        </p>
      )}

      {error && (
        <div style={{ ...panelStyle, borderColor: "#c0504d", color: "#c0504d" }}>{error}</div>
      )}
      {info && !error && (
        <div style={{ ...panelStyle, borderColor: "#3f6fae" }}>{info}</div>
      )}

      {/* ---- Saved definitions ---- */}
      <div style={panelStyle}>
        <h2 style={{ marginTop: 0 }}>Saved definitions</h2>
        {definitions.length === 0 && (
          <p style={{ opacity: 0.7 }}>
            No analysis-job definitions yet. Create the first one below - it is saved as
            tenant data and drives the engine run.
          </p>
        )}
        {definitions.length > 0 && (
          <StandardTable<AnalysisJobDefinitionRow>
            data={definitions}
            getRowKey={(row) => row.id}
            columns={[
              { key: "code", header: "Code", cell: (r) => r.code },
              { key: "name", header: "Name", cell: (r) => r.name },
              { key: "defect", header: "Defect", cell: (r) => r.defectType ?? "-" },
              { key: "param", header: "Parameter", cell: (r) => r.parameterCode ?? "-" },
              { key: "lastRun", header: "Last run", cell: (r) => r.lastRunAtUtc ?? "never" },
              { key: "status", header: "Status", cell: (r) => r.lastRunStatus ?? "-" },
              {
                key: "actions",
                header: "Actions",
                cell: (r) => (
                  <span style={{ display: "inline-flex", gap: 6 }}>
                    <StandardPageButton type="button" onClick={() => startEdit(r)}>
                      Edit
                    </StandardPageButton>
                    <StandardPageButton
                      type="button"
                      isDisabled={running}
                      onClick={() => void runDefinition(r.code)}
                    >
                      Run
                    </StandardPageButton>
                    <StandardPageButton
                      type="button"
                      isDisabled={loadingResults}
                      onClick={() => void loadResults(r.code)}
                    >
                      Results
                    </StandardPageButton>
                  </span>
                ),
              },
            ]}
          />
        )}
      </div>

      {/* ---- Define / edit ---- */}
      <div style={panelStyle}>
        <h2 style={{ marginTop: 0 }}>{editingCode ? "Edit definition " + editingCode : "New definition"}</h2>
        <div style={{ display: "grid", gridTemplateColumns: "220px 1fr", rowGap: 10, columnGap: 12, alignItems: "center" }}>
          <label>Name</label>
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Defect drivers last 30 days" />

          <label>Code</label>
          <input
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            placeholder="e.g. DEFECT_DRIVERS_30D"
            disabled={editingCode !== null}
          />

          <label>Outcome (defect type, live)</label>
          <select value={defectType} onChange={(e) => setDefectType(e.target.value)}>
            <option value="">-- select from live quality events --</option>
            {(options?.defectTypes ?? []).map((d) => (
              <option key={d.eventType} value={d.eventType}>
                {d.eventType} ({d.eventCount})
              </option>
            ))}
          </select>

          <label>Process parameter (live)</label>
          <select value={parameterCode} onChange={(e) => setParameterCode(e.target.value)}>
            <option value="">-- optional: parameter for the rule-based pass --</option>
            {(options?.parameters ?? []).map((p) => (
              <option key={p.parameterCode} value={p.parameterCode}>
                {p.parameterCode} - {p.parameterName} ({p.observationCount})
              </option>
            ))}
          </select>

          <label>Window (days)</label>
          <input
            type="number"
            min={1}
            max={3650}
            value={windowDays}
            onChange={(e) => setWindowDays(Number(e.target.value))}
            style={{ width: 120 }}
          />

          <label>Engine outcome key</label>
          <select value={engineOutcomeKey} onChange={(e) => setEngineOutcomeKey(e.target.value)}>
            {(options?.engineOutcomes ?? []).map((o) => (
              <option key={o.outcomeKey} value={o.outcomeKey}>
                {o.outcomeKey} - {o.displayName}
              </option>
            ))}
          </select>

          <label>Governed learning job</label>
          <select value={engineJobCode} onChange={(e) => setEngineJobCode(e.target.value)}>
            {(options?.engineJobs ?? []).map((j) => (
              <option key={j.jobCode} value={j.jobCode}>
                {j.jobCode} - {j.jobName}
              </option>
            ))}
          </select>

          <label>Description</label>
          <input value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>

        <h3>Population filters (declared scope)</h3>
        <p style={{ opacity: 0.75, fontSize: 13 }}>
          {options?.populationFilterNote ??
            "Population filters are saved as declared scope on the definition; engine-level population filtering ships with the M2 generic projector."}
        </p>
        {filters.map((f, i) => (
          <div key={i} style={{ display: "flex", gap: 8, marginBottom: 6 }}>
            <input
              value={f.k}
              placeholder="key (e.g. grade)"
              onChange={(e) => {
                const next = filters.slice();
                next[i] = { k: e.target.value, v: next[i].v };
                setFilters(next);
              }}
            />
            <input
              value={f.v}
              placeholder="value (e.g. S355J2)"
              onChange={(e) => {
                const next = filters.slice();
                next[i] = { k: next[i].k, v: e.target.value };
                setFilters(next);
              }}
            />
            <StandardPageButton type="button" onClick={() => setFilters(filters.filter((_, j) => j !== i))}>
              Remove
            </StandardPageButton>
          </div>
        ))}
        <div style={{ display: "flex", gap: 8, marginTop: 10 }}>
          <StandardPageButton type="button" onClick={() => setFilters([...filters, { k: "", v: "" }])}>
            Add filter
          </StandardPageButton>
          <StandardPageButton
            type="button"
            isLoading={saving}
            isDisabled={saving || name.trim() === "" || defectType === "" || (editingCode === null && code.trim() === "")}
            onClick={() => void saveDefinition()}
          >
            {editingCode ? "Save changes" : "Save definition"}
          </StandardPageButton>
          {editingCode && (
            <StandardPageButton type="button" onClick={resetForm}>
              New definition
            </StandardPageButton>
          )}
        </div>
      </div>

      {/* ---- Run outcome ---- */}
      {runResponse && (
        <div style={panelStyle}>
          <h2 style={{ marginTop: 0 }}>Run outcome for {runResponse.code}</h2>
          <p>
            <strong>ReadinessGate:</strong>{" "}
            <span style={{ color: readinessColor }}>{runResponse.readinessStatus}</span>
            {" - "}
            {runResponse.readinessReason}
          </p>
          <p>
            Governed learning ({runResponse.learningJobCode}): {runResponse.learningStatus}, results:{" "}
            {runResponse.learningResultCount}
            {runResponse.learningRunId ? ", run " + runResponse.learningRunId : ""}
          </p>
          <p>
            Deterministic compute ({runResponse.computeEngineKey}, outcome {runResponse.engineOutcomeKey}):{" "}
            {runResponse.computeStatus}, results: {runResponse.computeResultCount}
            {runResponse.computeRunId ? ", tied run " + runResponse.computeRunId : ""}
          </p>
          <p style={{ opacity: 0.75, fontSize: 13 }}>{runResponse.computeMessage}</p>
          <p style={{ opacity: 0.75, fontSize: 13 }}>{runResponse.populationFilterNote}</p>
          <p style={{ fontStyle: "italic" }}>{runResponse.honestPositioning}</p>
        </div>
      )}

      {/* ---- Rule-based pass ---- */}
      {ruleResponse && (
        <div style={panelStyle}>
          <h2 style={{ marginTop: 0 }}>
            Rule-based pass: {ruleResponse.parameterCode} vs {ruleResponse.defectType}
          </h2>
          <p>
            Strength: {ruleResponse.ruleStrength} - {ruleResponse.interpretation}
          </p>
          {ruleResponse.buckets.length > 0 ? (
            <StandardTable<RuleCorrelationBucketRow>
              data={ruleResponse.buckets}
              getRowKey={(b) => String(b.bucketNumber)}
              columns={[
                { key: "bucket", header: "Bucket", cell: (b) => b.bucketNumber },
                { key: "materials", header: "Materials", cell: (b) => b.materialCount },
                { key: "defects", header: "Defects", cell: (b) => b.defectCount },
                { key: "rate", header: "Rate %", cell: (b) => b.defectRatePct },
                { key: "min", header: "Min", cell: (b) => fmt(b.minValue) },
                { key: "max", header: "Max", cell: (b) => fmt(b.maxValue) },
                { key: "avg", header: "Avg", cell: (b) => fmt(b.avgValue) },
              ]}
            />
          ) : (
            <p style={{ opacity: 0.7 }}>
              No parameter observations matched the definition scope in the selected window.
            </p>
          )}
        </div>
      )}

      {/* ---- Tied engine results (results_v2) ---- */}
      {results && (
        <div style={panelStyle}>
          <h2 style={{ marginTop: 0 }}>
            Engine results tied to {results.code}
            {results.computeRunId ? " (run " + results.computeRunId + ")" : ""}
          </h2>
          {results.count === 0 ? (
            <p style={{ opacity: 0.7 }}>{results.message ?? "No tied results yet."}</p>
          ) : (
            <StandardTable<AnalysisJobResultRow>
              data={results.results}
              getRowKey={(r) => r.id}
              columns={[
                { key: "feature", header: "Feature", cell: (r) => r.feature_key },
                { key: "method", header: "Method", cell: (r) => r.method },
                { key: "effect", header: "Effect size", cell: (r) => fmt(r.effect_size) },
                { key: "q", header: "q-value", cell: (r) => fmt(r.q_value) },
                { key: "p", header: "p-value", cell: (r) => fmt(r.p_value) },
                { key: "n", header: "n", cell: (r) => r.sample_size },
                { key: "stable", header: "Stable", cell: (r) => (r.is_stable === null ? "-" : String(r.is_stable)) },
              ]}
            />
          )}
          <p style={{ fontStyle: "italic", marginTop: 10 }}>{results.honestPositioning}</p>
        </div>
      )}
    </div>
  );
}