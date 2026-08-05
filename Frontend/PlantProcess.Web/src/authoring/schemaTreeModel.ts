// PPIQ T-034. THE SCHEMA TREE MODEL.
//
// Chapter 4 section 5.2.4 makes the tree the INLINE-START region of both modes,
// because a SQL author needs column names and types constantly. T-034 asks it
// for search, multi-select, drag of a table AND of a single attribute, the
// column type AND its nullability, and an approximate row count per table.
//
// The decisions behind all of that live here, with no React and no DOM, so each
// one is asserted once as a fact rather than fifteen times through a rendered
// tree.
//
// NOTHING IN THIS FILE NAMES A TABLE OR A COLUMN. The task text forbids it and
// its validation greps for it. Every function takes the catalogue as it arrives
// from the registry-driven endpoint.

import type { StagedColumn, StagedDataset } from "@/api/canvasApi";

// ---------------------------------------------------------------- WORDING

/**
 * What the tree says about a column's type, including whether it may be empty.
 *
 * Nullability is OPTIONAL on the wire, because a client built before T-034 is
 * still a valid client. When it is absent the type is shown alone - the tree
 * never invents "not null", which would be a claim about the customer's data
 * that nothing in the response supports.
 */
export function describeColumnType(column: StagedColumn): string {
  if (column.isNullable === undefined) { return column.sqlType; }
  return column.sqlType + (column.isNullable ? ", nullable" : ", not null");
}

/**
 * What the tree says about how big a table is.
 *
 * THREE STATES, and the middle one is the point. Absent means an older server
 * that never sent the field. NULL means the database has never analysed the
 * table, which is a fact about the catalogue and NOT a claim that the table is
 * empty. A number is the planner's estimate and is shown as approximate,
 * because it is.
 */
export function describeRowCount(approxRowCount: number | null | undefined): string {
  if (approxRowCount === undefined) { return ""; }
  if (approxRowCount === null) { return "row count not analysed"; }
  return "~" + approxRowCount.toLocaleString("en-US") + " rows";
}

// ---------------------------------------------------------------- SEARCH

export interface SchemaSearchResult {
  /** The catalogue narrowed to what matches. Column matches narrow the columns. */
  tables: StagedDataset[];
  /** Tables the tree should unfold, because the match is INSIDE them. */
  openTables: string[];
  /** Schemas holding any surviving table. */
  openSchemas: string[];
  /** False when there is no query, in which case nothing is narrowed or forced open. */
  active: boolean;
}

function has(haystack: string, needle: string): boolean {
  return haystack.toLowerCase().indexOf(needle) >= 0;
}

/**
 * Search across schema, table and column, in that order of breadth.
 *
 *   schema match   the whole schema survives, tables unchanged
 *   table match    that table survives with ALL its columns
 *   column match   that table survives with ONLY its matching columns, and the
 *                  table is opened, because the thing that matched is inside it
 *
 * The last rule is what "only matching tables expand" means: a table whose NAME
 * matched is not forced open, since nothing inside it is what the author was
 * looking for.
 */
export function searchCatalogue(catalogue: StagedDataset[], query: string): SchemaSearchResult {
  const q = (query ?? "").trim().toLowerCase();
  if (q === "") {
    return { tables: catalogue.slice(), openTables: [], openSchemas: [], active: false };
  }

  const tables: StagedDataset[] = [];
  const openTables: string[] = [];
  const openSchemas: string[] = [];

  for (const dataset of catalogue) {
    const schemaHit = has(dataset.source ?? "", q);
    const tableHit = has(dataset.table, q);
    const columnHits = dataset.columns.filter((c) => has(c.name, q));

    if (schemaHit || tableHit) {
      tables.push(dataset);
    } else if (columnHits.length > 0) {
      tables.push({ ...dataset, columns: columnHits });
    } else {
      continue;
    }

    if (columnHits.length > 0 && !tableHit && !schemaHit) {
      openTables.push(dataset.table);
    }
    const schema = dataset.source ?? "";
    if (openSchemas.indexOf(schema) < 0) { openSchemas.push(schema); }
  }

  return { tables, openTables, openSchemas, active: true };
}

