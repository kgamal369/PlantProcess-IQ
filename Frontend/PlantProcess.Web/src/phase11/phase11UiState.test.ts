import { describe, expect, it } from "vitest";
import {
  calculateProgress,
  explicitDisabledReason,
  resolvePhase11FiveState,
  shouldShowSlowOperationProgress,
} from "./phase11UiState";

describe("Phase 11 universal five-state/progress runtime", () => {
  it("T-068 returns error state with retry recovery", () => {
    const decision = resolvePhase11FiveState({ error: new Error("boom") });

    expect(decision.state).toBe("error");
    expect(decision.recoveryAction).toBe("retry");
  });

  it("T-065 shows progress for loading operations", () => {
    const decision = resolvePhase11FiveState({ isLoading: true, itemCount: 4, expectedCount: 10 });

    expect(decision.state).toBe("loading");
    expect(decision.showProgress).toBe(true);
    expect(decision.progressPercent).toBe(40);
    expect(decision.disabledReason).toMatch(/progress/i);
  });

  it("T-068 returns empty state with refine-filter recovery", () => {
    const decision = resolvePhase11FiveState({ itemCount: 0 });

    expect(decision.state).toBe("empty");
    expect(decision.recoveryAction).toBe("refine-filter");
  });

  it("T-068 returns partial and ready states explicitly", () => {
    expect(resolvePhase11FiveState({ hasPartialData: true, itemCount: 2 }).state).toBe("partial");
    expect(resolvePhase11FiveState({ itemCount: 3 }).state).toBe("ready");
  });

  it("T-065 identifies slow operations and clamps progress", () => {
    expect(shouldShowSlowOperationProgress(401)).toBe(true);
    expect(calculateProgress(12, 10)).toBe(100);
    expect(calculateProgress(-2, 10)).toBe(0);
    expect(explicitDisabledReason("")).toMatch(/disabled/i);
  });
});