
/**
 * M1-16. The bind step of Authoring Layer Specification section 16.3.
 *
 * THE BUG THIS CLOSES. SavedDashboardWidget inferred its category column by
 * matching the stored dimension code, then fell back to "the first column not
 * named value". For a three-column result that is a GUESS - and it guesses
 * correctly often enough to stay hidden until a query returns its columns in a
 * different order, which is the worst possible failure mode in front of a
 * customer, because the chart looks fine and is wrong.
 *
 * WHERE IT PERSISTS, and why no migration is needed.
 * DashboardWidgetDefinitionRecord already carries displayOptionsJson, a free
 * JSON string that round-trips through create and update on the same record as
 * queryExpression. The specification asks for the mapping to be persisted
 * BESIDE THE EXPRESSION; this is that place, and it costs no column, no DTO
 * change and no migration.
 *
 * BY NAME, NEVER BY INDEX. The acceptance criterion is that reordering the
 * query's columns preserves the mapping by name or reports it stale by name,
 * and never silently repoints it. Storing an index would repoint silently on
 * the first reorder, so roles store the column CODE.
 */

export type WidgetRole = "category" | "value" | "secondary";

export interface WidgetRoleBinding {
  category: string | null;
  value: string | null;
  secondary: string | null;
}

export const EMPTY_ROLE_BINDING: WidgetRoleBinding = {
  category: null,
  value: null,
  secondary: null,
};

const KEY = "roleBinding";

function parseOptions(displayOptionsJson: string | null | undefined): Record<string, unknown> {
  if (!displayOptionsJson) { return {}; }
  try {
    const parsed = JSON.parse(displayOptionsJson) as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : {};
  } catch {
    // A malformed options blob is not a reason to lose the widget. It is
    // treated as absent, and the caller falls back to inference.
    return {};
  }
}

function asColumn(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}

/** Returns null when the widget has never been bound - the caller then infers. */
export function readRoleBinding(
  displayOptionsJson: string | null | undefined,
): WidgetRoleBinding | null {
  const raw = parseOptions(displayOptionsJson)[KEY];
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) { return null; }
  const o = raw as Record<string, unknown>;
  const binding: WidgetRoleBinding = {
    category: asColumn(o.category),
    value: asColumn(o.value),
    secondary: asColumn(o.secondary),
  };
  // An object with no roles set is the same as never having been bound.
  return binding.category || binding.value || binding.secondary ? binding : null;
}

/** Merges the binding into the existing options, preserving every other key. */
export function writeRoleBinding(
  displayOptionsJson: string | null | undefined,
  binding: WidgetRoleBinding,
): string {
  const options = parseOptions(displayOptionsJson);
  if (!binding.category && !binding.value && !binding.secondary) {
    delete options[KEY];
  } else {
    options[KEY] = {
      category: binding.category,
      value: binding.value,
      secondary: binding.secondary,
    };
  }
  return JSON.stringify(options);
}

/**
 * Returns the roles whose bound column is absent from the columns the query
 * actually returned. A non-empty result must be REPORTED, never repaired by
 * repointing - that is the whole point of the acceptance criterion.
 */
export function staleRoles(
  binding: WidgetRoleBinding | null,
  columns: readonly string[],
): WidgetRole[] {
  if (!binding) { return []; }
  const present = new Set(columns);
  const stale: WidgetRole[] = [];
  for (const role of ["category", "value", "secondary"] as WidgetRole[]) {
    const bound = binding[role];
    if (bound && !present.has(bound)) { stale.push(role); }
  }
  return stale;
}

/** "category (shift_code)" - a sentence fragment that names the column. */
export function describeStale(
  binding: WidgetRoleBinding | null,
  roles: readonly WidgetRole[],
): string {
  if (!binding || roles.length === 0) { return ""; }
  return roles.map((r) => r + " (" + String(binding[r]) + ")").join(", ");
}