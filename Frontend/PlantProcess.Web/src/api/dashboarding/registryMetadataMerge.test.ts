import { describe, expect, it } from "vitest";
import { mergeCanonicalRegistryMetadata } from "./registryMetadataMerge";

const legacy = {
  chartTypes: [
    { code: "bar", label: "Bar" },
    { code: "line", label: "Line" },
  ],
  dimensions: [{ code: "CompiledPlantVocabulary", label: "must disappear" }],
  measures: [{ code: "CompiledAverage", label: "must disappear" }],
  filters: [{ code: "closed-filter-grammar" }],
};

describe("T-092 canonical registry frontend convergence", () => {
  it("makes a customer-shaped dimension and measure selectable with the same binary", () => {
    const result = mergeCanonicalRegistryMetadata(legacy, {
      authority: "canonical-definition-projection",
      dimensions: [{
        code: "customer.shift_family",
        label: "Customer Shift Family",
        grainCode: "customer.event",
        dataType: "text",
        compatibleChartTypes: [],
      }],
      measures: [{
        code: "customer.energy_delta",
        label: "Customer Energy Delta",
        grainCode: "customer.event",
        aggregation: "Delta",
        signalCode: "energy.counter",
        compatibleChartTypes: ["line"],
      }],
      refusals: [],
    });

    expect(result.chartTypes).toEqual(legacy.chartTypes);
    expect(result.dimensions).toEqual([
      expect.objectContaining({
        code: "customer.shift_family",
        compatibleChartTypes: ["bar", "line"],
      }),
    ]);
    expect(result.measures).toEqual([
      expect.objectContaining({
        code: "customer.energy_delta",
        aggregation: "Delta",
        compatibleChartTypes: ["line"],
      }),
    ]);
  });

  it("does not reintroduce the legacy compiled dimension/measure catalogue", () => {
    const result = mergeCanonicalRegistryMetadata(legacy, {
      dimensions: [],
      measures: [],
      refusals: [],
    });

    expect(result.dimensions).toEqual([]);
    expect(result.measures).toEqual([]);
    expect(JSON.stringify(result)).not.toContain("CompiledPlantVocabulary");
    expect(JSON.stringify(result)).not.toContain("CompiledAverage");
    expect(result.chartTypes).toEqual(legacy.chartTypes);
    expect(result.filters).toEqual(legacy.filters);
  });

  it("keeps semantic refusals observable and never invents Average", () => {
    const result = mergeCanonicalRegistryMetadata(legacy, {
      dimensions: [],
      measures: [],
      refusals: [
        { code: "m1", errorCode: "AG01", message: "aggregation_semantics_undeclared" },
        { code: "d1", errorCode: "GR01", message: "analysis_grain_undeclared" },
      ],
    });

    expect(result.registryRefusals).toEqual([
      expect.objectContaining({ errorCode: "AG01" }),
      expect.objectContaining({ errorCode: "GR01" }),
    ]);
    expect(JSON.stringify(result.registryRefusals)).not.toContain("Average");
  });
});
