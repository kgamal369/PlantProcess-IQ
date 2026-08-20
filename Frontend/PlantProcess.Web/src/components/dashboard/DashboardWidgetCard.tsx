// ============================================================
// FILE: Frontend/PlantProcess.Web/src/components/dashboard/DashboardWidgetCard.tsx
//
// FIXES:
// 1. Fullscreen now uses a real overlay (fixed position, z-index 9999)
//    instead of just toggling a CSS class — works reliably.
// 2. Separate Expand (Maximize2) and Fullscreen (Scan/Minimize2) icons
//    so they are visually distinct and both function correctly.
// 3. Chart-type selector styled as piq pill buttons instead of raw <StandardP2Select>.
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
import type { ChartSwitcherOption } from "../../api/product-core/dashboard-widget-types";
import { StandardButton } from "@/components/standard";

import { StandardP2Select } from "@/components/standard/StandardP2Controls";
interface DashboardWidgetCardProps {
  widgetId: DashboardWidgetId;
  title: string;
  subtitle?: string;
  icon?: ReactNode;
  chartOptions?: ChartSwitcherOption[];
  /**
   * DEMO-BI-R1. The chart type the WIDGET WAS SAVED AS.
   *
   * Without it this card fell back to the first available switcher option when
   * the reader had made no choice, so a widget saved as "kpi" - a single number
   * - rendered as whatever happened to be first in the option list. Material
   * Units and Quality Events drew one dot on an axis and Process Observations
   * drew a full circle, none of which is a KPI tile.
   *
   * The reader's explicit choice still wins. This only decides what to show
   * before any choice is made, and the honest answer to that is what the widget
   * says it is.
   */
  savedChartType?: string | null;
  exportRows?: Record<string, unknown>[];
  children: ReactNode;
  onRename?: () => void | Promise<void>;
  onEdit?: () => void | Promise<void>;
  onRemove?: () => void | Promise<void>;
  onClone?: () => void | Promise<void>;
  onHide?: () => void | Promise<void>;
  disableActions?: boolean;
}

// T-046. The local label map is retired. It named eight of the seventeen
// types in the product grammar, so a ninth rendered as its raw code and
// nothing failed. Labels are published beside each type by the server.

// Shown when the renderer does not exist. This is a build fact, so it is the
// one sentence here the server does not supply - and it is deliberately
// silent about the author's dimension and measure, which are not at fault.
const RENDERER_UNAVAILABLE = "This chart type is not available in this build yet.";

// Shown when the server refused the type for this binding but published no
// sentence. The card never invents a cause.
const REFUSAL_WITHOUT_REASON = "The server does not allow this chart type for this widget.";

