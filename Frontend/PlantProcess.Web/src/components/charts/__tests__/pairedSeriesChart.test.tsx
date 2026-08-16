// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { PAIRED_ROLES, PairedSeriesChart } from "../PairedSeriesChart";

// T-046-R1. P1-P6.

function pair(category: string, a: number | null, b: number | null) {
  return {
    state: "PUBLISHED",
    category, categoryLabel: category,
    seriesALabel: "A", seriesAValue: a,
    seriesBLabel: "B", seriesBValue: b,
  };
}

describe("T-046-R1 the paired renderer keeps two series independent", () => {
  it("P5 declares roles, and binding is by role not position", () => {
    expect([...PAIRED_ROLES]).toEqual([
      "category", "categoryLabel", "seriesALabel", "seriesAValue", "seriesBLabel", "seriesBValue",
    ]);
  });

  it("P1 draws two values against one category", () => {
    render(<PairedSeriesChart rows={[pair("E1", 10, 4), pair("E2", 6, 6)]} />);
    expect(screen.getByTestId("paired-series-chart")).toBeInTheDocument();
  });

  it("P2 preserves both series when they differ", () => {
    // The renderer must not normalise, ratio or reconcile them.
    render(<PairedSeriesChart rows={[pair("E1", 120, 15)]} />);
    expect(screen.getByTestId("paired-series-chart")).toBeInTheDocument();
  });

  it("P3 refuses a category missing one series rather than copying the other", () => {
    render(<PairedSeriesChart rows={[pair("E1", 10, null)]} />);

    // Nothing drawn, because the only category was incomplete.
    expect(screen.queryByTestId("paired-series-chart")).toBeNull();
    expect(screen.getByTestId("paired-state")).toBeInTheDocument();
  });

  it("P3b omits the incomplete category and says so when others are complete", () => {
    render(<PairedSeriesChart rows={[pair("E1", 10, 4), pair("E2", 6, null)]} />);

    expect(screen.getByTestId("paired-series-chart")).toBeInTheDocument();
    expect(screen.getByTestId("paired-incomplete")).toBeInTheDocument();
  });

  it("P4 does not invent a zero for a null value", () => {
    // A published zero is a measurement and draws; a null is not and does not.
    const { unmount } = render(<PairedSeriesChart rows={[pair("E1", 0, 0)]} />);
    expect(screen.getByTestId("paired-series-chart")).toBeInTheDocument();
    unmount();

    render(<PairedSeriesChart rows={[pair("E1", null, null)]} />);
    expect(screen.queryByTestId("paired-series-chart")).toBeNull();
  });

  it("reports a terminal state rather than an empty frame", () => {
    render(<PairedSeriesChart rows={[{ state: "NO_DOWNTIME_IN_SELECTION" }]} />);
    expect(screen.getByTestId("paired-state")).toBeInTheDocument();
  });
});