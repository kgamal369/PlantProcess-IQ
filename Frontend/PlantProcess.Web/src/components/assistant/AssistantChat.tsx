// ============================================================
// FILE: src/components/assistant/AssistantChat.tsx
// M1-11: the conversation surface for the grounded assistant.
//
// Types come from @/api/assistantApi - this component used to redeclare them,
// which meant the wire contract and the UI could drift apart silently.
//
// Clicking a citation EXPANDS its provenance handle inline. It does not
// navigate: there is no evidence-row route in this application yet. Showing the
// handle is honest; pretending to open a row that does not exist is not.
// ============================================================
import { useState } from "react";
import { StandardButton } from "@/components/standard";
import { StandardP2Input } from "@/components/standard/StandardP2Controls";
import type { AssistantAnswer, AssistantCitation } from "@/api/assistantApi";

export type Turn = { role: "user" | "assistant"; answer?: AssistantAnswer; text?: string };

const SUGGESTED = [
  "Summarise the top risk this week",
  "What is associated with edge-crack defects?",
  "Show open suggestions by impact",
];

export function AssistantChat({
  turns,
  chips = [],
  isBusy = false,
  onAsk,
  onOpenEvidence,
}: {
  turns: Turn[];
  chips?: string[];
  isBusy?: boolean;
  onAsk?: (question: string) => void;
  onOpenEvidence?: (handle: AssistantCitation) => void;
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
          ) : t.answer ? (
            <AssistantBubble key={i} answer={t.answer} onOpenEvidence={onOpenEvidence} />
          ) : null,
        )}
      </div>

      <div className="ppiq-assistant-chat__suggestions">
        {SUGGESTED.map((s) => (
          <StandardButton key={s} type="button" isDisabled={isBusy} onClick={() => submit(s)}>
            {s}
          </StandardButton>
        ))}
      </div>

      <div className="ppiq-assistant-chat__composer">
        <StandardP2Input
          value={text}
          onChange={(e) => setText(e.target.value)}
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
  answer,
  onOpenEvidence,
}: {
  answer: AssistantAnswer;
  onOpenEvidence?: (h: AssistantCitation) => void;
}) {
  const [openHandle, setOpenHandle] = useState<string | null>(null);

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
            const key = h.kind + ":" + h.id;
            const isOpen = openHandle === key;
            return (
              <div key={key + ":" + i}>
                <StandardButton
                  type="button"
                  data-testid="assistant-citation"
                  onClick={() => {
                    setOpenHandle(isOpen ? null : key);
                    onOpenEvidence?.(h);
                  }}
                >
                  Source: {h.kind} ({h.id.slice(0, 8)})
                </StandardButton>
                {isOpen ? (
                  <dl className="ppiq-assistant-chat__evidence" data-testid="assistant-evidence">
                    <dt>Kind</dt>
                    <dd>{h.kind}</dd>
                    <dt>Identifier</dt>
                    <dd>{h.id}</dd>
                    {h.detail ? (
                      <>
                        <dt>Detail</dt>
                        <dd>{h.detail}</dd>
                      </>
                    ) : null}
                  </dl>
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