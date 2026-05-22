// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/Admin/AdminSharedComponents.tsx
//
// Fix 3: Extracted from the monolithic AdminPageContent.tsx (2,362 lines).
// Contains all shared primitive components and utility functions used
// across all admin tab sub-pages.
// ============================================================

import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock,
  DatabaseZap,
  FileJson,
  Layers3,
  RefreshCw,
  Settings2,
} from "lucide-react";
import type { ReactNode } from "react";

// ── Types ────────────────────────────────────────────────────────────────────

export type AdminData = {
  overview: import("@/api/plantProcessApi").AdminOverview | null;
  model: import("@/api/plantProcessApi").TwoStageImportModel | null;
  dbConfig: import("@/api/plantProcessApi").DbConfigurationSummary | null;
  schemaConfig: import("@/api/plantProcessApi").SchemaConfigurationSummary | null;
  jobs: import("@/api/plantProcessApi").AdminJobsMonitor | null;
};

export const emptyAdminData: AdminData = {
  overview: null,
  model: null,
  dbConfig: null,
  schemaConfig: null,
  jobs: null,
};

// ── AdminPanel ────────────────────────────────────────────────────────────────

export function AdminPanel({
  title,
  subtitle,
  icon,
  wide,
  children,
}: {
  title: string;
  subtitle: string;
  icon: ReactNode;
  wide?: boolean;
  children: ReactNode;
}) {
  return (
    <section className={`admin-panel ${wide ? "admin-panel--wide" : ""}`}>
      <div className="admin-panel__header">
        <div className="admin-panel__icon">{icon}</div>
        <div>
          <h2>{title}</h2>
          <p>{subtitle}</p>
        </div>
      </div>
      {children}
    </section>
  );
}

// ── MiniKpi ───────────────────────────────────────────────────────────────────

export function MiniKpi({ label, value }: { label: string; value: number }) {
  return (
    <div className="admin-mini-kpi">
      <span>{label}</span>
      <strong>{formatNumber(value)}</strong>
    </div>
  );
}

// ── EmptyAdminState ───────────────────────────────────────────────────────────

export function EmptyAdminState({ text }: { text: string }) {
  return (
    <div className="empty-insight">
      <AlertTriangle size={20} />
      <strong>No data yet</strong>
      <p>{text}</p>
    </div>
  );
}

// ── StatusPill ────────────────────────────────────────────────────────────────

export function StatusPill({
  status,
  statusClass,
}: {
  status: string;
  statusClass: string;
}) {
  return (
    <span className={`admin-status admin-status--${statusClass}`}>
      {statusClass === "success" ? <CheckCircle2 size={13} /> : null}
      {statusClass === "running" ? <RefreshCw size={13} className="spin" /> : null}
      {statusClass === "warning" || statusClass === "danger" ? (
        <AlertTriangle size={13} />
      ) : null}
      {statusClass === "info" || statusClass === "neutral" ? (
        <Clock size={13} />
      ) : null}
      {status}
    </span>
  );
}

// ── iconForGroup ──────────────────────────────────────────────────────────────

export function iconForGroup(group: string) {
  const normalized = group.toLowerCase();

  if (normalized.includes("db") || normalized.includes("source"))
    return <DatabaseZap size={18} />;

  if (normalized.includes("raw") || normalized.includes("stage"))
    return <FileJson size={18} />;

  if (normalized.includes("canonical"))
    return <Layers3 size={18} />;

  if (normalized.includes("hmi") || normalized.includes("dashboard"))
    return <Settings2 size={18} />;

  return <Activity size={18} />;
}

// ── Formatters ────────────────────────────────────────────────────────────────

export function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  try {
    return new Date(value).toLocaleString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return value;
  }
}

export function formatDuration(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  if (value < 1000) return `${value}ms`;
  if (value < 60_000) return `${(value / 1000).toFixed(1)}s`;
  return `${(value / 60_000).toFixed(1)}m`;
}

export function formatNumber(value: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(value);
}
