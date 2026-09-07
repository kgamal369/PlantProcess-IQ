/** T-094. Fields the associative engine tracks.
 *
 * Product metadata contributes only PRODUCT-OWNED structural dimensions.
 * Tenant reference data contributes only PUBLISHED declared dimensions.
 * Keeping the two inputs separate prevents an arbitrary product metadata entry
 * from being reclassified as a customer declaration. */
import { structuralFilterFieldFor, isTemporalDimension } from "./widgetSelectionMap";

export type AssocFieldKind = "structural" | "declared";

export type AssocField = {
  key: string;
  dimension: string;
  label: string;
  measureCode: string;
  kind: AssocFieldKind;
};

type ProductDimension = { code: string; label?: string | null };
type PublishedDimension = { code: string; label?: string | null; isExecutable?: boolean };

export function buildAssociativeFields(
  productDimensions: ReadonlyArray<ProductDimension> | null | undefined,
  publishedDeclared: ReadonlyArray<PublishedDimension> | null | undefined,
): AssocField[] {
  if (!productDimensions && !publishedDeclared) return [];

  const fields: AssocField[] = [];
  const seen = new Set<string>();

  for (const dimension of productDimensions ?? []) {
    if (!dimension?.code || isTemporalDimension(dimension.code)) continue;
    const structural = structuralFilterFieldFor(dimension.code);
    if (!structural) continue;

    fields.push({
      key: String(structural),
      dimension: dimension.code,
      label: dimension.label?.trim() || dimension.code,
      measureCode: "observationCount",
      kind: "structural",
    });
    seen.add(dimension.code.toLowerCase());
  }

  for (const dimension of publishedDeclared ?? []) {
    if (!dimension?.code || isTemporalDimension(dimension.code)) continue;
    const normalized = dimension.code.toLowerCase();
    if (seen.has(normalized)) continue;

    fields.push({
      key: dimension.code,
      dimension: dimension.code,
      label: dimension.label?.trim() || dimension.code,
      measureCode: "observationCount",
      kind: "declared",
    });
    seen.add(normalized);
  }

  return fields;
}
