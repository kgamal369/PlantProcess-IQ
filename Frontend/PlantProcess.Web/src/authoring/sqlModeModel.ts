// PPIQ T-036. THE SQL MODE MODEL.
//
// Chapter 4 section 5.2.12 governs the dual-mode contract. Three decisions in
// it are decisions rather than rendering, so they live here with no React and
// no DOM: whether SQL can go back to blocks, what the editor may offer as a
// completion, and what the Run Test panel says about a returned column.
//
// NOTHING HERE NAMES A TABLE OR A COLUMN. Every function takes the live
// catalogue the schema tree already reads.

import type { RunSqlResult, StagedDataset } from "@/api/canvasApi";

// ------------------------------------------------------- RECONSTRUCTABILITY

/**
 * The ONLY normalisation applied before comparing two statements: line endings
 * and outer whitespace.
 *
 * DELIBERATELY NOT: case folding, inner whitespace collapsing, comment
 * stripping, keyword ordering. Each of those would be a claim about what SQL
 * means, and this product does not have a parser to back such a claim. A
 * harmless reformat therefore reads as a divergence and asks the author to
 * confirm - which is the safe direction to be wrong in.
 */
export function normaliseSqlForComparison(sql: string | null | undefined): string {
  return (sql ?? "").replace(/\r\n/g, "\n").trim();
}

export type ReconstructVerdict = "reconstructable" | "diverged" | "no-origin";

/**
 * Can the block representation be restored without discarding anything?
 *
 * FAILS CLOSED. "reconstructable" is returned only when this product can PROVE
 * it: the authored statement is still, character for character after the
 * normalisation above, the statement the current graph compiled to. Anything
 * else - an edit, an unknown origin, an empty editor - is unproven, and
 * unproven means the author is asked.
 *
 * No SQL parser is built for this and none is implied. A statement that a
 * parser COULD show to be equivalent still returns "diverged" here, and that is
 * the correct answer for a product that cannot demonstrate the equivalence.
 */
export function reconstructVerdict(
  authoredSql: string | null | undefined, forkedSql: string | null | undefined,
): ReconstructVerdict {
  const origin = normaliseSqlForComparison(forkedSql);
  if (origin === "") { return "no-origin"; }
  return normaliseSqlForComparison(authoredSql) === origin ? "reconstructable" : "diverged";
}

export function isReconstructable(
  authoredSql: string | null | undefined, forkedSql: string | null | undefined,
): boolean {
  return reconstructVerdict(authoredSql, forkedSql) === "reconstructable";
}

/**
 * What the author is asked before the SQL is thrown away. Section 5.2.8 wants
 * the block named, the rule stated and the remedy given; this is the same
 * shape for a destructive choice.
 */
export function describeDiscardWarning(verdict: ReconstructVerdict): string {
  if (verdict === "reconstructable") { return ""; }
  const why = verdict === "no-origin"
    ? "This SQL was not compiled from the blocks on the board, so there is nothing to reconstruct it from."
    : "This SQL has been edited since it was compiled from the blocks, so it can no longer be shown as blocks.";
  return why + " Switching to Block mode will DISCARD the SQL and return to the block"
    + " representation. Cancel to stay in SQL mode and keep it.";
}

// ------------------------------------------------------------- COMPLETIONS

export type CompletionKind = "schema" | "table" | "column";

export interface SqlCompletion {
  /** What is inserted. */
  label: string;
  kind: CompletionKind;
  /** Where it came from, so two same-named columns are told apart. */
  detail: string;
}

/**
 * The word being completed: everything after the last character that cannot be
 * part of an identifier. A trailing dot yields an empty prefix qualified by
 * what precedes it, which is how "alpha." offers that table's columns.
 */
export function completionPrefix(text: string, caret: number): { qualifier: string; prefix: string } {
  const upto = text.slice(0, Math.max(0, Math.min(caret, text.length)));
  const match = /([A-Za-z0-9_]*)\.?([A-Za-z0-9_]*)$/.exec(upto);
  if (!match) { return { qualifier: "", prefix: "" }; }
  const hasDot = /[A-Za-z0-9_]*\.[A-Za-z0-9_]*$/.test(upto);
  if (hasDot) { return { qualifier: match[1] ?? "", prefix: match[2] ?? "" }; }
  return { qualifier: "", prefix: match[1] ?? "" };
}

/**
 * What the editor may offer, drawn ENTIRELY from the live catalogue.
 *
 * With a qualifier ("alpha.") only that table's columns are offered, because
 * that is what the author has already committed to. Without one, schemas,
 * tables and columns are all candidates and each column says which table it
 * belongs to - two tables in a join can carry the same column name, and an
 * unqualified list that hides that is worse than no list.
 */
export function completionsFor(
  catalogue: StagedDataset[], text: string, caret: number, limit = 20,
): SqlCompletion[] {
  const { qualifier, prefix } = completionPrefix(text, caret);
  const needle = prefix.toLowerCase();
  const out: SqlCompletion[] = [];

  const matches = (candidate: string) => candidate.toLowerCase().indexOf(needle) === 0;

  if (qualifier !== "") {
    for (const d of catalogue) {
      if (d.table.toLowerCase() !== qualifier.toLowerCase()) { continue; }
      for (const c of d.columns) {
        if (matches(c.name)) { out.push({ label: c.name, kind: "column", detail: d.table }); }
      }
    }
    return out.slice(0, limit);
  }

  const schemas: string[] = [];
  for (const d of catalogue) {
    const schema = d.source ?? "";
    if (schema !== "" && schemas.indexOf(schema) < 0) { schemas.push(schema); }
  }
  for (const s of schemas) {
    if (matches(s)) { out.push({ label: s, kind: "schema", detail: "schema" }); }
  }
  for (const d of catalogue) {
    if (matches(d.table)) { out.push({ label: d.table, kind: "table", detail: d.source ?? "" }); }
  }
  for (const d of catalogue) {
    for (const c of d.columns) {
      if (matches(c.name)) { out.push({ label: c.name, kind: "column", detail: d.table }); }
    }
  }
  return out.slice(0, limit);
}

// ------------------------------------------------------- RUN TEST COLUMNS

export interface ReturnedColumn {
  name: string;
  /** The DATABASE type the server measured, or the honest absence of one. */
  databaseType: string;
  sample: string;
}

/** What the panel shows when the server sent no type for a column. */
export const TYPE_NOT_REPORTED = "type not reported";

/** What the panel shows when nothing came back to sample. */
export const NO_SAMPLE = "no rows to sample";

function sampleOf(result: RunSqlResult, index: number): string {
  for (const row of result.rows) {
    const value = Array.isArray(row) ? row[index] : undefined;
    if (value === null || value === undefined) { continue; }
    const text = String(value);
    if (text === "") { continue; }
    return text.length > 40 ? text.slice(0, 40) + "..." : text;
  }
  return NO_SAMPLE;
}

/**
 * The returned column list with its types and one representative value each.
 *
 * THE TYPE IS NEVER INFERRED FROM THE SAMPLE. "3" is a text column as often as
 * an integer one, and a wrong type in front of an engineer deciding whether two
 * columns can be joined is worse than an admitted absence.
 */
export function describeReturnedColumns(result: RunSqlResult): ReturnedColumn[] {
  const details = result.columnDetails ?? [];
  return result.columns.map((name, index) => {
    let databaseType = TYPE_NOT_REPORTED;
    for (const d of details) {
      if (d.name === name) { databaseType = d.databaseType; break; }
    }
    return { name, databaseType, sample: sampleOf(result, index) };
  });
}