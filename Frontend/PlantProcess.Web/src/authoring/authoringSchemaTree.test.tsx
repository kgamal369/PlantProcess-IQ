// PPIQ T-034. The schema tree under test.
//
// The model is already proven headlessly, so this asserts the WIRING: that the
// rendered tree calls the model, shows what it returns, and puts the right
// payload on a drag. No fixture name here belongs to the emulated plant.

import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { StagedDataset } from "@/api/canvasApi";
import { AuthoringSchemaTree, type AuthoringSchemaTreeProps } from "./AuthoringSchemaTree";
import { SCHEMA_DRAG_MIME } from "./schemaTreeModel";

const catalogue: StagedDataset[] = [
  {
    table: "alpha", source: "staging_one", approxRowCount: 1234,
    columns: [
      { name: "alpha_key", sqlType: "text", isKeyCandidate: true, isNullable: false },
      { name: "widget_mass", sqlType: "numeric", isKeyCandidate: false, isNullable: true },
    ],
  },
  {
    table: "beta", source: "staging_one", approxRowCount: null,
    columns: [{ name: "widget_width", sqlType: "numeric", isKeyCandidate: false, isNullable: true }],
  },
];

function renderTree(overrides: Record<string, unknown> = {}) {
  const props = {
    catalogue,
    openSchemas: { staging_one: true },
    openTables: { alpha: true, beta: true },
    onToggleSchema: vi.fn(),
    onToggleTable: vi.fn(),
    onAddTable: vi.fn(),
    emptyMessage: "nothing staged",
    query: "",
    onQueryChange: vi.fn(),
    selection: {},
    onToggleColumn: vi.fn(),
    ...overrides,
  };
  // Cast through unknown, NOT to never. A value typed never cannot be
  // spread at all - TS2698 - and vitest never catches it because esbuild
  // strips the types without checking them.
  const typed = props as unknown as AuthoringSchemaTreeProps;
  return { props, ...render(<AuthoringSchemaTree {...typed} />) };
}

function fakeTransfer() {
  const store: Record<string, string> = {};
  return {
    store,
    dataTransfer: {
      setData: (mime: string, value: string) => { store[mime] = value; },
      getData: (mime: string) => store[mime] ?? "",
      effectAllowed: "none",
    },
  };
}

describe("what the tree shows", () => {
  it("puts nullability beside the type", () => {
    renderTree();
    // Scoped to the row. "numeric, nullable" is true of TWO columns in
    // this fixture, so a bare getByText matches more than one element and
    // fails on the duplicate rather than on the behaviour.
    expect(screen.getByTestId("schema-tree-column-alpha-alpha_key")).toHaveTextContent("text, not null");
    expect(screen.getByTestId("schema-tree-column-alpha-widget_mass")).toHaveTextContent("numeric, nullable");
    expect(screen.getByTestId("schema-tree-column-beta-widget_width")).toHaveTextContent("numeric, nullable");
  });

  it("shows an approximate row count, and says when there is none", () => {
    renderTree();
    expect(screen.getByTestId("schema-tree-table-alpha")).toHaveTextContent("~1,234 rows");
    expect(screen.getByTestId("schema-tree-table-beta")).toHaveTextContent("row count not analysed");
  });

  it("says nothing matched instead of showing an empty panel", () => {
    renderTree({ query: "no_such_thing" });
    expect(screen.getByTestId("canvas-schema-tree")).toHaveTextContent("Nothing in the schema list matches");
  });
});

describe("search", () => {
  it("narrows to the matching column and unfolds only the table holding it", () => {
    renderTree({ query: "width", openTables: {} });
    expect(screen.queryByTestId("schema-tree-table-alpha")).toBeNull();
    expect(screen.getByTestId("schema-tree-column-beta-widget_width")).toBeInTheDocument();
  });

  it("reports every keystroke to the surface that owns the query", async () => {
    const onQueryChange = vi.fn();
    renderTree({ onQueryChange });
    await userEvent.type(screen.getByLabelText("Search schema, table or column"), "w");
    expect(onQueryChange).toHaveBeenCalledWith("w");
  });
});

describe("multi-select", () => {
  it("marks a chosen column and reports a click to the surface", async () => {
    const onToggleColumn = vi.fn();
    renderTree({ onToggleColumn, selection: { alpha: ["widget_mass"] } });
    expect(screen.getByTestId("schema-tree-column-alpha-widget_mass")).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByTestId("schema-tree-column-alpha-alpha_key")).toHaveAttribute("aria-pressed", "false");
    await userEvent.click(screen.getByTestId("schema-tree-column-alpha-alpha_key"));
    expect(onToggleColumn).toHaveBeenCalledWith("alpha", "alpha_key");
  });

  it("states the selection, including that a drag carries one table", () => {
    renderTree({ selection: { alpha: ["alpha_key"], beta: ["widget_width"] } });
    expect(screen.getByTestId("schema-tree-selection")).toHaveTextContent("across 2 tables");
  });
});

describe("drag", () => {
  it("a table row with nothing picked carries the whole table", () => {
    renderTree();
    const t = fakeTransfer();
    fireEvent.dragStart(screen.getByTestId("schema-tree-table-alpha"), t);
    expect(JSON.parse(t.store[SCHEMA_DRAG_MIME])).toEqual({ kind: "table", table: "alpha" });
  });

  it("a table row with picked columns carries them as selection metadata", () => {
    renderTree({ selection: { alpha: ["alpha_key", "widget_mass"] } });
    const t = fakeTransfer();
    fireEvent.dragStart(screen.getByTestId("schema-tree-table-alpha"), t);
    expect(JSON.parse(t.store[SCHEMA_DRAG_MIME]))
      .toEqual({ kind: "columns", table: "alpha", columns: ["alpha_key", "widget_mass"] });
  });

  it("an unpicked attribute carries only itself", () => {
    renderTree({ selection: { alpha: ["alpha_key"] } });
    const t = fakeTransfer();
    fireEvent.dragStart(screen.getByTestId("schema-tree-column-alpha-widget_mass"), t);
    expect(JSON.parse(t.store[SCHEMA_DRAG_MIME]))
      .toEqual({ kind: "columns", table: "alpha", columns: ["widget_mass"] });
  });

  it("a picked attribute carries the whole selection of its table", () => {
    renderTree({ selection: { alpha: ["alpha_key", "widget_mass"] } });
    const t = fakeTransfer();
    fireEvent.dragStart(screen.getByTestId("schema-tree-column-alpha-alpha_key"), t);
    expect(JSON.parse(t.store[SCHEMA_DRAG_MIME]))
      .toEqual({ kind: "columns", table: "alpha", columns: ["alpha_key", "widget_mass"] });
  });

  it("a double-click still puts the table on the board, with what was picked", async () => {
    const onAddTable = vi.fn();
    renderTree({ onAddTable, selection: { alpha: ["alpha_key"] } });
    await userEvent.dblClick(screen.getByTestId("schema-tree-table-alpha"));
    expect(onAddTable).toHaveBeenCalledWith(catalogue[0], ["alpha_key"]);
  });
});
