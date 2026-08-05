// PPIQ T-034. The schema tree model under test.
//
// No table or column name here belongs to the emulated plant; the fixtures are
// invented so the test proves the FUNCTION and not one customer's schema.

import { describe, expect, it } from "vitest";
import type { StagedDataset } from "@/api/canvasApi";
import {
  SCHEMA_DRAG_MIME, datasetForDrop, decodeSchemaDrag, describeColumnType,
  describeRowCount, describeSelection, encodeSchemaDrag, isSelected,
  searchCatalogue, selectedColumnsOf, selectionCount, toggleColumn,
  type ColumnSelection,
} from "./schemaTreeModel";

const first: StagedDataset = {
  table: "alpha", source: "staging_one", approxRowCount: 1234,
  columns: [
    { name: "alpha_key", sqlType: "text", isKeyCandidate: true, isNullable: false },
    { name: "widget_mass", sqlType: "numeric", isKeyCandidate: false, isNullable: true },
  ],
};
const second: StagedDataset = {
  table: "beta", source: "staging_one", approxRowCount: null,
  columns: [
    { name: "alpha_key", sqlType: "text", isKeyCandidate: true, isNullable: false },
    { name: "widget_width", sqlType: "numeric", isKeyCandidate: false, isNullable: true },
  ],
};
const third: StagedDataset = {
  table: "gamma", source: "staging_two",
  columns: [{ name: "note", sqlType: "text", isKeyCandidate: false }],
};
const catalogue = [first, second, third];

describe("wording for facts the catalogue may not have", () => {
  it("states nullability beside the type when the server sent it", () => {
    expect(describeColumnType(first.columns[0])).toBe("text, not null");
    expect(describeColumnType(first.columns[1])).toBe("numeric, nullable");
  });

  it("shows the type alone rather than inventing not null", () => {
    expect(describeColumnType(third.columns[0])).toBe("text");
  });

  it("says a table was never analysed instead of calling it empty", () => {
    expect(describeRowCount(null)).toBe("row count not analysed");
    expect(describeRowCount(undefined)).toBe("");
    expect(describeRowCount(0)).toBe("~0 rows");
    expect(describeRowCount(1234)).toBe("~1,234 rows");
  });
});

describe("search", () => {
  it("narrows nothing and forces nothing open without a query", () => {
    const r = searchCatalogue(catalogue, "   ");
    expect(r.active).toBe(false);
    expect(r.tables.length).toBe(3);
    expect(r.openTables).toEqual([]);
  });

  it("keeps a whole schema when the schema name matches", () => {
    const r = searchCatalogue(catalogue, "staging_two");
    expect(r.tables.map((t) => t.table)).toEqual(["gamma"]);
    expect(r.tables[0].columns.length).toBe(1);
  });

  it("keeps every column of a table whose NAME matched", () => {
    const r = searchCatalogue(catalogue, "alph");
    const alpha = r.tables.filter((t) => t.table === "alpha")[0];
    expect(alpha.columns.length).toBe(2);
  });

  it("narrows to the matching columns and opens ONLY the tables that hold them", () => {
    const r = searchCatalogue(catalogue, "width");
    expect(r.tables.map((t) => t.table)).toEqual(["beta"]);
    expect(r.tables[0].columns.map((c) => c.name)).toEqual(["widget_width"]);
    expect(r.openTables).toEqual(["beta"]);
  });

  it("does not force open a table that matched only by its own name", () => {
    const r = searchCatalogue(catalogue, "gamma");
    expect(r.tables.map((t) => t.table)).toEqual(["gamma"]);
    expect(r.openTables).toEqual([]);
  });

  it("is case insensitive and reports the schemas to unfold", () => {
    const r = searchCatalogue(catalogue, "WIDGET");
    expect(r.tables.map((t) => t.table)).toEqual(["alpha", "beta"]);
    expect(r.openSchemas).toEqual(["staging_one"]);
  });

  it("returns nothing at all rather than everything when nothing matches", () => {
    const r = searchCatalogue(catalogue, "no_such_thing");
    expect(r.tables).toEqual([]);
    expect(r.active).toBe(true);
  });
});

