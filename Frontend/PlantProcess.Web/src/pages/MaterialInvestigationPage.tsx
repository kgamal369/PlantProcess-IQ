import { useEffect, useState } from "react";
import { Download, Search, ShieldCheck } from "lucide-react";
import { plantProcessApi } from "../api/plantProcessApi";
import { MetricCard } from "../components/MetricCard";

export function MaterialInvestigationPage() {
  const [materials, setMaterials] = useState<any[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [investigation, setInvestigation] = useState<any>(null);
  const [features, setFeatures] = useState<any>(null);
  const [riskResult, setRiskResult] = useState<any>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    plantProcessApi
      .getMaterialSample(20)
      .then((rows) => {
        setMaterials(rows);
        if (rows.length > 0) setSelectedId(rows[0].id);
      })
      .catch((err) => setError(err.message));
  }, []);

  async function loadInvestigation() {
    if (!selectedId) return;

    setError("");
    setRiskResult(null);

    try {
      const [investigationResult, featuresResult] = await Promise.all([
        plantProcessApi.getMaterialInvestigation(selectedId),
        plantProcessApi.getMaterialFeatures(selectedId),
      ]);

      setInvestigation(investigationResult);
      setFeatures(featuresResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load material investigation.");
    }
  }

  async function calculateRisk() {
    if (!selectedId) return;

    try {
      const result = await plantProcessApi.calculateRisk(selectedId);
      setRiskResult(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to calculate risk.");
    }
  }

  const pdfUrl = selectedId ? plantProcessApi.getInvestigationPdfUrl(selectedId) : "#";

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h1>Material Investigation</h1>
          <p>Inspect genealogy, process history, quality events, features and risk.</p>
        </div>
      </div>

      {error && <div className="error-box">{error}</div>}

      <div className="toolbar">
        <select value={selectedId} onChange={(e) => setSelectedId(e.target.value)}>
          {materials.map((m) => (
            <option key={m.id} value={m.id}>
              {m.materialCode} — {m.materialUnitType}
            </option>
          ))}
        </select>

        <button onClick={loadInvestigation}>
          <Search size={16} />
          Load Investigation
        </button>

        <button onClick={calculateRisk}>
          <ShieldCheck size={16} />
          Calculate Risk
        </button>

        <a className="button-link" href={pdfUrl} target="_blank" rel="noreferrer">
          <Download size={16} />
          Download PDF
        </a>
      </div>

      <div className="metric-grid">
        <MetricCard title="Materials" value={investigation?.summary?.materials ?? "-"} />
        <MetricCard title="Process Steps" value={investigation?.summary?.processSteps ?? "-"} />
        <MetricCard title="Parameters" value={investigation?.summary?.parameterObservations ?? "-"} />
        <MetricCard title="Quality Events" value={investigation?.summary?.qualityEvents ?? "-"} />
        <MetricCard title="Risk Scores" value={investigation?.summary?.riskScores ?? "-"} />
        <MetricCard title="DQ Issues" value={investigation?.summary?.dataQualityIssues ?? "-"} />
      </div>

      {riskResult && (
        <div className="panel success-panel">
          <h3>Calculated Risk</h3>
          <pre>{JSON.stringify(riskResult, null, 2)}</pre>
        </div>
      )}

      <div className="two-column">
        <div className="panel">
          <h3>Feature Vector</h3>
          <pre>{JSON.stringify(features, null, 2)}</pre>
        </div>

        <div className="panel">
          <h3>Investigation Result</h3>
          <pre>{JSON.stringify(investigation, null, 2)}</pre>
        </div>
      </div>
    </section>
  );
}