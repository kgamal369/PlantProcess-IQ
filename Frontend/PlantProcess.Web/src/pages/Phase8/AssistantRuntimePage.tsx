// ============================================================
// FILE: src/pages/Phase8/AssistantRuntimePage.tsx
// M1-11: THE assistant page. Routed at /assistant.
//
// Renders <AssistantChat/> against the shared dock conversation. Since T-071 it
// holds no state of its own: AssistantDockContext owns the turns and the single
// ask call. (The endpoint path is deliberately not written here: the M1-11 gate
// greps src/ for it to prove nothing bypasses the api client.)
//
// If no LLM provider is configured, the backend abstains and the abstention is
// shown as an abstention. Nothing is fabricated to fill the silence.
// ============================================================
import { assistantModeLabel, type AssistantCitation } from "@/api/assistantApi";
import { AssistantChat } from "@/components/assistant/AssistantChat";
import { ASSISTANT_CONTEXT_CHIPS, useAssistantDock } from "@/components/assistant/AssistantDockContext";
import { StandardStatGrid } from "@/components/standard";
import "./phase8-ai.css";

const CONTEXT_CHIPS = ASSISTANT_CONTEXT_CHIPS;

/* PPIQ-T071: this page is now a CONSUMER. The conversation, the configuration
   and the single assistantApi.askAssistant call live in AssistantDockContext,
   above the router outlet, so they survive navigation. /assistant and the dock
   are therefore one conversation rather than two. */
export function AssistantRuntimePage() {
  const { config, turns, busy, status, ask, setStatus } = useAssistantDock();

  function openEvidence(handle: AssistantCitation) {
    // No evidence-row route exists yet; AssistantChat expands the handle inline.
    // When a material/evidence route lands, navigate here instead.
    setStatus("Evidence handle: " + handle.kind + " " + handle.id);
  }

  const stats = [
    { label: "Mode", value: assistantModeLabel(config) },
    { label: "Grounding", value: config?.groundingPolicy ?? "pending" },
    { label: "Evidence policy", value: config?.evidencePolicy ?? "pending" },
    { label: "External egress", value: config?.noEgress ? "Blocked" : "Per configuration" },
  ];

  return (
    <main className="phase8-page" data-testid="assistant-runtime-page">
      <section className="phase8-hero">
        <p className="phase8-eyebrow">Ask questions about your plant data and receive answers with cited evidence.</p>
        <h1>Grounded Assistant</h1>
        <p className="phase8-muted">
          The assistant can answer only with grounded evidence. Missing evidence produces an
          abstention, not an invented number or an unsupported causal claim.
        </p>
        <strong className="phase8-badge">{status}</strong>
      </section>

      <StandardStatGrid items={stats} emphasize="Grounding" ariaLabel="Assistant runtime configuration" />

      <section className="phase8-two-col">
        <div className="phase8-card">
          <h2>Conversation</h2>
          <AssistantChat
            turns={turns}
            chips={CONTEXT_CHIPS}
            isBusy={busy}
            onAsk={(question) => void ask(question)}
            onOpenEvidence={openEvidence}
          />
        </div>

        <div className="phase8-card">
          <h2>Runtime guardrails</h2>
          <ul className="phase8-list">
            <li>Abstain when evidence is missing.</li>
            <li>Show citations and provenance handles.</li>
            <li>Block unsupported claims.</li>
            <li>No external egress unless configured through private endpoint mode.</li>
          </ul>
        </div>
      </section>
    </main>
  );
}

export default AssistantRuntimePage;