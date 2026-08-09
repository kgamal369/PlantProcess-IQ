// ============================================================
// FILE: src/components/assistant/AssistantChat.tsx
// M1-11: the conversation surface for the grounded assistant.
//
// Types come from @/api/assistantApi - this component used to redeclare them,
// which meant the wire contract and the UI could drift apart silently.
//
// T-075: a citation is now an accessible chip that opens the REAL evidence.
// The header of this file used to say "there is no evidence-row route in this
// application yet. Showing the handle is honest; pretending to open a row that
// does not exist is not." That was true when it was written. T-073 built the
// persisted snapshot and the tenant-scoped endpoint, so the row now exists and
// this surface opens it.
//
// Two rules the strip keeps:
//   - it renders the RESOLVED evidence payload, never the answer prose;
//   - unavailable evidence and a failed request are different states, and are
//     never dressed as one another.
// ============================================================
import { useCallback, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { StandardButton, StandardTable, type StandardTableColumn } from "@/components/standard";
import { StandardP2Input } from "@/components/standard/StandardP2Controls";
import { assistantApi } from "@/api/assistantApi";
import type { AssistantAnswer, AssistantCitation, AssistantWidgetResultEvidence } from "@/api/assistantApi";
import {
  NO_STARTER_PROMPT,
  WIDGET_RESULT_KIND,
  chipLabel,
  citationKey,
  openInPageHref,
  stripFields,
  type EvidenceState,
} from "./assistantEvidence";
import "./AssistantChat.css";

export type Turn = { role: "user" | "assistant"; answer?: AssistantAnswer; text?: string; error?: string };

export type EvidenceLoader = (evidenceId: string) => Promise<AssistantWidgetResultEvidence | null>;

export function AssistantChat({
  turns,
  chips = [],
  starters = [],
  isBusy = false,
  onAsk,
  onOpenEvidence,
  loadEvidence,
}: {
  turns: Turn[];
  chips?: string[];
  /** T-075: derived from the CURRENT page and widget context by the caller. */
  starters?: string[];
  isBusy?: boolean;
  onAsk?: (question: string) => void;
  onOpenEvidence?: (handle: AssistantCitation) => void;
  loadEvidence?: EvidenceLoader;
}) {
  const [text, setText] = useState("");

  const submit = (question: string) => {
    const trimmed = question.trim();
    if (trimmed.length === 0 || isBusy) return;
    onAsk?.(trimmed);
    setText("");
  };

  return (
    <div className="ppiq-assistant-chat" data-testid="assistant-chat">
      {chips.length > 0 ? (
        <div className="ppiq-assistant-chat__chips" data-testid="context-chips">
          {chips.map((c) => (
            <span className="ppiq-assistant-chat__chip" key={c}>
              {c}
            </span>
          ))}
        </div>
      ) : null}

      <div className="ppiq-assistant-chat__transcript" role="log" aria-live="polite">
        {turns.length === 0 ? (
          <p className="ppiq-assistant-chat__empty">
            Ask a question about your plant data. The assistant answers only from evidence it can
            cite, and abstains when the evidence is insufficient.
          </p>
        ) : null}

        {turns.map((t, i) =>
          t.role === "user" ? (
            <div className="ppiq-assistant-chat__turn ppiq-assistant-chat__turn--user" key={i}>
              {t.text}
            </div>
          ) : t.error ? (
            <div
              className="ppiq-assistant-chat__turn ppiq-assistant-chat__turn--error"
              data-testid="assistant-technical-error"
              key={i}
            >
              <strong>Request failed</strong>
              <p>
                The assistant could not be reached. This is a technical fault, not a
                judgement about the evidence.
              </p>
              <code>{t.error}</code>
            </div>
          ) : t.answer ? (
            <AssistantBubble
              key={i}
              index={i}
              answer={t.answer}
              onOpenEvidence={onOpenEvidence}
              loadEvidence={loadEvidence}
            />
          ) : null,
        )}
      </div>

      {/* T-075: starters come from the current context. The retired global list
          was three questions that were true on no particular page. */}
      {turns.length === 0 ? (
        <div className="ppiq-assistant-chat__suggestions" data-testid="assistant-starters">
          {starters.length > 0 ? (
            starters.map((s) => (
              <StandardButton key={s} type="button" isDisabled={isBusy} onClick={() => submit(s)}>
                {s}
              </StandardButton>
            ))
          ) : (
            <p className="ppiq-assistant-chat__empty" data-testid="assistant-no-starters">
              {NO_STARTER_PROMPT}
            </p>
          )}
        </div>
      ) : null}

      <div className="ppiq-assistant-chat__composer">
        <StandardP2Input
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              submit(text);
            }
          }}
          placeholder="Ask about your approved findings..."
        />
        <StandardButton
          className="ppiq-std-button--primary"
          type="button"
          isDisabled={isBusy || text.trim().length === 0}
          isLoading={isBusy}
          onClick={() => submit(text)}
        >
          Ask
        </StandardButton>
      </div>
    </div>
  );
}

