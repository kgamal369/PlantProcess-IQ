import { RefreshCw } from "lucide-react";
import type { OperationProgressRow } from "@/api/phase2WorkflowApi";
import { StandardButton, StandardCard, StandardTable, type StandardTableColumn } from "@/components/standard";

type Props = {
  rows: OperationProgressRow[];
  onRefresh?: () => void | Promise<void>;
};

export function OperationProgressPanel({ rows, onRefresh }: Props) {
  const columns: StandardTableColumn<OperationProgressRow>[] = [
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
    { key: "step", header: "Step", sortable: true, accessor: (row) => row.currentStep ?? "-" },
    { key: "message", header: "Message", accessor: (row) => row.message ?? "-" },
  ];

  return (
    <StandardCard
      eyebrow="PPIQ-HARD-026"
      title="Long Operation Progress"
      subtitle="Import, report and analysis operations show visible progress using Standard* primitives."
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
        data={rows}
        getRowKey={(row) => row.id}
        emptyTitle="No long operations recorded yet"
        emptyDescription="Run a demo reset or import workflow to populate operation progress."
        enableDensityToggle
      />
    </StandardCard>
  );
}

export default OperationProgressPanel;
