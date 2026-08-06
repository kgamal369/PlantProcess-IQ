// PPIQ T-037. THE PRESENTATION HALF OF THE ROLE-BINDING CAPABILITY.
//
// His ruling of 06-Aug, stated in two lines and implemented literally here:
//
//   persisted   category | value | secondary     never renamed, never migrated
//   presented   Axis     | Value | Series        Chapter 4 section 5.1.11
//
// So this module owns WORDS ONLY. Every mechanic - what is stored, where it is
// stored, and which roles have gone stale - stays in
// src/api/product-core/widget-role-binding.ts and is CALLED from here. There is
// no second persistence and no second stale detection, because a second
// implementation of a governance rule is what Constitution II.7.6 forbids.

import { staleRoles, type WidgetRole, type WidgetRoleBinding } from "@/api/product-core/widget-role-binding";

// The stored object is keyed, so the order an author reads is a presentation
// decision and belongs here rather than in the persistence module.
export const ROLE_ORDER: readonly WidgetRole[] = ["category", "value", "secondary"];

const ROLE_LABEL: Record<WidgetRole, string> = {
  category: "Axis",
  value: "Value",
  secondary: "Series",
};

export function roleLabel(role: WidgetRole): string {
  return ROLE_LABEL[role];
}

// Axis and Value are the two a chart needs; Series is genuinely optional, and
// the empty choice says which of those two situations the author is in.
export function rolePlaceholder(role: WidgetRole): string {
  return role === "secondary" ? "none" : "choose a column...";
}

/**
 * The engineer-facing sentence for a stale mapping. IT NAMES THE COLUMN, which
 * is the whole acceptance criterion of this task: "some roles are stale" tells
 * a plant engineer nothing he can act on, and section 5.2.8 forbids exactly
 * that kind of message. Detection is delegated to staleRoles; only the wording
 * is decided here.
 */
export function describeStaleBinding(
  binding: WidgetRoleBinding | null,
  columns: readonly string[],
): string {
  const stale = staleRoles(binding, columns);
  if (!binding || stale.length === 0) { return ""; }
  const named = stale.map((r) => roleLabel(r) + " (" + String(binding[r]) + ")").join(", ");
  return "The saved mapping points at columns this query no longer returns: " + named
    + ". Choose again. Nothing has been repointed for you, because a chart that"
    + " silently moves to another column is the failure this step exists to prevent.";
}

export const ROLE_STABLE_HINT =
  "Roles are stored by column name, so reordering the query keeps them."
  + " A column that disappears is reported here by name rather than replaced.";