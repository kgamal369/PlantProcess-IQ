// PPIQ T-036. The SQL mode surface under test.
//
// Its own mocks, so it does not depend on the T-032 acceptance file's fixtures
// and cannot be broken by an edit there. The decisions are already proven in
// sqlModeModel.test.ts; this asserts the WIRING - that the shell asks the
// model, and that the toolbox is GONE rather than disabled.

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, describe, expect, it, vi } from "vitest";

vi.mock("@/canvas/CanvasShell", () => ({
  CanvasShell: (props: { boardActions?: unknown }) => (
    <div data-testid="authoring-board">{props.boardActions as never}</div>
  ),
}));

vi.mock("@/api/canvasApi", () => ({
  listStagedDatasets: () => Promise.resolve([
    {
      table: "source_a", source: "staging_one", approxRowCount: 12,
      columns: [{ name: "key_column", sqlType: "text", isKeyCandidate: true, isNullable: false }],
    },
  ]),
  createSession: () => Promise.resolve({ sessionId: "test-session" }),
  saveGraph: () => Promise.resolve({ ok: true }),
  runDryRun: () => Promise.resolve({ dryRunId: "d", status: "succeeded", rowCount: 0, columns: [], rows: [] }),
  publishVersion: () => Promise.resolve({ versionId: "v", versionNumber: 1 }),
  runAuthoredSql: () => Promise.resolve({
    status: "succeeded", rowCount: 0, columns: [], rows: [],
    message: "", errorCode: null, sql: null, appliedRowLimit: 100,
  }),
  saveSqlVersion: () => Promise.resolve({ saved: true, versionNumber: 1, id: "1", message: "saved", errorCode: null }),
}));

const { SharedAuthoringShell } = await import("./SharedAuthoringShell");

beforeAll(() => {
  if (!("ResizeObserver" in globalThis)) {
    (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver = class {
      observe() {} unobserve() {} disconnect() {}
    };
  }
});

describe("section 5.2.12, the toolbox in SQL mode", () => {
  it("is present in Block mode and GONE in SQL mode, not merely disabled", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText("staging_one");
    expect(screen.getByTestId("authoring-toolbox-region")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "SQL" }));
    // Not disabled, not hidden: absent from the tree entirely.
    expect(screen.queryByTestId("authoring-toolbox-region")).toBeNull();
    expect(screen.getByTestId("canvas-sql-pane")).toBeInTheDocument();
  });

  it("keeps the schema tree in SQL mode, which the shell contract requires", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText("staging_one");
    await userEvent.click(screen.getByRole("button", { name: "SQL" }));
    expect(screen.getByTestId("canvas-schema-tree")).toBeInTheDocument();
  });

  it("returns to the board without a prompt when nothing was ever authored", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText("staging_one");
    await userEvent.click(screen.getByRole("button", { name: "SQL" }));
    await userEvent.click(screen.getByRole("button", { name: "Block wiring" }));
    expect(screen.queryByTestId("canvas-discard-warning")).toBeNull();
    expect(screen.getByTestId("authoring-toolbox-region")).toBeInTheDocument();
  });
});
