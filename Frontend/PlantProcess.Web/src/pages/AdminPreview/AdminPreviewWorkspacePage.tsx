import { useMemo, useState } from "react";
import {
  AlertTriangle,
  BadgeEuro,
  BarChart3,
  BrainCircuit,
  CalendarCheck,
  CheckCircle2,
  ClipboardCheck,
  DatabaseZap,
  FileText,
  GitBranch,
  LockKeyhole,
  MessageCircleQuestion,
  PlayCircle,
  RotateCcw,
  ShieldCheck,
  TableProperties,
  Users,
  XCircle,
} from "lucide-react";

import { LockedFeatureOverlay } from "@/components/demo/LockedFeatureOverlay";
import { licensePlans, useDemoMode } from "@/state/DemoModeContext";
import type { DemoLicensePlan } from "@/demo/plantProcessDemoScenario";

type AdminPreviewTab =
  | "license"
  | "features"
  | "users"
  | "ml"
  | "demo"
  | "report"
  | "truth";

const tabs: Array<{
  id: AdminPreviewTab;
  label: string;
  icon: React.ComponentType<{ size?: number }>;
}> = [
  { id: "license", label: "License & Access", icon: BadgeEuro },
  { id: "features", label: "Feature Matrix", icon: TableProperties },
  { id: "users", label: "Users / Roles", icon: Users },
  { id: "ml", label: "ML Workspace", icon: BrainCircuit },
  { id: "demo", label: "Demo Checklist", icon: ClipboardCheck },
  { id: "report", label: "Report Preview", icon: FileText },
  { id: "truth", label: "Truth Guard", icon: ShieldCheck },
];

export function AdminPreviewWorkspacePage() {
  const [activeTab, setActiveTab] = useState<AdminPreviewTab>("license");

  return (
    <main className="page-shell admin-preview-page">
      <section className="dashboard-hero admin-preview-hero">
        <div>
          <div className="eyebrow">
            <PlayCircle size={14} />
            Embedded frontend demo workflows
          </div>

          <h1>Admin Preview Workspace</h1>

          <p>
            This is the real app preview area for license access, feature
            matrix, users/roles, ML workspace, scripts, report output and demo
            truth checks. Backend enforcement can be connected later.
          </p>
        </div>

        <AdminPreviewSummary />
      </section>

      <section className="admin-preview-layout">
        <aside className="admin-preview-tabs">
          {tabs.map((tab) => {
            const Icon = tab.icon;
            const active = activeTab === tab.id;

            return (
              <button
                key={tab.id}
                className={`admin-preview-tab ${active ? "active" : ""}`}
                type="button"
                onClick={() => setActiveTab(tab.id)}
              >
                <Icon size={17} />
                <span>{tab.label}</span>
              </button>
            );
          })}
        </aside>

        <section className="admin-preview-panel">
          {activeTab === "license" ? <LicenseAccessTab /> : null}
          {activeTab === "features" ? <FeatureMatrixTab /> : null}
          {activeTab === "users" ? <UsersRolesTab /> : null}
          {activeTab === "ml" ? <MlWorkspaceTab /> : null}
          {activeTab === "demo" ? <DemoChecklistTab /> : null}
          {activeTab === "report" ? <ReportPreviewTab /> : null}
          {activeTab === "truth" ? <TruthGuardTab /> : null}
        </section>
      </section>
    </main>
  );
}

function AdminPreviewSummary() {
  const { selectedPlan, demoModeEnabled, resetDemoState } = useDemoMode();

  return (
    <div className="admin-preview-summary">
      <span className="status-chip">{demoModeEnabled ? "Demo mode on" : "Demo mode off"}</span>
      <strong>{selectedPlan}</strong>
      <small>Frontend preview state</small>

      <button className="secondary-button" type="button" onClick={resetDemoState}>
        <RotateCcw size={15} />
        Reset demo
      </button>
    </div>
  );
}

