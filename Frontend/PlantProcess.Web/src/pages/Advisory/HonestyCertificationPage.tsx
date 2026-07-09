import { useEffect, useMemo, useState } from "react";
import { advisoryApi, type HonestyCertificationContract, type HonestyCertificationHealth, type HonestyCertificationReport } from "@/api/advisoryApi";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { StandardButton, StandardPageHeader, StandardStatGrid } from "@/components/standard";
const cardStyle = { border: "1px solid rgba(124,220,255,0.18)", borderRadius: 22, padding: 20, background: "linear-gradient(135deg, rgba(7,18,34,0.95), rgba(4,10,22,0.9))", boxShadow: "0 18px 50px rgba(0,0,0,0.22)" } as const;
const buttonStyle = { border: "1px solid rgba(0,212,255,0.34)", borderRadius: 14, padding: "10px 14px", color: "#eaf7ff", background: "rgba(0,132,255,0.24)", cursor: "pointer", fontWeight: 700 } as const;

export function HonestyCertificationPage() {
  const [health, setHealth] = useState<HonestyCertificationHealth | null>(null);
  const [contract, setContract] = useState<HonestyCertificationContract | null>(null);
  const [report, setReport] = useState<HonestyCertificationReport | null>(null);
  const [status, setStatus] = useState("Loading honesty certification...");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    setBusy(true);
    try {
      const [nextHealth, nextContract] = await Promise.all([
        advisoryApi.honestyCertificationHealth(),
        advisoryApi.honestyCertificationContract(),
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
      const result = await advisoryApi.runHonestyCertification();
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
    { label: "Status", value: report?.status ?? "not run" },
    { label: "Passed", value: String(report?.passedCases ?? 0) },
    { label: "Failed", value: String(report?.failedCases ?? 0) },
    { label: "Approval", value: health?.approvalRequired ? "Required" : "pending" },
    { label: "Write-back", value: health ? (health.automaticWriteBackBlocked ? "Blocked" : "Not blocked") : "pending" },
  ], [health, report]);

  return (
    <main data-testid="honesty-certification-page">
      <StandardPageHeader
        title="Advisory Honesty Certification"
        subtitle="Independent verification that advisory output never overstates what the evidence supports."
        description="Before any recommendation reaches an engineer, it is tested against a fixed set of honesty rules. This page runs those tests on demand and shows exactly which passed and which failed. A failed certification blocks the recommendation; it does not annotate it."
        status={status}
      />

      <StandardStatGrid items={summaryCards} emphasize="Status" />

      <section>
        <div>
          <h2>1. Certification cases</h2>
          <StandardButton type="button" isDisabled={busy} onClick={() => void runCertification()}>Run honesty certification</StandardButton>
          {report?.cases.length ? (
            <StandardP2Table>
              <thead><tr><th>Case</th><th>Result</th><th>Expected</th><th>Actual</th></tr></thead>
              <tbody>
                {report.cases.map((item) => (
                  <tr key={item.caseCode}>
                    <td>{item.title}</td>
                    <td>{item.passed ? "PASS" : "FAIL"}</td>
                    <td>{item.expectedBehavior}</td>
                    <td>{item.actualBehavior}</td>
                  </tr>
                ))}
              </tbody>
            </StandardP2Table>
          ) : <p>Run certification to verify advisory honesty guardrails.</p>}
        </div>

        <div>
          <h2>2. Required guardrails</h2>
          <ul>
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
