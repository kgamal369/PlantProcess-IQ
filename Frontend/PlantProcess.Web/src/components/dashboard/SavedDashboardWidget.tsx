import { BarChart3 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import { plantProcessApi } from "../../api/plantProcessApi";
import type {
  DashboardWidgetDefinitionRecord,
  DashboardWidgetQueryResult,
} from "../../api/plantProcessApi";

import {
  InteractiveBarChart,
  InteractiveLineChart,
  InteractivePieChart,
} from "../charts/InteractiveCharts";
import type { ChartRow } from "../charts/InteractiveCharts";
import { DashboardWidgetCard } from "./DashboardWidgetCard";
import { EmptyInsightState } from "./EmptyInsightState";

interface SavedDashboardWidgetProps {
  dashboardDefinitionId: string;
  widget: DashboardWidgetDefinitionRecord;
  onEdit: () => void | Promise<void>;
  onRemoved: () => void | Promise<void>;
  onCloned: () => void | Promise<void>;
  onHidden?: () => void | Promise<void>;
}

export function SavedDashboardWidget({
  widget,
  onEdit,
  onRemoved,
  onCloned,
  onHidden,
}: SavedDashboardWidgetProps) {
  const [result, setResult] = useState<DashboardWidgetQueryResult | null>(null);
  const [error, setError] = useState<unknown>(null);

  const filters = useMemo(() => {
    try {
      return widget.filterJson ? JSON.parse(widget.filterJson) : {};
    } catch {
      return {};
    }
  }, [widget.filterJson]);

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
        const response = await plantProcessApi.queryDashboardWidget({
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
      chartTypes={["bar", "line", "pie", "table"] as any}
      exportRows={rows as Record<string, unknown>[]}
      onEdit={onEdit}
      onRename={onEdit}
      onRemove={onRemoved}
      onClone={onCloned}
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
        widget.chartType === "line" || widget.chartType === "area" ? (
          <InteractiveLineChart
            data={rows}
            categoryKey={categoryKey}
            valueKey={valueKey}
            area={widget.chartType === "area"}
            selection={{
              type: "generic",
              field: "materialCode",
              sourceWidget: widget.widgetTitle,
              valueKey: categoryKey,
              labelKey: "dimensionLabel",
            }}
          />
        ) : widget.chartType === "pie" || widget.chartType === "donut" ? (
          <InteractivePieChart
            data={rows}
            categoryKey={categoryKey}
            valueKey={valueKey}
            donut={widget.chartType === "donut"}
            selection={{
              type: "generic",
              field: "materialCode",
              sourceWidget: widget.widgetTitle,
              valueKey: categoryKey,
              labelKey: "dimensionLabel",
            }}
          />
        ) : widget.chartType === "table" ? (
          <MiniTable rows={rows} />
        ) : (
          <InteractiveBarChart
            data={rows}
            categoryKey={categoryKey}
            valueKey={valueKey}
            selection={{
              type: "generic",
              field: "materialCode",
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
      <table>
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
      </table>
    </div>
  );
}