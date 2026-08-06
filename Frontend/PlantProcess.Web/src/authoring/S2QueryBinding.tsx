// PPIQ T-038 pack 02b. THE S2 FACE.
//
// His ruling, implemented literally:
//
//   canonical catalogue -> Query Expression editor -> executeWidgetQueryExpression
//   -> Run test -> returned columns -> Axis / Value / Series -> save
//
// NOT the staged catalogue and NOT the preparation SQL endpoint, and the
// expression is never called SQL. It is the existing widget query expression contract, parsed and
// compiled by IWidgetQueryExpressionService, and it carries its own safety
// rules. Routing S2 at the preparation path would change the data boundary and
// the safety contract in one move.
//
// This component decides nothing about the definition: every field it edits is
// handed back through onChange, and the shell owns the title, the save and the
// Job Log. That is what lets the same face serve Add and Edit in pack 03
// without a second copy of anything.
//
// THE DEFECT THIS CLOSES, measured in the shipped code before it was written.
// The surface T-038 retires offered the returned columns' LABELS as the role
// choices and stored a label. SavedDashboardWidget resolves the saved binding
// against
// the columns' CODES. Unless the two are identical for every column, every
// query-bound widget reads as stale at render time and the chart silently falls
// back to inference, which is the exact failure the role binding was written to
// prevent. Roles here bind to the CODE, which is also what Chapter 4 section
// 5.1.11 step 3 calls the returned column's name. A widget bound under the old
// behaviour is reported stale BY NAME and re-mapped by the author, never
// repointed for him.

import { useCallback, useEffect, useState } from "react";
import {
  StandardP2Button, StandardP2Input, StandardP2Select,
  StandardP2Table, StandardP2TextArea,
} from "@/components/standard/StandardP2Controls";
import { dashboardingApi, type WidgetQueryColumn } from "@/api/dashboarding/dashboarding.api";
import { staleRoles } from "@/api/product-core/widget-role-binding";
import { RoleBindingFields } from "./RoleBindingFields";
import { describeStaleBinding } from "./roleBindingPresentation";
import {
  describeAmbiguousResolution, describeLegacyResolution, normaliseRoleBinding,
} from "./roleBindingCompat";
import { describeThrownAction } from "./previewReport";
import {
  chartCapabilities, requiresParameter,
  type S2AuthoringState, type WidgetFilterRow,
} from "./widgetDefinitionModel";
import "./s2-query-binding.css";

// RULE 1: every list on this face is published by the server. Nothing below is
// a plant word, a chart word or a filter word written into the product.
interface MetaChartType {
  code: string; label: string; category: string;
  supportsDimension: boolean; supportsMeasure: boolean;
}
interface MetaField {
  code: string; label: string; unit?: string | null;
  compatibleChartTypes: string[]; requiresParameterCode: boolean;
}
interface MetaFilter { code: string; label: string; sourceCatalog?: string | null }
export interface S2Metadata {
  chartTypes: MetaChartType[];
  dimensions: MetaField[];
  measures: MetaField[];
  filters: MetaFilter[];
}

type RefItem = { code?: string; id?: string; label?: string; name?: string };
type RefData = Record<string, RefItem[] | string | undefined>;

interface RunResult {
  columns: WidgetQueryColumn[];
  rows: Record<string, unknown>[];
  warnings: string[];
}

export type S2LogSeverity = "success" | "warning" | "error";

export interface S2QueryBindingProps {
  state: S2AuthoringState;
  onChange: (next: S2AuthoringState) => void;
  /** Section 5.2.8: the Job Log is the authoritative surface. Never a toast. */
  onLog: (severity: S2LogSeverity, message: string, facts?: string) => void;
  /**
   * T-038 pack 03a. The shell compiles the save payload and needs the same
   * catalogue this face reads - the chart type's category, and which fields
   * declare that they require a parameter. Reported upward from the ONE fetch
   * rather than requested a second time.
   */
  onCatalogue?: (catalogue: S2Metadata) => void;
}

