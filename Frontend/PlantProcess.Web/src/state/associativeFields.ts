/** M2-37: fields the associative engine tracks.
 * dimension = the dashboard widget-query dimensionCode used to enumerate the
 * field's values. If a code is not in the safety registry, the field degrades
 * honestly to "unavailable" (console.warn, no error surface). EDIT the
 * dimension strings here if your registry names differ. */
import { dimensionToFilterField, isTemporalDimension } from "./widgetSelectionMap";

export type AssocField = { key: string; dimension: string; label: string };

/** T-048. THE FIELD SET IS DERIVED FROM THE REGISTRY.
 *
 * The eight rows that used to sit here carried a comment telling the reader to
 * hand-edit dimension strings when the registry disagreed. That made adding a
 * dimension a frontend code change, and it let this file drift out of step
 * with the server without anything failing.
 *
 * NOTHING NEW IS MAPPED HERE. dimensionToFilterField already owns the
 * dimension-to-filter question and TEMPORAL_DIMENSIONS already owns which
 * dimensions are time buckets. This is a projection over both.
 *
 * A dimension is associative when it is FILTERABLE and NOT TEMPORAL. A time
 * bucket is a range, not a set of chips: rendering "2026-08-05" beside
 * "Line 3" would invite a reader to select a day as though it were a category.
 */
export function buildAssociativeFields(
  dimensions: ReadonlyArray<{ code: string; label?: string | null }> | null | undefined
): AssocField[] {
  if (!dimensions || dimensions.length === 0) {
    // FAIL CLOSED. No registry means no field set. An invented fallback list
    // would be the hardcoding this task removes, wearing a different name.
    return [];
  }

  const fields: AssocField[] = [];

  for (const dimension of dimensions) {
    if (!dimension?.code) { continue; }
    if (isTemporalDimension(dimension.code)) { continue; }

    const key = dimensionToFilterField(dimension.code);
    if (!key) { continue; }

    fields.push({
      key: String(key),
      dimension: dimension.code,
      // The registry's own label. A local override would be a second name for
      // the same thing, free to disagree with every other surface.
      label: dimension.label && dimension.label.trim() !== "" ? dimension.label : dimension.code,
    });
  }

  return fields;
}