function LicenseAccessTab() {
  const { selectedPlan, setSelectedPlan } = useDemoMode();

  return (
    <div className="admin-preview-content">
      <SectionHeader
        icon={<BadgeEuro size={18} />}
        eyebrow="Priority 4"
        title="License / Feature / Pricing Preview"
        description="Toggle Light, Pro, Pro Plus and Enterprise to demonstrate feature progression inside the real application."
      />

      <div className="license-plan-grid admin-license-grid">
        {licensePlans.map((plan) => (
          <button
            key={plan.code}
            type="button"
            className={`license-plan-card ${
              selectedPlan === plan.code ? "active" : ""
            }`}
            onClick={() => setSelectedPlan(plan.code)}
          >
            <div className="license-plan-card-top">
              <strong>{plan.name}</strong>
              {selectedPlan === plan.code ? <CheckCircle2 size={18} /> : null}
            </div>

            <span>{plan.priceLabel}</span>

            <small>{plan.description}</small>

            <div className="license-limits-row">
              <b>{plan.users}</b>
              <span>users</span>
              <b>{plan.sources}</b>
              <span>sources</span>
              <b>{plan.dashboards}</b>
              <span>dashboards</span>
            </div>

            <ul>
              {plan.features.map((feature) => (
                <li key={feature}>{feature}</li>
              ))}
            </ul>
          </button>
        ))}
      </div>

      <div className="demo-note-block">
        <AlertTriangle size={18} />
        <p>
          This is not a billing engine yet. This is the correct demo-stage
          implementation: frontend visibility, lock states, plan progression
          and workflow simulation. Backend tenant enforcement can replace this
          state later.
        </p>
      </div>
    </div>
  );
}

