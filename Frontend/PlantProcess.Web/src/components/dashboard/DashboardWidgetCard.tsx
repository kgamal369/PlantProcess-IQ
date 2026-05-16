import {
  BarChart3,
  ChevronDown,
  ChevronUp,
  Copy,
  Download,
  Edit3,
  EyeOff,
  GripVertical,
  Maximize2,
  Minimize2,
  MoreVertical,
  PanelTop,
  Shrink,
  Trash2,
} from "lucide-react";
import type { ReactNode } from "react";
import { useState } from "react";

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

  onRename?: () => void | Promise<void>;
  onEdit?: () => void | Promise<void>;
  onRemove?: () => void | Promise<void>;
  onClone?: () => void | Promise<void>;
  onHide?: () => void | Promise<void>;
  disableActions?: boolean;
}

export function DashboardWidgetCard({
  widgetId,
  title,
  subtitle,
  icon,
  chartTypes = ["bar", "line", "pie", "table"],
  exportRows,
  children,
  onRename,
  onEdit,
  onRemove,
  onClone,
  onHide,
  disableActions = false,
}: DashboardWidgetCardProps) {
 const { getWidgetState, setWidgetChartType } =
  useDashboardSelections();

  const {
    expandWidgetToFullRow,
    expandWidgetToHalfRow,
    compactWidget,
  } = useDashboardGridLayout();

  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isActionMenuOpen, setIsActionMenuOpen] = useState(false);

  const state = getWidgetState(widgetId);
  const activeChartType = state.chartType ?? chartTypes[0];

  function exportCsv() {
    if (!exportRows?.length) return;

    const headers = Object.keys(exportRows[0] ?? {});
    const escapeValue = (value: unknown) => {
      if (value === null || value === undefined) return "";
      const raw = String(value).replaceAll('"', '""');
      return `"${raw}"`;
    };

    const csv = [
      headers.map(escapeValue).join(","),
      ...exportRows.map((row) =>
        headers.map((header) => escapeValue(row[header])).join(",")
      ),
    ].join("\n");

    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");

    link.href = url;
    link.download = `${title.replace(/[^a-z0-9]+/gi, "_").toLowerCase()}.csv`;
    link.click();

    URL.revokeObjectURL(url);
  }

  async function execute(action?: () => void | Promise<void>) {
    setIsActionMenuOpen(false);
    await action?.();
  }

  return (
    <article
      className={`dashboard-widget ${
        isFullscreen ? "dashboard-widget--fullscreen" : ""
      }`}
    >
      <header className="dashboard-widget__header">
        <div className="dashboard-widget__title-row">
          <span className="dashboard-widget__drag-handle" title="Drag widget">
            <GripVertical size={16} />
          </span>

          <span className="widget-icon">{icon ?? <BarChart3 size={18} />}</span>

          <div>
            <h3>{title}</h3>
            {subtitle ? <p>{subtitle}</p> : null}
          </div>
        </div>

        <div className="dashboard-widget__actions">
          {chartTypes.length > 1 ? (
            <select
              value={activeChartType}
              onChange={(event) =>
                setWidgetChartType(widgetId, event.target.value as DashboardChartType)
              }
              title="Switch chart type"
            >
              {chartTypes.map((chartType) => (
                <option key={chartType} value={chartType}>
                  {chartType}
                </option>
              ))}
            </select>
          ) : null}

          <button
            type="button"
            className="icon-button"
            onClick={() =>
              activeChartType === "table"
                ? compactWidget(String(widgetId))
                : expandWidgetToHalfRow(String(widgetId))
            }
            title="Resize widget"
          >
            <PanelTop size={16} />
          </button>

          <button
            type="button"
            className="icon-button"
            onClick={() => expandWidgetToFullRow(String(widgetId))}
            title="Full row"
          >
            <Maximize2 size={16} />
          </button>

          <button
            type="button"
            className="icon-button"
            onClick={() => compactWidget(String(widgetId))}
            title="Compact"
          >
            <Shrink size={16} />
          </button>

          <button
            type="button"
            className="icon-button"
            onClick={exportCsv}
            disabled={!exportRows?.length}
            title="Export CSV"
          >
            <Download size={16} />
          </button>

          <button
            type="button"
            className="icon-button"
            onClick={() => setIsCollapsed((value) => !value)}
            title={isCollapsed ? "Expand" : "Collapse"}
          >
            {isCollapsed ? <ChevronDown size={16} /> : <ChevronUp size={16} />}
          </button>

          <button
            type="button"
            className="icon-button"
            onClick={() => setIsFullscreen((value) => !value)}
            title="Fullscreen"
          >
            {isFullscreen ? <Minimize2 size={16} /> : <Maximize2 size={16} />}
          </button>

          {!disableActions ? (
            <div className="widget-action-menu">
              <button
                type="button"
                className="icon-button"
                onClick={() => setIsActionMenuOpen((value) => !value)}
                title="Widget actions"
              >
                <MoreVertical size={16} />
              </button>

              {isActionMenuOpen ? (
                <div className="widget-action-menu__panel">
                  <button type="button" onClick={() => execute(onRename)}>
                    <Edit3 size={14} />
                    Rename
                  </button>

                  <button type="button" onClick={() => execute(onEdit)}>
                    <Edit3 size={14} />
                    Edit
                  </button>

                  <button type="button" onClick={() => execute(onClone)}>
                    <Copy size={14} />
                    Duplicate / Clone
                  </button>

                  <button
                    type="button"
                    onClick={() =>
                      execute(async () => {
                        await onHide?.();
                      })
                    }
                  >
                    <EyeOff size={14} />
                    Hide
                  </button>

                  <button
                    type="button"
                    className="danger"
                    onClick={() =>
                      execute(async () => {
                        const confirmed = window.confirm(
                          `Remove widget "${title}" from this dashboard?`
                        );

                        if (!confirmed) return;

                        await onRemove?.();
                      })
                    }
                  >
                    <Trash2 size={14} />
                    Remove
                  </button>
                </div>
              ) : null}
            </div>
          ) : null}
        </div>
      </header>

      {!isCollapsed ? (
        <div className="dashboard-widget__body">{children}</div>
      ) : null}
    </article>
  );
}