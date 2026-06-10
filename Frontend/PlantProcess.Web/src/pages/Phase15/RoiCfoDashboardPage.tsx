import { useEffect, useMemo, useState } from "react";
import { phase15AdvisoryApi, type P15CfoEvidencePack, type P15RoiCfoDashboardContract, type P15RoiCfoDashboardHealth, type P15RoiCfoDashboardResponse } from "@/api/phase15Advisory";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
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
    <main data-testid="phase15-roi-cfo-dashboard-page">
      <section>
        <p>
          Pack G · T-099 · ROI / CFO value dashboard
        </p>
        <h1>Phase 15 ROI / CFO Value Dashboard</h1>
        <p>
          Buyer-facing value dashboard separating potential value from realized value, showing payback period and producing an exportable CFO evidence pack with ledger IDs, provenance and caveats.
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
          <h2>1. Value buckets</h2>
          {dashboard?.buckets.length ? (
            <StandardP2Table>
              <thead><tr><th>Bucket</th><th>Kind</th><th>Expected</th><th>Source</th></tr></thead>
              <tbody>
                {dashboard.buckets.map((bucket) => (
                  <tr key={bucket.bucketCode}>
                    <td>{bucket.label}</td>
                    <td>{String(bucket.valueKind)}</td>
                    <td>{asMoney(bucket.expectedValue, bucket.currencyCode)}</td>
                    <td>{bucket.source}</td>
                  </tr>
                ))}
              </tbody>
            </StandardP2Table>
          ) : <p>Dashboard data is loading...</p>}
        </div>

        <div>
          <h2>2. CFO evidence pack</h2>
          <p>Evidence pack reconciles potential value, realized value, payback period, recommendation IDs and ledger entry IDs.</p>
          <StandardButton type="button" isDisabled={busy} onClick={() => void exportEvidencePack()}>Export CFO evidence pack</StandardButton>
          {evidencePack ? (
            <div>
              <strong>{evidencePack.evidencePackId}</strong>
              <p>Potential: {asMoney(evidencePack.potentialExpectedValue, evidencePack.currencyCode)}</p>
              <p>Realized: {asMoney(evidencePack.realizedExpectedValue, evidencePack.currencyCode)}</p>
              <p>Payback: {asNumber(evidencePack.paybackPeriodMonths)} months</p>
            </div>
          ) : null}
        </div>
      </section>

      <section>
        <h2>3. CFO caveats and guardrails</h2>
        <ul>
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
