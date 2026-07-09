// ============================================================
// FILE: src/pages/Phase8/AssistantRuntimePage.tsx
// M1-11: THE assistant page. Routed at /assistant.
//
// Holds the conversation, renders <AssistantChat/>, and wires its onAsk to
// assistantApi.askAssistant(), which is the ONLY place the ask endpoint is
// called. (The endpoint path is deliberately not written here: the M1-11 gate
// greps src/ for it to prove nothing bypasses the api client.)
//
// If no LLM provider is configured, the backend abstains and the abstention is
// shown as an abstention. Nothing is fabricated to fill the silence.
// ============================================================
import { useEffect, useState } from "react";
import { assistantModeLabel, assistantApi, type AssistantCitation, type AssistantConfiguration } from "@/api/assistantApi";
import { AssistantChat, type Turn } from "@/components/assistant/AssistantChat";
import { StandardStatGrid } from "@/components/standard";
import "./phase8-ai.css";

const CONTEXT_CHIPS = ["grounded", "approved findings"];

export function AssistantRuntimePage() {
  const [config, setConfig] = useState<AssistantConfiguration | null>(null);
  const [turns, setTurns] = useState<Turn[]>([]);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("Loading assistant runtime configuration...");

  useEffect(() => {
    let active = true;
    assistantApi
      .getAssistantConfig()
      .then((next) => {
        if (!active) return;
        setConfig(next);
        setStatus("Assistant runtime is configured.");
      })
      .catch((error: Error) => {
        if (!active) return;
        setStatus("Assistant configuration not reachable: " + error.message);
      });
    return () => {
      active = false;
    };
  }, []);

  async function ask(question: string) {
    setBusy(true);
    setStatus("Asking grounded assistant...");
    setTurns((prev) => [...prev, { role: "user", text: question }]);

    try {
      const result = await assistantApi.askAssistant(question, CONTEXT_CHIPS, config?.allowedTools ?? []);
      setTurns((prev) => [...prev, { role: "assistant", answer: result }]);
      setStatus(
        result.isRefusal
          ? "Assistant abstained because evidence was insufficient."
          : "Grounded answer returned with evidence.",
      );
    } catch (error) {
      setTurns((prev) => [
        ...prev,
        {
          role: "assistant",
          answer: {
            isRefusal: true,
            refusalReason: error instanceof Error ? error.message : String(error),
            text: "",
            citations: [],
            blocked: [],
          },
        },
      ]);
      setStatus("Assistant request failed.");
    } finally {
      setBusy(false);
    }
  }

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