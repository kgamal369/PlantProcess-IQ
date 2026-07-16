import { useEffect, useState } from "react";
import {
  DataFetchBoundary,
  StandardButton,
  StandardCard,
  StandardPageHeader,
} from "@/components/standard";
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
      const response = await listSupervisorReports();
      setReports(Array.isArray(response) ? response : []);
    } catch (caught: unknown) {
      setError(caught);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    listSupervisorReports()
      .then((response) => {
        if (!cancelled) setReports(Array.isArray(response) ? response : []);
      })
      .catch((caught: unknown) => {
        if (!cancelled) setError(caught);
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function onRun() {
    setBusy(true);
    setError(null);
    try {
      await runSupervisor();
      await load();
    } catch (caught: unknown) {
      setError(caught);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="ppiq-supervisor">
      <StandardPageHeader
        title="Engine Supervisor"
        subtitle="Review the latest governed analysis results and generate a concise operational report."
        description="Current release: read-only review. It does not change job configuration or weaken readiness and evidence rules."
        actions={
          <StandardButton onClick={onRun} isDisabled={busy} isLoading={busy}>
            Run review now
          </StandardButton>
        }
      />

      <StandardCard
        eyebrow="Journey step 14"
        title="Supervisor reports"
        subtitle="The newest report stays open; older reviews are folded to keep the page focused."
        elevation="flat"
      >
        <DataFetchBoundary
          title="Supervisor reports"
          isLoading={isLoading}
          error={error}
          isEmpty={reports.length === 0}
          emptyMessage={'No supervisor reports yet. Click "Run review now" to generate the first one.'}
          onRetry={() => void load()}
        >
          <ol className="ppiq-sup-list">
            {reports.map((report, index) => (
              <li key={report.id} className="ppiq-sup-item">
                <details open={index === 0}>
                  <summary>
                    <span className="ppiq-sup-title">{report.title}</span>
                    <span className="ppiq-sup-date">{report.createdAtUtc}</span>
                  </summary>
                  <pre className="ppiq-sup-body">{report.body}</pre>
                </details>
              </li>
            ))}
          </ol>
        </DataFetchBoundary>
      </StandardCard>
    </div>
  );
}

export default SupervisorReportPage;
