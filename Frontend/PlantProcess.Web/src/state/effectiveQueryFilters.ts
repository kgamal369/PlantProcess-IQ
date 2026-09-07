// T-094. THE EFFECTIVE POPULATION, COMPOSED IN ONE PLACE.
//
// A workspace selection now lives in two contracts: structural filters the
// product owns, and keyed filters on dimensions the customer published. Every
// consumer - widget query, workspace query, associative enumeration, reference
// discovery - needs the SAME effective set, and a consumer that receives only
// the structural half silently drops the declared selections and reports a
// wider population as the answer.
//
// So the merge is written once, here, and consumers ask for it rather than
// remembering to do it. Nothing in this file names a dimension.

import type { DeclaredDimensionFilter } from "../api/product-core/declared-dimension-types";

/** Transport state that is not a filter and must never narrow a population. */
export const TRANSPORT_ONLY_KEYS: readonly string[] = [
  "page", "pageSize", "sortBy", "sortDirection",
];

export interface EffectiveQueryFilters {
  /** Structural filters, transport keys removed. */
  structural: Record<string, unknown>;
  /** Keyed filters on published declarations, in stable code order. */
  dimensionFilters: DeclaredDimensionFilter[];
}

function isMeaningful(value: unknown): boolean {
  return value !== undefined && value !== null && value !== "";
}

/**
 * The effective population predicate: structural filters plus declared filters,
 * with transport-only state removed.
 *
 * `omitStructuralKey` and `omitDeclaredCode` express the associative law - "every
 * active selection MINUS the field currently being enumerated". Omitting one
 * declared code must never drop the others, which is what makes an enumeration
 * honest when two declared dimensions are both selected.
 */
export function composeEffectiveFilters(
  structural: Record<string, unknown> | null | undefined,
  declared: readonly DeclaredDimensionFilter[] | null | undefined,
  options?: {
    omitStructuralKey?: string | null;
    omitDeclaredCode?: string | null;
    includeTransport?: boolean;
  }
): EffectiveQueryFilters {
  const omitStructuralKey = options?.omitStructuralKey ?? null;
  const omitDeclaredCode = options?.omitDeclaredCode ?? null;
  const includeTransport = options?.includeTransport === true;

  const keptStructural: Record<string, unknown> = {};
  for (const key of Object.keys(structural ?? {})) {
    if (key === omitStructuralKey) { continue; }
    if (!includeTransport && TRANSPORT_ONLY_KEYS.indexOf(key) >= 0) { continue; }
    const value = (structural as Record<string, unknown>)[key];
    if (!isMeaningful(value)) { continue; }
    keptStructural[key] = value;
  }

  const keptDeclared = (declared ?? [])
    .filter((filter) => !!filter && !!filter.code && isMeaningful(filter.value))
    .filter((filter) => filter.code !== omitDeclaredCode)
    .map((filter) => ({ code: filter.code, value: String(filter.value) }))
    .sort((left, right) => left.code.localeCompare(right.code));

  return { structural: keptStructural, dimensionFilters: keptDeclared };
}

/**
 * The effective set as ONE request body fragment. A consumer spreads this into
 * its query payload and cannot forget the declared half, because there is no
 * separate half to forget.
 */
export function toRequestFilters(
  effective: EffectiveQueryFilters
): Record<string, unknown> {
  const body: Record<string, unknown> = { ...effective.structural };

  // An empty keyed set is omitted rather than sent as [], so a request with no
  // declared selection canonicalises exactly as it did before this contract.
  if (effective.dimensionFilters.length > 0) {
    body.dimensionFilters = effective.dimensionFilters;
  }

  return body;
}

/** The count a filter bar shows: structural plus declared, transport excluded. */
export function activeSelectionCount(effective: EffectiveQueryFilters): number {
  return Object.keys(effective.structural).length + effective.dimensionFilters.length;
}