describe("multi-select", () => {
  it("adds, reports and removes without mutating what it was given", () => {
    const empty: ColumnSelection = {};
    const one = toggleColumn(empty, "alpha", "alpha_key");
    const two = toggleColumn(one, "alpha", "widget_mass");
    expect(empty).toEqual({});
    expect(selectionCount(one)).toBe(1);
    expect(selectionCount(two)).toBe(2);
    expect(isSelected(two, "alpha", "widget_mass")).toBe(true);
    expect(selectedColumnsOf(two, "alpha")).toEqual(["alpha_key", "widget_mass"]);

    const back = toggleColumn(two, "alpha", "alpha_key");
    expect(selectedColumnsOf(back, "alpha")).toEqual(["widget_mass"]);
  });

  it("drops a table from the selection rather than leaving it empty", () => {
    const one = toggleColumn({}, "alpha", "alpha_key");
    const none = toggleColumn(one, "alpha", "alpha_key");
    expect(none).toEqual({});
  });

  it("keeps three columns of one table in the order they were chosen", () => {
    let s: ColumnSelection = {};
    s = toggleColumn(s, "beta", "widget_width");
    s = toggleColumn(s, "beta", "alpha_key");
    s = toggleColumn(s, "alpha", "widget_mass");
    expect(selectionCount(s)).toBe(3);
    expect(selectedColumnsOf(s, "beta")).toEqual(["widget_width", "alpha_key"]);
  });

  it("says plainly that a drag carries one table when the selection spans two", () => {
    let s: ColumnSelection = {};
    s = toggleColumn(s, "alpha", "alpha_key");
    s = toggleColumn(s, "beta", "widget_width");
    expect(describeSelection(s)).toContain("across 2 tables");
    expect(describeSelection(s)).toContain("one table at a time");
  });

  it("names the single table and gets the singular right", () => {
    const s = toggleColumn({}, "alpha", "alpha_key");
    expect(describeSelection(s)).toBe("1 column selected in alpha");
    expect(describeSelection({})).toBe("");
  });
});

describe("drag payload", () => {
  it("round-trips a whole table and a set of attributes", () => {
    const table = encodeSchemaDrag({ kind: "table", table: "alpha" });
    expect(decodeSchemaDrag(table)).toEqual({ kind: "table", table: "alpha" });

    const columns = encodeSchemaDrag({ kind: "columns", table: "beta", columns: ["widget_width"] });
    expect(decodeSchemaDrag(columns)).toEqual({ kind: "columns", table: "beta", columns: ["widget_width"] });
  });

  it("fails closed on anything it did not write", () => {
    expect(decodeSchemaDrag(null)).toBeNull();
    expect(decodeSchemaDrag("")).toBeNull();
    expect(decodeSchemaDrag("not json at all")).toBeNull();
    expect(decodeSchemaDrag("[]")).toBeNull();
    expect(decodeSchemaDrag(JSON.stringify({ kind: "table" }))).toBeNull();
    expect(decodeSchemaDrag(JSON.stringify({ kind: "elsewhere", table: "alpha" }))).toBeNull();
    expect(decodeSchemaDrag(JSON.stringify({ kind: "columns", table: "alpha" }))).toBeNull();
    expect(decodeSchemaDrag(JSON.stringify({ kind: "columns", table: "alpha", columns: [] }))).toBeNull();
    expect(decodeSchemaDrag(JSON.stringify({ kind: "columns", table: "alpha", columns: [7] }))).toBeNull();
  });

  it("resolves a drop against the catalogue and never invents the dataset", () => {
    expect(datasetForDrop(catalogue, { kind: "table", table: "beta" })).toBe(second);
    expect(datasetForDrop(catalogue, { kind: "table", table: "vanished" })).toBeNull();
  });

  it("carries one MIME type both sides agree on", () => {
    expect(SCHEMA_DRAG_MIME).toBe("application/x-ppiq-schema");
  });
});