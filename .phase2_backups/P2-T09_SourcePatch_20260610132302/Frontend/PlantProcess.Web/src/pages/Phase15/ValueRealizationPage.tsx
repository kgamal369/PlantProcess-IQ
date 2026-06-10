import { useEffect, useMemo, useState } from "react";
import { phase15AdvisoryApi, type P15ValueRealizationContract, type P15ValueRealizationHealth, type P15ValueRealizationRequest, type P15ValueRealizationResponse } from "@/api/phase15Advisory";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Button, StandardP2Input } from "@/components/standard/StandardP2Controls";
const cardStyle = { border: "1px solid rgba(124,220,255,0.18)", borderRadius: 22, padding: 20, background: "linear-gradient(135deg, rgba(7,18,34,0.95), rgba(4,10,22,0.9))", boxShadow: "0 18px 50px rgba(0,0,0,0.22)" } as const;
const buttonStyle = { border: "1px solid rgba(0,212,255,0.34)", borderRadius: 14, padding: "10px 14px", color: "#eaf7ff", background: "rgba(0,132,255,0.24)", cursor: "pointer", fontWeight: 700 } as const;
const inputStyle = { width: "100%", border: "1px solid rgba(120,190,255,0.22)", borderRadius: 12, padding: "10px 12px", background: "rgba(2,7,18,0.75)", color: "#eaf7ff" } as const;

function asMoney(value?: number | null, currency = "EUR") {
  if (value === undefined || value === null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(value);
}

function asNumber(value?: number | null) {
  if (value === undefined || value === null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 3 }).format(value);
}

export function ValueRealizationPage() {
  const [health, setHealth] = useState<P15ValueRealizationHealth | null>(null);
  const [contract, setContract] = useState<P15ValueRealizationContract | null>(null);
  const [request, setRequest] = useState<P15ValueRealizationRequest | null>(null);
  const [response, setResponse] = useState<P15ValueRealizationResponse | null>(null);
  const [status, setStatus] = useState("Loading Phase 15 value-realization engine...");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    const [nextHealth, nextContract, nextRequest] = await Promise.all([
      phase15AdvisoryApi.valueRealizationHealth(),
      phase15AdvisoryApi.valueRealizationContract(),
      phase15AdvisoryApi.valueRealizationDemoRequest(),
    ]);
    setHealth(nextHealth);
    setContract(nextContract);
    setRequest(nextRequest);
    setStatus("Value-realization engine is reachable.");
  }

  useEffect(() => {
    let active = true;
    refresh().catch((error: Error) => { if (active) setStatus(`Value-realization engine not reachable: ${error.message}`); });
    return () => { active = false; };
  }, []);

  function updateActualValue(value: string) {
    if (!request) return;
    setRequest({ ...request, actualWindow: { ...request.actualWindow, value: Number(value) } });
    setResponse(null);
  }

  async function calculate() {
    if (!request) return;
    setBusy(true);
    setStatus("Calculating baseline-vs-actual realized value...");
    try {
      const result = await phase15AdvisoryApi.calculateValueRealization(request);
      setResponse(result);
      setStatus("Value-realization calculation completed with attribution caveat.");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`Value-realization calculation failed: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  const ledger = response?.ledgerEntry;
  const summaryCards = useMemo(() => [
    { label: "Mode", value: health?.mode ?? "pending" },
    { label: "Baseline", value: asNumber(request?.baselineWindow.value) },
    { label: "Actual", value: asNumber(request?.actualWindow.value) },
    { label: "Delta", value: asNumber(response?.baselineVsActualDelta) },
    { label: "Realized €", value: asMoney(ledger?.realizedValue.expectedValue, ledger?.realizedValue.currencyCode ?? request?.currencyCode ?? "EUR") },
    { label: "Recommendation link", value: request?.recommendationId ? "YES" : "pending" },
  ], [health?.mode, ledger?.realizedValue.currencyCode, ledger?.realizedValue.expectedValue, request, response?.baselineVsActualDelta]);

  return (
    <main data-testid="phase15-value-realization-page">
      <section>
        <p>
          Pack G · T-098 · Value-realization tracking baseline vs actual
        </p>
        <h1>Phase 15 Value Realization</h1>
        <p>
          Tracks realized customer value by comparing a reproducible baseline KPI window against an actual KPI window after recommendation review. Attribution caveats remain visible.
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
          <h2>1. Baseline vs actual input</h2>
          {request ? (
            <>
              <p>Recommendation: <strong>{request.recommendationId}</strong></p>
              <p>Metric: <strong>{request.baselineWindow.metricCode}</strong></p>
              <label>
                <span>Actual KPI value</span>
                <StandardP2Input type="number" value={request.actualWindow.value} onChange={(event) => updateActualValue(event.target.value)} />
              </label>
              <StandardP2Button type="button" disabled={busy} onClick={calculate}>Calculate realized value</StandardP2Button>
              <StandardP2Button type="button" disabled={busy} onClick={() => void refresh()}>Reload demo request</StandardP2Button>
            </>
          ) : <p>Loading demo request...</p>}
        </div>

        <div>
          <h2>2. Ledger result</h2>
          {ledger ? (
            <div>
              <strong>{ledger.ledgerEntryId}</strong>
              <p>{response?.message}</p>
              <p>{ledger.attributionCaveat}</p>
              <div>
                <strong>Realized value range</strong>
                <p>Min: {asMoney(ledger.realizedValue.minValue, ledger.realizedValue.currencyCode)}</p>
                <p>Expected: {asMoney(ledger.realizedValue.expectedValue, ledger.realizedValue.currencyCode)}</p>
                <p>Max: {asMoney(ledger.realizedValue.maxValue, ledger.realizedValue.currencyCode)}</p>
              </div>
              <h3>Provenance</h3>
              <ul>{ledger.provenance.map((item) => <li key={item}>{item}</li>)}</ul>
            </div>
          ) : <p>Calculate realized value to create a ledger entry preview.</p>}
        </div>
      </section>

      <section>
        <h2>3. Guardrails</h2>
        <ul>
          {(contract?.guardrails ?? [
            "Baseline and actual windows must use the same KPI metric.",
            "Realized value must link to a source recommendation.",
            "Attribution caveat must be visible.",
            "Correlation is not causation.",
          ]).map((rule) => <li key={rule}>{rule}</li>)}
        </ul>
      </section>
    </main>
  );
}
