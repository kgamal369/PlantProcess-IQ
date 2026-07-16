// FILE: src/components/journey/JourneyRail.tsx
// M1-17: persistent, read-only "step X of N" affordance. Reflects route only;
// no backend logic. Alerts is a roadmap node (4th UI, M1-06) shown as "soon".
import { NavLink, useLocation } from "react-router-dom";
import "./JourneyRail.css";

type Stage = {
  n: number;
  label: string;
  to: string | null;       // null => roadmap node (not yet a destination)
  match: string[];         // path prefixes that mean "you are on this stage"
};

const STAGES: Stage[] = [
  { n: 1,  label: "Connect",    to: "/data-integration/connections", match: ["/data-integration/connections"] },
  { n: 2,  label: "Schedule",   to: "/data-integration/jobs",        match: ["/data-integration/jobs"] },
  { n: 3,  label: "Import",     to: "/data-integration/importing",   match: ["/data-integration/importing"] },
  { n: 4,  label: "Prepare",    to: "/data-integration/prepare",     match: ["/data-integration/prepare"] },
  { n: 5,  label: "Load",       to: "/data-integration/registry",    match: ["/data-integration/registry"] },
  { n: 6,  label: "Dashboards", to: "/dashboard",                    match: ["/dashboard"] },
  { n: 7,  label: "Analysis",   to: "/correlations",                 match: ["/correlations"] },
  { n: 8,  label: "Findings",   to: "/risk",                         match: ["/risk", "/materials", "/data-quality"] },
  { n: 9,  label: "Alerts",     to: null,                            match: [] },
  { n: 10, label: "Assistant",  to: "/assistant",                    match: ["/assistant", "/suggestions"] },
];

function activeIndex(pathname: string): number {
  // longest-prefix wins so /assistant/configuration still resolves to Assistant
  let best = -1;
  let bestLen = -1;
  STAGES.forEach((s, i) => {
    s.match.forEach((m) => {
      if (pathname === m || pathname.startsWith(m + "/") || pathname.startsWith(m)) {
        if (m.length > bestLen) { bestLen = m.length; best = i; }
      }
    });
  });
  return best;
}

export function JourneyRail() {
  const { pathname } = useLocation();
  const current = activeIndex(pathname);

  // next actionable stage after the current one (skip roadmap nodes without a route)
  let nextStage: Stage | null = null;
  for (let i = current + 1; i < STAGES.length; i++) {
    if (STAGES[i].to) { nextStage = STAGES[i]; break; }
  }

  const currentLabel = current >= 0 ? STAGES[current].label : "Start";
  const currentNumber = current >= 0 ? STAGES[current].n : 0;

  return (
    <nav className="piq-journey-rail" aria-label="Product journey progress">
      <div className="piq-journey-rail__head">
        <span className="piq-journey-rail__step">
          Step {currentNumber} of 15
        </span>
        <span className="piq-journey-rail__here">{currentLabel}</span>
        {nextStage && (
          <NavLink className="piq-journey-rail__next" to={nextStage.to as string}>
            Next: {nextStage.label} &rarr;
          </NavLink>
        )}
      </div>

      <ol className="piq-journey-rail__track">
        {STAGES.map((s, i) => {
          const state =
            i === current ? "current" : i < current ? "done" : "upcoming";
          const roadmap = s.to === null;
          const cls = `piq-journey-node piq-journey-node--${state}${roadmap ? " piq-journey-node--roadmap" : ""}`;
          const inner = (
            <span className="piq-journey-node__inner">
              <span className="piq-journey-node__dot" aria-hidden="true">{s.n}</span>
              <span className="piq-journey-node__label">{s.label}</span>
              {roadmap && <span className="piq-journey-node__soon">soon</span>}
            </span>
          );
          return (
            <li key={s.n} className={cls}>
              {roadmap || !s.to ? (
                <span className="piq-journey-node__link" aria-disabled="true">{inner}</span>
              ) : (
                <NavLink className="piq-journey-node__link" to={s.to} aria-current={i === current ? "step" : undefined}>
                  {inner}
                </NavLink>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

export default JourneyRail;