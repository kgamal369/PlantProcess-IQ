
import { describe, expect, it } from "vitest";
import {
  backendAcceptedChartTypes,
  buildWidgetHeatmapCells,
  buildWidgetQueryFromDefinition,
  filterSortHeatmapCells,
  heatmapSeriesSignature,
  normalizeDashboardWidgetDefinition,
  p3t15DemoBackendWidget,
  validateWidgetDefinitionSchema,
} from "../../api/p3T15WidgetSchemaContract";

describe("P3-T15 widget schema-drift contract", () => {
  it("normalizes PascalCase backend widget definitions into one frontend contract", () => {
    const normalized = normalizeDashboardWidgetDefinition(p3t15DemoBackendWidget);

    expect(normalized.widgetCode).toBe("P3T15_HEATMAP_SCHEMA_DRIFT_PROOF");
    expect(normalized.chartType).toBe("heatmap");
    expect(normalized.dimensionCode).toBe("equipment");
    expect(normalized.measureCode).toBe("defectRate");
  });

  it("fails contract validation when a required field is missing", () => {
    const result = validateWidgetDefinitionSchema({
      ...p3t15DemoBackendWidget,
      ChartType: "",
    });

    expect(result.isValid).toBe(false);
    expect(result.errors.join(" ")).toContain("chartType");
  });

  it("keeps heatmap as a first-class backend accepted chart type", () => {
    expect(backendAcceptedChartTypes).toContain("heatmap");
  });

  it("builds widget query body from persisted widget definition JSON", () => {
    const normalized = normalizeDashboardWidgetDefinition(p3t15DemoBackendWidget);
    const query = buildWidgetQueryFromDefinition(normalized);

    expect(query.widgetType).toBe("chart");
    expect(query.chartType).toBe("heatmap");
    expect(query.dimensionCode).toBe("equipment");
    expect(query.measureCode).toBe("defectRate");
    expect(query.filters.sourceSystem).toBe("demo");
    expect(query.options.sortDirection).toBe("desc");
    expect(query.options.maxRows).toBe(50);
  });

  it("filters and sorts heatmap cells without mutating the base series", () => {
    const cells = buildWidgetHeatmapCells(
      [
        { equipment: "Caster 1", day: "Mon", defectRate: 0.16 },
        { equipment: "Caster 2", day: "Mon", defectRate: 0.34 },
        { equipment: "Mill 1", day: "Tue", defectRate: 0.45 },
      ],
      "equipment",
      "day",
      "defectRate",
    );

    const desc = filterSortHeatmapCells(cells, {
      sortBy: "value",
      direction: "desc",
    });

    const asc = filterSortHeatmapCells(cells, {
      sortBy: "value",
      direction: "asc",
    });

    const filtered = filterSortHeatmapCells(cells, {
      search: "caster",
      minValue: 0.2,
      sortBy: "value",
      direction: "desc",
    });

    expect(desc[0].x).toBe("Mill 1");
    expect(asc[0].x).toBe("Caster 1");
    expect(filtered).toHaveLength(1);
    expect(filtered[0].x).toBe("Caster 2");
    expect(heatmapSeriesSignature(desc)).not.toBe(heatmapSeriesSignature(asc));
    expect(cells).toHaveLength(3);
  });
});
