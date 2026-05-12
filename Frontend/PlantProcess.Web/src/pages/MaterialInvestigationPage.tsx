import { useEffect, useState } from "react";
import { Download, Search, ShieldCheck } from "lucide-react";
import { plantProcessApi, type DashboardMaterialRow } from "../api/plantProcessApi";
import { MetricCard } from "../components/MetricCard";
import { SortableDataTable } from "../components/SortableDataTable";
import type { SortableColumn } from "../components/SortableDataTable";
import { useDashboardFilters } from "../state/DashboardFilterContext";

export function MaterialInvestigationPage() {
  const { filters, mergeFilters } = useDashboardFilters();
  const [materials, setMaterials] = useState<DashboardMaterialRow[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [materialSearch, setMaterialSearch] = useState(filters.materialCode ?? "");
  const [investigation, setInvestigation] = useState<any>(null);
  const [features, setFeatures] = useState<any>(null);
  const [riskResult, setRiskResult] = useState<any>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    plantProcessApi
      .searchDashboardMaterials({
        ...filters,
        materialCode: materialSearch || filters.materialCode,
        page: 1,
        pageSize: 25,
        sortBy: "materialCode",
        sortDirection: "asc",
      })
      .then((result) => {
        setMaterials(result.items);
        if (result.items.length > 0 && !selectedId) {
          setSelectedId(result.items[0].materialUnitId);
        }
      })
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load materials.")
      );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.siteId, filters.sourceSystem, filters.riskClass]);

  async function searchMaterials() {
    setError("");

    try {
      const result = await plantProcessApi.searchDashboardMaterials({
        ...filters,
        materialCode: materialSearch,
        page: 1,
        pageSize: 25,
        sortBy: "materialCode",
        sortDirection: "asc",
      });

      setMaterials(result.items);
      if (result.items.length > 0) {
        setSelectedId(result.items[0].materialUnitId);
      }

      mergeFilters({ materialCode: materialSearch || undefined, page: 1 });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to search materials.");
    }
  }

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
      setError(
        err instanceof Error ? err.message : "Failed to load material investigation."
      );
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

  const selectedMaterial = materials.find((x) => x.materialUnitId === selectedId);
  const pdfUrl = selectedId ? plantProcessApi.getInvestigationPdfUrl(selectedId) : "#";

  const columns: SortableColumn<DashboardMaterialRow>[] = [
    {
      key: "materialCode",
      title: "Material",
      render: (row) => (
        <button
          className="link-button"
          onClick={() => {
            setSelectedId(row.materialUnitId);
            mergeFilters({ materialCode: row.materialCode });
          }}
        >
          {row.materialCode}
        </button>
      ),
    },
    {
      key: "materialUnitType",
      title: "Type",
      render: (row) => row.materialUnitType,
    },
    {
      key: "productFamily",
      title: "Family",
      render: (row) => row.productFamily ?? "-",
    },
    {
      key: "latestRiskClass",
      title: "Risk",
      render: (row) => row.latestRiskClass ?? "-",
    },
    {
      key: "defectEventCount",
      title: "Defects",
      align: "right",
      render: (row) => row.defectEventCount,
    },
  ];

  return (
    <section className="page-shell">
      <div className="page-title">
        <div>
          <h1>Material Search and Drilldown</h1>
          <p>
            Search material/batch/coil/lot, then inspect genealogy, process
            history, quality events, feature vector and risk.
          </p>
        </div>
      </div>

      {error ? <div className="error-box">{error}</div> : null}

      <div className="toolbar">
        <input
          value={materialSearch}
          onChange={(event) => setMaterialSearch(event.target.value)}
          placeholder="Search material code..."
        />
        <button onClick={searchMaterials}>
          <Search size={16} />
          Search
        </button>
        <button onClick={loadInvestigation} disabled={!selectedId}>
          <Search size={16} />
          Load Investigation
        </button>
        <button onClick={calculateRisk} disabled={!selectedId}>
          <ShieldCheck size={16} />
          Calculate Risk
        </button>
        <a className="button-link" href={pdfUrl} target="_blank" rel="noreferrer">
          <Download size={16} />
          PDF Report
        </a>
      </div>

      <section className="dashboard-panel">
        <div className="panel-header">
          <div>
            <h3>Search Results</h3>
            <p>Click one material to drill down and synchronize global filters.</p>
          </div>
        </div>

        <SortableDataTable
          rows={materials}
          columns={columns}
          emptyText="No materials found."
        />
      </section>

      <div className="metric-grid">
        <MetricCard title="Selected Material" value={selectedMaterial?.materialCode ?? "-"} />
        <MetricCard title="Type" value={selectedMaterial?.materialUnitType ?? "-"} />
        <MetricCard title="Risk Class" value={selectedMaterial?.latestRiskClass ?? "-"} />
        <MetricCard title="Defects" value={selectedMaterial?.defectEventCount ?? "-"} />
      </div>

      {riskResult ? (
        <div className="panel">
          <h3>Latest Risk Calculation</h3>
          <pre>{JSON.stringify(riskResult, null, 2)}</pre>
        </div>
      ) : null}

      {investigation ? (
        <div className="panel">
          <h3>Material Investigation</h3>
          <pre>{JSON.stringify(investigation, null, 2)}</pre>
        </div>
      ) : null}

      {features ? (
        <div className="panel">
          <h3>Feature Vector</h3>
          <pre>{JSON.stringify(features, null, 2)}</pre>
        </div>
      ) : null}
    </section>
  );
}