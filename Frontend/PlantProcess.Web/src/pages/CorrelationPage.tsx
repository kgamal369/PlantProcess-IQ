import { useState } from "react";
import { GitBranch, Search } from "lucide-react";
import { plantProcessApi } from "../api/plantProcessApi";

const API_BASE_URL = plantProcessApi.apiBaseUrl;

export function CorrelationPage() {
  const [parameterCode, setParameterCode] = useState("CastingSpeed");
  const [defectType, setDefectType] = useState("SurfaceCrack");
  const [result, setResult] = useState<any>(null);
  const [error, setError] = useState("");

  async function runCorrelation() {
    setError("");

    try {
      const url =
        `${API_BASE_URL}/analytics/correlations/parameter-defect` +
        `?parameterCode=${encodeURIComponent(parameterCode)}` +
        `&defectType=${encodeURIComponent(defectType)}` +
        `&bins=5&minimumObservationsPerBin=1&persistResult=true`;

      const response = await fetch(url);

      if (!response.ok) {
        throw new Error(await response.text());
      }

      setResult(await response.json());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Correlation request failed.");
    }
  }

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h1>Correlation Analysis</h1>
          <p>Analyze which parameter ranges are linked to quality defects.</p>
        </div>
      </div>

      {error && <div className="error-box">{error}</div>}

      <div className="toolbar">
        <input
          value={parameterCode}
          onChange={(e) => setParameterCode(e.target.value)}
          placeholder="Parameter code"
        />
        <input
          value={defectType}
          onChange={(e) => setDefectType(e.target.value)}
          placeholder="Defect type"
        />
        <button onClick={runCorrelation}>
          <Search size={16} />
          Run Correlation
        </button>
      </div>

      <div className="panel">
        <h3>
          <GitBranch size={18} />
          Correlation Result
        </h3>
        <pre>{JSON.stringify(result, null, 2)}</pre>
      </div>
    </section>
  );
}