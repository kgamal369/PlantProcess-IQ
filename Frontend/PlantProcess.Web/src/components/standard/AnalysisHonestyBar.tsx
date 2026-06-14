// PPIQ-304: the single honesty surface every analysis page must render. It states
// population + exclusions and, when the engine abstains or a gate is blocked,
// shows AbstainPanel instead of a fabricated driver. Composes the existing
// PopulationBadge + AbstainPanel primitives - no new visual language.
import type { ReactElement } from "react";
import { PopulationBadge } from "./PopulationBadge";
import { AbstainPanel } from "./AbstainPanel";

export function AnalysisHonestyBar({
  population,
  excluded = 0,
  blocked = false,
  reason,
}: {
  population: number | null | undefined;
  excluded?: number;
  blocked?: boolean;
  reason?: string | null;
}): ReactElement {
  const n = typeof population === "number" && Number.isFinite(population) ? population : 0;
  const included = Math.max(0, n - (excluded ?? 0));

  if (blocked || included <= 0) {
    return (
      <div className="ppiq-analysis-honesty-bar" data-testid="analysis-honesty-bar" data-state="abstain">
        <AbstainPanel state={blocked ? "Blocked" : "InsufficientEvidence"} reason={reason} />
      </div>
    );
  }

  return (
    <div className="ppiq-analysis-honesty-bar" data-testid="analysis-honesty-bar" data-state="ready">
      <PopulationBadge n={included} />
      <span className="ppiq-analysis-honesty-detail" data-testid="population-exclusions">
        {included} of {n} included{excluded > 0 ? ` · ${excluded} excluded` : ""}
      </span>
    </div>
  );
}