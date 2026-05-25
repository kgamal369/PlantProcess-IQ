import { RefreshCw } from "lucide-react";
import type { OperationProgressRow } from "@/api/phase2WorkflowApi";

type Props = {
  rows: OperationProgressRow[];
  onRefresh?: () => void | Promise<void>;
};

export function OperationProgressPanel({ rows, onRefresh }: Props) {
  return (
    <section className="admin-card">
      <header className="admin-card-header">
        <div>
          <p className="eyebrow">PPIQ-HARD-026</p>
          <h3>Long Operation Progress</h3>
          <p>Import, report, and analysis operations should show visible progress.</p>
        </div>

        {onRefresh ? (
          <button className="secondary-button" type="button" onClick={() => void onRefresh()}>
            <RefreshCw size={15} />
            Refresh
          </button>
        ) : null}
      </header>

      <div className="phase2-progress-list">
        {rows.length === 0 ? (
          <div className="empty-insight">
            <strong>No long operations recorded yet.</strong>
          </div>
        ) : (
          rows.map((row) => (
            <article className="phase2-progress-row" key={row.id}>
              <div className="phase2-progress-row__top">
                <strong>{row.operationName}</strong>
                <span>{row.status}</span>
              </div>

              <div className="phase2-progress-bar" aria-label={`${row.percentComplete}% complete`}>
                <span style={{ width: `${Math.min(100, Math.max(0, row.percentComplete))}%` }} />
              </div>

              <div className="phase2-progress-row__meta">
                <span>{row.percentComplete.toFixed(1)}%</span>
                {row.currentStep ? <span>{row.currentStep}</span> : null}
                {row.message ? <span>{row.message}</span> : null}
              </div>
            </article>
          ))
        )}
      </div>
    </section>
  );
}

export default OperationProgressPanel;