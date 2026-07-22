import type { DashboardFilters } from "@/api/productApiClient";

/** M2-43 / DEF-005: dimensionCode -> workspace filter field.
 *
 * A chart click must filter by the field the chart is actually dimensioned on.
 * Before this map every selection wrote into "materialCode", so a donut of
 * defect types applied materialCode='CRACK_LONG' and emptied every widget
 * until "Clear all".
 *
 * HONEST SCOPE: dimensions with no filter counterpart (productFamily,
 * gradeOrRecipe, materialUnitType, day/week/month) keep the legacy
 * materialCode behaviour, because the chart selection contract requires a
 * valid filter key. Those dimensions are not used by the demo dashboards; a
 * true "no selection" path is full-catalogue scope. */
export type SelectionFilterField = keyof DashboardFilters;

const DIMENSION_TO_FILTER: Record<string, SelectionFilterField> = {
  site: "siteId",
  area: "areaId",
  equipment: "equipmentId",
  sourceSystem: "sourceSystem",
  shiftCode: "shiftCode",
  defectType: "defectType",
  parameterCode: "parameterCode",
  riskClass: "riskClass",
};

export function dimensionToFilterField(dimensionCode?: string | null): SelectionFilterField {
  if (!dimensionCode) return "materialCode";
  return DIMENSION_TO_FILTER[dimensionCode] ?? "materialCode";
}

/** Dimensions that genuinely drive a workspace filter. */
export const FILTERABLE_DIMENSIONS = Object.keys(DIMENSION_TO_FILTER);