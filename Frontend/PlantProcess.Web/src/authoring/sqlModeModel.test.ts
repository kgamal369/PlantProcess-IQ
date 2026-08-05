// PPIQ T-036. The SQL mode model under test.
//
// The reconstructability rule is the one that can lose an author's work, so it
// is asserted in both directions: what it proves, and what it refuses to.

import { describe, expect, it } from "vitest";
import type { RunSqlResult, StagedDataset } from "@/api/canvasApi";
import {
  NO_SAMPLE, TYPE_NOT_REPORTED, completionPrefix, completionsFor,
  describeDiscardWarning, describeReturnedColumns, isReconstructable,
  normaliseSqlForComparison, reconstructVerdict,
} from "./sqlModeModel";

const compiled = 'SELECT t0."alpha_key"\nFROM "staging_one"."alpha" t0';

const catalogue: StagedDataset[] = [
  {
    table: "alpha", source: "staging_one",
    columns: [
      { name: "alpha_key", sqlType: "text", isKeyCandidate: true },
      { name: "widget_mass", sqlType: "numeric", isKeyCandidate: false },
    ],
  },
  {
    table: "beta", source: "staging_two",
    columns: [{ name: "widget_mass", sqlType: "numeric", isKeyCandidate: false }],
  },
];

describe("reconstructability, which fails closed", () => {
  it("proves the untouched case", () => {
    expect(reconstructVerdict(compiled, compiled)).toBe("reconstructable");
    expect(isReconstructable(compiled, compiled)).toBe(true);
  });

  it("forgives only line endings and outer whitespace", () => {
    const same = "\n  " + compiled.replace(/\n/g, "\r\n") + "  \n";
    expect(isReconstructable(same, compiled)).toBe(true);
    expect(normaliseSqlForComparison(same)).toBe(normaliseSqlForComparison(compiled));
  });

  it("refuses a reformat rather than claiming to understand it", () => {
    const reformatted = compiled.replace(/\n/g, " ");
    expect(reconstructVerdict(reformatted, compiled)).toBe("diverged");
  });

  it("refuses a case change, because it has no parser to say that is safe", () => {
    expect(reconstructVerdict(compiled.toUpperCase(), compiled)).toBe("diverged");
  });

  it("refuses an edit", () => {
    expect(reconstructVerdict(compiled + " WHERE 1=1", compiled)).toBe("diverged");
  });

  it("refuses when there is no recorded origin at all", () => {
    expect(reconstructVerdict(compiled, null)).toBe("no-origin");
    expect(reconstructVerdict(compiled, "   ")).toBe("no-origin");
    expect(isReconstructable(compiled, undefined)).toBe(false);
  });

  it("warns in words that name the reason and the consequence", () => {
    expect(describeDiscardWarning("reconstructable")).toBe("");
    const edited = describeDiscardWarning("diverged");
    expect(edited).toContain("edited since it was compiled");
    expect(edited).toContain("DISCARD the SQL");
    expect(edited).toContain("Cancel to stay in SQL mode");
    expect(describeDiscardWarning("no-origin")).toContain("nothing to reconstruct it from");
  });
});

describe("completions, drawn from the live catalogue", () => {
  it("reads the word under the caret", () => {
    expect(completionPrefix("select wid", 10)).toEqual({ qualifier: "", prefix: "wid" });
    expect(completionPrefix("select alpha.", 13)).toEqual({ qualifier: "alpha", prefix: "" });
    expect(completionPrefix("select alpha.wid", 16)).toEqual({ qualifier: "alpha", prefix: "wid" });
  });

  it("offers only that table's columns after a qualifier", () => {
    const got = completionsFor(catalogue, "select alpha.", 13);
    expect(got.map((c) => c.label)).toEqual(["alpha_key", "widget_mass"]);
    expect(got.every((c) => c.kind === "column")).toBe(true);
  });

  it("offers schemas, tables and columns unqualified, and says where each came from", () => {
    const got = completionsFor(catalogue, "select stag", 11);
    expect(got.map((c) => c.label)).toEqual(["staging_one", "staging_two"]);
    expect(got[0].kind).toBe("schema");

    const cols = completionsFor(catalogue, "select widget", 13);
    expect(cols.length).toBe(2);
    expect(cols.map((c) => c.detail)).toEqual(["alpha", "beta"]);
  });

  it("returns nothing rather than everything when nothing matches", () => {
    expect(completionsFor(catalogue, "select zzz", 10)).toEqual([]);
  });

  it("is bounded, so a wide catalogue cannot flood the editor", () => {
    expect(completionsFor(catalogue, "select ", 7, 2).length).toBe(2);
  });
});

describe("the Run Test column report", () => {
  function ran(over: Partial<RunSqlResult>): RunSqlResult {
    return {
      status: "succeeded", rowCount: 2, columns: ["alpha_key", "widget_mass"],
      rows: [[null, 1], ["A-1", 2]], message: "ok", errorCode: null,
      sql: compiled, appliedRowLimit: 100, ...over,
    };
  }

  it("pairs each column with the type the server measured", () => {
    const got = describeReturnedColumns(ran({
      columnDetails: [
        { name: "alpha_key", databaseType: "text" },
        { name: "widget_mass", databaseType: "numeric" },
      ],
    }));
    expect(got.map((c) => c.databaseType)).toEqual(["text", "numeric"]);
  });

  it("admits an absent type instead of inferring one from the sample", () => {
    const got = describeReturnedColumns(ran({ columnDetails: null }));
    expect(got[0].databaseType).toBe(TYPE_NOT_REPORTED);
    expect(got[1].databaseType).toBe(TYPE_NOT_REPORTED);
  });

  it("skips empty cells to find a representative sample", () => {
    const got = describeReturnedColumns(ran({}));
    expect(got[0].sample).toBe("A-1");
    expect(got[1].sample).toBe("1");
  });

  it("says so when there is nothing to sample", () => {
    const got = describeReturnedColumns(ran({ rows: [], rowCount: 0 }));
    expect(got[0].sample).toBe(NO_SAMPLE);
  });

  it("truncates a long value rather than filling the panel with it", () => {
    const long = "x".repeat(120);
    const got = describeReturnedColumns(ran({ rows: [[long, 1]] }));
    expect(got[0].sample.length).toBe(43);
    expect(got[0].sample.endsWith("...")).toBe(true);
  });
});