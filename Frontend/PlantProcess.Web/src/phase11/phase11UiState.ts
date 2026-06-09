export type Phase11FiveState = "idle" | "loading" | "empty" | "partial" | "ready" | "error";

export type Phase11StateInput = {
  isLoading?: boolean;
  error?: unknown;
  itemCount?: number | null;
  expectedCount?: number | null;
  hasPartialData?: boolean;
};

export type Phase11StateDecision = {
  state: Phase11FiveState;
  showProgress: boolean;
  progressPercent: number | null;
  disablePrimaryAction: boolean;
  disabledReason: string;
  recoveryAction: "none" | "retry" | "refine-filter" | "wait";
};

export function resolvePhase11FiveState(input: Phase11StateInput): Phase11StateDecision {
  if (input.error) {
    return {
      state: "error",
      showProgress: false,
      progressPercent: null,
      disablePrimaryAction: false,
      disabledReason: "",
      recoveryAction: "retry",
    };
  }

  if (input.isLoading) {
    const progress = calculateProgress(input.itemCount ?? null, input.expectedCount ?? null);

    return {
      state: "loading",
      showProgress: true,
      progressPercent: progress,
      disablePrimaryAction: true,
      disabledReason: "Operation is in progress.",
      recoveryAction: "wait",
    };
  }

  const count = input.itemCount ?? 0;

  if (input.hasPartialData) {
    return {
      state: "partial",
      showProgress: false,
      progressPercent: null,
      disablePrimaryAction: false,
      disabledReason: "",
      recoveryAction: "retry",
    };
  }

  if (count === 0) {
    return {
      state: "empty",
      showProgress: false,
      progressPercent: null,
      disablePrimaryAction: false,
      disabledReason: "",
      recoveryAction: "refine-filter",
    };
  }

  return {
    state: "ready",
    showProgress: false,
    progressPercent: null,
    disablePrimaryAction: false,
    disabledReason: "",
    recoveryAction: "none",
  };
}

export function calculateProgress(done: number | null, total: number | null) {
  if (done === null || total === null || total <= 0) return null;

  return Math.max(0, Math.min(100, Math.round((done / total) * 100)));
}

export function shouldShowSlowOperationProgress(durationMs: number) {
  return durationMs >= 400;
}

export function explicitDisabledReason(reason: string | null | undefined) {
  return reason && reason.trim().length > 0
    ? reason.trim()
    : "Action is disabled until the required input or permission is available.";
}