// PPIQ T-032 / T-034. Chapter 4 section 5.2.4 - the schema table bar, the
// INLINE-START region of the shell. Three levels, every level unfolding:
// schema, table, attribute with name, type and key marker.
//
// Section 5.2.4 also states WHY it is present in both modes: a SQL author needs
// the column names and types constantly, which is why the tree is unchanged
// when the palette disappears.
//
// T-034 ADDS: search across schema, table and column; multi-select of columns;
// drag of a whole table AND of a single attribute; the column's nullability
// beside its type; and an approximate row count per table.
//
// EVERY DECISION IS IN schemaTreeModel. This file renders and reports; it
// decides nothing, so what "only matching tables expand" means is asserted once
// as a fact about a function rather than fifteen times through the DOM.
//
// NOTHING HERE NAMES A TABLE OR A COLUMN. The task text forbids it and its
// validation greps for it.

import { StandardP2Button, StandardP2Input } from "@/components/standard/StandardP2Controls";
import type { StagedDataset } from "@/api/canvasApi";
import {
  SCHEMA_DRAG_MIME, describeColumnType, describeRowCount, describeSelection,
  encodeSchemaDrag, isSelected, searchCatalogue, selectedColumnsOf,
  type ColumnSelection,
} from "./schemaTreeModel";

export interface AuthoringSchemaTreeProps {
  catalogue: StagedDataset[];
  openSchemas: Record<string, boolean>;
  openTables: Record<string, boolean>;
  onToggleSchema: (schema: string) => void;
  onToggleTable: (table: string) => void;
  onAddTable: (dataset: StagedDataset, selectedColumns?: string[]) => void;
  /** Stated when the catalogue is empty. Never a bare blank panel. */
  emptyMessage: string;
  /**
   * T-034. All optional, so a surface that wants the T-032 tree still gets
   * exactly the T-032 tree and nothing on it does nothing.
   */
  query?: string;
  onQueryChange?: (query: string) => void;
  selection?: ColumnSelection;
  onToggleColumn?: (table: string, column: string) => void;
}

interface SchemaGroup {
  schema: string;
  tables: StagedDataset[];
}

export function groupBySchema(catalogue: StagedDataset[]): SchemaGroup[] {
  const groups: Record<string, StagedDataset[]> = {};
  for (const d of catalogue) {
    const key = d.source || "unknown";
    if (!groups[key]) { groups[key] = []; }
    groups[key].push(d);
  }
  return Object.keys(groups).sort().map((k) => ({
    schema: k,
    tables: groups[k].slice().sort((a, b) => a.table.localeCompare(b.table)),
  }));
}

