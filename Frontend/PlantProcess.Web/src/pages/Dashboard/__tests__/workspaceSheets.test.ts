import { describe, expect, it } from "vitest";
import {
  DEFAULT_SHEET_ID,
  DEFAULT_SHEET_NAME,
  buildSheetDocument,
  nextSheet,
  readSheets,
  readWidgetSheetIds,
  sheetIdForWidget,
} from "../workspaceSheets";

// T-043 slice 3 proofs of the sheet document. Option A puts sheets inside
// layout_json, so these are the contract between what is written and what is
// read back, and they run without a database because there is not one.

const ONE = [{ id: DEFAULT_SHEET_ID, name: DEFAULT_SHEET_NAME }];
const TWO = [
  { id: DEFAULT_SHEET_ID, name: DEFAULT_SHEET_NAME },
  { id: "quality", name: "Quality" },
];

describe("T-043 reading the sheet document", () => {
  it("describes a document with no sheets as the one sheet it actually has", () => {
    expect(readSheets(null)).toEqual(ONE);
    expect(readSheets("{}")).toEqual(ONE);
    expect(readSheets("not json")).toEqual(ONE);
    expect(readSheets('{"sheets":[]}')).toEqual(ONE);
    expect(readSheets('{"lg":[],"md":[]}')).toEqual(ONE);
  });

  it("reads sheets that are actually persisted and ignores unreadable entries", () => {
    expect(readSheets('{"sheets":[{"id":"a","name":"A"},{"id":"b","name":"B"}]}')).toEqual([
      { id: "a", name: "A" },
      { id: "b", name: "B" },
    ]);
    expect(readSheets('{"sheets":[{"id":"a"},{"name":"B"},7]}')).toEqual(ONE);
  });

  it("reads widget assignments and drops anything that is not a pair of strings", () => {
    expect(readWidgetSheetIds('{"widgetSheets":{"w1":"quality","w2":"default"}}')).toEqual({
      w1: "quality",
      w2: "default",
    });
    expect(readWidgetSheetIds('{"widgetSheets":{"w1":7,"w2":"","w3":null}}')).toEqual({});
    expect(readWidgetSheetIds("{}")).toEqual({});
    expect(readWidgetSheetIds(null)).toEqual({});
  });
});

describe("T-043 which sheet a widget belongs to", () => {
  it("puts an unassigned widget on the first sheet", () => {
    expect(sheetIdForWidget({}, TWO, "w1")).toBe(DEFAULT_SHEET_ID);
  });

  it("honours an assignment to a sheet that exists", () => {
    expect(sheetIdForWidget({ w1: "quality" }, TWO, "w1")).toBe("quality");
  });

  it("rescues a widget assigned to a sheet that no longer exists", () => {
    expect(sheetIdForWidget({ w1: "deleted" }, TWO, "w1")).toBe(DEFAULT_SHEET_ID);
  });

  it("never returns nothing, even for an empty sheet list", () => {
    expect(sheetIdForWidget({}, [], "w1")).toBe(DEFAULT_SHEET_ID);
  });
});

describe("T-043 writing the sheet document", () => {
  it("writes exactly the two keys and survives a round trip", () => {
    const document = buildSheetDocument(TWO, { w1: "quality" });

    expect(Object.keys(document).sort()).toEqual(["sheets", "widgetSheets"]);

    const layoutJson = JSON.stringify({ lg: [], ...document });
    expect(readSheets(layoutJson)).toEqual(TWO);
    expect(readWidgetSheetIds(layoutJson)).toEqual({ w1: "quality" });
  });

  it("writes the default sheet rather than an empty list", () => {
    expect(buildSheetDocument([], {})).toEqual({ sheets: ONE, widgetSheets: {} });
  });
});

describe("T-043 creating a sheet", () => {
  it("derives a stable id rather than a random one", () => {
    expect(nextSheet(ONE)).toEqual({ id: "sheet-2", name: "Sheet 2" });
    expect(nextSheet(ONE)).toEqual(nextSheet(ONE));
  });

  it("never collides with an id already in the document", () => {
    const taken = [...ONE, { id: "sheet-2", name: "Renamed" }];
    expect(nextSheet(taken).id).toBe("sheet-3");
  });
});