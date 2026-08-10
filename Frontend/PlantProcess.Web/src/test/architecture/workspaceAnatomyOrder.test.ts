// @vitest-environment node
// ============================================================
// T-043 slice 1. The D1 region order of Chapter 4 section 5.1.2.
//
// The guard reads JSX ELEMENT tokens ("<Name"), not bare identifiers, so an
// import line can never satisfy it, and it strips comments first so prose
// about a region can never stand in for the region. This project has paid for
// both lessons more than once.
// ============================================================
import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const PAGE = resolve(__dirname, "../../pages/Dashboard/InteractiveWorkspacePage.tsx");

function withoutComments(text: string): string {
  return text.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");
}

const SOURCE = withoutComments(readFileSync(PAGE, "utf8"));

function elementAt(name: string): number {
  return SOURCE.indexOf("<" + name);
}

describe("T-043 D1 workspace anatomy (Chapter 4 5.1.2)", () => {
  it("renders every region the anatomy names", () => {
    for (const name of [
      "SelectionBreadcrumb",
      "AssociativePanel",
      "DashboardFilterBar",
      "DashboardGridLayout",
      "DrilldownDrawer",
    ]) {
      expect(elementAt(name), name + " is not rendered by the workspace page").toBeGreaterThan(-1);
    }
  });

  it("puts the always-present selections bar above the associative strip", () => {
    expect(elementAt("SelectionBreadcrumb")).toBeLessThan(elementAt("AssociativePanel"));
  });

  it("puts the associative strip above the global filter bar", () => {
    expect(elementAt("AssociativePanel")).toBeLessThan(elementAt("DashboardFilterBar"));
  });

  it("puts the filter bar above the widget grid", () => {
    expect(elementAt("DashboardFilterBar")).toBeLessThan(elementAt("DashboardGridLayout"));
  });

  it("keeps the drill drawer outside the region sequence", () => {
    expect(SOURCE.indexOf("</DashboardGridLayout>")).toBeLessThan(elementAt("DrilldownDrawer"));
  });
});