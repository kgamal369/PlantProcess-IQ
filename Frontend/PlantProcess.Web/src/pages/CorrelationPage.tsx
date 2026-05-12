import { useEffect, useMemo, useState } from "react";
import {
  GitBranch,
  Network,
  RefreshCw,
  Sigma,
  TrendingUp,
} from "lucide-react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { plantProcessApi } from "../api/plantProcessApi";
import type { GenealogyAwareCorrelationResult } from "../api/plantProcessApi";;
import { ActiveFilterChips } from "../components/ActiveFilterChips";
import { DashboardFilterBar } from "../components/DashboardFilterBar";
import { ErrorPanel, LoadingPanel } from "../components/AsyncState";
import { useDashboardFilters } from "../state/DashboardFilterContext";

export function CorrelationPage() {
  const { filters, setFilter } = useDashboardFilters();
  const [result, setResult] =
    useState<GenealogyAwareCorrelationResult | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const effectiveFilters = useMemo(
    () => ({
      ...filters,
      parameterCode: filters.parameterCode || "CastingSpeed",
      defectType: filters.defectType || "SurfaceCrack",
      linkMode: filters.linkMode || "DownstreamChildren",
      genealogyDepth: filters.genealogyDepth ?? 3,
      bins: filters.bins ?? 8,
      minimumObservationsPerBin: filters.minimumObservationsPerBin ?? 3,
    }),
    [filters]
  );

  async function load() {
    setIsLoading(true);
    setError(null);

    try {
      const response =
        await plantProcessApi.getGenealogyAwareCorrelation(effectiveFilters);
        setResult(response);
        
        //  persist correlation run metadata.
        // This gives an auditable correlation run without claiming root cause.
        await plantProcessApi.persistCorrelationRun(effectiveFilters, response);
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [effectiveFilters]);

  const chartData = useMemo(() => {
    return (result?.bins ?? []).map((bin) => ({
      label: bin.binLabel,
      defectRate: bin.defectRatePercent,
      lift: bin.liftVsBaseline ?? 0,
      observations: bin.observationCount,
      confidence: bin.confidence,
    }));
  }, [result]);

  return (
    <main className="page-shell">
      <DashboardFilterBar />
      <ActiveFilterChips />

      <section className="correlation-hero">
        <div>
          <div className="eyebrow">
            <Network size={14} />
            Phase 10 — Genealogy-aware correlation
          </div>
          <h1>Process-to-Quality Correlation</h1>
          <p>
            Connect upstream process parameters to downstream quality outcomes
            through material genealogy.
          </p>
        </div>

        <button className="primary-button" onClick={load}>
          <RefreshCw size={16} />
          Recalculate
        </button>
      </section>

      {isLoading && <LoadingPanel text="Calculating genealogy-aware correlation..." />}
      {error ? <ErrorPanel error={error} /> : null}
      {!isLoading && !error && result && (
        <>
          <section className="metric-grid">
            <CorrelationMetric
              icon={<Sigma size={20} />}
              label="Observations"
              value={result.totalObservationCount}
              note="Numeric parameter observations"
            />
            <CorrelationMetric
              icon={<GitBranch size={20} />}
              label="Linked Materials"
              value={result.totalMaterialCount}
              note={`${result.linkMode}, depth ${result.genealogyDepth}`}
            />
            <CorrelationMetric
              icon={<TrendingUp size={20} />}
              label="Baseline Defect Rate"
              value={`${formatNumber(result.baselineDefectRatePercent)}%`}
              note={result.defectType}
            />
            <CorrelationMetric
              icon={<Network size={20} />}
              label="Linked Defect Observations"
              value={result.totalDefectLinkedObservationCount}
              note="Defect matched through genealogy"
            />
          </section>

          <section className="dashboard-grid">
            <section className="dashboard-panel wide-panel">
              <div className="panel-header">
                <div>
                  <h3>
                    <TrendingUp size={18} />
                    Defect Rate by Parameter Bin
                  </h3>
                  <p>
                    Parameter: <strong>{result.parameterCode}</strong> → Defect:{" "}
                    <strong>{result.defectType}</strong>. Click a bar to change
                    bin count for exploration.
                  </p>
                </div>
              </div>

              <div className="chart-box chart-box--large">
                <ResponsiveContainer width="100%" height={360}>
                  <BarChart data={chartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="label" />
                    <YAxis />
                    <Tooltip />
                    <Bar dataKey="defectRate" name="Defect rate %">
                      {chartData.map((item, index) => (
                        <Cell
                          key={index}
                          className={
                            item.lift >= 1.5
                              ? "bar-hot"
                              : item.lift >= 1.1
                              ? "bar-warm"
                              : "bar-normal"
                          }
                        />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </section>

            <section className="dashboard-panel">
              <div className="panel-header">
                <div>
                  <h3>
                    <GitBranch size={18} />
                    Genealogy Mode
                  </h3>
                  <p>Control how source material observations connect to quality events.</p>
                </div>
              </div>

              <div className="mode-grid">
                {[
                  ["SameMaterial", "Same material only"],
                  ["DownstreamChildren", "Upstream parameter → downstream defect"],
                  ["UpstreamParents", "Downstream material → upstream context"],
                  ["FullGenealogy", "Parents + children"],
                ].map(([value, label]) => (
                  <button
                    key={value}
                    className={
                      result.linkMode === value
                        ? "mode-card mode-card--active"
                        : "mode-card"
                    }
                    onClick={() => setFilter("linkMode", value as any)}
                  >
                    <strong>{label}</strong>
                    <span>{value}</span>
                  </button>
                ))}
              </div>
            </section>

            <section className="dashboard-panel wide-panel">
              <div className="panel-header">
                <div>
                  <h3>
                    <Sigma size={18} />
                    Correlation Result Table
                  </h3>
                  <p>No raw rows. Aggregated dashboard-ready bins only.</p>
                </div>
              </div>

              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Bin</th>
                      <th>Range</th>
                      <th>Observations</th>
                      <th>Materials</th>
                      <th>Defect Obs.</th>
                      <th>Defect Rate</th>
                      <th>Lift</th>
                      <th>Confidence</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.bins.length === 0 && (
                      <tr>
                        <td colSpan={8}>No bins passed the selected minimum observation threshold.</td>
                      </tr>
                    )}

                    {result.bins.map((bin) => (
                      <tr key={bin.binNo}>
                        <td>{bin.binNo}</td>
                        <td>{bin.binLabel}</td>
                        <td>{bin.observationCount}</td>
                        <td>{bin.materialCount}</td>
                        <td>{bin.defectLinkedObservationCount}</td>
                        <td>{formatNumber(bin.defectRatePercent)}%</td>
                        <td>
                          {bin.liftVsBaseline == null
                            ? "-"
                            : `${formatNumber(bin.liftVsBaseline)}x`}
                        </td>
                        <td>
                          <span
                            className={`confidence-pill confidence-${bin.confidence.toLowerCase()}`}
                          >
                            {bin.confidence}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          </section>

          <section className="insight-box">
            <strong>Engineering interpretation</strong>
            <p>
              This is not claiming automatic root cause. It shows suspected risk
              indicators by comparing defect rate per parameter range against
              the selected baseline while following material genealogy.
            </p>
            <small>{result.message}</small>
          </section>
        </>
      )}
    </main>
  );
}

function CorrelationMetric({
  icon,
  label,
  value,
  note,
}: {
  icon: React.ReactNode;
  label: string;
  value: string | number;
  note: string;
}) {
  return (
    <div className="metric-tile">
      <div className="metric-icon">{icon}</div>
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
        <small>{note}</small>
      </div>
    </div>
  );
}

function formatNumber(value: number) {
  return new Intl.NumberFormat(undefined, {
    maximumFractionDigits: 2,
  }).format(value);
}