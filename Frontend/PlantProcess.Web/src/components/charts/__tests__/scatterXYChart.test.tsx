// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SCATTER_XY_ROLES, ScatterXYChart } from "../ScatterXYChart";

function point(label: string, x: number, y: number) {
  return {
    state: "RELATIONSHIP_PUBLISHED", materialUnitId: label, materialLabel: label,
    xValue: x, yValue: y, xParameterCode: "P_ONE", yParameterCode: "P_TWO",
  };
}

describe("T-047 the scatter binds two numeric axes by name", () => {
  it("declares exactly the seven published roles", () => {
    expect([...SCATTER_XY_ROLES]).toEqual([
      "state", "materialUnitId", "materialLabel", "xValue", "yValue", "xParameterCode", "yParameterCode",
    ]);
  });

  it("draws a published relationship", () => {
    render(<ScatterXYChart rows={[point("A", 1, 9), point("B", 2, 7), point("C", 3, 5)]} />);
    expect(screen.getByTestId("scatter-xy-chart")).toBeInTheDocument();
  });

  it("refuses a parameter scattered against itself", () => {
    render(<ScatterXYChart rows={[{ state: "SAME_PARAMETER_SELECTED" }]} />);
    expect(screen.getByTestId("relationship-state")).toBeInTheDocument();
    expect(screen.queryByTestId("scatter-xy-chart")).toBeNull();
  });

  it("distinguishes a missing second parameter from an absent overlap", () => {
    const { unmount } = render(<ScatterXYChart rows={[{ state: "SECOND_PARAMETER_NOT_SELECTED" }]} />);
    const missing = screen.getByTestId("relationship-state").textContent ?? "";
    unmount();

    render(<ScatterXYChart rows={[{ state: "NO_OVERLAPPING_MATERIALS" }]} />);
    expect(screen.getByTestId("relationship-state").textContent).not.toEqual(missing);
  });

  it("treats no rows as an absent overlap, never as a drawn chart", () => {
    render(<ScatterXYChart rows={[]} />);
    expect(screen.queryByTestId("scatter-xy-chart")).toBeNull();
  });
});