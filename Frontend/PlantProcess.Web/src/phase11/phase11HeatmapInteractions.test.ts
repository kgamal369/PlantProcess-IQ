import { describe, expect, it } from "vitest";
import {
  bucketForValue,
  buildHeatmap,
  chartSeriesSignature,
  filterAndSortHeatmap,
} from "./phase11HeatmapInteractions";

const points = [
  { id: "a", label: "Caster", group: "line-1", value: 0.2 },
  { id: "b", label: "Furnace", group: "line-1", value: 0.7 },
  { id: "c", label: "Rolling", group: "line-2", value: 0.9 },
];

describe("Phase 11 heatmap and interactive chart helpers", () => {
  it("T-067 assigns correct heatmap buckets and color tokens", () => {
    expect(bucketForValue(0.1)).toBe("low");
    expect(bucketForValue(0.5)).toBe("medium");
    expect(bucketForValue(0.7)).toBe("high");
    expect(bucketForValue(0.9)).toBe("critical");

    const cells = buildHeatmap(points);
    expect(cells.map((x) => x.colorToken)).toContain("heatmap-critical");
  });

  it("T-067 filters by group and value threshold", () => {
    const rows = filterAndSortHeatmap(points, { group: "line-1", minValue: 0.3 });

    expect(rows).toHaveLength(1);
    expect(rows[0].id).toBe("b");
  });

  it("T-067 sorting changes rendered series signature", () => {
    const desc = filterAndSortHeatmap(points, { sortBy: "value", direction: "desc" });
    const asc = filterAndSortHeatmap(points, { sortBy: "value", direction: "asc" });

    expect(chartSeriesSignature(desc)).not.toBe(chartSeriesSignature(asc));
    expect(desc[0].id).toBe("c");
    expect(asc[0].id).toBe("a");
  });
});