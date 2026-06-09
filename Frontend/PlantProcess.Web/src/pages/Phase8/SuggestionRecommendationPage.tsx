
import { useEffect, useMemo, useState } from "react";
import {
  formatEuroRange,
  phase8AssistantApi,
  type Phase8SuggestionRecommendation,
  type Phase8SuggestionRequest,
  type Phase8SuggestionResponse,
} from "@/api/phase8Assistant";
import "./phase8-ai.css";

const defaultRequest: Phase8SuggestionRequest = {
  scope: "demo",
  outcomeKey: "defect.edge_crack_rate",
  materialScope: "coil",
  minimumConfidence: 0.72,
  includeValueProjection: true,
};

export function SuggestionRecommendationPage() {
  const [health, setHealth] = useState<Record<string, unknown> | null>(null);
  const [request, setRequest] = useState<Phase8SuggestionRequest>(defaultRequest);
  const [response, setResponse] = useState<Phase8SuggestionResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("Loading suggestion runtime...");
  const [decision, setDecision] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    phase8AssistantApi.getSuggestionHealth()
      .then((result) => {
        if (!active) return;
        setHealth(result);
        setStatus("Suggestion runtime is reachable.");
      })
      .catch((error: Error) => {
        if (!active) return;
        setStatus("Suggestion runtime not reachable: " + error.message);
      });

    return () => {
      active = false;
    };
  }, []);

  async function generate() {
    setBusy(true);
    setDecision(null);
    setStatus("Generating guarded suggestion and recommendation output...");
    try {
      const result = await phase8AssistantApi.generateSuggestions(request);
      setResponse(result);
      setStatus("Recommendation generation completed. Review guardrails before approval.");
    } catch (error) {
      setStatus("Recommendation generation failed: " + (error instanceof Error ? error.message : String(error)));
    } finally {
      setBusy(false);
    }
  }

  async function decide(item: Phase8SuggestionRecommendation, nextDecision: "approve" | "dismiss") {
    setBusy(true);
    try {
      const result = await phase8AssistantApi.decideSuggestion(
        item.recommendationId,
        nextDecision,
        nextDecision === "approve" ? "Approved for engineering review." : "Dismissed from HMI workspace.",
      );
      setDecision(result.message);
    } catch (error) {
      setDecision("Decision failed: " + (error instanceof Error ? error.message : String(error)));
    } finally {
      setBusy(false);
    }
  }

  const summary = useMemo(
    () => [
      { label: "Runtime", value: String(health?.status ?? "pending") },
      { label: "Mode", value: String(health?.mode ?? "pending") },
      { label: "No egress", value: health?.noEgress === true ? "YES" : "pending" },
      { label: "Recommendations", value: String(response?.recommendations.length ?? 0) },
    ],
    [health, response?.recommendations.length],
  );

  return (
    <main className="phase8-page" data-testid="phase8-suggestion-recommendation-page">
      <section className="phase8-hero">
        <p className="phase8-eyebrow">P08 · T-045 · Suggestion & Recommendation page</p>
        <h1>Suggestion & Recommendation</h1>
        <p className="phase8-muted">
          Generates guarded advisory recommendations from grounded analysis and value evidence. The page explicitly separates advisory recommendation from causal proof and requires human approval before any action.
        </p>
        <strong className="phase8-badge">{status}</strong>
        {decision ? <p className="phase8-muted">{decision}</p> : null}
      </section>

      <section className="phase8-grid">
        {summary.map((item) => (
          <div className="phase8-card phase8-kpi" key={item.label}>
            <span>{item.label}</span>
            <strong>{item.value}</strong>
          </div>
        ))}
      </section>

      <section className="phase8-two-col">
        <div className="phase8-card">
          <h2>Generate guarded recommendation</h2>
          <label>
            Outcome key
            <input className="phase8-input" value={request.outcomeKey} onChange={(event) => setRequest({ ...request, outcomeKey: event.target.value })} />
          </label>
          <label>
            Material scope
            <input className="phase8-input" value={request.materialScope} onChange={(event) => setRequest({ ...request, materialScope: event.target.value })} />
          </label>
          <label>
            Minimum confidence
            <input className="phase8-input" type="number" min="0.1" max="0.98" step="0.01" value={request.minimumConfidence} onChange={(event) => setRequest({ ...request, minimumConfidence: Number(event.target.value) })} />
          </label>
          <label style={{ display: "flex", gap: 10, alignItems: "center", marginTop: 12 }}>
            <input type="checkbox" checked={request.includeValueProjection} onChange={(event) => setRequest({ ...request, includeValueProjection: event.target.checked })} />
            Include projected euro value range
          </label>
          <button className="phase8-button" type="button" disabled={busy} onClick={() => void generate()}>
            {busy ? "Working..." : "Generate recommendation"}
          </button>
        </div>

        <div className="phase8-card">
          <h2>Honesty contract</h2>
          <ul className="phase8-list">
            <li>No causal claim without a separate root-cause investigation.</li>
            <li>No uncited number can appear in assistant or suggestion output.</li>
            <li>Projected euro value is shown as a bounded estimate only.</li>
            <li>Human approval is required before execution.</li>
          </ul>
        </div>
      </section>

      <section className="phase8-card">
        <h2>Recommendation output</h2>
        {response?.honestyCaveat ? <p className="phase8-muted">{response.honestyCaveat}</p> : null}
        {response?.recommendations.length ? (
          <div className="phase8-grid">
            {response.recommendations.map((item) => (
              <article className="phase8-card" key={item.recommendationId}>
                <span className="phase8-badge">{item.actionType}</span>
                <h3>{item.title}</h3>
                <p className="phase8-muted">{item.summary}</p>
                <p><strong>Confidence:</strong> {Math.round(item.confidence * 100)}%</p>
                <p><strong>Value:</strong> {formatEuroRange(item)}</p>
                <p><strong>Approval:</strong> {item.requiresHumanApproval ? "Required" : "Not required"}</p>

                <h4>Evidence</h4>
                <ul className="phase8-list">
                  {item.evidence.map((evidence) => <li key={evidence}>{evidence}</li>)}
                </ul>

                <h4>Guardrails</h4>
                <ul className="phase8-list">
                  {item.guardrails.map((rule) => <li key={rule}>{rule}</li>)}
                </ul>

                <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                  <button className="phase8-button" type="button" disabled={busy} onClick={() => void decide(item, "approve")}>Approve for review</button>
                  <button className="phase8-button" type="button" disabled={busy} onClick={() => void decide(item, "dismiss")}>Dismiss</button>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <p className="phase8-muted">No recommendation generated yet.</p>
        )}
      </section>
    </main>
  );
}

export default SuggestionRecommendationPage;
