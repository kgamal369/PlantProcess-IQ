import type { ReactNode } from "react";

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
   * The sizing is stated here rather than left to a class because the number is
   * the entire point of the tile and must not depend on which stylesheet wins.
   */
  variant?: "default" | "kpi";
};

export function MetricCard({ title, value, subtitle, icon, variant = "default" }: MetricCardProps) {
  if (variant === "kpi") {
    return (
      <div
        className="metric-card metric-card--kpi"
        style={{
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          height: "100%",
          minHeight: 0,
          padding: "8px 16px",
          textAlign: "center",
        }}
      >
        <strong
          style={{
            fontSize: "clamp(2rem, 7vh, 4.5rem)",
            fontWeight: 700,
            lineHeight: 1.05,
            letterSpacing: "-0.02em",
            fontVariantNumeric: "tabular-nums",
          }}
        >
          {value}
        </strong>
        {subtitle ? (
          <p
            style={{
              margin: "10px 0 0",
              fontSize: "0.75rem",
              letterSpacing: "0.08em",
              textTransform: "uppercase",
              opacity: 0.65,
            }}
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
