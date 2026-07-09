// What-If Scenario Analysis
import { useEffect, useMemo, useState } from "react";
import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Input, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { StandardButton, StandardPageHeader, StandardStatGrid } from "@/components/standard";
import {
  advisoryApi,
  type ScenarioContract,
  type ScenarioHealth,
  type ScenarioRequest,
  type ScenarioResponse,
} from "@/api/advisoryApi";

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
  if (value === undefined || value === null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(value);
}

function asNumber(value?: number | null) {
  if (value === undefined || value === null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 3 }).format(value);
}

export function ScenarioSimulationPage() {
  const [health, setHealth] = useState<ScenarioHealth | null>(null);
  const [contract, setContract] = useState<ScenarioContract | null>(null);
  const [request, setRequest] = useState<ScenarioRequest | null>(null);
  const [response, setResponse] = useState<ScenarioResponse | null>(null);
  const [status, setStatus] = useState("Loading what-if engine...");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    const [nextHealth, nextContract, nextSample] = await Promise.all([
      advisoryApi.scenarioHealth(),
      advisoryApi.scenarioContract(),
      advisoryApi.sampleRequest(),
    ]);

    setHealth(nextHealth);
    setContract(nextContract);
    setRequest(nextSample);
    setStatus("what-if engine is reachable.");
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
      { label: "Projection only", value: health?.projectionOnly ? "YES" : "pending" },
      { label: "Auto write-back", value: health ? (health.automaticWriteBack ? "YES" : "NO") : "pending" },
      { label: "Support", value: String(response?.supportStatus ?? "not simulated") },
      { label: "Seed", value: String(response?.seed ?? request?.seed ?? "—") },
      { label: "Expected € impact", value: asMoney(valueImpact?.expectedValue, valueImpact?.currencyCode ?? "EUR") },
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
      const result = await advisoryApi.simulate(request);
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
    setStatus("Out-of-envelope value prepared. Run the simulation to confirm the engine abstains.");
  }

  return (
    <main data-testid="scenario-simulation-page">
      <StandardPageHeader
        title="What-If Scenario Analysis"
        subtitle="Projected outcomes for process changes inside the observed operating envelope."
        description="Propose a change to a process parameter and see the projected effect. The engine refuses to project outside the envelope actually observed in your plant data, and it never writes a value back to the process."
        status={status}
      />

      <StandardStatGrid items={summaryCards} emphasize="Support" />

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
                    <p>Observed envelope: {asNumber(adjustment.minimumObservedValue)} → {asNumber(adjustment.maximumObservedValue)} {adjustment.unit ?? ""}</p>
                    <label><span>Proposed value</span><StandardP2Input type="number" value={adjustment.proposedValue} onChange={(event) => updateProposedValue(index, event.target.value)} /></label>
                  </div>
                ))}
              </div>
              <div>
                <StandardButton type="button" isDisabled={busy} onClick={simulate}>Run what-if simulation</StandardButton>
                <StandardButton type="button" isDisabled={busy} onClick={makeOutOfEnvelope}>Test an out-of-envelope value</StandardButton>
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


