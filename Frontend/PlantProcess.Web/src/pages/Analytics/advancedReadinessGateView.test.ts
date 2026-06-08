
import { describe, expect, it } from "vitest";
import {
  normalizeReadinessGateState,
  readinessGateSummaryText,
  readinessGateView,
} from "./advancedReadinessGateView";

describe("T-045 advanced readiness gate view", () => {
  it("normalizes Ready / Partial / Blocked states", () => {
    expect(normalizeReadinessGateState("Ready")).toBe("Ready");
    expect(normalizeReadinessGateState("Warning")).toBe("Partial");
    expect(normalizeReadinessGateState("Partial")).toBe("Partial");
    expect(normalizeReadinessGateState("Failed")).toBe("Blocked");
    expect(normalizeReadinessGateState("Blocked")).toBe("Blocked");
  });

  it("returns UI tone for HMI badges", () => {
    expect(readinessGateView("Ready")).toEqual({ state: "Ready", label: "READY", tone: "success" });
    expect(readinessGateView("Partial").tone).toBe("warning");
    expect(readinessGateView("Blocked").tone).toBe("danger");
  });

  it("builds explicit readiness summary text", () => {
    expect(readinessGateSummaryText("Ready", 4, 0, 0)).toContain("may run");
    expect(readinessGateSummaryText("Partial", 3, 1, 0)).toContain("partial");
    expect(readinessGateSummaryText("Blocked", 2, 0, 1)).toContain("must abstain");
  });
});
