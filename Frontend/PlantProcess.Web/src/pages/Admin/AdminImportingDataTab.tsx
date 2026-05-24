

// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/Admin/AdminImportingDataTab.tsx
//
// Extracted from monolithic AdminPageContent.tsx.
// Contains: ImportingDataTab
// ============================================================

import { useState } from "react";
import { Clock, Layers3, Loader2, PlayCircle, Workflow } from "lucide-react";
import {
  plantProcessApi,
  type AdminJobsMonitor,
  type SchemaConfigurationSummary,
  type TwoStageImportModel,
} from "@/api/plantProcessApi";
import { useOptimisticSave } from "@/hooks/useOptimisticSave";
import {
  AdminPanel,
  StatusPill,
  formatDate,
  formatDuration,
  formatNumber,
} from "./AdminSharedComponents";

// ── ImportingDataTab ──────────────────────────────────────────────────────────

export function ImportingDataTab({
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
  const metrics = data?.metrics ?? [];
  const canonicalJobs = (jobs?.jobs ?? []).filter(
    (job) => job.jobType === "CanonicalRefresh"
  );

  const [selectedMappingId, setSelectedMappingId] = useState("");
  const [refreshIntervalMinutes, setRefreshIntervalMinutes] = useState(15);

  // ── FE-HARD-005: Optimistic save for mapping refresh schedule ──────────────
  const { isSaving, save: saveMappingRefreshSchedule } = useOptimisticSave({
    successMessage: "Canonical refresh schedule saved and JobDefinition updated",
    toastId: "save-mapping-refresh-schedule",
    onSave: async () => {
      if (!selectedMappingId) {
        throw new Error("Select a mapping first.");
      }
      await plantProcessApi.updateMappingRefreshSchedule(selectedMappingId, {
        scheduleExpression: `Every ${refreshIntervalMinutes} minutes`,
        refreshIntervalMinutes,
      });
    },
    onSuccess: async () => {
      await onRefresh();
    },
  });

  return (
    <section className="admin-panel-grid">

      {/* Two-stage model overview */}
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

          {stages.length === 0 ? (
            <p className="admin-copy">No stage definitions returned from API yet.</p>
          ) : null}
        </div>
      </AdminPanel>

      {/* Canonical refresh scheduling */}
      <AdminPanel
        title="Canonical Refresh Scheduling"
        subtitle="Configure refresh cycle per MappingDefinition"
        icon={<Workflow size={18} />}
        wide
      >
        <div className="admin-form-row">
          <label className="admin-form-label">
            Mapping definition
          </label>
          <select
            className="admin-select"
            value={selectedMappingId}
            onChange={(e) => setSelectedMappingId(e.target.value)}
          >
            <option value="">Select mapping…</option>
            {mappings.map((mapping) => (
              <option key={mapping.id} value={mapping.id}>
                {mapping.mappingCode} · {mapping.mappingName}
              </option>
            ))}
          </select>
        </div>

        <div className="admin-form-row">
          <label className="admin-form-label">
            Refresh interval (minutes)
          </label>
          <select
            className="admin-select admin-select--narrow"
            value={refreshIntervalMinutes}
            onChange={(e) => setRefreshIntervalMinutes(Number(e.target.value))}
          >
            {[2, 5, 10, 15, 30, 60, 120, 360, 720, 1440].map((v) => (
              <option key={v} value={v}>
                {v < 60 ? `${v} min` : v < 1440 ? `${v / 60}h` : "24h"}
              </option>
            ))}
          </select>
        </div>

        <div className="admin-action-row">
          <button
            className="primary-button"
            type="button"
            onClick={saveMappingRefreshSchedule}
            disabled={!selectedMappingId || isSaving}
          >
            {isSaving
              ? <><Loader2 size={16} className="spin" /> Saving…</>
              : <><Clock size={16} /> Save Refresh Schedule</>}
          </button>
        </div>

        {/* Canonical jobs table */}
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
                    No canonical refresh jobs configured yet. Save a mapping
                    refresh schedule above to create one.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </AdminPanel>

      {/* Current import metrics */}
      <AdminPanel
        title="Current Import Metrics"
        subtitle="Live counts from existing integration entities"
        icon={<PlayCircle size={18} />}
        wide
      >
        {metrics.length > 0 ? (
          <div className="metric-grid admin-metric-grid">
            {metrics.map((metric) => (
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
        ) : (
          <p className="admin-copy">No import metrics available yet.</p>
        )}
      </AdminPanel>

    </section>
  );
}