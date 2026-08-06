// PPIQ T-038. The legacy label adapter under test.
//
// The rule has four outcomes and exactly one of them rewrites anything. The
// last three tests are the ones that matter: no fuzzy match, no index match,
// and no resolution at all when two columns share a label.

import { describe, expect, it } from "vitest";
import {
  describeAmbiguousResolution, describeLegacyResolution,
  normaliseRoleBinding, resolveRoleToken, type ReturnedColumn,
} from "./roleBindingCompat";
import { EMPTY_ROLE_BINDING, type WidgetRoleBinding } from "@/api/product-core/widget-role-binding";

const COLUMNS: ReturnedColumn[] = [
  { code: "group_code", label: "Group code" },
  { code: "measured_value", label: "Measured value" },
];
const TWINS: ReturnedColumn[] = [
  { code: "left_total", label: "Total" },
  { code: "right_total", label: "Total" },
];

describe("T-038 resolving one persisted role token", () => {
  it("takes a code match as the current binding and rewrites nothing", () => {
    expect(resolveRoleToken("group_code", COLUMNS)).toEqual({ kind: "code", column: "group_code" });
  });

  it("resolves a token that matches exactly one label to that column's code", () => {
    expect(resolveRoleToken("Measured value", COLUMNS))
      .toEqual({ kind: "legacy-label", column: "measured_value" });
  });

  it("refuses to resolve a label two columns share", () => {
    expect(resolveRoleToken("Total", TWINS)).toEqual({ kind: "ambiguous", matches: 2 });
  });

  it("reports a token that matches nothing as missing", () => {
    expect(resolveRoleToken("gone_column", COLUMNS)).toEqual({ kind: "missing" });
  });

  it("treats an unbound role as nothing to resolve", () => {
    expect(resolveRoleToken(null, COLUMNS)).toBeNull();
    expect(resolveRoleToken("", COLUMNS)).toBeNull();
  });

  it("matches exactly, never approximately", () => {
    // Case, spacing and prefixes are all a different string, and a different
    // string is not this column. Anything looser would be inference.
    for (const near of ["group code", "GROUP_CODE", " group_code", "group_cod"]) {
      expect(resolveRoleToken(near, COLUMNS), near).toEqual({ kind: "missing" });
    }
  });

  it("never resolves by position", () => {
    // A token naming the second column's label must not land on the first.
    const outcome = resolveRoleToken("Measured value", COLUMNS);
    expect(outcome).toEqual({ kind: "legacy-label", column: "measured_value" });
    expect(outcome).not.toEqual({ kind: "legacy-label", column: "group_code" });
  });
});

describe("T-038 normalising a whole binding", () => {
  it("rewrites only the legacy roles and leaves the code-bound ones alone", () => {
    const binding: WidgetRoleBinding = {
      category: "group_code", value: "Measured value", secondary: null,
    };
    const out = normaliseRoleBinding(binding, COLUMNS);
    expect(out.binding).toEqual({
      category: "group_code", value: "measured_value", secondary: null,
    });
    expect(out.resolved).toEqual([{ role: "value", from: "Measured value", to: "measured_value" }]);
    expect(out.ambiguous).toEqual([]);
  });

  it("leaves an ambiguous token exactly as it was found", () => {
    const binding: WidgetRoleBinding = { category: "Total", value: null, secondary: null };
    const out = normaliseRoleBinding(binding, TWINS);
    expect(out.binding.category).toBe("Total");
    expect(out.resolved).toEqual([]);
    expect(out.ambiguous).toEqual([{ role: "category", token: "Total" }]);
  });

  it("changes nothing when there is nothing bound", () => {
    const out = normaliseRoleBinding(EMPTY_ROLE_BINDING, COLUMNS);
    expect(out.binding).toEqual(EMPTY_ROLE_BINDING);
    expect(out.resolved.length + out.ambiguous.length).toBe(0);
  });

  it("says what it did in the doctrine words, naming both the label and the column", () => {
    const out = normaliseRoleBinding(
      { category: "Group code", value: null, secondary: null }, COLUMNS);
    const sentence = describeLegacyResolution(out.resolved);
    expect(sentence).toContain("Axis");
    expect(sentence).toContain("Group code");
    expect(sentence).toContain("group_code");
    expect(sentence).toContain("Nothing was guessed");
    expect(describeLegacyResolution([])).toBe("");
  });

  it("says plainly that an ambiguous label was NOT resolved", () => {
    const out = normaliseRoleBinding({ category: "Total", value: null, secondary: null }, TWINS);
    const sentence = describeAmbiguousResolution(out.ambiguous);
    expect(sentence).toContain("Axis");
    expect(sentence).toContain("Total");
    expect(sentence).toContain("NOT been resolved");
    expect(describeAmbiguousResolution([])).toBe("");
  });
});