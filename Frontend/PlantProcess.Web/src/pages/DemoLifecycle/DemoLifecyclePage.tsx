import {
  Activity,
  AlertTriangle,
  ArrowRight,
  BadgeEuro,
  BarChart3,
  BrainCircuit,
  CheckCircle2,
  DatabaseZap,
  FileText,
  GitBranch,
  LockKeyhole,
  PlayCircle,
  RefreshCw,
  ShieldCheck,
  TableProperties,
  Users,
  Workflow,
} from "lucide-react";

import { useDemoMode, licensePlans } from "@/state/DemoModeContext";

const lifecycleSteps = [
  {
    title: "1. Configure source connector",
    description: "Select CSV, PostgreSQL, Excel planned, Oracle planned, or future historian.",
    icon: DatabaseZap,
  },
  {
    title: "2. Import raw source snapshot",
    description: "Show import jobs, scan cycle, raw staging and source freshness.",
    icon: RefreshCw,
  },
  {
    title: "3. Map to canonical schema",
    description: "Preview SQL mapping from plant-specific source data to generic PlantProcess IQ model.",
    icon: TableProperties,
  },
  {
    title: "4. Build dashboard widget",
    description: "Dynamic widget configuration and query preview.",
    icon: BarChart3,
  },
  {
    title: "5. Investigate quality issue",
    description: "Move from quality event to material, process route and suspected contributors.",
    icon: Workflow,
  },
  {
    title: "6. Run ML preview",
    description: "Frontend-only ML workflow preview. No trained backend model yet.",
    icon: BrainCircuit,
  },
  {
    title: "7. Show result on dashboard/report",
    description: "End with dashboard insight and customer-grade report preview.",
    icon: FileText,
  },
];

export function DemoLifecyclePage() {
 const {
    selectedPlan,
    setSelectedPlan,
    activeLifecycleStep,
    setActiveLifecycleStep,
    connectors,
    jobs,
    mappings,
    visibleWidgets,
    users,
    mlPreview,
    mlTrainingForm,
    isFeatureAvailable,
  } = useDemoMode();

  const activeStep = lifecycleSteps[activeLifecycleStep];
  const ActiveIcon = activeStep.icon;

  return (
    <main className="page-shell demo-lifecycle-page">
      <section className="dashboard-hero demo-lifecycle-hero">
        <div>
          <div className="eyebrow">
            <PlayCircle size={14} />
            Real app demo workflow
          </div>

          <h1>PlantProcess IQ Lifecycle Demo</h1>

          <p>
            Demonstrate the full product story from connector setup to import
            job, schema mapping, dashboard widget, investigation, ML preview and
            final report. Some steps are frontend preview only, but they are
            embedded inside the real app workflow.
          </p>

          <div className="dashboard-subtitle-row">
            <span>
              Active plan: <strong>{selectedPlan}</strong>
            </span>
            <span className="status-chip">Frontend demo mode</span>
            <span className="status-chip status-chip--warning">
              ML preview only
            </span>
          </div>
        </div>

        <div className="demo-plan-switcher">
          <BadgeEuro size={22} />
          <strong>License simulation</strong>
          <select
            value={selectedPlan}
            onChange={(event) => setSelectedPlan(event.target.value as typeof selectedPlan)}
          >
            {licensePlans.map((plan) => (
              <option key={plan.code} value={plan.code}>
                {plan.name}
              </option>
            ))}
          </select>
          <small>
            Toggle the plan to show more or fewer pages, widgets and privileges.
          </small>
        </div>
      </section>

      <section className="demo-lifecycle-grid">
        <aside className="demo-stepper">
          {lifecycleSteps.map((step, index) => {
            const Icon = step.icon;
            const active = index === activeLifecycleStep;
            const done = index < activeLifecycleStep;

            return (
              <button
                key={step.title}
                className={`demo-stepper-item ${active ? "active" : ""} ${
                  done ? "done" : ""
                }`}
                type="button"
                onClick={() => setActiveLifecycleStep(index)}
              >
                <Icon size={18} />
                <span>{step.title}</span>
                {done ? <CheckCircle2 size={16} /> : null}
              </button>
            );
          })}
        </aside>

        <section className="demo-stage">
          <div className="demo-stage-header">
            <div>
              <div className="eyebrow">
                <ActiveIcon size={14} />
                Active workflow step
              </div>
              <h2>{activeStep.title}</h2>
              <p>{activeStep.description}</p>
            </div>

            <button
              className="primary-button"
              type="button"
              onClick={() =>
                setActiveLifecycleStep(
                  Math.min(activeLifecycleStep + 1, lifecycleSteps.length - 1)
                )
              }
            >
              Next step
              <ArrowRight size={16} />
            </button>
          </div>

          {activeLifecycleStep === 0 ? <ConnectorStage connectors={connectors} /> : null}
          {activeLifecycleStep === 1 ? <JobsStage jobs={jobs} /> : null}
          {activeLifecycleStep === 2 ? <SchemaStage mappings={mappings} /> : null}
          {activeLifecycleStep === 3 ? (
            <WidgetStage
              widgets={visibleWidgets}
              isFeatureAvailable={isFeatureAvailable}
            />
          ) : null}
          {activeLifecycleStep === 4 ? (
            <InvestigationStage isFeatureAvailable={isFeatureAvailable} />
          ) : null}
          {activeLifecycleStep === 5 ? (
            <MlStage
            mlPreview={mlPreview}
            selectedFeatures={mlTrainingForm.selectedFeatures}
            isFeatureAvailable={isFeatureAvailable}
            />         
         ) : null}
          {activeLifecycleStep === 6 ? (
            <ReportStage isFeatureAvailable={isFeatureAvailable} />
          ) : null}
        </section>
      </section>

      <section className="demo-integrated-grid">
        <LicenseWorkflowPanel />
        <UsersRolesPreview users={users} />
      </section>
    </main>
  );
}

