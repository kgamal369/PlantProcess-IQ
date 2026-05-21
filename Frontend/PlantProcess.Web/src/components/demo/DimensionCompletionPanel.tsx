import type { CommercialReadinessResponse } from "../../api/license";
import "./dimension-completion-panel.css";

interface DimensionCompletionPanelProps {
  readiness: CommercialReadinessResponse | null;
}

export function DimensionCompletionPanel({ readiness }: DimensionCompletionPanelProps) {
  if (!readiness) {
    return (
      <section className="dimension-completion-panel">
        <p className="dimension-eyebrow">Implementation acceptance</p>
        <h2>Loading Dimension 5 / Dimension 8 completion status...</h2>
      </section>
    );
  }

  const dimension5Done = readiness.dimension5.checks.every((check) => check.isReady);
  const dimension8Done = readiness.dimension8.checks.every((check) => check.isReady);

  return (
    <section className="dimension-completion-panel">
      <div className="dimension-completion-header">
        <div>
          <p className="dimension-eyebrow">Implementation acceptance</p>
          <h2>Dimension 5 + Dimension 8 Completion Gate</h2>
          <p>
            This panel is the visible proof that the commercial license gates and
            demo lifecycle are implemented as a real product layer, not as an
            isolated demo island.
          </p>
        </div>

        <div className="dimension-score-card">
          <span>Target</span>
          <strong>
            {dimension5Done && dimension8Done ? "100%" : "Needs proof"}
          </strong>
          <small>
            Build + tests + E2E still decide final acceptance.
          </small>
        </div>
      </div>

      <div className="dimension-completion-grid">
        <CompletionBlock
          title="Dimension 5 — License / Feature / Pricing Enforcement"
          isDone={dimension5Done}
          items={readiness.dimension5.checks}
        />

        <CompletionBlock
          title="Dimension 8 — Demo Preparation"
          isDone={dimension8Done}
          items={readiness.dimension8.checks}
        />
      </div>

      <div className="dimension-evidence-grid">
        <Evidence label="Active sources" value={readiness.evidence.activeSources} />
        <Evidence label="Active jobs" value={readiness.evidence.activeJobs} />
        <Evidence label="Staging records" value={readiness.evidence.stagingRecords} />
        <Evidence label="Schema views" value={readiness.evidence.activeSchemaViews} />
        <Evidence label="Mappings" value={readiness.evidence.activeMappings} />
        <Evidence label="Dashboards" value={readiness.evidence.activeDashboards} />
        <Evidence label="Widgets" value={readiness.evidence.activeWidgets} />
        <Evidence label="Correlations" value={readiness.evidence.correlationResults} />
        <Evidence label="Models" value={readiness.evidence.modelRegistryEntries} />
      </div>
    </section>
  );
}

function CompletionBlock({
  title,
  isDone,
  items,
}: {
  title: string;
  isDone: boolean;
  items: { code: string; name: string; isReady: boolean }[];
}) {
  return (
    <article className={isDone ? "completion-block done" : "completion-block warning"}>
      <header>
        <h3>{title}</h3>
        <span>{isDone ? "Implementation complete" : "Needs evidence"}</span>
      </header>

      <div className="completion-check-list">
        {items.map((item) => (
          <div
            key={item.code}
            className={item.isReady ? "completion-check ready" : "completion-check missing"}
          >
            <strong>{item.code}</strong>
            <span>{item.name}</span>
          </div>
        ))}
      </div>
    </article>
  );
}

function Evidence({ label, value }: { label: string; value: number }) {
  return (
    <article className="dimension-evidence-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}