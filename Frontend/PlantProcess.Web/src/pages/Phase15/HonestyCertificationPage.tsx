import { useEffect, useMemo, useState } from "react";
import { phase15AdvisoryApi, type P15HonestyCertificationContract, type P15HonestyCertificationHealth, type P15HonestyCertificationReport } from "@/api/phase15Advisory";

const cardStyle = { border: "1px solid rgba(124,220,255,0.18)", borderRadius: 22, padding: 20, background: "linear-gradient(135deg, rgba(7,18,34,0.95), rgba(4,10,22,0.9))", boxShadow: "0 18px 50px rgba(0,0,0,0.22)" } as const;
const buttonStyle = { border: "1px solid rgba(0,212,255,0.34)", borderRadius: 14, padding: "10px 14px", color: "#eaf7ff", background: "rgba(0,132,255,0.24)", cursor: "pointer", fontWeight: 700 } as const;

export function HonestyCertificationPage() {
  const [health, setHealth] = useState<P15HonestyCertificationHealth | null>(null);
  const [contract, setContract] = useState<P15HonestyCertificationContract | null>(null);
  const [report, setReport] = useState<P15HonestyCertificationReport | null>(null);
  const [status, setStatus] = useState("Loading Phase 15 honesty certification...");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    setBusy(true);
    try {
      const [nextHealth, nextContract] = await Promise.all([
        phase15AdvisoryApi.honestyCertificationHealth(),
        phase15AdvisoryApi.honestyCertificationContract(),
      ]);
      setHealth(nextHealth);
      setContract(nextContract);
      setStatus("Honesty certification service is reachable.");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`Honesty certification not reachable: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => { void refresh(); }, []);

  async function runCertification() {
    setBusy(true);
    try {
      const result = await phase15AdvisoryApi.runHonestyCertification();
      setReport(result);
      setStatus(result.failedCases === 0 ? "Certification passed." : "Certification failed. Review failed cases.");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`Certification run failed: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  const summaryCards = useMemo(() => [
    { label: "Mode", value: health?.mode ?? "pending" },
    { label: "Status", value: report?.status ?? "not run" },
    { label: "Passed", value: String(report?.passedCases ?? 0) },
    { label: "Failed", value: String(report?.failedCases ?? 0) },
    { label: "Approval", value: health?.approvalRequired ? "Required" : "pending" },
    { label: "Write-back", value: health ? (health.automaticWriteBackBlocked ? "Blocked" : "Not blocked") : "pending" },
  ], [health, report]);

  return (
    <main style={{ display: "grid", gap: 18, color: "#eaf7ff" }} data-testid="phase15-honesty-certification-page">
      <section style={{ ...cardStyle, borderColor: "rgba(19,216,255,0.28)" }}>
        <p style={{ color: "#13d8ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12, margin: 0 }}>
          Pack G · T-101 · Recommendation honesty & approval certification
        </p>
        <h1 style={{ margin: "8px 0 8px", fontSize: 34 }}>Phase 15 Honesty Certification</h1>
        <p style={{ margin: 0, color: "#9ab8d7", maxWidth: 980, lineHeight: 1.65 }}>
          Adversarial certification for the advisory layer. It verifies no causal claims, no guaranteed savings, weak-evidence blocking, out-of-envelope abstain, explicit approval requirement and no automatic write-back.
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
          <h2 style={{ marginTop: 0 }}>1. Certification cases</h2>
          <button type="button" disabled={busy} onClick={() => void runCertification()} style={buttonStyle}>Run honesty certification</button>
          {report?.cases.length ? (
            <table style={{ width: "100%", borderCollapse: "collapse", marginTop: 12 }}>
              <thead><tr style={{ color: "#9ab8d7", textAlign: "left" }}><th style={{ padding: 8 }}>Case</th><th style={{ padding: 8 }}>Result</th><th style={{ padding: 8 }}>Expected</th><th style={{ padding: 8 }}>Actual</th></tr></thead>
              <tbody>
                {report.cases.map((item) => (
                  <tr key={item.caseCode} style={{ borderTop: "1px solid rgba(120,190,255,0.12)" }}>
                    <td style={{ padding: 8 }}>{item.title}</td>
                    <td style={{ padding: 8, color: item.passed ? "#64ffda" : "#ffb86b" }}>{item.passed ? "PASS" : "FAIL"}</td>
                    <td style={{ padding: 8, color: "#9ab8d7" }}>{item.expectedBehavior}</td>
                    <td style={{ padding: 8, color: "#9ab8d7" }}>{item.actualBehavior}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : <p style={{ color: "#9ab8d7" }}>Run certification to verify advisory honesty guardrails.</p>}
        </div>

        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>2. Required guardrails</h2>
          <ul style={{ color: "#9ab8d7", lineHeight: 1.75, paddingLeft: 18 }}>
            {(report?.requiredGuardrails ?? contract?.guardrails ?? [
              "No causal language.",
              "No guaranteed saving claim.",
              "Weak evidence blocks recommendation.",
              "No automatic write-back path.",
            ]).map((rule) => <li key={rule}>{rule}</li>)}
          </ul>
        </div>
      </section>
    </main>
  );
}
