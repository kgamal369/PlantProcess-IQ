// PPIQ T-243. THE REOPEN JOURNEY, EXECUTED.
//
// An earlier version of this file asserted around the journey rather than through it,
// while the closure evidence claimed the journey was proven. That gap is closed here:
// every test below drives the real shell against a mocked canonical server and reads
// the board model the shell actually renders.
//
// The board seam is CanvasShell's nodes prop. Mocking it to print node ids and
// positions makes the reconstructed board observable without a browser, which is what
// lets these run with no database at all.

import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

type BoardNodeProbe = { id: string; position: { x: number; y: number } };

vi.mock("@/canvas/CanvasShell", () => ({
  CanvasShell: (props: { nodes?: BoardNodeProbe[]; boardActions?: unknown }) => (
    <div data-testid="authoring-board">
      <span data-testid="board-model">
        {(props.nodes ?? []).map((n) => n.id + "@" + n.position.x + "," + n.position.y).join("|")}
      </span>
      {props.boardActions as never}
    </div>
  ),
}));

const CODE = "demo_definition";

const boardV1 = {
  purpose: "S1",
  nodes: [{ id: "t0", kind: "dataset", position: { x: 80, y: 90 }, data: {} }],
  edges: [],
};

const boardV2 = {
  purpose: "S1",
  nodes: [
    { id: "t0", kind: "dataset", position: { x: 240, y: 90 }, data: {} },
    { id: "t1", kind: "dataset", position: { x: 540, y: 90 }, data: {} },
  ],
  edges: [{ source: "t0", target: "t1", sourceHandle: "flow:out", targetHandle: "flow:in" }],
};

function response(version: number, board: unknown, name: string) {
  return {
    definitionId: "d", versionId: "v" + version, definitionCode: CODE, versionNumber: version,
    status: "published", definitionHash: "hash-v" + version, representation: "graph",
    graph: { name, targetEntity: "QualityEvent", tables: ["t0"], joins: [] },
    sql: null, forkedFromGraph: null, outputTarget: "QualityEvent", board,
  };
}

const reopenDefinition = vi.fn();
const listDefinitionVersions = vi.fn();
const saveGraph = vi.fn();
const publishVersion = vi.fn();

vi.mock("@/api/canvasApi", () => ({
  listOutputTargets: () => Promise.resolve({ source: "test", targets: ["QualityEvent"] }),
  listStagedDatasets: () => Promise.resolve([]),
  createSession: () => Promise.resolve({ sessionId: "s" }),
  saveGraph: (...a: unknown[]) => saveGraph(...a),
  runDryRun: () => Promise.resolve({ dryRunId: "d", status: "succeeded", rowCount: 0, columns: [], rows: [] }),
  publishVersion: (...a: unknown[]) => publishVersion(...a),
  listDefinitionVersions: (...a: unknown[]) => listDefinitionVersions(...a),
  reopenDefinition: (...a: unknown[]) => reopenDefinition(...a),
  runAuthoredSql: () => Promise.resolve({
    status: "succeeded", rowCount: 0, columns: [], rows: [],
    message: "", errorCode: null, sql: null, appliedRowLimit: 100,
  }),
  saveSqlVersion: () => Promise.resolve({ saved: true, versionNumber: 1, id: "1", message: "saved", errorCode: null }),
}));

const { SharedAuthoringShell } = await import("./SharedAuthoringShell");

const HISTORY = {
  definitionCode: CODE,
  versions: [
    { versionNumber: 2, status: "published", definitionHash: "hash-v2", createdAtUtc: "2026-09-09T09:00:00Z", isCurrent: true },
    { versionNumber: 1, status: "published", definitionHash: "hash-v1", createdAtUtc: "2026-09-08T09:00:00Z", isCurrent: false },
  ],
};