function ConnectorStage({
  connectors,
}: {
  connectors: ReturnType<typeof useDemoMode>["connectors"];
}) {
  return (
    <div className="demo-card-grid">
      {connectors.map((connector) => (
        <article className="demo-card" key={connector.id}>
          <div className="demo-card-top">
            <DatabaseZap size={22} />
            <span className={`connector-status connector-status--${connector.status}`}>
              {connector.status}
            </span>
          </div>

          <h3>{connector.name}</h3>
          <p>{connector.description}</p>

          <div className="demo-mini-table">
            {connector.tables.length === 0 ? (
              <div className="empty-mini-row">
                Planned connector — shown for roadmap/demo only.
              </div>
            ) : (
              connector.tables.map((table) => (
                <div key={table.id}>
                  <strong>{table.name}</strong>
                  <span>{table.rows.toLocaleString()} rows</span>
                  <small>Last import: {table.lastImportedAt}</small>
                </div>
              ))
            )}
          </div>
        </article>
      ))}
    </div>
  );
}

function JobsStage({
  jobs,
}: {
  jobs: ReturnType<typeof useDemoMode>["jobs"];
}) {
  return (
    <div className="demo-card-grid">
      {jobs.map((job) => (
        <article className="demo-card" key={job.id}>
          <div className="demo-card-top">
            <Activity size={22} />
            <span className={`job-status job-status--${job.status.toLowerCase()}`}>
              {job.status}
            </span>
          </div>

          <h3>{job.name}</h3>
          <p>{job.message}</p>

          <div className="progress-track">
            <span style={{ width: `${job.progress}%` }} />
          </div>

          <div className="demo-card-footer">
            <small>Last: {job.lastRun}</small>
            <small>Next: {job.nextRun}</small>
          </div>
        </article>
      ))}
    </div>
  );
}

function SchemaStage({
  mappings,
}: {
  mappings: ReturnType<typeof useDemoMode>["mappings"];
}) {
  return (
    <div className="demo-card-grid demo-card-grid--two">
      {mappings.map((mapping) => (
        <article className="demo-card demo-card--wide" key={mapping.id}>
          <div className="demo-card-top">
            <TableProperties size={22} />
            <span className="status-chip">{mapping.status}</span>
          </div>

          <h3>{mapping.name}</h3>
          <p>
            {mapping.source} → <strong>{mapping.target}</strong>
          </p>

          <pre className="sql-preview">{mapping.sqlPreview}</pre>

          <div className="field-map-grid">
            {mapping.mappedFields.map((field) => (
              <div key={`${field.sourceField}-${field.targetField}`}>
                <strong>{field.sourceField}</strong>
                <ArrowRight size={14} />
                <span>{field.targetField}</span>
                <small>{field.confidence}% mapping confidence</small>
              </div>
            ))}
          </div>
        </article>
      ))}
    </div>
  );
}

