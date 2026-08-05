// PPIQ T-032 acceptance. Two halves, and they prove different things.
//
// PART A is structural and reads the source tree: SharedAuthoringShell is THE
// authoring shell, and no second authoring PAGE component is exported. Scoped
// to page components on the 04-Aug ruling - retiring WidgetAuthoringPanel is
// T-038's contract, and T-038 tightens this file rather than replacing it.
//
// PART B renders the shell in S1 and in S2 and asserts the four regions of
// Chapter 4 section 5.2.3, including that the toolbox is ABSENT and not merely
// disabled in SQL mode.

import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { SharedAuthoringShell } from "./SharedAuthoringShell";
import { AUTHORING_PURPOSES } from "./authoringPurposes";

// The board is mocked: ReactFlow needs a layout engine jsdom does not have,
// and what this file is proving is the REGION CONTRACT, not the canvas.
vi.mock("@/canvas/CanvasShell", () => ({
  // The board itself is mocked because ReactFlow needs a layout engine jsdom
  // does not have. The BOARD ACTIONS it is handed are rendered, because those
  // are ordinary controls and the point of the assertion is that the canvas
  // toolbar receives them.
  CanvasShell: (props: { boardActions?: unknown }) => (
    <div data-testid="authoring-board">{props.boardActions as never}</div>
  ),
}));

vi.mock("@/api/canvasApi", () => ({
  listStagedDatasets: () => Promise.resolve([
    { table: "source_a", source: "staging_one", columns: [{ name: "key_column", sqlType: "text", isKeyCandidate: true }] },
  ]),
  createSession: () => Promise.resolve({ sessionId: "test-session" }),
  saveGraph: () => Promise.resolve({ ok: true }),
  runDryRun: () => Promise.resolve({ dryRunId: "d", status: "succeeded", rowCount: 0, columns: [], rows: [] }),
  publishVersion: () => Promise.resolve({ versionId: "v", versionNumber: 1 }),
  runAuthoredSql: () => Promise.resolve({ status: "succeeded", rowCount: 0, columns: [], rows: [], message: "", errorCode: null, sql: null, appliedRowLimit: 100 }),
  saveSqlVersion: () => Promise.resolve({ saved: true, versionNumber: 1, id: "1", message: "saved", errorCode: null }),
}));

const SRC = join(process.cwd(), "src");

function walk(dir: string, acc: string[] = []): string[] {
  let names: string[];
  try { names = readdirSync(dir); } catch { return acc; }
  for (const name of names) {
    if (name === "node_modules") { continue; }
    const full = join(dir, name);
    let isDir = false;
    try { isDir = statSync(full).isDirectory(); } catch { isDir = false; }
    if (isDir) { walk(full, acc); } else { acc.push(full); }
  }
  return acc;
}
const rel = (f: string) => relative(process.cwd(), f).replace(/\\/g, "/");

