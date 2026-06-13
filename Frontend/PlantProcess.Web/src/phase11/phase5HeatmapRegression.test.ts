// P5-T05 heatmap interaction regression: filter/sort/drill/determinism on the real module API, against
// a deterministic demo cell set (not fixtures pulled at runtime). Proves the interactions the
// InteractiveHeatmap relies on behave correctly and reproducibly.
import { describe, expect, it } from "vitest";
import {
  bucketForValue,
  buildHeatmap,
  filterAndSortHeatmap,
  pointsInGroup,
  populationCount,
  type Phase11HeatmapPoint,
} from "@/phase11/phase11HeatmapInteractions";

const DEMO: Phase11HeatmapPoint[] = [
  { id: "c1", label: "Mn", group: "LineA", value: 0.91 },
  { id: "c2", label: "Si", group: "LineA", value: 0.42 },
  { id: "c3", label: "C", group: "LineB", value: 0.71 },
  { id: "c4", label: "S", group: "LineB", value: 0.12 },
  { id: "c5", label: "P", group: "LineA", value: 0.66 },
];

describe("P5-T05 heatmap interaction regression", () => {
  it("filter changes the cell set and population N updates", () => {
    expect(populationCount(filterAndSortHeatmap(DEMO, {}))).toBe(5);
    const lineA = filterAndSortHeatmap(DEMO, { group: "LineA" });
    expect(populationCount(lineA)).toBe(3);
    expect(lineA.every((c) => c.group === "LineA")).toBe(true);
    const strong = filterAndSortHeatmap(DEMO, { minValue: 0.65 });
    expect(strong.map((c) => c.id).sort()).toEqual(["c1", "c3", "c5"]);
  });

  it("sort re-orders the axis when direction changes", () => {
    const asc = filterAndSortHeatmap(DEMO, { sortBy: "value", direction: "asc" }).map((c) => c.value);
    const desc = filterAndSortHeatmap(DEMO, { sortBy: "value", direction: "desc" }).map((c) => c.value);
    expect(asc).toEqual([...asc].sort((a, b) => a - b));
    expect(desc).toEqual([...asc].reverse());
    const byLabel = filterAndSortHeatmap(DEMO, { sortBy: "label", direction: "asc" }).map((c) => c.label);
    expect(byLabel).toEqual([...byLabel].sort((a, b) => a.localeCompare(b)));
  });

  it("drill returns exactly the underlying records for a cell's row", () => {
    const cell = buildHeatmap(DEMO).find((c) => c.id === "c1");
    expect(cell).toBeDefined();
    const underlying = pointsInGroup(DEMO, cell!.group);
    expect(underlying.map((p) => p.id).sort()).toEqual(["c1", "c2", "c5"]);
    expect(populationCount(underlying)).toBe(3);
  });

  it("is deterministic: identical inputs produce identical output across runs", () => {
    const f = { group: "LineA", sortBy: "value", direction: "desc" } as const;
    expect(filterAndSortHeatmap(DEMO, f)).toEqual(filterAndSortHeatmap(DEMO, f));
  });

  it("buckets honour the published scale thresholds (legend boundaries)", () => {
    expect(bucketForValue(0.85)).toBe("critical");
    expect(bucketForValue(0.84)).toBe("high");
    expect(bucketForValue(0.65)).toBe("high");
    expect(bucketForValue(0.64)).toBe("medium");
    expect(bucketForValue(0.35)).toBe("medium");
    expect(bucketForValue(0.34)).toBe("low");
  });
});