beforeAll(() => {
  if (!("ResizeObserver" in globalThis)) {
    (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver = class {
      observe() {}
      unobserve() {}
      disconnect() {}
    };
  }
});

beforeEach(() => {
  reopenDefinition.mockReset();
  listDefinitionVersions.mockReset();
  saveGraph.mockReset();
  publishVersion.mockReset();

  listDefinitionVersions.mockResolvedValue(HISTORY);
  saveGraph.mockResolvedValue({ ok: true });
  publishVersion.mockResolvedValue({ versionId: "v3", versionNumber: 3, definitionCode: CODE });
  reopenDefinition.mockImplementation((_code: string, version?: number) =>
    Promise.resolve(version === 1 ? response(1, boardV1, "first draft") : response(2, boardV2, "current")));
});

const board = () => screen.getByTestId("board-model").textContent;

describe("opening an existing definition", () => {
  it("asks the canonical store for the current version and rebuilds the board from it", async () => {
    render(<SharedAuthoringShell purpose="S1" initialDefinitionCode={CODE} />);

    await waitFor(() => expect(board()).toBe("t0@240,90|t1@540,90"));

    // Fetched, not remembered. The code is the only thing the surface was given.
    expect(reopenDefinition).toHaveBeenCalledWith(CODE);
    expect(listDefinitionVersions).toHaveBeenCalledWith(CODE);

    const targets = screen.getByTestId("authoring-output-target") as HTMLSelectElement;
    expect(targets.value).toBe("QualityEvent");

    expect(screen.getByTestId("authoring-version-picker")).toBeTruthy();
  });

  it("offers no history for a definition that has none", async () => {
    render(<SharedAuthoringShell purpose="S1" />);

    await screen.findByTestId("authoring-output-target");

    expect(reopenDefinition).not.toHaveBeenCalled();
    expect(screen.queryByTestId("authoring-version-picker")).toBeNull();
  });
});

describe("choosing a historical version", () => {
  it("fetches that version and replaces the authored board with it", async () => {
    const user = userEvent.setup();
    render(<SharedAuthoringShell purpose="S1" initialDefinitionCode={CODE} />);

    await waitFor(() => expect(board()).toBe("t0@240,90|t1@540,90"));

    await user.selectOptions(screen.getByTestId("authoring-version-picker"), "1");

    expect(reopenDefinition).toHaveBeenCalledWith(CODE, 1);

    // The board MODEL changed, not merely the select value: one block, back at 80,90.
    await waitFor(() => expect(board()).toBe("t0@80,90"));
  });

  it("restores the original board when an older version is chosen again after an edit", async () => {
    const user = userEvent.setup();
    render(<SharedAuthoringShell purpose="S1" initialDefinitionCode={CODE} />);

    await waitFor(() => expect(board()).toBe("t0@240,90|t1@540,90"));

    await user.selectOptions(screen.getByTestId("authoring-version-picker"), "1");
    await waitFor(() => expect(board()).toBe("t0@80,90"));

    await user.selectOptions(screen.getByTestId("authoring-version-picker"), "2");
    await waitFor(() => expect(board()).toBe("t0@240,90|t1@540,90"));

    // V1 came back as V1. Reopening never rewrote it, which is the surface half of
    // the immutability the content tests prove at byte level.
    await user.selectOptions(screen.getByTestId("authoring-version-picker"), "1");
    await waitFor(() => expect(board()).toBe("t0@80,90"));
  });
});

describe("publishing after a reopen", () => {
  it("sends the reopened board and its governed target, and refreshes history", async () => {
    const user = userEvent.setup();
    render(<SharedAuthoringShell purpose="S1" initialDefinitionCode={CODE} />);

    await waitFor(() => expect(board()).toBe("t0@240,90|t1@540,90"));

    await user.selectOptions(screen.getByTestId("authoring-version-picker"), "1");
    await waitFor(() => expect(board()).toBe("t0@80,90"));

    listDefinitionVersions.mockClear();
    await user.click(screen.getByRole("button", { name: /publish/i }));

    await waitFor(() => expect(saveGraph).toHaveBeenCalled());

    const sent = saveGraph.mock.calls[0][1] as {
      targetEntity: string;
      board: { purpose: string; nodes: BoardNodeProbe[] };
    };

    // What was reopened is what is sent. Nothing legacy is invented on the way out.
    expect(sent.targetEntity).toBe("QualityEvent");
    expect(sent.board.purpose).toBe("S1");
    expect(sent.board.nodes.map((n) => n.id)).toEqual(["t0"]);
    expect(sent.board.nodes[0].position).toEqual({ x: 80, y: 90 });

    // A new version, and history is asked again rather than assumed.
    await waitFor(() => expect(publishVersion).toHaveBeenCalled());
    await waitFor(() => expect(listDefinitionVersions).toHaveBeenCalledWith(CODE));
  });
});

describe("compatibility", () => {
  it("fabricates no blocks for a version saved before boards were kept", async () => {
    reopenDefinition.mockImplementation(() => Promise.resolve(response(2, null, "legacy")));

    render(<SharedAuthoringShell purpose="S1" initialDefinitionCode={CODE} />);

    await waitFor(() => expect(listDefinitionVersions).toHaveBeenCalledWith(CODE));

    // No blocks were invented, and the surface says why rather than showing an
    // empty board with no explanation.
    expect(board()).toBe("");
    expect(await screen.findByText(/saved before boards were kept/i)).toBeTruthy();
  });

  it("refuses to load a board authored under another purpose", async () => {
    reopenDefinition.mockImplementation(() =>
      Promise.resolve(response(2, { ...boardV2, purpose: "S3" }, "other purpose")));

    render(<SharedAuthoringShell purpose="S1" initialDefinitionCode={CODE} />);

    await waitFor(() => expect(listDefinitionVersions).toHaveBeenCalledWith(CODE));

    expect(board()).toBe("");
    expect(await screen.findByText(/authored under purpose S3/i)).toBeTruthy();
  });
});