function itemCode(i: RefItem) { return String(i.code ?? i.id ?? ""); }
function itemLabel(i: RefItem) { return String(i.label ?? i.name ?? i.code ?? i.id ?? ""); }

function sampleOf(rows: readonly Record<string, unknown>[], code: string): string {
  for (const row of rows) {
    const v = row[code];
    if (v !== null && v !== undefined && String(v) !== "") { return String(v); }
  }
  return "no rows to sample";
}

export function S2QueryBinding({ state, onChange, onLog, onCatalogue }: S2QueryBindingProps) {
  const [meta, setMeta] = useState<S2Metadata | null>(null);
  const [refData, setRefData] = useState<RefData | null>(null);
  const [result, setResult] = useState<RunResult | null>(null);
  const [running, setRunning] = useState(false);

  useEffect(() => {
    let alive = true;
    Promise.all([
      dashboardingApi.getDashboardMetadata() as Promise<S2Metadata>,
      dashboardingApi.getDashboardReferenceData() as Promise<RefData>,
    ]).then(([m, r]) => {
      if (!alive) { return; }
      setMeta(m);
      setRefData(r);
      if (onCatalogue) { onCatalogue(m); }
    }).catch(() => {
      if (!alive) { return; }
      onLog("error",
        "The widget catalogue did not answer, so the chart, dimension, measure and filter lists are empty."
        + " Check that the API is running, then reopen this widget.");
    });
    return () => { alive = false; };
    // onLog is stable per the shell's debug log, and re-running this effect on
    // its own output is the hazard recorded in the shell.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const set = useCallback((patch: Partial<S2AuthoringState>) => {
    onChange({ ...state, ...patch });
  }, [state, onChange]);

  const catalogFor = useCallback((name?: string | null): RefItem[] | null => {
    if (!name || !refData) { return null; }
    const v = refData[name];
    return Array.isArray(v) ? v : null;
  }, [refData]);

  const chartTypes = meta?.chartTypes ?? [];
  const caps = chartCapabilities(chartTypes, state.chartType);
  const dimensions = (meta?.dimensions ?? []).filter(
    (d) => !state.chartType || d.compatibleChartTypes.indexOf(state.chartType) >= 0);
  const measures = (meta?.measures ?? []).filter(
    (m) => !state.chartType || m.compatibleChartTypes.indexOf(state.chartType) >= 0);
  const needsParameter = requiresParameter(
    meta?.dimensions ?? [], meta?.measures ?? [], state.dimensionCode, state.measureCode);

  // The role choices are the returned column CODES. See the defect note above.
  const returnedCodes = (result?.columns ?? []).map((c) => c.code);

  const setFilters = (rows: WidgetFilterRow[]) => set({ filters: rows });

  const runTest = useCallback(async () => {
    if (!state.expression.trim()) {
      onLog("error", "Write a query expression first, then run the test.");
      return;
    }
    setRunning(true);
    try {
      const r = await dashboardingApi.executeWidgetQueryExpression({
        expression: state.expression, filters: null, options: null,
      });
      const run: RunResult = {
        columns: Array.isArray(r?.columns) ? r.columns : [],
        rows: Array.isArray(r?.rows) ? r.rows : [],
        warnings: Array.isArray(r?.warnings) ? r.warnings.map((w) => String(w)) : [],
      };
      setResult(run);
      const codes = run.columns.map((c) => c.code);
      const facts = run.rows.length + " rows | " + codes.length + " columns: " + codes.join(", ");
      if (codes.length === 0) {
        onLog("warning",
          "The expression ran but described no columns, so there is nothing to map to a chart role."
          + " Add a dimension or a measure to the expression, then run it again.");
      } else if (run.rows.length === 0) {
        // The zero-row contract: state what happened and what to check, and
        // never claim a cause the server has no evidence for.
        onLog("warning",
          "The expression ran and returned 0 rows. Review the active filters or confirm"
          + " that the selected source contains matching rows.", facts);
      } else {
        onLog("success", "The expression ran.", facts);
      }
      for (const w of run.warnings) { onLog("warning", w); }

      // THE LEGACY ADAPTER, applied at the one boundary where returned columns
      // exist. A token stored as a label by the retiring panel is rewritten to
      // its column code when exactly one label matches, and the rewrite is
      // stated rather than done quietly. Everything else is left exactly as it
      // was found, so an ambiguous or a genuinely missing token still reaches
      // the author as a named remap.
      const norm = normaliseRoleBinding(state.roleBinding, run.columns);
      if (norm.resolved.length > 0) {
        set({ roleBinding: norm.binding });
        onLog("warning", describeLegacyResolution(norm.resolved));
      }
      if (norm.ambiguous.length > 0) {
        onLog("warning", describeAmbiguousResolution(norm.ambiguous));
      }
      // Section 5.2.8 wants the finding in the log too, not only beside the
      // control, and it names the column either way.
      if (staleRoles(norm.binding, codes).length > 0) {
        onLog("warning", describeStaleBinding(norm.binding, codes));
      }
    } catch (e) {
      // The thrown value is never read and never shown.
      onLog("error", describeThrownAction(e));
    } finally {
      setRunning(false);
    }
  }, [state, set, onLog]);

  return (
    <div className="ppiq-s2" data-testid="s2-query-binding">
      {/* The binding toggle the shipped surface already has, carried across
          rather than invented. Chapter 4 section 5.1.11: catalogue binding is
          the simple path, the authored query is the general one. */}
      <div className="ppiq-s2__modes" data-testid="s2-bind-mode">
        <StandardP2Button
          variant={state.bindMode === "catalogue" ? "primary" : "ghost"}
          onClick={() => set({ bindMode: "catalogue" })}
        >
          Catalogue
        </StandardP2Button>
        <StandardP2Button
          variant={state.bindMode === "query" ? "primary" : "ghost"}
          onClick={() => set({ bindMode: "query" })}
        >
          Query Expression
        </StandardP2Button>
      </div>

      <div className="ppiq-s2__grid" hidden={state.bindMode === "query"}>
        <div className="ppiq-s2__field">
          <span className="ppiq-s2__label">Chart type</span>
          <StandardP2Select aria-label="Chart type" value={state.chartType}
            onChange={(e) => set({ chartType: e.target.value })}>
            <option value="">choose...</option>
            {chartTypes.map((c) => <option key={c.code} value={c.code}>{c.label}</option>)}
          </StandardP2Select>
        </div>

        {caps.usesDimension && (
          <div className="ppiq-s2__field">
            <span className="ppiq-s2__label">Dimension, optional</span>
            <StandardP2Select aria-label="Dimension" value={state.dimensionCode}
              onChange={(e) => set({ dimensionCode: e.target.value })}>
              <option value="">none</option>
              {dimensions.map((d) => <option key={d.code} value={d.code}>{d.label}</option>)}
            </StandardP2Select>
          </div>
        )}

        {caps.usesMeasure && (
          <div className="ppiq-s2__field">
            <span className="ppiq-s2__label">Measure, optional</span>
            <StandardP2Select aria-label="Measure" value={state.measureCode}
              onChange={(e) => set({ measureCode: e.target.value })}>
              <option value="">choose...</option>
              {measures.map((m) => (
                <option key={m.code} value={m.code}>{m.label}{m.unit ? " (" + m.unit + ")" : ""}</option>
              ))}
            </StandardP2Select>
          </div>
        )}

        {needsParameter && (
          <div className="ppiq-s2__field">
            <span className="ppiq-s2__label">Parameter</span>
            <StandardP2Select aria-label="Parameter" value={state.parameterCode}
              onChange={(e) => set({ parameterCode: e.target.value })}>
              <option value="">choose...</option>
              {(catalogFor("parameters") ?? []).map((p) => (
                <option key={itemCode(p)} value={itemCode(p)}>{itemLabel(p)}</option>
              ))}
            </StandardP2Select>
          </div>
        )}
      </div>

      <div className="ppiq-s2__field" data-testid="s2-filters">
        <div className="ppiq-s2__rowhead">
          <span className="ppiq-s2__label">Filters</span>
          <span className="ppiq-s2__count">{state.filters.length}</span>
          <span className="ppiq-s2__spacer" />
          <StandardP2Button variant="ghost"
            onClick={() => setFilters(state.filters.concat({ code: "", value: "" }))}>
            Add filter
          </StandardP2Button>
        </div>

        {state.filters.map((f, i) => {
          const spec = (meta?.filters ?? []).find((x) => x.code === f.code);
          const catalog = catalogFor(spec?.sourceCatalog);
          return (
            <div className="ppiq-s2__filterrow" key={"s2f" + i}>
              <StandardP2Select aria-label="Filter" value={f.code}
                onChange={(e) => setFilters(state.filters.map(
                  (r, j) => (j === i ? { code: e.target.value, value: "" } : r)))}>
                <option value="">choose...</option>
                {(meta?.filters ?? []).map((x) => (
                  <option key={x.code} value={x.code}>{x.label}</option>
                ))}
              </StandardP2Select>

              {catalog ? (
                <StandardP2Select aria-label="Filter value" value={f.value}
                  onChange={(e) => setFilters(state.filters.map(
                    (r, j) => (j === i ? { code: r.code, value: e.target.value } : r)))}>
                  <option value="">any</option>
                  {catalog.map((c) => (
                    <option key={itemCode(c)} value={itemCode(c)}>{itemLabel(c)}</option>
                  ))}
                </StandardP2Select>
              ) : (
                <StandardP2Input aria-label="Filter value" value={f.value} placeholder="value"
                  onChange={(e) => setFilters(state.filters.map(
                    (r, j) => (j === i ? { code: r.code, value: e.target.value } : r)))} />
              )}

              <StandardP2Button variant="ghost" aria-label="Remove filter"
                onClick={() => setFilters(state.filters.filter((_, j) => j !== i))}>
                Remove
              </StandardP2Button>
            </div>
          );
        })}

        <p className="ppiq-s2__hint">
          A filter saved here is this widget's permanent scope. The page filter bar and a
          click on any other widget apply on top of it, narrowing further inside that
          scope rather than replacing it.
        </p>
      </div>

      {state.bindMode === "query" && (
        <div className="ppiq-s2__field" data-testid="s2-query-mode">
          <span className="ppiq-s2__label">Query expression</span>
          <StandardP2TextArea
            aria-label="Query expression"
            className="ppiq-s2__expression"
            rows={8}
            spellCheck={false}
            value={state.expression}
            onChange={(e) => set({ expression: e.target.value })}
          />
          <p className="ppiq-s2__hint">
            One statement per line, over the canonical model. A measure takes an
            aggregate, a column and an optional alias. Anything the grammar does not
            permit is refused by name rather than guessed at.
          </p>

          <div className="ppiq-s2__rowhead">
            <span className="ppiq-s2__spacer" />
            <StandardP2Button variant="primary" onClick={() => { void runTest(); }} disabled={running}>
              {running ? "Running..." : "Run test"}
            </StandardP2Button>
          </div>

          {result && result.columns.length > 0 && (
            <div className="ppiq-s2__result" data-testid="s2-returned-columns">
              <span className="ppiq-s2__label">Returned columns</span>
              <StandardP2Table className="ppiq-s2__table">
                <thead><tr><th>column</th><th>type</th><th>sample</th></tr></thead>
                <tbody>
                  {result.columns.map((c) => (
                    <tr key={"s2c" + c.code} data-testid={"s2-column-" + c.code}>
                      <td>{c.code}</td>
                      <td>{c.dataType}</td>
                      <td>{sampleOf(result.rows, c.code)}</td>
                    </tr>
                  ))}
                </tbody>
              </StandardP2Table>
            </div>
          )}

          {result && result.columns.length > 0 && (
            <RoleBindingFields
              columns={returnedCodes}
              binding={state.roleBinding}
              onChange={(next) => set({ roleBinding: next })}
            />
          )}
        </div>
      )}
    </div>
  );
}

export default S2QueryBinding;