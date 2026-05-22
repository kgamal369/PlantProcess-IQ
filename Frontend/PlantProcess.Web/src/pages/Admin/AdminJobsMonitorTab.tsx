// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/Admin/AdminJobsMonitorTab.tsx
//
// Fix 3: Extracted from monolithic AdminPageContent.tsx.
// Contains: JobsMonitorTab — full job table with history expand,
// run-now, pause, resume controls.
// ============================================================

import { Fragment, useMemo, useState } from "react";
import { Activity, PlayCircle } from "lucide-react";
import {
  plantProcessApi,
  type AdminJobsMonitor,
  type JobRunHistoryRecord,
} from "@/api/plantProcessApi";
import {
  AdminPanel,
  MiniKpi,
  StatusPill,
  formatDate,
  formatDuration,
} from "./AdminSharedComponents";

// ── JobsMonitorTab ────────────────────────────────────────────────────────────

export function JobsMonitorTab({
  data,
  onRefresh,
}: {
  data: AdminJobsMonitor | null;
  onRefresh: () => Promise<void> | void;
}) {
  const jobs = data?.jobs ?? [];
  const summary = data?.summary ?? [];

  const [expandedJobId, setExpandedJobId] = useState<string | null>(null);
  const [historyByJobId, setHistoryByJobId] = useState<
    Record<string, JobRunHistoryRecord[]>
  >({});
  const [workingJobId, setWorkingJobId] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [messageKind, setMessageKind] = useState<"success" | "error" | null>(null);

  const sortedJobs = useMemo(
    () =>
      [...jobs].sort((a, b) => {
        const aDate = a.lastRunAtUtc ? new Date(a.lastRunAtUtc).getTime() : 0;
        const bDate = b.lastRunAtUtc ? new Date(b.lastRunAtUtc).getTime() : 0;
        return bDate - aDate;
      }),
    [jobs]
  );

  function setSuccess(text: string) {
    setMessage(text);
    setMessageKind("success");
  }

  function setFailure(error: unknown, fallback: string) {
    setMessage(error instanceof Error ? error.message : fallback);
    setMessageKind("error");
  }

  async function refreshJobHistory(jobId: string) {
    const history = await plantProcessApi.getJobHistory(jobId, 20);
    setHistoryByJobId((current) => ({ ...current, [jobId]: history }));
    return history;
  }

  async function toggleHistory(jobId: string) {
    setMessage(null);
    setMessageKind(null);
    if (expandedJobId === jobId) {
      setExpandedJobId(null);
      return;
    }
    setExpandedJobId(jobId);
    if (!historyByJobId[jobId]) {
      try {
        await refreshJobHistory(jobId);
      } catch (error) {
        setFailure(error, "Failed to load job history.");
      }
    }
  }

  async function runNow(jobId: string) {
    setWorkingJobId(jobId);
    setMessage(null);
    setMessageKind(null);
    try {
      const response = await plantProcessApi.runJobNow(jobId, "Admin UI");
      setSuccess(response.message ?? "Job triggered successfully.");
      await onRefresh();
      await refreshJobHistory(jobId);
    } catch (error) {
      setFailure(error, "Failed to run job.");
    } finally {
      setWorkingJobId(null);
    }
  }

  async function pause(jobId: string) {
    setWorkingJobId(jobId);
    setMessage(null);
    setMessageKind(null);
    try {
      const response = await plantProcessApi.pauseJob(jobId);
      setSuccess(response.message ?? "Job paused.");
      await onRefresh();
      if (expandedJobId === jobId) await refreshJobHistory(jobId);
    } catch (error) {
      setFailure(error, "Failed to pause job.");
    } finally {
      setWorkingJobId(null);
    }
  }

  async function resume(jobId: string) {
    setWorkingJobId(jobId);
    setMessage(null);
    setMessageKind(null);
    try {
      const response = await plantProcessApi.resumeJob(jobId);
      setSuccess(response.message ?? "Job resumed.");
      await onRefresh();
      if (expandedJobId === jobId) await refreshJobHistory(jobId);
    } catch (error) {
      setFailure(error, "Failed to resume job.");
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
        {/* Summary KPI strip */}
        <div className="admin-kpi-row">
          {summary.map((item) => (
            <MiniKpi key={item.status} label={item.status} value={item.count} />
          ))}
        </div>

        {/* Inline feedback */}
        {message ? (
          <div
            className={
              messageKind === "error" ? "admin-inline-error" : "admin-inline-success"
            }
          >
            {message}
          </div>
        ) : null}

        {/* Jobs table */}
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
                const isPaused =
                  job.statusClass === "paused" ||
                  job.status.toLowerCase() === "paused";
                const history = historyByJobId[job.id] ?? [];
                const isHistoryOpen = expandedJobId === job.id;

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
                            onClick={() => void runNow(job.id)}
                          >
                            <PlayCircle size={14} />
                            {isWorking ? "Working…" : "Run Now"}
                          </button>
                          {isPaused ? (
                            <button
                              className="secondary-button"
                              type="button"
                              disabled={isWorking}
                              onClick={() => void resume(job.id)}
                            >
                              Resume
                            </button>
                          ) : (
                            <button
                              className="secondary-button"
                              type="button"
                              disabled={isWorking}
                              onClick={() => void pause(job.id)}
                            >
                              Pause
                            </button>
                          )}
                          <button
                            className="secondary-button"
                            type="button"
                            disabled={isWorking}
                            onClick={() => void toggleHistory(job.id)}
                          >
                            {isHistoryOpen ? "Hide History" : "History"}
                          </button>
                        </div>
                      </td>
                    </tr>

                    {/* Expandable run history */}
                    {isHistoryOpen ? (
                      <tr key={`${job.id}-history`}>
                        <td colSpan={8}>
                          <div className="job-history-panel">
                            <div className="panel-header">
                              <div>
                                <h3>Last Runs</h3>
                                <p>
                                  Latest executions for{" "}
                                  <strong>{job.jobCode}</strong>
                                </p>
                              </div>
                              <button
                                type="button"
                                className="ghost-button"
                                onClick={() => setExpandedJobId(null)}
                              >
                                Close
                              </button>
                            </div>

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
                                            item.status === "Ok" ||
                                            item.status === "Succeeded" ||
                                            item.status === "Completed"
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
                                      <td>
                                        {item.failureReason ?? item.runMessage ?? "—"}
                                      </td>
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
                    startup job registration.
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