function WidgetStage({
  widgets,
  isFeatureAvailable,
}: {
  widgets: ReturnType<typeof useDemoMode>["widgets"];
  isFeatureAvailable: ReturnType<typeof useDemoMode>["isFeatureAvailable"];
}) {
  return (
    <div className="demo-card-grid">
      {widgets.map((widget) => {
        const unlocked = isFeatureAvailable(widget.requiredPlan);

        return (
          <article
            className={`demo-card demo-widget-card ${!unlocked ? "locked" : ""}`}
            key={widget.id}
          >
            <div className="demo-card-top">
              <BarChart3 size={22} />
              <span className="status-chip">{widget.requiredPlan}</span>
            </div>

            <h3>{widget.title}</h3>
            <p>{widget.description}</p>

            {!unlocked ? (
              <div className="locked-inline">
                <LockKeyhole size={18} />
                Requires {widget.requiredPlan}
              </div>
            ) : (
              <>
                {widget.value ? (
                  <strong className="big-widget-value">{widget.value}</strong>
                ) : null}

                {widget.trend ? <small>{widget.trend}</small> : null}

                <div className="demo-chart-bars">
                  {widget.data.map((point) => (
                    <div key={point.label}>
                      <span>{point.label}</span>
                      <div>
                        <b style={{ width: `${point.value}%` }} />
                      </div>
                      <small>{point.value}</small>
                    </div>
                  ))}
                </div>

                <pre className="query-preview">{widget.queryPreview}</pre>
              </>
            )}
          </article>
        );
      })}
    </div>
  );
}

function InvestigationStage({
  isFeatureAvailable,
}: {
  isFeatureAvailable: ReturnType<typeof useDemoMode>["isFeatureAvailable"];
}) {
  const unlocked = isFeatureAvailable("ProPlus");

  return (
    <article className="demo-card demo-card--wide">
      <div className="demo-card-top">
        <Workflow size={22} />
        <span className="status-chip">Investigation-first story</span>
      </div>

      <h3>Quality Issue Investigation</h3>

      <p>
        Start from a defect signal, open the affected material, review genealogy,
        compare process windows, and identify suspected contributors.
      </p>

      {!unlocked ? (
        <div className="locked-inline">
          <LockKeyhole size={18} />
          Investigation workflow requires Pro Plus.
        </div>
      ) : (
        <div className="investigation-timeline">
          <div>
            <strong>Quality event</strong>
            <span>Surface defect detected on Coil C-2048</span>
          </div>
          <div>
            <strong>Material genealogy</strong>
            <span>Linked to upstream heat, slab and process route</span>
          </div>
          <div>
            <strong>Process evidence</strong>
            <span>Temperature instability and speed variation found</span>
          </div>
          <div>
            <strong>Suspected contributors</strong>
            <span>Correlation suggests temperature and speed windows</span>
          </div>
        </div>
      )}
    </article>
  );
}

