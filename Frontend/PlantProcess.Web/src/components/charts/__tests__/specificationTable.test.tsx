// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SPECIFICATION_ROLES, SpecificationTable, conformanceOf } from "../SpecificationTable";

// T-047. Conditional formatting states a relationship between two persisted
// numbers. The scopes and parameters below are invented: a table that needed
// real chemistry names would not be exercised by S1/P1.

function spec(
  scope: string, parameter: string,
  minimum: number | null, target: number | null, maximum: number | null,
  actual: number | null
) {
  return {
    state: "SPECIFICATIONS_PUBLISHED",
    gradeOrRecipe: scope, parameterCode: parameter,
    minValue: minimum, targetValue: target, maxValue: maximum,
    unitOfMeasure: "pct", actualValue: actual,
    observationCount: actual === null ? 0 : 12,
    provenance: "test",
  };
}

describe("T-047 the specification table states conformance from its own row", () => {
  it("declares the published roles", () => {
    expect([...SPECIFICATION_ROLES]).toContain("actualValue");
    expect([...SPECIFICATION_ROLES]).toContain("minValue");
    expect([...SPECIFICATION_ROLES]).toContain("maxValue");
  });

  it("marks a value inside its bounds as within range", () => {
    expect(conformanceOf(5, 1, 10)).toBe("within");
  });

  it("marks a value under the minimum as below range", () => {
    expect(conformanceOf(0.5, 1, 10)).toBe("below");
  });

  it("marks a value over the maximum as above range", () => {
    expect(conformanceOf(11, 1, 10)).toBe("above");
  });

  it("treats a null minimum as untested rather than as zero", () => {
    // A maximum with no floor cannot be breached from below.
    expect(conformanceOf(-5, null, 10)).toBe("within");
    expect(conformanceOf(15, null, 10)).toBe("above");
  });

  it("NEVER calls an unobserved parameter conforming", () => {
    // The most damaging possible defect in this table.
    expect(conformanceOf(null, 1, 10)).toBe("unobserved");
    expect(conformanceOf(undefined, 1, 10)).toBe("unobserved");
  });

  it("renders the three comparison states as distinct cells", () => {
    render(
      <SpecificationTable
        rows={[
          spec("S1", "P1", 1, 5, 10, 5),
          spec("S1", "P2", 1, 5, 10, 0.2),
          spec("S1", "P3", 1, 5, 10, 40),
        ]}
      />
    );

    const states = screen.getAllByTestId("specification-observed").map((c) => c.getAttribute("data-state"));
    expect(states).toEqual(["within", "below", "above"]);
  });

  it("shows an unobserved parameter without a pass colour", () => {
    render(<SpecificationTable rows={[spec("S1", "P1", 1, 5, 10, null)]} />);

    const cell = screen.getByTestId("specification-observed");
    expect(cell.getAttribute("data-state")).toBe("unobserved");
    expect(cell.textContent).toContain("not observed");
  });

  it("keeps the value and the unit visible", () => {
    // Target and observed are deliberately DIFFERENT. Identical values make
    // this assertion ambiguous and hide whether the observed cell is the one
    // being read.
    render(<SpecificationTable rows={[spec("S1", "P1", 1, 5, 10, 7.25)]} />);

    expect(screen.getByTestId("specification-table")).toBeInTheDocument();
    expect(screen.getByText("pct")).toBeInTheDocument();

    const observed = screen.getByTestId("specification-observed");
    expect(observed.textContent).toBe("7.25");
    expect(observed.getAttribute("data-state")).toBe("within");
  });

  it("says so rather than drawing an empty table", () => {
    render(<SpecificationTable rows={[{ state: "NO_SPECIFICATIONS_RECORDED" }]} />);
    expect(screen.getByTestId("specification-state")).toBeInTheDocument();
  });
});