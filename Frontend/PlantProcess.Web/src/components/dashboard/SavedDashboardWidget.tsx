import { ExtraChart, isExtraChartType } from "./ChartExtras";
import { dimensionToFilterField, isTemporalDimension } from "@/state/widgetSelectionMap";
import { MetricCard } from "@/components/MetricCard";
import { BarChart3 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import { productApi } from "../../api/productApiClient";
import {
  readRoleBinding, staleRoles, describeStale,
} from "../../api/product-core/widget-role-binding";
import { dashboardingApi } from "../../api/dashboarding/dashboarding.api";
import type {
  DashboardWidgetDefinitionRecord,
  DashboardWidgetQueryResult,
} from "../../api/productApiClient";
import {
  resolveChartSwitcherOptions,
  type DashboardMetadata,
} from "../../api/product-core/dashboard-widget-types";

// T-046. One in-flight metadata request per session, shared by every card.
let dashboardMetadataPromise: Promise<DashboardMetadata> | null = null;

function loadDashboardMetadata(): Promise<DashboardMetadata> {
  if (!dashboardMetadataPromise) {
    dashboardMetadataPromise = (
      productApi.getDashboardMetadata() as Promise<DashboardMetadata>
    ).catch((error) => {
      dashboardMetadataPromise = null;
      throw error;
    });
  }
  return dashboardMetadataPromise;
}

import {
  InteractiveBarChart,
  InteractiveLineChart,
  InteractivePieChart,
} from "../charts/InteractiveCharts";
import type { ChartRow } from "../charts/InteractiveCharts";
import { DashboardWidgetCard } from "./DashboardWidgetCard";
import { HistogramChart } from "../charts/HistogramChart";
import { BoxPlotChart } from "../charts/BoxPlotChart";
import { ScatterXYChart } from "../charts/ScatterXYChart";
import { StackedSeriesChart } from "../charts/StackedSeriesChart";
import { EmptyInsightState } from "./EmptyInsightState";

import { StandardP2Table } from "@/components/standard/StandardP2Controls";
import { useDashboardFilters } from "../../state/DashboardFilterContext";
import { useDashboardSelection } from "../../state/DashboardSelectionContext";
interface SavedDashboardWidgetProps {
  dashboardDefinitionId: string;
  widget: DashboardWidgetDefinitionRecord;
  onEdit?: () => void | Promise<void>;
  onRemoved: () => void | Promise<void>;
  onCloned: () => void | Promise<void>;
  onHidden?: () => void | Promise<void>;
}

/** PPIQ-WIDGETFIX: widgets stored with chartType "kpi" previously fell through  *  every branch below into InteractiveBarChart, so a KPI tile rendered as a  *  50-bar chart of daily counts. MetricCard already existed and was unused  *  for this. Rate, score and avg measures are averaged; max/min take the  *  extreme; everything else sums. */ const AVERAGED_MEASURES = ["defectRate", "riskScore", "avgParameterValue", "processStepDuration"];  function kpiValue(rows: ChartRow[], valueKey: string, measureCode?: string | null): string {   const numbers = rows     .map((row) => Number(row[valueKey]))     .filter((n) => Number.isFinite(n));   if (!numbers.length) return "-";    let result: number;   if (measureCode === "maxParameterValue") {     result = Math.max(...numbers);   } else if (measureCode === "minParameterValue") {     result = Math.min(...numbers);   } else if (measureCode && AVERAGED_MEASURES.indexOf(measureCode) >= 0) {     result = numbers.reduce((a, b) => a + b, 0) / numbers.length;   } else {     result = numbers.reduce((a, b) => a + b, 0);   }    const decimals = Number.isInteger(result) ? 0 : 2;   return result.toLocaleString(undefined, {     minimumFractionDigits: decimals,     maximumFractionDigits: decimals,   }); }  export function SavedDashboardWidget({ dashboardDefinitionId,
  widget,
  onEdit,
  onRemoved,
  onCloned,
  onHidden,
}: SavedDashboardWidgetProps) {
  const [result, setResult] = useState<DashboardWidgetQueryResult | null>(null);
  const [error, setError] = useState<unknown>(null);
  const { filters: globalFilters } = useDashboardFilters();
  const { getWidgetState } = useDashboardSelection();
  const widgetState = getWidgetState(("saved-" + widget.id) as never);
  const activeChartType = widgetState.chartType ?? widget.chartType;

  const filters = useMemo(() => {
    try {
      const base: Record<string, unknown> = widget.filterJson
        ? JSON.parse(widget.filterJson)
        : {};
      const g = (globalFilters ?? {}) as Record<string, unknown>;
      for (const k of [
        "siteId", "areaId", "equipmentId", "materialCode", "sourceSystem",
        "defectType", "riskClass", "shiftCode", "fromUtc", "toUtc",
      ]) {
        const v = g[k];
        if (v !== undefined && v !== null && v !== "") { base[k] = v; }
      }
      return base;
    } catch {
      return {};
    }
  }, [widget.filterJson, globalFilters]);

  const displayOptions = useMemo(() => {
    try {
      return widget.displayOptionsJson
        ? JSON.parse(widget.displayOptionsJson)
        : {};
    } catch {
      return {};
    }
  }, [widget.displayOptionsJson]);

  useEffect(() => {
    let ignore = false;

    async function load() {
      setError(null);

      try {
        // sortDirection is pinned with "as const". Inline in the call below,
        // TypeScript contextually typed it as the literal "desc"; hoisted into
        // a shared const it widens to string, which DashboardWidgetQueryOptions
        // rejects because SortDirection is "asc" | "desc".
        const options = {
          maxRows: displayOptions.maxRows ?? 100,
          rawRowLimit: displayOptions.rawRowLimit ?? 500,
          sortDirection: "desc" as const,
          includeWarnings: true,
        };

        // An expression is enabled only when the server parsed it at save
        // time, so this branch is never taken by an expression that failed
        // validation - such a widget keeps its catalogue binding and draws
        // exactly as before. Filters are passed to both paths so that
        // cross-filtering behaves the same on either kind of widget.
        const useExpression =
          Boolean(widget.expressionEnabled) && Boolean(widget.queryExpression);

        const response = useExpression
          ? ((await dashboardingApi.executeWidgetQueryExpression({
              expression: String(widget.queryExpression),
              filters,
              options,
            })) as unknown as DashboardWidgetQueryResult)
          : await productApi.queryDashboardWidget({
              widgetType: widget.widgetType,
              chartType: widget.chartType,
              dimensionCode: widget.dimensionCode,
              measureCode: widget.measureCode,
              parameterCode: widget.parameterCode,
              filters,
              options,
            });

        if (!ignore) setResult(response);
      } catch (loadError) {
        if (!ignore) setError(loadError);
      }
    }

    load();

    return () => {
      ignore = true;
    };
  }, [widget, filters, displayOptions]);

  const rows = (result?.rows ?? []) as ChartRow[];

  // M1-16. An explicitly bound widget READS its mapping. Only a widget that
  // has never been bound falls back to inference, so every widget authored
  // before this shipped keeps rendering exactly as it did.
  const roleBinding = useMemo(
    () => readRoleBinding(widget.displayOptionsJson),
    [widget.displayOptionsJson],
  );
  const resultColumns = useMemo(
    () => (result?.columns ?? []).map((c) => c.code),
    [result],
  );
  const stale = useMemo(
    () => (result ? staleRoles(roleBinding, resultColumns) : []),
    [roleBinding, resultColumns, result],
  );

  const categoryKey =
    (roleBinding?.category ?? null) ??
    result?.columns.find((column) => column.code === widget.dimensionCode)?.code ??
    result?.columns.find((column) => column.code !== "value")?.code ??
    widget.dimensionCode;

  // T-044 D7. IDENTITY AND DISPLAY ARE TWO DIFFERENT THINGS.
  //
  // categoryKey above is the CANONICAL dimension column. It is what the
  // backend groups by, what selection state filters on, and what a saved
  // selection must carry. For a relational dimension it is a UUID.
  //
  // displayKey is what a person reads. BuildResult returns dimensionLabel
  // beside every category, resolved from the database, and until now nothing
  // rendered it: the axis was bound to the identity column, so a bar chart on
  // equipment plotted UUIDs.
  //
  // The two must never be swapped. Passing displayKey where identity is
  // expected would fix the picture and silently break filtering, because a
  // label matches no row in the canonical column.
  const displayKey =
    result?.columns.find((column) => column.code === "dimensionLabel")?.code ??
    categoryKey;

  const valueKey =
    (roleBinding?.value ?? null) ??
    result?.columns.find((column) => column.code === "value")?.code ??
    result?.columns.find((column) => column.dataType === "number")?.code ??
    "value";

  // T-046. THE SWITCHER READS THE SERVER, AND A DASHBOARD ASKS ONCE.
  //
  // Every widget on a page needs the same catalogue, so the request is shared
  // rather than repeated per card. A failure clears the cache so the next
  // mount retries instead of pinning an empty catalogue for the session.
  const [metadata, setMetadata] = useState<DashboardMetadata | null>(null);

  useEffect(() => {
    let ignore = false;
    loadDashboardMetadata()
      .then((loaded) => { if (!ignore) { setMetadata(loaded); } })
      .catch(() => { if (!ignore) { setMetadata(null); } });
    return () => { ignore = true; };
  }, []);

  // The rule is keyed on the binding this widget actually carries. No rule
  // means no published verdict, and the projection returns nothing.
  const compatibilityRule = useMemo(
    () =>
      (metadata?.compatibilityRules ?? []).find(
        (rule) =>
          rule.dimensionCode === widget.dimensionCode &&
          rule.measureCode === widget.measureCode
      ) ?? null,
    [metadata, widget.dimensionCode, widget.measureCode]
  );

  // T-047 Pack D. A series role is a property of the RESULT too.
  const hasSeriesRole = useMemo(
    () =>
      ["category", "series", "value"].every((role) =>
        Boolean(result?.columns.some((column) => column.code === role))
      ),
    [result]
  );

  // T-047 Pack C2. Two numeric axes are a property of the RESULT, established
  // by the roles the source published, never by the chart code.
  const hasTwoNumericAxes = useMemo(
    () =>
      Boolean(result?.columns.some((column) => column.code === "xValue")) &&
      Boolean(result?.columns.some((column) => column.code === "yValue")),
    [result]
  );

  const chartOptions = useMemo(
    () =>
      resolveChartSwitcherOptions(
        metadata?.chartTypes ?? null,
        compatibilityRule,
        activeChartType
      ),
    [metadata, compatibilityRule, activeChartType]
  );

  return (
    <DashboardWidgetCard
      widgetId={`saved-${widget.id}` as any}
      title={widget.widgetTitle}
      subtitle={`${widget.chartType} · ${widget.dimensionCode} · ${widget.measureCode}`}
      icon={<BarChart3 size={18} />}
      chartOptions={chartOptions}
      exportRows={rows as Record<string, unknown>[]}
      onEdit={onEdit}
      onRename={onEdit ? onEdit : undefined}
      onRemove={async () => { await productApi.deleteDashboardWidget(dashboardDefinitionId, widget.id); await Promise.resolve(onRemoved()); }}
      onClone={async () => { await productApi.cloneDashboardWidget(dashboardDefinitionId, widget.id, { widgetTitle: widget.widgetTitle + " (copy)" }); await Promise.resolve(onCloned()); }}
      onHide={onHidden}
    >
      {/* M1-16. A bound column that the query no longer returns is REPORTED
          BY NAME. It is never repaired by repointing to another column: a
          chart that silently moves to a different column is the exact failure
          this bind step exists to prevent, and it is invisible to the viewer.
          The chart below still renders, so the widget degrades honestly
          instead of going blank. */}
      {stale.length > 0 ? (
        <div className="widget-stale" role="alert" data-testid="widget-stale">
          <strong>This widget's column mapping is out of date.</strong>
          <p>
            {describeStale(roleBinding, stale)} {stale.length === 1 ? "is" : "are"} not in
            what the query returns now. Open the widget and choose again -
            nothing has been repointed for you.
          </p>
        </div>
      ) : null}

      {error ? (
        <div className="empty-insight">
          <strong>Widget failed</strong>
          <p>{String(error)}</p>
        </div>
      ) : null}

      {!error && !result ? (
        <div className="empty-insight">
          <strong>Loading widget...</strong>
        </div>
      ) : null}

      {result && !rows.length ? <EmptyInsightState /> : null}

      {result && rows.length ? (
        activeChartType === "kpi" ? (
          <MetricCard
            title={widget.widgetTitle}
            value={kpiValue(rows, valueKey, widget.measureCode)}
            subtitle={widget.measureCode}
          />
        ) : hasSeriesRole ? (
          // T-047 Pack D. A result carrying category, series and value is a
          // multi-series result whatever chart code names it.
          <StackedSeriesChart rows={rows as Record<string, unknown>[]} />
        ) : hasTwoNumericAxes ? (
          // T-047 Pack C2. Routed on the ROLES PRESENT, not on the chart code.
          // "scatter" already reaches ExtraChart, which collapses a result into
          // one category and one value - it would silently plot xValue against
          // itself. A result that publishes xValue and yValue is a two-axis
          // result whatever it is called, and only that draws here.
          <ScatterXYChart rows={rows as Record<string, unknown>[]} />
        ) : activeChartType === "boxPlot" ? (
          // Bound by name for the same reason as the histogram: a box plot row
          // carries six numeric columns, and an inferring renderer would pick
          // whichever came first.
          <BoxPlotChart rows={rows as Record<string, unknown>[]} />
        ) : activeChartType === "histogram" ? (
          // T-047 Pack A. Routed AHEAD of the extra-chart branch on purpose.
          // ExtraChart resolves its value column by finding the first numeric
          // one, which here is binLower - it would plot the axis against
          // itself and look plausible. HistogramChart binds every role by name.
          <HistogramChart rows={rows as Record<string, unknown>[]} />
        ) : isExtraChartType(activeChartType) ? (
          <ExtraChart type={String(activeChartType)} rows={rows as Record<string, unknown>[]} categoryKey={categoryKey} labelKey={displayKey} valueKey={valueKey} field={dimensionToFilterField(widget.dimensionCode)} timeDimension={isTemporalDimension(widget.dimensionCode) ? widget.dimensionCode : null} />
        ) : activeChartType === "line" || activeChartType === "area" ? (
          <InteractiveLineChart
            data={rows}
            categoryKey={displayKey}
            valueKey={valueKey}
            area={activeChartType === "area"}
            selection={{
              type: "generic",
              field: dimensionToFilterField(widget.dimensionCode),
              timeDimension: isTemporalDimension(widget.dimensionCode) ? widget.dimensionCode : null,
              sourceWidget: widget.widgetTitle,
              valueKey: categoryKey,
              labelKey: "dimensionLabel",
            }}
          />
        ) : activeChartType === "pie" || activeChartType === "donut" ? (
          <InteractivePieChart
            data={rows}
            categoryKey={displayKey}
            valueKey={valueKey}
            donut={activeChartType === "donut"}
            selection={{
              type: "generic",
              field: dimensionToFilterField(widget.dimensionCode),
              timeDimension: isTemporalDimension(widget.dimensionCode) ? widget.dimensionCode : null,
              sourceWidget: widget.widgetTitle,
              valueKey: categoryKey,
              labelKey: "dimensionLabel",
            }}
          />
        ) : activeChartType === "table" ? (
          <MiniTable rows={rows} />
        ) : (
          <InteractiveBarChart
            data={rows}
            categoryKey={displayKey}
            valueKey={valueKey}
            selection={{
              type: "generic",
              field: dimensionToFilterField(widget.dimensionCode),
              timeDimension: isTemporalDimension(widget.dimensionCode) ? widget.dimensionCode : null,
              sourceWidget: widget.widgetTitle,
              valueKey: categoryKey,
              labelKey: "dimensionLabel",
            }}
          />
        )
      ) : null}
    </DashboardWidgetCard>
  );
}

function MiniTable({ rows }: { rows: ChartRow[] }) {
  if (!rows.length) return <EmptyInsightState />;

  const columns = Object.keys(rows[0] ?? {});

  return (
    <div className="table-shell">
      <StandardP2Table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column}>{column}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.slice(0, 50).map((row, index) => (
            <tr key={index}>
              {columns.map((column) => (
                <td key={column}>{String(row[column] ?? "")}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </StandardP2Table>
    </div>
  );
}