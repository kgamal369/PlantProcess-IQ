import { useEffect, useMemo, useState } from "react";
import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
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
    <main data-testid="phase15-recommendations-page">
      <section>
        <p>
          Pack G · T-097 · Recommendation generator with expected €-impact
        </p>
        <h1>Phase 15 Recommendations</h1>
        <p>
          Generates guarded advisory recommendations from supported what-if projections. Every recommendation carries expected €-impact, confidence, evidence, provenance and explicit approval requirement.
        </p>
        <strong>{status}</strong>
        {decisionMessage ? <strong>{decisionMessage}</strong> : null}
      </section>

      <section>
        {summaryCards.map((card) => (
          <div key={card.label}>
            <span>{card.label}</span>
            <strong>{card.value}</strong>
          </div>
        ))}
      </section>

      <section>
        <h2>1. Generate recommendation</h2>
        <p>
          Demo request uses the Pack G-3 deterministic scenario engine. The recommendation generator blocks unsupported scenarios and weak evidence.
        </p>
        <div>
          <StandardButton type="button" isDisabled={busy || !request} onClick={generate}>Generate recommendation</StandardButton>
          <StandardButton type="button" isDisabled={busy} onClick={() => void refresh()}>Reload demo request</StandardButton>
        </div>
      </section>

      <section>
        <div>
          <h2>2. Recommendation output</h2>
          {response?.recommendations.length ? (
            <div>
              {response.recommendations.map((recommendation) => (
                <div key={recommendation.recommendationId}>
                  <h3>{recommendation.title}</h3>
                  <p>{recommendation.advisoryText}</p>
                  <p>{recommendation.honestyCaveat}</p>
                  <div>
                    <div><small>Confidence</small><strong>{percent(recommendation.confidence)}</strong></div>
                    <div><small>Status</small><strong>{String(recommendation.status)}</strong></div>
                    <div><small>Expected</small><strong>{asMoney(recommendation.expectedImpact?.expectedValue, recommendation.expectedImpact?.currencyCode ?? "EUR")}</strong></div>
                    <div><small>Approval</small><strong>{recommendation.requiresHumanApproval ? "Required" : "Not required"}</strong></div>
                  </div>
                  <h4>Parameter windows</h4>
                  <StandardP2Table>
                    <thead><tr><th>Parameter</th><th>Min</th><th>Max</th><th>Basis</th></tr></thead>
                    <tbody>
                      {recommendation.parameterWindows.map((window) => (
                        <tr key={window.parameterCode}>
                          <td>{window.displayName}</td>
                          <td>{window.recommendedMinimum} {window.unit ?? ""}</td>
                          <td>{window.recommendedMaximum} {window.unit ?? ""}</td>
                          <td>{window.basis}</td>
                        </tr>
                      ))}
                    </tbody>
                  </StandardP2Table>
                  <div>
                    <StandardButton type="button" isDisabled={busy} onClick={() => void decide(recommendation, 1)}>Approve for review</StandardButton>
                    <StandardButton type="button" isDisabled={busy} onClick={() => void decide(recommendation, 2)}>Dismiss</StandardButton>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p>{response?.message ?? "Generate a recommendation to view guarded advisory output."}</p>
          )}
        </div>

        <div>
          <h2>3. Guardrails</h2>
          <ul>
            {(response?.guardrails ?? contract?.guardrails ?? [
              "No causal language.",
              "Expected e-impact is projection-only.",
              "Human approval is required.",
              "No automatic process write-back.",
            ]).map((rule) => <li key={rule}>{rule}</li>)}
          </ul>
          <h3>Provenance</h3>
          <ul>
            {(firstRecommendation?.provenance ?? ["No recommendation generated yet."]).map((item) => <li key={item}>{item}</li>)}
          </ul>
        </div>
      </section>
    </main>
  );
}
