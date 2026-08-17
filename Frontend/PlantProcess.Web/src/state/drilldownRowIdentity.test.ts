import { describe, expect, it } from "vitest";
import {
  PPIQ_ROW_INDEX, populationForRow, sourceRowIndex, stampSourceRowIndices,
} from "./drilldownRowIdentity";

const BACKEND = [
  { code: "A", value: 10 },
  { code: "B", value: 20 },
  { code: "C", value: 30 },
];

describe("T-050 drilldown row identity", () => {
  it("stamps each row with its backend position without mutating the source", () => {
    const stamped = stampSourceRowIndices(BACKEND);

    expect(stamped.map((row) => row[PPIQ_ROW_INDEX])).toEqual([0, 1, 2]);
    expect(BACKEND[0]).not.toHaveProperty(PPIQ_ROW_INDEX);
  });

  it("survives a chart that REORDERS the rows", () => {
    // backend A B C, rendered C A B - the case the ruling named.
    const stamped = stampSourceRowIndices(BACKEND);
    const rendered = [stamped[2], stamped[0], stamped[1]];

    // Click the FIRST VISUAL point. Its backend row index must be 2, not 0.
    expect(sourceRowIndex(rendered[0])).toBe(2);
    expect(sourceRowIndex(rendered[1])).toBe(0);
    expect(sourceRowIndex(rendered[2])).toBe(1);
  });

  it("survives a chart that SORTS by value", () => {
    const stamped = stampSourceRowIndices(BACKEND);
    const sorted = [...stamped].sort((a, b) => Number(b.value) - Number(a.value));

    expect(sorted.map((row) => row.code)).toEqual(["C", "B", "A"]);
    expect(sorted.map((row) => sourceRowIndex(row))).toEqual([2, 1, 0]);
  });

  it("survives a chart that SLICES, as MiniTable does at fifty rows", () => {
    const many = Array.from({ length: 60 }, (_, i) => ({ code: "R" + i }));
    const sliced = stampSourceRowIndices(many).slice(50);

    // The first VISUAL row of the slice is backend row 50.
    expect(sourceRowIndex(sliced[0])).toBe(50);
    expect(sourceRowIndex(sliced[sliced.length - 1])).toBe(59);
  });

  it("survives a chart that PROJECTS a subset of fields", () => {
    const stamped = stampSourceRowIndices(BACKEND);
    const projected = stamped.map((row) => ({ ...row, value: undefined }));

    expect(sourceRowIndex(projected[1])).toBe(1);
  });

  it("returns null for an unstamped or malformed datum rather than guessing", () => {
    expect(sourceRowIndex({ code: "A" })).toBeNull();
    expect(sourceRowIndex(null)).toBeNull();
    expect(sourceRowIndex(undefined)).toBeNull();
    expect(sourceRowIndex("A")).toBeNull();
    expect(sourceRowIndex({ [PPIQ_ROW_INDEX]: -1 })).toBeNull();
    expect(sourceRowIndex({ [PPIQ_ROW_INDEX]: 1.5 })).toBeNull();
    expect(sourceRowIndex({ [PPIQ_ROW_INDEX]: "2" })).toBeNull();
  });

  it("matches a population by its rowIndex, never by array position", () => {
    // Descriptors deliberately out of order: matching by position would pick
    // the wrong population and look right.
    const populations = [
      { rowIndex: 2, measureCode: "m" },
      { rowIndex: 0, measureCode: "m" },
      { rowIndex: 1, measureCode: "m" },
    ];

    expect(populationForRow(populations, 0)).toEqual({ rowIndex: 0, measureCode: "m" });
    expect(populationForRow(populations, 2)).toEqual({ rowIndex: 2, measureCode: "m" });
  });

  it("yields no population rather than a wrong one when identity is missing", () => {
    const populations = [{ rowIndex: 0 }];

    expect(populationForRow(populations, null)).toBeNull();
    expect(populationForRow(populations, 7)).toBeNull();
    expect(populationForRow(null, 0)).toBeNull();
    expect(populationForRow(undefined, 0)).toBeNull();
  });
});
