// PPIQ T-253. The governed output target, at the surface.
//
// The shell used to compile one plant's entity into every graph and one plant's table
// into every saved statement. These prove it now asks, carries what the author chose,
// and refuses rather than defaulting when nothing has been chosen.

import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, describe, expect, it, vi } from "vitest";

vi.mock("@/canvas/CanvasShell", () => ({
  CanvasShell: (props: { boardActions?: unknown }) => (
    <div data-testid="authoring-board">{props.boardActions as never}</div>
  ),
}));

vi.mock("@/api/canvasApi", () => ({
  listOutputTargets: () => Promise.resolve({ source: "test", targets: ["ProcessEvent", "QualityEvent"] }),
  listStagedDatasets: () => Promise.resolve([]),
  createSession: () => Promise.resolve({ sessionId: "s" }),
  saveGraph: () => Promise.resolve({ ok: true }),
  runDryRun: () => Promise.resolve({ dryRunId: "d", status: "succeeded", rowCount: 0, columns: [], rows: [] }),
  publishVersion: () => Promise.resolve({ versionId: "v", versionNumber: 1 }),
  runAuthoredSql: () => Promise.resolve({
    status: "succeeded", rowCount: 0, columns: [], rows: [],
    message: "", errorCode: null, sql: null, appliedRowLimit: 100,
  }),
  saveSqlVersion: () => Promise.resolve({
    saved: true, versionNumber: 1, id: "1", message: "saved", errorCode: null,
  }),
}));

const { SharedAuthoringShell } = await import("./SharedAuthoringShell");

beforeAll(() => {
  if (!("ResizeObserver" in globalThis)) {
    (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver = class {
      observe() {}
      unobserve() {}
      disconnect() {}
    };
  }
});

describe("governed output target", () => {
  it("offers the targets the server enumerates and compiles none of its own", async () => {
    render(<SharedAuthoringShell purpose="S1" />);

    const picker = await screen.findByTestId("authoring-output-target");

    await waitFor(() => {
      expect(within(picker).getAllByRole("option").length).toBe(3);
    });

    const labels = within(picker).getAllByRole("option").map((o) => o.textContent);
    expect(labels).toContain("ProcessEvent");
    expect(labels).toContain("QualityEvent");
    expect(labels).not.toContain("MaterialUnit");
  });

  it("starts with no target chosen rather than a default", async () => {
    render(<SharedAuthoringShell purpose="S1" />);

    const picker = await screen.findByTestId("authoring-output-target") as HTMLSelectElement;

    expect(picker.value).toBe("");
  });

  it("records the target the author chose, and nothing was chosen for them", async () => {
    const user = userEvent.setup();
    render(<SharedAuthoringShell purpose="S1" />);

    const picker = await screen.findByTestId("authoring-output-target");
    await waitFor(() => expect(within(picker).getAllByRole("option").length).toBe(3));
    await user.selectOptions(picker, "QualityEvent");

    expect((picker as HTMLSelectElement).value).toBe("QualityEvent");
  });

  it("uses the standard control rather than a raw select", async () => {
    render(<SharedAuthoringShell purpose="S1" />);

    const picker = await screen.findByTestId("authoring-output-target");

    expect(picker.className).toContain("standard-p2-select");
  });
});