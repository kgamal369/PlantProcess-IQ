// PPIQ T-040 03b3. THE KEYBOARD PATH (Golden Gate G10).
//
// Tab order is the DOM order, so it is proved NEGATIVELY: no authoring source
// may assign a positive tabIndex. Zero and minus one are legitimate; any
// positive value pulls one control to the front of the whole document and
// silently strands every control after it.
//
// The catalogue mock returns nothing, so the schema tree renders its empty
// message. Waiting for that is what proves the catalogue effect has settled -
// an earlier pack in this task waited for a dataset the mock never supplied and
// failed four tests over code that was already correct.
//
// AND THE SAME LESSON AGAIN, ONE REVISION LATER: the refusal sentence is on
// screen TWICE. The centre banner states it as the blocked-state sentence from
// the moment the shell mounts, and the debug log states it only when a run was
// actually attempted. Asking the whole document whether the sentence is present
// therefore answers a different question than the one being tested. Every
// assertion below about whether a run happened is scoped to the debug log,
// section 5.2.8's authoritative surface for a refusal.
//
// AND TO NOTHING ELSE. A later revision added an error-counter read as a second
// opinion. It was the only thing the four failing tests had in common and it
// proved nothing the scoped query does not already prove. A test file earns its
// place by asserting the contract, not by asserting it twice.

import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it, vi } from "vitest";
import { SharedAuthoringShell } from "./SharedAuthoringShell";

const executeExpression = vi.fn(() => Promise.resolve({ columns: [], rows: [], warnings: [] }));

vi.mock("@/canvas/CanvasShell", () => ({
  CanvasShell: () => <div data-testid="authoring-board" />,
}));

vi.mock("@/api/canvasApi", () => ({
  listStagedDatasets: () => Promise.resolve([]),
  createSession: () => Promise.resolve({ sessionId: "s" }),
  saveGraph: () => Promise.resolve({ ok: true }),
  runDryRun: () => Promise.resolve({ dryRunId: "d", status: "succeeded", rowCount: 0, columns: [], rows: [] }),
  publishVersion: () => Promise.resolve({ versionId: "v", versionNumber: 1 }),
  runAuthoredSql: () => Promise.resolve({ status: "succeeded", rowCount: 0, columns: [], rows: [], message: "", errorCode: null, sql: null, appliedRowLimit: 100 }),
  saveSqlVersion: () => Promise.resolve({ saved: true, versionNumber: 1, id: "1", message: "saved", errorCode: null }),
}));

vi.mock("@/api/dashboarding/dashboarding.api", () => ({
  dashboardingApi: {
    getDashboardMetadata: () => Promise.resolve({ chartTypes: [], dimensions: [], measures: [], filters: [] }),
    getDashboardReferenceData: () => Promise.resolve({}),
    executeWidgetQueryExpression: (...args: unknown[]) => executeExpression(...(args as [])),
    createDashboardWidgetDefinition: () => Promise.resolve({}),
    updateDashboardWidgetDefinition: () => Promise.resolve({}),
  },
}));

const AUTHORING_DIR = join(process.cwd(), "src/authoring");
const SHELL_FILE = join(AUTHORING_DIR, "SharedAuthoringShell.tsx");

function authoringSources() {
  return readdirSync(AUTHORING_DIR)
    .filter((f) => (f.endsWith(".ts") || f.endsWith(".tsx")) && !f.includes(".test."))
    .map((f) => ({ name: f, text: readFileSync(join(AUTHORING_DIR, f), "utf8") }));
}

async function shellReady() {
  render(<SharedAuthoringShell purpose="S1" />);
  await screen.findByText(/No staged datasets/);
  return screen.getByTestId("authoring-shell");
}

const debugLog = () => screen.getByTestId("canvas-debug-log");

