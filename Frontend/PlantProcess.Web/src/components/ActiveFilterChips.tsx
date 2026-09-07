import { X } from "lucide-react";
import { useDashboardFilters } from "../state/DashboardFilterContext";
import type { DashboardFilters } from "../api/productApiClient";
import { StandardButton } from "@/components/standard";

// T-094. Labels for the STRUCTURAL filters the product owns. A declared
// dimension is not listed here. The chip uses the published code as its
// canonical identity; display labels remain catalogue data and are not compiled.
const labels: Partial<Record<keyof DashboardFilters, string>> = {
  siteId: "Site",
  areaId: "Area",
  equipmentId: "Equipment",
  materialCode: "Material",
  materialUnitType: "Material type",
  sourceSystem: "Source",
  parameterCode: "Parameter",
  fromUtc: "From",
  toUtc: "To",
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
  const { filters, clearFilter, declaredFilters, clearDeclaredFilter } = useDashboardFilters();

  const entries = Object.entries(filters).filter(
    ([key, value]) =>
      labels[key as keyof DashboardFilters] !== undefined &&
      value !== undefined && value !== null && value !== ""
  ) as [keyof DashboardFilters, string | number][];

  if (entries.length === 0 && declaredFilters.length === 0) {
    return (
      <div className="chip-row muted">
        No active filters. The dashboard is showing the full imported dataset.
      </div>
    );
  }

  return (
    <div className="chip-row">
      {entries.map(([key, value]) => (
        <StandardButton
          key={String(key)}
          className="filter-chip"
          onClick={() => clearFilter(key)}
          title="Remove filter"
        >
          <strong>{labels[key]}:</strong> {formatValue(key, value)}
          <X size={13} />
        </StandardButton>
      ))}
      {declaredFilters.map((filter) => (
        <StandardButton
          key={"declared:" + filter.code}
          className="filter-chip"
          onClick={() => clearDeclaredFilter(filter.code)}
          title="Remove filter"
        >
          <strong>{filter.code}:</strong> {filter.value}
          <X size={13} />
        </StandardButton>
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