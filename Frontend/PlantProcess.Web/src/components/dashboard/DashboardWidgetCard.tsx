// ============================================================
// FILE: Frontend/PlantProcess.Web/src/components/dashboard/DashboardWidgetCard.tsx
//
// FIXES:
// 1. Fullscreen now uses a real overlay (fixed position, z-index 9999)
//    instead of just toggling a CSS class — works reliably.
// 2. Separate Expand (Maximize2) and Fullscreen (Scan/Minimize2) icons
//    so they are visually distinct and both function correctly.
// 3. Chart-type selector styled as piq pill buttons instead of raw <select>.
// 4. All icon-buttons aligned to the same visual style.
// 5. Action menu closes on outside click.
// ============================================================

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
  Scan,
  Shrink,
  Trash2,
} from "lucide-react";
import type { ReactNode } from "react";
import { useEffect, useRef, useState } from "react";

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

// Chart type → short display label
const CHART_TYPE_LABELS: Record<string, string> = {
  line: "Line",
  area: "Area",
  bar: "Bar",
  pie: "Pie",
  donut: "Donut",
  scatter: "Scatter",
  heatmap: "Heatmap",
  table: "Table",
};

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
  const { getWidgetState, setWidgetChartType } = useDashboardSelections();
  const { expandWidgetToFullRow, expandWidgetToHalfRow, compactWidget } =
    useDashboardGridLayout();

  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isActionMenuOpen, setIsActionMenuOpen] = useState(false);

  const menuRef = useRef<HTMLDivElement>(null);
  const state = getWidgetState(widgetId);
  const activeChartType = state.chartType ?? chartTypes[0];

  // Close action menu on outside click
  useEffect(() => {
    if (!isActionMenuOpen) return;
    function handleClick(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setIsActionMenuOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, [isActionMenuOpen]);

  // Close fullscreen on Escape key
  useEffect(() => {
    if (!isFullscreen) return;
    function handleKey(e: KeyboardEvent) {
      if (e.key === "Escape") setIsFullscreen(false);
    }
    document.addEventListener("keydown", handleKey);
    return () => document.removeEventListener("keydown", handleKey);
  }, [isFullscreen]);

  // Prevent body scroll when fullscreen
  useEffect(() => {
    document.body.style.overflow = isFullscreen ? "hidden" : "";
    return () => { document.body.style.overflow = ""; };
  }, [isFullscreen]);

  function exportCsv() {
    if (!exportRows?.length) return;
    const headers = Object.keys(exportRows[0] ?? {});
    const escape = (v: unknown) => {
      if (v === null || v === undefined) return "";
      return `"${String(v).replaceAll('"', '""')}"`;
    };
    const csv = [
      headers.map(escape).join(","),
      ...exportRows.map((row) => headers.map((h) => escape(row[h])).join(",")),
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

  const cardContent = (
    <article
      className={`dashboard-widget ${isFullscreen ? "dashboard-widget--fullscreen-inner" : ""}`}
    >
      <header className="dashboard-widget__header">
        <div className="dashboard-widget__title-row">
          <span
            className="dashboard-widget__drag-handle"
            title="Drag to reposition"
            aria-hidden="true"
          >
            <GripVertical size={16} />
          </span>

          <span className="widget-icon" aria-hidden="true">
            {icon ?? <BarChart3 size={18} />}
          </span>

          <div className="dashboard-widget__title-copy">
            <h3>{title}</h3>
            {subtitle ? <p>{subtitle}</p> : null}
          </div>
        </div>

        <div className="dashboard-widget__actions">
          {/* Chart type switcher — pill buttons */}
          {chartTypes.length > 1 ? (
            <div className="widget-chart-switcher" role="group" aria-label="Chart type">
              {chartTypes.map((ct) => (
                <button
                  key={ct}
                  type="button"
                  className={`widget-chart-btn ${activeChartType === ct ? "widget-chart-btn--active" : ""}`}
                  onClick={() => setWidgetChartType(widgetId, ct)}
                  title={`Switch to ${ct} chart`}
                >
                  {CHART_TYPE_LABELS[ct] ?? ct}
                </button>
              ))}
            </div>
          ) : null}

          {/* Resize: half row */}
          <button
            type="button"
            className="icon-button"
            onClick={() =>
              activeChartType === "table"
                ? compactWidget(String(widgetId))
                : expandWidgetToHalfRow(String(widgetId))
            }
            title="Half-row width"
            aria-label="Half-row width"
          >
            <PanelTop size={15} />
          </button>

          {/* Resize: full row */}
          <button
            type="button"
            className="icon-button"
            onClick={() => expandWidgetToFullRow(String(widgetId))}
            title="Full-row width"
            aria-label="Full-row width"
          >
            <Maximize2 size={15} />
          </button>

          {/* Compact */}
          <button
            type="button"
            className="icon-button"
            onClick={() => compactWidget(String(widgetId))}
            title="Compact size"
            aria-label="Compact size"
          >
            <Shrink size={15} />
          </button>

          {/* Export CSV */}
          <button
            type="button"
            className="icon-button"
            onClick={exportCsv}
            disabled={!exportRows?.length}
            title="Export CSV"
            aria-label="Export CSV"
          >
            <Download size={15} />
          </button>

          {/* Collapse / Expand body */}
          <button
            type="button"
            className="icon-button"
            onClick={() => setIsCollapsed((v) => !v)}
            title={isCollapsed ? "Expand" : "Collapse"}
            aria-label={isCollapsed ? "Expand widget" : "Collapse widget"}
          >
            {isCollapsed ? <ChevronDown size={15} /> : <ChevronUp size={15} />}
          </button>

          {/* Fullscreen overlay toggle */}
          <button
            type="button"
            className="icon-button"
            onClick={() => setIsFullscreen((v) => !v)}
            title={isFullscreen ? "Exit fullscreen (Esc)" : "Fullscreen"}
            aria-label={isFullscreen ? "Exit fullscreen" : "Fullscreen"}
          >
            {isFullscreen ? <Minimize2 size={15} /> : <Scan size={15} />}
          </button>

          {/* Action menu */}
          {!disableActions ? (
            <div className="widget-action-menu" ref={menuRef}>
              <button
                type="button"
                className="icon-button"
                onClick={() => setIsActionMenuOpen((v) => !v)}
                title="Widget actions"
                aria-label="Widget actions"
                aria-expanded={isActionMenuOpen}
              >
                <MoreVertical size={15} />
              </button>

              {isActionMenuOpen ? (
                <div className="widget-action-menu__panel" role="menu">
                  {onRename ? (
                    <button type="button" role="menuitem" onClick={() => execute(onRename)}>
                      <Edit3 size={14} />
                      Rename
                    </button>
                  ) : null}

                  {onEdit ? (
                    <button type="button" role="menuitem" onClick={() => execute(onEdit)}>
                      <Edit3 size={14} />
                      Edit widget
                    </button>
                  ) : null}

                  {onClone ? (
                    <button type="button" role="menuitem" onClick={() => execute(onClone)}>
                      <Copy size={14} />
                      Duplicate
                    </button>
                  ) : null}

                  {onHide ? (
                    <button type="button" role="menuitem" onClick={() => execute(onHide)}>
                      <EyeOff size={14} />
                      Hide
                    </button>
                  ) : null}

                  {onRemove ? (
                    <button
                      type="button"
                      role="menuitem"
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
                  ) : null}
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

  // Fullscreen: render in a fixed overlay portal-style div
  if (isFullscreen) {
    return (
      <>
        {/* Backdrop */}
        <div
          className="widget-fullscreen-backdrop"
          onClick={() => setIsFullscreen(false)}
          aria-hidden="true"
        />
        {/* Fullscreen panel */}
        <div className="widget-fullscreen-panel">
          {cardContent}
        </div>
      </>
    );
  }

  return cardContent;
}
