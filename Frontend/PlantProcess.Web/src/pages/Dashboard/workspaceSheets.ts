// ============================================================
// T-043 slice 3. THE SHEET DOCUMENT.
//
// Ruling of 10-Aug, option A: sheets live inside the existing page layout
// persistence, through the T-039 path, with no table, no migration and no
// /api/pages/{id}/sheets endpoint.
//
// OWNED M2a DEBT, recorded here where it will be read: Chapter 3 documents
// POST /api/pages/{id}/sheets and page_details holding sheets. This file is
// deliberately not that contract. It is replaced when the real definition_store
// and its sheet persistence arrive in M2a.
//
// THE DOCUMENT SHAPE, and why it is this one. layout_json today serialises as
// the breakpoint map itself, {"lg":[...],"md":[...],...}, with no wrapper. So
// the sheet document is two sibling keys beside those breakpoints:
//
//   { "lg": [...], ..., "sheets": [ { "id", "name" } ],
//                       "widgetSheets": { "<widgetId>": "<sheetId>" } }
//
// The widget-to-sheet assignment is one map rather than a sheetId on each grid
// item, because a grid item exists once PER BREAKPOINT: putting the sheetId
// there would store the same fact five times and let the five disagree.
//
// A document written before sheets existed carries neither key, and a page in
// that state genuinely has one sheet with every widget on it. Saying so is the
// honest description, not a fallback hiding a fault.
// ============================================================

export interface WorkspaceSheet {
  id: string;
  name: string;
}

export const DEFAULT_SHEET_ID = "default";
export const DEFAULT_SHEET_NAME = "Sheet 1";

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" ? (value as Record<string, unknown>) : {};
}

function parseDocument(layoutJson: string | null | undefined): Record<string, unknown> {
  if (!layoutJson) return {};
  try {
    return asRecord(JSON.parse(layoutJson));
  } catch {
    return {};
  }
}

function toSheet(value: unknown): WorkspaceSheet | null {
  const record = asRecord(value);
  const id = typeof record["id"] === "string" ? record["id"] : "";
  const name = typeof record["name"] === "string" ? record["name"] : "";
  if (!id || !name) return null;
  return { id, name };
}

function defaultSheets(): WorkspaceSheet[] {
  return [{ id: DEFAULT_SHEET_ID, name: DEFAULT_SHEET_NAME }];
}

/** Reads the sheet list out of a persisted layout_json document. */
export function readSheets(layoutJson: string | null | undefined): WorkspaceSheet[] {
  const raw = parseDocument(layoutJson)["sheets"];
  if (!Array.isArray(raw)) return defaultSheets();

  const sheets = raw.map(toSheet).filter((sheet): sheet is WorkspaceSheet => sheet !== null);
  return sheets.length > 0 ? sheets : defaultSheets();
}

/** Reads the widget-to-sheet assignments out of a persisted layout_json document. */
export function readWidgetSheetIds(
  layoutJson: string | null | undefined
): Record<string, string> {
  const raw = asRecord(parseDocument(layoutJson)["widgetSheets"]);
  const assignments: Record<string, string> = {};

  for (const widgetId of Object.keys(raw)) {
    const sheetId = raw[widgetId];
    if (widgetId && typeof sheetId === "string" && sheetId) {
      assignments[widgetId] = sheetId;
    }
  }

  return assignments;
}

/**
 * The sheet a widget belongs to.
 *
 * An unassigned widget, or one assigned to a sheet that no longer exists,
 * belongs to the first sheet. The second case matters: without it, deleting a
 * sheet would make its widgets unreachable on every sheet rather than visible
 * on one, and a widget that renders nowhere reads as data loss.
 */
export function sheetIdForWidget(
  assignments: Record<string, string>,
  sheets: WorkspaceSheet[],
  widgetId: string
): string {
  const known = sheets.length > 0 ? sheets : defaultSheets();
  const assigned = assignments[widgetId];
  if (assigned && known.some((sheet) => sheet.id === assigned)) return assigned;
  return known[0].id;
}

/** The two keys this product adds to the layout document, and nothing else. */
export function buildSheetDocument(
  sheets: WorkspaceSheet[],
  assignments: Record<string, string>
): Record<string, unknown> {
  return {
    sheets: (sheets.length > 0 ? sheets : defaultSheets()).map((sheet) => ({
      id: sheet.id,
      name: sheet.name,
    })),
    widgetSheets: { ...assignments },
  };
}

/**
 * The next sheet to create.
 *
 * The id is derived and stable rather than random, so the same document read
 * twice describes the same sheets, and a diff of layout_json is readable.
 */
export function nextSheet(sheets: WorkspaceSheet[]): WorkspaceSheet {
  const taken = new Set(sheets.map((sheet) => sheet.id));

  let ordinal = sheets.length + 1;
  let id = "sheet-" + ordinal;
  while (taken.has(id)) {
    ordinal = ordinal + 1;
    id = "sheet-" + ordinal;
  }

  return { id, name: "Sheet " + ordinal };
}