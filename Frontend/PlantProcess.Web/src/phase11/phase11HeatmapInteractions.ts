export type Phase11HeatmapPoint = {
  id: string;
  label: string;
  group: string;
  value: number;
};

export type Phase11HeatmapBucket = "low" | "medium" | "high" | "critical";

export type Phase11HeatmapCell = Phase11HeatmapPoint & {
  bucket: Phase11HeatmapBucket;
  colorToken: string;
};

export type Phase11ChartFilter = {
  group?: string;
  minValue?: number;
  sortBy?: "label" | "value";
  direction?: "asc" | "desc";
};

export function bucketForValue(value: number): Phase11HeatmapBucket {
  if (value >= 0.85) return "critical";
  if (value >= 0.65) return "high";
  if (value >= 0.35) return "medium";
  return "low";
}

export function colorTokenForBucket(bucket: Phase11HeatmapBucket) {
  return {
    low: "heatmap-low",
    medium: "heatmap-medium",
    high: "heatmap-high",
    critical: "heatmap-critical",
  }[bucket];
}

export function buildHeatmap(points: Phase11HeatmapPoint[]): Phase11HeatmapCell[] {
  return points.map((point) => {
    const bucket = bucketForValue(point.value);

    return {
      ...point,
      bucket,
      colorToken: colorTokenForBucket(bucket),
    };
  });
}

export function filterAndSortHeatmap(
  points: Phase11HeatmapPoint[],
  filter: Phase11ChartFilter,
): Phase11HeatmapCell[] {
  let rows = buildHeatmap(points);

  if (filter.group) {
    rows = rows.filter((row) => row.group === filter.group);
  }

  if (typeof filter.minValue === "number") {
    rows = rows.filter((row) => row.value >= filter.minValue!);
  }

  const sortBy = filter.sortBy ?? "value";
  const direction = filter.direction ?? "desc";

  rows = rows.slice().sort((a, b) => {
    const result = sortBy === "label"
      ? a.label.localeCompare(b.label)
      : a.value - b.value;

    return direction === "asc" ? result : -result;
  });

  return rows;
}

export function chartSeriesSignature(points: Phase11HeatmapCell[]) {
  return points.map((point) => `${point.id}:${point.bucket}:${point.value}`).join("|");
}

/** P5-T01/T05: underlying records for a drilled heatmap cell - every point sharing the cell's row/group. */
export function pointsInGroup(points: Phase11HeatmapPoint[], group: string): Phase11HeatmapPoint[] {
  return points.filter((point) => point.group === group);
}

/** P5-T01/T05: population N for a rendered cell set. A3 requires N to be stated on every surface. */
export function populationCount(points: { length: number }): number {
  return points.length;
}
