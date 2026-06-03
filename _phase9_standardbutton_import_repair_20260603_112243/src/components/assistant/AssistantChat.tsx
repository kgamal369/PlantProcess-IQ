import { useState } from "react";

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
    <div data-testid="assistant-chat" style={{ background: "#0f1115", color: "#e6e9ef", borderRadius: 12, padding: 16 }}>
      {chips.length > 0 ? (
        <div style={{ display: "flex", gap: 6, marginBottom: 10 }} data-testid="context-chips">
          {chips.map((c) => (
            <span key={c} style={{ fontSize: 11, background: "#1b2030", border: "1px solid #2a3142", borderRadius: 999, padding: "2px 8px" }}>{c}</span>
          ))}
        </div>
      ) : null}

      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {turns.map((t, i) =>
          t.role === "user" ? (
            <div key={i} style={{ alignSelf: "flex-end", background: "#1f6feb", borderRadius: 10, padding: "8px 12px", maxWidth: "80%" }}>{t.text}</div>
          ) : (
            <AssistantBubble key={i} answer={t.answer!} onOpenEvidence={onOpenEvidence} />
          ),
        )}
      </div>

      <div style={{ display: "flex", gap: 6, marginTop: 10, flexWrap: "wrap" }}>
        {SUGGESTED.map((s) => (
          <StandardButton key={s} type="button" onClick={() => onAsk?.(s)} style={chip}>{s}</StandardButton>
        ))}
      </div>

      <div style={{ display: "flex", gap: 8, marginTop: 10 }}>
        <input
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Ask about your approved findings..."
          style={{ flex: 1, background: "#161a22", border: "1px solid #2a3142", borderRadius: 8, padding: "8px 10px", color: "#e6e9ef" }}
        />
        <StandardButton type="button" onClick={() => { if (text.trim()) { onAsk?.(text.trim()); setText(""); } }} style={{ ...chip, background: "#1f6feb", color: "#fff" }}>Ask</StandardButton>
      </div>
    </div>
  );
}

function AssistantBubble({ answer, onOpenEvidence }: { answer: AssistantAnswer; onOpenEvidence?: (h: ProvenanceHandleRef) => void }) {
  if (answer.isRefusal) {
    return (
      <div data-testid="assistant-refusal" style={{ alignSelf: "flex-start", background: "#241b1b", border: "1px solid #5b2f2f", borderRadius: 10, padding: "8px 12px", maxWidth: "85%" }}>
        <strong style={{ color: "#e0a3a3", fontSize: 12 }}>Insufficient evidence</strong>
        <p style={{ margin: "4px 0 0", fontSize: 13 }}>{answer.refusalReason ?? "I can't answer that from approved, in-scope evidence."}</p>
      </div>
    );
  }
  return (
    <div data-testid="assistant-answer" style={{ alignSelf: "flex-start", background: "#161a22", border: "1px solid #2a3142", borderRadius: 10, padding: "10px 12px", maxWidth: "85%" }}>
      <p style={{ margin: 0, fontSize: 14 }}>{answer.text}</p>
      {answer.citations.length > 0 ? (
        <div style={{ marginTop: 8, display: "flex", flexDirection: "column", gap: 4 }}>
          {answer.citations.map((h, i) => (
            <StandardButton key={i} type="button" data-testid="assistant-citation" onClick={() => onOpenEvidence?.(h)} style={{ ...chip, textAlign: "left" }}>
              Source: {h.kind} ({h.id.slice(0, 8)})
            </StandardButton>
          ))}
        </div>
      ) : null}
    </div>
  );
}

const chip: React.CSSProperties = { fontSize: 12, background: "#1b2030", border: "1px solid #2a3142", borderRadius: 8, padding: "4px 8px", color: "#9fb4d8", cursor: "pointer" };