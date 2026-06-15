import { useState } from "react";
import { demoReadinessApi, type DemoReadinessReport } from "@/api/demo/demoReadiness.api";
import { StandardButton } from "@/components/standard";
import { StandardCard } from "@/components/standard/StandardSurface";
import "./demo-readiness-panel.css";

export function DemoReadinessPanel() {
  const [report, setReport] = useState<DemoReadinessReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function runCheck() {
    setLoading(true);
    setError(null);
    try {
      setReport(await demoReadinessApi.get());
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Readiness check failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="demo-readiness-panel" aria-label="Demo readiness" data-testid="demo-readiness-panel">
      <StandardCard title="Customer demo readiness" subtitle="One click, exact blockers, no false-green result.">
        <div className="demo-readiness-panel__actions">
          <StandardButton
            variant="primary"
            onClick={runCheck}
            isLoading={loading}
            data-testid="run-demo-readiness"
          >
            Run readiness check
          </StandardButton>
          {report ? (
            <strong data-testid="demo-readiness-status" className={`demo-readiness-panel__status demo-readiness-panel__status--${report.status}`}>
              {report.isReady ? "READY" : "BLOCKED"}
            </strong>
          ) : null}
        </div>

        {error ? <div role="alert" className="demo-readiness-panel__error">{error}</div> : null}
        {report ? (
          <div data-testid="demo-readiness-result">
            <dl className="demo-readiness-panel__metrics">
              <div><dt>Sources</dt><dd>{report.inputs.sourcesLinked}/{report.inputs.sourcesExpected}</dd></div>
              <div><dt>Staging</dt><dd>{report.inputs.stagingPopulated ? "Populated" : "Empty"}</dd></div>
              <div><dt>Mappings</dt><dd>{report.inputs.mappingsPublished ? "Published" : "Missing"}</dd></div>
              <div><dt>Jobs</dt><dd>{report.inputs.jobsRunnable}/{report.inputs.jobsExpected}</dd></div>
              <div><dt>Pages</dt><dd>{report.inputs.demoPagesPresent ? "Present" : "Missing"}</dd></div>
            </dl>
            {!report.isReady ? (
              <ul data-testid="demo-readiness-blockers">
                {report.blockers.map((blocker) => <li key={blocker}>{blocker}</li>)}
              </ul>
            ) : <p>All mandatory demo prerequisites are green.</p>}
          </div>
        ) : null}
      </StandardCard>
    </section>
  );
}