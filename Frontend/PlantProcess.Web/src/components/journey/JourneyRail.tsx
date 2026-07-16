import { NavLink, useLocation } from "react-router-dom";
import { BellRing, Check, ChevronRight } from "lucide-react";
import "./JourneyRail.css";

type Stage = {
  n: number;
  label: string;
  shortLabel: string;
  to: string;
  match: string[];
  group: "Data" | "Experience" | "Intelligence" | "Governance";
};

const STAGES: ReadonlyArray<Stage> = [
  { n: 1, label: "Connect", shortLabel: "Connect", to: "/data-integration/connections", match: ["/data-integration/connections"], group: "Data" },
  { n: 2, label: "Register & schedule", shortLabel: "Register", to: "/data-integration/registry", match: ["/data-integration/registry", "/data-integration/prepare"], group: "Data" },
  { n: 3, label: "Incremental import", shortLabel: "Import", to: "/data-integration/importing", match: ["/data-integration/importing"], group: "Data" },
  { n: 4, label: "Prepare mapping", shortLabel: "Prepare", to: "/data-integration/prepare", match: ["/data-integration/prepare"], group: "Data" },
  { n: 5, label: "Load to plant data", shortLabel: "Map", to: "/data-integration/author-mapping", match: ["/data-integration/author-mapping"], group: "Data" },
  { n: 6, label: "Verify loaded data", shortLabel: "Loaded", to: "/materials", match: ["/materials", "/material-investigation"], group: "Data" },
  { n: 7, label: "Dashboards & widgets", shortLabel: "Dashboards", to: "/dashboard", match: ["/dashboard", "/page-builder", "/analytics-widgets"], group: "Experience" },
  { n: 8, label: "Author analysis", shortLabel: "Analysis", to: "/investigate/analysis-jobs", match: ["/investigate/analysis-jobs"], group: "Intelligence" },
  { n: 9, label: "Run analysis jobs", shortLabel: "Run", to: "/investigate/inspect", match: ["/investigate/inspect", "/investigate/advanced"], group: "Intelligence" },
  { n: 10, label: "Review findings", shortLabel: "Findings", to: "/correlations", match: ["/correlations", "/correlation", "/risk", "/data-quality", "/quality"], group: "Intelligence" },
  { n: 11, label: "AI/ML readiness", shortLabel: "ML ready", to: "/ml-readiness", match: ["/ml-readiness"], group: "Intelligence" },
  { n: 12, label: "AI/ML jobs", shortLabel: "ML jobs", to: "/data-integration/jobs", match: ["/data-integration/jobs"], group: "Intelligence" },
  { n: 13, label: "AI/ML results", shortLabel: "ML results", to: "/suggestions", match: ["/suggestions"], group: "Intelligence" },
  { n: 14, label: "Engine supervisor", shortLabel: "Supervisor", to: "/data-integration/supervisor", match: ["/data-integration/supervisor"], group: "Governance" },
  { n: 15, label: "Grounded assistant", shortLabel: "Assistant", to: "/assistant", match: ["/assistant"], group: "Governance" },
];

function activeIndex(pathname: string): number {
  let best = -1;
  let bestLength = -1;
  STAGES.forEach((stage, index) => {
    stage.match.forEach((prefix) => {
      const matches = pathname === prefix || pathname.startsWith(prefix + "/");
      if (matches && prefix.length > bestLength) {
        best = index;
        bestLength = prefix.length;
      }
    });
  });
  return best;
}

export function JourneyRail() {
  const { pathname } = useLocation();
  const current = activeIndex(pathname);
  const currentStage = current >= 0 ? STAGES[current] : null;
  const nextStage = current >= 0 && current < STAGES.length - 1 ? STAGES[current + 1] : STAGES[0];

  return (
    <nav className="piq-journey-rail" aria-label="PlantProcess IQ canonical journey">
      <div className="piq-journey-rail__head">
        <div className="piq-journey-rail__identity">
          <span className="piq-journey-rail__eyebrow">Canonical journey</span>
          <strong className="piq-journey-rail__current">
            {currentStage ? `Step ${currentStage.n} of 15 - ${currentStage.label}` : "15-step product journey"}
          </strong>
        </div>

        <div className="piq-journey-rail__actions">
          <NavLink className="piq-journey-rail__alerts" to="/data-integration/alerting">
            <BellRing size={15} aria-hidden="true" />
            Plant data log
          </NavLink>
          <NavLink className="piq-journey-rail__next" to={nextStage.to}>
            Next: {nextStage.shortLabel}
            <ChevronRight size={15} aria-hidden="true" />
          </NavLink>
        </div>
      </div>

      <div className="piq-journey-rail__viewport" tabIndex={0} aria-label="Journey steps; scroll horizontally on small screens">
        <ol className="piq-journey-rail__track">
          {STAGES.map((stage, index) => {
            const state = index === current ? "current" : current >= 0 && index < current ? "done" : "upcoming";
            return (
              <li key={stage.n} className={`piq-journey-node piq-journey-node--${state}`} data-group={stage.group}>
                <NavLink
                  className="piq-journey-node__link"
                  to={stage.to}
                  title={`${stage.n}. ${stage.label}`}
                  aria-current={state === "current" ? "step" : undefined}
                >
                  <span className="piq-journey-node__dot" aria-hidden="true">
                    {state === "done" ? <Check size={12} strokeWidth={3} /> : stage.n}
                  </span>
                  <span className="piq-journey-node__label">{stage.shortLabel}</span>
                </NavLink>
              </li>
            );
          })}
        </ol>
      </div>
    </nav>
  );
}

export default JourneyRail;
