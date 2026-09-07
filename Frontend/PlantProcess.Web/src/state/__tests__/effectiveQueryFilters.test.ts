import { describe, expect, it } from "vitest";
import {
  composeEffectiveFilters,
  toRequestFilters,
  activeSelectionCount,
} from "../effectiveQueryFilters";
import {
  parseDeclaredFilterParam,
  formatDeclaredFilterParam,
  DeclaredFilterFormatError,
} from "@/api/product-core/declared-dimension-types";

// T-094 stage 2B-i. The cutover infrastructure, proved on its own before any
// consumer routes through it. Nothing here names a plant concept: the declared
// codes are neutral, which is the point of the contract.

describe("effective filter composition", () => {
  it("keeps structural filters and drops transport-only state", () => {
    const effective = composeEffectiveFilters(
      { siteId: "S1", page: 3, pageSize: 25, sortBy: "code", sortDirection: "asc" },
      []
    );

    expect(effective.structural).toEqual({ siteId: "S1" });
    expect(effective.dimensionFilters).toEqual([]);
  });

  it("drops empty, null and undefined structural values rather than sending them", () => {
    const effective = composeEffectiveFilters(
      { siteId: "S1", areaId: "", equipmentId: null, materialCode: undefined },
      []
    );

    expect(effective.structural).toEqual({ siteId: "S1" });
  });

  it("carries declared filters in stable code order", () => {
    const effective = composeEffectiveFilters({}, [
      { code: "bravo", value: "Y" },
      { code: "alpha", value: "X" },
    ]);

    expect(effective.dimensionFilters).toEqual([
      { code: "alpha", value: "X" },
      { code: "bravo", value: "Y" },
    ]);
  });

  it("THE ASSOCIATIVE LAW: omitting one declared code preserves the others", () => {
    // A minus-own must not mean "drop every declared filter". If it did, an
    // enumeration would return values that are impossible under the selections
    // still in force, and nobody would see it because the result looks fuller.
    const effective = composeEffectiveFilters(
      { siteId: "S1" },
      [
        { code: "alpha", value: "X" },
        { code: "bravo", value: "Y" },
      ],
      { omitDeclaredCode: "alpha" }
    );

    expect(effective.dimensionFilters).toEqual([{ code: "bravo", value: "Y" }]);
    expect(effective.structural).toEqual({ siteId: "S1" });
  });

  it("omitting a structural key preserves declared filters and other structural keys", () => {
    const effective = composeEffectiveFilters(
      { siteId: "S1", areaId: "A1" },
      [{ code: "alpha", value: "X" }],
      { omitStructuralKey: "siteId" }
    );

    expect(effective.structural).toEqual({ areaId: "A1" });
    expect(effective.dimensionFilters).toEqual([{ code: "alpha", value: "X" }]);
  });

  it("includes transport state only when a caller explicitly asks", () => {
    const effective = composeEffectiveFilters({ page: 2 }, [], { includeTransport: true });
    expect(effective.structural).toEqual({ page: 2 });
  });

  it("omits an empty keyed set from the request body so nothing canonicalises differently", () => {
    const body = toRequestFilters(composeEffectiveFilters({ siteId: "S1" }, []));
    expect(body).toEqual({ siteId: "S1" });
    expect("dimensionFilters" in body).toBe(false);
  });

  it("sends both halves as one body a consumer cannot half-forget", () => {
    const body = toRequestFilters(
      composeEffectiveFilters({ siteId: "S1" }, [{ code: "alpha", value: "X" }])
    );

    expect(body).toEqual({
      siteId: "S1",
      dimensionFilters: [{ code: "alpha", value: "X" }],
    });
  });

  it("counts structural and declared selections alike", () => {
    const effective = composeEffectiveFilters(
      { siteId: "S1", page: 4 },
      [{ code: "alpha", value: "X" }]
    );

    expect(activeSelectionCount(effective)).toBe(2);
  });
});

describe("the keyed filter wire form fails closed", () => {
  it("round-trips a well-formed entry", () => {
    const filter = { code: "alpha", value: "X" };
    expect(parseDeclaredFilterParam(formatDeclaredFilterParam(filter))).toEqual(filter);
  });

  it("splits on the FIRST separator so a value may contain one", () => {
    expect(parseDeclaredFilterParam("alpha:a:b")).toEqual({ code: "alpha", value: "a:b" });
  });

  it.each([
    "noSeparator",
    ":valueOnly",
    "codeOnly:",
    "   ",
    "",
  ])("REFUSES a malformed entry rather than dropping it: %s", (entry) => {
    // The whole point. A filter the client cannot read must not vanish, because
    // a vanished filter widens the population and the wider number is returned
    // as though it were the answer.
    let refusal: DeclaredFilterFormatError | null = null;
    try {
      parseDeclaredFilterParam(entry);
    } catch (error) {
      refusal = error as DeclaredFilterFormatError;
    }

    expect(refusal).not.toBeNull();
    expect(refusal!.refusalCode).toBe("DB09_dimension_filter_malformed");
  });

  it("names the same refusal the backend raises, so one vocabulary describes one fault", () => {
    try {
      parseDeclaredFilterParam("broken");
      throw new Error("expected a refusal");
    } catch (error) {
      expect((error as DeclaredFilterFormatError).refusalCode).toBe(
        "DB09_dimension_filter_malformed"
      );
    }
  });
});