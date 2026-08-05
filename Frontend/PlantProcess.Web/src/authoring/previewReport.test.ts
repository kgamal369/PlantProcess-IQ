// PPIQ T-035. The debug-log contract under test.
//
// The three cases the task text names, asserted as facts about a function
// rather than clicked three times in a browser.

import { describe, expect, it } from "vitest";
import type { DryRunResult } from "@/api/canvasApi";
import {
  describeEstimate, describeMeasured, describePreview, describeThrownAction,
  describeThrownPreview,
} from "./previewReport";

function result(over: Partial<DryRunResult>): DryRunResult {
  return {
    dryRunId: "d1", status: "succeeded", rowCount: 3,
    columns: ["one", "two"], rows: [], ...over,
  };
}

describe("a valid preview", () => {
  it("is a Success carrying the row count, the columns and the estimate", () => {
    const r = describePreview(result({ plannerCost: 12.6, estimatedRows: 1200 }), 42);
    expect(r.severity).toBe("success");
    expect(r.message).toBe("Preview ran.");
    expect(r.facts).toContain("3 sample rows");
    expect(r.facts).toContain("2 columns: one, two");
    expect(r.facts).toContain("elapsed 42 ms");
    expect(r.facts).toContain("planner cost estimate 13");
    expect(r.facts).toContain("about 1,200 rows");
  });

  it("says the preview stopped at its limit instead of calling the cap a total", () => {
    const r = describePreview(result({ rowCount: 50, previewTruncated: true }), 10);
    expect(r.facts).toContain("stopped at the preview limit");
  });

  it("states only the numbers it was given", () => {
    expect(describeEstimate(undefined, undefined)).toBe("");
    expect(describeEstimate(null, null)).toBe("");
    expect(describeEstimate(9.2, undefined)).toBe("planner cost estimate 9");
    const r = describePreview(result({}), 5);
    expect(r.facts).not.toContain("planner");
  });

  it("calls the estimate an estimate, never a runtime and never a cost in money", () => {
    const facts = describeMeasured(result({ plannerCost: 100, estimatedRows: 7 }), 1);
    expect(facts).toContain("planner cost estimate");
    expect(facts).toContain("planner estimates about");
  });
});

describe("a preview that returns nothing", () => {
  it("is a Warning, in the agreed wording", () => {
    const r = describePreview(result({ rowCount: 0 }), 12);
    expect(r.severity).toBe("warning");
    expect(r.message).toContain("Preview completed successfully but returned 0 rows.");
    expect(r.message).toContain("Review the active filters");
    expect(r.message).toContain("contains matching rows");
  });

  it("does not claim a cause the server cannot see", () => {
    const r = describePreview(result({ rowCount: 0 }), 12);
    expect(r.message).not.toContain("too restrictive");
    expect(r.message).not.toContain("filter is wrong");
  });
});

describe("a rejected operator", () => {
  it("is an Error and the operator survives into the entry", () => {
    const r = describePreview(result({
      status: "rejected_by_safe_sql", rowCount: 0,
      message: "operator 'REGEXP' is not permitted in a filter",
    }), 3);
    expect(r.severity).toBe("error");
    expect(r.message).toContain("REGEXP");
    expect(r.message).toContain("not permitted in a filter");
  });

  it("says so plainly when a refusal arrives with no reason", () => {
    const r = describePreview(result({ status: "failed", rowCount: 0, message: "   " }), 3);
    expect(r.severity).toBe("error");
    expect(r.message).toContain("no reason came back with it");
  });
});

describe("nothing an engineer cannot act on", () => {
  it("reads nothing at all from a thrown value", () => {
    const thrown = new Error("TypeError: Failed to fetch at http://localhost:5063/api");
    const said = describeThrownPreview(thrown);
    expect(said).not.toContain("TypeError");
    expect(said).not.toContain("http");
    expect(said).toContain("Check that the API is running");
  });

  it("gives every other action the same guarantee", () => {
    const said = describeThrownAction(new Error("SyntaxError: Unexpected token < in JSON"));
    expect(said).not.toContain("SyntaxError");
    expect(said).not.toContain("JSON");
    expect(said).toContain("Check that the API is running");
  });

  it("uses none of the phrases the PPIQ-T09 contract forbids", () => {
    const forbidden = ["could not " + "load", "failed to " + "load", "unable to " + "load"];
    const said = [
      describeThrownPreview(new Error("x")),
      describePreview(result({ rowCount: 0 }), 1).message,
      describePreview(result({ status: "failed", rowCount: 0, message: "" }), 1).message,
      describePreview(result({}), 1).message,
    ].join(" ").toLowerCase();
    for (const phrase of forbidden) {
      expect(said).not.toContain(phrase);
    }
  });
});
