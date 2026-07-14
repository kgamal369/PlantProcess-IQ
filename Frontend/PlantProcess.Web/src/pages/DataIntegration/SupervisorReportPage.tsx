// M1-05 SupervisorReportPage - design-system compliant (PPIQ-T09/T11).
import { useEffect, useState } from "react";
import { StandardPageHeader, StandardButton, DataFetchBoundary } from "@/components/standard";
import {
  listSupervisorReports,
  runSupervisor,
  type SupervisorReport,
} from "@/api/engine/supervisor.api";
import "./SupervisorReportPage.css";

export function SupervisorReportPage() {
  const [reports, setReports] = useState<SupervisorReport[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const r = await listSupervisorReports();
      setReports(Array.isArray(r) ? r : []);
    } catch (e: unknown) {
      setError(e);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    listSupervisorReports()
      .then((r) => { if (!cancelled) setReports(Array.isArray(r) ? r : []); })
      .catch((e: unknown) => { if (!cancelled) setError(e); })
      .finally(() => { if (!cancelled) setIsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  async function onRun() {
    setBusy(true);
    setError(null);
    try {
      await runSupervisor();
      await load();
    } catch (e: unknown) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="ppiq-supervisor">
      <StandardPageHeader
        title="Engine Supervisor"
        subtitle="A weekly review of the engine's findings (journey step 14). Read-only: it never changes a job automatically."
        actions={
          <StandardButton onClick={onRun} isDisabled={busy} isLoading={busy}>
            Run review now
          </StandardButton>
        }
      />
      <DataFetchBoundary
        title="Supervisor reports"
        isLoading={isLoading}
        error={error}
        isEmpty={reports.length === 0}
        emptyMessage={'No supervisor reports yet. Click "Run review now" to generate the first one.'}
        onRetry={() => void load()}
      >
        <ol className="ppiq-sup-list">
          {reports.map((r) => (
            <li key={r.id} className="ppiq-sup-item">
              <div className="ppiq-sup-item-head">
                <span className="ppiq-sup-title">{r.title}</span>
                <span className="ppiq-sup-date">{r.createdAtUtc}</span>
              </div>
              <pre className="ppiq-sup-body">{r.body}</pre>
            </li>
          ))}
        </ol>
      </DataFetchBoundary>
    </div>
  );
}

export default SupervisorReportPage;