export function AuthoringSchemaTree({
  catalogue, openSchemas, openTables, onToggleSchema, onToggleTable, onAddTable, emptyMessage,
  query, onQueryChange, selection, onToggleColumn,
}: AuthoringSchemaTreeProps) {
  const currentSelection: ColumnSelection = selection ?? {};
  const found = searchCatalogue(catalogue, query ?? "");
  const schemaGroups = groupBySchema(found.tables);
  const selectionNote = describeSelection(currentSelection);

  // A search result opens what it found. The author's own unfolding is kept, so
  // clearing the box returns the tree to the shape they left it in.
  const schemaOpenNow = (schema: string) =>
    openSchemas[schema] === true || found.openSchemas.indexOf(schema) >= 0;
  const tableOpenNow = (table: string) =>
    openTables[table] === true || found.openTables.indexOf(table) >= 0;

  return (
    <div className="schema-tree" data-testid="canvas-schema-tree">
      {onQueryChange && (
        <StandardP2Input
          className="schema-tree__search"
          aria-label="Search schema, table or column"
          placeholder="Search schema, table or column"
          value={query ?? ""}
          onChange={(e) => onQueryChange(e.target.value)}
        />
      )}

      {selectionNote !== "" && (
        <div className="schema-tree__selection" data-testid="schema-tree-selection">{selectionNote}</div>
      )}

      {schemaGroups.length === 0 && (
        <div className="schema-tree__empty">
          {found.active ? "Nothing in the schema list matches that." : emptyMessage}
        </div>
      )}

      {schemaGroups.map((g) => {
        const schemaOpen = schemaOpenNow(g.schema);
        return (
          <div key={g.schema}>
            <StandardP2Button
              variant="ghost"
              type="button"
              className="schema-tree__row schema-tree__row--schema"
              aria-expanded={schemaOpen}
              onClick={() => onToggleSchema(g.schema)}
            >
              <span className={"schema-tree__chev" + (schemaOpen ? " schema-tree__chev--open" : "")} />
              <span className="schema-tree__name">{g.schema}</span>
              <span className="schema-tree__meta">{g.tables.length} tables</span>
            </StandardP2Button>

            {schemaOpen && g.tables.map((d) => {
              const tableOpen = tableOpenNow(d.table);
              const keys = d.columns.filter((c) => c.isKeyCandidate).length;
              const rows = describeRowCount(d.approxRowCount);
              const picked = selectedColumnsOf(currentSelection, d.table);
              return (
                <div key={d.table}>
                  <StandardP2Button
                    variant="ghost"
                    type="button"
                    className="schema-tree__row schema-tree__row--table"
                    aria-expanded={tableOpen}
                    draggable
                    data-testid={"schema-tree-table-" + d.table}
                    onDragStart={(e) => {
                      // DRAGGING THE TABLE ROW CARRIES THE TABLE. If columns of
                      // this table are picked, they travel with it as authoring
                      // metadata - they do not become a projection.
                      e.dataTransfer.setData(
                        SCHEMA_DRAG_MIME,
                        encodeSchemaDrag(picked.length > 0
                          ? { kind: "columns", table: d.table, columns: picked }
                          : { kind: "table", table: d.table }),
                      );
                      e.dataTransfer.effectAllowed = "copy";
                    }}
                    onClick={() => onToggleTable(d.table)}
                    onDoubleClick={() => onAddTable(d, picked)}
                    title="Click to unfold columns, drag or double-click to put it on the board"
                  >
                    <span className={"schema-tree__chev" + (tableOpen ? " schema-tree__chev--open" : "")} />
                    <span className="schema-tree__name">{d.table}</span>
                    <span className="schema-tree__meta">
                      {d.columns.length} cols{keys > 0 ? " / " + keys + " key" : ""}
                      {rows !== "" ? " / " + rows : ""}
                    </span>
                  </StandardP2Button>

                  {tableOpen && d.columns.map((c) => {
                    const chosen = isSelected(currentSelection, d.table, c.name);
                    return (
                      <StandardP2Button
                        key={d.table + "." + c.name}
                        variant={chosen ? "primary" : "ghost"}
                        type="button"
                        className={"schema-tree__col" + (chosen ? " schema-tree__col--chosen" : "")}
                        aria-pressed={onToggleColumn ? chosen : undefined}
                        draggable
                        data-testid={"schema-tree-column-" + d.table + "-" + c.name}
                        onDragStart={(e) => {
                          // DRAGGING ONE ATTRIBUTE carries the whole current
                          // selection when this column is part of it, and just
                          // this column when it is not - which is what an author
                          // means by dragging a row they did not tick.
                          const columns = chosen && picked.length > 0 ? picked : [c.name];
                          e.dataTransfer.setData(
                            SCHEMA_DRAG_MIME,
                            encodeSchemaDrag({ kind: "columns", table: d.table, columns }),
                          );
                          e.dataTransfer.effectAllowed = "copy";
                        }}
                        onClick={() => {
                          // T-034 replaces the T-032 behaviour where clicking a
                          // column added its whole table. A column row is now a
                          // selection control; the table row and the drag both
                          // still put the table on the board.
                          if (onToggleColumn) { onToggleColumn(d.table, c.name); } else { onAddTable(d); }
                        }}
                        title={onToggleColumn ? "Click to select, drag onto the board" : "Add to the board"}
                      >
                        <span className="schema-tree__name">{c.name}</span>
                        {c.isKeyCandidate && <span className="schema-tree__key">key</span>}
                        <span className="schema-tree__coltype">{describeColumnType(c)}</span>
                      </StandardP2Button>
                    );
                  })}
                </div>
              );
            })}
          </div>
        );
      })}
    </div>
  );
}
