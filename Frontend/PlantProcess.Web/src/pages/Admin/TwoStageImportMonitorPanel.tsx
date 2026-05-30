import { useEffect, useMemo, useState } from "react";
import { DatabaseZap, GitBranch, PlayCircle, RefreshCw } from "lucide-react";
import {
  twoStageImportApi,
  type TwoStageImportOverview,
  type TwoStageSourceTable,
} from "@/api/twoStageImport/twoStageImport.api";
import {
  AdminPanel,
  EmptyAdminState,
  MiniKpi,
  StatusPill,
  formatDate,
  formatDuration,
} from "./AdminSharedComponents";

function statusClass(status: string | null | undefined) {
  const value = (status ?? "").toLowerCase();

  if (value === "ok" || value === "completed" || value === "success") return "success";
  if (value === "running") return "running";
  if (value === "failed" || value === "timeout") return "danger";
  if (value === "neverrun") return "neutral";
  return "info";
}

function tableLabel(table: TwoStageSourceTable) {
  return `${table.sourceSchemaName}.${table.sourceTableName}`;
}

export function TwoStageImportMonitorPanel() {
  const [overview, setOverview] = useState<TwoStageImportOverview | null>(null);
  const [selectedRegistryId, setSelectedRegistryId] = useState<string | null>(null);
  const [working, setWorking] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [messageKind, setMessageKind] = useState<"success" | "error" | null>(null);

  const selectedTable = useMemo(
    () => overview?.sourceTables.find((x) => x.id === selectedRegistryId) ?? null,
    [overview, selectedRegistryId]
  );

  async function load() {
    const next = await twoStageImportApi.getOverview();
    setOverview(next);

    if (!selectedRegistryId && next.sourceTables.length > 0) {
      setSelectedRegistryId(next.sourceTables[0].id);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function runAction(action: "stage1" | "stage2" | "full" | "provision") {
    setWorking(action);
    setMessage(null);
    setMessageKind(null);

    try {
      if (action === "stage1") {
        const response = await twoStageImportApi.runStage1({
          registryId: selectedRegistryId,
          requestedBy: "Admin UI",
          maxRows: 50000,
          timeoutSeconds: 120,
        });
        setMessage(`Stage 1 completed with ${response.rows.length} run row(s).`);
      }

      if (action === "stage2") {
        const response = await twoStageImportApi.runStage2({
          registryId: selectedRegistryId,
          requestedBy: "Admin UI",
          maxMinutes: 1,
        });
        setMessage(`Stage 2 completed with ${response.rows.length} run row(s).`);
      }

      if (action === "full") {
        const response = await twoStageImportApi.runFullCycle({
          requestedBy: "Admin UI",
          maxRows: 50000,
          timeoutSeconds: 120,
          maxMinutes: 1,
        });
        setMessage(`Full cycle completed with ${response.rows.length} stage result row(s).`);
      }

      if (action === "provision") {
        const response = await twoStageImportApi.provisionBaseline();
        setMessage(response.message);
      }

      setMessageKind("success");
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Two-stage import action did not complete.");
      setMessageKind("error");
    } finally {
      setWorking(null);
    }
  }

  if (!overview) {
    return (
      <AdminPanel
        title="Two-Stage Delta Import"
        subtitle="Loading source-shaped dump-copy registry and import jobs"
        icon={<DatabaseZap size={18} />}
        wide
      >
        <EmptyAdminState text="Loading Phase 03 source table registry, dump store status, and runtime job telemetry." />
      </AdminPanel>
    );
  }

  return (
    <AdminPanel
      title="Two-Stage Delta Import"
      subtitle="Source-shaped dump copies Ã¢â€ â€™ generic canonical refresh Ã¢â€ â€™ unified job telemetry"
      icon={<DatabaseZap size={18} />}
      wide
    >
      <div className="admin-kpi-row">
        {overview.summary.map((item) => (
          <MiniKpi key={item.metric} label={item.metric} value={Number(item.value) || 0} />
        ))}
      </div>

      <div className="admin-inline-alert">
        <strong>Runtime truth:</strong> {overview.message}
      </div>

      {message ? (
        <div
          className={
            messageKind === "error" ? "admin-inline-error" : "admin-inline-success"
          }
        >
          {message}
        </div>
      ) : null}

      <div className="admin-action-row">
        <button
          type="button"
          className="secondary-button"
          disabled={working !== null}
          onClick={() => void load()}
        >
          <RefreshCw size={14} />
          Refresh
        </button>

        <button
          type="button"
          className="secondary-button"
          disabled={working !== null}
          onClick={() => void runAction("provision")}
        >
          <DatabaseZap size={14} />
          Re-Provision Registry
        </button>

        <button
          type="button"
          className="secondary-button"
          disabled={working !== null || !selectedRegistryId}
          onClick={() => void runAction("stage1")}
        >
          <PlayCircle size={14} />
          {working === "stage1" ? "RunningÃ¢â‚¬Â¦" : "Run Stage 1"}
        </button>

        <button
          type="button"
          className="secondary-button"
          disabled={working !== null || !selectedRegistryId}
          onClick={() => void runAction("stage2")}
        >
          <GitBranch size={14} />
          {working === "stage2" ? "RunningÃ¢â‚¬Â¦" : "Run Stage 2"}
        </button>

        <button
          type="button"
          className="primary-button"
          disabled={working !== null}
          onClick={() => void runAction("full")}
        >
          <PlayCircle size={14} />
          {working === "full" ? "RunningÃ¢â‚¬Â¦" : "Run Full Cycle"}
        </button>
      </div>

      <div className="admin-form-grid">
        <label>
          Source table
          <select
            value={selectedRegistryId ?? ""}
            onChange={(event) => setSelectedRegistryId(event.target.value || null)}
          >
            {overview.sourceTables.map((table) => (
              <option key={table.id} value={table.id}>
                {tableLabel(table)} Ã¢â€ â€™ {table.dumpSchemaName}.{table.dumpTableName}
              </option>
            ))}
          </select>
        </label>

        {selectedTable ? (
          <div className="admin-inline-alert">
            <strong>Selected:</strong> {tableLabel(selectedTable)} | PK:{" "}
            {Array.isArray(selectedTable.primaryKeyColumns)
              ? selectedTable.primaryKeyColumns.join(", ")
              : selectedTable.primaryKeyColumns ?? "Ã¢â‚¬â€"}{" "}
            | Last index: {selectedTable.lastIndexColumn} ={" "}
            {selectedTable.lastIndexValueText ?? "not imported yet"}
          </div>
        ) : null}
      </div>

      <div className="admin-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Source Table</th>
              <th>Dump Table</th>
              <th>Shape</th>
              <th>Stage 1</th>
              <th>Stage 2</th>
              <th>Last Index</th>
              <th>Rows</th>
              <th>Cadence</th>
            </tr>
          </thead>
          <tbody>
            {overview.sourceTables.map((table) => (
              <tr key={table.id}>
                <td>
                  <strong>{table.sourceSystemCode}</strong>
                  <small>{tableLabel(table)}</small>
                </td>
                <td>
                  <strong>{table.dumpSchemaName}.{table.dumpTableName}</strong>
                  <small>Source-shaped dump-copy table</small>
                </td>
                <td>
                  <strong>{table.sourceColumnCount} columns</strong>
                  <small>{table.sourceShapeHash ?? "shape hash pending"}</small>
                </td>
                <td>
                  <StatusPill
                    status={table.stage1Status}
                    statusClass={statusClass(table.stage1Status)}
                  />
                </td>
                <td>
                  <StatusPill
                    status={table.stage2Status}
                    statusClass={statusClass(table.stage2Status)}
                  />
                </td>
                <td>
                  <strong>{table.lastIndexColumn}</strong>
                  <small>{table.lastIndexValueText ?? "Ã¢â‚¬â€"}</small>
                </td>
                <td>
                  <strong>Dump +{table.lastStage1InsertedRows}</strong>
                  <small>Canonical +{table.lastStage2CanonicalRows}</small>
                </td>
                <td>
                  <strong>{table.importCycleMinutes} min import</strong>
                  <small>{table.hmiRefreshSeconds} sec HMI refresh</small>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="admin-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Unified Job</th>
              <th>Stage</th>
              <th>Type</th>
              <th>Status</th>
              <th>Last Run</th>
              <th>Duration</th>
              <th>Rows</th>
              <th>Failure Count</th>
            </tr>
          </thead>
          <tbody>
            {overview.jobs.map((job) => (
              <tr key={job.id}>
                <td>
                  <strong>{job.jobCode}</strong>
                  <small>{job.jobName}</small>
                </td>
                <td>{job.stageKey ?? job.jobCategory ?? "ML / Operational"}</td>
                <td>{job.jobType}</td>
                <td>
                  <StatusPill
                    status={job.isEnabled ? job.lastRunStatus : "Paused"}
                    statusClass={job.isEnabled ? statusClass(job.lastRunStatus) : "paused"}
                  />
                </td>
                <td>{formatDate(job.lastRunStartedAtUtc)}</td>
                <td>{formatDuration(job.lastRunDurationMs)}</td>
                <td>
                  <strong>{job.lastSuccessRowCount ?? 0}</strong>
                  <small>last successful rows</small>
                </td>
                <td>{job.consecutiveFailureCount ?? 0}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="admin-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Run</th>
              <th>Status</th>
              <th>Source</th>
              <th>Started</th>
              <th>Duration</th>
              <th>Rows</th>
              <th>Watermark</th>
              <th>Message</th>
            </tr>
          </thead>
          <tbody>
            {overview.recentRuns.slice(0, 10).map((run) => (
              <tr key={run.id}>
                <td>
                  <strong>{run.runKind}</strong>
                  <small>{run.id}</small>
                </td>
                <td>
                  <StatusPill
                    status={run.runStatus}
                    statusClass={statusClass(run.runStatus)}
                  />
                </td>
                <td>
                  <strong>{run.sourceSchemaName ?? "Ã¢â‚¬â€"}</strong>
                  <small>{run.sourceTableName ?? "Ã¢â‚¬â€"}</small>
                </td>
                <td>{formatDate(run.startedAtUtc)}</td>
                <td>{formatDuration(run.durationMs)}</td>
                <td>
                  <strong>Dump +{run.insertedRows ?? 0}</strong>
                  <small>Canonical +{run.canonicalRows ?? 0}</small>
                </td>
                <td>
                  <strong>{run.lastIndexAfter ?? "Ã¢â‚¬â€"}</strong>
                  <small>before: {run.lastIndexBefore ?? "Ã¢â‚¬â€"}</small>
                </td>
                <td>{run.failureReason ?? run.message ?? "Ã¢â‚¬â€"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </AdminPanel>
  );
}