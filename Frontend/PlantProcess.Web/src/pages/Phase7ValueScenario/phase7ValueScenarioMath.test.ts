
import { describe, expect, it } from "vitest";
import {
  formatMoney,
  normalizeImpact,
  normalizeRealization,
  workedCaseLocalProjection,
} from "./phase7ValueScenarioMath";

describe("T-040 Phase7 value scenario page math", () => {
  it("reproduces the EUR 28k-56k worked-case projection", () => {
    expect(workedCaseLocalProjection()).toEqual({
      low: 28_000,
      expected: 42_000,
      high: 56_000,
    });
  });

  it("normalizes camelCase and PascalCase impact results", () => {
    expect(
      normalizeImpact({
        Currency: "EUR",
        Low: 28_000,
        Expected: 42_000,
        High: 56_000,
        IsAbstained: false,
        HonestyCaveat: "not a guaranteed saving",
      })
    ).toMatchObject({
      currency: "EUR",
      low: 28_000,
      expected: 42_000,
      high: 56_000,
      isAbstained: false,
    });

    expect(
      normalizeImpact({
        currency: "EUR",
        low: 28_000,
        mid: 42_000,
        high: 56_000,
      })
    ).toMatchObject({
      expected: 42_000,
    });
  });

  it("normalizes realization result and caveat", () => {
    const result = normalizeRealization({
      currency: "EUR",
      realizedLow: 2_000,
      realizedMid: 3_000,
      realizedHigh: 4_000,
      improvementUnits: 20,
      captureRateMid: 0.0714,
      roiMid: 3,
      status: "PositiveTrackedValue",
      attributionCaveat: "Correlation is not causation",
    });

    expect(result.expected).toBe(3_000);
    expect(result.captureRateMid).toBe(0.0714);
    expect(result.roiMid).toBe(3);
    expect(result.caveat).toContain("Correlation is not causation");
  });

  it("formats money for the scenario cards", () => {
    expect(formatMoney(28_000, "EUR")).toContain("28,000");
  });
});
