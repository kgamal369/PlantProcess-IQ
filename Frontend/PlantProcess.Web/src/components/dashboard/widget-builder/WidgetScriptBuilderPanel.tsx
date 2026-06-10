import { useMemo, useState } from "react";
import { StandardButton, StandardCard } from "@/components/standard";
import { LicenseGate } from "@/components/license/LicenseGate";
import { apiClient } from "@/api/http";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Table, StandardP2TextArea } from "@/components/standard/StandardP2Controls";
interface WidgetColumn { name?: string; key?: string; label?: string }
interface WidgetExecResult {
  generatedAtUtc?: string;
  widget?: Record<string, unknown>;
  columns?: WidgetColumn[];
  rows?: Record<string, unknown>[];
  warnings?: string[];
}

const DOCTRINE_EXAMPLE = "source=v_ppiq_p04_completion_golden_thread\nmeasure=count(*)\ngroup_by=shift\nlimit=200";
const WIDGET_SCRIPT_FEATURE = "WidgetScriptLayer";

function colName(c: WidgetColumn): string { return c.name ?? c.key ?? c.label ?? ""; }
function perfEstimate(rows: number, cols: number): { label: string; tone: string; detail: string } {
  const cells = rows * Math.max(cols, 1);
  if (cells < 2000) return { label: "Light", tone: "#2CE6A2", detail: `${rows} rows x ${cols} cols (preview sample)` };
  if (cells < 50000) return { label: "Moderate", tone: "#FFD166", detail: `${rows} rows x ${cols} cols (preview sample)` };
  return { label: "Heavy", tone: "#FF4D6D", detail: `${rows} rows x ${cols} cols (preview sample) â€” consider tighter filters` };
}

export function WidgetScriptBuilderPanel({ onPublish }: { onPublish?: (widget: Record<string, unknown>) => void }) {
  const [script, setScript] = useState(DOCTRINE_EXAMPLE);
  const [result, setResult] = useState<WidgetExecResult | null>(null);
  const [validated, setValidated] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<null | "validate" | "preview" | "publish">(null);
  const [published, setPublished] = useState(false);
  const [showSql, setShowSql] = useState(false);

  const run = async (): Promise<void> => {
    setError(null);
    const res = await apiClient.post<WidgetExecResult>(
      "/analytics/dashboard/widgets/execute", { expression: script }, { suppressToast: true },
    );
    setResult(res);
  };
  const onValidate = async () => {
    setBusy("validate"); setPublished(false);
    try { await run(); setValidated(true); } catch (e) { setValidated(false); setError(e instanceof Error ? e.message : "Validation failed."); } finally { setBusy(null); }
  };
  const onPreview = async () => {
    setBusy("preview"); setPublished(false);
    try { await run(); setValidated(true); } catch (e) { setValidated(false); setError(e instanceof Error ? e.message : "Preview failed."); } finally { setBusy(null); }
  };
  const onPublishClick = async () => {
    if (!validated || !result?.widget) return;
    setBusy("publish");
    try { onPublish?.(result.widget); setPublished(true); } finally { setBusy(null); }
  };

  const cols = result?.columns?.map(colName).filter(Boolean) ?? [];
  const rows = result?.rows ?? [];
  const perf = useMemo(() => perfEstimate(rows.length, cols.length), [rows.length, cols.length]);
  const compiledSql = useMemo(() => {
    const w = result?.widget as Record<string, unknown> | undefined;
    if (!w) return "";
    const sql = (w.sql ?? w.resolvedSql ?? w.querySql ?? w.compiledSql) as string | undefined;
    return sql ?? JSON.stringify(w, null, 2);
  }, [result]);

  return (
    <LicenseGate
      feature={WIDGET_SCRIPT_FEATURE}
      fallbackTitle="Widget Script is a ProPlus feature"
      fallbackDescription="Compiling widget scripts (group_by, aggregates, having) into safe queries requires a ProPlus license."
    >
      <StandardCard
        eyebrow="Widget Builder Â· PPIQ-WF-012/013/014"
        title="Widget Script Studio"
        subtitle="Compile a widget script into a safe, parameterized, read-only query. Validate, preview (row-limited) and review a cost estimate before publishing."
        actions={
          <div>
            <StandardButton variant="ghost" type="button" onClick={() => setShowSql((s) => !s)}>
              {showSql ? "Show script" : "Show compiled query"}
            </StandardButton>
            <StandardButton variant="ghost" type="button" onClick={onValidate} isDisabled={busy !== null}>
              {busy === "validate" ? "Validating..." : "Validate"}
            </StandardButton>
            <StandardButton variant="ghost" type="button" onClick={onPreview} isDisabled={busy !== null}>
              {busy === "preview" ? "Previewing..." : "Preview"}
            </StandardButton>
            <StandardButton variant="primary" type="button" onClick={onPublishClick} isDisabled={!validated || busy !== null}
              title={validated ? "Publish the validated widget" : "Validate before publishing"}>
              {busy === "publish" ? "Publishing..." : "Publish"}
            </StandardButton>
          </div>
        }
      >
        {!showSql ? (
          <StandardP2TextArea value={script} spellCheck={false} rows={7}
            onChange={(e) => { setScript(e.target.value); setValidated(false); setPublished(false); }} />
        ) : (
          <pre>
            {compiledSql || "Run Validate or Preview to see the compiled query."}
          </pre>
        )}

        <div>
          {validated && !error && <span>Validated</span>}
          {error && <span>{error}</span>}
          {published && <span>Published</span>}
        </div>

        {result && !error && (
          <div>
            <div>
              <Metric label="Rows (preview)" value={String(rows.length)} />
              <Metric label="Columns" value={String(cols.length)} />
              <Metric label="Est. cost" value={perf.label} tone={perf.tone} sub={perf.detail} />
            </div>
            {result.warnings && result.warnings.length > 0 && (
              <div>
                {result.warnings.map((w, i) => <div key={i}>{w}</div>)}
              </div>
            )}
            {cols.length > 0 && (
              <div>
                <StandardP2Table>
                  <thead><tr>{cols.map((c) => (
                    <th key={c}>{c}</th>
                  ))}</tr></thead>
                  <tbody>{rows.slice(0, 20).map((r, i) => (
                    <tr key={i}>{cols.map((c) => (
                      <td key={c}>
                        {r[c] === null || r[c] === undefined ? "" : String(r[c])}
                      </td>
                    ))}</tr>
                  ))}</tbody>
                </StandardP2Table>
              </div>
            )}
          </div>
        )}
      </StandardCard>
    </LicenseGate>
  );
}

function Metric({ label, value, tone, sub }: { label: string; value: string; tone?: string; sub?: string }) {
  return (
    <div>
      <div>{label}</div>
      <div>{value}</div>
      {sub && <div>{sub}</div>}
    </div>
  );
}