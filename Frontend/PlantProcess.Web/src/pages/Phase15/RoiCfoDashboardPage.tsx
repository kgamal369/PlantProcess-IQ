import { useEffect, useMemo, useState } from "react";
import { phase15AdvisoryApi, type P15CfoEvidencePack, type P15RoiCfoDashboardContract, type P15RoiCfoDashboardHealth, type P15RoiCfoDashboardResponse } from "@/api/phase15Advisory";

const cardStyle = { border: "1px solid rgba(124,220,255,0.18)", borderRadius: 22, padding: 20, background: "linear-gradient(135deg, rgba(7,18,34,0.95), rgba(4,10,22,0.9))", boxShadow: "0 18px 50px rgba(0,0,0,0.22)" } as const;
const buttonStyle = { border: "1px solid rgba(0,212,255,0.34)", borderRadius: 14, padding: "10px 14px", color: "#eaf7ff", background: "rgba(0,132,255,0.24)", cursor: "pointer", fontWeight: 700 } as const;

function asMoney(value?: number | null, currency = "EUR") {
  if (value === undefined || value === null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(value);
}

function asNumber(value?: number | null) {
  if (value === undefined || value === null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value);
}

export function RoiCfoDashboardPage() {
  const [health, setHealth] = useState<P15RoiCfoDashboardHealth | null>(null);
  const [contract, setContract] = useState<P15RoiCfoDashboardContract | null>(null);
  const [dashboard, setDashboard] = useState<P15RoiCfoDashboardResponse | null>(null);
  const [evidencePack, setEvidencePack] = useState<P15CfoEvidencePack | null>(null);
  const [status, setStatus] = useState("Loading Phase 15 ROI/CFO dashboard...");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    setBusy(true);
    try {
      const [nextHealth, nextContract, nextDashboard] = await Promise.all([
        phase15AdvisoryApi.roiCfoDashboardHealth(),
        phase15AdvisoryApi.roiCfoDashboardContract(),
        phase15AdvisoryApi.roiCfoDashboardSummary(),
      ]);
      setHealth(nextHealth);
      setContract(nextContract);
      setDashboard(nextDashboard);
      setStatus("ROI/CFO value dashboard is ready.");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`ROI/CFO dashboard not reachable: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => { void refresh(); }, []);

  async function exportEvidencePack() {
    setBusy(true);
    try {
      const pack = await phase15AdvisoryApi.roiCfoEvidencePack();
      setEvidencePack(pack);
      setStatus("CFO evidence pack loaded. It reconciles with the dashboard ledger snapshot.");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`Evidence pack export failed: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  const summary = dashboard?.summary;
  const currency = summary?.realizedValue.currencyCode ?? "EUR";

  const summaryCards = useMemo(() => [
    { label: "Mode", value: health?.mode ?? "pending" },
    { label: "Potential value", value: asMoney(summary?.potentialValue.expectedValue, currency) },
    { label: "Realized value", value: asMoney(summary?.realizedValue.expectedValue, currency) },
    { label: "Payback months", value: asNumber(summary?.paybackPeriodMonths) },
    { label: "Recommendations", value: String(summary?.recommendationCount ?? 0) },
    { label: "Ledger entries", value: String(summary?.realizedLedgerEntryCount ?? 0) },
  ], [currency, health?.mode, summary]);

  return (
    <main style={{ display: "grid", gap: 18, color: "#eaf7ff" }} data-testid="phase15-roi-cfo-dashboard-page">
      <section style={{ ...cardStyle, borderColor: "rgba(19,216,255,0.28)" }}>
        <p style={{ color: "#13d8ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12, margin: 0 }}>
          Pack G · T-099 · ROI / CFO value dashboard
        </p>
        <h1 style={{ margin: "8px 0 8px", fontSize: 34 }}>Phase 15 ROI / CFO Value Dashboard</h1>
        <p style={{ margin: 0, color: "#9ab8d7", maxWidth: 980, lineHeight: 1.65 }}>
          Buyer-facing value dashboard separating potential value from realized value, showing payback period and producing an exportable CFO evidence pack with ledger IDs, provenance and caveats.
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

      <section style={{ display: "grid", gridTemplateColumns: "minmax(360px, 1fr) minmax(340px, 0.9fr)", gap: 14 }}>
        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>1. Value buckets</h2>
          {dashboard?.buckets.length ? (
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead><tr style={{ color: "#9ab8d7", textAlign: "left" }}><th style={{ padding: 8 }}>Bucket</th><th style={{ padding: 8 }}>Kind</th><th style={{ padding: 8 }}>Expected</th><th style={{ padding: 8 }}>Source</th></tr></thead>
              <tbody>
                {dashboard.buckets.map((bucket) => (
                  <tr key={bucket.bucketCode} style={{ borderTop: "1px solid rgba(120,190,255,0.12)" }}>
                    <td style={{ padding: 8 }}>{bucket.label}</td>
                    <td style={{ padding: 8 }}>{String(bucket.valueKind)}</td>
                    <td style={{ padding: 8 }}>{asMoney(bucket.expectedValue, bucket.currencyCode)}</td>
                    <td style={{ padding: 8, color: "#9ab8d7" }}>{bucket.source}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : <p style={{ color: "#9ab8d7" }}>Dashboard data is loading...</p>}
        </div>

        <div style={{ ...cardStyle, display: "grid", gap: 12 }}>
          <h2 style={{ margin: 0 }}>2. CFO evidence pack</h2>
          <p style={{ color: "#9ab8d7", lineHeight: 1.65, margin: 0 }}>Evidence pack reconciles potential value, realized value, payback period, recommendation IDs and ledger entry IDs.</p>
          <button type="button" disabled={busy} onClick={() => void exportEvidencePack()} style={buttonStyle}>Export CFO evidence pack</button>
          {evidencePack ? (
            <div style={{ border: "1px solid rgba(100,255,218,0.22)", borderRadius: 14, padding: 12 }}>
              <strong>{evidencePack.evidencePackId}</strong>
              <p style={{ color: "#9ab8d7" }}>Potential: {asMoney(evidencePack.potentialExpectedValue, evidencePack.currencyCode)}</p>
              <p style={{ color: "#9ab8d7" }}>Realized: {asMoney(evidencePack.realizedExpectedValue, evidencePack.currencyCode)}</p>
              <p style={{ color: "#9ab8d7" }}>Payback: {asNumber(evidencePack.paybackPeriodMonths)} months</p>
            </div>
          ) : null}
        </div>
      </section>

      <section style={cardStyle}>
        <h2 style={{ marginTop: 0 }}>3. CFO caveats and guardrails</h2>
        <ul style={{ color: "#9ab8d7", lineHeight: 1.75, paddingLeft: 18 }}>
          {(dashboard?.caveats ?? contract?.guardrails ?? [
            "Potential value and realized value are separated.",
            "Realized value reconciles with value-realization ledger.",
            "Correlation is not causation.",
          ]).map((rule) => <li key={rule}>{rule}</li>)}
        </ul>
      </section>
    </main>
  );
}
