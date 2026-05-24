/**
 * components/skeletons/Skeleton.tsx
 * --------------------------------------------------------------------
 * Skeleton placeholders for loading states.
 *
 * Use these instead of <LoadingPanel /> inside pages (dashboard widgets,
 * admin tables, detail panels). The shape of the skeleton should match
 * the shape of the data that will eventually replace it, so the transition
 * is smooth and no layout shift occurs when the data arrives.
 *
 * <SkeletonLine />        — single line, default full width
 * <SkeletonCard />        — title + body + footer line
 * <SkeletonTable />       — table with N rows
 * <SkeletonChart />       — chart area + axis lines
 * <SkeletonKpi />         — large number + label
 * <SkeletonWidgetGrid />  — full dashboard widget grid placeholder
 *
 * All animations are CSS-only — zero JS cost.
 */

import React from "react";
import "./Skeleton.css";

interface BaseProps {
  className?: string;
  style?: React.CSSProperties;
}

interface LineProps extends BaseProps {
  width?: string | number;
  height?: string | number;
}

export function SkeletonLine({ width = "100%", height = 12, className, style }: LineProps) {
  const w = typeof width === "number" ? `${width}px` : width;
  const h = typeof height === "number" ? `${height}px` : height;
  return (
    <div
      className={`ppiq-skeleton ppiq-skeleton--line ${className ?? ""}`}
      style={{ width: w, height: h, ...style }}
      role="presentation"
      aria-hidden="true"
    />
  );
}

export function SkeletonCircle({ size = 40, className, style }: BaseProps & { size?: number }) {
  return (
    <div
      className={`ppiq-skeleton ppiq-skeleton--circle ${className ?? ""}`}
      style={{ width: size, height: size, ...style }}
      role="presentation"
      aria-hidden="true"
    />
  );
}

export function SkeletonCard({ className, style }: BaseProps) {
  return (
    <div className={`ppiq-skeleton-card ${className ?? ""}`} style={style} role="presentation" aria-hidden="true">
      <SkeletonLine width="40%" height={16} />
      <SkeletonLine width="100%" height={120} className="ppiq-skeleton-card__body" />
      <SkeletonLine width="60%" />
    </div>
  );
}

export function SkeletonKpi({ className, style }: BaseProps) {
  return (
    <div className={`ppiq-skeleton-kpi ${className ?? ""}`} style={style} role="presentation" aria-hidden="true">
      <SkeletonLine width="50%" height={12} />
      <SkeletonLine width="35%" height={36} className="ppiq-skeleton-kpi__value" />
      <SkeletonLine width="65%" height={10} />
    </div>
  );
}

interface TableProps extends BaseProps {
  rows?: number;
  columns?: number;
  /** Show a header row at the top. */
  withHeader?: boolean;
}

export function SkeletonTable({
  rows = 8,
  columns = 5,
  withHeader = true,
  className,
  style,
}: TableProps) {
  return (
    <div className={`ppiq-skeleton-table ${className ?? ""}`} style={style} role="presentation" aria-hidden="true">
      {withHeader && (
        <div className="ppiq-skeleton-table__row ppiq-skeleton-table__row--header">
          {Array.from({ length: columns }).map((_, i) => (
            <SkeletonLine key={i} height={14} className="ppiq-skeleton-table__cell" />
          ))}
        </div>
      )}
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="ppiq-skeleton-table__row">
          {Array.from({ length: columns }).map((_, c) => (
            <SkeletonLine
              key={c}
              height={12}
              width={c === 0 ? "60%" : c === columns - 1 ? "40%" : "80%"}
              className="ppiq-skeleton-table__cell"
            />
          ))}
        </div>
      ))}
    </div>
  );
}

interface ChartProps extends BaseProps {
  /** Height of the chart placeholder area. */
  height?: number;
}

export function SkeletonChart({ height = 240, className, style }: ChartProps) {
  return (
    <div
      className={`ppiq-skeleton-chart ${className ?? ""}`}
      style={{ height, ...style }}
      role="presentation"
      aria-hidden="true"
    >
      <SkeletonLine width="35%" height={14} className="ppiq-skeleton-chart__title" />
      <div className="ppiq-skeleton-chart__plot">
        {/* Simulated bars / line */}
        <div className="ppiq-skeleton-chart__bars">
          {Array.from({ length: 12 }).map((_, i) => (
            <span
              key={i}
              className="ppiq-skeleton ppiq-skeleton-chart__bar"
              style={{ height: `${20 + ((i * 17) % 70)}%` }}
            />
          ))}
        </div>
      </div>
      <SkeletonLine width="60%" height={10} />
    </div>
  );
}

interface WidgetGridProps extends BaseProps {
  widgetCount?: number;
}

export function SkeletonWidgetGrid({ widgetCount = 6, className, style }: WidgetGridProps) {
  return (
    <div className={`ppiq-skeleton-widget-grid ${className ?? ""}`} style={style} role="presentation" aria-hidden="true">
      {Array.from({ length: widgetCount }).map((_, i) => {
        // Mix of card and chart shapes so the layout looks realistic
        return i % 3 === 0 ? <SkeletonChart key={i} /> : <SkeletonCard key={i} />;
      })}
    </div>
  );
}
