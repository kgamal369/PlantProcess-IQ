import { useCallback, useEffect, useRef, useState } from "react";
import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import {
  describeExecution, bindCanvasJob, getCanvasJobBinding, launchCanvasRun,
  findCanvasRun, readCanvasRun, requestCanvasRunCancellation,
  type CanvasExecutionCapability, type CanvasJobBinding, type CanvasRunEvidence,
} from "@/api/canvasJobApi";

// PPIQ T-245. THE RUN SURFACE OF A CANVAS DEFINITION.
//
// THE SERVER OWNS THE RUN. This panel starts one and then watches it. Closing the tab,
// refreshing or losing the network changes nothing about the execution; the only way to
// stop a run is the authorized cancellation operation, and even that stays REQUESTED
// until the run authority records an acknowledgement.
//
// EVERY ANSWER IS KEYED. A response is applied only if it belongs to the subscription
// that asked for it and to the run currently selected. Out-of-order polls are discarded
// rather than allowed to move a terminal run back to Running.

export type CanvasRunPanelProps = {
  definitionCode: string | null;
  publishedVersion: number | null;
  requestedBy?: string | null;
  pollIntervalMs?: number;
};

type PanelState = {
  capability: CanvasExecutionCapability | null;
  binding: CanvasJobBinding | null;
  run: CanvasRunEvidence | null;
  busy: boolean;
  message: string | null;
};

const initial: PanelState = { capability: null, binding: null, run: null, busy: false, message: null };

