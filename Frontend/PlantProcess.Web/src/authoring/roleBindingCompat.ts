// PPIQ T-038. THE LEGACY LABEL ADAPTER.
//
// WHY IT EXISTS. The retiring panel offered the returned columns' LABELS as the
// role choices and stored a label; SavedDashboardWidget resolves the saved
// binding against the columns' CODES. So a widget authored before T-038 can
// carry a token that is a label, and reading it strictly would report a column
// as missing when it is right there under another name. That would be a false
// alarm, and a false alarm about a stale mapping is as damaging as a silent
// repoint.
//
// HIS RULE, implemented exactly and with nothing added:
//
//   the token equals a returned column CODE          -> current binding
//   otherwise it equals EXACTLY ONE column LABEL      -> legacy binding,
//                                                        resolved to that code
//   otherwise                                         -> unresolved, and the
//                                                        author remaps by name
//
// NO fuzzy matching. NO index matching. NO first-column-wins. NO resolution at
// all when two columns share a label. This normalises one known bug in one
// direction; it is not an inference mechanism and it must never grow into one.
// Resolved tokens are written back into the binding, so the next successful
// save persists the code and the widget stops being legacy.

import type { WidgetRole, WidgetRoleBinding } from "@/api/product-core/widget-role-binding";
import { ROLE_ORDER, roleLabel } from "./roleBindingPresentation";

export interface ReturnedColumn { code: string; label: string }

export type RoleTokenResolution =
  | { kind: "code"; column: string }
  | { kind: "legacy-label"; column: string }
  | { kind: "missing" }
  | { kind: "ambiguous"; matches: number };

/** Null token means the role was never bound, which is not a resolution problem. */
export function resolveRoleToken(
  token: string | null | undefined,
  columns: readonly ReturnedColumn[],
): RoleTokenResolution | null {
  if (!token) { return null; }
  for (const c of columns) {
    if (c.code === token) { return { kind: "code", column: c.code }; }
  }
  const byLabel = columns.filter((c) => c.label === token);
  if (byLabel.length === 1) { return { kind: "legacy-label", column: byLabel[0].code }; }
  if (byLabel.length > 1) { return { kind: "ambiguous", matches: byLabel.length }; }
  return { kind: "missing" };
}

export interface RoleBindingNormalisation {
  /** The binding with every resolvable legacy token rewritten to its code. */
  binding: WidgetRoleBinding;
  /** Roles that were stored as a label and now carry the column code. */
  resolved: { role: WidgetRole; from: string; to: string }[];
  /** Roles whose token matches more than one column label. */
  ambiguous: { role: WidgetRole; token: string }[];
}

export function normaliseRoleBinding(
  binding: WidgetRoleBinding,
  columns: readonly ReturnedColumn[],
): RoleBindingNormalisation {
  const next: WidgetRoleBinding = { ...binding };
  const resolved: { role: WidgetRole; from: string; to: string }[] = [];
  const ambiguous: { role: WidgetRole; token: string }[] = [];
  for (const role of ROLE_ORDER) {
    const token = binding[role];
    const outcome = resolveRoleToken(token, columns);
    if (outcome === null) { continue; }
    if (outcome.kind === "legacy-label") {
      next[role] = outcome.column;
      resolved.push({ role, from: String(token), to: outcome.column });
    } else if (outcome.kind === "ambiguous") {
      ambiguous.push({ role, token: String(token) });
    }
  }
  return { binding: next, resolved, ambiguous };
}

/** Said once, in the words the author reads, and it names every column. */
export function describeLegacyResolution(
  resolved: readonly { role: WidgetRole; from: string; to: string }[],
): string {
  if (resolved.length === 0) { return ""; }
  const named = resolved
    .map((r) => roleLabel(r.role) + " (" + r.from + " is the column " + r.to + ")")
    .join(", ");
  return "This widget stored its mapping under a column label, which the previous"
    + " authoring surface did. " + named + ". Nothing was guessed: each label matched"
    + " exactly one returned column. Saving this widget stores the column code.";
}

export function describeAmbiguousResolution(
  ambiguous: readonly { role: WidgetRole; token: string }[],
): string {
  if (ambiguous.length === 0) { return ""; }
  const named = ambiguous.map((a) => roleLabel(a.role) + " (" + a.token + ")").join(", ");
  return "This widget stored its mapping under a label that more than one returned"
    + " column carries: " + named + ". It has NOT been resolved for you, because"
    + " choosing one of two identical labels would be a guess. Choose the column.";
}