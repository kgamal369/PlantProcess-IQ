// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { BOX_PLOT_ROLES, BoxPlotChart } from "../BoxPlotChart";

function group(label: string, min: number, q1: number, med: number, q3: number, max: number, n: number) {
  return {
    state: "SPREAD_PUBLISHED", category: label, label,
    minimum: min, q1, median: med, q3, maximum: max, observationCount: n,
  };
}

describe("T-047 the box plot binds spread roles by name", () => {
  it("declares exactly the nine published roles", () => {
    expect([...BOX_PLOT_ROLES]).toEqual([
      "state", "category", "label", "minimum", "q1", "median", "q3", "maximum", "observationCount",
    ]);
  });

  it("draws a published spread", () => {
    render(<BoxPlotChart rows={[group("A", 1, 2, 3, 4, 5, 40), group("B", 2, 3, 4, 5, 9, 31)]} />);
    expect(screen.getByTestId("box-plot-chart")).toBeInTheDocument();
  });

  it("lists a thin group instead of drawing a box for it", () => {
    render(
      <BoxPlotChart
        rows={[
          group("A", 1, 2, 3, 4, 5, 40),
          { state: "INSUFFICIENT_OBSERVATIONS", category: "B", label: "B", observationCount: 3 },
        ]}
      />
    );

    expect(screen.getByTestId("withheld-groups")).toBeInTheDocument();
  });

  it("says why there is no spread rather than drawing an empty frame", () => {
    render(<BoxPlotChart rows={[{ state: "GROUPING_NOT_SELECTED" }]} />);
    expect(screen.getByTestId("spread-state")).toBeInTheDocument();
    expect(screen.queryByTestId("box-plot-chart")).toBeNull();
  });

  it("distinguishes a too-large population from an empty one", () => {
    const { unmount } = render(<BoxPlotChart rows={[{ state: "POPULATION_EXCEEDS_SAFE_LIMIT" }]} />);
    const tooLarge = screen.getByTestId("spread-state").textContent ?? "";
    unmount();

    render(<BoxPlotChart rows={[{ state: "NO_OBSERVATIONS_IN_SELECTION" }]} />);
    expect(screen.getByTestId("spread-state").textContent).not.toEqual(tooLarge);
  });
});