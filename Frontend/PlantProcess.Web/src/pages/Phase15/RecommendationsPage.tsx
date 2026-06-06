import { useEffect, useMemo, useState } from "react";
import {
  phase15AdvisoryApi,
  type P15RecommendationCandidate,
  type P15RecommendationContract,
  type P15RecommendationGenerationRequest,
  type P15RecommendationGenerationResponse,
  type P15RecommendationHealth,
} from "@/api/phase15Advisory";

const cardStyle = {
  border: "1px solid rgba(124, 220, 255, 0.18)",
  borderRadius: 22,
  padding: 20,
  background: "linear-gradient(135deg, rgba(7, 18, 34, 0.95), rgba(4, 10, 22, 0.9))",
  boxShadow: "0 18px 50px rgba(0, 0, 0, 0.22)",
} as const;

const buttonStyle = {
  border: "1px solid rgba(0, 212, 255, 0.34)",
  borderRadius: 14,
  padding: "10px 14px",
  color: "#eaf7ff",
  background: "rgba(0, 132, 255, 0.24)",
  cursor: "pointer",
  fontWeight: 700,
} as const;

function asMoney(value?: number | null, currency = "EUR") {
  if (value === undefined || value === null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(value);
}

function percent(value?: number | null) {
  if (value === undefined || value === null || Number.isNaN(value)) return "—";
  return `${Math.round(value * 100)}%`;
}

export function RecommendationsPage() {
  const [health, setHealth] = useState<P15RecommendationHealth | null>(null);
  const [contract, setContract] = useState<P15RecommendationContract | null>(null);
  const [request, setRequest] = useState<P15RecommendationGenerationRequest | null>(null);
  const [response, setResponse] = useState<P15RecommendationGenerationResponse | null>(null);
  const [status, setStatus] = useState("Loading Phase 15 recommendation generator...");
  const [busy, setBusy] = useState(false);
  const [decisionMessage, setDecisionMessage] = useState<string | null>(null);

  async function refresh() {
    const [nextHealth, nextContract, nextRequest] = await Promise.all([
      phase15AdvisoryApi.recommendationHealth(),
      phase15AdvisoryApi.recommendationContract(),
      phase15AdvisoryApi.recommendationDemoRequest(),
    ]);
    setHealth(nextHealth);
    setContract(nextContract);
    setRequest(nextRequest);
    setStatus("Recommendation generator is reachable.");
  }

  useEffect(() => {
    let active = true;
    refresh().catch((error: Error) => {
      if (active) setStatus(`Recommendation generator not reachable: ${error.message}`);
    });
    return () => { active = false; };
  }, []);

  async function generate() {
    if (!request) return;
    setBusy(true);
    setDecisionMessage(null);
    setStatus("Generating guarded recommendations...");
    try {
      const result = await phase15AdvisoryApi.generateRecommendations(request);
      setResponse(result);
      setStatus("Recommendation generation completed. Review confidence, evidence and approval requirement.");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`Recommendation generation failed: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  async function decide(recommendation: P15RecommendationCandidate, decision: 1 | 2) {
    setBusy(true);
    try {
      const result = await phase15AdvisoryApi.approveRecommendation({
        recommendationId: recommendation.recommendationId,
        approverUserId: "demo-approver",
        decision,
        comment: decision === 1 ? "Approved for engineering review." : "Dismissed from demo workspace.",
        decidedAtUtc: new Date().toISOString(),
      });
      setDecisionMessage(result.message);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setDecisionMessage(`Approval failed: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  const firstRecommendation = response?.recommendations[0];
  const impact = firstRecommendation?.expectedImpact;

  const summaryCards = useMemo(
    () => [
      { label: "Mode", value: health?.mode ?? "pending" },
      { label: "Expected € impact", value: health?.expectedEImpactRange ? "YES" : "pending" },
      { label: "Approval required", value: health?.humanApprovalRequired ? "YES" : "pending" },
      { label: "Auto write-back", value: health ? (health.automaticWriteBack ? "YES" : "NO") : "pending" },
      { label: "Recommendations", value: String(response?.recommendations.length ?? 0) },
      { label: "Expected value", value: asMoney(impact?.expectedValue, impact?.currencyCode ?? "EUR") },
    ],
    [health, impact?.currencyCode, impact?.expectedValue, response?.recommendations.length],
  );

  return (
    <main style={{ display: "grid", gap: 18, color: "#eaf7ff" }} data-testid="phase15-recommendations-page">
      <section style={{ ...cardStyle, borderColor: "rgba(19, 216, 255, 0.28)" }}>
        <p style={{ color: "#13d8ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12, margin: 0 }}>
          Pack G · T-097 · Recommendation generator with expected €-impact
        </p>
        <h1 style={{ margin: "8px 0 8px", fontSize: 34 }}>Phase 15 Recommendations</h1>
        <p style={{ margin: 0, color: "#9ab8d7", maxWidth: 980, lineHeight: 1.65 }}>
          Generates guarded advisory recommendations from supported what-if projections. Every recommendation carries expected €-impact, confidence, evidence, provenance and explicit approval requirement.
        </p>
        <strong style={{ display: "block", marginTop: 16, color: status.includes("failed") ? "#ffb86b" : "#64ffda" }}>{status}</strong>
        {decisionMessage ? <strong style={{ display: "block", marginTop: 8, color: "#ffdd8a" }}>{decisionMessage}</strong> : null}
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(170px, 1fr))", gap: 12 }}>
        {summaryCards.map((card) => (
          <div key={card.label} style={cardStyle}>
            <span style={{ color: "#7e9bb8", fontSize: 12, textTransform: "uppercase" }}>{card.label}</span>
            <strong style={{ display: "block", marginTop: 6, fontSize: 18 }}>{card.value}</strong>
          </div>
        ))}
      </section>

      <section style={{ ...cardStyle, display: "grid", gap: 14 }}>
        <h2 style={{ margin: 0 }}>1. Generate recommendation</h2>
        <p style={{ color: "#9ab8d7", lineHeight: 1.65, margin: 0 }}>
          Demo request uses the Pack G-3 deterministic scenario engine. The recommendation generator blocks unsupported scenarios and weak evidence.
        </p>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <button type="button" disabled={busy || !request} onClick={generate} style={buttonStyle}>Generate recommendation</button>
          <button type="button" disabled={busy} onClick={() => void refresh()} style={buttonStyle}>Reload demo request</button>
        </div>
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "minmax(360px, 1.2fr) minmax(280px, 0.8fr)", gap: 14 }}>
        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>2. Recommendation output</h2>
          {response?.recommendations.length ? (
            <div style={{ display: "grid", gap: 12 }}>
              {response.recommendations.map((recommendation) => (
                <div key={recommendation.recommendationId} style={{ border: "1px solid rgba(100,255,218,0.18)", borderRadius: 16, padding: 14, background: "rgba(2,7,18,0.42)" }}>
                  <h3 style={{ marginTop: 0 }}>{recommendation.title}</h3>
                  <p style={{ color: "#9ab8d7", lineHeight: 1.65 }}>{recommendation.advisoryText}</p>
                  <p style={{ color: "#ffdd8a", lineHeight: 1.65 }}>{recommendation.honestyCaveat}</p>
                  <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(150px, 1fr))", gap: 10 }}>
                    <div><small>Confidence</small><strong style={{ display: "block" }}>{percent(recommendation.confidence)}</strong></div>
                    <div><small>Status</small><strong style={{ display: "block" }}>{String(recommendation.status)}</strong></div>
                    <div><small>Expected</small><strong style={{ display: "block" }}>{asMoney(recommendation.expectedImpact?.expectedValue, recommendation.expectedImpact?.currencyCode ?? "EUR")}</strong></div>
                    <div><small>Approval</small><strong style={{ display: "block" }}>{recommendation.requiresHumanApproval ? "Required" : "Not required"}</strong></div>
                  </div>
                  <h4>Parameter windows</h4>
                  <table style={{ width: "100%", borderCollapse: "collapse" }}>
                    <thead><tr style={{ color: "#9ab8d7", textAlign: "left" }}><th style={{ padding: 8 }}>Parameter</th><th style={{ padding: 8 }}>Min</th><th style={{ padding: 8 }}>Max</th><th style={{ padding: 8 }}>Basis</th></tr></thead>
                    <tbody>
                      {recommendation.parameterWindows.map((window) => (
                        <tr key={window.parameterCode} style={{ borderTop: "1px solid rgba(120,190,255,0.12)" }}>
                          <td style={{ padding: 8 }}>{window.displayName}</td>
                          <td style={{ padding: 8 }}>{window.recommendedMinimum} {window.unit ?? ""}</td>
                          <td style={{ padding: 8 }}>{window.recommendedMaximum} {window.unit ?? ""}</td>
                          <td style={{ padding: 8, color: "#9ab8d7" }}>{window.basis}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <div style={{ display: "flex", gap: 10, marginTop: 12, flexWrap: "wrap" }}>
                    <button type="button" disabled={busy} onClick={() => void decide(recommendation, 1)} style={buttonStyle}>Approve for review</button>
                    <button type="button" disabled={busy} onClick={() => void decide(recommendation, 2)} style={buttonStyle}>Dismiss</button>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p style={{ color: "#9ab8d7" }}>{response?.message ?? "Generate a recommendation to view guarded advisory output."}</p>
          )}
        </div>

        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>3. Guardrails</h2>
          <ul style={{ color: "#9ab8d7", lineHeight: 1.75, paddingLeft: 18 }}>
            {(response?.guardrails ?? contract?.guardrails ?? [
              "No causal language.",
              "Expected e-impact is projection-only.",
              "Human approval is required.",
              "No automatic process write-back.",
            ]).map((rule) => <li key={rule}>{rule}</li>)}
          </ul>
          <h3>Provenance</h3>
          <ul style={{ color: "#9ab8d7", lineHeight: 1.75, paddingLeft: 18 }}>
            {(firstRecommendation?.provenance ?? ["No recommendation generated yet."]).map((item) => <li key={item}>{item}</li>)}
          </ul>
        </div>
      </section>
    </main>
  );
}