function MlStage({
  mlPreview,
  selectedFeatures,
  isFeatureAvailable,
}: {
  mlPreview: ReturnType<typeof useDemoMode>["mlPreview"];
  selectedFeatures: string[];
  isFeatureAvailable: ReturnType<typeof useDemoMode>["isFeatureAvailable"];
}) {

  const unlocked = isFeatureAvailable("ProPlus");

  return (
    <article className="demo-card demo-card--wide">
      <div className="demo-card-top">
        <BrainCircuit size={22} />
        <span className="status-chip status-chip--warning">Preview only</span>
      </div>

      <h3>ML Workspace Preview</h3>

      <p>
        Demonstrates future workflow only. No trained production model is active.
        Current product intelligence remains rule-based risk scoring and
        correlation analysis.
      </p>

      {!unlocked ? (
        <div className="locked-inline">
          <LockKeyhole size={18} />
          ML preview requires Pro Plus.
        </div>
      ) : (
        <>
          <div className="feature-chip-grid">
            {selectedFeatures.map((feature) => (
                  <span className="feature-chip active" key={feature}>
                <CheckCircle2 size={14} />
                {feature}
              </span>
            ))}
          </div>

          <div className="ml-result-card">
            <ShieldCheck size={28} />
            <div>
              <strong>{mlPreview.resultLabel}</strong>
              <span>{mlPreview.confidence}% preview confidence</span>
            </div>
          </div>

          <div className="explainability-grid">
            {mlPreview.explanation.map((item) => (
              <div key={item.feature}>
                <strong>{item.feature}</strong>
                <span>{item.direction}</span>
                <div className="progress-track">
                  <span style={{ width: `${item.contribution}%` }} />
                </div>
              </div>
            ))}
          </div>
        </>
      )}
    </article>
  );
}

function ReportStage({
  isFeatureAvailable,
}: {
  isFeatureAvailable: ReturnType<typeof useDemoMode>["isFeatureAvailable"];
}) {
  const unlocked = isFeatureAvailable("Pro");

  return (
    <article className="demo-card demo-card--wide">
      <div className="demo-card-top">
        <FileText size={22} />
        <span className="status-chip">Customer-grade output</span>
      </div>

      <h3>Final Dashboard + Report Preview</h3>

      {!unlocked ? (
        <div className="locked-inline">
          <LockKeyhole size={18} />
          Report preview requires Pro.
        </div>
      ) : (
        <div className="report-preview">
          <h4>Quality Investigation Report — Coil C-2048</h4>
          <p>
            Elevated quality risk was detected. Evidence suggests temperature
            instability and speed variation as suspected contributors. Process
            engineering validation is required before operational action.
          </p>

          <div className="report-kpi-grid">
            <div>
              <strong>72</strong>
              <span>Risk score</span>
            </div>
            <div>
              <strong>4</strong>
              <span>Suspected contributors</span>
            </div>
            <div>
              <strong>18,420</strong>
              <span>Inspection records</span>
            </div>
          </div>
        </div>
      )}
    </article>
  );
}

function LicenseWorkflowPanel() {
  const { selectedPlan, setSelectedPlan } = useDemoMode();

  return (
    <article className="demo-card demo-card--wide">
      <div className="demo-card-top">
        <BadgeEuro size={22} />
        <span className="status-chip">Integrated license preview</span>
      </div>

      <h3>License / Feature / Pricing inside the app</h3>

      <p>
        This is not a billing engine yet. It is frontend feature visibility to
        demonstrate how Light, Pro, Pro Plus and Enterprise unlock different
        workflows.
      </p>

      <div className="license-plan-grid">
        {licensePlans.map((plan) => (
          <button
            key={plan.code}
            type="button"
            className={`license-plan-card ${
              selectedPlan === plan.code ? "active" : ""
            }`}
            onClick={() => setSelectedPlan(plan.code)}
          >
            <strong>{plan.name}</strong>
            <span>{plan.priceLabel}</span>
            <small>{plan.description}</small>
          </button>
        ))}
      </div>
    </article>
  );
}

function UsersRolesPreview({
  users,
}: {
  users: ReturnType<typeof useDemoMode>["users"];
}) {
  return (
    <article className="demo-card demo-card--wide">
      <div className="demo-card-top">
        <Users size={22} />
        <span className="status-chip">User / Role preview</span>
      </div>

      <h3>Users, Roles & Privileges</h3>

      <p>
        Frontend preview for future RBAC flow: add user, define role, assign
        privileges and show license visibility.
      </p>

      <div className="user-role-grid">
        {users.map((user) => (
          <div className="user-role-card" key={user.id}>
            <strong>{user.userName}</strong>
            <span>{user.role}</span>
            <small>{user.licensePlan} · {user.status}</small>

            <ul>
              {user.privileges.map((privilege) => (
                <li key={privilege}>{privilege}</li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </article>
  );
}