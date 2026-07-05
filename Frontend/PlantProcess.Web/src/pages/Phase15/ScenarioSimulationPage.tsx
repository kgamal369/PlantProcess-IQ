// Pack G · T-096 · What-if scenario simulation engine
import { useEffect, useMemo, useState } from "react";
import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Input, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
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
    <main data-testid="phase15-scenario-page">
      <section>
        <p>
          Pack G Â· T-096 Â· What-if scenario simulation engine
        </p>
        <h1>Phase 15 What-if Scenario Simulation</h1>
        <p>
          Deterministic projection engine for guarded advisory scenarios. It is projection-only, rejects out-of-envelope changes, and never writes to the production process.
        </p>
        <strong>{status}</strong>
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
        <div>
          <h2>1. Scenario inputs</h2>
          {request ? (
            <>
              <label><span>Scenario name</span><StandardP2Input value={request.scenarioName} onChange={(event) => setRequest({ ...request, scenarioName: event.target.value })} /></label>
              <label><span>Seed</span><StandardP2Input type="number" value={request.seed} onChange={(event) => setRequest({ ...request, seed: Number(event.target.value) })} /></label>
              <div>
                {request.adjustments.map((adjustment, index) => (
                  <div key={adjustment.parameterCode}>
                    <strong>{adjustment.displayName}</strong>
                    <p>Observed envelope: {asNumber(adjustment.minimumObservedValue)} â†’ {asNumber(adjustment.maximumObservedValue)} {adjustment.unit ?? ""}</p>
                    <label><span>Proposed value</span><StandardP2Input type="number" value={adjustment.proposedValue} onChange={(event) => updateProposedValue(index, event.target.value)} /></label>
                  </div>
                ))}
              </div>
              <div>
                <StandardButton type="button" isDisabled={busy} onClick={simulate}>Run what-if simulation</StandardButton>
                <StandardButton type="button" isDisabled={busy} onClick={makeOutOfEnvelope}>Demo out-of-envelope abstain</StandardButton>
                <StandardButton type="button" isDisabled={busy} onClick={() => void refresh()}>Reload sample</StandardButton>
              </div>
            </>
          ) : <p>Loading sample request...</p>}
        </div>

        <div>
          <h2>2. Projection result</h2>
          {response ? (
            <div>
              <div>
                <strong>{response.scenarioId}</strong>
                <p>{response.supportMessage}</p>
                <p>{response.projectionOnlyStatement}</p>
              </div>
              {response.projectedValueImpact ? (
                <div>
                  <strong>Projected value range</strong>
                  <p>Min: {asMoney(response.projectedValueImpact.minValue, response.projectedValueImpact.currencyCode)}</p>
                  <p>Expected: {asMoney(response.projectedValueImpact.expectedValue, response.projectedValueImpact.currencyCode)}</p>
                  <p>Max: {asMoney(response.projectedValueImpact.maxValue, response.projectedValueImpact.currencyCode)}</p>
                </div>
              ) : null}
              <StandardP2Table>
                <thead><tr><th>Metric</th><th>Baseline</th><th>Projected</th><th>Delta</th></tr></thead>
                <tbody>
                  {response.projectionPoints.map((point) => (
                    <tr key={point.metricCode}>
                      <td>{point.label}</td>
                      <td>{asNumber(point.baselineValue)}</td>
                      <td>{asNumber(point.projectedValue)}</td>
                      <td>{asNumber(point.delta)}</td>
                    </tr>
                  ))}
                </tbody>
              </StandardP2Table>
            </div>
          ) : <p>Run a simulation to see deterministic projection output.</p>}
        </div>
      </section>

      <section>
        <h2>3. Honesty and support rules</h2>
        <ul>
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


