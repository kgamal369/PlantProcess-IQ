// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { HEATMAP_ROLES, HeatmapChart, intensityBucket } from "../HeatmapChart";

// T-046-R1. H1-H8. The axes are invented on purpose: a renderer that needed
// domain field names would not be exercised by x1/y1.

function cell(x: string, y: string, value: number | null) {
  return { state: "PUBLISHED", x, y, value };
}

describe("T-046-R1 the heatmap binds two axes and an intensity by name", () => {
  it("H8 declares exactly three domain-free roles", () => {
    expect([...HEATMAP_ROLES]).toEqual(["x", "y", "value"]);
  });

  it("H1 renders a cell per observed combination", () => {
    render(<HeatmapChart rows={[cell("x1", "y1", 1), cell("x2", "y1", 2), cell("x1", "y2", 3)]} />);

    expect(screen.getByTestId("heatmap-chart")).toBeInTheDocument();
    expect(screen.getAllByTestId("heatmap-cell")).toHaveLength(3);
  });

  it("H2 gives different values different intensity", () => {
    render(<HeatmapChart rows={[cell("x1", "y1", 0), cell("x2", "y1", 100)]} />);

    const buckets = screen.getAllByTestId("heatmap-cell").map((c) => c.getAttribute("data-bucket"));
    expect(new Set(buckets).size).toBeGreaterThan(1);
  });

  it("H3 refuses when the x role is absent", () => {
    render(<HeatmapChart rows={[{ state: "PUBLISHED", y: "y1", value: 1 }]} />);
    expect(screen.queryByTestId("heatmap-cell")).toBeNull();
  });

  it("H4 refuses when the y role is absent", () => {
    render(<HeatmapChart rows={[{ state: "PUBLISHED", x: "x1", value: 1 }]} />);
    expect(screen.queryByTestId("heatmap-cell")).toBeNull();
  });

  it("H5 never coerces a missing value to zero", () => {
    render(<HeatmapChart rows={[cell("x1", "y1", null), cell("x2", "y1", 5)]} />);

    // The null cell is absent, not a zero cell.
    expect(screen.getAllByTestId("heatmap-cell-absent")).toHaveLength(1);
    expect(screen.getAllByTestId("heatmap-cell")).toHaveLength(1);
  });

  it("H6 leaves an unobserved combination missing", () => {
    // Two axis values each, but only three of the four combinations exist.
    render(<HeatmapChart rows={[cell("x1", "y1", 1), cell("x2", "y1", 2), cell("x1", "y2", 3)]} />);

    expect(screen.getAllByTestId("heatmap-cell-absent")).toHaveLength(1);
  });

  it("H7 is deterministic under reordered input", () => {
    const forward = [cell("x1", "y1", 1), cell("x2", "y2", 2)];
    const reversed = [...forward].reverse();

    const first = render(<HeatmapChart rows={forward} />);
    const forwardCells = screen.getAllByTestId("heatmap-cell").length;
    first.unmount();

    render(<HeatmapChart rows={reversed} />);
    expect(screen.getAllByTestId("heatmap-cell").length).toBe(forwardCells);
  });

  it("buckets a flat range without dividing by zero", () => {
    expect(intensityBucket(5, 5, 5)).toBe(2);
    expect(intensityBucket(0, 0, 100)).toBe(0);
    expect(intensityBucket(100, 0, 100)).toBe(4);
  });

  it("says why rather than drawing an empty grid", () => {
    render(<HeatmapChart rows={[{ state: "NO_OBSERVATIONS_IN_SELECTION" }]} />);
    expect(screen.getByTestId("heatmap-state")).toBeInTheDocument();
  });
});