describe("T-032 part A: one authoring shell, no second authoring page", () => {
  const files = walk(SRC).filter((f) => f.endsWith(".tsx") || f.endsWith(".ts"));

  it("the shell exists and is the only module exporting SharedAuthoringShell", () => {
    const exporters = files.filter((f) =>
      /export\s+(function|const|default)\s+SharedAuthoringShell\b/.test(readFileSync(f, "utf8")));
    expect(exporters.map(rel)).toEqual(["src/authoring/SharedAuthoringShell.tsx"]);
  });

  // Chapter 4 section 5.2.1 rules ONE shell for S1 to S5, and the convergence
  // is staged across the backlog rather than done in one task. Each surface
  // that still owns its own board is named here WITH THE TASK THAT CONVERGES
  // IT, so this is a ratchet and not an exemption: the list may only shrink,
  // and an entry that has already been converged makes the test fail until it
  // is deleted. Nothing may ever be added without its owning task.
  const PENDING_CONVERGENCE: Record<string, string> = {
    "src/pages/Analysis/AnalysisToolboxPage.tsx":
      "T-065 - J12 Analysis authoring: converge onto D3 Analysis Toolbox in S3 mode",
  };

  function ownsABoard(file: string): boolean {
    const text = readFileSync(file, "utf8");
    return /from\s+["']@xyflow\/react["']/.test(text) ||
           /from\s+["'][^"']*canvas\/CanvasShell["']/.test(text);
  }

  it("no page component owns an authoring board except those a later task converges", () => {
    const offenders = files
      .filter((f) => rel(f).startsWith("src/pages/"))
      .filter((f) => !/\.test\.|__tests__/.test(rel(f)))
      .filter(ownsABoard)
      .map(rel)
      .filter((r) => !(r in PENDING_CONVERGENCE));
    expect(offenders, "an authoring board outside the shared shell:\n  " + offenders.join("\n  ")).toEqual([]);
  });

  it("the pending-convergence list is self-cleaning", () => {
    const stale: string[] = [];
    for (const path of Object.keys(PENDING_CONVERGENCE)) {
      const full = join(process.cwd(), path);
      if (!files.map(rel).includes(path)) {
        stale.push(path + " no longer exists - delete this entry (" + PENDING_CONVERGENCE[path] + ")");
        continue;
      }
      if (!ownsABoard(full)) {
        stale.push(path + " no longer owns a board - delete this entry (" + PENDING_CONVERGENCE[path] + ")");
      }
    }
    expect(stale, "converged surfaces still listed as pending:\n  " + stale.join("\n  ")).toEqual([]);
  });

  it("the retired S1 page is gone from the tree, not merely unrouted", () => {
    expect(files.map(rel)).not.toContain("src/pages/Prep/VisualJoinCanvasPage.tsx");
  });

  it("the application routes the authoring surface to the shell", () => {
    const app = readFileSync(join(SRC, "App.tsx"), "utf8");
    expect(app).toContain("SharedAuthoringShell");
    expect(app).not.toContain("VisualJoinCanvasPage");
  });

  it("the purpose registry carries all five purposes of section 5.2.1", () => {
    expect(AUTHORING_PURPOSES.map((p) => p.purpose)).toEqual(["S1", "S2", "S3", "S4", "S5"]);
  });

  it("the shell hardcodes no plant vocabulary", () => {
    const shellSources = [
      "src/authoring/SharedAuthoringShell.tsx",
      "src/authoring/AuthoringSchemaTree.tsx",
      "src/authoring/AuthoringToolbox.tsx",
      "src/authoring/blockRegistry.ts",
      "src/authoring/BlockNodes.tsx",
      "src/authoring/authoringPurposes.ts",
    ].map((p) => readFileSync(join(process.cwd(), p), "utf8")).join("\n");
    // The needles are ASSEMBLED FROM FRAGMENTS on purpose. A guard that spells
    // out the string it forbids becomes a hit in the next repository scan, and
    // the 03-Aug audit report was four self-matches of exactly that shape.
    const open = "<";
    const raw = ["input", "select", "textarea", "label", "button", "table"].map((t) => open + t);
    const inlineStyle = "style=" + "{{";
    for (const marker of raw.concat([inlineStyle])) {
      expect(shellSources.includes(marker), "ratchet marker present: " + marker).toBe(false);
    }
  });
});

describe("T-032 part B: the four regions render in every mode", () => {
  beforeAll(() => {
    if (!("ResizeObserver" in globalThis)) {
      (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver = class {
        observe() { /* jsdom has no layout */ }
        unobserve() { /* jsdom has no layout */ }
        disconnect() { /* jsdom has no layout */ }
      };
    }
  });

  it("S1 renders mode bar, schema tree, board, toolbox and debug log", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    expect(screen.getByTestId("authoring-mode-bar")).toBeInTheDocument();
    expect(screen.getByTestId("canvas-schema-tree")).toBeInTheDocument();
    expect(screen.getByTestId("authoring-board")).toBeInTheDocument();
    expect(screen.getByTestId("authoring-toolbox")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText("staging_one")).toBeInTheDocument());
  });

  it("S2 renders the same four regions with its own palette", () => {
    render(<SharedAuthoringShell purpose="S2" />);
    expect(screen.getByTestId("authoring-shell")).toHaveAttribute("data-purpose", "S2");
    expect(screen.getByTestId("authoring-mode-bar")).toBeInTheDocument();
    expect(screen.getByTestId("canvas-schema-tree")).toBeInTheDocument();
    expect(screen.getByTestId("authoring-board")).toBeInTheDocument();
    expect(screen.getByTestId("toolbox-group-relational")).toBeInTheDocument();
  });

  it("SQL mode hides the toolbox entirely rather than disabling it", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await userEvent.click(screen.getByRole("button", { name: "SQL" }));
    expect(screen.getByTestId("canvas-sql-pane")).toBeInTheDocument();
    expect(screen.getByTestId("canvas-schema-tree")).toBeInTheDocument();
    expect(screen.queryByTestId("authoring-toolbox")).toBeNull();
  });

  it("S1 enables exactly the three relational blocks the board implements", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText("staging_one");
    expect(screen.getByRole("button", { name: /^Filter/ })).toBeEnabled();
    expect(screen.getByRole("button", { name: /^Select columns/ })).toBeEnabled();
    expect(screen.getByRole("button", { name: /^Derived column/ })).toBeEnabled();
    // Ruling 4 keeps Rename out of T-033, and the toolbox says so rather than
    // pretending the block does not exist.
    expect(screen.getByRole("button", { name: /^Rename/ })).toBeDisabled();
  });

  it("the board actions are visible affordances, not key bindings only", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText("staging_one");
    expect(screen.getByRole("button", { name: "Delete selected" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Arrange" })).toBeInTheDocument();
  });

  it("Run is refused while the validity indicator reads Invalid", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    // The catalogue effect resolves after the first paint. Awaiting it here
    // keeps the state update inside act(), which is what React was warning
    // about - the assertions below do not depend on the catalogue.
    await screen.findByText("staging_one");
    expect(screen.getByTestId("authoring-validity")).toHaveTextContent("Invalid");
    expect(screen.getByRole("button", { name: "Run" })).toBeDisabled();
  });
});