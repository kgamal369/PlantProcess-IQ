import { useMemo } from "react";
import { ConnectorTruthPanel } from "../../components/license/ConnectorTruthPanel";
import { LicenseGate } from "../../components/license/LicenseGate";
import { LicenseUsagePanel } from "../../components/license/LicenseUsagePanel";
import { DemoEnvironmentBanner } from "../../components/demo/DemoEnvironmentBanner";
import { DimensionCompletionPanel } from "../../components/demo/DimensionCompletionPanel";
import { useLicense } from "../../state/LicenseContext";
import "../../components/license/license-components.css";

export function CommercialLicensePage() {
  const { license, readiness } = useLicense();

  const premiumChecks = useMemo(
    () => [
      "PostgreSqlConnector",
      "SchemaSqlViewBuilder",
      "KpiViewBuilder",
      "DashboardPageBuilder",
      "CorrelationManualRun",
      "CorrelationScheduledRun",
      "InvestigationWorkflow",
      "MlWorkspacePreview",
      "MlLearningJobs",
      "BasicInvestigationReportPdf",
      "FullGenealogyReportPdf",
      "BrandedReportPdf",
    ],
    []
  );

  return (
    <main className="demo-lifecycle-page">
      <DemoEnvironmentBanner />

      <section className="demo-hero-panel">
        <div>
          <p className="eyebrow">Dimension 5 + Dimension 8</p>
          <h1>Commercial License & Demo Readiness Workspace</h1>
          <p className="hero-copy">
            This page proves that PlantProcess IQ has backend-controlled license
            gates, usage counters, connector truth, staging and mapping evidence,
            dashboard limits, premium intelligence gates, and an honest ML/demo
            lifecycle.
          </p>
        </div>

        <div className="hero-license-card">
          <span>Active license</span>
          <strong>{license?.displayName ?? "Loading"}</strong>
          <small>{license?.environment ?? "Environment loading"}</small>
        </div>
      </section>

      <DimensionCompletionPanel readiness={readiness} />

      <LicenseUsagePanel />

      <ConnectorTruthPanel />

      <section className="license-panel">
        <div className="license-panel-header">
          <div>
            <p className="license-eyebrow">Feature gates</p>
            <h2>Premium Feature Enforcement</h2>
            <p>
              Every premium capability below is resolved from the backend license
              matrix, not from static frontend demo data.
            </p>
          </div>
        </div>

        <div className="license-capability-grid">
          {premiumChecks.map((feature) => {
            const status = license?.features.find(
              (item) => item.feature === feature
            );

            return (
              <div
                className={
                  status?.isEnabled ? "capability enabled" : "capability locked"
                }
                key={feature}
              >
                <strong>{feature}</strong>
                <span>
                  {status?.isEnabled
                    ? "Enabled"
                    : status?.message ?? "Locked by license"}
                </span>
              </div>
            );
          })}
        </div>
      </section>

      <LicenseGate
        feature="MlWorkspacePreview"
        fallbackTitle="ML workspace preview requires Pro Plus"
        fallbackDescription="Current intelligence remains rule-based risk, data quality scanning, and statistical correlation."
      >
        <section className="license-panel">
          <div className="license-panel-header">
            <div>
              <p className="license-eyebrow">Honest ML</p>
              <h2>ML Preview Is License-Aware</h2>
              <p>
                ML preview is visible only when the license permits it. Training
                remains disabled until validated labeled historical data,
                genealogy coverage, and feature vectors are ready.
              </p>
            </div>
          </div>
        </section>
      </LicenseGate>
    </main>
  );
}

export default CommercialLicensePage;