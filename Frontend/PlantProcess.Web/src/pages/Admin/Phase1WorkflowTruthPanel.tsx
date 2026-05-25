import { useEffect, useMemo, useState } from "react";
import {
  phase1WorkflowApi,
  type ConnectorTruthMatrixResponse,
  type ImportJobConfigurationBoardResponse,
  type SchemaMappingWorkbenchResponse,
  type SourceScheduleBoardResponse,
  type StagingSummaryResponse,
} from "@/api/phase1/phase1Workflow.api";

type LoadState = "idle" | "loading" | "ready" | "error";

export default function Phase1WorkflowTruthPanel() {
  const [state, setState] = useState<LoadState>("idle");
  const [error, setError] = useState<string | null>(null);

  const [connectorTruth, setConnectorTruth] =
    useState<ConnectorTruthMatrixResponse | null>(null);

  const [scheduleBoard, setScheduleBoard] =
    useState<SourceScheduleBoardResponse | null>(null);

  const [stagingSummary, setStagingSummary] =
    useState<StagingSummaryResponse | null>(null);

  const [workbench, setWorkbench] =
    useState<SchemaMappingWorkbenchResponse | null>(null);

  const [importJobs, setImportJobs] =
    useState<ImportJobConfigurationBoardResponse | null>(null);

  const dueRows = useMemo(
    () => scheduleBoard?.rows.filter((x) => x.isDueNow) ?? [],
    [scheduleBoard]
  );

  async function load() {
    setState("loading");
    setError(null);

    try {
      const [
        connectorTruthResult,
        scheduleBoardResult,
        stagingSummaryResult,
        workbenchResult,
        importJobsResult,
      ] = await Promise.all([
        phase1WorkflowApi.getConnectorTruth(),
        phase1WorkflowApi.getSourceScheduleBoard(),
        phase1WorkflowApi.getStagingSummary(),
        phase1WorkflowApi.getSchemaMappingWorkbench(),
        phase1WorkflowApi.getImportJobConfigurationBoard(),
      ]);

      setConnectorTruth(connectorTruthResult);
      setScheduleBoard(scheduleBoardResult);
      setStagingSummary(stagingSummaryResult);
      setWorkbench(workbenchResult);
      setImportJobs(importJobsResult);
      setState("ready");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown Phase 1 load error");
      setState("error");
    }
  }

  async function runDueImports() {
    setState("loading");
    setError(null);

    try {
      await phase1WorkflowApi.runDueSourceImports(25, 5000);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Run due imports failed");
      setState("error");
    }
  }

  useEffect(() => {
    void load();
  }, []);

  return (
    <section className="admin-panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Phase 1</p>
          <h2>Golden Demo & Workflow Truth</h2>
          <p>
            Connector truth, source scheduling, staging copy, schema mapping,
            and canonical import job configuration.
          </p>
        </div>

        <div className="admin-action-row">
          <button className="secondary-button" type="button" onClick={() => void load()}>
            Refresh
          </button>
          <button
            className="primary-button"
            type="button"
            disabled={state === "loading"}
            onClick={() => void runDueImports()}
          >
            {state === "loading" ? "Working…" : "Run due source imports"}
          </button>
        </div>
      </div>

      {error ? <div className="admin-alert danger">{error}</div> : null}

      <div className="admin-grid">
        <Metric label="Connectors" value={connectorTruth?.providers.length ?? 0} />
        <Metric label="Available now" value={connectorTruth?.providers.filter((x) => x.isAvailableNow).length ?? 0} />
        <Metric label="Source datasets" value={scheduleBoard?.totalDatasets ?? 0} />
        <Metric label="Due now" value={scheduleBoard?.dueNowDatasets ?? 0} />
        <Metric label="Staging batches" value={stagingSummary?.rows.length ?? 0} />
        <Metric label="Mappings" value={workbench?.mappings.length ?? 0} />
        <Metric label="Import jobs" value={importJobs?.existingImportJobs.length ?? 0} />
      </div>

      <div className="admin-preview-panel">
        <h3>Connector Truth Matrix</h3>
        <table>
          <thead>
            <tr>
              <th>Provider</th>
              <th>Status</th>
              <th>Available</th>
              <th>Implemented</th>
              <th>Demo Certified</th>
              <th>Profiles</th>
              <th>Datasets</th>
              <th>Limitation</th>
            </tr>
          </thead>
          <tbody>
            {connectorTruth?.providers.map((provider) => (
              <tr key={provider.providerType}>
                <td>{provider.displayName}</td>
                <td>{provider.statusLabel}</td>
                <td>{provider.isAvailableNow ? "Yes" : "No"}</td>
                <td>{provider.isImplemented ? "Yes" : "No"}</td>
                <td>{provider.isDemoCertified ? "Yes" : "No"}</td>
                <td>
                  {provider.activeConnectionProfiles}/{provider.totalConnectionProfiles}
                </td>
                <td>
                  {provider.activeSourceDatasets}/{provider.totalSourceDatasets}
                </td>
                <td>{provider.limitation}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="admin-preview-panel">
        <h3>DB-Driven Source Schedule Board</h3>
        <p>
          Due rows: <strong>{dueRows.length}</strong>
        </p>
        <table>
          <thead>
            <tr>
              <th>Provider</th>
              <th>Source</th>
              <th>Dataset</th>
              <th>Object</th>
              <th>Cursor Field</th>
              <th>Last Cursor</th>
              <th>Refresh</th>
              <th>Next Run</th>
              <th>Due</th>
            </tr>
          </thead>
          <tbody>
            {scheduleBoard?.rows.slice(0, 40).map((row) => (
              <tr key={row.sourceDatasetDefinitionId}>
                <td>{row.providerType}</td>
                <td>{row.sourceSystemCode}</td>
                <td>{row.datasetCode}</td>
                <td>
                  {row.sourceSchemaName ? `${row.sourceSchemaName}.` : ""}
                  {row.sourceObjectName}
                </td>
                <td>{row.incrementalCursorField ?? "—"}</td>
                <td>{row.lastCursorValue ?? "—"}</td>
                <td>{row.refreshIntervalSeconds}s</td>
                <td>{row.nextRunAtUtc ?? "Now"}</td>
                <td>{row.isDueNow ? "Yes" : "No"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="admin-preview-panel">
        <h3>Raw/Staging Latest Copy</h3>
        <p>{stagingSummary?.message}</p>
        <table>
          <thead>
            <tr>
              <th>Batch</th>
              <th>Type</th>
              <th>Status</th>
              <th>Source Object</th>
              <th>Rows</th>
              <th>Pending</th>
              <th>Mapped</th>
              <th>Failed</th>
            </tr>
          </thead>
          <tbody>
            {stagingSummary?.rows.slice(0, 30).map((row) => (
              <tr key={row.importBatchId}>
                <td>{row.importBatchCode}</td>
                <td>{row.importType}</td>
                <td>{row.status}</td>
                <td>{row.sourceObjectName ?? "—"}</td>
                <td>{row.stagingRecordCount}</td>
                <td>{row.pendingCount}</td>
                <td>{row.mappedCount}</td>
                <td>{row.failedCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="admin-preview-panel">
        <h3>Schema Mapping Workbench</h3>
        <p>{workbench?.message}</p>
        <div className="admin-grid">
          <Metric label="Datasets" value={workbench?.datasets.length ?? 0} />
          <Metric label="Source fields" value={workbench?.sourceFields.length ?? 0} />
          <Metric label="Canonical targets" value={workbench?.canonicalTargets.length ?? 0} />
          <Metric label="Schema views" value={workbench?.schemaViews.length ?? 0} />
        </div>
      </div>

      <div className="admin-preview-panel">
        <h3>Importing Data Job Configuration</h3>
        <p>{importJobs?.message}</p>
        <table>
          <thead>
            <tr>
              <th>Mapping</th>
              <th>Source Object</th>
              <th>Target</th>
              <th>Has Job</th>
              <th>Enabled</th>
              <th>Schedule</th>
              <th>Last Run</th>
            </tr>
          </thead>
          <tbody>
            {importJobs?.mappingCandidates.map((row) => (
              <tr key={row.mappingDefinitionId}>
                <td>{row.mappingCode}</td>
                <td>{row.sourceObjectName}</td>
                <td>{row.targetEntityName}</td>
                <td>{row.existingJobDefinitionId ? "Yes" : "No"}</td>
                <td>{row.hasEnabledJob ? "Yes" : "No"}</td>
                <td>{row.existingScheduleExpression ?? "—"}</td>
                <td>{row.lastRunStatus ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="metric-card">
      <span>{label}</span>
      <strong>{value.toLocaleString()}</strong>
    </div>
  );
}