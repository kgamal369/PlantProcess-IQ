// P5-T04: when the engine abstains (insufficient evidence) or a readiness gate is blocked, EVERY
// analytics surface renders this one standardized panel with the reason - never a misleading zero or
// blank. Maps 1:1 onto ReadinessGateState (Ready/Partial/Blocked) used across the analytics surfaces.
import type { ReactElement } from "react";

export type AbstainReasonState = "InsufficientEvidence" | "Blocked" | "Partial";

const TITLES: Record<AbstainReasonState, string> = {
  InsufficientEvidence: "Insufficient evidence — analysis abstained",
  Blocked: "Readiness gate blocked — analysis abstained",
  Partial: "Partial readiness — result shown with caveats",
};

export function AbstainPanel({
  state = "InsufficientEvidence",
  reason,
}: {
  state?: AbstainReasonState;
  reason?: string | null;
}): ReactElement {
  return (
    <div className="ppiq-abstain-panel" role="status" data-testid="abstain-panel" data-state={state}>
      <strong className="ppiq-abstain-title">{TITLES[state]}</strong>
      {reason ? <p className="ppiq-abstain-reason">{reason}</p> : null}
      <p className="ppiq-abstain-note">
        PPIQ states what the evidence supports and abstains when it does not — it never fabricates a result.
      </p>
    </div>
  );
}