// ---------------------------------------------------------------- SELECTION

/** Chosen columns, keyed by table. A table with none is absent, never empty. */
export type ColumnSelection = Record<string, string[]>;

export function isSelected(selection: ColumnSelection, table: string, column: string): boolean {
  const chosen = selection[table];
  return Array.isArray(chosen) && chosen.indexOf(column) >= 0;
}

/**
 * Add or remove one column. Returns a NEW selection; the argument is never
 * mutated, so a caller holding the previous value still holds the previous
 * value.
 */
export function toggleColumn(selection: ColumnSelection, table: string, column: string): ColumnSelection {
  const next: ColumnSelection = { ...selection };
  const chosen = Array.isArray(next[table]) ? next[table].slice() : [];
  const at = chosen.indexOf(column);
  if (at >= 0) {
    chosen.splice(at, 1);
  } else {
    chosen.push(column);
  }
  if (chosen.length === 0) {
    delete next[table];
  } else {
    next[table] = chosen;
  }
  return next;
}

export function selectedColumnsOf(selection: ColumnSelection, table: string): string[] {
  const chosen = selection[table];
  return Array.isArray(chosen) ? chosen.slice() : [];
}

export function selectionCount(selection: ColumnSelection): number {
  let total = 0;
  for (const table of Object.keys(selection)) { total = total + selection[table].length; }
  return total;
}

export function selectionTables(selection: ColumnSelection): string[] {
  return Object.keys(selection).sort();
}

/**
 * What the tree says about the current selection. A selection spanning more
 * than one table is stated as such rather than silently summed, because a drag
 * carries ONE table's columns and the author needs to know that before they
 * drag.
 */
export function describeSelection(selection: ColumnSelection): string {
  const columns = selectionCount(selection);
  if (columns === 0) { return ""; }
  const tables = selectionTables(selection);
  const columnWord = columns === 1 ? "column" : "columns";
  if (tables.length === 1) {
    return columns + " " + columnWord + " selected in " + tables[0];
  }
  return columns + " " + columnWord + " selected across " + tables.length + " tables"
    + " - a drag carries one table at a time";
}

// ---------------------------------------------------------------- DRAG

/**
 * One MIME type, two payloads, one encoder and one decoder. The tree and the
 * board cannot disagree about what was dropped, because neither of them writes
 * the format.
 */
export const SCHEMA_DRAG_MIME = "application/x-ppiq-schema";

export type SchemaDragPayload =
  | { kind: "table"; table: string }
  | { kind: "columns"; table: string; columns: string[] };

export function encodeSchemaDrag(payload: SchemaDragPayload): string {
  return JSON.stringify(payload);
}

/**
 * FAILS CLOSED. A drop carrying anything this did not write returns null, and
 * the board refuses it with a sentence rather than half-reading it. Browsers
 * hand over whatever the drag source put on the clipboard, including drags
 * that started in another application entirely.
 */
export function decodeSchemaDrag(raw: string | null | undefined): SchemaDragPayload | null {
  if (!raw) { return null; }
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return null;
  }
  if (typeof parsed !== "object" || parsed === null) { return null; }
  const candidate = parsed as { kind?: unknown; table?: unknown; columns?: unknown };
  if (typeof candidate.table !== "string" || candidate.table === "") { return null; }

  if (candidate.kind === "table") {
    return { kind: "table", table: candidate.table };
  }
  if (candidate.kind === "columns") {
    if (!Array.isArray(candidate.columns)) { return null; }
    const columns = candidate.columns.filter((c): c is string => typeof c === "string" && c !== "");
    if (columns.length === 0) { return null; }
    return { kind: "columns", table: candidate.table, columns };
  }
  return null;
}

/**
 * The dataset a dropped payload refers to, or null when the board is being
 * handed a table that is not in the catalogue any more. Never invented.
 */
export function datasetForDrop(
  catalogue: StagedDataset[], payload: SchemaDragPayload,
): StagedDataset | null {
  for (const dataset of catalogue) {
    if (dataset.table === payload.table) { return dataset; }
  }
  return null;
}