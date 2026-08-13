// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

import {
  resolveChartSwitcherOptions,
  type DashboardChartAvailability,
  type DashboardChartTypeMetadata,
  type DashboardCompatibilityRule,
} from "../../../api/product-core/dashboard-widget-types";

// T-046. THE SWITCHER CONSUMES BACKEND TRUTH.
//
// Every case below is a claim about the PROJECTION, not about a chart code.
// The catalogue here is invented on purpose: if the projection held any
// knowledge of real chart codes these fixtures would not exercise it.

function chartType(
  code: string,
  availability: DashboardChartAvailability,
  label?: string
): DashboardChartTypeMetadata {
  return {
    code,
    label: label ?? code.toUpperCase(),
    category: "Test",
    supportsDimension: true,
    supportsMeasure: true,
    supportsMultipleSeries: false,
    supportsParameterSelection: false,
    availability,
    description: undefined,
  };
}

const CATALOGUE: DashboardChartTypeMetadata[] = [
  chartType("alpha", "implemented", "Alpha"),
  chartType("beta", "implemented", "Beta"),
  chartType("gamma", "not-yet-available", "Gamma"),
  chartType("delta", "implemented", "Delta"),
];

const RULE: DashboardCompatibilityRule = {
  dimensionCode: "dimOne",
  measureCode: "measOne",
  allowedChartTypes: ["alpha", "gamma"],
  refusedChartTypes: [
    { chartTypeCode: "beta", reason: "Beta needs two numeric axes and this binding has one." },
  ],
  requiresParameterCode: false,
  warningMessage: null,
};

describe("T-046 the switcher reads the server and decides nothing", () => {
  it("marks an allowed type with an implemented renderer available", () => {
    const options = resolveChartSwitcherOptions(CATALOGUE, RULE, "alpha");
    const alpha = options.find((o) => o.code === "alpha");

    expect(alpha?.state).toBe("available");
    expect(alpha?.reason).toBeNull();
  });

  it("keeps unavailable renderer and incompatible binding as DISTINCT states", () => {
    const options = resolveChartSwitcherOptions(CATALOGUE, RULE, "alpha");

    expect(options.find((o) => o.code === "gamma")?.state).toBe("unavailable");
    expect(options.find((o) => o.code === "beta")?.state).toBe("incompatible");
  });

  it("carries the server refusal sentence verbatim", () => {
    const options = resolveChartSwitcherOptions(CATALOGUE, RULE, "alpha");

    expect(options.find((o) => o.code === "beta")?.reason).toBe(
      RULE.refusedChartTypes[0].reason
    );
  });

  it("never reports a missing renderer as a binding problem", () => {
    // gamma is ALLOWED by the rule and simply not drawable. Calling that
    // incompatible would send an author to change a dimension that is correct.
    const gamma = resolveChartSwitcherOptions(CATALOGUE, RULE, "alpha").find(
      (o) => o.code === "gamma"
    );

    expect(gamma?.state).toBe("unavailable");
    expect(gamma?.reason).toBeNull();
  });

  it("gives availability precedence when a type is both unbuilt and refused", () => {
    const rule: DashboardCompatibilityRule = {
      ...RULE,
      allowedChartTypes: ["alpha"],
      refusedChartTypes: [{ chartTypeCode: "gamma", reason: "structural refusal" }],
    };

    expect(
      resolveChartSwitcherOptions(CATALOGUE, rule, "alpha").find((o) => o.code === "gamma")?.state
    ).toBe("unavailable");
  });

  it("omits a type the server named in neither list", () => {
    const options = resolveChartSwitcherOptions(CATALOGUE, RULE, "alpha");

    expect(options.some((o) => o.code === "delta")).toBe(false);
  });

  it("always lists the type the widget is currently drawn as", () => {
    // A widget must never render as something its own switcher denies exists.
    const options = resolveChartSwitcherOptions(CATALOGUE, RULE, "delta");
    const delta = options.find((o) => o.code === "delta");

    expect(delta).toBeDefined();
    expect(delta?.state).toBe("incompatible");
  });

  it("takes labels from the catalogue, never from a local map", () => {
    expect(
      resolveChartSwitcherOptions(CATALOGUE, RULE, "alpha").find((o) => o.code === "alpha")?.label
    ).toBe("Alpha");
  });

  it("FAILS CLOSED with no metadata", () => {
    expect(resolveChartSwitcherOptions(null, RULE, "alpha")).toEqual([]);
    expect(resolveChartSwitcherOptions([], RULE, "alpha")).toEqual([]);
  });

  it("FAILS CLOSED when no rule covers this binding", () => {
    expect(resolveChartSwitcherOptions(CATALOGUE, null, "alpha")).toEqual([]);
  });

  it("survives a server that omits the refusal array", () => {
    const rule = {
      ...RULE,
      refusedChartTypes: undefined,
    } as unknown as DashboardCompatibilityRule;

    const options = resolveChartSwitcherOptions(CATALOGUE, rule, "alpha");
    expect(options.find((o) => o.code === "alpha")?.state).toBe("available");
  });
});

// The React surfaces are proved by a comment-stripped source guard rather than
// by mounting. Mounting the card drags in the grid-layout and selection
// contexts, and a passing mount would prove those harnesses work, not that the
// decision tree is gone. This is stated rather than hidden.
function strip(relativePath: string) {
  return readFileSync(resolve(__dirname, relativePath), "utf8")
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/^\s*\/\/.*$/gm, "");
}

describe("T-046 no chart-code decision tree survives in React", () => {
  const card = strip("../DashboardWidgetCard.tsx");
  const saved = strip("../SavedDashboardWidget.tsx");
  const extras = strip("../ChartExtras.tsx");

  it("the card holds no hardcoded chart-code list or label map", () => {
    expect(card).not.toContain("CHART_TYPE_LABELS");
    expect(card).not.toMatch(/chartTypes\s*=\s*\[/);
  });

  it("the card renders from resolved options", () => {
    expect(card).toContain("chartOptions.map");
    expect(card).toContain("option.state");
  });

  it("the card distinguishes the two refusal states in the DOM", () => {
    expect(card).toContain("data-chart-state");
  });

  it("the retired switcher authority is gone and not re-imported", () => {
    expect(extras).not.toContain("export function extendChartTypes");
    expect(extras).not.toContain("SCATTER_MEASURES");
    expect(saved).not.toContain("extendChartTypes");
  });

  it("the renderer helper is untouched", () => {
    expect(extras).toContain("export const isExtraChartType");
    expect(saved).toContain("isExtraChartType(activeChartType)");
  });

  it("the widget resolves options through the single projection", () => {
    expect(saved).toContain("resolveChartSwitcherOptions");
  });
});