
import { describe, expect, it } from "vitest";
import {
  buildMonthlyValueReportHtml,
  computePayback,
  computeWorkedCasePreview,
  formatMoney,
  normalizeImpact,
} from "../../api/p3T14ValueExecutive";

describe("P3-T14 executive value surface helpers", () => {
  it("reproduces the worked EUR 28k / 42k / 56k range from arithmetic inputs", () => {
    const preview = computeWorkedCasePreview();

    expect(preview.affectedTons).toBe(200);
    expect(preview.low).toBe(28000);
    expect(preview.mid).toBe(42000);
    expect(preview.high).toBe(56000);
  });

  it("normalizes real engine Low/Mid/High and provenance terms without changing the numbers", () => {
    const result = normalizeImpact({
      Currency: "EUR",
      Low: 28000,
      Mid: 42000,
      High: 56000,
      Expected: 42000,
      IsAbstained: false,
      AssumptionVersion: 7,
      SupportStatus: "BoundedRange",
      Terms: [
        {
          Name: "Downgrade impact",
          InputsJson: "{\"affectedTons\":200,\"band\":[140,210,280]}",
          Low: 28000,
          Mid: 42000,
          High: 56000,
          Handle: { Handle: "prov:value:edge-crack:001" },
        },
      ],
    });

    expect(result.low).toBe(28000);
    expect(result.mid).toBe(42000);
    expect(result.high).toBe(56000);
    expect(result.terms[0].handle).toBe("prov:value:edge-crack:001");
  });

  it("renders ABSTAIN report without fabricated euro values", () => {
    const result = normalizeImpact({
      Currency: "EUR",
      Low: 0,
      Mid: 0,
      High: 0,
      IsAbstained: true,
      AbstainReason: "insufficient basis: downgradeDeltaPerTon missing",
      Terms: [],
    });

    const html = buildMonthlyValueReportHtml(result, 12000);

    expect(html).toContain("ABSTAIN");
    expect(html).toContain("insufficient basis");
    expect(html).not.toContain("€0");
    expect(html).not.toMatch(/guaranteed|will save/i);
  });

  it("computes payback multiples from engine output versus license cost", () => {
    const result = normalizeImpact({
      Currency: "EUR",
      Low: 28000,
      Mid: 42000,
      High: 56000,
      IsAbstained: false,
      Terms: [],
    });

    const payback = computePayback(result, 12000);

    expect(payback.lowMultiple).toBeCloseTo(2.333, 2);
    expect(payback.midMultiple).toBeCloseTo(3.5, 2);
    expect(payback.highMultiple).toBeCloseTo(4.666, 2);
  });

  it("formats the executive values as euro money", () => {
    expect(formatMoney(28000, "EUR")).toMatch(/28,000|28\s000/);
  });
});