export function DashboardWidgetCard({
  widgetId,
  title,
  subtitle,
  icon,
  chartOptions = [],
  savedChartType,
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
  const { expandWidgetToFullRow, compactWidget } = useDashboardGridLayout();

  /** 
PPIQ-WIDGETFIX
: react-grid-layout keys each item by the child key, which is
   *  the raw widget id. This card receives "saved-<id>", so every resize call
   *  used to look up an id that no layout item carried and silently did nothing.
   *  Strip the prefix before touching the grid. */
  const gridItemId = String(widgetId).replace(/^saved-/, "");

  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isActionMenuOpen, setIsActionMenuOpen] = useState(false);

  const menuRef = useRef<HTMLDivElement>(null);
  const state = getWidgetState(widgetId);
  // DEMO-BI-R1. Precedence: what the reader chose, then what the widget was
  // saved as, then the first option that is actually available. The saved type
  // used to be missing from this chain entirely, which is how a kpi tile became
  // a one-point chart.
  const activeChartType =
    state.chartType ??
    (savedChartType ? savedChartType : undefined) ??
    chartOptions.find((option) => option.state === "available")?.code ??
    chartOptions[0]?.code;

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
          {chartOptions.length > 1 ? (
            <div className="widget-chart-switcher" role="group" aria-label="Chart type">
              {chartOptions.map((option) => {
                const selectable = option.state === "available";
                const title =
                  option.state === "available"
                    ? "Switch to " + option.label
                    : option.state === "unavailable"
                      ? RENDERER_UNAVAILABLE
                      : (option.reason ?? REFUSAL_WITHOUT_REASON);

                return (
                  <StandardButton
                    key={option.code}
                    type="button"
                    className={`widget-chart-btn ${activeChartType === option.code ? "widget-chart-btn--active" : ""}`}
                    isDisabled={!selectable}
                    data-chart-state={option.state}
                    onClick={
                      selectable
                        ? () => setWidgetChartType(widgetId, option.code as DashboardChartType)
                        : undefined
                    }
                    title={title}
                  >
                    {option.label}
                  </StandardButton>
                );
              })}
            </div>
          ) : null}

          {/* Resize: full row */}
          <StandardButton
            type="button"
            className="icon-button"
            onClick={() => expandWidgetToFullRow(gridItemId)}
            title="Full-row width"
            aria-label="Full-row width"
          >
            <Maximize2 size={15} />
          </StandardButton>

          {/* Compact */}
          <StandardButton
            type="button"
            className="icon-button"
            onClick={() => compactWidget(gridItemId)}
            title="Compact size"
            aria-label="Compact size"
          >
            <Shrink size={15} />
          </StandardButton>

          {/* Export CSV */}
          <StandardButton
            type="button"
            className="icon-button"
            onClick={exportCsv}
            isDisabled={!exportRows?.length}
            title="Export CSV"
            ariaLabel="Export CSV"
          >
            <Download size={15} />
          </StandardButton>

          {/* Collapse / Expand body */}
          <StandardButton
            type="button"
            className="icon-button"
            onClick={() => setIsCollapsed((v) => !v)}
            title={isCollapsed ? "Expand" : "Collapse"}
            aria-label={isCollapsed ? "Expand widget" : "Collapse widget"}
          >
            {isCollapsed ? <ChevronDown size={15} /> : <ChevronUp size={15} />}
          </StandardButton>

          {/* Fullscreen overlay toggle */}
          <StandardButton
            type="button"
            className="icon-button"
            onClick={() => setIsFullscreen((v) => !v)}
            title={isFullscreen ? "Exit fullscreen (Esc)" : "Fullscreen"}
            aria-label={isFullscreen ? "Exit fullscreen" : "Fullscreen"}
          >
            {isFullscreen ? <Minimize2 size={15} /> : <Scan size={15} />}
          </StandardButton>

          {/* Action menu */}
          {!disableActions ? (
            <div className="widget-action-menu" ref={menuRef}>
              <StandardButton
                type="button"
                className="icon-button"
                onClick={() => setIsActionMenuOpen((v) => !v)}
                title="Widget actions"
                aria-label="Widget actions"
                aria-expanded={isActionMenuOpen}
              >
                <MoreVertical size={15} />
              </StandardButton>

              {isActionMenuOpen ? (
                <div className="widget-action-menu__panel" role="menu">
                  {onRename ? (
                    <StandardButton type="button" role="menuitem" onClick={() => execute(onRename)}>
                      <Edit3 size={14} />
                      Rename
                    </StandardButton>
                  ) : null}

                  {onEdit ? (
                    <StandardButton type="button" role="menuitem" onClick={() => execute(onEdit)}>
                      <Edit3 size={14} />
                      Edit widget
                    </StandardButton>
                  ) : null}

                  {onClone ? (
                    <StandardButton type="button" role="menuitem" onClick={() => execute(onClone)}>
                      <Copy size={14} />
                      Duplicate
                    </StandardButton>
                  ) : null}

                  {onHide ? (
                    <StandardButton type="button" role="menuitem" onClick={() => execute(onHide)}>
                      <EyeOff size={14} />
                      Hide
                    </StandardButton>
                  ) : null}

                  {onRemove ? (
                    <StandardButton
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
                    </StandardButton>
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
