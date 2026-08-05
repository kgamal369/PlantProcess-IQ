// PPIQ T-033. THE INTERFACE OPERATOR LIST EQUALS THE SERVER WHITELIST.
//
// T-033's validation asks for exactly this test. It does not compare the
// interface list against a second hand-written copy - it PARSES
// VisualMapperEndpoints.cs and compares against what BuildSafeSelect actually
// enforces. A copy of a copy proves nothing; this fails the build the moment
// either side drifts.

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { FILTER_OPERATORS, MATH_OPERATORS, UNARY_FILTER_OPERATORS } from "./operatorContract";

const ENDPOINTS = join(
  process.cwd(), "..", "..",
  "Backend", "PlantProcess.Api", "Endpoints", "Prep", "VisualMapperEndpoints.cs",
);

function serverArray(source: string, fieldName: string): string[] {
  // Matches:  private static readonly string[] FilterOps = { "=", "<>", ... };
  // across however many lines the declaration is wrapped over.
  const start = source.indexOf("string[] " + fieldName);
  if (start < 0) { throw new Error("server field not found: " + fieldName); }
  const open = source.indexOf("{", start);
  const close = source.indexOf("}", open);
  if (open < 0 || close < 0) { throw new Error("malformed declaration: " + fieldName); }

  const body = source.slice(open + 1, close);
  const out: string[] = [];
  const quoted = /"((?:[^"\\]|\\.)*)"/g;
  let m = quoted.exec(body);
  while (m !== null) {
    out.push(m[1]);
    m = quoted.exec(body);
  }
  return out;
}

describe("T-033: the interface operator lists equal the server whitelist", () => {
  const source = readFileSync(ENDPOINTS, "utf8");

  it("filter operators match BuildSafeSelect's FilterOps, in order", () => {
    expect(FILTER_OPERATORS.slice()).toEqual(serverArray(source, "FilterOps"));
  });

  it("derived-column operators match BuildSafeSelect's MathOps, in order", () => {
    expect(MATH_OPERATORS.slice()).toEqual(serverArray(source, "MathOps"));
  });

  it("the unary operators are the two the server emits without a bound value", () => {
    // THE NEEDLE IS THE WHOLE BRANCH, NOT A PER-OPERATOR PROBE. A first draft
    // tested source.includes('op is "' + o + '"') for each operator and matched
    // the symbol-passthrough line above it - `if (op is "=" or "<>" or ...)` -
    // reporting "=" as unary. A guard names the exact artifact it forbids or
    // requires, never a fragment that also appears elsewhere.
    expect(source).toContain('if (op is "IS NULL" or "IS NOT NULL")');
    expect(UNARY_FILTER_OPERATORS.slice()).toEqual(["IS NULL", "IS NOT NULL"]);

    // And every non-unary operator must reach the value requirement, so the
    // board is right to demand a value for all of them.
    expect(source).toContain("needs a value for operator");
  });

  it("every interface operator is one the server would accept", () => {
    const filterOps = serverArray(source, "FilterOps");
    for (const op of FILTER_OPERATORS) {
      expect(filterOps, "interface offers an operator the server refuses: " + op).toContain(op);
    }
    const mathOps = serverArray(source, "MathOps");
    for (const op of MATH_OPERATORS) {
      expect(mathOps, "interface offers an operator the server refuses: " + op).toContain(op);
    }
  });
});