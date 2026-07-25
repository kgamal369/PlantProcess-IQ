import { ExtraChart, isExtraChartType, extendChartTypes } from "./ChartExtras";
import { dimensionToFilterField, isTemporalDimension } from "@/state/widgetSelectionMap";
import { MetricCard } from "@/components/MetricCard";
import { BarChart3 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import { productApi } from "../../api/productApiClient";
import type {
  DashboardWidgetDefinitionRecord,
  DashboardWidgetQueryResult,
} from "../../api/productApiClient";

import {
  InteractiveBarChart,
  InteractiveLineChart,
  InteractivePieChart,
} from "../charts/InteractiveCharts";
import type { ChartRow } from "../charts/InteractiveCharts";
import { DashboardWidgetCard } from "./DashboardWidgetCard";
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
        const response = await productApi.queryDashboardWidget({
          widgetType: widget.widgetType,
          chartType: widget.chartType,
          dimensionCode: widget.dimensionCode,
          measureCode: widget.measureCode,
          parameterCode: widget.parameterCode,
          filters,
          options: {
            maxRows: displayOptions.maxRows ?? 100,
            rawRowLimit: displayOptions.rawRowLimit ?? 500,
            sortDirection: "desc",
            includeWarnings: true,
          },
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

  const categoryKey =
    result?.columns.find((column) => column.code === widget.dimensionCode)?.code ??
    result?.columns.find((column) => column.code !== "value")?.code ??
    widget.dimensionCode;

  const valueKey =
    result?.columns.find((column) => column.code === "value")?.code ??
    result?.columns.find((column) => column.dataType === "number")?.code ??
    "value";

  return (
    <DashboardWidgetCard
      widgetId={`saved-${widget.id}` as any}
      title={widget.widgetTitle}
      subtitle={`${widget.chartType} · ${widget.dimensionCode} · ${widget.measureCode}`}
      icon={<BarChart3 size={18} />}
      chartTypes={extendChartTypes(widget.measureCode) as any}
      exportRows={rows as Record<string, unknown>[]}
      onEdit={onEdit}
      onRename={onEdit ? onEdit : undefined}
      onRemove={async () => { await productApi.deleteDashboardWidget(dashboardDefinitionId, widget.id); await Promise.resolve(onRemoved()); }}
      onClone={async () => { await productApi.cloneDashboardWidget(dashboardDefinitionId, widget.id, { widgetTitle: widget.widgetTitle + " (copy)" }); await Promise.resolve(onCloned()); }}
      onHide={onHidden}
    >
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
        ) : isExtraChartType(activeChartType) ? (
          <ExtraChart type={String(activeChartType)} rows={rows as Record<string, unknown>[]} categoryKey={categoryKey} valueKey={valueKey} field={dimensionToFilterField(widget.dimensionCode)} timeDimension={isTemporalDimension(widget.dimensionCode) ? widget.dimensionCode : null} />
        ) : activeChartType === "line" || activeChartType === "area" ? (
          <InteractiveLineChart
            data={rows}
            categoryKey={categoryKey}
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
            categoryKey={categoryKey}
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
            categoryKey={categoryKey}
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