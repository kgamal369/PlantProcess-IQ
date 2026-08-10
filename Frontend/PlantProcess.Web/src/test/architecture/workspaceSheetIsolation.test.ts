// @vitest-environment node
// ============================================================
// T-043 slice 3. What a mounted test cannot see: that the sheet path never
// touches selections, and that the sheet document travels on the T-039 path
// rather than on a second one someone added quietly.
//
// Comment-stripped, and matched on code tokens, so prose about selections can
// never satisfy a rule about selections.
// ============================================================
import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const PAGE = resolve(__dirname, "../../pages/Dashboard/InteractiveWorkspacePage.tsx");
const HOOK = resolve(__dirname, "../../hooks/useDashboardLayoutPersistence.ts");
const SHEETS = resolve(__dirname, "../../pages/Dashboard/workspaceSheets.ts");

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

const PAGE_SOURCE = withoutComments(readFileSync(PAGE, "utf8"));
const HOOK_SOURCE = withoutComments(readFileSync(HOOK, "utf8"));
const SHEETS_SOURCE = withoutComments(readFileSync(SHEETS, "utf8"));

describe("T-043 a sheet change is not a selection change", () => {
  it("changes nothing but the active sheet when the navigator moves", () => {
    expect(PAGE_SOURCE).toContain("onSheetChange={setActiveSheetId}");
  });

  it("never clears selections anywhere on the workspace page", () => {
    expect(occurrences(PAGE_SOURCE, "clearSelections")).toBe(0);
    expect(occurrences(PAGE_SOURCE, "undoSelection")).toBe(0);
    expect(occurrences(PAGE_SOURCE, "removeSelection")).toBe(0);
  });

  it("renders the active sheet's widgets and not the whole definition", () => {
    expect(PAGE_SOURCE).toContain("sheetIdForWidget(");
    expect(PAGE_SOURCE).toContain("{visibleWidgets.map((widget) => (");
    expect(occurrences(PAGE_SOURCE, "{dashboard.widgets.map((widget) => (")).toBe(0);
  });
});

describe("T-043 sheets travel on the T-039 path and nowhere else", () => {
  it("the layout persistence hook carries the sheet document both ways", () => {
    expect(HOOK_SOURCE).toContain("onLayoutJsonLoaded");
    expect(HOOK_SOURCE).toContain("buildExtraDocument");
  });

  it("no sheets endpoint was introduced", () => {
    expect(occurrences(PAGE_SOURCE, "/sheets")).toBe(0);
    expect(occurrences(HOOK_SOURCE, "/sheets")).toBe(0);
    expect(occurrences(SHEETS_SOURCE, "fetch(")).toBe(0);
    expect(occurrences(SHEETS_SOURCE, "Api.")).toBe(0);
  });

  it("the Chapter 3 divergence stays recorded where it will be read", () => {
    expect(readFileSync(SHEETS, "utf8")).toContain("OWNED M2a DEBT");
  });
});