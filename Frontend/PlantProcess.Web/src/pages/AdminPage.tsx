import { useEffect, useMemo, useState } from "react";
import { Navigate, NavLink, Route, Routes } from "react-router-dom";
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock,
  DatabaseZap,
  FileJson,
  Layers3,
  Link2,
  PlayCircle,
  RadioTower,
  RefreshCw,
  ServerCog,
  Settings2,
  TableProperties,
  Workflow,
  Wrench,
} from "lucide-react";
import {
  plantProcessApi,
  type AdminJobsMonitor,
  type AdminMetricCard,
  type AdminOverview,
  type DbConfigurationSummary,
  type SchemaConfigurationSummary,
  type TwoStageImportModel,
} from "../api/plantProcessApi";
import { ErrorPanel, LoadingPanel } from "../components/AsyncState";

type AdminData = {
  overview: AdminOverview | null;
  model: TwoStageImportModel | null;
  dbConfig: DbConfigurationSummary | null;
  schemaConfig: SchemaConfigurationSummary | null;
  jobs: AdminJobsMonitor | null;
};

const emptyAdminData: AdminData = {
  overview: null,
  model: null,
  dbConfig: null,
  schemaConfig: null,
  jobs: null,
};

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

export function AdminPage() {
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

      setData({
        overview,
        model,
        dbConfig,
        schemaConfig,
        jobs,
      });
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  }

  useEffect(() => {
    loadAdminData();
  }, []);

  const status = data.overview?.status ?? "Loading";

  return (
    <main className="page-shell admin-shell">
      <section className="dashboard-hero admin-hero">
        <div>
          <div className="eyebrow">
            <Settings2 size={14} />
            Phase 2 administrator foundation
          </div>

          <h1>PlantProcess IQ Administrator</h1>

          <p>
            Configure how each plant connects source data, stages raw snapshots,
            maps schemas into the canonical model, and monitors refresh jobs.
          </p>

          <div className="dashboard-subtitle-row">
            <span>
              Admin status: <strong>{status}</strong>
            </span>
            <span className="status-chip">Customer discovery shell</span>
          </div>
        </div>

        <div className="dashboard-hero__actions">
          <button
            className="secondary-button"
            onClick={() => loadAdminData(true)}
            disabled={isRefreshing}
            type="button"
          >
            <RefreshCw size={16} />
            {isRefreshing ? "Refreshing..." : "Refresh Admin Data"}
          </button>
        </div>
      </section>

      <section className="admin-stage-banner">
        <div className="admin-stage-banner__icon">
          <Layers3 size={22} />
        </div>

        <div>
          <strong>Two-stage import model</strong>
          <p>
            Stage 1 copies raw source data into PlantProcess IQ staging/dump
            records. Stage 2 maps the staged records into the generic canonical
            manufacturing model used by dashboards, risk, correlation, and
            investigation workflows.
          </p>
        </div>
      </section>

      <nav className="admin-tabs" aria-label="Administrator navigation">
        {adminTabs.map((tab) => {
          const Icon = tab.icon;

          return (
            <NavLink
              key={tab.to}
              to={tab.to}
              className={({ isActive }) =>
                isActive ? "admin-tab active" : "admin-tab"
              }
            >
              <Icon size={18} />
              <div>
                <span>{tab.label}</span>
                <small>{tab.description}</small>
              </div>
            </NavLink>
          );
        })}
      </nav>

      {isLoading ? <LoadingPanel text="Loading administrator workspace..." /> : null}
      {error ? <ErrorPanel title="Could not load admin data" error={error} /> : null}

      {!isLoading && !error ? (
        <>
          <AdminOverviewCards cards={data.overview?.cards ?? []} />

          <Routes>
            <Route index element={<Navigate to="db-configuration" replace />} />
            <Route
              path="db-configuration"
              element={<DbConfigurationTab data={data.dbConfig} />}
            />
            <Route
              path="schema-configuration"
              element={<SchemaConfigurationTab data={data.schemaConfig} />}
            />
            <Route
              path="importing-data"
              element={<ImportingDataTab data={data.model} />}
            />
            <Route
              path="jobs-monitor"
              element={<JobsMonitorTab data={data.jobs} />}
            />
          </Routes>
        </>
      ) : null}
    </main>
  );
}

