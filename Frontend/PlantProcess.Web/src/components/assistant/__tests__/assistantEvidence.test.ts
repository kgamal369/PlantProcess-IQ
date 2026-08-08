// @vitest-environment node
import { describe, expect, it } from "vitest";

import type { AssistantWidgetResultEvidence } from "@/api/assistantApi";
import {
  NO_STARTER_PROMPT,
  chipLabel,
  citationKey,
  openInPageHref,
  starterQuestions,
  stripFields,
} from "../assistantEvidence";

/* PPIQ-T075. The pure evidence logic.
 *
 * Every code and label here is invented for the test. If a real page name, a
 * real widget code or an industry word were needed to make these pass, the
 * implementation would be wrong. */

function evidence(overrides: Partial<AssistantWidgetResultEvidence> = {}): AssistantWidgetResultEvidence {
  return {
    evidenceId: "11111111-1111-1111-1111-111111111111",
    available: true,
    pageCode: "PAGE_ALPHA",
    widgetCode: "WIDGET_ALPHA",
    chartType: "bar",
    dimensionCode: "DIM_ALPHA",
    measureCode: "MEASURE_ALPHA",
    filterContext: "{}",
    generatedAtUtc: "2026-08-08T20:00:00Z",
    columns: ["DIM_ALPHA", "dimensionLabel", "value", "observationCount"],
    rows: [["KEY_ONE", "LABEL_ONE", "12.5", "900"]],
    hasObservationCount: true,
    observationCountTotal: 900,
    sentence: "On page PAGE_ALPHA, widget WIDGET_ALPHA shows MEASURE_ALPHA by DIM_ALPHA.",
    ...overrides,
  };
}

describe("citation chips", () => {
  it("labels a chip from the evidence handle, never from prose", () => {
    expect(chipLabel({ kind: "WidgetResult", id: "abcdef1234567890" })).toBe("WidgetResult \u00b7 abcdef12");
  });

  it("keys a chip by kind and full id so two kinds of the same id do not collide", () => {
    expect(citationKey({ kind: "WidgetResult", id: "x" })).not.toBe(citationKey({ kind: "Dataset", id: "x" }));
  });
});

describe("the evidence strip", () => {
  it("renders the real fields the evidence supplies", () => {
    const labels = stripFields(evidence()).map((f) => f.label);

    expect(labels).toContain("Page");
    expect(labels).toContain("Widget");
    expect(labels).toContain("Measure");
    expect(labels).toContain("Dimension");
    expect(labels).toContain("As of");
  });

  it("never relabels observationCount as population", () => {
    const fields = stripFields(evidence());
    const observation = fields.find((f) => f.label.includes("observationCount"));

    expect(observation?.value).toBe("900");
    expect(fields.some((f) => f.label.toLowerCase().includes("population"))).toBe(false);
  });

  it("omits fields the evidence does not supply", () => {
    const labels = stripFields(
      evidence({ measureCode: null, dimensionCode: null, chartType: null, hasObservationCount: false }),
    ).map((f) => f.label);

    expect(labels).not.toContain("Measure");
    expect(labels).not.toContain("Dimension");
    expect(labels.some((l) => l.includes("observationCount"))).toBe(false);
  });

  it("shows a filter context only when one genuinely exists", () => {
    expect(stripFields(evidence()).some((f) => f.label === "Filter context")).toBe(false);

    const withFilters = stripFields(evidence({ filterContext: '{"siteId":"SITE_ALPHA"}' }));
    expect(withFilters.some((f) => f.label === "Filter context")).toBe(true);
  });
});

describe("open in page", () => {
  it("uses the canonical workspace route and the evidence page identity", () => {
    expect(openInPageHref(evidence())).toBe("/workspace/PAGE_ALPHA?focusWidget=WIDGET_ALPHA");
  });

  it("navigates to the page alone when the evidence names no widget", () => {
    expect(openInPageHref(evidence({ widgetCode: "" }))).toBe("/workspace/PAGE_ALPHA");
  });

  it("offers no link at all when the evidence names no page", () => {
    /* A link that cannot be honoured should not be shown. */
    expect(openInPageHref(evidence({ pageCode: "" }))).toBeNull();
  });

  it("encodes identities rather than trusting them in a URL", () => {
    expect(openInPageHref(evidence({ pageCode: "PAGE ALPHA", widgetCode: "W/1" })))
      .toBe("/workspace/PAGE%20ALPHA?focusWidget=W%2F1");
  });
});

describe("suggested questions", () => {
  it("derives three starters from a focused widget", () => {
    const starters = starterQuestions({ pageCode: "PAGE_ALPHA", widgetCode: "WIDGET_ALPHA" });

    expect(starters).toHaveLength(3);
    expect(starters.every((s) => s.includes("WIDGET_ALPHA") || s.includes("PAGE_ALPHA"))).toBe(true);
  });

  it("uses a live selection when one exists", () => {
    const starters = starterQuestions({
      pageCode: "PAGE_ALPHA",
      widgetCode: "WIDGET_ALPHA",
      selections: ["siteId=SITE_ALPHA"],
    });

    expect(starters.some((s) => s.includes("siteId=SITE_ALPHA"))).toBe(true);
  });

  it("two different contexts produce different starters", () => {
    const first = starterQuestions({ pageCode: "PAGE_ALPHA", widgetCode: "WIDGET_ALPHA" });
    const second = starterQuestions({ pageCode: "PAGE_BETA", widgetCode: "WIDGET_BETA" });

    expect(first).not.toEqual(second);
  });

  it("falls back to fewer page-level starters rather than to a demo list", () => {
    const starters = starterQuestions({ pageCode: "PAGE_ALPHA" });

    expect(starters).toHaveLength(2);
    expect(starters.every((s) => s.includes("PAGE_ALPHA"))).toBe(true);
  });

  it("offers nothing at all when there is no real context", () => {
    /* The retired global list had three questions that were true nowhere. */
    expect(starterQuestions({})).toEqual([]);
    expect(NO_STARTER_PROMPT.length).toBeGreaterThan(0);
  });
});