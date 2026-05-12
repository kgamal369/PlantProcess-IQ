import {
  BarChart3,
  ChevronDown,
  ChevronUp,
  Download,
  EyeOff,
  GripVertical,
  Maximize2,
  Minimize2,
  PanelTop,
  Shrink,
} from "lucide-react";
import type { ReactNode } from "react";
import {
  useDashboardSelections,
  type DashboardChartType,
  type DashboardWidgetId,
} from "../../state/DashboardSelectionContext";
import { useDashboardGridLayout } from "../../state/DashboardGridLayoutContext";

interface DashboardWidgetCardProps {
  widgetId: DashboardWidgetId;
  title: string;
  subtitle?: string;
  icon?: ReactNode;
  chartTypes?: DashboardChartType[];
  exportRows?: Record<string, unknown>[];
  children: ReactNode;
}

export function DashboardWidgetCard({
  widgetId,
  title,
  subtitle,
  icon,
  chartTypes,
  exportRows,
  children,
}: DashboardWidgetCardProps) {
  const {
    getWidgetState,
    setWidgetChartType,
    toggleWidgetCollapsed,
    toggleWidgetFullscreen,
    toggleWidgetHidden,
  } = useDashboardSelections();

  const { expandWidgetToFullRow, expandWidgetToHalfRow, compactWidget } =
    useDashboardGridLayout();

  const state = getWidgetState(widgetId);

  if (state.hidden) {
    return null;
  }

  const activeChartType = state.chartType ?? chartTypes?.[0];

  return (
    <section
      className={`dashboard-widget ${
        state.fullscreen ? "dashboard-widget--fullscreen" : ""
      }`}
    >
      <div className="dashboard-widget__header">
        <div className="dashboard-widget__title">
          <button
            className="dashboard-widget__drag-handle"
            title="Drag widget"
            type="button"
          >
            <GripVertical size={18} />
          </button>

          <div className="dashboard-widget__icon">
            {icon ?? <BarChart3 size={18} />}
          </div>

          <div>
            <h3>{title}</h3>
            {subtitle ? <p>{subtitle}</p> : null}
          </div>
        </div>

        <div className="dashboard-widget__actions">
          {chartTypes && chartTypes.length > 0 ? (
            <div className="chart-switcher">
              {chartTypes.map((type) => (
                <button
                  key={type}
                  className={
                    activeChartType === type
                      ? "chart-switcher__button chart-switcher__button--active"
                      : "chart-switcher__button"
                  }
                  onClick={() => setWidgetChartType(widgetId, type)}
                  type="button"
                >
                  {type}
                </button>
              ))}
            </div>
          ) : null}

          {exportRows && exportRows.length > 0 ? (
            <button
              className="icon-button"
              onClick={() => exportCsv(`${widgetId}.csv`, exportRows)}
              title="Export widget data"
              type="button"
            >
              <Download size={16} />
            </button>
          ) : null}

          <button
            className="icon-button"
            onClick={() => compactWidget(widgetId)}
            title="Compact widget"
            type="button"
          >
            <Shrink size={16} />
          </button>

          <button
            className="icon-button"
            onClick={() => expandWidgetToHalfRow(widgetId)}
            title="Expand to half row"
            type="button"
          >
            <PanelTop size={16} />
          </button>

          <button
            className="icon-button"
            onClick={() => expandWidgetToFullRow(widgetId)}
            title="Expand to full row"
            type="button"
          >
            <Maximize2 size={16} />
          </button>

          <button
            className="icon-button"
            onClick={() => toggleWidgetCollapsed(widgetId)}
            title={state.collapsed ? "Expand content" : "Collapse content"}
            type="button"
          >
            {state.collapsed ? (
              <ChevronDown size={16} />
            ) : (
              <ChevronUp size={16} />
            )}
          </button>

          <button
            className="icon-button"
            onClick={() => toggleWidgetFullscreen(widgetId)}
            title={
              state.fullscreen
                ? "Exit overlay fullscreen"
                : "Overlay fullscreen"
            }
            type="button"
          >
            {state.fullscreen ? (
              <Minimize2 size={16} />
            ) : (
              <Maximize2 size={16} />
            )}
          </button>

          <button
            className="icon-button"
            onClick={() => toggleWidgetHidden(widgetId)}
            title="Hide widget"
            type="button"
          >
            <EyeOff size={16} />
          </button>
        </div>
      </div>

      {!state.collapsed ? (
        <div className="dashboard-widget__body">{children}</div>
      ) : (
        <div className="dashboard-widget__collapsed">
          Widget collapsed. Use the arrow button to expand.
        </div>
      )}
    </section>
  );
}

function exportCsv(filename: string, rows: Record<string, unknown>[]) {
  if (rows.length === 0) return;

  const headers = Object.keys(rows[0]);

  const escapeValue = (value: unknown) => {
    if (value === null || value === undefined) return "";
    const raw = String(value).replaceAll('"', '""');
    return `"${raw}"`;
  };

  const csv = [
    headers.join(","),
    ...rows.map((row) =>
      headers.map((header) => escapeValue(row[header])).join(",")
    ),
  ].join("\n");

  const blob = new Blob([csv], {
    type: "text/csv;charset=utf-8;",
  });

  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");

  link.href = url;
  link.download = filename;
  link.click();

  URL.revokeObjectURL(url);
}