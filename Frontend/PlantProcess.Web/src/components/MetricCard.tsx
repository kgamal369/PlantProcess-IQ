import type { ReactNode } from "react";

type MetricCardProps = {
  title: string;
  value: string | number;
  subtitle?: string;
  icon?: ReactNode;
};

export function MetricCard({ title, value, subtitle, icon }: MetricCardProps) {
  return (
    <div className="metric-card">
      <div className="metric-header">
        <span>{title}</span>
        {icon && <div className="metric-icon">{icon}</div>}
      </div>
      <strong>{value}</strong>
      {subtitle && <p>{subtitle}</p>}
    </div>
  );
}