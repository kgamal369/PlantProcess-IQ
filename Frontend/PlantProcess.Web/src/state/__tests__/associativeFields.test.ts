import { describe, expect, it } from "vitest";
import { buildAssociativeFields } from "../associativeFields";
import { STRUCTURAL_FILTER_DIMENSIONS } from "../widgetSelectionMap";

describe("T-094 associative fields use two explicit authorities", () => {
  it("projects product metadata only when the dimension is structural", () => {
    const fields = buildAssociativeFields([
      { code: STRUCTURAL_FILTER_DIMENSIONS[0], label: "Owned" },
      { code: "domainMetadataOnly", label: "Not a declaration" },
    ], []);
    expect(fields.map((x) => x.dimension)).toEqual([STRUCTURAL_FILTER_DIMENSIONS[0]]);
    expect(fields[0].kind).toBe("structural");
  });

  it("adds a customer dimension only from the published-declaration input", () => {
    const fields = buildAssociativeFields([], [
      { code: "customerConcept", label: "Customer concept", isExecutable: true },
    ]);
    expect(fields).toEqual([{
      key: "customerConcept", dimension: "customerConcept",
      label: "Customer concept", measureCode: "observationCount", kind: "declared",
    }]);
  });

  it("does not turn arbitrary product metadata into a declared dimension", () => {
    expect(buildAssociativeFields([{ code: "customerConcept", label: "Looks custom" }], []))
      .toEqual([]);
  });

  it("structural authority wins a code collision and no second field is invented", () => {
    const code = STRUCTURAL_FILTER_DIMENSIONS[0];
    const fields = buildAssociativeFields(
      [{ code, label: "Product label" }],
      [{ code, label: "Tenant collision", isExecutable: true }],
    );
    expect(fields).toHaveLength(1);
    expect(fields[0].kind).toBe("structural");
    expect(fields[0].label).toBe("Product label");
  });

  it("fails closed with neither authority instead of inventing fallback fields", () => {
    expect(buildAssociativeFields(null, null)).toEqual([]);
    expect(buildAssociativeFields(undefined, undefined)).toEqual([]);
  });
});