describe("T-040 G10 the keyboard path", () => {
  it("assigns no positive tabIndex anywhere in the authoring surface", () => {
    const offenders = authoringSources()
      .filter((s) => /tabIndex\s*=\s*\{\s*[1-9]/.test(s.text) || /tabIndex\s*=\s*"[1-9]/.test(s.text))
      .map((s) => s.name);
    expect(offenders).toEqual([]);
  });

  it("puts the handler on the shell root and never on the window", () => {
    const text = readFileSync(SHELL_FILE, "utf8");
    expect(text).toContain("onKeyDown={onShellKeyDown}");
    expect(text).not.toContain("addEventListener");
  });

  it("runs from Enter when nothing else owns the key", async () => {
    const root = await shellReady();
    fireEvent.keyDown(root, { key: "Enter" });
    expect(await within(debugLog()).findByText(/Nothing on the board yet/)).toBeInTheDocument();
  });

  it("leaves Enter to the definition name control", async () => {
    await shellReady();
    fireEvent.keyDown(screen.getByLabelText("Definition name"), { key: "Enter" });
    expect(within(debugLog()).queryByText(/Nothing on the board yet/)).toBeNull();
  });

  it("refuses through the same sentence the validity indicator carries", async () => {
    const root = await shellReady();
    const reason = screen.getByTestId("authoring-validity").getAttribute("title") ?? "";
    expect(reason.length).toBeGreaterThan(0);
    fireEvent.keyDown(root, { key: "Enter" });
    const entry = await within(debugLog()).findByText(/Nothing on the board yet/);
    expect(entry.textContent ?? "").toContain(reason);
  });

  it("leaves Enter to a focused control that already answers it", async () => {
    await shellReady();
    fireEvent.keyDown(screen.getByRole("button", { name: "Block wiring" }), { key: "Enter" });
    expect(within(debugLog()).queryByText(/Nothing on the board yet/)).toBeNull();
  });

  it("does not run a query purpose, whose face owns its own control", async () => {
    executeExpression.mockClear();
    render(<SharedAuthoringShell purpose="S2" />);
    const root = await screen.findByTestId("authoring-shell");
    fireEvent.keyDown(root, { key: "Enter" });
    expect(executeExpression).not.toHaveBeenCalled();
  });

  it("dismisses the innermost surface before it considers closing", async () => {
    const onClose = vi.fn();
    render(<SharedAuthoringShell purpose="S1" onClose={onClose} />);
    await screen.findByText(/No staged datasets/);
    await userEvent.click(screen.getByRole("button", { name: "SQL" }));
    await userEvent.click(await screen.findByRole("button", { name: "Author SQL from here" }));
    expect(await screen.findByTestId("canvas-fork-warning")).toBeInTheDocument();

    fireEvent.keyDown(screen.getByTestId("authoring-shell"), { key: "Escape" });
    expect(screen.queryByTestId("canvas-fork-warning")).toBeNull();
    expect(onClose).not.toHaveBeenCalled();
  });

  it("closes on Escape when the shell was opened as a dialog", async () => {
    const onClose = vi.fn();
    render(<SharedAuthoringShell purpose="S1" onClose={onClose} />);
    await screen.findByText(/No staged datasets/);
    fireEvent.keyDown(screen.getByTestId("authoring-shell"), { key: "Escape" });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("does nothing on Escape when there is nothing to dismiss and nowhere to close to", async () => {
    const root = await shellReady();
    fireEvent.keyDown(root, { key: "Escape" });
    expect(screen.getByTestId("authoring-shell")).toBeInTheDocument();
    expect(within(debugLog()).queryByText(/Nothing on the board yet/)).toBeNull();
  });

  it("keeps the definition intact when Escape closes the shell", async () => {
    const onClose = vi.fn();
    render(<SharedAuthoringShell purpose="S1" onClose={onClose} />);
    await screen.findByText(/No staged datasets/);
    const nameField = screen.getByLabelText("Definition name");
    await userEvent.clear(nameField);
    await userEvent.type(nameField, "Yield by grade");

    fireEvent.keyDown(screen.getByTestId("authoring-shell"), { key: "Escape" });
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText("Definition name")).toHaveValue("Yield by grade");
  });
});
