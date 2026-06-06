import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { AdvancedResultPanel, isRenderable, type AdvancedResult } from "../AdvancedResultPanel";

const renderable: AdvancedResult = {
  findingId: "param.cast_speed",
  method: "Spearman",
  effectSize: 0.42,
  qValue: 0.01,
  sampleSize: 128,
  stabilityLower: 0.31,
  stabilityUpper: 0.55,
  stabilityConsistency: 0.92,
  isStable: true,
  survivesStratification: true,
  honestyCaveat: "This is a diagnostic association, not a evidence-backed root-cause investigation.",
};

describe("AdvancedResultPanel", () => {
  it("renders a valid finding with method, sample size, and the mandatory caveat", () => {
    const html = renderToString(<AdvancedResultPanel result={renderable} />);
    expect(html).toContain("Spearman");
    expect(html).toContain("128");
    expect(html).toContain("not a evidence-backed root-cause investigation");
    expect(html).not.toContain("Result cannot be displayed");
  });

  it("withholds a result with no applicable method (guard)", () => {
    const blocked: AdvancedResult = { ...renderable, method: "NotApplicable" };
    expect(isRenderable(blocked)).toBe(false);
    const html = renderToString(<AdvancedResultPanel result={blocked} />);
    expect(html).toContain("Result cannot be displayed");
    expect(html).not.toContain("0.420");
  });

  it("withholds a result with zero sample size (guard)", () => {
    const blocked: AdvancedResult = { ...renderable, sampleSize: 0 };
    expect(isRenderable(blocked)).toBe(false);
    const html = renderToString(<AdvancedResultPanel result={blocked} />);
    expect(html).toContain("Result cannot be displayed");
  });
});