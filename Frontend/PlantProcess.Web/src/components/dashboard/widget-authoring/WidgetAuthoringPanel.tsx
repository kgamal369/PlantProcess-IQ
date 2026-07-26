// Widget authoring surface. Constitution v3 II.6.7: authoring opens from the
// page the widget lives on, and the same surface serves add and edit.
//
// EVERY LIST HERE COMES FROM THE SERVER. GET /analytics/dashboard/metadata
// publishes dimensions, measures, chart types, filters, purposes and the
// compatibility rules. Repeating any of them as a literal in this file would be
// the Rule 1 violation this panel exists to remove, so there are none.

import { useCallback, useEffect, useMemo, useState } from "react";
import { StandardButton } from "@/components/standard";
import { StandardP2Input, StandardP2Select } from "@/components/standard/StandardP2Controls";
import { dashboardingApi } from "@/api/dashboarding/dashboarding.api";
import "./WidgetAuthoringPanel.css";

type Meta = {
  dimensions: { code: string; label: string; compatibleChartTypes: string[]; requiresParameterCode: boolean }[];
  measures: { code: string; label: string; unit?: string | null; compatibleChartTypes: string[]; requiresParameterCode: boolean }[];
  chartTypes: { code: string; label: string; category: string; supportsDimension: boolean; supportsMeasure: boolean }[];
  filters: { code: string; label: string; dataType: string; operatorMode: string; sourceCatalog?: string | null }[];
  purposes: { code: string; label: string; description: string; recommendedDimensions: string[]; recommendedMeasures: string[]; recommendedChartTypes: string[] }[];
};

type RefItem = { code?: string; id?: string; label?: string; name?: string };
type RefData = Record<string, RefItem[] | string | undefined>;

export type AuthoredWidget = {
  id?: string;
  widgetCode?: string;
  widgetTitle?: string;
  widgetType?: string;
  chartType?: string;
  dimensionCode?: string;
  measureCode?: string;
  parameterCode?: string | null;
  filterJson?: string;
  layoutJson?: string;
  displayOptionsJson?: string;
  sortOrder?: number;
};

export type WidgetAuthoringPanelProps = {
  isOpen: boolean;
  dashboardDefinitionId: string;
  existing?: AuthoredWidget | null;
  onClose: () => void;
  onSaved: () => void | Promise<void>;
};

type FilterRow = { code: string; value: string };

function itemCode(i: RefItem) { return String(i.code ?? i.id ?? ""); }
function itemLabel(i: RefItem) { return String(i.label ?? i.name ?? i.code ?? i.id ?? ""); }

function slug(title: string) {
  const base = title.trim().toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "");
  return (base || "widget") + "_" + Math.random().toString(36).slice(2, 7);
}

function parseFilters(json?: string): FilterRow[] {
  if (!json) { return []; }
  try {
    const obj = JSON.parse(json) as Record<string, unknown>;
    return Object.keys(obj)
      .filter((k) => obj[k] !== null && obj[k] !== undefined && String(obj[k]) !== "")
      .map((k) => ({ code: k, value: String(obj[k]) }));
  } catch { return []; }
}