export function CanvasRunPanel(props: CanvasRunPanelProps) {
  const { definitionCode, publishedVersion, requestedBy, pollIntervalMs } = props;
  const [state, setState] = useState<PanelState>(initial);

  // The subscription generation. Every asynchronous answer carries the generation it was
  // asked under; anything older is dropped on arrival.
  const generation = useRef(0);
  const correlation = useRef<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const stopPolling = useCallback(() => {
    if (timer.current !== null) {
      clearTimeout(timer.current);
      timer.current = null;
    }
  }, []);

  // A new definition is a new subscription: the old generation is abandoned and no
  // answer still in flight for the previous definition can land on this one.
  useEffect(() => {
    generation.current = generation.current + 1;
    correlation.current = null;
    stopPolling();
    setState(initial);

    if (!definitionCode) { return; }

    const mine = generation.current;

    void (async () => {
      try {
        const capability = await describeExecution(definitionCode,
          publishedVersion === null ? undefined : publishedVersion);
        if (generation.current !== mine) { return; }
        setState((s) => ({ ...s, capability }));
      } catch {
        if (generation.current !== mine) { return; }
        setState((s) => ({ ...s, message: "Execution capability could not be read." }));
      }

      try {
        const binding = await getCanvasJobBinding(definitionCode);
        if (generation.current !== mine) { return; }
        setState((s) => ({ ...s, binding }));
      } catch {
        // Not bound yet is a normal state for a definition nobody has run.
        if (generation.current !== mine) { return; }
        setState((s) => ({ ...s, binding: null }));
      }
    })();

    return stopPolling;
  }, [definitionCode, publishedVersion, stopPolling]);

  const applyRun = useCallback((mine: number, next: CanvasRunEvidence | null) => {
    if (generation.current !== mine || next === null) { return; }

    setState((s) => {
      // NEVER REGRESS. A late answer about the run we are watching cannot undo a
      // terminal state, and an answer about a different run is not ours to show.
      if (s.run && s.run.runId !== next.runId) { return s; }
      if (s.run && s.run.isTerminal && !next.isTerminal) { return s; }
      return { ...s, run: next };
    });
  }, []);

  const poll = useCallback((mine: number, code: string, runId: string | null, corr: string | null) => {
    stopPolling();

    timer.current = setTimeout(() => {
      void (async () => {
        if (generation.current !== mine) { return; }

        try {
          const next = runId
            ? await readCanvasRun(code, runId)
            : (corr ? await findCanvasRun(code, corr) : null);

          applyRun(mine, next);

          if (generation.current !== mine) { return; }
          if (next && next.isTerminal) { return; }

          poll(mine, code, next ? next.runId : runId, corr);
        } catch {
          if (generation.current !== mine) { return; }
          // A failed poll is a lost answer, not a failed run. Keep watching.
          poll(mine, code, runId, corr);
        }
      })();
    }, pollIntervalMs ?? 1500);
  }, [applyRun, pollIntervalMs, stopPolling]);

  const doBind = useCallback(async () => {
    if (!definitionCode) { return; }
    const mine = generation.current;
    setState((s) => ({ ...s, busy: true, message: null }));

    try {
      const binding = await bindCanvasJob(definitionCode, { pinnedVersion: publishedVersion });
      if (generation.current !== mine) { return; }
      setState((s) => ({ ...s, binding, busy: false, message: "Bound to job " + binding.jobCode + "." }));
    } catch {
      if (generation.current !== mine) { return; }
      setState((s) => ({ ...s, busy: false, message: "The binding was refused. Nothing was launched." }));
    }
  }, [definitionCode, publishedVersion]);

  const doLaunch = useCallback(async () => {
    if (!definitionCode) { return; }

    const mine = generation.current;
    const corr = "canvas-" + Date.now().toString(36) + "-" + Math.random().toString(36).slice(2, 10);
    correlation.current = corr;

    setState((s) => ({ ...s, busy: true, run: null, message: null }));
    poll(mine, definitionCode, null, corr);

    try {
      const finished = await launchCanvasRun(definitionCode, { correlationId: corr, requestedBy: requestedBy ?? null });
      applyRun(mine, finished);
      if (generation.current !== mine) { return; }
      setState((s) => ({ ...s, busy: false }));
      stopPolling();
    } catch {
      if (generation.current !== mine) { return; }
      setState((s) => ({ ...s, busy: false, message: "The launch was refused before a run was created." }));
      stopPolling();
    }
  }, [applyRun, definitionCode, poll, requestedBy, stopPolling]);

  const doCancel = useCallback(async () => {
    const run = state.run;
    if (!run) { return; }

    try {
      await requestCanvasRunCancellation(run.jobDefinitionId, run.runId, { requestedBy: requestedBy ?? null, reason: "Requested from the canvas." });
      // The request is recorded. The run is NOT cancelled until the authority says so,
      // so nothing here changes the displayed status.
      setState((s) => ({ ...s, message: "Cancellation requested. The run stops when the executor acknowledges it." }));
    } catch {
      setState((s) => ({ ...s, message: "The cancellation request was refused." }));
    }
  }, [requestedBy, state.run]);

  const capability = state.capability;
  const runnable = capability !== null && capability.staticallyEligible && state.binding !== null;

  return (
    <section className="canvas-run-panel" data-testid="canvas-run-panel">
      <h3 className="canvas-run-panel__title">Run</h3>

      {capability && (
        <p data-testid="canvas-run-capability" data-eligible={capability.staticallyEligible ? "yes" : "no"}>
          {capability.staticallyEligible
            ? "Version " + (capability.resolvedVersion ?? "?") + " can be executed by this runtime."
            : (capability.refusalCode ?? "UNAVAILABLE") + ": " + (capability.refusalDetail ?? "This definition cannot be executed here.")}
          <span className="canvas-run-panel__assurance"> {capability.assurance}</span>
        </p>
      )}

      <div className="canvas-run-panel__actions">
        <StandardP2Button variant="secondary" onClick={doBind} disabled={state.busy || !capability || !capability.staticallyEligible}>
          Bind to job
        </StandardP2Button>
        <StandardP2Button variant="primary" onClick={doLaunch} disabled={state.busy || !runnable}>
          Run now
        </StandardP2Button>
        <StandardP2Button variant="secondary" onClick={doCancel} disabled={!state.run || state.run.isTerminal}>
          Request cancellation
        </StandardP2Button>
      </div>

      {state.binding && (
        <p data-testid="canvas-run-binding">
          Job {state.binding.jobCode} targets {state.binding.targetKind}{" "}
          {state.binding.pinnedVersion === null ? "(current published)" : "version " + state.binding.pinnedVersion}.
        </p>
      )}

      {state.run && (
        <div data-testid="canvas-run-evidence" data-run-id={state.run.runId} data-status={state.run.status}>
          <p>
            Run {state.run.runId} executed version {state.run.targetDefinitionVersion ?? "?"} and is {state.run.status}.
            {state.run.cancellationRequested && !state.run.cancellationAcknowledgedAtUtc
              ? " Cancellation requested and not yet acknowledged."
              : ""}
          </p>
          {state.run.failureReason && <p data-testid="canvas-run-failure">{state.run.failureReason}</p>}
          <ol className="canvas-run-panel__blocks">
            {state.run.blocks.map((b) => (
              <li key={b.blockId} data-testid={"canvas-run-block-" + b.blockId} data-block-status={b.status}>
                {b.blockId}: {b.status}
                {b.outputRows === null ? "" : " (" + b.outputRows + " rows)"}
                {b.diagnosticCode ? " " + b.diagnosticCode + ": " + (b.diagnosticDetail ?? "") : ""}
              </li>
            ))}
          </ol>
        </div>
      )}

      {state.message && <p data-testid="canvas-run-message">{state.message}</p>}
    </section>
  );
}
