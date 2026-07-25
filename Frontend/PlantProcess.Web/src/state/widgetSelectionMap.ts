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

/** PPIQ-WIDGETFIX: temporal dimensions have no single filter key - they map to
 * the fromUtc/toUtc range instead. A click on a day, week or month therefore
 * narrows the whole workspace to that period, which is the honest Qlik
 * behaviour. Unparseable values return null so the click falls back to opening
 * drilldown evidence without filtering anything. */
export const TEMPORAL_DIMENSIONS = ["day", "week", "month"];

export function isTemporalDimension(dimensionCode?: string | null): boolean {
  if (!dimensionCode) return false;
  return TEMPORAL_DIMENSIONS.indexOf(dimensionCode) >= 0;
}

export function timeDimensionRange(
  dimensionCode?: string | null,
  value?: string | number | null
): { fromUtc: string; toUtc: string } | null {
  if (!dimensionCode) return null;
  if (value === null || value === undefined) return null;
  const raw = String(value).trim();
  if (!raw) return null;
  const dayMs = 86400000;
  const iso = (d: Date) => d.toISOString();

  if (dimensionCode === "day") {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(raw);
    if (!m) return null;
    const start = new Date(Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]), 0, 0, 0, 0));
    if (Number.isNaN(start.getTime())) return null;
    return { fromUtc: iso(start), toUtc: iso(new Date(start.getTime() + dayMs - 1)) };
  }

  if (dimensionCode === "month") {
    const m = /^(\d{4})-(\d{2})$/.exec(raw);
    if (!m) return null;
    const start = new Date(Date.UTC(Number(m[1]), Number(m[2]) - 1, 1, 0, 0, 0, 0));
    const next = new Date(Date.UTC(Number(m[1]), Number(m[2]), 1, 0, 0, 0, 0));
    if (Number.isNaN(start.getTime()) || Number.isNaN(next.getTime())) return null;
    return { fromUtc: iso(start), toUtc: iso(new Date(next.getTime() - 1)) };
  }

  if (dimensionCode === "week") {
    const m = /^(\d{4})-W(\d{1,2})$/i.exec(raw);
    if (!m) return null;
    const year = Number(m[1]);
    const week = Number(m[2]);
    if (week < 1 || week > 53) return null;
    // ISO-8601: the week containing 4 January is week 1; weeks start Monday.
    const jan4 = new Date(Date.UTC(year, 0, 4, 0, 0, 0, 0));
    const jan4Dow = jan4.getUTCDay() === 0 ? 7 : jan4.getUTCDay();
    const week1Monday = new Date(jan4.getTime() - (jan4Dow - 1) * dayMs);
    const start = new Date(week1Monday.getTime() + (week - 1) * 7 * dayMs);
    if (Number.isNaN(start.getTime())) return null;
    return { fromUtc: iso(start), toUtc: iso(new Date(start.getTime() + 7 * dayMs - 1)) };
  }

  return null;
}
