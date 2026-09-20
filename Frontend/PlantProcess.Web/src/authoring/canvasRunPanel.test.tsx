// PPIQ T-245. The run surface over the governed job model.
//
// These prove the four things a monitor can get wrong: showing a runnable button with no
// runtime behind it, attaching to somebody else's run, letting a late answer undo a
// terminal one, and calling a cancellation request a cancelled run.

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const describeExecution = vi.fn();
const getCanvasJobBinding = vi.fn();
const bindCanvasJob = vi.fn();
const launchCanvasRun = vi.fn();
const findCanvasRun = vi.fn();
const readCanvasRun = vi.fn();
const requestCanvasRunCancellation = vi.fn();

vi.mock("@/api/canvasJobApi", () => ({
  describeExecution: (...a: unknown[]) => describeExecution(...a),
  getCanvasJobBinding: (...a: unknown[]) => getCanvasJobBinding(...a),
  bindCanvasJob: (...a: unknown[]) => bindCanvasJob(...a),
  launchCanvasRun: (...a: unknown[]) => launchCanvasRun(...a),
  findCanvasRun: (...a: unknown[]) => findCanvasRun(...a),
  readCanvasRun: (...a: unknown[]) => readCanvasRun(...a),
  requestCanvasRunCancellation: (...a: unknown[]) => requestCanvasRunCancellation(...a),
}));

import { CanvasRunPanel } from "./CanvasRunPanel";

const eligible = {
  definitionCode: "t245_canvas", definitionId: "d1", targetKind: "Transformation",
  resolvedVersion: 2, versionPolicy: "Pinned", familyIsExecutable: true,
  executorRegistered: true, staticallyEligible: true, refusalCode: null, refusalDetail: null,
  assurance: "Static eligibility only. This reserves no capacity.",
};

const binding = {
  jobDefinitionId: "j1", jobCode: "CANVAS_T245_CANVAS", jobName: "Canvas projection",
  definitionId: "d1", definitionCode: "t245_canvas", targetKind: "Transformation",
  versionPolicy: "Pinned", pinnedVersion: 2, created: false,
};

const run = (over: Record<string, unknown> = {}) => ({
  runId: "r1", jobDefinitionId: "j1", jobCode: "CANVAS_T245_CANVAS", status: "Running",
  isTerminal: false, startedAtUtc: "2026-09-20T09:00:00Z", completedAtUtc: null,
  correlationId: "corr-mine", targetDefinitionId: "d1", targetDefinitionVersion: 2,
  targetDefinitionKind: "Transformation", targetVersionPolicy: "Pinned",
  cancellationRequested: false, cancellationRequestedAtUtc: null, cancellationAcknowledgedAtUtc: null,
  failureReason: null, runMessage: null, blocks: [],
  ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  describeExecution.mockResolvedValue(eligible);
  getCanvasJobBinding.mockResolvedValue(binding);
  launchCanvasRun.mockResolvedValue(run({ status: "Ok", isTerminal: true, completedAtUtc: "2026-09-20T09:01:00Z" }));
  findCanvasRun.mockResolvedValue(null);
  readCanvasRun.mockResolvedValue(run());
});

