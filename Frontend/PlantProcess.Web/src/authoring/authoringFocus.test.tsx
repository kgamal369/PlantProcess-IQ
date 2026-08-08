// PPIQ T-040 FOCUS-01. AN AUTHORING SURFACE OPENED AS A DIALOG TAKES FOCUS.
//
// The browser run proved the gap: Add widget opened S2 and focus remained on the
// document body, so Escape was unanswerable until the author clicked into the
// surface. These proofs fix the contract in place - focus moves in when, and
// only when, the surface was opened as a dialog, and Escape then works from the
// first keystroke without any prior interaction.

import { render, screen, waitFor } from "@testing-library/react";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it, vi } from "vitest";
import { SharedAuthoringShell } from "./SharedAuthoringShell";

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
    executeWidgetQueryExpression: () => Promise.resolve({ columns: [], rows: [], warnings: [] }),
    createDashboardWidgetDefinition: () => Promise.resolve({}),
    updateDashboardWidgetDefinition: () => Promise.resolve({}),
  },
}));

const SHELL_FILE = join(process.cwd(), "src/authoring/SharedAuthoringShell.tsx");

describe("T-040 FOCUS-01 the dialog surface takes focus", () => {
  it("makes the shell root programmatically focusable and nothing more", () => {
    const source = readFileSync(SHELL_FILE, "utf8");
    expect(source).toContain("tabIndex={-1}");
    expect(/tabIndex\s*=\s*\{\s*[1-9]/.test(source), "a positive tabIndex strands every later control").toBe(false);
    expect(source).not.toContain("addEventListener");
  });

  it("moves focus into the surface when it was opened as a dialog", async () => {
    render(<SharedAuthoringShell purpose="S2" onClose={() => undefined} />);
    const shell = await screen.findByTestId("authoring-shell");
    await waitFor(() => expect(shell.contains(document.activeElement)).toBe(true));
  });

  it("leaves focus alone when the purpose was opened as a page", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText(/No staged datasets/);
    const shell = screen.getByTestId("authoring-shell");
    expect(shell.contains(document.activeElement)).toBe(false);
  });

  it("answers Escape from the first keystroke, with no prior interaction", async () => {
    const onClose = vi.fn();
    render(<SharedAuthoringShell purpose="S2" onClose={onClose} />);
    const shell = await screen.findByTestId("authoring-shell");
    await waitFor(() => expect(shell.contains(document.activeElement)).toBe(true));

    const target = (document.activeElement ?? shell) as HTMLElement;
    const { fireEvent } = await import("@testing-library/react");
    fireEvent.keyDown(target, { key: "Escape" });
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});