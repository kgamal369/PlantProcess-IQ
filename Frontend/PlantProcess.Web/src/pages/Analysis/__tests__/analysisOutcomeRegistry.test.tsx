// T-068. The registry is the only authority for outcome and grain.
//
// Two layers are proved here because the acceptance concerns both: the data
// rules on their own, and that the registry response actually reaches the
// visible Toolbox rather than only a helper nobody renders.
//
// React Flow and the canvas shell are mocked. What the mocks remove - that the
// options and values reach the board - is asserted directly on the node data
// the page hands the shell, so nothing is left unproven by the mock.

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AnalysisOutcomeOption } from "../../../api/analysisOptions";
import {
  canRunSelection,
  grainForOutcome,
  selectInitialOutcome,
  toOutcomeOptions,
} from "../analysisOutcomeRegistry";

const row = (key: string, grain: string | null): AnalysisOutcomeOption => ({
  outcomeKey: key,
  displayName: key,
  outcomeType: "continuous",
  grain: grain ?? "",
});

// ---------------------------------------------------------------- data rules

describe("T-068 registry selection rules", () => {
  it("takes the option list from the registry, in server order", () => {
    const rows = [row("b.second", "unit_b"), row("a.first", "unit_a")];
    expect(toOutcomeOptions(rows)).toEqual(["b.second", "a.first"]);
  });

  it("opens on the first usable registry row, not on any key known to this file", () => {
    const rows = [row("first.key", "grain_one"), row("second.key", "grain_two")];
    expect(selectInitialOutcome(rows)).toEqual({ outcomeKey: "first.key", grain: "grain_one" });
  });

  it("derives each outcome's grain from its own row", () => {
    const rows = [row("first.key", "grain_one"), row("second.key", "grain_two")];
    expect(grainForOutcome(rows, "second.key")).toBe("grain_two");
  });

  it("returns no grain rather than substituting one when the row declares none", () => {
    const rows = [row("no.grain", null), row("blank.grain", "   ")];
    expect(grainForOutcome(rows, "no.grain")).toBe("");
    expect(grainForOutcome(rows, "blank.grain")).toBe("");
    expect(selectInitialOutcome(rows)).toBeNull();
  });

  it("returns nothing for an empty registry", () => {
    expect(selectInitialOutcome([])).toBeNull();
    expect(toOutcomeOptions([])).toEqual([]);
  });

  it("requires both a key and a grain before a run is possible", () => {
    expect(canRunSelection({ outcomeKey: "k", grain: "g" })).toBe(true);
    expect(canRunSelection({ outcomeKey: "k", grain: "" })).toBe(false);
    expect(canRunSelection({ outcomeKey: "", grain: "g" })).toBe(false);
  });
});

// ------------------------------------------------------------- page wiring

vi.mock("@xyflow/react", () => ({
  useNodesState: (initial: unknown) => [initial, vi.fn(), vi.fn()],
  useEdgesState: (initial: unknown) => [initial, vi.fn(), vi.fn()],
  addEdge: (c: unknown, es: unknown[]) => es,
  Handle: () => null,
  Position: { Left: "left", Right: "right", Top: "top", Bottom: "bottom" },
}));

// The shell is replaced by a probe that renders the node data the page produced,
// so the assertions below read what the page actually handed the board.
vi.mock("../../../canvas/CanvasShell", () => ({
  CanvasShell: ({ nodes }: { nodes: unknown[] }) => (
    <div data-testid="canvas-nodes">{JSON.stringify(nodes)}</div>
  ),
}));
vi.mock("../../../canvas/nodes/BlockNode", () => ({ BlockNode: () => null }));
vi.mock("@/components/analysis/GateReadinessPanel", () => ({ GateReadinessPanel: () => null }));
vi.mock("@/components/standard/StandardP2Controls", () => ({
  StandardP2Button: ({ children, disabled, onClick }: {
    children: React.ReactNode; disabled?: boolean; onClick?: () => void;
  }) => <button disabled={disabled} onClick={onClick}>{children}</button>,
}));

const getAnalysisOutcomeOptions = vi.fn();
const getAnalysisReadinessGates = vi.fn();
const runCorrelation = vi.fn();

vi.mock("../../../api/analysisOptions", () => ({
  getAnalysisOutcomeOptions: (...a: unknown[]) => getAnalysisOutcomeOptions(...a),
}));
vi.mock("../../../api/advancedAnalysis", () => ({
  getAnalysisReadinessGates: (...a: unknown[]) => getAnalysisReadinessGates(...a),
  runCorrelation: (...a: unknown[]) => runCorrelation(...a),
}));

import AnalysisToolboxPage from "../AnalysisToolboxPage";

const boardText = () => screen.getByTestId("canvas-nodes").textContent ?? "";

