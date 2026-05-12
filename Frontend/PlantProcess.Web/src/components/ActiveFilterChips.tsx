import { X } from "lucide-react";
import { useDashboardFilters } from "../state/DashboardFilterContext";
import type { DashboardFilters } from "../api/plantProcessApi";

const labels: Record<keyof DashboardFilters, string> = {
  siteId: "Site",
  areaId: "Area",
  equipmentId: "Equipment",
  materialCode: "Material",
  sourceSystem: "Source",
  defectType: "Defect",
  parameterCode: "Parameter",
  riskClass: "Risk",
  fromUtc: "From",
  toUtc: "To",
  shiftCode: "Shift",
  linkMode: "Genealogy",
  genealogyDepth: "Depth",
  bins: "Bins",
  minimumObservationsPerBin: "Min/bin",

  // Phase 9 pagination / sorting
  page: "Page",
  pageSize: "Page size",
  sortBy: "Sort by",
  sortDirection: "Sort",
};

export function ActiveFilterChips() {
  const { filters, clearFilter } = useDashboardFilters();

  const entries = Object.entries(filters).filter(
    ([, value]) => value !== undefined && value !== null && value !== ""
  ) as [keyof DashboardFilters, string | number][];

  if (entries.length === 0) {
    return (
      <div className="chip-row muted">
        No active filters. Dashboard is showing the current available demo
        dataset.
      </div>
    );
  }

  return (
    <div className="chip-row">
      {entries.map(([key, value]) => (
        <button
          key={key}
          className="filter-chip"
          onClick={() => clearFilter(key)}
          title="Remove filter"
        >
          <strong>{labels[key]}:</strong> {formatValue(key, value)}
          <X size={13} />
        </button>
      ))}
    </div>
  );
}

function formatValue(key: keyof DashboardFilters, value: string | number) {
  if (key === "fromUtc" || key === "toUtc") {
    const date = new Date(String(value));
    if (!Number.isNaN(date.getTime())) {
      return date.toLocaleString();
    }
  }

  return String(value);
}