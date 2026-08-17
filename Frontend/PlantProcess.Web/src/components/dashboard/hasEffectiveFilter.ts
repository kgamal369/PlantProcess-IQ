// T-051. IS A FILTER ACTUALLY NARROWING THIS QUERY?
//
// The discriminator between "empty" and "filtered-empty". It must be asked of
// the SAME merged object the query was sent with, because a filter that was not
// sent cannot have narrowed anything.
//
// A key that is present but carries nothing does not narrow. The dashboard
// merges ten global filter slots into every request, so counting keys would
// make a genuinely empty result look like a scope answer - which is the one
// distinction this state exists to protect.
//
// This normalises values only. It does not interpret them, and it is not a
// filter framework.

export function hasEffectiveFilter(filters: unknown): boolean {
  if (filters === null || typeof filters !== "object") return false;

  return Object.values(filters as Record<string, unknown>).some(isEffectiveValue);
}

function isEffectiveValue(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === "string") return value.trim() !== "";
  if (Array.isArray(value)) return value.some(isEffectiveValue);
  if (typeof value === "object") {
    return Object.values(value as Record<string, unknown>).some(isEffectiveValue);
  }
  return true;
}

export default hasEffectiveFilter;
