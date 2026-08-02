
import { useMemo } from "react";
import type {
  AdvancedReadinessGateDto,
  AdvancedReadinessGateSummaryDto,
} from "@/api/advancedAnalysis";
import "./GateReadinessPanel.css";

/**
 * M1-18. The readiness gate, made visible.
 *
 * Constitution v3 II.8.5: the readiness panel is shown to customers
 * DELIBERATELY. Four green dimensions with real measured numbers and one honest
 * red naming a specific data deficiency is a stronger demonstration than a
 * fabricated result, because it tells the customer something true about their
 * own data. No competitor in this market shows a prospect a red status.
 *
 * The engine has never completed a run on this dataset. That is not a hole in
 * the demonstration - it IS the demonstration, and until now it had no picture.
 *
 * WHAT THE SERVER GIVES US, and what it does not.
 * AdvancedReadinessGateDto carries gateCode, title, state, reason, evidence and
 * isBlocking. It carries NO numeric value and NO numeric threshold: per
 * Constitution II.8.3 both live inside `reason`, which is required to contain
 * the measured value, the threshold and the verdict as a human-readable
 * sentence. So the sentence is rendered verbatim - it is the honest artefact -
 * and the bar's fill is derived from that sentence where two numbers can be
 * read out of it, falling back to a state bucket where they cannot. Nothing is
 * invented, and readBucket() says so at the point it gives up.
 */

type GateState = "Ready" | "Partial" | "Blocked";

function normaliseState(state: string): GateState {
  const s = (state ?? "").toLowerCase();
  if (s === "ready") { return "Ready"; }
  if (s === "partial") { return "Partial"; }
  return "Blocked";
}

/** Fallback fill when the reason carries no readable pair of numbers. */
const STATE_BUCKET: Record<GateState, number> = { Ready: 10, Partial: 5, Blocked: 1 };

/**
 * Returns 0..10, the bucket index used as a CSS class. Bucketed rather than a
 * width percentage because the UI conformance ratchet forbids inline style
 * objects, and a bucketed class is also honest about the precision we have.
 */
export function readBucket(gate: AdvancedReadinessGateDto): number {
  const state = normaliseState(gate.state);
  const text = (gate.reason ?? "") + " " + (gate.evidence ?? "");
  // Two numbers, in order: the measured value then the threshold. Percent signs
  // and thousands separators are tolerated; anything else and we do not guess.
  const numbers = (text.match(/\d[\d.,]*/g) ?? [])
    .map((n) => Number(n.replace(/,/g, "")))
    .filter((n) => Number.isFinite(n));
  if (numbers.length < 2) { return STATE_BUCKET[state]; }
  const [value, threshold] = numbers;
  if (threshold <= 0) { return STATE_BUCKET[state]; }
  const ratio = value / threshold;
  if (!Number.isFinite(ratio)) { return STATE_BUCKET[state]; }
  return Math.max(0, Math.min(10, Math.round(ratio * 10)));
}

export interface GateReadinessPanelProps {
  summary: AdvancedReadinessGateSummaryDto | null;
  /** True when the gates endpoint refused or was unreachable. */
  failed?: boolean;
  /** Present only after a run has been submitted from this page. */
  runId?: string | null;
}

export function GateReadinessPanel({ summary, failed, runId }: GateReadinessPanelProps) {
  const gates = useMemo(() => summary?.gates ?? [], [summary]);

  if (failed) {
    return (
      <section className="gaterp" data-testid="gate-readiness-panel">
        <div className="gaterp__unavailable">
          The gate summary did not answer for this outcome, grain and window.
          That is a transport failure, not a verdict about the data - the engine
          has not said anything about readiness either way.
        </div>
      </section>
    );
  }

  if (!summary) {
    return (
      <section className="gaterp" data-testid="gate-readiness-panel">
        <div className="gaterp__loading">Reading gates...</div>
      </section>
    );
  }

  const overall = normaliseState(summary.state);

  return (
    <section className="gaterp" data-testid="gate-readiness-panel">
      <header className="gaterp__head">
        <span className={"gaterp__chip gaterp__chip--" + overall.toLowerCase()}>
          {overall.toUpperCase()}
        </span>
        <span className="gaterp__counts">
          {summary.readyCount} ready / {summary.partialCount} partial / {summary.blockedCount} blocked
        </span>
        <span className="gaterp__spacer" />
        <span className="gaterp__population">
          {summary.independentHeats.toLocaleString()} independent heats,{" "}
          {summary.outcomeEvents.toLocaleString()} outcome events
        </span>
      </header>

      <div className="gaterp__rows">
        {gates.length === 0 && (
          <div className="gaterp__loading">
            The endpoint answered with no dimensions. Nothing is being asserted
            about this dataset.
          </div>
        )}

        {gates.map((gate) => {
          const state = normaliseState(gate.state);
          const bucket = readBucket(gate);
          return (
            <div
              className="gaterp__row"
              key={gate.gateCode}
              data-gate={gate.gateCode}
              data-state={state}
            >
              <div className="gaterp__rowhead">
                <span className="gaterp__title">{gate.title}</span>
                {gate.isBlocking && state !== "Ready" && (
                  <span className="gaterp__blocking">blocking</span>
                )}
                <span className="gaterp__spacer" />
                <span className={"gaterp__chip gaterp__chip--" + state.toLowerCase()}>
                  {state.toUpperCase()}
                </span>
              </div>

              <div className={"gaterp__bar gaterp__bar--" + state.toLowerCase()}>
                <span className={"gaterp__fill gaterp__fill--b" + bucket} />
              </div>

              {/* Verbatim. Per II.8.3 this sentence carries the measured value,
                  the threshold and the verdict, and it is what the customer is
                  meant to read. Paraphrasing it would be the dishonest move. */}
              <div className="gaterp__reason">{gate.reason}</div>

              {gate.evidence && (
                <div className="gaterp__evidence">{gate.evidence}</div>
              )}
            </div>
          );
        })}
      </div>

      <footer className="gaterp__foot">
        <span className="gaterp__message">{summary.message}</span>
        {runId && <span className="gaterp__runid">run id: {runId}</span>}
        {!summary.canRun && (
          <span className="gaterp__refusal">
            The engine will refuse this run and record it with a real run id and
            the reason above. It will not compute a number it cannot defend.
          </span>
        )}
      </footer>
    </section>
  );
}