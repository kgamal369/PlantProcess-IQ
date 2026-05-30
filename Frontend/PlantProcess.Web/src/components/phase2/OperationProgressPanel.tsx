import { useEffect, useState } from "react";
import { CheckCircle2, RefreshCw, TriangleAlert } from "lucide-react";
import { StandardButton, StandardCard, StandardTable, type StandardTableColumn } from "@/components/standard";
import { phase78Api, type DemoResetJob, type DemoResetStep } from "@/api/phase78/phase78.api";

export type OperationProgressRow = {
  id: string;
  operationCode: string;
  operationType: string;
  operationName: string;
  status: string;
  percentComplete: number;
  currentStep?: string | null;
  totalSteps?: number | null;
  completedSteps?: number | null;
  message?: string | null;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  failedAtUtc?: string | null;
  failureReason?: string | null;
  metadataJson?: string | null;
};

type Props = {
  rows?: OperationProgressRow[];
  resetJobId?: string | null;
  pollEveryMs?: number;
  onRefresh?: () => void | Promise<void>;
  title?: string;
};

function toRows(job: DemoResetJob | null): OperationProgressRow[] {
  if (!job) return [];

  return job.steps.map((step: DemoResetStep, index) => ({
    id: job.jobId + "-" + step.code,
    operationCode: step.code,
    operationType: "DemoReset",
    operationName: step.label,
    status: step.status,
    percentComplete: step.percentComplete,
    currentStep: step.label,
    totalSteps: job.steps.length,
    completedSteps: job.steps.filter((item) => item.status === "Completed").length,
    message: step.exceptionDetail ?? step.status,
    startedAtUtc: job.startedAtUtc,
    completedAtUtc: job.completedAtUtc ?? null,
    failedAtUtc: job.status === "Failed" ? new Date().toISOString() : null,
    failureReason: step.exceptionDetail ?? job.failureReason ?? null,
    metadataJson: JSON.stringify({ scope: job.scope, operatorName: job.operatorName }),
  }));
}

export function OperationProgressPanel({
  rows,
  resetJobId,
  pollEveryMs = 1000,
  onRefresh,
  title = "Long Operation Progress",
}: Props) {
  const [job, setJob] = useState<DemoResetJob | null>(null);
  const [pollError, setPollError] = useState<string | null>(null);

  useEffect(() => {
    if (!resetJobId) return;

    let active = true;
    let timer: number | null = null;

    const poll = async () => {
      try {
        const next = await phase78Api.getDemoResetProgress(resetJobId);
        if (!active) return;

        setJob(next);
        setPollError(null);

        if (next.status === "Completed" || next.status === "Failed") {
          return;
        }

        timer = window.setTimeout(poll, pollEveryMs);
      } catch (error) {
        if (!active) return;
        setPollError(error instanceof Error ? error.message : "Progress polling failed.");
        timer = window.setTimeout(poll, pollEveryMs);
      }
    };

    void poll();

    return () => {
      active = false;
      if (timer) window.clearTimeout(timer);
    };
  }, [resetJobId, pollEveryMs]);

  const data = rows ?? toRows(job);

  const columns: StandardTableColumn<OperationProgressRow>[] = [
    {
      key: "statusIcon",
      header: "",
      cell: (row) =>
        row.status === "Completed" ? (
          <CheckCircle2 size={16} aria-label="Completed" />
        ) : row.status === "Failed" ? (
          <TriangleAlert size={16} aria-label="Failed" />
        ) : (
          <RefreshCw size={16} aria-label="Running" />
        ),
    },
    { key: "operation", header: "Operation", sortable: true, accessor: "operationName" },
    { key: "status", header: "Status", sortable: true, accessor: "status" },
    {
      key: "progress",
      header: "Progress",
      sortable: true,
      accessor: (row) => row.percentComplete,
      cell: (row) => (
        <div>
          <div className="phase56-progress" aria-label={row.percentComplete.toFixed(1) + "% complete"}>
            <span style={{ "--value": Math.min(100, Math.max(0, row.percentComplete)) + "%" } as React.CSSProperties} />
          </div>
          <small>{row.percentComplete.toFixed(1)}%</small>
        </div>
      ),
    },
    { key: "message", header: "Message", accessor: (row) => row.message ?? row.failureReason ?? "-" },
  ];

  return (
    <StandardCard
      eyebrow="PPIQ-T050"
      title={title}
      subtitle={pollError ?? "Step-by-step operation progress with error visibility and reusable Standard* primitives."}
      actions={
        onRefresh ? (
          <StandardButton variant="secondary" leadingIcon={<RefreshCw size={15} />} onClick={() => void onRefresh()}>
            Refresh
          </StandardButton>
        ) : null
      }
    >
      <StandardTable
        columns={columns}
        data={data}
        getRowKey={(row) => row.id}
        emptyTitle="No operation progress yet"
        emptyDescription="Start a demo reset or import workflow to populate progress."
        enableDensityToggle
      />
    </StandardCard>
  );
}

export default OperationProgressPanel;
