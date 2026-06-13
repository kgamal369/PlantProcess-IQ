/* PPIQ-PHASE6 JobsMonitorTable - every import/analytics job with last-run,
 * outcome, duration, rows-affected, next-run; failed flagged; guarded re-run.
 * Built on the design-system StandardDataTable + StandardButton. */
import { useState } from "react";
import { StandardButton, StandardDataTable } from "../standard";
import type { StandardDataTableColumn } from "../standard";
import type { JobRow } from "../../types/execOpsContracts";

const T = { ok: "#2CE6A2", warn: "#FFB020", crit: "#FF4D6D", cyan: "#00D4FF", steel: "#8EA7C1" };

function dur(ms?: number) {
  if (typeof ms !== "number") return "-";
  if (ms < 1000) return `${ms} ms`;
  const s = Math.round(ms / 100) / 10;
  return s < 60 ? `${s}s` : `${Math.floor(s / 60)}m ${Math.round(s % 60)}s`;
}
function when(iso?: string) { return iso ? new Date(iso).toLocaleString() : "-"; }
function outcomeColor(o?: string) { return o === "success" ? T.ok : o === "failed" ? T.crit : o === "running" ? T.cyan : T.steel; }

export function JobsMonitorTable({
  jobs,
  onRerun,
  canRerun = true,
}: {
  jobs: JobRow[];
  onRerun?: (jobId: string) => Promise<void> | void;
  canRerun?: boolean;
}) {
  const [busy, setBusy] = useState<string | null>(null);

  async function rerun(id: string) {
    if (!onRerun) return;
    setBusy(id);
    try { await onRerun(id); } finally { setBusy(null); }
  }

  const columns: StandardDataTableColumn<JobRow>[] = [
    { key: "name", title: "Job", render: (row) => <strong>{row.name}</strong> },
    { key: "lastRun", title: "Last run", render: (row) => <span data-testid="job-lastrun">{when(row.lastRunAt)}</span> },
    {
      key: "outcome", title: "Outcome",
      render: (row) => (
        <span>
          <span data-testid="job-outcome" style={{ color: outcomeColor(row.outcome), fontWeight: 700 }}>{(row.outcome || "never").toUpperCase()}</span>
          {row.outcome === "failed" && row.error ? <span data-testid="job-error" style={{ display: "block", color: T.crit, fontSize: 11 }}>{row.error}</span> : null}
        </span>
      ),
    },
    { key: "duration", title: "Duration", render: (row) => dur(row.durationMs) },
    { key: "rows", title: "Rows", align: "right", render: (row) => <span data-testid="job-rows">{typeof row.rowsAffected === "number" ? row.rowsAffected.toLocaleString() : "-"}</span> },
    { key: "next", title: "Next", render: (row) => <span style={{ color: T.steel }}>{when(row.nextRunAt)}</span> },
    {
      key: "action", title: "",
      render: (row) => (
        <StandardButton
          variant="ghost"
          size="sm"
          isDisabled={!canRerun || busy === row.id}
          data-testid="job-rerun"
          onClick={() => rerun(row.id)}
        >
          {busy === row.id ? "..." : "Re-run"}
        </StandardButton>
      ),
    },
  ];

  return (
    <StandardDataTable<JobRow>
      data-testid="jobs-monitor"
      ariaLabel="Jobs monitor"
      rows={jobs}
      columns={columns}
      rowKey="id"
      getRowClassName={(row) => (row.outcome === "failed" ? "ppiq-job-failed" : undefined)}
      emptyText="No jobs have run yet."
    />
  );
}
export default JobsMonitorTable;