import { useLicense } from "../../state/LicenseContext";
import { LicenseBadge } from "./LicenseBadge";
import "./license-components.css";

function limitText(value: number | null | undefined) {
  return value === null || value === undefined ? "Unlimited" : String(value);
}

function usageText(current: number, max: number | null | undefined) {
  return `${current} / ${limitText(max)}`;
}

export function LicenseUsagePanel() {
  const { license, usage, readiness, isLoading, error, refresh } = useLicense();

  if (isLoading) {
    return (
      <section className="license-panel">
        <p className="license-eyebrow">Commercial gates</p>
        <h2>Loading license usage...</h2>
      </section>
    );
  }

  if (error) {
    return (
      <section className="license-panel license-panel--error">
        <p className="license-eyebrow">Commercial gates</p>
        <h2>License usage unavailable</h2>
        <p>{error}</p>
        <button type="button" onClick={() => void refresh()}>
          Retry
        </button>
      </section>
    );
  }

  if (!license || !usage) {
    return null;
  }

  return (
    <section className="license-panel">
      <div className="license-panel-header">
        <div>
          <p className="license-eyebrow">Dimension 5</p>
          <h2>License / Feature / Pricing Enforcement</h2>
          <p>
            Backend-controlled tier, feature gates, connector restrictions,
            refresh limits, source/job limits, schema gates, dashboard limits,
            and premium intelligence gates.
          </p>
        </div>
        <LicenseBadge license={license} />
      </div>

      <div className="license-usage-grid">
        <article>
          <span>Data sources</span>
          <strong>
            {usageText(usage.usage.activeSources, usage.limits.maxDataSources)}
          </strong>
        </article>
        <article>
          <span>Scheduled jobs</span>
          <strong>
            {usageText(
              usage.usage.activeJobs,
              usage.limits.maxScheduledJobs
            )}
          </strong>
        </article>
        <article>
          <span>Dashboards/pages</span>
          <strong>
            {usageText(
              usage.usage.activeDashboards,
              usage.limits.maxDashboards
            )}
          </strong>
        </article>
        <article>
          <span>Minimum refresh</span>
          <strong>{usage.limits.minRefreshIntervalMinutes ?? 1} min</strong>
        </article>
      </div>

      <div className="license-capability-grid">
        <Capability
          label="SQL editor"
          enabled={usage.limits.allowsSqlEditor}
        />
        <Capability
          label="KPI builder"
          enabled={usage.limits.allowsKpiBuilder}
        />
        <Capability
          label="Widget script layer"
          enabled={usage.limits.allowsWidgetScriptLayer}
        />
        <Capability
          label="Scheduled correlation"
          enabled={usage.limits.allowsScheduledCorrelation}
        />
        <Capability
          label="ML learning jobs"
          enabled={usage.limits.allowsMlLearningJobs}
        />
        <Capability
          label="Branded reports"
          enabled={usage.limits.allowsBrandedReports}
        />
      </div>

      {readiness && (
        <div className="license-readiness-grid">
          <ReadinessBlock title="Dimension 5" items={readiness.dimension5.checks} />
          <ReadinessBlock title="Dimension 8" items={readiness.dimension8.checks} />
        </div>
      )}
    </section>
  );
}

function Capability({
  label,
  enabled,
}: {
  label: string;
  enabled: boolean;
}) {
  return (
    <div className={enabled ? "capability enabled" : "capability locked"}>
      <strong>{label}</strong>
      <span>{enabled ? "Enabled" : "Locked by license"}</span>
    </div>
  );
}

function ReadinessBlock({
  title,
  items,
}: {
  title: string;
  items: { code: string; name: string; isReady: boolean }[];
}) {
  return (
    <article className="readiness-block">
      <h3>{title}</h3>
      <div>
        {items.map((item) => (
          <span
            key={item.code}
            className={item.isReady ? "ready-chip" : "warning-chip"}
          >
            {item.code} · {item.name}
          </span>
        ))}
      </div>
    </article>
  );
}