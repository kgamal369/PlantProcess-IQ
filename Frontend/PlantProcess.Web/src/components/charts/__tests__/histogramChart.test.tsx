// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { HISTOGRAM_ROLES, HistogramChart } from "../HistogramChart";

// T-047 Pack A. The claim under test is that the renderer binds published
// column ROLES by name and reports a terminal state instead of drawing an
// empty chart. The populations are invented: a renderer that knew what a
// parameter or a risk score was would not be exercised by these fixtures.

function bin(label: string, lower: number, upper: number, count: number) {
  return { state: "DISTRIBUTION_PUBLISHED", binLabel: label, binLower: lower, binUpper: upper, count };
}

describe("T-047 the histogram binds roles by name", () => {
  it("declares exactly the five published roles", () => {
    expect([...HISTOGRAM_ROLES]).toEqual(["state", "binLabel", "binLower", "binUpper", "count"]);
  });

  it("draws every published interval, including empty ones", () => {
    render(
      <HistogramChart
        rows={[bin("0 to 5", 0, 5, 3), bin("5 to 10", 5, 10, 0), bin("10 to 15", 10, 15, 7)]}
      />
    );

    expect(screen.getByTestId("histogram-chart")).toBeInTheDocument();
  });

  it("reports an unselected parameter rather than drawing nothing", () => {
    render(<HistogramChart rows={[{ state: "PARAMETER_NOT_SELECTED", binLabel: null, count: 0 }]} />);

    expect(screen.getByTestId("distribution-state")).toBeInTheDocument();
    expect(screen.queryByTestId("histogram-chart")).toBeNull();
  });

  it("distinguishes an empty selection from a single-valued population", () => {
    const { unmount } = render(
      <HistogramChart rows={[{ state: "NO_OBSERVATIONS_IN_SELECTION", binLabel: null, count: 0 }]} />
    );
    const empty = screen.getByTestId("distribution-state").textContent ?? "";
    unmount();

    render(<HistogramChart rows={[{ state: "SINGLE_VALUE_POPULATION", binLabel: null, count: 0 }]} />);
    const single = screen.getByTestId("distribution-state").textContent ?? "";

    expect(empty).not.toEqual(single);
  });

  it("treats no rows at all as an empty selection, never as a drawn chart", () => {
    render(<HistogramChart rows={[]} />);
    expect(screen.queryByTestId("histogram-chart")).toBeNull();
  });
});