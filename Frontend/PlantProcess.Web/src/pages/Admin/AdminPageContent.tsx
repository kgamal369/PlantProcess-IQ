// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/Admin/AdminPageContent.tsx
//
// Fix 3: Refactored from 2,362-line monolith into a clean shell that
// owns only routing + top-level data loading.
// All tab implementations moved to dedicated sub-component files.
//
// Sub-component files to create alongside this file:
//   AdminSharedComponents.tsx   ← shared helpers (already generated)
//   AdminDbConfigurationTab.tsx ← DbConfigurationTab + ConnectionSchedulePanel + ConnectorFoundationPanel
//   AdminSchemaConfigurationTab.tsx ← SchemaConfigurationTab + SchemaViewBuilderPanel
//   AdminImportingDataTab.tsx   ← ImportingDataTab
//   AdminJobsMonitorTab.tsx     ← JobsMonitorTab
// ============================================================

import { useEffect, useState } from "react";
import { Navigate, NavLink, Route, Routes } from "react-router-dom";
import {
  Activity,
  DatabaseZap,
  RefreshCw,
  Settings2,
  TableProperties,
  Workflow,
} from "lucide-react";

import { plantProcessApi } from "@/api/plantProcessApi";
import { ErrorPanel, LoadingPanel } from "@/components/AsyncState";

import { emptyAdminData, iconForGroup, formatNumber, type AdminData } from "./AdminSharedComponents";
import { DbConfigurationTab } from "./AdminDbConfigurationTab";
import { SchemaConfigurationTab } from "./AdminSchemaConfigurationTab";
import { ImportingDataTab } from "./AdminImportingDataTab";
import { JobsMonitorTab } from "./AdminJobsMonitorTab";
import type { AdminMetricCard } from "@/api/plantProcessApi";

// ── Tab navigation config ────────────────────────────────────────────────────

const adminTabs = [
  {
    to: "/admin/db-configuration",
    label: "DB Configuration",
    description: "DB links and raw source snapshots",
    icon: DatabaseZap,
  },
  {
    to: "/admin/schema-configuration",
    label: "Schema Configuration",
    description: "Mappings, views and canonical refresh",
    icon: TableProperties,
  },
  {
    to: "/admin/importing-data",
    label: "Importing Data",
    description: "Two-stage raw-to-canonical model",
    icon: Workflow,
  },
  {
    to: "/admin/jobs-monitor",
    label: "Jobs Monitor",
    description: "Import, canonical and ML jobs",
    icon: Activity,
  },
];

// ── AdminPageContent (main shell) ────────────────────────────────────────────

export function AdminPageContent() {
  const [data, setData] = useState<AdminData>(emptyAdminData);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState<unknown>(null);

  async function loadAdminData(isManualRefresh = false) {
    if (isManualRefresh) {
      setIsRefreshing(true);
    } else {
      setIsLoading(true);
    }
    setError(null);

    try {
      const [overview, model, dbConfig, schemaConfig, jobs] = await Promise.all([
        plantProcessApi.getAdminOverview(),
        plantProcessApi.getAdminTwoStageImportModel(),
        plantProcessApi.getAdminDbConfigurationSummary(),
        plantProcessApi.getAdminSchemaConfigurationSummary(),
        plantProcessApi.getAdminJobsMonitor(),
      ]);

      setData({ overview, model, dbConfig, schemaConfig, jobs });
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  }

  useEffect(() => { loadAdminData(); }, []);

  const status = data.overview?.status ?? "Loading";

  return (
    <main className="page-shell admin-shell">

      {/* ── Header ─────────────────────────────────────────────────── */}
      <section className="dashboard-hero admin-hero">
        <div>
          <div className="eyebrow">
            <Settings2 size={14} />
            Administrator
          </div>
          <h1>PlantProcess IQ Administrator</h1>
          <p>
            Configure how each plant connects source data, stages raw snapshots,
            maps schemas into the canonical model, and monitors refresh jobs.
          </p>

          <div className="dashboard-subtitle-row">
            <span>
              Platform status:{" "}
              <strong className={status === "Healthy" ? "text-success" : "text-warning"}>
                {status}
              </strong>
            </span>
            <button
              className="secondary-button"
              onClick={() => loadAdminData(true)}
              disabled={isRefreshing}
              type="button"
            >
              <RefreshCw size={14} className={isRefreshing ? "spin" : undefined} />
              {isRefreshing ? "Refreshing…" : "Refresh"}
            </button>
          </div>
        </div>
      </section>

      {/* ── Overview KPI strip ─────────────────────────────────────── */}
      {data.overview?.cards?.length ? (
        <AdminOverviewCards cards={data.overview.cards} />
      ) : null}

      {/* ── Stage banner ───────────────────────────────────────────── */}
      <section className="admin-stage-banner">
        <nav className="admin-tab-nav">
          {adminTabs.map((tab) => (
            <NavLink
              key={tab.to}
              to={tab.to}
              className={({ isActive }) =>
                `admin-tab-link ${isActive ? "admin-tab-link--active" : ""}`
              }
            >
              <tab.icon size={16} />
              <span>{tab.label}</span>
              <small>{tab.description}</small>
            </NavLink>
          ))}
        </nav>
      </section>

      {/* ── Loading / Error states ─────────────────────────────────── */}
      {isLoading ? <LoadingPanel /> : null}
      {error ? <ErrorPanel error={error} /> : null}

      {/* ── Tab routes ─────────────────────────────────────────────── */}
      {!isLoading && !error ? (
        <Routes>
          <Route index element={<Navigate to="db-configuration" replace />} />

          <Route
            path="db-configuration"
            element={
              <DbConfigurationTab
                data={data.dbConfig}
                onRefresh={() => loadAdminData(true)}
              />
            }
          />

          <Route
            path="schema-configuration"
            element={<SchemaConfigurationTab data={data.schemaConfig} />}
          />

          <Route
            path="importing-data"
            element={
              <ImportingDataTab
                data={data.model}
                schemaConfig={data.schemaConfig}
                jobs={data.jobs}
                onRefresh={() => loadAdminData(true)}
              />
            }
          />

          <Route
            path="jobs-monitor"
            element={
              <JobsMonitorTab
                data={data.jobs}
                onRefresh={() => loadAdminData(true)}
              />
            }
          />
        </Routes>
      ) : null}
    </main>
  );
}

// ── AdminOverviewCards ───────────────────────────────────────────────────────

function AdminOverviewCards({ cards }: { cards: AdminMetricCard[] }) {
  if (!cards.length) return null;

  return (
    <section className="metric-grid admin-metric-grid">
      {cards.map((card) => (
        <div
          className="metric-tile admin-metric-tile"
          key={`${card.group}-${card.label}`}
        >
          <div className="metric-icon">{iconForGroup(card.group)}</div>
          <div>
            <span>{card.label}</span>
            <strong>{formatNumber(card.value)}</strong>
            <small>{card.note}</small>
          </div>
        </div>
      ))}
    </section>
  );
}
