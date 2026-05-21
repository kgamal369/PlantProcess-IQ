import type { DemoLifecycle } from "../../api/demo";
import "./demo-lifecycle-kpi-strip.css";

interface DemoLifecycleKpiStripProps {
  lifecycle: DemoLifecycle;
}

export function DemoLifecycleKpiStrip({ lifecycle }: DemoLifecycleKpiStripProps) {
  const readySteps = lifecycle.steps.filter((step) =>
    step.status.toLowerCase().includes("ready")
  ).length;

  const lockedSteps = lifecycle.steps.filter((step) =>
    step.status.toLowerCase().includes("locked")
  ).length;

  const warningSteps = lifecycle.steps.length - readySteps - lockedSteps;

  return (
    <section className="demo-lifecycle-kpi-strip">
      <Kpi label="Lifecycle steps" value={lifecycle.steps.length} />
      <Kpi label="Ready steps" value={readySteps} />
      <Kpi label="Warning steps" value={warningSteps} />
      <Kpi label="Locked steps" value={lockedSteps} />
      <Kpi label="Connectors" value={lifecycle.connectorTruth.connectors.length} />
      <Kpi label="Staging rows" value={lifecycle.stagingSummary.stagingRecordCount} />
      <Kpi label="Jobs" value={lifecycle.jobChain.totalJobs} />
      <Kpi label="Widgets" value={lifecycle.dashboardOutput.activeWidgetCount} />
    </section>
  );
}

function Kpi({ label, value }: { label: string; value: number }) {
  return (
    <article className="demo-lifecycle-kpi-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}