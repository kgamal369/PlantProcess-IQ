
export type ReadinessGateState = "Ready" | "Partial" | "Blocked";

export type ReadinessGateView = {
  state: ReadinessGateState;
  label: string;
  tone: "success" | "warning" | "danger";
};

export function normalizeReadinessGateState(raw: string | null | undefined): ReadinessGateState {
  if (!raw) return "Blocked";

  const value = raw.trim().toLowerCase();

  if (value === "ready") return "Ready";
  if (value === "partial" || value === "warning" || value === "warn") return "Partial";
  if (value === "blocked" || value === "failed" || value === "blocker") return "Blocked";

  if (value.includes("partial")) return "Partial";
  if (value.includes("ready")) return "Ready";

  return "Blocked";
}

export function readinessGateView(raw: string | null | undefined): ReadinessGateView {
  const state = normalizeReadinessGateState(raw);

  if (state === "Ready") {
    return { state, label: "READY", tone: "success" };
  }

  if (state === "Partial") {
    return { state, label: "PARTIAL", tone: "warning" };
  }

  return { state, label: "BLOCKED", tone: "danger" };
}

export function readinessGateSummaryText(state: string, ready: number, partial: number, blocked: number): string {
  const normalized = normalizeReadinessGateState(state);

  if (normalized === "Ready") {
    return `Ready: ${ready} gate(s) ready; advanced analysis may run.`;
  }

  if (normalized === "Partial") {
    return `Partial: ${ready} ready, ${partial} partial, ${blocked} blocked.`;
  }

  return `Blocked: ${blocked} blocking gate(s); advanced analysis must abstain.`;
}
