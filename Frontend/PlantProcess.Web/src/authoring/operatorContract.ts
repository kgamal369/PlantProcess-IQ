// PPIQ T-033. THE OPERATOR CONTRACT.
//
// Chapter 4 section 5.2.5 puts Filter and Derived Column on the board, and the
// T-033 contract requires that "the operator lists in the interface must stay
// byte-identical to the whitelist BuildSafeSelect enforces, so an illegal
// state is unreachable rather than rejected afterwards".
//
// This module is the interface half of that contract. The server half is
// Backend/PlantProcess.Api/Endpoints/Prep/VisualMapperEndpoints.cs, which
// declares FilterOps at line 180 and MathOps at line 182 and refuses anything
// outside them with a named sentence.
//
// THE TWO HALVES ARE NOT KEPT IN STEP BY DISCIPLINE. operatorContract.test.ts
// PARSES THE C# FILE and fails the build if either list drifts by a single
// character. Adding an operator on one side without the other cannot reach a
// green suite. That is the only reason it is safe to write the list twice.

export type FilterOperator =
  | "=" | "<>" | ">" | ">=" | "<" | "<="
  | "LIKE" | "NOT LIKE" | "IS NULL" | "IS NOT NULL";

export type MathOperator = "+" | "-" | "*" | "/";

/** Byte-identical to FilterOps in VisualMapperEndpoints.cs. Order included. */
export const FILTER_OPERATORS: readonly FilterOperator[] = [
  "=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IS NULL", "IS NOT NULL",
];

/** Byte-identical to MathOps in VisualMapperEndpoints.cs. Order included. */
export const MATH_OPERATORS: readonly MathOperator[] = ["+", "-", "*", "/"];

/**
 * The two operators the server treats as unary: it emits the predicate with no
 * bound parameter, and refuses the rest with "needs a value for operator".
 * The board uses this to hide the value field rather than to let an author
 * submit something the server will reject.
 */
export const UNARY_FILTER_OPERATORS: readonly FilterOperator[] = ["IS NULL", "IS NOT NULL"];

export function isUnaryFilterOperator(op: string): boolean {
  return UNARY_FILTER_OPERATORS.indexOf(op as FilterOperator) >= 0;
}

/**
 * A field on a dataset, carrying the lineage the compatibility serialisation
 * needs. Section ruling of 04-Aug: every field a dataset exposes retains its
 * origin table, origin column, data type and display identity, so a Filter
 * downstream of a Join still resolves to a single table without guessing.
 *
 * A field whose originTable is empty has NO RESOLVABLE LINEAGE. The block that
 * holds it is invalid and Run is refused - the table is never inferred.
 */
export interface FieldLineage {
  /** The table the value actually comes from. Empty means unresolvable. */
  originTable: string;
  /** The column on that table. */
  originColumn: string;
  /** The SQL type, carried so port typing survives the join. */
  sqlType: string;
  /** What the author sees, qualified: "hsm_coils.width_mm". */
  displayName: string;
  isKeyCandidate?: boolean;
}

export function fieldDisplayName(originTable: string, originColumn: string): string {
  return originTable + "." + originColumn;
}

/** True when the field can be serialised to a FilterSpec or DerivedSpec. */
export function hasResolvableLineage(f: FieldLineage | null | undefined): boolean {
  return Boolean(f && f.originTable && f.originColumn);
}