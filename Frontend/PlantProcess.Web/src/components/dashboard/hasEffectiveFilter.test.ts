import { describe, expect, it } from "vitest";
import { hasEffectiveFilter } from "./hasEffectiveFilter";

describe("T-051 hasEffectiveFilter", () => {
  it("treats an absent or empty filter set as not narrowing", () => {
    expect(hasEffectiveFilter(null)).toBe(false);
    expect(hasEffectiveFilter(undefined)).toBe(false);
    expect(hasEffectiveFilter({})).toBe(false);
  });

  it("ignores the present-but-empty slots the dashboard always sends", () => {
    // Ten global slots, none of them narrowing anything. Counting keys here
    // would turn a genuine empty result into filtered-empty.
    expect(hasEffectiveFilter({
      equipment: null, material: undefined, grade: "", shift: "   ",
      from: null, to: null, defect: [], line: {}, batch: [null, ""], site: { code: "" },
    })).toBe(false);
  });

  it("counts a value that genuinely narrows the query", () => {
    expect(hasEffectiveFilter({ equipment: null, grade: "S355" })).toBe(true);
    expect(hasEffectiveFilter({ defect: ["scale"] })).toBe(true);
    expect(hasEffectiveFilter({ line: { code: "L2" } })).toBe(true);
    expect(hasEffectiveFilter({ shift: 2 })).toBe(true);
    expect(hasEffectiveFilter({ active: false })).toBe(true);
  });
});
