import type { DashboardFilters } from "@/api/productApiClient";

/** Maps a rendered dashboard dimension to its real workspace filter.
 * Dimensions without an honest filter counterpart return null: they may
 * open drilldown evidence, but they must never fabricate materialCode. */
export type SelectionFilterField = keyof DashboardFilters;

const DIMENSION_TO_FILTER: Record<string, SelectionFilterField> = {
  site: "siteId",
  area: "areaId",
  equipment: "equipmentId",
  sourceSystem: "sourceSystem",
  materialUnitType: "materialUnitType",
  shiftCode: "shiftCode",
  defectType: "defectType",
  parameterCode: "parameterCode",
  riskClass: "riskClass",
};

export function dimensionToFilterField(dimensionCode?: string | null): SelectionFilterField | null {
  if (!dimensionCode) return null;
  return DIMENSION_TO_FILTER[dimensionCode] ?? null;
}

/** Dimensions that genuinely drive a workspace filter. */
export const FILTERABLE_DIMENSIONS = Object.keys(DIMENSION_TO_FILTER);