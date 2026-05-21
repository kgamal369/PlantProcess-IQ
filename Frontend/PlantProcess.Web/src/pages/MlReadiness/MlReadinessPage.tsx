import { useEffect, useMemo, useState } from "react";
import {
  mlReadinessApi,
  type MlWorkspaceReadiness,
} from "../../api/ml";
import { DemoEnvironmentBanner } from "../../components/demo/DemoEnvironmentBanner";
import "../../styles/pages/demo-lifecycle.css";
import "./ml-readiness.css";

function statusClass(isReady: boolean) {
  return isReady ? "ppi-status ppi-status--success" : "ppi-status ppi-status--warning";
}

export function MlReadinessPage() {
  const [workspace, setWorkspace] = useState<MlWorkspaceReadiness | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isEnsuringJobs, setIsEnsuringJobs] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setIsLoading(true);
    setError(null);

    try {
      const result = await mlReadinessApi.getWorkspace(25);
      setWorkspace(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load ML readiness.");
    } finally {
      setIsLoading(false);
    }
  }

  async function ensureJobs() {
    setIsEnsuringJobs(true);
    setError(null);

    try {
      await mlReadinessApi.ensureJobs();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to ensure ML jobs.");
    } finally {
      setIsEnsuringJobs(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  const readyCount = useMemo(
    () => workspace?.readiness.metrics.filter((metric) => metric.isReady).length ?? 0,
    [workspace]
  );

  if (isLoading) {
    return (
      <main className="demo-lifecycle-page">
        <DemoEnvironmentBanner />
        <section className="demo-hero-panel">
          <div>
            <p className="eyebrow">Dimension 6</p>
            <h1>Loading ML readiness workspace...</h1>
            <p className="hero-copy">
              Checking source-to-canonical data, labels, feature vectors, jobs,
              model registry, and correlation lifecycle.
            </p>
          </div>
        </section>
      </main>
    );
  }

  if (error || !workspace) {
    return (
      <main className="demo-lifecycle-page">
        <DemoEnvironmentBanner />
        <section className="demo-hero-panel demo-hero-panel--error">
          <div>
            <p className="eyebrow">Dimension 6</p>
            <h1>ML readiness unavailable</h1>
            <p className="hero-copy">{error ?? "No workspace data returned."}</p>
            <button className="primary-action" type="button" onClick={() => void load()}>
              Retry
            </button>
          </div>
        </section>
      </main>
    );
  }

  return (
    <main className="demo-lifecycle-page ml-readiness-page">
      <DemoEnvironmentBanner />

      <section className="demo-hero-panel">
        <div>
          <p className="eyebrow">Dimension 6 — ML Engine Readiness</p>
          <h1>ML readiness before training, not fake AI theater.</h1>
          <p className="hero-copy">
            This workspace prepares the complete ML landing area: canonical data
            readiness, feature vectors, quality labels, disabled ML jobs,
            ModelRegistry lifecycle, correlation lifecycle, dashboard/report
            integration, and honest training status.
          </p>
        </div>

        <div className="hero-license-card">
          <span>Readiness score</span>
          <strong>{workspace.readiness.scorePercent}%</strong>
          <small>{workspace.readiness.overallStatus}</small>
        </div>
      </section>

      <section className="ml-honesty-panel">
        <strong>{workspace.readiness.trainingStatus}</strong>
        <p>{workspace.readiness.honestPositioning}</p>
        <p>{workspace.disclaimer}</p>
      </section>

      <section className="ml-readiness-grid">
        {workspace.readiness.metrics.map((metric) => (
          <article className="ml-metric-card" key={metric.code}>
            <div className="section-title-row">
              <h2>{metric.name}</h2>
              <span className={statusClass(metric.isReady)}>{metric.status}</span>
            </div>

            <strong>
              {metric.currentValue} / {metric.requiredValue} {metric.unit}
            </strong>

            <p>{metric.message}</p>
          </article>
        ))}
      </section>

      <section className="demo-section">
        <div className="section-title-row">
          <div>
            <p className="eyebrow">Quality labels</p>
            <h2>Training label preview</h2>
          </div>

          <span className="ppi-status ppi-status--muted">
            {workspace.labelPreview.returnedCount} labels
          </span>
        </div>

        <div className="ml-label-table">
          <div className="ml-label-row ml-label-row--head">
            <span>Material</span>
            <span>Label</span>
            <span>Defect</span>
            <span>Events</span>
            <span>Observations</span>
            <span>Genealogy</span>
          </div>

          {workspace.labelPreview.labels.map((label) => (
            <div className="ml-label-row" key={label.materialUnitId}>
              <strong>{label.materialCode}</strong>
              <span>{label.labelCode}</span>
              <span>{label.primaryDefectName ?? "—"}</span>
              <span>{label.qualityEventCount}</span>
              <span>{label.upstreamObservationCount}</span>
              <span>{label.genealogyEdgeCount}</span>
            </div>
          ))}
        </div>
      </section>

      <section className="demo-section">
        <div className="section-title-row">
          <div>
            <p className="eyebrow">Jobs Monitor</p>
            <h2>Disabled ML jobs are visible and honest</h2>
          </div>

          <button
            className="primary-action"
            type="button"
            disabled={isEnsuringJobs}
            onClick={() => void ensureJobs()}
          >
            {isEnsuringJobs ? "Ensuring..." : "Ensure ML jobs"}
          </button>
        </div>

        <div className="job-chain-grid">
          {workspace.mlJobs.map((job) => (
            <article className="job-chain-card" key={job.jobId}>
              <header>
                <strong>{job.jobName}</strong>
                <span className={job.isEnabled ? "ppi-status ppi-status--warning" : "ppi-status ppi-status--muted"}>
                  {job.isEnabled ? "Enabled" : "Disabled"}
                </span>
              </header>

              <p>{job.reason}</p>

              <footer>
                <code>{job.jobCode}</code>
                <span>{job.scheduleExpression}</span>
              </footer>
            </article>
          ))}
        </div>
      </section>

      <section className="ml-two-column">
        <article className="demo-section">
          <p className="eyebrow">ModelRegistry</p>
          <h2>Model lifecycle states</h2>

          <div className="ml-lifecycle-list">
            {workspace.modelRegistry.length === 0 ? (
              <p className="muted-copy">
                No model registry records yet. This is acceptable before real
                training. Rule-based scoring can be registered separately.
              </p>
            ) : (
              workspace.modelRegistry.map((model) => (
                <div className="ml-lifecycle-item" key={model.id}>
                  <strong>{model.modelName}</strong>
                  <span>{model.lifecycleState}</span>
                  <p>{model.governanceMessage}</p>
                </div>
              ))
            )}
          </div>
        </article>

        <article className="demo-section">
          <p className="eyebrow">CorrelationResult</p>
          <h2>Suspected-contributor lifecycle</h2>

          <div className="ml-lifecycle-list">
            {workspace.correlations.length === 0 ? (
              <p className="muted-copy">
                No correlation results yet. Run correlation analysis after data
                quality and canonical mapping are ready.
              </p>
            ) : (
              workspace.correlations.map((correlation) => (
                <div className="ml-lifecycle-item" key={correlation.id}>
                  <strong>
                    {correlation.subjectCode} → {correlation.outcomeCode}
                  </strong>
                  <span>{correlation.lifecycleState}</span>
                  <p>{correlation.governanceMessage}</p>
                </div>
              ))
            )}
          </div>
        </article>
      </section>

      <section className="report-close">
        <p className="eyebrow">Definition of Done</p>
        <h2>
          {readyCount} of {workspace.readiness.metrics.length} ML readiness gates are green.
        </h2>

        <p>{workspace.currentIntelligence}</p>
        <p>{workspace.futureMlLifecycle}</p>

        <ul>
          {workspace.readiness.nextActions.map((action) => (
            <li key={action}>{action}</li>
          ))}
        </ul>
      </section>
    </main>
  );
}

export default MlReadinessPage;