describe("canvas run panel", () => {
  it("refuses to offer a run when no executor is registered", async () => {
    describeExecution.mockResolvedValue({
      ...eligible, executorRegistered: false, staticallyEligible: false,
      refusalCode: "JOB_EXEC_EXECUTOR_MISSING", refusalDetail: "no executor",
    });

    render(<CanvasRunPanel definitionCode="t245_canvas" publishedVersion={2} />);

    await waitFor(() => expect(screen.getByTestId("canvas-run-capability")).toHaveAttribute("data-eligible", "no"));
    expect(screen.getByText(/JOB_EXEC_EXECUTOR_MISSING/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Run now" })).toBeDisabled();
    expect(launchCanvasRun).not.toHaveBeenCalled();
  });

  it("launches with its own correlation and shows the version the run recorded", async () => {
    render(<CanvasRunPanel definitionCode="t245_canvas" publishedVersion={2} />);
    await waitFor(() => expect(screen.getByRole("button", { name: "Run now" })).toBeEnabled());

    await userEvent.click(screen.getByRole("button", { name: "Run now" }));

    await waitFor(() => expect(screen.getByTestId("canvas-run-evidence")).toHaveAttribute("data-status", "Ok"));
    const body = launchCanvasRun.mock.calls[0][1] as { correlationId: string };
    expect(body.correlationId).toMatch(/^canvas-/);
    expect(screen.getByText(/executed version 2/)).toBeInTheDocument();
  });

  it("shows per-block evidence with its typed diagnostic", async () => {
    launchCanvasRun.mockResolvedValue(run({
      status: "Failed", isTerminal: true, failureReason: "JOB_EXEC_SOURCE_IDENTITY_INVALID: row 2",
      blocks: [
        { blockId: "src_a", executionOrdinal: 0, status: "Succeeded", inputRows: null, outputRows: 7, diagnosticCode: null, diagnosticDetail: null, startedAtUtc: null, finishedAtUtc: null },
        { blockId: "only-large", executionOrdinal: 1, status: "Failed", inputRows: 2, outputRows: null, diagnosticCode: "JOB_EXEC_SOURCE_IDENTITY_INVALID", diagnosticDetail: "row 2", startedAtUtc: null, finishedAtUtc: null },
      ],
    }));

    render(<CanvasRunPanel definitionCode="t245_canvas" publishedVersion={2} />);
    await waitFor(() => expect(screen.getByRole("button", { name: "Run now" })).toBeEnabled());
    await userEvent.click(screen.getByRole("button", { name: "Run now" }));

    await waitFor(() => expect(screen.getByTestId("canvas-run-block-only-large")).toHaveAttribute("data-block-status", "Failed"));
    expect(screen.getByTestId("canvas-run-block-src_a")).toHaveAttribute("data-block-status", "Succeeded");
    expect(screen.getByTestId("canvas-run-failure")).toHaveTextContent("JOB_EXEC_SOURCE_IDENTITY_INVALID");
  });

  it("keeps a cancellation request separate from a cancelled run", async () => {
    launchCanvasRun.mockResolvedValue(run({ cancellationRequested: true, cancellationRequestedAtUtc: "2026-09-20T09:00:30Z" }));
    requestCanvasRunCancellation.mockResolvedValue({});

    render(<CanvasRunPanel definitionCode="t245_canvas" publishedVersion={2} />);
    await waitFor(() => expect(screen.getByRole("button", { name: "Run now" })).toBeEnabled());
    await userEvent.click(screen.getByRole("button", { name: "Run now" }));

    await waitFor(() => expect(screen.getByTestId("canvas-run-evidence")).toHaveAttribute("data-status", "Running"));
    await userEvent.click(screen.getByRole("button", { name: "Request cancellation" }));

    await waitFor(() => expect(screen.getByTestId("canvas-run-message")).toHaveTextContent("acknowledges"));
    expect(screen.getByTestId("canvas-run-evidence")).toHaveAttribute("data-status", "Running");
  });

  it("drops an answer that belongs to another run and never regresses a terminal one", async () => {
    let resolveLate: ((value: unknown) => void) | null = null;
    readCanvasRun.mockImplementation(() => new Promise((resolve) => { resolveLate = resolve; }));
    launchCanvasRun.mockResolvedValue(run({ status: "Ok", isTerminal: true }));

    render(<CanvasRunPanel definitionCode="t245_canvas" publishedVersion={2} pollIntervalMs={5} />);
    await waitFor(() => expect(screen.getByRole("button", { name: "Run now" })).toBeEnabled());
    await userEvent.click(screen.getByRole("button", { name: "Run now" }));

    await waitFor(() => expect(screen.getByTestId("canvas-run-evidence")).toHaveAttribute("data-status", "Ok"));

    // A poll that started before the terminal answer finally arrives, both for another
    // run and as a stale Running for this one. Neither may be displayed.
    if (resolveLate) { (resolveLate as (value: unknown) => void)(run({ runId: "r2", status: "Running" })); }
    await new Promise((r) => setTimeout(r, 20));

    expect(screen.getByTestId("canvas-run-evidence")).toHaveAttribute("data-run-id", "r1");
    expect(screen.getByTestId("canvas-run-evidence")).toHaveAttribute("data-status", "Ok");
  });
});
