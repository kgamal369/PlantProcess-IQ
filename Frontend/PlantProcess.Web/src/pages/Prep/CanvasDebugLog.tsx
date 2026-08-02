
import { useCallback, useMemo, useRef, useState } from "react";
import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import "./CanvasDebugLog.css";

/**
 * M1-17. The debug log of Authoring Layer Specification section 13 and
 * Constitution v3 II.6.3.
 *
 * Three severities, each carrying a WRITTEN DESCRIPTION:
 *   ERROR   - which block or wire, which rule was broken, what would fix it
 *   WARNING - what the risk is, in plain language
 *   SUCCESS - rows returned, columns, cost estimate
 *
 * Why it is a panel and never a toast: the customer's engineer authors the
 * mapping and there is no vendor engineer beside him to interpret a red
 * outline. A refusal that vanishes after four seconds is a refusal he cannot
 * act on. The log is the support model, not a nicety.
 *
 * The five drag-time refusal sentences and the run-time unjoined-table
 * sentence move in here UNCHANGED. They were written to this shape on 27-Jul,
 * which is why VisualJoinCanvasPage carries the note "when the debug log lands
 * these sentences move into it unchanged".
 */

export type DebugSeverity = "error" | "warning" | "success";

export interface DebugEntry {
  /** Monotonic, so React keys never collide when two entries share a second. */
  id: number;
  /** Wall-clock HH:MM:SS. The engineer correlates this with what he just did. */
  at: string;
  severity: DebugSeverity;
  /**
   * WHICH block or wire. Empty for definition-wide events such as publish.
   * Kept separate from the sentence so the sentence stays quotable.
   */
  subject: string;
  /** The sentence. Never truncated, never a status code, never "invalid". */
  message: string;
  /**
   * Measured facts for SUCCESS - rows, columns, elapsed. Free text so the
   * caller decides what it can honestly state.
   */
  facts?: string;
}

const MAX_ENTRIES = 200;

function stamp(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, "0");
  return p(d.getHours()) + ":" + p(d.getMinutes()) + ":" + p(d.getSeconds());
}

export interface DebugLogApi {
  entries: DebugEntry[];
  error: (subject: string, message: string) => void;
  warning: (subject: string, message: string) => void;
  success: (subject: string, message: string, facts?: string) => void;
  clear: () => void;
  /** The newest entry, for the compact one-line status that already exists. */
  latest: DebugEntry | null;
}

export function useDebugLog(): DebugLogApi {
  const [entries, setEntries] = useState<DebugEntry[]>([]);
  const nextId = useRef(1);

  const push = useCallback(
    (severity: DebugSeverity, subject: string, message: string, facts?: string) => {
      setEntries((rows) => {
        const entry: DebugEntry = {
          id: nextId.current++,
          at: stamp(),
          severity,
          subject,
          message,
          facts,
        };
        // Newest first: the engineer reads the top line, not the bottom one.
        const next = [entry, ...rows];
        return next.length > MAX_ENTRIES ? next.slice(0, MAX_ENTRIES) : next;
      });
    },
    [],
  );

  const error = useCallback((s: string, m: string) => push("error", s, m), [push]);
  const warning = useCallback((s: string, m: string) => push("warning", s, m), [push]);
  const success = useCallback(
    (s: string, m: string, f?: string) => push("success", s, m, f),
    [push],
  );
  const clear = useCallback(() => setEntries([]), []);

  return useMemo(
    () => ({ entries, error, warning, success, clear, latest: entries[0] ?? null }),
    [entries, error, warning, success, clear],
  );
}

const SEVERITY_LABEL: Record<DebugSeverity, string> = {
  error: "ERROR",
  warning: "WARNING",
  success: "SUCCESS",
};

export function CanvasDebugLog({ log }: { log: DebugLogApi }) {
  const counts = useMemo(() => {
    let e = 0;
    let w = 0;
    let s = 0;
    for (const row of log.entries) {
      if (row.severity === "error") { e += 1; }
      else if (row.severity === "warning") { w += 1; }
      else { s += 1; }
    }
    return { e, w, s };
  }, [log.entries]);

  return (
    <section className="dbglog" data-testid="canvas-debug-log" aria-label="Debug log">
      <header className="dbglog__head">
        <span className="dbglog__title">Debug log</span>
        <span className="dbglog__count dbglog__count--error" data-testid="dbglog-count-error">
          {counts.e} error
        </span>
        <span className="dbglog__count dbglog__count--warning">{counts.w} warning</span>
        <span className="dbglog__count dbglog__count--success">{counts.s} success</span>
        <span className="dbglog__spacer" />
        <StandardP2Button
          variant="ghost"
          type="button"
          className="dbglog__clear"
          onClick={log.clear}
          disabled={log.entries.length === 0}
        >
          Clear
        </StandardP2Button>
      </header>

      <div className="dbglog__body" role="log" aria-live="polite">
        {log.entries.length === 0 && (
          <div className="dbglog__empty">
            Nothing yet. Every refused wire, every preview and every publish is
            written here with the reason, and stays until you clear it.
          </div>
        )}

        {log.entries.map((row) => (
          <div
            key={row.id}
            className={"dbglog__row dbglog__row--" + row.severity}
            data-severity={row.severity}
          >
            <span className="dbglog__at">{row.at}</span>
            <span className={"dbglog__sev dbglog__sev--" + row.severity}>
              {SEVERITY_LABEL[row.severity]}
            </span>
            <span className="dbglog__text">
              {row.subject && <span className="dbglog__subject">{row.subject}</span>}
              <span className="dbglog__message">{row.message}</span>
              {row.facts && <span className="dbglog__facts">{row.facts}</span>}
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}