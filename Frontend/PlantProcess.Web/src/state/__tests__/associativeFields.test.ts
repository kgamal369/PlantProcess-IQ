import { describe, expect, it } from "vitest";
import { buildAssociativeFields } from "../associativeFields";
import { FILTERABLE_DIMENSIONS, isTemporalDimension } from "../widgetSelectionMap";

// Real filterable, non-temporal dimension codes, taken from the product's own
// map rather than typed here. A literal would make this test assert which
// dimensions exist, which is precisely the coupling T-048 removes.
const FILTERABLE = FILTERABLE_DIMENSIONS.filter((code) => !isTemporalDimension(code));

// T-048. The field set is a projection over the published dimension
// catalogue. These cases prove that adding a dimension to the registry adds a
// field with NO code change here - which is the whole point of the task.

describe("T-048 the associative field set reads the registry", () => {
  it("derives a field for every filterable, non-temporal dimension", () => {
    expect(FILTERABLE.length).toBeGreaterThan(1);

    const fields = buildAssociativeFields(FILTERABLE.map((code) => ({ code, label: code })));

    expect(fields.map((f) => f.dimension)).toEqual(FILTERABLE);
  });

  it("maps each dimension to its filter key through the existing authority", () => {
    // The key comes from dimensionToFilterField, never from this file, so it
    // may legitimately differ from the dimension code.
    const fields = buildAssociativeFields(FILTERABLE.map((code) => ({ code, label: code })));

    for (const field of fields) {
      expect(field.key).toBeTruthy();
      expect(FILTERABLE).toContain(field.dimension);
    }
  });

  it("adds a NEW registry dimension with no change to this file", () => {
    // T-048's own validation, expressed as a test: publish one more dimension
    // and one more field appears. The added code is taken from the product's
    // filter map, not invented - an unfilterable code would be dropped, and
    // dropping it would be correct rather than a bug.
    const [first, second] = FILTERABLE;

    const before = buildAssociativeFields([{ code: first, label: first }]);
    const after = buildAssociativeFields([
      { code: first, label: first },
      { code: second, label: second },
    ]);

    expect(before.length).toBe(1);
    expect(after.length).toBe(before.length + 1);
    expect(after.some((f) => f.dimension === second)).toBe(true);
  });

  it("drops a dimension the filter contract cannot express", () => {
    // gradeOrRecipe is a real backend dimension with no field in
    // DashboardFilters, so it cannot become a selectable chip. Dropping it is
    // the honest outcome: a chip that cannot filter anything is a dead control.
    const fields = buildAssociativeFields([
      { code: FILTERABLE[0], label: "kept" },
      { code: "gradeOrRecipe", label: "unfilterable" },
    ]);

    expect(fields.map((f) => f.dimension)).toEqual([FILTERABLE[0]]);
  });

  it("omits temporal dimensions, which are ranges rather than chip sets", () => {
    const fields = buildAssociativeFields([
      { code: "day", label: "Day" },
      { code: "week", label: "Week" },
      { code: "month", label: "Month" },
      { code: "site", label: "Site" },
    ]);

    expect(fields.map((f) => f.dimension)).toEqual(["site"]);
  });

  it("omits a dimension that cannot be expressed as a filter", () => {
    const fields = buildAssociativeFields([
      { code: FILTERABLE[0], label: "kept" },
      { code: "notAFilterableDimension", label: "Nonsense" },
    ]);

    expect(fields.map((f) => f.dimension)).toEqual([FILTERABLE[0]]);
  });

  it("uses the registry label, and falls back to the code rather than inventing one", () => {
    const labelled = buildAssociativeFields([{ code: FILTERABLE[0], label: "Published label" }]);
    expect(labelled[0].label).toBe("Published label");

    const unlabelled = buildAssociativeFields([{ code: FILTERABLE[0], label: "  " }]);
    expect(unlabelled[0].label).toBe(FILTERABLE[0]);
  });

  it("FAILS CLOSED with no registry rather than falling back to a typed list", () => {
    // A default field set would be the hardcoding this task removes.
    expect(buildAssociativeFields(null)).toEqual([]);
    expect(buildAssociativeFields(undefined)).toEqual([]);
    expect(buildAssociativeFields([])).toEqual([]);
  });
});