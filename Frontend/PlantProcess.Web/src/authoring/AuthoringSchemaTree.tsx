// PPIQ T-032. Chapter 4 section 5.2.4 - the schema table bar, the INLINE-START
// region of the shell. Three levels, every level unfolding: schema, table,
// attribute with name, type and key marker.
//
// Section 5.2.4 also states WHY it is present in both modes: a SQL author
// needs the column names and types constantly, which is why the tree is
// unchanged when the palette disappears.
//
// Behaviour carried over from the S1 canvas UNCHANGED, because T-032 is a
// convergence and not a rewrite: click a schema or table row to unfold,
// double-click a table or click a column to put the table on the board.

import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import type { StagedDataset } from "@/api/canvasApi";

export interface AuthoringSchemaTreeProps {
  catalogue: StagedDataset[];
  openSchemas: Record<string, boolean>;
  openTables: Record<string, boolean>;
  onToggleSchema: (schema: string) => void;
  onToggleTable: (table: string) => void;
  onAddTable: (dataset: StagedDataset) => void;
  /** Stated when the catalogue is empty. Never a bare blank panel. */
  emptyMessage: string;
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
}: AuthoringSchemaTreeProps) {
  const schemaGroups = groupBySchema(catalogue);

  return (
    <div className="schema-tree" data-testid="canvas-schema-tree">
      {schemaGroups.length === 0 && (
        <div className="schema-tree__empty">{emptyMessage}</div>
      )}
      {schemaGroups.map((g) => {
        const schemaOpen = openSchemas[g.schema] === true;
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
              const tableOpen = openTables[d.table] === true;
              const keys = d.columns.filter((c) => c.isKeyCandidate).length;
              return (
                <div key={d.table}>
                  <StandardP2Button
                    variant="ghost"
                    type="button"
                    className="schema-tree__row schema-tree__row--table"
                    aria-expanded={tableOpen}
                    onClick={() => onToggleTable(d.table)}
                    onDoubleClick={() => onAddTable(d)}
                    title="Click to unfold columns, double-click to add to the board"
                  >
                    <span className={"schema-tree__chev" + (tableOpen ? " schema-tree__chev--open" : "")} />
                    <span className="schema-tree__name">{d.table}</span>
                    <span className="schema-tree__meta">
                      {d.columns.length} cols{keys > 0 ? " / " + keys + " key" : ""}
                    </span>
                  </StandardP2Button>

                  {tableOpen && d.columns.map((c) => (
                    <StandardP2Button
                      key={d.table + "." + c.name}
                      variant="ghost"
                      type="button"
                      className="schema-tree__col"
                      onClick={() => onAddTable(d)}
                      title={"Add " + d.table + " to the board"}
                    >
                      <span className="schema-tree__name">{c.name}</span>
                      {c.isKeyCandidate && <span className="schema-tree__key">key</span>}
                      <span className="schema-tree__coltype">{c.sqlType}</span>
                    </StandardP2Button>
                  ))}
                </div>
              );
            })}
          </div>
        );
      })}
    </div>
  );
}