function FeatureMatrixTab() {
  const { selectedPlan, privilegeGroups } = useDemoMode();

  const selectedColumn = selectedPlanToColumn(selectedPlan);

  return (
    <div className="admin-preview-content">
      <SectionHeader
        icon={<TableProperties size={18} />}
        eyebrow="Priority 4"
        title="Feature Matrix"
        description="A real feature access matrix for demo presentation, showing how Light → Pro → Pro Plus → Enterprise unlocks workflows."
      />

      <div className="feature-matrix-scroll">
        <table className="admin-feature-matrix">
          <thead>
            <tr>
              <th>Privilege</th>
              <th>Light</th>
              <th>Pro</th>
              <th>Pro Plus</th>
              <th>Enterprise</th>
            </tr>
          </thead>

          <tbody>
            {privilegeGroups.map((group) => (
              <>
                <tr key={group.group} className="feature-group-row">
                  <td colSpan={5}>{group.group}</td>
                </tr>

                {group.privileges.map((privilege) => (
                  <tr key={privilege.code}>
                    <td>
                      <strong>{privilege.label}</strong>
                      <small>{privilege.code}</small>
                    </td>
                    <MatrixCell enabled={privilege.light} active={selectedColumn === "light"} />
                    <MatrixCell enabled={privilege.pro} active={selectedColumn === "pro"} />
                    <MatrixCell enabled={privilege.proPlus} active={selectedColumn === "proPlus"} />
                    <MatrixCell enabled={privilege.enterprise} active={selectedColumn === "enterprise"} />
                  </tr>
                ))}
              </>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function UsersRolesTab() {
  const { users, selectedPlan, isFeatureAvailable } = useDemoMode();

  const unlocked = isFeatureAvailable("Enterprise");

  return (
    <div className="admin-preview-content">
      <SectionHeader
        icon={<Users size={18} />}
        eyebrow="Priority 4"
        title="Users / Roles / Privileges Preview"
        description="Preview the future RBAC workflow: add users, define roles, assign privileges and show license-based access."
      />

      {!unlocked ? (
        <LockedFeatureOverlay
          featureName="Advanced users, roles and privileges"
          requiredPlan="Enterprise"
        />
      ) : null}

      <div className={`user-role-grid ${!unlocked ? "preview-blurred" : ""}`}>
        {users.map((user) => (
          <article className="user-role-card" key={user.id}>
            <div className="user-role-card-top">
              <Users size={20} />
              <span className="status-chip">{user.status}</span>
            </div>

            <h3>{user.userName}</h3>
            <p>{user.email}</p>

            <div className="role-pill-row">
              <span>{user.role}</span>
              <span>{user.licensePlan}</span>
            </div>

            <ul>
              {user.privileges.map((privilege) => (
                <li key={privilege}>
                  <CheckCircle2 size={14} />
                  {privilege}
                </li>
              ))}
            </ul>
          </article>
        ))}
      </div>

      <div className="mock-action-row">
        <button className="secondary-button" type="button">
          <Users size={15} />
          Add user preview
        </button>

        <button className="secondary-button" type="button">
          <ShieldCheck size={15} />
          Define role preview
        </button>

        <button className="secondary-button" type="button">
          <LockKeyhole size={15} />
          Assign privileges preview
        </button>
      </div>

      <p className="demo-muted">
        Current selected plan: <strong>{selectedPlan}</strong>. Backend RBAC can
        be connected later without changing the demo story.
      </p>
    </div>
  );
}

function MlWorkspaceTab() {
  const {
    selectedPlan,
    isFeatureAvailable,
    mlPreview,
    mlTrainingForm,
    setMlTrainingForm,
  } = useDemoMode();

  const unlocked = isFeatureAvailable("ProPlus");

  function toggleFeature(feature: string) {
    const selected = mlTrainingForm.selectedFeatures.includes(feature);

    setMlTrainingForm({
      ...mlTrainingForm,
      selectedFeatures: selected
        ? mlTrainingForm.selectedFeatures.filter((item) => item !== feature)
        : [...mlTrainingForm.selectedFeatures, feature],
    });
  }

  const allFeatures = [
    "Equipment",
    "Process step",
    "Temperature trend",
    "Speed variation",
    "Material genealogy",
    "Defect history",
    "Shift context",
    "Quality history",
    "Downtime events",
    "Inspection device",
  ];

  return (
    <div className="admin-preview-content">
      <SectionHeader
        icon={<BrainCircuit size={18} />}
        eyebrow="Priority 7"
        title="ML Workspace Preview"
        description="Frontend-only ML workflow preview: feature selection, mock training form, model registry and explainability panel."
      />

      <div className="demo-note-block warning">
        <AlertTriangle size={18} />
        <p>{mlPreview.disclaimer}</p>
      </div>

      {!unlocked ? (
        <LockedFeatureOverlay
          featureName="ML Workspace Preview"
          requiredPlan="Pro Plus"
        />
      ) : null}

      <div className={`ml-preview-grid ${!unlocked ? "preview-blurred" : ""}`}>
        <article className="admin-preview-card">
          <div className="card-title-row">
            <GitBranch size={18} />
            <h3>Feature Selection</h3>
          </div>

          <div className="feature-chip-grid">
            {allFeatures.map((feature) => {
              const active = mlTrainingForm.selectedFeatures.includes(feature);

              return (
                <button
                  key={feature}
                  className={`feature-chip ${active ? "active" : ""}`}
                  type="button"
                  onClick={() => toggleFeature(feature)}
                >
                  {active ? <CheckCircle2 size={14} /> : <XCircle size={14} />}
                  {feature}
                </button>
              );
            })}
          </div>
        </article>

        <article className="admin-preview-card">
          <div className="card-title-row">
            <PlayCircle size={18} />
            <h3>Training Job Mock Form</h3>
          </div>

          <div className="mock-form-grid">
            <label>
              Target outcome
              <select
                value={mlTrainingForm.targetOutcome}
                onChange={(event) =>
                  setMlTrainingForm({
                    ...mlTrainingForm,
                    targetOutcome: event.target.value,
                  })
                }
              >
                <option>Quality risk class</option>
                <option>Defect family</option>
                <option>Downgrade / rework risk</option>
                <option>Inspection severity</option>
              </select>
            </label>

            <label>
              Time window
              <select
                value={mlTrainingForm.timeWindow}
                onChange={(event) =>
                  setMlTrainingForm({
                    ...mlTrainingForm,
                    timeWindow: event.target.value,
                  })
                }
              >
                <option>Last 30 days</option>
                <option>Last 90 days</option>
                <option>Last 180 days</option>
                <option>Last 12 months</option>
              </select>
            </label>

            <label>
              Validation method
              <select
                value={mlTrainingForm.validationMethod}
                onChange={(event) =>
                  setMlTrainingForm({
                    ...mlTrainingForm,
                    validationMethod: event.target.value,
                  })
                }
              >
                <option>Holdout validation</option>
                <option>K-fold preview</option>
                <option>Time-series split preview</option>
                <option>Human review only</option>
              </select>
            </label>
          </div>

          <button className="secondary-button" type="button" disabled>
            <BrainCircuit size={15} />
            Preview only — backend training disabled
          </button>
        </article>

        <article className="admin-preview-card">
          <div className="card-title-row">
            <DatabaseZap size={18} />
            <h3>Model Registry Placeholder</h3>
          </div>

          <div className="registry-table">
            {mlPreview.registry.map((model) => (
              <div key={`${model.modelName}-${model.version}`}>
                <strong>{model.modelName}</strong>
                <span>{model.version}</span>
                <small>{model.status}</small>
                <p>{model.note}</p>
              </div>
            ))}
          </div>
        </article>

        <article className="admin-preview-card">
          <div className="card-title-row">
            <BarChart3 size={18} />
            <h3>Explainability Mock Panel</h3>
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
        </article>
      </div>

      <p className="demo-muted">
        Selected plan: <strong>{selectedPlan}</strong>. This page intentionally
        says preview only. Do not say AI prediction or guaranteed root cause.
      </p>
    </div>
  );
}

function DemoChecklistTab() {
  const {
    checklist,
    screenshots,
    executiveFiveMinuteScript,
    twentyMinuteScript,
    objections,
    resetDemoState,
  } = useDemoMode();

  return (
    <div className="admin-preview-content">
      <SectionHeader
        icon={<ClipboardCheck size={18} />}
        eyebrow="Priority 6"
        title="Demo Preparation"
        description="20-minute script, 5-minute executive script, screenshot pack, objections, checklist and frontend demo reset."
      />

      <div className="mock-action-row">
        <button className="primary-button" type="button" onClick={resetDemoState}>
          <RotateCcw size={15} />
          Reset frontend demo state
        </button>

        <a
          className="secondary-button"
          href="mailto:info@plantprocessiq.com?subject=PlantProcess%20IQ%20demo%20request"
        >
          <CalendarCheck size={15} />
          Demo CTA mailto
        </a>
      </div>

      <div className="demo-prep-grid">
        <article className="admin-preview-card">
          <div className="card-title-row">
            <ClipboardCheck size={18} />
            <h3>Pre-demo Checklist</h3>
          </div>

          <ul className="checklist-list">
            {checklist.map((item) => (
              <li key={item.id}>
                {item.done ? <CheckCircle2 size={15} /> : <AlertTriangle size={15} />}
                <span>
                  <strong>{item.title}</strong>
                  <small>{item.acceptance}</small>
                </span>
                <b>{item.priority}</b>
              </li>
            ))}
          </ul>
        </article>

        <article className="admin-preview-card">
          <div className="card-title-row">
            <FileText size={18} />
            <h3>Screenshot Pack</h3>
          </div>

          <ul className="screenshot-list">
            {screenshots.map((item) => (
              <li key={item.id}>
                <span>
                  <strong>{item.title}</strong>
                  <small>{item.targetRoute} — {item.purpose}</small>
                </span>
              </li>
            ))}
          </ul>
        </article>

        <ScriptCard title="5-minute executive script" lines={executiveFiveMinuteScript} />
        <ScriptCard title="20-minute investigation-first script" lines={twentyMinuteScript} />

        <article className="admin-preview-card admin-preview-card--wide">
          <div className="card-title-row">
            <MessageCircleQuestion size={18} />
            <h3>Objection Handling</h3>
          </div>

          <div className="objection-grid">
            {objections.map((item) => (
              <div key={item.objection} className="objection-card">
                <strong>{item.objection}</strong>
                <p>{item.answer}</p>
              </div>
            ))}
          </div>
        </article>
      </div>
    </div>
  );
}

function ReportPreviewTab() {
  const { customerReportSections, isFeatureAvailable } = useDemoMode();

  const unlocked = isFeatureAvailable("Pro");

  return (
    <div className="admin-preview-content">
      <SectionHeader
        icon={<FileText size={18} />}
        eyebrow="Priority 6"
        title="Customer-Grade PDF Report Preview"
        description="Frontend report preview that can later be connected to real PDF generation."
      />

      {!unlocked ? (
        <LockedFeatureOverlay
          featureName="Customer-grade report"
          requiredPlan="Pro"
        />
      ) : null}

      <article className={`customer-report-preview ${!unlocked ? "preview-blurred" : ""}`}>
        <div className="report-header">
          <div>
            <h2>Quality Investigation Report</h2>
            <p>PlantProcess IQ — Demo Plant — Coil C-2048</p>
          </div>

          <span className="status-chip">Preview report</span>
        </div>

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
          <div>
            <strong>84%</strong>
            <span>Data readiness</span>
          </div>
        </div>

        {customerReportSections.map((section) => (
          <section key={section.title} className="report-section">
            <h3>{section.title}</h3>
            <p>{section.content}</p>
          </section>
        ))}

        <div className="report-footer-note">
          <AlertTriangle size={16} />
          This report uses suspected contributor wording. It does not claim
          guaranteed root cause or live AI prediction.
        </div>

        <button className="secondary-button" type="button" disabled>
          <FileText size={15} />
          Export PDF later — preview only
        </button>
      </article>
    </div>
  );
}

function TruthGuardTab() {
  const {
    connectors,
    forbiddenLanguageExamples,
    safeLanguageExamples,
  } = useDemoMode();

  const unavailableButHonest = connectors.filter((x) => !x.availableNow);

  return (
    <div className="admin-preview-content">
      <SectionHeader
        icon={<ShieldCheck size={18} />}
        eyebrow="Priority 2"
        title="Demo Truth Guard"
        description="Connector honesty, AI wording safety, URL/login/seed checklist and browser console validation target."
      />

      <div className="truth-grid">
        <article className="admin-preview-card">
          <div className="card-title-row">
            <DatabaseZap size={18} />
            <h3>Connector Honesty</h3>
          </div>

          <div className="connector-truth-list">
            {connectors.map((connector) => (
              <div key={connector.id}>
                <strong>{connector.name}</strong>
                <span>{connector.safeDemoLabel}</span>
                <small>{connector.description}</small>
              </div>
            ))}
          </div>

          <p className="demo-muted">
            Planned/future connectors:{" "}
            {unavailableButHonest.map((item) => item.provider).join(", ")}.
          </p>
        </article>

        <article className="admin-preview-card">
          <div className="card-title-row">
            <AlertTriangle size={18} />
            <h3>Forbidden Wording</h3>
          </div>

          <ul className="language-list forbidden">
            {forbiddenLanguageExamples.map((item) => (
              <li key={item}>
                <XCircle size={15} />
                {item}
              </li>
            ))}
          </ul>
        </article>

        <article className="admin-preview-card">
          <div className="card-title-row">
            <CheckCircle2 size={18} />
            <h3>Safe Wording</h3>
          </div>

          <ul className="language-list safe">
            {safeLanguageExamples.map((item) => (
              <li key={item}>
                <CheckCircle2 size={15} />
                {item}
              </li>
            ))}
          </ul>
        </article>

        <article className="admin-preview-card">
          <div className="card-title-row">
            <ClipboardCheck size={18} />
            <h3>URL / Login / Seed Confirmation</h3>
          </div>

          <div className="url-confirmation-grid">
            <div>
              <strong>App URL</strong>
              <span>https://app.plantprocessiq.com</span>
            </div>
            <div>
              <strong>API URL</strong>
              <span>https://api.plantprocessiq.com</span>
            </div>
            <div>
              <strong>Website URL</strong>
              <span>https://plantprocessiq.com</span>
            </div>
            <div>
              <strong>Login</strong>
              <span>Use seeded demo admin from deployment env. Never expose real password.</span>
            </div>
            <div>
              <strong>Seeded data</strong>
              <span>Materials, genealogy, defects, process observations, jobs and reports.</span>
            </div>
            <div>
              <strong>Browser console</strong>
              <span>Must pass Playwright no-failed-request guard.</span>
            </div>
          </div>
        </article>
      </div>
    </div>
  );
}

function SectionHeader({
  icon,
  eyebrow,
  title,
  description,
}: {
  icon: React.ReactNode;
  eyebrow: string;
  title: string;
  description: string;
}) {
  return (
    <div className="section-heading">
      <div>
        <div className="eyebrow">
          {icon}
          {eyebrow}
        </div>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>
    </div>
  );
}

function MatrixCell({
  enabled,
  active,
}: {
  enabled: boolean;
  active: boolean;
}) {
  return (
    <td className={active ? "active-plan-column" : ""}>
      {enabled ? (
        <span className="matrix-yes">
          <CheckCircle2 size={15} />
          Included
        </span>
      ) : (
        <span className="matrix-no">
          <LockKeyhole size={15} />
          Locked
        </span>
      )}
    </td>
  );
}

function ScriptCard({ title, lines }: { title: string; lines: string[] }) {
  return (
    <article className="admin-preview-card">
      <div className="card-title-row">
        <PlayCircle size={18} />
        <h3>{title}</h3>
      </div>

      <ol className="script-list">
        {lines.map((line) => (
          <li key={line}>{line}</li>
        ))}
      </ol>
    </article>
  );
}

function selectedPlanToColumn(plan: DemoLicensePlan) {
  if (plan === "Light") return "light";
  if (plan === "Pro") return "pro";
  if (plan === "ProPlus") return "proPlus";
  return "enterprise";
}