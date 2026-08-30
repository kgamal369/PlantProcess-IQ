import type { ReactNode } from "react";
import "./MetricCard.css";

type MetricCardProps = {
  /** Optional. Omit it when the surrounding card already shows the name. */
  title?: string;
  value: string | number;
  subtitle?: string;
  icon?: ReactNode;
  /**
   * DEMO-BI-R1. Presentation variant for a dashboard KPI tile.
   *
   * The widget card above this already carries the title and the
   * "kpi - dimension - measure" line, so a KPI tile that repeats both reads as
   * a form rather than a figure. In this variant the number is the content:
   * large, centred in the space the tile owns, with nothing competing.
   *
   * T-250/F4: the sizing lives in MetricCard.css. It was inline until the D2
   * ratchet caught it, and the values were constants, so nothing was lost.
   */
  variant?: "default" | "kpi";
};

export function MetricCard({ title, value, subtitle, icon, variant = "default" }: MetricCardProps) {
  if (variant === "kpi") {
    return (
      <div
        className="metric-card metric-card--kpi"
      >
        <strong
          className="metric-card__kpi-value"
        >
          {value}
        </strong>
        {subtitle ? (
          <p
            className="metric-card__kpi-subtitle"
          >
            {subtitle}
          </p>
        ) : null}
      </div>
    );
  }

  return (
    <div className="metric-card">
      <div className="metric-header">
        {title ? <span>{title}</span> : null}
        {icon && <div className="metric-icon">{icon}</div>}
      </div>
      <strong>{value}</strong>
      {subtitle && <p>{subtitle}</p>}
    </div>
  );
}