export function WidgetAuthoringPanel({
  isOpen, dashboardDefinitionId, existing, onClose, onSaved,
}: WidgetAuthoringPanelProps) {
  const [meta, setMeta] = useState<Meta | null>(null);
  const [refData, setRefData] = useState<RefData | null>(null);
  const [problem, setProblem] = useState<string>("");
  const [busy, setBusy] = useState(false);

  const [title, setTitle] = useState("");
  const [purpose, setPurpose] = useState("");
  const [chartType, setChartType] = useState("");
  const [dimensionCode, setDimensionCode] = useState("");
  const [measureCode, setMeasureCode] = useState("");
  const [parameterCode, setParameterCode] = useState("");
  const [filters, setFilters] = useState<FilterRow[]>([]);

  const isEdit = Boolean(existing?.id);

  useEffect(() => {
    if (!isOpen) { return; }
    setProblem("");
    Promise.all([
      dashboardingApi.getDashboardMetadata() as Promise<Meta>,
      dashboardingApi.getDashboardReferenceData() as Promise<RefData>,
    ])
      .then(([m, r]) => { setMeta(m); setRefData(r); })
      .catch((e) => setProblem(e instanceof Error ? e.message : String(e)));
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) { return; }
    setTitle(existing?.widgetTitle ?? "");
    setChartType(existing?.chartType ?? "");
    setDimensionCode(existing?.dimensionCode ?? "");
    setMeasureCode(existing?.measureCode ?? "");
    setParameterCode(existing?.parameterCode ?? "");
    setFilters(parseFilters(existing?.filterJson));
    setPurpose("");
  }, [isOpen, existing]);

  // Compatibility comes from the server too: a dimension or measure declares
  // which chart types it works with, so the lists narrow themselves.
  const dimensions = useMemo(
    () => (meta?.dimensions ?? []).filter((d) => !chartType || d.compatibleChartTypes.indexOf(chartType) >= 0),
    [meta, chartType],
  );
  const measures = useMemo(
    () => (meta?.measures ?? []).filter((m) => !chartType || m.compatibleChartTypes.indexOf(chartType) >= 0),
    [meta, chartType],
  );
  // The server declares per chart type whether it uses a dimension and a
  // measure, so the form follows the catalogue instead of assuming both.
  const chartSpec = useMemo(
    () => (meta?.chartTypes ?? []).find((c) => c.code === chartType) ?? null,
    [meta, chartType],
  );
  const usesDimension = !chartType || (chartSpec?.supportsDimension ?? true);
  const usesMeasure = !chartType || (chartSpec?.supportsMeasure ?? true);

  const needsParameter = useMemo(() => {
    const d = (meta?.dimensions ?? []).find((x) => x.code === dimensionCode);
    const m = (meta?.measures ?? []).find((x) => x.code === measureCode);
    return Boolean(d?.requiresParameterCode || m?.requiresParameterCode);
  }, [meta, dimensionCode, measureCode]);

  const catalogFor = useCallback((name?: string | null) => {
    if (!name || !refData) { return null; }
    const v = refData[name];
    return Array.isArray(v) ? v : null;
  }, [refData]);

  const applyPurpose = (code: string) => {
    setPurpose(code);
    const p = (meta?.purposes ?? []).find((x) => x.code === code);
    if (!p) { return; }
    if (p.recommendedChartTypes[0]) { setChartType(p.recommendedChartTypes[0]); }
    if (p.recommendedDimensions[0]) { setDimensionCode(p.recommendedDimensions[0]); }
    if (p.recommendedMeasures[0]) { setMeasureCode(p.recommendedMeasures[0]); }
  };

  const save = async () => {
    setProblem("");
    if (!title.trim()) { setProblem("Give the widget a title."); return; }
    if (!chartType) { setProblem("Choose a chart type."); return; }
    if (usesMeasure && !measureCode && !dimensionCode) {
      setProblem("Pick a dimension or a measure so the widget has something to show.");
      return;
    }

    const filterObject: Record<string, string> = {};
    for (const f of filters) { if (f.code && f.value) { filterObject[f.code] = f.value; } }

    const payload = {
      widgetCode: existing?.widgetCode ?? slug(title),
      widgetTitle: title.trim(),
      widgetType: existing?.widgetType ?? ((meta?.chartTypes ?? []).find((c) => c.code === chartType)?.category ?? "chart"),
      chartType,
      dimensionCode,
      measureCode,
      parameterCode: needsParameter && parameterCode ? parameterCode : null,
      filterJson: JSON.stringify(filterObject),
      layoutJson: existing?.layoutJson ?? "{}",
      displayOptionsJson: existing?.displayOptionsJson ?? "{}",
      sortOrder: existing?.sortOrder ?? 0,
      isSynthetic: false,
    };

    setBusy(true);
    try {
      if (isEdit && existing?.id) {
        await dashboardingApi.updateDashboardWidgetDefinition(dashboardDefinitionId, existing.id, payload);
      } else {
        await dashboardingApi.createDashboardWidgetDefinition(dashboardDefinitionId, payload);
      }
      await onSaved();
      onClose();
    } catch (e) {
      setProblem(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  if (!isOpen) { return null; }

  return (
    <div className="wauth-backdrop" role="presentation">
      <section className="wauth-panel" role="dialog" aria-modal="true" aria-label={isEdit ? "Edit widget" : "Add widget"} data-testid="widget-authoring-panel">
        <header className="wauth-head">
          <div>
            <p className="wauth-eyebrow">Widget authoring</p>
            <h3 className="wauth-title">{isEdit ? "Edit widget" : "Add widget"}</h3>
          </div>
          <StandardButton variant="ghost" onClick={onClose} aria-label="Close">Close</StandardButton>
        </header>

        <div className="wauth-body">
          {!meta && !problem && <p className="wauth-note">Reading the catalogue from the server...</p>}

          {meta && (
            <>
              <div className="wauth-field">
                <span className="wauth-label">Title</span>
                <StandardP2Input aria-label="Title" value={title} placeholder="What this widget shows"
                  onChange={(e) => setTitle(e.target.value)} />
              </div>

              {meta.purposes.length > 0 && (
                <div className="wauth-field">
                  <span className="wauth-label">Purpose, optional</span>
                  <StandardP2Select aria-label="Purpose" value={purpose} onChange={(e) => applyPurpose(e.target.value)}>
                    <option value="">no preset</option>
                    {meta.purposes.map((p) => <option key={p.code} value={p.code}>{p.label}</option>)}
                  </StandardP2Select>
                  <p className="wauth-hint">
                    A purpose only preselects a chart, a dimension and a measure. Every choice
                    stays editable and the list comes from the server.
                  </p>
                </div>
              )}

              <div className="wauth-grid">
                <div className="wauth-field">
                  <span className="wauth-label">Chart type</span>
                  <StandardP2Select aria-label="Chart type" value={chartType} onChange={(e) => setChartType(e.target.value)}>
                    <option value="">choose...</option>
                    {meta.chartTypes.map((c) => <option key={c.code} value={c.code}>{c.label}</option>)}
                  </StandardP2Select>
                </div>

                {usesDimension && (
                <div className="wauth-field">
                  <span className="wauth-label">Dimension, optional</span>
                  <StandardP2Select aria-label="Dimension" value={dimensionCode} onChange={(e) => setDimensionCode(e.target.value)}>
                    <option value="">none</option>
                    {dimensions.map((d) => <option key={d.code} value={d.code}>{d.label}</option>)}
                  </StandardP2Select>
                </div>
                )}

                {usesMeasure && (
                <div className="wauth-field">
                  <span className="wauth-label">Measure, optional</span>
                  <StandardP2Select aria-label="Measure" value={measureCode} onChange={(e) => setMeasureCode(e.target.value)}>
                    <option value="">choose...</option>
                    {measures.map((m) => <option key={m.code} value={m.code}>{m.label}{m.unit ? " (" + m.unit + ")" : ""}</option>)}
                  </StandardP2Select>
                </div>
                )}

                {needsParameter && (
                  <div className="wauth-field">
                    <span className="wauth-label">Parameter</span>
                    <StandardP2Select aria-label="Parameter" value={parameterCode} onChange={(e) => setParameterCode(e.target.value)}>
                      <option value="">choose...</option>
                      {(catalogFor("parameters") ?? []).map((p) => (
                        <option key={itemCode(p)} value={itemCode(p)}>{itemLabel(p)}</option>
                      ))}
                    </StandardP2Select>
                  </div>
                )}
              </div>

              <div className="wauth-field">
                <div className="wauth-rowhead">
                  <span className="wauth-label">Filters</span>
                  <span className="wauth-count">{filters.length}</span>
                  <span className="wauth-spacer" />
                  <StandardButton variant="ghost"
                    onClick={() => setFilters((f) => f.concat({ code: "", value: "" }))}>
                    Add filter
                  </StandardButton>
                </div>

                {filters.map((f, i) => {
                  const spec = meta.filters.find((x) => x.code === f.code);
                  const catalog = catalogFor(spec?.sourceCatalog);
                  return (
                    <div className="wauth-filterrow" key={"wf" + i}>
                      <StandardP2Select aria-label="Filter" value={f.code}
                        onChange={(e) => setFilters((rows) => rows.map((r, j) => j === i ? { code: e.target.value, value: "" } : r))}>
                        <option value="">choose...</option>
                        {meta.filters.map((x) => <option key={x.code} value={x.code}>{x.label}</option>)}
                      </StandardP2Select>

                      {catalog ? (
                        <StandardP2Select aria-label="Filter value" value={f.value}
                          onChange={(e) => setFilters((rows) => rows.map((r, j) => j === i ? { ...r, value: e.target.value } : r))}>
                          <option value="">any</option>
                          {catalog.map((c) => <option key={itemCode(c)} value={itemCode(c)}>{itemLabel(c)}</option>)}
                        </StandardP2Select>
                      ) : (
                        <StandardP2Input aria-label="Filter value" value={f.value} placeholder="value"
                          onChange={(e) => setFilters((rows) => rows.map((r, j) => j === i ? { ...r, value: e.target.value } : r))} />
                      )}

                      <StandardButton variant="ghost" aria-label="Remove filter"
                        onClick={() => setFilters((rows) => rows.filter((_, j) => j !== i))}>
                        Remove
                      </StandardButton>
                    </div>
                  );
                })}

                <p className="wauth-hint">
                  The filter list is published by the server, so a plant that adds a filter
                  category gets it here without a new release. Which categories can be stored
                  is still fixed in the widget contract; opening that is a backend change.
                </p>
                <p className="wauth-hint">
                  A filter saved here is this widget's permanent scope. The page filter bar
                  and a click on any other widget apply on top of it, narrowing further
                  inside that scope rather than replacing it. Leave the filters empty and
                  the widget follows the page alone.
                </p>
              </div>

              {problem && <p className="wauth-problem" role="alert">{problem}</p>}
            </>
          )}

          {!meta && problem && <p className="wauth-problem" role="alert">{problem}</p>}
        </div>

        <footer className="wauth-foot">
          <StandardButton variant="ghost" onClick={onClose}>Cancel</StandardButton>
          <StandardButton variant="primary" onClick={save} isDisabled={busy || !meta}>
            {isEdit ? "Save changes" : "Add to page"}
          </StandardButton>
        </footer>
      </section>
    </div>
  );
}

export default WidgetAuthoringPanel;