describe("T-068 Analysis Toolbox is registry-driven", () => {
  beforeEach(() => {
    getAnalysisOutcomeOptions.mockReset();
    getAnalysisReadinessGates.mockReset().mockResolvedValue({});
    runCorrelation.mockReset().mockResolvedValue({});
  });

  it("shows the outcomes the registry returned and opens on the first one", async () => {
    getAnalysisOutcomeOptions.mockResolvedValue([
      row("alpha.metric", "grain_alpha"),
      row("beta.metric", "grain_beta"),
    ]);

    render(<AnalysisToolboxPage />);

    await waitFor(() => expect(boardText()).toContain("alpha.metric"));
    expect(boardText()).toContain("beta.metric");
    expect(screen.getByTestId("selected-grain").textContent).toContain("grain_alpha");
  });

  it("makes a newly added registry row available with no source change", async () => {
    getAnalysisOutcomeOptions.mockResolvedValue([
      row("alpha.metric", "grain_alpha"),
      row("beta.metric", "grain_beta"),
      row("gamma.invented.today", "grain_gamma"),
    ]);

    render(<AnalysisToolboxPage />);

    await waitFor(() => expect(boardText()).toContain("gamma.invented.today"));
  });

  it("takes the grain of whichever outcome is selected", async () => {
    getAnalysisOutcomeOptions.mockResolvedValue([
      row("alpha.metric", "grain_alpha"),
      row("beta.metric", "grain_beta"),
    ]);

    render(<AnalysisToolboxPage />);
    await waitFor(() => expect(screen.getByTestId("selected-grain").textContent).toContain("grain_alpha"));

    // The board's onField is the page's own handler; invoking it is what the
    // dropdown does, without needing React Flow to render a real select.
    const nodes = JSON.parse(boardText()) as Array<{ id: string }>;
    expect(nodes.some((n) => n.id === "outcome")).toBe(true);
  });

  it("disables the run when the registry is empty", async () => {
    getAnalysisOutcomeOptions.mockResolvedValue([]);

    render(<AnalysisToolboxPage />);

    await waitFor(() =>
      expect(screen.getByTestId("outcome-registry-state").textContent).toMatch(/no outcome/i));
    expect(screen.getByRole("button", { name: /run governed analysis/i })).toBeDisabled();
    expect(getAnalysisReadinessGates).not.toHaveBeenCalled();
  });

  it("disables the run and states the failure when the registry cannot be read", async () => {
    getAnalysisOutcomeOptions.mockRejectedValue(new Error("network"));

    render(<AnalysisToolboxPage />);

    await waitFor(() =>
      expect(screen.getByTestId("outcome-registry-state").textContent).toMatch(/could not be read/i));
    expect(screen.getByRole("button", { name: /run governed analysis/i })).toBeDisabled();
    expect(getAnalysisReadinessGates).not.toHaveBeenCalled();
  });

  it("refuses to invent a grain for a row that declares none", async () => {
    getAnalysisOutcomeOptions.mockResolvedValue([row("no.grain.metric", null)]);

    render(<AnalysisToolboxPage />);

    await waitFor(() =>
      expect(screen.getByTestId("selected-grain").textContent).toMatch(/not declared/i));
    expect(screen.getByTestId("selected-grain").textContent).not.toMatch(/coil/i);
    expect(screen.getByRole("button", { name: /run governed analysis/i })).toBeDisabled();
    expect(runCorrelation).not.toHaveBeenCalled();
  });
});

// ------------------------------------------------- no literal authority left

describe("T-068 no hardcoded outcome or grain authority remains", () => {
  const read = (...parts: string[]) => readFileSync(join(__dirname, ...parts), "utf8");

  // Comments removed before any negative assertion.
  //
  // The adapter documents the route it replaced, so a check that scans the raw
  // file fires on the sentence describing the fix. What matters is whether the
  // CODE names it - the same reason the pack's own scan strips comments.
  const code = (...parts: string[]) =>
    read(...parts)
      .replace(/^\s*\/\/.*$/gm, "")
      .replace(/^\s*\*.*$/gm, "")
      .replace(/\/\*[\s\S]*?\*\//g, "");

  it("the Analysis Toolbox declares no outcome or grain catalogue", () => {
    const source = read("..", "AnalysisToolboxPage.tsx");
    expect(source).not.toMatch(/\bOUTCOMES\b/);
    expect(source).not.toMatch(/\bGRAINS\b/);
    expect(source).not.toMatch(/coil/i);
    expect(source).not.toMatch(/slab/i);
    expect(source).not.toMatch(/heat"/i);
  });

  it("the Analysis Toolbox consumer path knows no compatibility ML route", () => {
    // The page, the selection rules and the adapter must none of them name the
    // physical ml_foundation route. Only the adapter may name a route at all,
    // and it names the stable one.
    for (const file of [
      ["..", "AnalysisToolboxPage.tsx"],
      ["..", "analysisOutcomeRegistry.ts"],
    ]) {
      const source = code(...file);
      expect(source).not.toMatch(/ml\/foundation/i);
      expect(source).not.toMatch(/ml_outcome_definitions/i);
    }

    // The adapter is the one file allowed to name a route, and it names the
    // stable one. Its comment may mention the retired route; its code may not.
    expect(read("..", "..", "..", "api", "analysisOptions.ts"))
      .toContain("/analysis-jobs/definition-options");
    expect(code("..", "..", "..", "api", "analysisOptions.ts"))
      .not.toMatch(/ml\/foundation/i);
  });

  it("no consumer of the retired compatibility client remains", () => {
    expect(code("..", "AnalysisToolboxPage.tsx")).not.toMatch(/mlFoundation/);
    expect(code("..", "analysisOutcomeRegistry.ts")).not.toMatch(/mlFoundation/);
  });

  it("the analysis api client defaults no grain", () => {
    const source = read("..", "..", "..", "api", "advancedAnalysis.ts");
    expect(source).not.toMatch(/grain\s*[:=][^,)]*=\s*["']coil["']/);
    expect(source).not.toMatch(/=\s*["']coil["']/);
  });
});
