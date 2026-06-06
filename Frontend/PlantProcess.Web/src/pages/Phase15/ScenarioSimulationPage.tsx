// Pack G · T-096 · What-if scenario simulation engine
import { useEffect, useMemo, useState } from "react";
import {
  phase15AdvisoryApi,
  type P15ScenarioContract,
  type P15ScenarioHealth,
  type P15ScenarioRequest,
  type P15ScenarioResponse,
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

const inputStyle = {
  width: "100%",
  border: "1px solid rgba(120, 190, 255, 0.22)",
  borderRadius: 12,
  padding: "10px 12px",
  background: "rgba(2, 7, 18, 0.75)",
  color: "#eaf7ff",
} as const;

function asMoney(value?: number | null, currency = "EUR") {
  if (value === undefined || value === null || Number.isNaN(value)) return "â€”";
  return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(value);
}

function asNumber(value?: number | null) {
  if (value === undefined || value === null || Number.isNaN(value)) return "â€”";
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 3 }).format(value);
}

export function ScenarioSimulationPage() {
  const [health, setHealth] = useState<P15ScenarioHealth | null>(null);
  const [contract, setContract] = useState<P15ScenarioContract | null>(null);
  const [request, setRequest] = useState<P15ScenarioRequest | null>(null);
  const [response, setResponse] = useState<P15ScenarioResponse | null>(null);
  const [status, setStatus] = useState("Loading Phase 15 what-if engine...");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    const [nextHealth, nextContract, nextSample] = await Promise.all([
      phase15AdvisoryApi.scenarioHealth(),
      phase15AdvisoryApi.scenarioContract(),
      phase15AdvisoryApi.sampleRequest(),
    ]);

    setHealth(nextHealth);
    setContract(nextContract);
    setRequest(nextSample);
    setStatus("Phase 15 what-if engine is reachable.");
  }

  useEffect(() => {
    let active = true;
    refresh().catch((error: Error) => {
      if (active) setStatus(`Scenario engine not reachable: ${error.message}`);
    });
    return () => { active = false; };
  }, []);

  const valueImpact = response?.projectedValueImpact;
  const summaryCards = useMemo(
    () => [
      { label: "Mode", value: health?.mode ?? "pending" },
      { label: "Projection only", value: health?.projectionOnly ? "YES" : "pending" },
      { label: "Auto write-back", value: health ? (health.automaticWriteBack ? "YES" : "NO") : "pending" },
      { label: "Support", value: String(response?.supportStatus ?? "not simulated") },
      { label: "Seed", value: String(response?.seed ?? request?.seed ?? "â€”") },
      { label: "Expected â‚¬ impact", value: asMoney(valueImpact?.expectedValue, valueImpact?.currencyCode ?? "EUR") },
    ],
    [health, request?.seed, response?.seed, response?.supportStatus, valueImpact?.currencyCode, valueImpact?.expectedValue],
  );

  function updateProposedValue(index: number, nextValue: string) {
    if (!request) return;
    const numeric = Number(nextValue);
    setRequest({
      ...request,
      adjustments: request.adjustments.map((adjustment, currentIndex) =>
        currentIndex === index ? { ...adjustment, proposedValue: numeric } : adjustment,
      ),
    });
  }

  async function simulate() {
    if (!request) return;
    setBusy(true);
    setStatus("Running deterministic what-if projection...");
    try {
      const result = await phase15AdvisoryApi.simulate(request);
      setResponse(result);
      setStatus("Scenario projection completed. Review support status and projection-only caveat.");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`Scenario projection failed: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  function makeOutOfEnvelope() {
    if (!request) return;
    setRequest({
      ...request,
      adjustments: request.adjustments.map((adjustment, index) =>
        index === 0 ? { ...adjustment, proposedValue: adjustment.maximumObservedValue + 100 } : adjustment,
      ),
    });
    setResponse(null);
    setStatus("Out-of-envelope demo prepared. Run simulation to confirm abstain/insufficient support behavior.");
  }

  return (
    <main style={{ display: "grid", gap: 18, color: "#eaf7ff" }} data-testid="phase15-scenario-page">
      <section style={{ ...cardStyle, borderColor: "rgba(19, 216, 255, 0.28)" }}>
        <p style={{ color: "#13d8ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12, margin: 0 }}>
          Pack G Â· T-096 Â· What-if scenario simulation engine
        </p>
        <h1 style={{ margin: "8px 0 8px", fontSize: 34 }}>Phase 15 What-if Scenario Simulation</h1>
        <p style={{ margin: 0, color: "#9ab8d7", maxWidth: 980, lineHeight: 1.65 }}>
          Deterministic projection engine for guarded advisory scenarios. It is projection-only, rejects out-of-envelope changes, and never writes to the production process.
        </p>
        <strong style={{ display: "block", marginTop: 16, color: status.includes("failed") ? "#ffb86b" : "#64ffda" }}>{status}</strong>
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(170px, 1fr))", gap: 12 }}>
        {summaryCards.map((card) => (
          <div key={card.label} style={cardStyle}>
            <span style={{ color: "#7e9bb8", fontSize: 12, textTransform: "uppercase" }}>{card.label}</span>
            <strong style={{ display: "block", marginTop: 6, fontSize: 18 }}>{card.value}</strong>
          </div>
        ))}
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "minmax(320px, 0.9fr) minmax(360px, 1.1fr)", gap: 14 }}>
        <div style={{ ...cardStyle, display: "grid", gap: 14 }}>
          <h2 style={{ margin: 0 }}>1. Scenario inputs</h2>
          {request ? (
            <>
              <label><span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Scenario name</span><input value={request.scenarioName} onChange={(event) => setRequest({ ...request, scenarioName: event.target.value })} style={inputStyle} /></label>
              <label><span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Seed</span><input type="number" value={request.seed} onChange={(event) => setRequest({ ...request, seed: Number(event.target.value) })} style={inputStyle} /></label>
              <div style={{ display: "grid", gap: 10 }}>
                {request.adjustments.map((adjustment, index) => (
                  <div key={adjustment.parameterCode} style={{ border: "1px solid rgba(120, 190, 255, 0.14)", borderRadius: 14, padding: 12, background: "rgba(2, 7, 18, 0.42)" }}>
                    <strong>{adjustment.displayName}</strong>
                    <p style={{ margin: "6px 0", color: "#9ab8d7" }}>Observed envelope: {asNumber(adjustment.minimumObservedValue)} â†’ {asNumber(adjustment.maximumObservedValue)} {adjustment.unit ?? ""}</p>
                    <label><span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Proposed value</span><input type="number" value={adjustment.proposedValue} onChange={(event) => updateProposedValue(index, event.target.value)} style={inputStyle} /></label>
                  </div>
                ))}
              </div>
              <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                <button type="button" disabled={busy} onClick={simulate} style={buttonStyle}>Run what-if simulation</button>
                <button type="button" disabled={busy} onClick={makeOutOfEnvelope} style={buttonStyle}>Demo out-of-envelope abstain</button>
                <button type="button" disabled={busy} onClick={() => void refresh()} style={buttonStyle}>Reload sample</button>
              </div>
            </>
          ) : <p style={{ color: "#9ab8d7" }}>Loading sample request...</p>}
        </div>

        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>2. Projection result</h2>
          {response ? (
            <div style={{ display: "grid", gap: 12 }}>
              <div>
                <strong>{response.scenarioId}</strong>
                <p style={{ color: "#9ab8d7", lineHeight: 1.6 }}>{response.supportMessage}</p>
                <p style={{ color: "#ffdd8a", lineHeight: 1.6 }}>{response.projectionOnlyStatement}</p>
              </div>
              {response.projectedValueImpact ? (
                <div style={{ border: "1px solid rgba(100, 255, 218, 0.22)", borderRadius: 14, padding: 12 }}>
                  <strong>Projected value range</strong>
                  <p style={{ margin: "6px 0", color: "#9ab8d7" }}>Min: {asMoney(response.projectedValueImpact.minValue, response.projectedValueImpact.currencyCode)}</p>
                  <p style={{ margin: "6px 0", color: "#9ab8d7" }}>Expected: {asMoney(response.projectedValueImpact.expectedValue, response.projectedValueImpact.currencyCode)}</p>
                  <p style={{ margin: "6px 0", color: "#9ab8d7" }}>Max: {asMoney(response.projectedValueImpact.maxValue, response.projectedValueImpact.currencyCode)}</p>
                </div>
              ) : null}
              <table style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead><tr style={{ color: "#9ab8d7", textAlign: "left" }}><th style={{ padding: 8 }}>Metric</th><th style={{ padding: 8 }}>Baseline</th><th style={{ padding: 8 }}>Projected</th><th style={{ padding: 8 }}>Delta</th></tr></thead>
                <tbody>
                  {response.projectionPoints.map((point) => (
                    <tr key={point.metricCode} style={{ borderTop: "1px solid rgba(120, 190, 255, 0.12)" }}>
                      <td style={{ padding: 8 }}>{point.label}</td>
                      <td style={{ padding: 8 }}>{asNumber(point.baselineValue)}</td>
                      <td style={{ padding: 8 }}>{asNumber(point.projectedValue)}</td>
                      <td style={{ padding: 8 }}>{asNumber(point.delta)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : <p style={{ color: "#9ab8d7" }}>Run a simulation to see deterministic projection output.</p>}
        </div>
      </section>

      <section style={cardStyle}>
        <h2 style={{ marginTop: 0 }}>3. Honesty and support rules</h2>
        <ul style={{ color: "#9ab8d7", lineHeight: 1.75, paddingLeft: 18 }}>
          {(contract?.safetyRules ?? [
            "Projection only. Not a guaranteed saving.",
            "No automatic process write-back.",
            "Out-of-envelope scenario requests must abstain.",
          ]).map((rule) => <li key={rule}>{rule}</li>)}
        </ul>
      </section>
    </main>
  );
}


