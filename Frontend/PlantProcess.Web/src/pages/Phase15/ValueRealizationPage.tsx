import { useEffect, useMemo, useState } from "react";
import { phase15AdvisoryApi, type P15ValueRealizationContract, type P15ValueRealizationHealth, type P15ValueRealizationRequest, type P15ValueRealizationResponse } from "@/api/phase15Advisory";

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
    <main style={{ display: "grid", gap: 18, color: "#eaf7ff" }} data-testid="phase15-value-realization-page">
      <section style={{ ...cardStyle, borderColor: "rgba(19,216,255,0.28)" }}>
        <p style={{ color: "#13d8ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12, margin: 0 }}>
          Pack G · T-098 · Value-realization tracking baseline vs actual
        </p>
        <h1 style={{ margin: "8px 0 8px", fontSize: 34 }}>Phase 15 Value Realization</h1>
        <p style={{ margin: 0, color: "#9ab8d7", maxWidth: 980, lineHeight: 1.65 }}>
          Tracks realized customer value by comparing a reproducible baseline KPI window against an actual KPI window after recommendation review. Attribution caveats remain visible.
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
          <h2 style={{ margin: 0 }}>1. Baseline vs actual input</h2>
          {request ? (
            <>
              <p style={{ color: "#9ab8d7", lineHeight: 1.65, margin: 0 }}>Recommendation: <strong>{request.recommendationId}</strong></p>
              <p style={{ color: "#9ab8d7", lineHeight: 1.65, margin: 0 }}>Metric: <strong>{request.baselineWindow.metricCode}</strong></p>
              <label>
                <span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Actual KPI value</span>
                <input type="number" value={request.actualWindow.value} onChange={(event) => updateActualValue(event.target.value)} style={inputStyle} />
              </label>
              <button type="button" disabled={busy} onClick={calculate} style={buttonStyle}>Calculate realized value</button>
              <button type="button" disabled={busy} onClick={() => void refresh()} style={buttonStyle}>Reload demo request</button>
            </>
          ) : <p style={{ color: "#9ab8d7" }}>Loading demo request...</p>}
        </div>

        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>2. Ledger result</h2>
          {ledger ? (
            <div style={{ display: "grid", gap: 12 }}>
              <strong>{ledger.ledgerEntryId}</strong>
              <p style={{ color: "#9ab8d7", lineHeight: 1.65 }}>{response?.message}</p>
              <p style={{ color: "#ffdd8a", lineHeight: 1.65 }}>{ledger.attributionCaveat}</p>
              <div style={{ border: "1px solid rgba(100,255,218,0.22)", borderRadius: 14, padding: 12 }}>
                <strong>Realized value range</strong>
                <p style={{ margin: "6px 0", color: "#9ab8d7" }}>Min: {asMoney(ledger.realizedValue.minValue, ledger.realizedValue.currencyCode)}</p>
                <p style={{ margin: "6px 0", color: "#9ab8d7" }}>Expected: {asMoney(ledger.realizedValue.expectedValue, ledger.realizedValue.currencyCode)}</p>
                <p style={{ margin: "6px 0", color: "#9ab8d7" }}>Max: {asMoney(ledger.realizedValue.maxValue, ledger.realizedValue.currencyCode)}</p>
              </div>
              <h3>Provenance</h3>
              <ul style={{ color: "#9ab8d7", lineHeight: 1.75, paddingLeft: 18 }}>{ledger.provenance.map((item) => <li key={item}>{item}</li>)}</ul>
            </div>
          ) : <p style={{ color: "#9ab8d7" }}>Calculate realized value to create a ledger entry preview.</p>}
        </div>
      </section>

      <section style={cardStyle}>
        <h2 style={{ marginTop: 0 }}>3. Guardrails</h2>
        <ul style={{ color: "#9ab8d7", lineHeight: 1.75, paddingLeft: 18 }}>
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
