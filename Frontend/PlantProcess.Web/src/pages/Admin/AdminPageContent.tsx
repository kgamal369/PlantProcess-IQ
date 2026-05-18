import { Fragment, useEffect, useMemo, useState } from "react";
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
} from "lucide-react";

import {
  plantProcessApi,
  type AdminJobsMonitor,
  type AdminMetricCard,
  type AdminOverview,
  type ConnectionProfileRecord,
  type DbConfigurationSummary,
  type JobRunHistoryRecord,
  type KpiDefinitionRecord,
  type ProviderTypeRecord,
  type SchemaConfigurationSummary,
  type SchemaViewDefinitionRecord,
  type SchemaViewPreviewResult,
  type SourceDatasetDefinitionRecord,
  type SourceFieldDefinitionRecord,
  type TwoStageImportModel,
} from "@/api/plantProcessApi";

import { ErrorPanel, LoadingPanel } from "@/components/AsyncState";

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

function DbConfigurationTab({ data, onRefresh,}: { data: DbConfigurationSummary | null; onRefresh: () => Promise<void> | void;}) 
{  
   return (
    <section className="admin-panel-grid">
    <ConnectorFoundationPanel />
    <ConnectionSchedulePanel onRefresh={onRefresh} />

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

function ConnectionSchedulePanel({
  onRefresh,
}: {
  onRefresh: () => Promise<void> | void;
}) {
  const [connections, setConnections] = useState<ConnectionProfileRecord[]>([]);
  const [selectedConnectionId, setSelectedConnectionId] = useState("");
  const [intervalMinutes, setIntervalMinutes] = useState(15);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function loadConnections() {
    const result = await plantProcessApi.getConnectionProfiles(true);
    setConnections(result);

    if (!selectedConnectionId && result.length > 0) {
      setSelectedConnectionId(result[0].id);
    }
  }

  useEffect(() => {
    loadConnections();
  }, []);

  async function saveSchedule() {
    if (!selectedConnectionId) return;

    setIsSaving(true);
    setMessage(null);

    try {
      await plantProcessApi.updateConnectionImportSchedule(selectedConnectionId, {
        scheduleExpression: `Every ${intervalMinutes} minutes`,
        importIntervalMinutes: intervalMinutes,
      });

      setMessage("Import schedule saved and JobDefinition updated.");
      await loadConnections();
      await onRefresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <AdminPanel
      title="DB Link Import Scheduling"
      subtitle="Configure import cycle rate per connection profile"
      icon={<Clock size={18} />}
      wide
    >
      <div className="form-grid">
        <label>
          Connection profile
          <select
            value={selectedConnectionId}
            onChange={(event) => setSelectedConnectionId(event.target.value)}
          >
            <option value="">Select connection</option>
            {connections.map((connection) => (
              <option key={connection.id} value={connection.id}>
                {connection.connectionProfileCode} Â· {connection.connectionProfileName}
              </option>
            ))}
          </select>
        </label>

        <label>
          Import interval minutes
          <input
            type="number"
            min={2}
            max={10080}
            value={intervalMinutes}
            onChange={(event) => setIntervalMinutes(Number(event.target.value))}
          />
        </label>
      </div>

      <div className="admin-action-row">
        <button
          className="primary-button"
          type="button"
          onClick={saveSchedule}
          disabled={!selectedConnectionId || isSaving}
        >
          <Clock size={16} />
          {isSaving ? "Saving..." : "Save Import Schedule"}
        </button>

        {message ? <span className="admin-help-text">{message}</span> : null}
      </div>
    </AdminPanel>
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
     <SchemaViewBuilderPanel />

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

function ImportingDataTab({
  data,
  schemaConfig,
  jobs,
  onRefresh,
}: {
  data: TwoStageImportModel | null;
  schemaConfig: SchemaConfigurationSummary | null;
  jobs: AdminJobsMonitor | null;
  onRefresh: () => Promise<void> | void;
}) {
  const stages = data?.stages ?? [];
  const mappings = schemaConfig?.mappings ?? [];
  const canonicalJobs = (jobs?.jobs ?? []).filter(
    (job) => job.jobType === "CanonicalRefresh"
  );

  const [selectedMappingId, setSelectedMappingId] = useState("");
  const [refreshIntervalMinutes, setRefreshIntervalMinutes] = useState(15);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function saveMappingRefreshSchedule() {
    if (!selectedMappingId) return;

    setIsSaving(true);
    setMessage(null);

    try {
      await plantProcessApi.updateMappingRefreshSchedule(selectedMappingId, {
        scheduleExpression: `Every ${refreshIntervalMinutes} minutes`,
        refreshIntervalMinutes,
      });

      setMessage("Canonical refresh schedule saved and JobDefinition updated.");
      await onRefresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="admin-panel-grid">
      <AdminPanel
        title="Two-Stage Import Model"
        subtitle="Raw source snapshot first, canonical refresh second"
        icon={<Layers3 size={18} />}
        wide
      >
        <p className="admin-copy">
          {data?.summary ?? "The two-stage model is not available yet."}
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
        title="Canonical Refresh Scheduling"
        subtitle="Configure refresh cycle per MappingDefinition"
        icon={<Workflow size={18} />}
        wide
      >
        <div className="form-grid">
          <label>
            Mapping definition
            <select
              value={selectedMappingId}
              onChange={(event) => setSelectedMappingId(event.target.value)}
            >
              <option value="">Select mapping</option>
              {mappings.map((mapping) => (
                <option key={mapping.id} value={mapping.id}>
                  {mapping.mappingCode} Â· {mapping.mappingName}
                </option>
              ))}
            </select>
          </label>

          <label>
            Refresh interval minutes
            <input
              type="number"
              min={2}
              max={10080}
              value={refreshIntervalMinutes}
              onChange={(event) =>
                setRefreshIntervalMinutes(Number(event.target.value))
              }
            />
          </label>
        </div>

        <div className="admin-action-row">
          <button
            className="primary-button"
            type="button"
            onClick={saveMappingRefreshSchedule}
            disabled={!selectedMappingId || isSaving}
          >
            <Clock size={16} />
            {isSaving ? "Saving..." : "Save Refresh Schedule"}
          </button>

          {message ? <span className="admin-help-text">{message}</span> : null}
        </div>

        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Job</th>
                <th>Status</th>
                <th>Target</th>                
                <th>Last Run</th>
                <th>Duration</th>
              </tr>
            </thead>
            <tbody>
              {canonicalJobs.map((job) => (
                <tr key={job.id}>
                  <td>
                    <strong>{job.jobCode}</strong>
                    <small>{job.jobName}</small>
                  </td>
                  <td>
                    <StatusPill status={job.status} statusClass={job.statusClass} />
                  </td>
                  <td>{job.sourceSystemName}</td>
                  <td>{formatDate(job.lastRunAtUtc)}</td>
                  <td>{formatDuration(job.lastDurationMs)}</td>
                </tr>
              ))}

              {canonicalJobs.length === 0 ? (
                <tr>
                  <td colSpan={5}>
                    No canonical refresh jobs configured yet. Save a mapping refresh
                    schedule to create one.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
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
    </section>
  );
}

function JobsMonitorTab({
  data,
  onRefresh,
}: {
  data: AdminJobsMonitor | null;
  onRefresh: () => Promise<void> | void;
}) {
  const jobs = data?.jobs ?? [];
  const summary = data?.summary ?? [];

  const [expandedJobId, setExpandedJobId] = useState<string | null>(null);
  const [historyByJobId, setHistoryByJobId] = useState<Record<string, JobRunHistoryRecord[]>>({});
  const [workingJobId, setWorkingJobId] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const sortedJobs = useMemo(
    () =>
      [...jobs].sort((a, b) => {
        const aDate = a.lastRunAtUtc ? new Date(a.lastRunAtUtc).getTime() : 0;
        const bDate = b.lastRunAtUtc ? new Date(b.lastRunAtUtc).getTime() : 0;
        return bDate - aDate;
      }),
    [jobs]
  );

  async function toggleHistory(jobId: string) {
    if (expandedJobId === jobId) {
      setExpandedJobId(null);
      return;
    }

    setExpandedJobId(jobId);

    if (!historyByJobId[jobId]) {
      const history = await plantProcessApi.getJobHistory(jobId, 20);

      setHistoryByJobId((current) => ({
        ...current,
        [jobId]: history,
      }));
    }
  }

  async function runNow(jobId: string) {
    setWorkingJobId(jobId);
    setMessage(null);

    try {
      const response = await plantProcessApi.runJobNow(jobId, "Admin UI");
      setMessage(response.message);
      await onRefresh();

      const history = await plantProcessApi.getJobHistory(jobId, 20);
      setHistoryByJobId((current) => ({
        ...current,
        [jobId]: history,
      }));
    } catch (error) {
      setMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setWorkingJobId(null);
    }
  }

  async function pause(jobId: string) {
    setWorkingJobId(jobId);
    setMessage(null);

    try {
      const response = await plantProcessApi.pauseJob(jobId);
      setMessage(response.message);
      await onRefresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setWorkingJobId(null);
    }
  }

  async function resume(jobId: string) {
    setWorkingJobId(jobId);
    setMessage(null);

    try {
      const response = await plantProcessApi.resumeJob(jobId);
      setMessage(response.message);
      await onRefresh();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setWorkingJobId(null);
    }
  }

  return (
    <section className="admin-panel-grid">
      <AdminPanel
        title="Jobs Monitor"
        subtitle="DB-backed operational status, manual controls, and run history"
        icon={<Activity size={18} />}
        wide
      >
        <div className="admin-kpi-row">
          {summary.map((item) => (
            <MiniKpi key={item.status} label={item.status} value={item.count} />
          ))}
        </div>

        {message ? (
          <div className="admin-inline-message">
            {message}
          </div>
        ) : null}

        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Job</th>
                <th>Type</th>
                <th>Target</th>
                <th>Status</th>
                <th>Last Run</th>
                <th>Duration</th>
                <th>Runtime</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {sortedJobs.map((job) => {
                const isWorking = workingJobId === job.id;
                const isPaused = job.statusClass === "paused";
                const history = historyByJobId[job.id] ?? [];

                return (
                  <Fragment key={job.id}>
                    <tr>
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
                        <StatusPill
                          status={isPaused ? "Paused" : job.status}
                          statusClass={job.statusClass}
                        />
                      </td>
                      <td>{formatDate(job.lastRunAtUtc)}</td>
                      <td>{formatDuration(job.lastDurationMs)}</td>
                      <td>
                        {job.isRealRuntimeJob ? (
                          <span className="admin-pill success">Actual</span>
                        ) : (
                          <span className="admin-pill info">Configured</span>
                        )}
                      </td>
                      <td>
                        <div className="admin-action-row compact">
                          <button
                            className="secondary-button"
                            type="button"
                            disabled={isWorking || isPaused}
                            onClick={() => runNow(job.id)}
                          >
                            <PlayCircle size={14} />
                            {isWorking ? "Running..." : "Run Now"}
                          </button>

                          {isPaused ? (
                            <button
                              className="secondary-button"
                              type="button"
                              disabled={isWorking}
                              onClick={() => resume(job.id)}
                            >
                              Resume
                            </button>
                          ) : (
                            <button
                              className="secondary-button"
                              type="button"
                              disabled={isWorking}
                              onClick={() => pause(job.id)}
                            >
                              Pause
                            </button>
                          )}

                          <button
                            className="secondary-button"
                            type="button"
                            onClick={() => toggleHistory(job.id)}
                          >
                            History
                          </button>
                        </div>
                      </td>
                    </tr>

                    {expandedJobId === job.id ? (
                      <tr key={`${job.id}-history`}>
                        <td colSpan={8}>
                          <div className="job-history-panel">
                            <strong>Last Runs</strong>

                            {history.length === 0 ? (
                              <p>No run history found for this job yet.</p>
                            ) : (
                              <table>
                                <thead>
                                  <tr>
                                    <th>Status</th>
                                    <th>Started</th>
                                    <th>Completed</th>
                                    <th>Duration</th>
                                    <th>Trigger</th>
                                    <th>Message</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {history.slice(0, 5).map((item) => (
                                    <tr key={item.id}>
                                      <td>
                                        <StatusPill
                                          status={item.status}
                                          statusClass={
                                            item.status === "Ok"
                                              ? "success"
                                              : item.status === "Running"
                                                ? "running"
                                                : item.status === "Failed" ||
                                                    item.status === "Timeout"
                                                  ? "danger"
                                                  : "neutral"
                                          }
                                        />
                                      </td>
                                      <td>{formatDate(item.startedAtUtc)}</td>
                                      <td>{formatDate(item.completedAtUtc)}</td>
                                      <td>{formatDuration(item.durationMs)}</td>
                                      <td>{item.triggerSource}</td>
                                      <td>{item.failureReason ?? item.runMessage ?? "-"}</td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            )}
                          </div>
                        </td>
                      </tr>
                    ) : null}
                </Fragment>
                );
              })}

              {sortedJobs.length === 0 ? (
                <tr>
                  <td colSpan={8}>
                    No JobDefinition records found. Restart API/Workers to run
                    startup registration.
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

function SchemaViewBuilderPanel() {
  const [schemaViews, setSchemaViews] = useState<SchemaViewDefinitionRecord[]>([]);
  const [kpis, setKpis] = useState<KpiDefinitionRecord[]>([]);
  const [selectedViewId, setSelectedViewId] = useState("");
  const [preview, setPreview] = useState<SchemaViewPreviewResult | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const [viewForm, setViewForm] = useState({
    schemaViewCode: "CSV_STAGING_PREVIEW_VIEW",
    schemaViewName: "CSV Staging Preview View",
    viewKind: "SqlView",
    sqlText:
      "select sr.id, sr.source_object_name, sr.row_number, sr.processing_status, sr.raw_json\n" +
      "from staging_records sr\n" +
      "where sr.is_deleted = false\n" +
      "order by sr.row_number",
    maxPreviewRows: 50,
    timeoutSeconds: 15,
    description: "Controlled SQL view over raw staging records.",
  });

  const [kpiForm, setKpiForm] = useState({
    kpiCode: "STAGING_ROW_COUNT",
    kpiName: "Staging Row Count",
    kpiCategory: "Production",
    valueExpression: "count(*)",
    unit: "rows",
    dimensionExpression: "source_object_name",
    aggregationType: "Count",
  });

  async function loadSchemaConfig() {
    setIsBusy(true);
    setError(null);

    try {
      const [views, kpiRows] = await Promise.all([
        plantProcessApi.getSchemaViews(true),
        plantProcessApi.getKpiDefinitions(true),
      ]);

      setSchemaViews(views);
      setKpis(kpiRows);

      if (!selectedViewId && views.length > 0) {
        setSelectedViewId(views[0].id);
      }
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsBusy(false);
    }
  }

  useEffect(() => {
    loadSchemaConfig();
  }, []);

  async function createSchemaView() {
    setIsBusy(true);
    setError(null);
    setPreview(null);

    try {
      const created = await plantProcessApi.createSchemaView({
        schemaViewCode: viewForm.schemaViewCode,
        schemaViewName: viewForm.schemaViewName,
        viewKind: viewForm.viewKind,
        sqlText: viewForm.sqlText,
        sourceDatasetIdsJson: "[]",
        maxPreviewRows: viewForm.maxPreviewRows,
        timeoutSeconds: viewForm.timeoutSeconds,
        description: viewForm.description,
        isSynthetic: true,
        sourceSystem: "PlantProcessIQ.Admin",
        sourceRecordId: "PHASE4-SCHEMA-VIEW",
      });

      setSelectedViewId(created.id);
      await loadSchemaConfig();
    } catch (createError) {
      setError(createError);
    } finally {
      setIsBusy(false);
    }
  }

  async function previewAdHocSql() {
    setIsBusy(true);
    setError(null);
    setPreview(null);

    try {
      const result = await plantProcessApi.previewAdHocSchemaSql({
        sqlText: viewForm.sqlText,
        maxRows: viewForm.maxPreviewRows,
        timeoutSeconds: viewForm.timeoutSeconds,
      });

      setPreview(result);
    } catch (previewError) {
      setError(previewError);
    } finally {
      setIsBusy(false);
    }
  }

  async function previewSelectedView() {
    if (!selectedViewId) {
      setError(new Error("Select a schema view first."));
      return;
    }

    setIsBusy(true);
    setError(null);
    setPreview(null);

    try {
      const result = await plantProcessApi.previewSchemaView(selectedViewId, {
        maxRows: viewForm.maxPreviewRows,
        timeoutSeconds: viewForm.timeoutSeconds,
      });

      setPreview(result);
      await loadSchemaConfig();
    } catch (previewError) {
      setError(previewError);
    } finally {
      setIsBusy(false);
    }
  }

  async function approveSelectedView() {
    if (!selectedViewId) {
      setError(new Error("Select a schema view first."));
      return;
    }

    setIsBusy(true);
    setError(null);

    try {
      await plantProcessApi.approveSchemaView(selectedViewId);
      await loadSchemaConfig();
    } catch (approveError) {
      setError(approveError);
    } finally {
      setIsBusy(false);
    }
  }

  async function createKpi() {
    setIsBusy(true);
    setError(null);

    try {
      await plantProcessApi.createKpiDefinition({
        schemaViewDefinitionId: selectedViewId || null,
        kpiCode: kpiForm.kpiCode,
        kpiName: kpiForm.kpiName,
        kpiCategory: kpiForm.kpiCategory,
        valueExpression: kpiForm.valueExpression,
        unit: kpiForm.unit,
        dimensionExpression: kpiForm.dimensionExpression,
        aggregationType: kpiForm.aggregationType,
        kpiOptionsJson: "{}",
        description: "Created from Phase 4 Schema Configuration panel.",
        isSynthetic: true,
        sourceSystem: "PlantProcessIQ.Admin",
        sourceRecordId: "PHASE4-KPI",
      });

      await loadSchemaConfig();
    } catch (createError) {
      setError(createError);
    } finally {
      setIsBusy(false);
    }
  }

  const selectedView = schemaViews.find((x) => x.id === selectedViewId);

  return (
    <AdminPanel
      title="Phase 4 Schema View Builder"
      subtitle="Controlled SQL views, JOIN previews and KPI definitions"
      icon={<TableProperties size={18} />}
      wide
    >
      <p className="admin-copy">
        This layer converts raw/dump/staging records into customer-specific
        schema views before canonical mapping. Only safe SELECT/WITH queries are
        allowed. Destructive SQL is blocked.
      </p>

      {error ? (
        <div className="admin-inline-error">
          {error instanceof Error ? error.message : "Schema configuration action failed."}
        </div>
      ) : null}

      <div className="admin-schema-grid">
        <section className="admin-form-card admin-form-card--wide">
          <h3>Create / Preview Controlled SQL View</h3>

          <div className="admin-form-row">
            <label>
              View Code
              <input
                value={viewForm.schemaViewCode}
                onChange={(event) =>
                  setViewForm((current) => ({
                    ...current,
                    schemaViewCode: event.target.value,
                  }))
                }
              />
            </label>

            <label>
              View Name
              <input
                value={viewForm.schemaViewName}
                onChange={(event) =>
                  setViewForm((current) => ({
                    ...current,
                    schemaViewName: event.target.value,
                  }))
                }
              />
            </label>

            <label>
              View Kind
              <select
                value={viewForm.viewKind}
                onChange={(event) =>
                  setViewForm((current) => ({
                    ...current,
                    viewKind: event.target.value,
                  }))
                }
              >
                <option value="SqlView">SQL View</option>
                <option value="JoinView">Join View</option>
                <option value="KpiView">KPI View</option>
                <option value="MappingPreparationView">Mapping Preparation View</option>
              </select>
            </label>
          </div>

          <textarea
            className="admin-sql-editor"
            value={viewForm.sqlText}
            onChange={(event) =>
              setViewForm((current) => ({
                ...current,
                sqlText: event.target.value,
              }))
            }
            spellCheck={false}
          />

          <div className="admin-action-row">
            <button
              className="secondary-button"
              onClick={previewAdHocSql}
              disabled={isBusy || !viewForm.sqlText.trim()}
              type="button"
            >
              Preview SQL
            </button>

            <button
              className="primary-button"
              onClick={createSchemaView}
              disabled={isBusy || !viewForm.schemaViewCode.trim()}
              type="button"
            >
              Save Schema View
            </button>
          </div>
        </section>
      </div>

      <div className="admin-schema-grid">
        <section className="admin-form-card">
          <h3>Stored Schema Views</h3>

          <label>
            Select View
            <select
              value={selectedViewId}
              onChange={(event) => setSelectedViewId(event.target.value)}
            >
              <option value="">Select schema view...</option>
              {schemaViews.map((view) => (
                <option key={view.id} value={view.id}>
                  {view.schemaViewCode} â€” {view.viewKind}
                </option>
              ))}
            </select>
          </label>

          {selectedView ? (
            <div className="admin-selected-hint">
              <strong>{selectedView.schemaViewName}</strong>
              <br />
              Status: {selectedView.lastValidationStatus ?? "Not validated"} Â·
              Approved: {selectedView.isApproved ? "Yes" : "No"}
              <br />
              {selectedView.lastValidationMessage ?? ""}
            </div>
          ) : null}

          <div className="admin-action-row">
            <button
              className="secondary-button"
              onClick={previewSelectedView}
              disabled={isBusy || !selectedViewId}
              type="button"
            >
              Preview Selected
            </button>

            <button
              className="secondary-button"
              onClick={approveSelectedView}
              disabled={isBusy || !selectedViewId}
              type="button"
            >
              Approve View
            </button>
          </div>
        </section>

        <section className="admin-form-card">
          <h3>Create KPI Definition</h3>

          <label>
            KPI Code
            <input
              value={kpiForm.kpiCode}
              onChange={(event) =>
                setKpiForm((current) => ({
                  ...current,
                  kpiCode: event.target.value,
                }))
              }
            />
          </label>

          <label>
            KPI Name
            <input
              value={kpiForm.kpiName}
              onChange={(event) =>
                setKpiForm((current) => ({
                  ...current,
                  kpiName: event.target.value,
                }))
              }
            />
          </label>

          <label>
            Value Expression
            <input
              value={kpiForm.valueExpression}
              onChange={(event) =>
                setKpiForm((current) => ({
                  ...current,
                  valueExpression: event.target.value,
                }))
              }
            />
          </label>

          <button
            className="primary-button"
            onClick={createKpi}
            disabled={isBusy || !kpiForm.kpiCode.trim()}
            type="button"
          >
            Create KPI
          </button>
        </section>
      </div>

      {preview ? (
        <section className="admin-preview-panel">
          <div className="admin-panel__header">
            <div className="admin-panel__icon">
              <PlayCircle size={18} />
            </div>
            <div>
              <h2>Preview Result</h2>
              <p>
                {preview.message} Â· {preview.durationMs} ms Â· {preview.rowCount} rows
              </p>
            </div>
          </div>

          {preview.columns.length > 0 ? (
            <div className="admin-table-wrap">
              <table>
                <thead>
                  <tr>
                    {preview.columns.map((column) => (
                      <th key={column.columnName}>
                        {column.columnName}
                        <small>{column.dataType}</small>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {preview.rows.map((row, index) => (
                    <tr key={index}>
                      {preview.columns.map((column) => (
                        <td key={column.columnName}>
                          {String(row[column.columnName] ?? "-")}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </section>
      ) : null}

      <div className="admin-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Schema View</th>
              <th>Kind</th>
              <th>Status</th>
              <th>Approved</th>
              <th>Max Rows</th>
              <th>Last Validation</th>
            </tr>
          </thead>
          <tbody>
            {schemaViews.map((view) => (
              <tr key={view.id}>
                <td>
                  <strong>{view.schemaViewCode}</strong>
                  <small>{view.schemaViewName}</small>
                </td>
                <td>{view.viewKind}</td>
                <td>
                  <StatusPill
                    status={view.lastValidationStatus ?? "NotValidated"}
                    statusClass={view.lastValidationStatus === "Success" ? "success" : "warning"}
                  />
                </td>
                <td>{view.isApproved ? "Yes" : "No"}</td>
                <td>{view.maxPreviewRows}</td>
                <td>{formatDate(view.lastValidatedAtUtc)}</td>
              </tr>
            ))}

            {schemaViews.length === 0 ? (
              <tr>
                <td colSpan={6}>
                  No schema views yet. Create the first controlled SQL view above.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      {kpis.length > 0 ? (
        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>KPI</th>
                <th>Category</th>
                <th>Expression</th>
                <th>Unit</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {kpis.map((kpi) => (
                <tr key={kpi.id}>
                  <td>
                    <strong>{kpi.kpiCode}</strong>
                    <small>{kpi.kpiName}</small>
                  </td>
                  <td>{kpi.kpiCategory}</td>
                  <td>{kpi.valueExpression}</td>
                  <td>{kpi.unit ?? "-"}</td>
                  <td>
                    <StatusPill
                      status={kpi.isActive ? "Active" : "Inactive"}
                      statusClass={kpi.isActive ? "success" : "neutral"}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </AdminPanel>
  );
}

function ConnectorFoundationPanel() {
  const [providers, setProviders] = useState<ProviderTypeRecord[]>([]);
  const [profiles, setProfiles] = useState<ConnectionProfileRecord[]>([]);
  const [datasets, setDatasets] = useState<SourceDatasetDefinitionRecord[]>([]);
  const [selectedProfileId, setSelectedProfileId] = useState("");
  const [selectedDatasetId, setSelectedDatasetId] = useState("");
  const [csvText, setCsvText] = useState("");
  const [fields, setFields] = useState<SourceFieldDefinitionRecord[]>([]);
  const [previewRows, setPreviewRows] = useState<Record<string, string | null>[]>([]);
  const [lastImportResult, setLastImportResult] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const [profileForm, setProfileForm] = useState({
    sourceSystemDefinitionId: "",
    connectionProfileCode: "CSV_DEMO_PROFILE",
    connectionProfileName: "CSV Demo Snapshot Connection",
    providerType: "Csv",
    fileRootPath: "manual-upload",
    description: "CSV snapshot connector created from Admin DB Configuration.",
  });

  const [datasetForm, setDatasetForm] = useState({
    datasetCode: "CSV_DEMO_DATASET",
    datasetName: "CSV Demo Dataset",
    datasetKind: "CsvFile",
    sourceObjectName: "csv_demo_dataset",
    refreshIntervalSeconds: 300,
  });

  async function loadConnectorData() {
    setIsBusy(true);
    setError(null);

    try {
      const [providerRows, profileRows, datasetRows] = await Promise.all([
        plantProcessApi.getConnectorProviderTypes(),
        plantProcessApi.getConnectionProfiles(true),
        plantProcessApi.getSourceDatasets(undefined, true),
      ]);

      setProviders(providerRows);
      setProfiles(profileRows);
      setDatasets(datasetRows);

      if (!selectedProfileId && profileRows.length > 0) {
        setSelectedProfileId(profileRows[0].id);
      }

      if (!selectedDatasetId && datasetRows.length > 0) {
        setSelectedDatasetId(datasetRows[0].id);
      }
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsBusy(false);
    }
  }

  useEffect(() => {
    loadConnectorData();
  }, []);

  async function createCsvProfile() {
    if (!profileForm.sourceSystemDefinitionId) {
      setError(new Error("Select or paste a SourceSystemDefinitionId first."));
      return;
    }

    setIsBusy(true);
    setError(null);

    try {
      const created = await plantProcessApi.createConnectionProfile({
        sourceSystemDefinitionId: profileForm.sourceSystemDefinitionId,
        connectionProfileCode: profileForm.connectionProfileCode,
        connectionProfileName: profileForm.connectionProfileName,
        providerType: profileForm.providerType,
        connectionMode: "Snapshot",
        fileRootPath: profileForm.fileRootPath,
        connectionOptionsJson: "{}",
        readOnlyEnforced: true,
        description: profileForm.description,
        isSynthetic: true,
        sourceSystem: "PlantProcessIQ.Admin",
        sourceRecordId: "PHASE3-CSV-PROFILE",
      });

      setSelectedProfileId(created.id);
      await loadConnectorData();
    } catch (createError) {
      setError(createError);
    } finally {
      setIsBusy(false);
    }
  }

  async function testProfile(id: string) {
    setIsBusy(true);
    setError(null);

    try {
      await plantProcessApi.testConnectionProfile(id);
      await loadConnectorData();
    } catch (testError) {
      setError(testError);
    } finally {
      setIsBusy(false);
    }
  }

  async function createDataset() {
    if (!selectedProfileId) {
      setError(new Error("Create or select a connection profile first."));
      return;
    }

    setIsBusy(true);
    setError(null);

    try {
      const created = await plantProcessApi.createSourceDataset({
        connectionProfileId: selectedProfileId,
        datasetCode: datasetForm.datasetCode,
        datasetName: datasetForm.datasetName,
        datasetKind: datasetForm.datasetKind,
        sourceObjectName: datasetForm.sourceObjectName,
        refreshIntervalSeconds: datasetForm.refreshIntervalSeconds,
        datasetOptionsJson: "{}",
        description: "Created from Phase 3 Admin connector panel.",
        isSynthetic: true,
        sourceSystem: "PlantProcessIQ.Admin",
        sourceRecordId: "PHASE3-CSV-DATASET",
      });

      setSelectedDatasetId(created.id);
      await loadConnectorData();
    } catch (createError) {
      setError(createError);
    } finally {
      setIsBusy(false);
    }
  }

  async function discoverSchema() {
    if (!selectedDatasetId) {
      setError(new Error("Create or select a source dataset first."));
      return;
    }

    setIsBusy(true);
    setError(null);

    try {
      const result = await plantProcessApi.discoverCsvSchema(selectedDatasetId, {
        csvText,
        fileName: "admin-sample.csv",
        delimiter: ",",
        hasHeader: true,
        maxRowsToAnalyze: 100,
        persistFields: true,
      });

      setFields(result.fields);
    } catch (discoverError) {
      setError(discoverError);
    } finally {
      setIsBusy(false);
    }
  }

  async function previewCsv() {
    if (!selectedDatasetId) {
      setError(new Error("Create or select a source dataset first."));
      return;
    }

    setIsBusy(true);
    setError(null);

    try {
      const result = await plantProcessApi.previewCsv(selectedDatasetId, {
        csvText,
        delimiter: ",",
        hasHeader: true,
        maxRows: 10,
      });

      setPreviewRows(result.rows);
    } catch (previewError) {
      setError(previewError);
    } finally {
      setIsBusy(false);
    }
  }

  async function importCsvSnapshot() {
    if (!selectedDatasetId) {
      setError(new Error("Create or select a source dataset first."));
      return;
    }

    setIsBusy(true);
    setError(null);
    setLastImportResult(null);

    try {
      const result = await plantProcessApi.importCsvSnapshot(selectedDatasetId, {
        csvText,
        fileName: "admin-import.csv",
        delimiter: ",",
        hasHeader: true,
        isSynthetic: true,
        sourceSystem: "PlantProcessIQ.AdminCsvConnector",
        sourceRecordId: "PHASE3-CSV-IMPORT",
      });

      setLastImportResult(
        `${result.status}: imported ${result.rowCount} rows into ImportBatch ${result.importBatchCode}`
      );
    } catch (importError) {
      setError(importError);
    } finally {
      setIsBusy(false);
    }
  }

  const selectedProfile = profiles.find((x) => x.id === selectedProfileId);
  const selectedDataset = datasets.find((x) => x.id === selectedDatasetId);

  return (
    <AdminPanel
      title="Phase 3 Connector Foundation"
      subtitle="Connection profiles, CSV schema discovery and raw snapshot import"
      icon={<DatabaseZap size={18} />}
      wide
    >
      <p className="admin-copy">
        This panel makes the Admin DB Configuration page functional for early
        demo-based sales. CSV is active now; Excel, PostgreSQL, SQL Server and
        Oracle are registered as planned provider types but are not promised as
        live connectors yet.
      </p>

      {error ? (
        <div className="admin-inline-error">
          {error instanceof Error ? error.message : "Connector action failed."}
        </div>
      ) : null}

      {lastImportResult ? (
        <div className="admin-inline-success">{lastImportResult}</div>
      ) : null}

      <div className="admin-provider-grid">
        {providers.map((provider) => (
          <div
            key={provider.providerType}
            className={`admin-provider-card ${
              provider.isAvailableNow ? "recommended" : ""
            }`}
          >
            <div className="admin-provider-card__head">
              <strong>{provider.displayName}</strong>
              {provider.isAvailableNow ? (
                <span className="admin-pill success">Available</span>
              ) : (
                <span className="admin-pill neutral">Planned</span>
              )}
            </div>

            <p>{provider.description}</p>
            <small>
              Discovery: {provider.supportsSchemaDiscovery ? "yes" : "no"} Â·
              Snapshot: {provider.supportsSnapshotImport ? "yes" : "no"}
            </small>
          </div>
        ))}
      </div>

      <div className="admin-connector-grid">
        <section className="admin-form-card">
          <h3>Create CSV Connection Profile</h3>

          <label>
            SourceSystemDefinitionId
            <input
              value={profileForm.sourceSystemDefinitionId}
              onChange={(event) =>
                setProfileForm((current) => ({
                  ...current,
                  sourceSystemDefinitionId: event.target.value,
                }))
              }
              placeholder="Paste existing source system ID"
            />
          </label>

          <label>
            Profile Code
            <input
              value={profileForm.connectionProfileCode}
              onChange={(event) =>
                setProfileForm((current) => ({
                  ...current,
                  connectionProfileCode: event.target.value,
                }))
              }
            />
          </label>

          <label>
            Profile Name
            <input
              value={profileForm.connectionProfileName}
              onChange={(event) =>
                setProfileForm((current) => ({
                  ...current,
                  connectionProfileName: event.target.value,
                }))
              }
            />
          </label>

          <label>
            File Root / Source Hint
            <input
              value={profileForm.fileRootPath}
              onChange={(event) =>
                setProfileForm((current) => ({
                  ...current,
                  fileRootPath: event.target.value,
                }))
              }
            />
          </label>

          <button
            className="primary-button"
            onClick={createCsvProfile}
            disabled={isBusy}
            type="button"
          >
            Create CSV Profile
          </button>
        </section>

        <section className="admin-form-card">
          <h3>Create Source Dataset</h3>

          <label>
            Connection Profile
            <select
              value={selectedProfileId}
              onChange={(event) => setSelectedProfileId(event.target.value)}
            >
              <option value="">Select profile...</option>
              {profiles.map((profile) => (
                <option key={profile.id} value={profile.id}>
                  {profile.connectionProfileCode} â€” {profile.providerType}
                </option>
              ))}
            </select>
          </label>

          <label>
            Dataset Code
            <input
              value={datasetForm.datasetCode}
              onChange={(event) =>
                setDatasetForm((current) => ({
                  ...current,
                  datasetCode: event.target.value,
                }))
              }
            />
          </label>

          <label>
            Dataset Name
            <input
              value={datasetForm.datasetName}
              onChange={(event) =>
                setDatasetForm((current) => ({
                  ...current,
                  datasetName: event.target.value,
                }))
              }
            />
          </label>

          <label>
            Source Object Name
            <input
              value={datasetForm.sourceObjectName}
              onChange={(event) =>
                setDatasetForm((current) => ({
                  ...current,
                  sourceObjectName: event.target.value,
                }))
              }
            />
          </label>

          <button
            className="primary-button"
            onClick={createDataset}
            disabled={isBusy || !selectedProfileId}
            type="button"
          >
            Create Dataset
          </button>
        </section>
      </div>

      <div className="admin-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Profile</th>
              <th>Provider</th>
              <th>Source System</th>
              <th>Status</th>
              <th>Last Test</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {profiles.map((profile) => (
              <tr key={profile.id}>
                <td>
                  <strong>{profile.connectionProfileCode}</strong>
                  <small>{profile.connectionProfileName}</small>
                </td>
                <td>{profile.providerType}</td>
                <td>
                  <strong>{profile.sourceSystemCode}</strong>
                  <small>{profile.sourceSystemName}</small>
                </td>
                <td>
                  <StatusPill
                    status={profile.isActive ? "Active" : "Inactive"}
                    statusClass={profile.isActive ? "success" : "neutral"}
                  />
                </td>
                <td>
                  {profile.lastTestStatus ?? "-"}
                  <small>{profile.lastTestMessage ?? ""}</small>
                </td>
                <td>
                  <button
                    className="secondary-button compact-button"
                    onClick={() => testProfile(profile.id)}
                    disabled={isBusy}
                    type="button"
                  >
                    Test
                  </button>
                </td>
              </tr>
            ))}

            {profiles.length === 0 ? (
              <tr>
                <td colSpan={6}>
                  No connection profiles yet. Create a CSV profile above.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      <div className="admin-connector-grid">
        <section className="admin-form-card admin-form-card--wide">
          <h3>CSV Preview / Schema Discovery / Snapshot Import</h3>

          <label>
            Dataset
            <select
              value={selectedDatasetId}
              onChange={(event) => setSelectedDatasetId(event.target.value)}
            >
              <option value="">Select dataset...</option>
              {datasets.map((dataset) => (
                <option key={dataset.id} value={dataset.id}>
                  {dataset.datasetCode} â€” {dataset.sourceObjectName}
                </option>
              ))}
            </select>
          </label>

          <textarea
            className="admin-csv-textarea"
            value={csvText}
            onChange={(event) => setCsvText(event.target.value)}
            placeholder={"material_code,grade,temp,defect\nM001,G1,1210,No\nM002,G1,1260,Yes"}
          />

          <div className="admin-action-row">
            <button
              className="secondary-button"
              onClick={previewCsv}
              disabled={isBusy || !selectedDatasetId || !csvText.trim()}
              type="button"
            >
              Preview CSV
            </button>

            <button
              className="secondary-button"
              onClick={discoverSchema}
              disabled={isBusy || !selectedDatasetId || !csvText.trim()}
              type="button"
            >
              Discover Schema
            </button>

            <button
              className="primary-button"
              onClick={importCsvSnapshot}
              disabled={isBusy || !selectedDatasetId || !csvText.trim()}
              type="button"
            >
              Import Snapshot
            </button>
          </div>

          <div className="admin-selected-hint">
            <strong>Selected profile:</strong>{" "}
            {selectedProfile
              ? `${selectedProfile.connectionProfileCode} (${selectedProfile.providerType})`
              : "none"}{" "}
            Â· <strong>Selected dataset:</strong>{" "}
            {selectedDataset
              ? `${selectedDataset.datasetCode} (${selectedDataset.sourceObjectName})`
              : "none"}
          </div>
        </section>
      </div>

      {fields.length > 0 ? (
        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Ordinal</th>
                <th>Field</th>
                <th>Type</th>
                <th>Nullable</th>
                <th>Key?</th>
                <th>Timestamp?</th>
                <th>Sample</th>
              </tr>
            </thead>
            <tbody>
              {fields.map((field) => (
                <tr key={`${field.fieldName}-${field.ordinal}`}>
                  <td>{field.ordinal}</td>
                  <td><strong>{field.fieldName}</strong></td>
                  <td>{field.sourceDataType}</td>
                  <td>{field.isNullable ? "Yes" : "No"}</td>
                  <td>{field.isPrimaryKeyCandidate ? "Yes" : "No"}</td>
                  <td>{field.isTimestampCandidate ? "Yes" : "No"}</td>
                  <td>{field.sampleValue ?? "-"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {previewRows.length > 0 ? (
        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                {Object.keys(previewRows[0]).map((key) => (
                  <th key={key}>{key}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {previewRows.map((row, index) => (
                <tr key={index}>
                  {Object.keys(previewRows[0]).map((key) => (
                    <td key={key}>{row[key] ?? "-"}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </AdminPanel>
  );
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

