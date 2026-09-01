export type CanonicalRegistryMetadata = {
  authority?: string;
  dimensions?: Record<string, unknown>[];
  measures?: Record<string, unknown>[];
  refusals?: Record<string, unknown>[];
};

function asArray(value: unknown): Record<string, unknown>[] {
  return Array.isArray(value)
    ? value.filter((item): item is Record<string, unknown> =>
        typeof item === "object" && item !== null)
    : [];
}

function chartCode(item: Record<string, unknown>): string {
  return String(item.code ?? "");
}

function withClosedChartCompatibility(
  item: Record<string, unknown>,
  closedChartCodes: string[],
): Record<string, unknown> {
  const declared = Array.isArray(item.compatibleChartTypes)
    ? item.compatibleChartTypes.map(String)
    : [];

  const compatibleChartTypes =
    declared.length === 0
      ? closedChartCodes
      : declared.filter((code) => closedChartCodes.includes(code));

  return { ...item, compatibleChartTypes };
}

/**
 * T-092 boundary:
 * - chartTypes/numeric product grammar stay with the shipped binary;
 * - customer dimensions/measures come only from the published canonical registry;
 * - semantic refusals remain visible to authoring rather than being replaced by
 *   a guessed fallback such as Average.
 */
export function mergeCanonicalRegistryMetadata(
  legacyMetadata: Record<string, unknown> | null | undefined,
  registryMetadata: CanonicalRegistryMetadata | null | undefined,
): Record<string, unknown> {
  const legacy = legacyMetadata ?? {};
  const chartTypes = asArray(legacy.chartTypes);
  const closedChartCodes = chartTypes.map(chartCode).filter(Boolean);

  const dimensions = asArray(registryMetadata?.dimensions)
    .map((item) => withClosedChartCompatibility(item, closedChartCodes));
  const measures = asArray(registryMetadata?.measures)
    .map((item) => withClosedChartCompatibility(item, closedChartCodes));

  return {
    ...legacy,
    chartTypes,
    dimensions,
    measures,
    registryAuthority: registryMetadata?.authority ?? "canonical-definition-projection",
    registryRefusals: Array.isArray(registryMetadata?.refusals)
      ? registryMetadata?.refusals
      : [],
  };
}