function AdminOverviewCards({ cards }: { cards: AdminMetricCard[] }) {
  if (!cards.length) return null;

  return (
    <section className="metric-grid admin-metric-grid">
      {cards.map((card) => (
        <div className="metric-tile admin-metric-tile" key={`${card.group}-${card.label}`}>
          <div className="metric-icon">
            {iconForGroup(card.group)}
          </div>

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

function DbConfigurationTab({ data }: { data: DbConfigurationSummary | null }) {
  return (
    <section className="admin-panel-grid">
      <AdminPanel
        title="DB Link Configuration"
        subtitle="Customer source connection concept"
        icon={<ServerCog size={18} />}
      >
        <p className="admin-copy">
          {data?.message ??
            "DB Configuration summary is not available yet."}
        </p>

        <div className="admin-provider-grid">
          {(data?.plannedProviderTypes ?? []).map((provider) => (
            <div
              key={provider.providerType}
              className={`admin-provider-card ${
                provider.recommendedForFirstDemo ? "recommended" : ""
              }`}
            >
              <div className="admin-provider-card__head">
                <strong>{provider.providerType}</strong>
                {provider.recommendedForFirstDemo ? (
                  <span className="admin-pill success">First demo</span>
                ) : (
                  <span className="admin-pill neutral">Planned</span>
                )}
              </div>

              <p>{provider.description}</p>
              <small>{provider.roadmapStatus}</small>
            </div>
          ))}
        </div>
      </AdminPanel>

      <AdminPanel
        title="Current Source Systems"
        subtitle="Existing source-system master data"
        icon={<RadioTower size={18} />}
        wide
      >
        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Type</th>
                <th>Read-only</th>
                <th>Status</th>
                <th>Batches</th>
                <th>Failed</th>
                <th>Last Import</th>
              </tr>
            </thead>
            <tbody>
              {(data?.sourceSystems ?? []).map((source) => (
                <tr key={source.id}>
                  <td><strong>{source.sourceSystemCode}</strong></td>
                  <td>{source.sourceSystemName}</td>
                  <td>{source.sourceSystemType}</td>
                  <td>{source.isReadOnlySource ? "Yes" : "No"}</td>
                  <td>
                    <StatusPill
                      status={source.isActive ? "Active" : "Inactive"}
                      statusClass={source.isActive ? "success" : "neutral"}
                    />
                  </td>
                  <td>{source.importBatchCount}</td>
                  <td>{source.failedBatchCount}</td>
                  <td>{formatDate(source.lastImportAtUtc)}</td>
                </tr>
              ))}

              {(data?.sourceSystems ?? []).length === 0 ? (
                <tr>
                  <td colSpan={8}>
                    No source systems configured yet. Add source systems through
                    the Integration API until Phase 3 DB Link UI is implemented.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </AdminPanel>
    </section>
  );
}

function SchemaConfigurationTab({
  data,
}: {
  data: SchemaConfigurationSummary | null;
}) {
  const coverage = data?.sourceObjects ?? [];
  const mappings = data?.mappings ?? [];

  return (
    <section className="admin-panel-grid">
      <AdminPanel
        title="Schema Configuration"
        subtitle="Source objects, mappings and canonical targets"
        icon={<TableProperties size={18} />}
        wide
      >
        <p className="admin-copy">
          {data?.message ??
            "Schema Configuration summary is not available yet."}
        </p>

        <div className="admin-kpi-row">
          <MiniKpi label="Mappings" value={data?.mappingCount ?? 0} />
          <MiniKpi label="Active Mappings" value={data?.activeMappingCount ?? 0} />
          <MiniKpi label="Source Objects" value={coverage.length} />
          <MiniKpi label="Target Entities" value={data?.targetCoverage.length ?? 0} />
        </div>
      </AdminPanel>

      <AdminPanel
        title="Source Object Coverage"
        subtitle="Current raw/staging source-object distribution"
        icon={<FileJson size={18} />}
      >
        <div className="admin-list">
          {coverage.map((item) => (
            <div className="admin-list-item" key={item.sourceObjectName}>
              <div>
                <strong>{item.sourceObjectName}</strong>
                <span>
                  {item.mappedRows} mapped / {item.pendingRows} pending /{" "}
                  {item.failedRows} failed
                </span>
              </div>
              <b>{item.totalRows}</b>
            </div>
          ))}

          {coverage.length === 0 ? (
            <EmptyAdminState text="No staging source objects found yet." />
          ) : null}
        </div>
      </AdminPanel>

      <AdminPanel
        title="Mapping Definitions"
        subtitle="Existing raw-to-canonical mapping metadata"
        icon={<Link2 size={18} />}
        wide
      >
        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Mapping</th>
                <th>Source Object</th>
                <th>Target Entity</th>
                <th>Version</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {mappings.map((mapping) => (
                <tr key={mapping.id}>
                  <td>
                    <strong>{mapping.mappingCode}</strong>
                    <small>{mapping.mappingName}</small>
                  </td>
                  <td>{mapping.sourceObjectName}</td>
                  <td>{mapping.targetEntityName}</td>
                  <td>{mapping.mappingVersion}</td>
                  <td>
                    <StatusPill
                      status={mapping.isActive ? "Active" : "Inactive"}
                      statusClass={mapping.isActive ? "success" : "neutral"}
                    />
                  </td>
                </tr>
              ))}

              {mappings.length === 0 ? (
                <tr>
                  <td colSpan={5}>
                    No mapping definitions found. Phase 4 will add the visual
                    schema configuration and SQL/view layer.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </AdminPanel>
    </section>
  );
}

function ImportingDataTab({ data }: { data: TwoStageImportModel | null }) {
  const stages = data?.stages ?? [];

  return (
    <section className="admin-panel-grid">
      <AdminPanel
        title="Two-Stage Import Model"
        subtitle="Raw source snapshot first, canonical refresh second"
        icon={<Layers3 size={18} />}
        wide
      >
        <p className="admin-copy">
          {data?.summary ??
            "The two-stage model is not available yet."}
        </p>

        <div className="admin-stage-flow">
          {stages.map((stage) => (
            <div className="admin-stage-card" key={stage.stageCode}>
              <div className="admin-stage-card__number">{stage.stageNo}</div>
              <div>
                <strong>{stage.stageName}</strong>
                <p>{stage.purpose}</p>
                <small>{stage.currentImplementation}</small>
              </div>
              <StatusPill
                status={stage.status}
                statusClass={stage.status === "Available" ? "success" : "warning"}
              />
            </div>
          ))}
        </div>
      </AdminPanel>

      <AdminPanel
        title="Current Import Metrics"
        subtitle="Live counts from existing integration entities"
        icon={<PlayCircle size={18} />}
        wide
      >
        <div className="metric-grid admin-metric-grid">
          {(data?.metrics ?? []).map((metric) => (
            <div className="metric-tile admin-metric-tile" key={metric.label}>
              <div className="metric-icon">
                <Workflow size={18} />
              </div>
              <div>
                <span>{metric.label}</span>
                <strong>{formatNumber(metric.value)}</strong>
                <small>{metric.note}</small>
              </div>
            </div>
          ))}
        </div>
      </AdminPanel>

      <AdminPanel
        title="Phase 3/4 Functional Expansion"
        subtitle="What becomes configurable next"
        icon={<Wrench size={18} />}
        wide
      >
        <div className="admin-roadmap-grid">
          <RoadmapCard
            title="ConnectionProfile"
            text="Store provider type, host, database/schema, connection mode and SecretReference."
          />
          <RoadmapCard
            title="SourceDatasetDefinition"
            text="Represent source tables, SQL views, CSV files, Excel sheets and REST endpoints."
          />
          <RoadmapCard
            title="SourceFieldDefinition"
            text="Store discovered source columns, types, ordinals, nullability and sample values."
          />
          <RoadmapCard
            title="Canonical Import Job"
            text="Refresh canonical records from raw snapshots at an HMI-friendly frequency."
          />
        </div>
      </AdminPanel>
    </section>
  );
}

function JobsMonitorTab({ data }: { data: AdminJobsMonitor | null }) {
  const jobs = data?.jobs ?? [];
  const summary = data?.summary ?? [];

  const sortedJobs = useMemo(
    () =>
      [...jobs].sort((a, b) => {
        const aDate = a.lastRunAtUtc ? new Date(a.lastRunAtUtc).getTime() : 0;
        const bDate = b.lastRunAtUtc ? new Date(b.lastRunAtUtc).getTime() : 0;
        return bDate - aDate;
      }),
    [jobs]
  );

  return (
    <section className="admin-panel-grid">
      <AdminPanel
        title="Jobs Monitor"
        subtitle="DB link imports, canonical refresh and future ML jobs"
        icon={<Activity size={18} />}
        wide
      >
        <div className="admin-kpi-row">
          {summary.map((item) => (
            <MiniKpi key={item.status} label={item.status} value={item.count} />
          ))}
        </div>

        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Job</th>
                <th>Type</th>
                <th>Source</th>
                <th>Status</th>
                <th>Last Run</th>
                <th>Duration</th>
                <th>Rows</th>
                <th>Runtime</th>
              </tr>
            </thead>
            <tbody>
              {sortedJobs.map((job) => (
                <tr key={job.id}>
                  <td>
                    <strong>{job.jobCode}</strong>
                    <small>{job.jobName}</small>
                    {job.errorMessage ? (
                      <em className="admin-error-text">{job.errorMessage}</em>
                    ) : null}
                  </td>
                  <td>{job.jobType}</td>
                  <td>
                    <strong>{job.sourceSystemCode}</strong>
                    <small>{job.sourceSystemName}</small>
                  </td>
                  <td>
                    <StatusPill status={job.status} statusClass={job.statusClass} />
                  </td>
                  <td>{formatDate(job.lastRunAtUtc)}</td>
                  <td>{formatDuration(job.lastDurationMs)}</td>
                  <td>{job.rowCount ?? "-"}</td>
                  <td>
                    {job.isRealRuntimeJob ? (
                      <span className="admin-pill success">Actual</span>
                    ) : (
                      <span className="admin-pill info">Configured</span>
                    )}
                  </td>
                </tr>
              ))}

              {sortedJobs.length === 0 ? (
                <tr>
                  <td colSpan={8}>
                    No job activity found yet. Import batches will appear here
                    after source data or synthetic seeds are loaded.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </AdminPanel>
    </section>
  );
}

function AdminPanel({
  title,
  subtitle,
  icon,
  wide,
  children,
}: {
  title: string;
  subtitle: string;
  icon: React.ReactNode;
  wide?: boolean;
  children: React.ReactNode;
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

function MiniKpi({ label, value }: { label: string; value: number }) {
  return (
    <div className="admin-mini-kpi">
      <span>{label}</span>
      <strong>{formatNumber(value)}</strong>
    </div>
  );
}

function RoadmapCard({ title, text }: { title: string; text: string }) {
  return (
    <div className="admin-roadmap-card">
      <strong>{title}</strong>
      <p>{text}</p>
    </div>
  );
}

function EmptyAdminState({ text }: { text: string }) {
  return (
    <div className="empty-insight">
      <AlertTriangle size={20} />
      <strong>No data yet</strong>
      <p>{text}</p>
    </div>
  );
}

function StatusPill({
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

function iconForGroup(group: string) {
  const normalized = group.toLowerCase();

  if (normalized.includes("db") || normalized.includes("source")) {
    return <DatabaseZap size={18} />;
  }

  if (normalized.includes("raw") || normalized.includes("stage")) {
    return <FileJson size={18} />;
  }

  if (normalized.includes("canonical")) {
    return <Layers3 size={18} />;
  }

  if (normalized.includes("hmi") || normalized.includes("dashboard")) {
    return <Settings2 size={18} />;
  }

  return <Activity size={18} />;
}

function formatDate(value: string | null | undefined) {
  if (!value) return "-";

  try {
    return new Date(value).toLocaleString();
  } catch {
    return value;
  }
}

function formatDuration(value: number | null | undefined) {
  if (value === null || value === undefined) return "-";

  if (value < 1000) return `${Math.round(value)} ms`;

  const seconds = value / 1000;

  if (seconds < 60) return `${seconds.toFixed(1)} s`;

  return `${(seconds / 60).toFixed(1)} min`;
}

function formatNumber(value: number) {
  return new Intl.NumberFormat(undefined, {
    maximumFractionDigits: 0,
  }).format(value);
}