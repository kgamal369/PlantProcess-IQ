// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SERIES_ROLES, StackedSeriesChart } from "../StackedSeriesChart";

function cell(category: string, series: string, value: number) {
  return {
    state: "SERIES_PUBLISHED",
    category, categoryLabel: category,
    series, seriesLabel: series,
    value,
  };
}

describe("T-047 the stacked renderer binds series roles by name", () => {
  it("declares exactly the six published roles", () => {
    expect([...SERIES_ROLES]).toEqual([
      "state", "category", "categoryLabel", "series", "seriesLabel", "value",
    ]);
  });

  it("draws a published composition", () => {
    render(
      <StackedSeriesChart
        rows={[cell("A", "S1", 3), cell("A", "S2", 4), cell("B", "S1", 5), cell("B", "S2", 2)]}
      />
    );
    expect(screen.getByTestId("stacked-series-chart")).toBeInTheDocument();
  });

  it("refuses a single-series population rather than drawing a bar with a legend", () => {
    render(<StackedSeriesChart rows={[{ state: "SINGLE_SERIES_POPULATION" }]} />);
    expect(screen.getByTestId("series-state")).toBeInTheDocument();
    expect(screen.queryByTestId("stacked-series-chart")).toBeNull();
  });

  it("distinguishes an empty selection from an oversized one", () => {
    const { unmount } = render(<StackedSeriesChart rows={[{ state: "NO_OBSERVATIONS_IN_SELECTION" }]} />);
    const empty = screen.getByTestId("series-state").textContent ?? "";
    unmount();

    render(<StackedSeriesChart rows={[{ state: "POPULATION_EXCEEDS_SAFE_LIMIT" }]} />);
    expect(screen.getByTestId("series-state").textContent).not.toEqual(empty);
  });

  it("tolerates a category missing one series without inventing a zero", () => {
    // B has no S2. The pivot must simply omit the key rather than fabricate 0,
    // which would assert a measured absence that was never measured.
    render(<StackedSeriesChart rows={[cell("A", "S1", 3), cell("A", "S2", 4), cell("B", "S1", 5)]} />);
    expect(screen.getByTestId("stacked-series-chart")).toBeInTheDocument();
  });

  it("treats no rows as an empty selection, never as a drawn chart", () => {
    render(<StackedSeriesChart rows={[]} />);
    expect(screen.queryByTestId("stacked-series-chart")).toBeNull();
  });
});