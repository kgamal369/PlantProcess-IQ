import { useState } from "react";
import { StandardButton } from "@/components/standard";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Input } from "@/components/standard/StandardP2Controls";
export type ProvenanceHandleRef = { kind: string; id: string; detail?: string | null };

export type AssistantAnswer = {
  isRefusal: boolean;
  refusalReason?: string | null;
  text: string;
  citations: ProvenanceHandleRef[];
  blocked?: string[];
};

export type Turn = { role: "user" | "assistant"; answer?: AssistantAnswer; text?: string };

const SUGGESTED = ["Summarise the top risk this week", "What is associated with edge-crack defects?", "Show open suggestions by impact"];

export function AssistantChat({
  turns,
  chips = [],
  onAsk,
  onOpenEvidence,
}: {
  turns: Turn[];
  chips?: string[];
  onAsk?: (question: string) => void;
  onOpenEvidence?: (handle: ProvenanceHandleRef) => void;
}) {
  const [text, setText] = useState("");

  return (
    <div data-testid="assistant-chat">
      {chips.length > 0 ? (
        <div data-testid="context-chips">
          {chips.map((c) => (
            <span key={c}>{c}</span>
          ))}
        </div>
      ) : null}

      <div>
        {turns.map((t, i) =>
          t.role === "user" ? (
            <div key={i}>{t.text}</div>
          ) : (
            <AssistantBubble key={i} answer={t.answer!} onOpenEvidence={onOpenEvidence} />
          ),
        )}
      </div>

      <div>
        {SUGGESTED.map((s) => (
          <StandardButton key={s} type="button" onClick={() => onAsk?.(s)}>{s}</StandardButton>
        ))}
      </div>

      <div>
        <StandardP2Input
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Ask about your approved findings..."
        />
        <StandardButton type="button" onClick={() => { if (text.trim()) { onAsk?.(text.trim()); setText(""); } }}>Ask</StandardButton>
      </div>
    </div>
  );
}

function AssistantBubble({ answer, onOpenEvidence }: { answer: AssistantAnswer; onOpenEvidence?: (h: ProvenanceHandleRef) => void }) {
  if (answer.isRefusal) {
    return (
      <div data-testid="assistant-refusal">
        <strong>Insufficient evidence</strong>
        <p>{answer.refusalReason ?? "I can't answer that from approved, in-scope evidence."}</p>
      </div>
    );
  }
  return (
    <div data-testid="assistant-answer">
      <p>{answer.text}</p>
      {answer.citations.length > 0 ? (
        <div>
          {answer.citations.map((h, i) => (
            <StandardButton key={i} type="button" data-testid="assistant-citation" onClick={() => onOpenEvidence?.(h)}>
              Source: {h.kind} ({h.id.slice(0, 8)})
            </StandardButton>
          ))}
        </div>
      ) : null}
    </div>
  );
}

const chip: React.CSSProperties = { fontSize: 12, background: "#1b2030", border: "1px solid #2a3142", borderRadius: 8, padding: "4px 8px", color: "#9fb4d8", cursor: "pointer" };