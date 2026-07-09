import { useEffect, useMemo, useState } from "react";
import { advisoryApi, type ValueRealizationContract, type ValueRealizationHealth, type ValueRealizationRequest, type ValueRealizationResponse } from "@/api/advisoryApi";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Input } from "@/components/standard/StandardP2Controls";
import { StandardButton, StandardPageHeader, StandardStatGrid } from "@/components/standard";
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
  const [health, setHealth] = useState<ValueRealizationHealth | null>(null);
  const [contract, setContract] = useState<ValueRealizationContract | null>(null);
  const [request, setRequest] = useState<ValueRealizationRequest | null>(null);
  const [response, setResponse] = useState<ValueRealizationResponse | null>(null);
  const [status, setStatus] = useState("Loading value-realization engine...");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    const [nextHealth, nextContract, nextRequest] = await Promise.all([
      advisoryApi.valueRealizationHealth(),
      advisoryApi.valueRealizationContract(),
      advisoryApi.valueRealizationDemoRequest(),
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
      const result = await advisoryApi.calculateValueRealization(request);
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
    { label: "Baseline", value: asNumber(request?.baselineWindow.value) },
    { label: "Actual", value: asNumber(request?.actualWindow.value) },
    { label: "Delta", value: asNumber(response?.baselineVsActualDelta) },
    { label: "Realized €", value: asMoney(ledger?.realizedValue.expectedValue, ledger?.realizedValue.currencyCode ?? request?.currencyCode ?? "EUR") },
    { label: "Recommendation link", value: request?.recommendationId ? "YES" : "pending" },
  ], [health?.mode, ledger?.realizedValue.currencyCode, ledger?.realizedValue.expectedValue, request, response?.baselineVsActualDelta]);

  return (
    <main data-testid="value-realization-page">
      <StandardPageHeader
        title="Value Realisation Tracking"
        subtitle="Measured KPI movement after a recommendation was acted on, against a reproducible baseline."
        description="When a recommendation is implemented, this page compares a frozen baseline KPI window against the actual window that followed. Attribution caveats remain visible: a measured improvement is not proof that the recommendation caused it."
        status={status}
      />
      <p className="ppiq-std-sample-badge">Sample data - not from your plant</p>

      <StandardStatGrid items={summaryCards} emphasize="Realized" />

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
              <StandardButton type="button" isDisabled={busy} onClick={calculate}>Calculate realized value</StandardButton>
              <StandardButton type="button" isDisabled={busy} onClick={() => void refresh()}>Load sample request</StandardButton>
            </>
          ) : <p>Loading sample request...</p>}
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