function AssistantBubble({
  index,
  answer,
  onOpenEvidence,
  loadEvidence,
}: {
  index: number;
  answer: AssistantAnswer;
  onOpenEvidence?: (h: AssistantCitation) => void;
  loadEvidence?: EvidenceLoader;
}) {
  const [openKey, setOpenKey] = useState<string | null>(null);
  const [evidence, setEvidence] = useState<Record<string, EvidenceState>>({});
  const navigate = useNavigate();

  const fetcher = useMemo<EvidenceLoader>(
    () => loadEvidence ?? ((id: string) => assistantApi.getWidgetResultEvidence(id)),
    [loadEvidence],
  );

  const toggle = useCallback(
    (citation: AssistantCitation) => {
      const key = citationKey(citation);

      if (openKey === key) {
        setOpenKey(null);
        return;
      }

      // One strip at a time.
      setOpenKey(key);
      onOpenEvidence?.(citation);

      if (citation.kind !== WIDGET_RESULT_KIND) return;

      const existing = evidence[key];
      // Already resolved once in this conversation. A failed attempt is retried,
      // because a transport fault is not an answer about the evidence.
      if (existing && existing.status !== "failed") return;

      setEvidence((current) => ({ ...current, [key]: { status: "loading" } }));

      fetcher(citation.id)
        .then((resolved) => {
          setEvidence((current) => ({
            ...current,
            [key]: resolved
              ? { status: "loaded", evidence: resolved }
              : { status: "unavailable", reason: "This evidence is not available to your tenant." },
          }));
        })
        .catch((error: Error) => {
          setEvidence((current) => ({
            ...current,
            [key]: { status: "failed", reason: error.message },
          }));
        });
    },
    [openKey, evidence, fetcher, onOpenEvidence],
  );

  if (answer.isRefusal) {
    return (
      <div className="ppiq-assistant-chat__turn ppiq-assistant-chat__turn--refusal" data-testid="assistant-refusal">
        <strong>Insufficient evidence</strong>
        <p>{answer.refusalReason ?? "I cannot answer that from approved, in-scope evidence."}</p>
      </div>
    );
  }

  return (
    <div className="ppiq-assistant-chat__turn ppiq-assistant-chat__turn--answer" data-testid="assistant-answer">
      <p>{answer.text}</p>

      {answer.citations.length > 0 ? (
        <div className="ppiq-assistant-chat__citations">
          {answer.citations.map((h, i) => {
            const key = citationKey(h);
            const isOpen = openKey === key;
            const stripId = "ppiq-evidence-" + index + "-" + i;

            return (
              <div key={key + ":" + i}>
                <StandardButton
                  type="button"
                  data-testid="assistant-citation"
                  aria-expanded={isOpen}
                  aria-controls={stripId}
                  title={h.kind + " " + h.id}
                  onClick={() => toggle(h)}
                >
                  {chipLabel(h)}
                </StandardButton>

                {isOpen ? (
                  <EvidenceStrip
                    id={stripId}
                    citation={h}
                    state={evidence[key]}
                    onOpenInPage={(href) => navigate(href)}
                  />
                ) : null}
              </div>
            );
          })}
        </div>
      ) : (
        <p className="ppiq-assistant-chat__nocite">No citations were returned for this answer.</p>
      )}

      {answer.blocked && answer.blocked.length > 0 ? (
        <div className="ppiq-assistant-chat__blocked">
          <strong>Blocked unsupported claims</strong>
          <ul>
            {answer.blocked.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}

/**
 * T-075: one evidence row, carrying its own deterministic key.
 *
 * The cells stay an ordered array because the evidence contract is ordered
 * columns and ordered rows. Turning them into named properties would mean
 * inventing property names the evidence never had.
 */
type EvidenceRow = { key: string; cells: string[] };

/** Real column names from the snapshot, never renamed or interpreted. */
function evidenceColumns(columns: string[]): StandardTableColumn<EvidenceRow>[] {
  return columns.map((name, index) => ({
    key: "evidence-column-" + index,
    header: name,
    accessor: (row: EvidenceRow) => row.cells[index] ?? "",
  }));
}

/** Deterministic keys: the evidence identity plus the row's own index. */
function evidenceRows(evidenceId: string, rows: string[][]): EvidenceRow[] {
  return rows.slice(0, MAX_EVIDENCE_ROWS).map((cells, index) => ({
    key: evidenceId + ":" + index,
    cells,
  }));
}

/** A strip, not a grid. Enough rows to show the shape of the result. */
const MAX_EVIDENCE_ROWS = 8;

function EvidenceStrip({
  id,
  citation,
  state,
  onOpenInPage,
}: {
  id: string;
  citation: AssistantCitation;
  state?: EvidenceState;
  onOpenInPage: (href: string) => void;
}) {
  // A kind with no detailed evidence endpoint shows what is truthfully known
  // about it, and says so. Inventing rows to make every chip look equally rich
  // would be the exact opposite of what a citation is for.
  if (citation.kind !== WIDGET_RESULT_KIND) {
    return (
      <div className="ppiq-assistant-chat__evidence" id={id} data-testid="assistant-evidence">
        <dl>
          <dt>Evidence kind</dt>
          <dd>{citation.kind}</dd>
          <dt>Evidence id</dt>
          <dd>{citation.id}</dd>
        </dl>
        <p data-testid="assistant-evidence-nodetail">
          Detailed evidence is not available on this surface.
        </p>
      </div>
    );
  }

  if (!state || state.status === "loading") {
    return (
      <div className="ppiq-assistant-chat__evidence" id={id} data-testid="assistant-evidence">
        <p data-testid="assistant-evidence-loading">Loading evidence...</p>
      </div>
    );
  }

  if (state.status === "unavailable") {
    return (
      <div className="ppiq-assistant-chat__evidence" id={id} data-testid="assistant-evidence">
        <p data-testid="assistant-evidence-unavailable">{state.reason}</p>
      </div>
    );
  }

  if (state.status === "failed") {
    return (
      <div className="ppiq-assistant-chat__evidence" id={id} data-testid="assistant-evidence">
        <p data-testid="assistant-evidence-failed">
          The evidence could not be loaded. This is a technical fault, not a judgement
          about the evidence.
        </p>
        <code>{state.reason}</code>
      </div>
    );
  }

  const resolved = state.evidence;
  const href = openInPageHref(resolved);

  return (
    <div className="ppiq-assistant-chat__evidence" id={id} data-testid="assistant-evidence">
      <p className="ppiq-assistant-chat__evidence-sentence" data-testid="assistant-evidence-sentence">
        {resolved.sentence}
      </p>

      <dl>
        {stripFields(resolved).map((field) => (
          <div key={field.label}>
            <dt>{field.label}</dt>
            <dd>{field.value}</dd>
          </div>
        ))}
      </dl>

      {/* T-075: the canonical StandardTable, deliberately minimal.
          Headers are the evidence's OWN column names - nothing is renamed and no
          semantic column is invented. Row keys are derived from the evidence
          identity and the row index, so they are deterministic rather than
          positional accidents. No sorting, filtering, export or pagination: this
          is an evidence strip, not a data grid. */}
      {resolved.rows.length > 0 ? (
        <div className="ppiq-assistant-chat__evidence-rows" data-testid="assistant-evidence-rows">
          <StandardTable
            columns={evidenceColumns(resolved.columns)}
            data={evidenceRows(resolved.evidenceId, resolved.rows)}
            getRowKey={(row) => row.key}
          />
        </div>
      ) : (
        <p data-testid="assistant-evidence-norows">
          This evidence snapshot returned no rows.
        </p>
      )}

      {href ? (
        <StandardButton
          type="button"
          data-testid="assistant-open-in-page"
          data-href={href}
          onClick={() => onOpenInPage(href)}
        >
          Open in page
        </StandardButton>
      ) : null}
    </div>
  );
}