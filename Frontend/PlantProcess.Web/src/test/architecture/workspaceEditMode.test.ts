// @vitest-environment node
// ============================================================
// T-043 slice 2. Edit mode is explicit (Chapter 4 section 5.1.7:
// "A view-mode user cannot accidentally drag a widget").
//
// This is a source guard rather than a mounted test on purpose, and the reason
// is worth writing down: DashboardGridLayout constructs a ResizeObserver, which
// this test environment does not provide, so a mounted proof would be a proof
// of the polyfill rather than of the product. The browser walk is where the
// behaviour itself is accepted.
//
// The guard reads comment-stripped source and counts the exact prop tokens, so
// prose about drag can never satisfy it and a second unbound occurrence cannot
// hide behind a bound one.
// ============================================================
import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const GRID = resolve(__dirname, "../../components/dashboard/DashboardGridLayout.tsx");
const PAGE = resolve(__dirname, "../../pages/Dashboard/InteractiveWorkspacePage.tsx");

function withoutComments(text: string): string {
  return text.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");
}

function occurrences(text: string, token: string): number {
  let count = 0;
  let index = text.indexOf(token);
  while (index >= 0) {
    count = count + 1;
    index = text.indexOf(token, index + 1);
  }
  return count;
}

const GRID_SOURCE = withoutComments(readFileSync(GRID, "utf8"));
const PAGE_SOURCE = withoutComments(readFileSync(PAGE, "utf8"));

describe("T-043 explicit edit mode", () => {
  it("binds drag to the edit flag and binds it nowhere else", () => {
    expect(occurrences(GRID_SOURCE, "isDraggable")).toBe(1);
    expect(GRID_SOURCE).toContain("isDraggable={isEditing}");
  });

  it("binds resize to the edit flag and binds it nowhere else", () => {
    expect(occurrences(GRID_SOURCE, "isResizable")).toBe(1);
    expect(GRID_SOURCE).toContain("isResizable={isEditing}");
  });

  it("defaults to view mode, so a surface that passes nothing cannot drag", () => {
    expect(GRID_SOURCE).toContain("isEditing = false");
  });

  it("exposes the mode on the grid shell so it can be read without dragging", () => {
    expect(GRID_SOURCE).toContain('data-edit-mode={isEditing ? "on" : "off"}');
  });

  it("has the workspace page drive the grid from its own edit state", () => {
    expect(PAGE_SOURCE).toContain("<DashboardGridLayout isEditing={isLayoutEditing}>");
  });

  it("has the workspace page render the header component, not an inline header", () => {
    expect(PAGE_SOURCE).toContain("<WorkspaceHeader");
    expect(occurrences(PAGE_SOURCE, 'className="ppiq-journey-actions"')).toBe(0);
  });
});