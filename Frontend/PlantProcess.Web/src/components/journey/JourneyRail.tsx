import { Link, NavLink, useLocation } from "react-router-dom";
import { BellRing, Check, ChevronRight } from "lucide-react";
import "./JourneyRail.css";

/**
 * PPIQ-T15 - the canonical journey, and only the canonical journey.
 *
 * Chapter 2 section 3.3.1: "There is exactly one user journey in this product.
 * It is numbered J1 to J15 and it is defined here... A second journey written
 * anywhere is deleted rather than reconciled."
 *
 * Every label below is VERBATIM from that section. If a label here and the
 * chapter ever disagree, the chapter wins and journeyRailCanonical.test.ts
 * fails until they agree again.
 *
 * commissioned: J1 to J3 commission the platform. By the time anyone sees this
 * rail they are complete, so they render as done and are NOT links - J1's
 * surface is Login and Home, and there is no Home route to send anyone to.
 */
type Stage = {
  n: number;
  label: string;
  shortLabel: string;
  to: string;
  match: string[];
  group: "Data" | "Experience" | "Intelligence" | "Governance";
  commissioned?: boolean;
};

const STAGES: ReadonlyArray<Stage> = [
  { n: 1, label: "Install and first login", shortLabel: "Install", to: "", match: [], group: "Governance", commissioned: true },
  { n: 2, label: "Activate the licence", shortLabel: "Licence", to: "", match: ["/commercial-license", "/commercial/license"], group: "Governance", commissioned: true },
  { n: 3, label: "Create users and roles", shortLabel: "Users", to: "", match: ["/access-matrix"], group: "Governance", commissioned: true },
  { n: 4, label: "Declare read-only connections", shortLabel: "Connect", to: "/data-integration/connections", match: ["/data-integration/connections", "/connectors/historian", "/historian-connector"], group: "Data" },
  { n: 5, label: "Register datasets", shortLabel: "Register", to: "/data-integration/registry", match: ["/data-integration/registry", "/data-integration/prepare"], group: "Data" },
  { n: 6, label: "First incremental import", shortLabel: "Import", to: "/data-integration/importing", match: ["/data-integration/importing", "/data-integration/jobs"], group: "Data" },
  { n: 7, label: "Author the transformation and publish the relationship model", shortLabel: "Transform", to: "/prep/canvas", match: ["/prep/canvas"], group: "Data" },
  { n: 8, label: "Project to canonical, with validation", shortLabel: "Project", to: "/mapping-health", match: ["/mapping-health", "/data-quality", "/quality"], group: "Data" },
  { n: 9, label: "Walk the genealogy", shortLabel: "Genealogy", to: "/materials", match: ["/materials", "/material-investigation"], group: "Data" },
  { n: 10, label: "Build pages, widgets and filters", shortLabel: "Build", to: "/page-builder", match: ["/page-builder", "/analytics-widgets", "/pages"], group: "Experience" },
  { n: 11, label: "Explore associatively", shortLabel: "Explore", to: "/dashboard", match: ["/dashboard", "/workspace"], group: "Experience" },
  { n: 12, label: "Author and run analysis through the gate", shortLabel: "Analyse", to: "/analysis/toolbox", match: ["/analysis/toolbox", "/investigate/inspect", "/investigate/advanced", "/ml-readiness"], group: "Intelligence" },
  { n: 13, label: "Read findings, risk, practices and value", shortLabel: "Findings", to: "/correlations", match: ["/correlations", "/correlation", "/risk", "/advisory/value-realization", "/advisory/roi-cfo-dashboard", "/value/executive"], group: "Intelligence" },
  { n: 14, label: "Decide, act and measure", shortLabel: "Act", to: "/suggestions", match: ["/suggestions", "/advisory/recommendations", "/advisory/scenario-simulation", "/value/scenario"], group: "Intelligence" },
  { n: 15, label: "Operate, govern and retain", shortLabel: "Operate", to: "/data-integration/alerting", match: ["/data-integration/alerting", "/data-integration/supervisor", "/data-integration/connector-truth", "/advisory/honesty-certification", "/advisory/benchmarking", "/executive", "/edge-agent", "/edge-collector", "/assistant/configuration", "/assistant-config", "/widget-script-compiler"], group: "Governance" },
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
  // Never send anyone to a commissioned stage - they are done and have no target.
  const firstLive = STAGES.find((s) => !s.commissioned) ?? STAGES[STAGES.length - 1];
  const nextStage =
    current >= 0 && current < STAGES.length - 1
      ? STAGES.slice(current + 1).find((s) => !s.commissioned) ?? firstLive
      : firstLive;

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
            // J1 to J3 are commissioning: complete before anyone opens the app.
            const state = stage.commissioned
              ? "done"
              : index === current
                ? "current"
                : current >= 0 && index < current
                  ? "done"
                  : "upcoming";
            return (
              <li key={stage.n} className={`piq-journey-node piq-journey-node--${state}`} data-group={stage.group}>
                {/*
                  T-250/F2: Link, not NavLink, and the reason matters.

                  NavLink does not forward aria-current. It recomputes it as
                  isActive ? ariaCurrentProp : undefined, and isActive compares
                  the URL to this link's own `to`. A stage reached through one
                  of its alias prefixes - supervisor or the assistant route
                  under J15, for instance - is therefore current on screen and
                  silent to assistive technology, because its `to` points
                  somewhere else.

                  This component already knows which stage is current. Link
                  renders the attribute it is given. JourneyRail.css styles the
                  current node through .piq-journey-node--current on the li and
                  carries no .active rule, so nothing depended on NavLink here.
                */}
                <Link
                  className="piq-journey-node__link"
                  to={stage.to || "/dashboard"}
                  title={`${stage.n}. ${stage.label}`}
                  aria-current={state === "current" ? "step" : undefined}
                >
                  <span className="piq-journey-node__dot" aria-hidden="true">
                    {state === "done" ? <Check size={12} strokeWidth={3} /> : stage.n}
                  </span>
                  <span className="piq-journey-node__label">{stage.shortLabel}</span>
                </Link>
              </li>
            );
          })}
        </ol>
      </div>
    </nav>
  );
}

export default JourneyRail;
