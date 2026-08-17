// T-050. WHICH BACKEND ROW IS THIS POINT?
//
// PR-050-01 returns rowPopulations in the SAME ORDER as rows, addressed by
// rowIndex. The drawer must therefore know which backend row a clicked point
// came from - and it must be the backend's index, not the point's position on
// screen.
//
// Those two are not the same number. A table slices to fifty, an extra-chart
// collapses a result, a chart may sort. Reading the visual position would name
// the wrong population while looking entirely correct, which is the worst
// failure this feature can have: a confident answer about the wrong thing.
//
// The index is therefore stamped onto the row OBJECT before any chart sees it.
// Recharts hands the datum itself to onClick, so the stamp travels with the row
// through sorting, slicing and projection. No chart has to cooperate, and no
// caller has to translate.
//
// Nothing here reconstructs a population. The stamp only says which backend row
// this is; PR-050-01's descriptor remains the sole authority on what that row
// represents.

/** Deliberately verbose and namespaced: it shares an object with a widget's own
 *  columns, and a column called "index" is not far-fetched. */
export const PPIQ_ROW_INDEX = "__ppiqSourceRowIndex" as const;

export type StampedRow = Record<string, unknown> & {
  [PPIQ_ROW_INDEX]?: number;
};

/** Stamps each row with its position in the backend result. Non-mutating: the
 *  result rows belong to the query cache and are shared. */
export function stampSourceRowIndices<T extends Record<string, unknown>>(rows: readonly T[]): (T & StampedRow)[] {
  return rows.map((row, index) => ({ ...row, [PPIQ_ROW_INDEX]: index }));
}

/** Reads the stamp back off a clicked datum. Returns null rather than a guess
 *  when the datum never carried one - an unstamped point must produce "no
 *  population" honestly, never population zero. */
export function sourceRowIndex(row: unknown): number | null {
  if (row === null || typeof row !== "object") return null;

  const value = (row as StampedRow)[PPIQ_ROW_INDEX];
  if (typeof value !== "number" || !Number.isInteger(value) || value < 0) return null;

  return value;
}

/** The descriptor for a clicked point, from PR-050-01's rowPopulations. Matched
 *  by rowIndex rather than by array position, because the contract addresses
 *  descriptors by rowIndex and an array position is an assumption. */
export function populationForRow<T extends { rowIndex: number }>(
  populations: readonly T[] | null | undefined,
  index: number | null,
): T | null {
  if (populations === null || populations === undefined || index === null) return null;
  return populations.find((population) => population.rowIndex === index) ?? null;
}
