
import { useEffect, useState } from "react";
import { assistantModeLabel, assistantApi, type AssistantAnswer, type AssistantConfiguration } from "@/api/assistantApi";
import "./phase8-ai.css";

import { StandardP2Button, StandardP2TextArea } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
export function AssistantRuntimePage() {
  const [config, setConfig] = useState<AssistantConfiguration | null>(null);
  const [question, setQuestion] = useState("What evidence supports the latest quality recommendation?");
  const [answer, setAnswer] = useState<AssistantAnswer | null>(null);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("Loading assistant runtime configuration...");

  useEffect(() => {
    let active = true;

    assistantApi.getAssistantConfig()
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

  async function ask() {
    setBusy(true);
    setAnswer(null);
    setStatus("Asking grounded assistant...");
    try {
      const result = await assistantApi.askAssistant(question, ["phase8-hmi", "grounded"], config?.allowedTools ?? []);
      setAnswer(result);
      setStatus(result.isRefusal ? "Assistant abstained because evidence was insufficient." : "Grounded answer returned with evidence.");
    } catch (error) {
      setAnswer({
        isRefusal: true,
        refusalReason: error instanceof Error ? error.message : String(error),
        text: "",
        citations: [],
        blocked: [],
      });
      setStatus("Assistant request failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="phase8-page" data-testid="phase8-assistant-runtime-page">
      <section className="phase8-hero">
        <p className="phase8-eyebrow">Ask questions about your plant data and receive answers with cited evidence.</p>
        <h1>Grounded Assistant Runtime</h1>
        <p className="phase8-muted">
          The assistant can answer only with grounded evidence. Missing evidence produces an abstention, not an invented number or unsupported causal claim.
        </p>
        <strong className="phase8-badge">{status}</strong>
      </section>

      <section className="phase8-grid">
        <div className="phase8-card phase8-kpi">
          <span>Mode</span>
          <strong>{assistantModeLabel(config)}</strong>
        </div>
        <div className="phase8-card phase8-kpi">
          <span>Grounding</span>
          <strong>{config?.groundingPolicy ?? "pending"}</strong>
        </div>
        <div className="phase8-card phase8-kpi">
          <span>Evidence policy</span>
          <strong>{config?.evidencePolicy ?? "pending"}</strong>
        </div>
      </section>

      <section className="phase8-two-col">
        <div className="phase8-card">
          <h2>Ask a grounded question</h2>
          <StandardP2TextArea className="phase8-textarea" value={question} onChange={(event) => setQuestion(event.target.value)} />
          <StandardButton className="phase8-button" type="button" isDisabled={busy || question.trim().length === 0} onClick={() => void ask()}>
            {busy ? "Asking..." : "Ask assistant"}
          </StandardButton>
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

      {answer ? (
        <section className="phase8-card" aria-live="polite">
          <h2>Assistant answer</h2>
          {answer.isRefusal ? (
            <p className="phase8-muted">No grounded answer. Reason: {answer.refusalReason ?? "insufficient evidence"}</p>
          ) : (
            <p>{answer.text}</p>
          )}

          <h3>Citations</h3>
          {answer.citations.length ? (
            <ul className="phase8-list">
              {answer.citations.map((citation) => (
                <li key={citation.kind + ":" + citation.id + ":" + (citation.detail ?? "")}>
                  {citation.kind}:{citation.id}{citation.detail ? " - " + citation.detail : ""}
                </li>
              ))}
            </ul>
          ) : (
            <p className="phase8-muted">No citations returned.</p>
          )}

          {answer.blocked.length ? (
            <>
              <h3>Blocked unsupported claims</h3>
              <ul className="phase8-list">
                {answer.blocked.map((item) => <li key={item}>{item}</li>)}
              </ul>
            </>
          ) : null}
        </section>
      ) : null}
    </main>
  );
}

export default AssistantRuntimePage;
