import { useEffect, useMemo, useState } from "react";
import { demoLifecycleApi, type DemoLifecycle } from "../../api/demo";
import { LicenseBadge } from "../../components/license/LicenseBadge";
import { DimensionCompletionPanel } from "../../components/demo/DimensionCompletionPanel";
import { DemoEnvironmentBanner } from "../../components/demo/DemoEnvironmentBanner";
import { LicenseUsagePanel } from "../../components/license/LicenseUsagePanel";
import { ConnectorTruthPanel } from "../../components/license/ConnectorTruthPanel";
import { useLicense } from "../../state/LicenseContext";
import { DemoLifecycleKpiStrip } from "../../components/demo/DemoLifecycleKpiStrip";

import "../../styles/pages/demo-lifecycle.css";

function formatDate(value: string | null | undefined) {
  if (!value) return "—";
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function statusClass(status: string) {
  const normalized = status.toLowerCase();

  if (normalized.includes("ready") || normalized.includes("allowed")) {
    return "ppi-status ppi-status--success";
  }

  if (normalized.includes("partial") || normalized.includes("attention")) {
    return "ppi-status ppi-status--warning";
  }

  if (normalized.includes("locked") || normalized.includes("failed")) {
    return "ppi-status ppi-status--danger";
  }

  return "ppi-status ppi-status--muted";
}

  export function DemoLifecyclePage() {
  const { readiness } = useLicense();
  const [data, setData] = useState<DemoLifecycle | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setIsLoading(true);
    setError(null);

    try {
      const result = await demoLifecycleApi.getLifecycle();
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load demo lifecycle.");
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  const orderedSteps = useMemo(() => {
    return [...(data?.steps ?? [])].sort((a, b) => a.order - b.order);
  }, [data]);

  if (isLoading) {
    return (
      <main className="demo-lifecycle-page">
        <DemoEnvironmentBanner />
          <section className="demo-hero-panel">
            <p className="eyebrow">Controlled product lifecycle</p>
            <h1>Loading PlantProcess IQ demo lifecycle...</h1>
          </section>
      </main>
    );
  }

  if (error || !data) {
    return (
      <main className="demo-lifecycle-page">
        <section className="demo-hero-panel demo-hero-panel--error">
          <p className="eyebrow">Controlled product lifecycle</p>
          <h1>Demo lifecycle unavailable</h1>
          <p>{error ?? "No lifecycle data returned from backend."}</p>
          <button className="primary-action" onClick={() => void load()}>
            Retry
          </button>
        </section>
      </main>
    );
  }

  return (
    <main className="demo-lifecycle-page">
      <section className="demo-hero-panel">
        <div>
          <p className="eyebrow">Controlled product lifecycle</p>
          <h1>PlantProcess IQ Demo Lifecycle</h1>
          <p className="hero-copy">
            One real product journey: license gates → connector truth → staging copy →
            schema mapping → jobs monitor → dashboard output → risk/correlation →
            honest ML readiness → customer-grade report.
          </p>
        </div>

        <div className="hero-license-card">
          <span>Active license</span>
          <LicenseBadge license={data.license} />
          <strong>{data.license.tier}</strong>
          <small>{data.license.environment} · {data.license.source}</small>
        </div>
      </section>

      <DimensionCompletionPanel readiness={readiness} />
      <LicenseUsagePanel />
      <ConnectorTruthPanel connectors={data.connectorTruth.connectors} />

      <section className="lifecycle-grid">
        <DemoLifecycleKpiStrip lifecycle={data} />
        {orderedSteps.map((step) => (
          <article className="lifecycle-step-card" key={step.code}>
            <div className="step-header">
              <span className="step-number">{step.order}</span>
              <span className={statusClass(step.status)}>{step.status}</span>
            </div>
            <h2>{step.title}</h2>
            <p>{step.description}</p>
            <footer>
              <span>Required tier: {step.requiredLicenseTier}</span>
              <code>{step.evidenceEndpoint}</code>
            </footer>
          </article>
        ))}
      </section>

      <section className="demo-section">
        <div className="section-title-row">
          <div>
            <p className="eyebrow">Dimension 5</p>
            <h2>License / Feature / Pricing Enforcement</h2>
          </div>
          <LicenseBadge license={data.license} />
        </div>

        <div className="license-limit-grid">
          <div className="metric-card">
            <span>Data sources</span>
            <strong>
              {data.license.limits.maxDataSources ?? "Unlimited"}
            </strong>
          </div>
          <div className="metric-card">
            <span>Scheduled jobs</span>
            <strong>
              {data.license.limits.maxScheduledJobs ?? "Unlimited"}
            </strong>
          </div>
          <div className="metric-card">
            <span>Dashboards/pages</span>
            <strong>
              {data.license.limits.maxDashboards ?? "Unlimited"}
            </strong>
          </div>
          <div className="metric-card">
            <span>Minimum refresh</span>
            <strong>
              {data.license.limits.minRefreshIntervalMinutes ?? 1} min
            </strong>
          </div>
        </div>
      </section>

      <section className="demo-section">
        <div className="section-title-row">
          <div>
            <p className="eyebrow">Dimension 8</p>
            <h2>Connector Truth</h2>
          </div>
        </div>

        <div className="connector-truth-table">
          <div className="table-row table-row--head">
            <span>Connector</span>
            <span>Implementation</span>
            <span>License</span>
            <span>Demo safe</span>
          </div>

          {data.connectorTruth.connectors.map((connector) => (
            <div className="table-row" key={connector.providerType}>
              <strong>{connector.displayName}</strong>
              <span>{connector.implementationStatus}</span>
              <span className={statusClass(connector.licenseStatus)}>
                {connector.licenseStatus}
              </span>
              <span className={statusClass(connector.isSafeForDemo ? "Ready" : "Locked")}>
                {connector.isSafeForDemo ? "Yes" : "No"}
              </span>
            </div>
          ))}
        </div>
      </section>

      <section className="demo-section two-column">
        <article className="summary-card">
          <p className="eyebrow">Source → staging</p>
          <h2>Staging / Dump Copy</h2>
          <dl>
            <div>
              <dt>Active connections</dt>
              <dd>{data.stagingSummary.activeConnectionProfiles}</dd>
            </div>
            <div>
              <dt>Active datasets</dt>
              <dd>{data.stagingSummary.activeDatasets}</dd>
            </div>
            <div>
              <dt>Staging records</dt>
              <dd>{data.stagingSummary.stagingRecordCount}</dd>
            </div>
            <div>
              <dt>Import batches</dt>
              <dd>{data.stagingSummary.importBatchCount}</dd>
            </div>
            <div>
              <dt>Last snapshot</dt>
              <dd>{formatDate(data.stagingSummary.lastSnapshotUtc)}</dd>
            </div>
          </dl>
          <p>{data.stagingSummary.message}</p>
        </article>

        <article className="summary-card">
          <p className="eyebrow">Genericity layer</p>
          <h2>Schema Mapping</h2>
          <dl>
            <div>
              <dt>Schema views</dt>
              <dd>{data.schemaMapping.schemaViewCount}</dd>
            </div>
            <div>
              <dt>Active views</dt>
              <dd>{data.schemaMapping.activeSchemaViewCount}</dd>
            </div>
            <div>
              <dt>Mappings</dt>
              <dd>{data.schemaMapping.mappingDefinitionCount}</dd>
            </div>
            <div>
              <dt>Active mappings</dt>
              <dd>{data.schemaMapping.activeMappingDefinitionCount}</dd>
            </div>
            <div>
              <dt>KPI definitions</dt>
              <dd>{data.schemaMapping.kpiDefinitionCount}</dd>
            </div>
          </dl>
          <p>{data.schemaMapping.message}</p>
        </article>
      </section>

      <section className="demo-section">
        <div className="section-title-row">
          <div>
            <p className="eyebrow">Operational chain</p>
            <h2>Jobs Monitor</h2>
          </div>
          <span className={statusClass(data.jobChain.failedOrTimeoutJobs > 0 ? "NeedsAttention" : "Ready")}>
            {data.jobChain.enabledJobs}/{data.jobChain.totalJobs} enabled
          </span>
        </div>

        <div className="job-chain-list">
          {data.jobChain.jobs.map((job) => (
            <article className="job-chain-card" key={job.jobId}>
              <header>
                <strong>{job.jobName}</strong>
                <span className={statusClass(job.licenseStatus)}>{job.licenseStatus}</span>
              </header>
              <p>{job.operationalRole}</p>
              <footer>
                <code>{job.jobType}</code>
                <span>{job.lastRunStatus}</span>
                <span>{formatDate(job.lastRunStartedAtUtc)}</span>
              </footer>
            </article>
          ))}
        </div>
      </section>

      <section className="demo-section two-column">
        <article className="summary-card">
          <p className="eyebrow">Configured output</p>
          <h2>Dashboard / Page Output</h2>
          <dl>
            <div>
              <dt>Dashboards</dt>
              <dd>{data.dashboardOutput.dashboardCount}</dd>
            </div>
            <div>
              <dt>Active dashboards</dt>
              <dd>{data.dashboardOutput.activeDashboardCount}</dd>
            </div>
            <div>
              <dt>Widgets</dt>
              <dd>{data.dashboardOutput.widgetCount}</dd>
            </div>
            <div>
              <dt>Active widgets</dt>
              <dd>{data.dashboardOutput.activeWidgetCount}</dd>
            </div>
          </dl>
          <p>{data.dashboardOutput.message}</p>
        </article>

        <article className="summary-card">
          <p className="eyebrow">Honest AI / ML</p>
          <h2>ML Readiness Preview</h2>
          <dl>
            <div>
              <dt>Model status</dt>
              <dd>{data.mlReadiness.modelStatus}</dd>
            </div>
            <div>
              <dt>Training status</dt>
              <dd>{data.mlReadiness.trainingStatus}</dd>
            </div>
            <div>
              <dt>Parameters</dt>
              <dd>{data.mlReadiness.parameterObservationCount}</dd>
            </div>
            <div>
              <dt>Quality events</dt>
              <dd>{data.mlReadiness.qualityEventCount}</dd>
            </div>
            <div>
              <dt>Genealogy edges</dt>
              <dd>{data.mlReadiness.genealogyEdgeCount}</dd>
            </div>
          </dl>
          <ul className="warning-list">
            {data.mlReadiness.warnings.map((warning) => (
              <li key={warning}>{warning}</li>
            ))}
          </ul>
        </article>
      </section>

      <section className="demo-section report-close">
        <p className="eyebrow">Final close</p>
        <h2>Customer-grade Report</h2>
        <p>{data.reportClose.dataDiagnosticBridge}</p>
        <div className="report-section-grid">
          {data.reportClose.includedSections.map((section) => (
            <span key={section}>{section}</span>
          ))}
        </div>
        <blockquote>{data.reportClose.disclaimer}</blockquote>
      </section>
    </main>
  );
}
export default DemoLifecyclePage;