// T-068. The registry is the authority for outcome and grain.
//
// Pure on purpose: every rule below is a data rule, and proving it should not
// require standing up React Flow. The page wiring is proved separately.
//
// There is no default flag on the options contract, so "initial selection" is
// the first row of the server's own deterministic ORDER BY outcome_group,
// outcome_key. That is registry-driven. Picking a key here would be the same
// Rule 1 defect the task exists to remove.

import type { AnalysisOutcomeOption } from "../../api/analysisOptions";

export interface OutcomeSelection {
  outcomeKey: string;
  grain: string;
}

/** A row is usable only if it carries both an outcome key and its own grain. */
export function isUsableOutcomeRow(row: AnalysisOutcomeOption | null | undefined): boolean {
  if (!row) return false;
  const key = (row.outcomeKey ?? "").trim();
  const grain = (row.grain ?? "").trim();
  return key.length > 0 && grain.length > 0;
}

/** Option keys, in the order the server returned them. No sorting, no filtering
 *  by any vocabulary this file knows about. */
export function toOutcomeOptions(rows: readonly AnalysisOutcomeOption[]): string[] {
  return rows
    .filter((row) => (row.outcomeKey ?? "").trim().length > 0)
    .map((row) => (row.outcomeKey ?? "").trim());
}

/**
 * The grain the registry declares for this key.
 *
 * Returns "" when the key is unknown or its row carries no grain. It never
 * substitutes a value: an absent grain is an absent grain, and the caller
 * disables the run rather than running against a guess.
 */
export function grainForOutcome(
  rows: readonly AnalysisOutcomeOption[],
  outcomeKey: string
): string {
  const match = rows.find((row) => (row.outcomeKey ?? "").trim() === outcomeKey.trim());
  return (match?.grain ?? "").trim();
}

/**
 * The opening selection, taken from the first row the server returned that
 * carries both fields. Null when the registry is empty, or when no row is
 * usable - both of which leave the page with nothing selected and the run
 * disabled.
 */
export function selectInitialOutcome(
  rows: readonly AnalysisOutcomeOption[]
): OutcomeSelection | null {
  const first = rows.find(isUsableOutcomeRow);
  if (!first) return null;
  return {
    outcomeKey: (first.outcomeKey ?? "").trim(),
    grain: (first.grain ?? "").trim(),
  };
}

/** A governed run needs a key and a grain. Nothing is inferred to reach it. */
export function canRunSelection(selection: {
  outcomeKey: string;
  grain: string;
}): boolean {
  return selection.outcomeKey.trim().length > 0 && selection.grain.trim().length > 0;
}
