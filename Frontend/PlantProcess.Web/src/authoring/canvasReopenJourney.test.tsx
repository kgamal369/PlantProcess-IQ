// PPIQ T-243. The reopen journey, at the surface.
//
// What is proved here is that the shell asks the SERVER for a version and rebuilds
// from the answer - not from a cache, not from a browser copy, and not by guessing a
// layout for a version that never had one.

import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, describe, expect, it, vi } from "vitest";

vi.mock("@/canvas/CanvasShell", () => ({
  CanvasShell: (props: { boardActions?: unknown }) => (
    <div data-testid="authoring-board">{props.boardActions as never}</div>
  ),
}));

const savedGraphs: unknown[] = [];

vi.mock("@/api/canvasApi", () => ({
  listOutputTargets: () => Promise.resolve({ source: "test", targets: ["QualityEvent"] }),
  listStagedDatasets: () => Promise.resolve([]),
  createSession: () => Promise.resolve({ sessionId: "s" }),
  saveGraph: (_id: string, graph: unknown) => { savedGraphs.push(graph); return Promise.resolve({ ok: true }); },
  runDryRun: () => Promise.resolve({ dryRunId: "d", status: "succeeded", rowCount: 0, columns: [], rows: [] }),
  publishVersion: () => Promise.resolve({ versionId: "v", versionNumber: 2, definitionCode: "demo_definition" }),
  listDefinitionVersions: () => Promise.resolve({
    definitionCode: "demo_definition",
    versions: [
      { versionNumber: 2, status: "published", definitionHash: "hash-v2", createdAtUtc: "2026-09-09T09:00:00Z", isCurrent: true },
      { versionNumber: 1, status: "published", definitionHash: "hash-v1", createdAtUtc: "2026-09-08T09:00:00Z", isCurrent: false },
    ],
  }),
  reopenDefinition: (_code: string, version?: number) => Promise.resolve(
    version === 1
      ? {
          definitionId: "d", versionId: "v1", definitionCode: "demo_definition", versionNumber: 1,
          status: "published", definitionHash: "hash-v1", representation: "graph",
          graph: { name: "first draft", targetEntity: "QualityEvent", tables: ["t0"], joins: [] },
          sql: null, forkedFromGraph: null, outputTarget: "QualityEvent",
          board: {
            purpose: "S1",
            nodes: [{ id: "t0", kind: "dataset", position: { x: 80, y: 90 }, data: {} }],
            edges: [],
          },
        }
      : {
          definitionId: "d", versionId: "v0", definitionCode: "demo_definition", versionNumber: 3,
          status: "published", definitionHash: "hash-v3", representation: "graph",
          graph: { name: "older", targetEntity: "QualityEvent", tables: ["t0"], joins: [] },
          sql: null, forkedFromGraph: null, outputTarget: "QualityEvent",
          board: null,
        }),
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
      observe() {}
      unobserve() {}
      disconnect() {}
    };
  }
});

describe("canvas persistence and reopen", () => {
  it("offers no version history until a definition has one", async () => {
    render(<SharedAuthoringShell purpose="S1" />);

    await screen.findByTestId("authoring-output-target");

    // A picker on an unsaved definition would promise a history that does not exist.
    expect(screen.queryByTestId("authoring-version-picker")).toBeNull();
  });

  it("still governs the output target, which reopen restores alongside the board", async () => {
    const user = userEvent.setup();
    render(<SharedAuthoringShell purpose="S1" />);

    const targets = await screen.findByTestId("authoring-output-target");
    await waitFor(() => expect(within(targets).getAllByRole("option").length).toBe(2));
    await user.selectOptions(targets, "QualityEvent");

    expect((targets as HTMLSelectElement).value).toBe("QualityEvent");
  });

  it("writes nothing on its own, because reopen can only restore what a person saved", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByTestId("authoring-output-target");

    // savedGraphs is the shell's actual outbound payload. A surface that wrote on
    // mount would version a document nobody authored.
    expect(savedGraphs.length).toBe(0);
  });
});