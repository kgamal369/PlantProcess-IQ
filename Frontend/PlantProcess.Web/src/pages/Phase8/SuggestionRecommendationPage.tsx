
import { useEffect, useMemo, useState } from "react";
import {
  formatEuroRange, assistantApi, type SuggestionRecommendation, type SuggestionRequest, type SuggestionResponse, } from "@/api/assistantApi";
import "./phase8-ai.css";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Input } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
const defaultRequest: SuggestionRequest = {
  scope: "demo",
  outcomeKey: "defect.rate_per_m2",
  materialScope: "coil",
  minimumConfidence: 0.72,
  includeValueProjection: true,
};

export function SuggestionRecommendationPage() {
  const [health, setHealth] = useState<Record<string, unknown> | null>(null);
  const [request, setRequest] = useState<SuggestionRequest>(defaultRequest);
  const [response, setResponse] = useState<SuggestionResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("Loading suggestion runtime...");
  const [decision, setDecision] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    assistantApi.getSuggestionHealth()
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
      const result = await assistantApi.generateSuggestions(request);
      setResponse(result);
      setStatus("Recommendation generation completed. Review guardrails before approval.");
    } catch (error) {
      setStatus("Recommendation generation failed: " + (error instanceof Error ? error.message : String(error)));
    } finally {
      setBusy(false);
    }
  }

  async function decide(item: SuggestionRecommendation, nextDecision: "approve" | "dismiss") {
    setBusy(true);
    try {
      const result = await assistantApi.decideSuggestion(
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
        <p className="phase8-eyebrow">Guarded recommendations generated only from scenarios the evidence supports.</p>
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
            <StandardP2Input className="phase8-input" value={request.outcomeKey} onChange={(event) => setRequest({ ...request, outcomeKey: event.target.value })} />
          </label>
          <label>
            Material scope
            <StandardP2Input className="phase8-input" value={request.materialScope} onChange={(event) => setRequest({ ...request, materialScope: event.target.value })} />
          </label>
          <label>
            Minimum confidence
            <StandardP2Input className="phase8-input" type="number" min="0.1" max="0.98" step="0.01" value={request.minimumConfidence} onChange={(event) => setRequest({ ...request, minimumConfidence: Number(event.target.value) })} />
          </label>
          <label>
            <StandardP2Input type="checkbox" checked={request.includeValueProjection} onChange={(event) => setRequest({ ...request, includeValueProjection: event.target.checked })} />
            Include projected euro value range
          </label>
          <StandardButton className="phase8-button" type="button" isDisabled={busy} onClick={() => void generate()}>
            {busy ? "Working..." : "Generate recommendation"}
          </StandardButton>
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

                <div>
                  <StandardButton className="phase8-button" type="button" isDisabled={busy} onClick={() => void decide(item, "approve")}>Approve for review</StandardButton>
                  <StandardButton className="phase8-button" type="button" isDisabled={busy} onClick={() => void decide(item, "dismiss")}>Dismiss</StandardButton>
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
