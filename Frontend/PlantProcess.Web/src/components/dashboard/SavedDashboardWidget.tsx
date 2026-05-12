import { Copy, Trash2, BarChart3 } from "lucide-react";
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

interface SavedDashboardWidgetProps {
  dashboardDefinitionId: string;
  widget: DashboardWidgetDefinitionRecord;
  onRemoved: () => void | Promise<void>;
  onCloned: () => void | Promise<void>;
}

export function SavedDashboardWidget({
  widget,
  onRemoved,
  onCloned,
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
            maxRows: 100,
            rawRowLimit: 500,
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
  }, [widget, filters]);

  const rows = (result?.rows ?? []) as ChartRow[];
  const categoryKey =
    result?.columns.find((x) => x.code !== widget.measureCode)?.code ??
    widget.dimensionCode;
  const valueKey = widget.measureCode;

  return (
    <div key={`saved-${widget.id}`}>
      <DashboardWidgetCard
        widgetId={`saved-${widget.id}` as any}
        title={widget.widgetTitle}
        subtitle={`${widget.chartType} · ${widget.dimensionCode} · ${widget.measureCode}`}
        icon={<BarChart3 size={18} />}
        chartTypes={["bar", "line", "pie", "table"] as any}
        exportRows={rows as Record<string, unknown>[]}
      >
        <div className="saved-widget-actions">
          <button className="secondary-button" onClick={onCloned} type="button">
            <Copy size={14} />
            Clone
          </button>
          <button className="secondary-button" onClick={onRemoved} type="button">
            <Trash2 size={14} />
            Remove
          </button>
        </div>

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

        {result ? (
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
                labelKey: categoryKey,
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
                labelKey: categoryKey,
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
                labelKey: categoryKey,
              }}
            />
          )
        ) : null}
      </DashboardWidgetCard>
    </div>
  );
}

function MiniTable({ rows }: { rows: ChartRow[] }) {
  if (!rows.length) {
    return (
      <div className="empty-insight">
        <strong>No data</strong>
        <p>No records are available for this saved widget.</p>
      </div>
    );
  }

  const columns = Object.keys(rows[0]);

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column}>{column}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index}>
              {columns.map((column) => (
                <td key={column}>{String(row[column] ?? "-")}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}