/** M2-37: fields the associative engine tracks.
 * dimension = the dashboard widget-query dimensionCode used to enumerate the
 * field's values. If a code is not in the safety registry, the field degrades
 * honestly to "unavailable" (console.warn, no error surface). EDIT the
 * dimension strings here if your registry names differ. */
export type AssocField = { key: string; dimension: string; label: string };

export const ASSOC_FIELDS: AssocField[] = [
  { key: "materialCode", dimension: "materialCode", label: "Material" },
  { key: "defectType",   dimension: "defectType",   label: "Defect" },
  { key: "sourceSystem", dimension: "sourceSystem", label: "Source" },
  { key: "siteId",       dimension: "site",         label: "Site" },
  { key: "areaId",       dimension: "area",         label: "Area" },
  { key: "equipmentId",  dimension: "equipment",    label: "Equipment" },
  { key: "riskClass",    dimension: "riskClass",    label: "Risk class" },
  { key: "shiftCode",    dimension: "shift",        label: "Shift" },
];