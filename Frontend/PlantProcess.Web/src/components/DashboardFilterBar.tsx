// ============================================================
// FILE: Frontend/PlantProcess.Web/src/components/DashboardFilterBar.tsx
// Complete redesign — professional filter bar.
// Removed: "QlikSense-style" label, raw filter-grid layout.
// Added: grouped pill-style filter row with icon badges.
// ============================================================

import { useEffect, useMemo, useState } from "react";
import {
  CalendarDays,
  ChevronDown,
  Database,
  Filter,
  GitMerge,
  RotateCcw,
  Search,
  ShieldAlert,
  Sliders,
  Thermometer,
  Wrench,
} from "lucide-react";
import { plantProcessApi, type DashboardReferenceData } from "../api/plantProcessApi";
import { useDashboardFilters } from "../state/DashboardFilterContext";
import "./DashboardFilterBar.css";

// ── FilterSelect ──────────────────────────────────────────────
function FilterSelect({
  label,
  icon: Icon,
  value,
  onChange,
  children,
  active,
}: {
  label: string;
  icon: React.ElementType;
  value: string;
  onChange: (v: string) => void;
  children: React.ReactNode;
  active?: boolean;
}) {
  return (
    <label className={`piq-filter-select ${active ? "piq-filter-select--active" : ""}`}>
      <span className="piq-filter-select__icon" aria-hidden="true">
        <Icon size={12} />
      </span>
      <span className="piq-filter-select__label">{label}</span>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="piq-filter-select__native"
      >
        {children}
      </select>
      <ChevronDown size={11} className="piq-filter-select__caret" aria-hidden="true" />
    </label>
  );
}

// ── FilterInput ───────────────────────────────────────────────
function FilterInput({
  label,
  icon: Icon,
  value,
  onChange,
  placeholder,
  type = "text",
  active,
}: {
  label: string;
  icon: React.ElementType;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  type?: string;
  active?: boolean;
}) {
  return (
    <label className={`piq-filter-input ${active ? "piq-filter-input--active" : ""}`}>
      <span className="piq-filter-input__icon" aria-hidden="true">
        <Icon size={12} />
      </span>
      <span className="piq-filter-input__label">{label}</span>
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="piq-filter-input__native"
      />
    </label>
  );
}

// ── Main component ────────────────────────────────────────────
export function DashboardFilterBar() {
  const { filters, setFilter, clearAllFilters, activeFilterCount } =
    useDashboardFilters();

  const [referenceData, setReferenceData] =
    useState<DashboardReferenceData | null>(null);

  const materialSearchLabel = useMemo(() => {
    if (!filters.materialCode) return "Material";
    return `Material: ${filters.materialCode}`;
  }, [filters.materialCode]);

  useEffect(() => {
    let ignore = false;
    plantProcessApi
      .getDashboardReferenceData(filters)
      .then((data) => { if (!ignore) setReferenceData(data); })
      .catch(() => { if (!ignore) setReferenceData(null); });
    return () => { ignore = true; };
  }, [filters.siteId]);

  const hasActiveFilters = activeFilterCount > 0;

  return (
    <section className="piq-filter-bar" aria-label="Dashboard filters">
      {/* Header row */}
      <div className="piq-filter-bar__header">
        <div className="piq-filter-bar__title">
          <Sliders size={14} aria-hidden="true" />
          <span>Global filters</span>
          {hasActiveFilters && (
            <span className="piq-filter-bar__count">{activeFilterCount} active</span>
          )}
        </div>
        <button
          className={`piq-filter-clear ${hasActiveFilters ? "piq-filter-clear--visible" : ""}`}
          onClick={clearAllFilters}
          type="button"
          title="Clear all filters"
          disabled={!hasActiveFilters}
        >
          <RotateCcw size={12} />
          Clear all
        </button>
      </div>

      {/* Filter row — Group 1: Location */}
      <div className="piq-filter-row">
        <span className="piq-filter-group-label">Location</span>

        <FilterSelect
          label="Site"
          icon={Database}
          value={filters.siteId ?? ""}
          onChange={(v) => setFilter("siteId", v || undefined)}
          active={!!filters.siteId}
        >
          <option value="">All sites</option>
          {referenceData?.sites.map((item) => (
            <option key={item.id} value={item.id}>
              {item.code} — {item.name}
            </option>
          ))}
        </FilterSelect>

        <FilterSelect
          label="Area"
          icon={Filter}
          value={filters.areaId ?? ""}
          onChange={(v) => setFilter("areaId", v || undefined)}
          active={!!filters.areaId}
        >
          <option value="">All areas</option>
          {referenceData?.areas.map((item) => (
            <option key={item.id} value={item.id}>
              {item.code} — {item.name}
            </option>
          ))}
        </FilterSelect>

        <FilterSelect
          label="Equipment"
          icon={Wrench}
          value={filters.equipmentId ?? ""}
          onChange={(v) => setFilter("equipmentId", v || undefined)}
          active={!!filters.equipmentId}
        >
          <option value="">All equipment</option>
          {referenceData?.equipment.map((item) => (
            <option key={item.id} value={item.id}>
              {item.code} — {item.name}
            </option>
          ))}
        </FilterSelect>

        <FilterSelect
          label="Source"
          icon={Database}
          value={filters.sourceSystem ?? ""}
          onChange={(v) => setFilter("sourceSystem", v || undefined)}
          active={!!filters.sourceSystem}
        >
          <option value="">All source systems</option>
          {referenceData?.sourceSystems.map((item) => (
            <option key={item.id} value={item.code}>
              {item.code} — {item.name}
            </option>
          ))}
        </FilterSelect>

        <FilterInput
          label={materialSearchLabel}
          icon={Search}
          value={filters.materialCode ?? ""}
          onChange={(v) => setFilter("materialCode", v || undefined)}
          placeholder="Search material / batch / lot"
          active={!!filters.materialCode}
        />
      </div>

      {/* Filter row — Group 2: Process & Quality */}
      <div className="piq-filter-row">
        <span className="piq-filter-group-label">Process & Quality</span>

        <FilterSelect
          label="Parameter"
          icon={Thermometer}
          value={filters.parameterCode ?? "CastingSpeed"}
          onChange={(v) => setFilter("parameterCode", v || undefined)}
          active={!!filters.parameterCode}
        >
          {referenceData?.parameters.length ? (
            referenceData.parameters.map((item) => (
              <option key={item.id} value={item.code}>
                {item.code} — {item.name}
              </option>
            ))
          ) : (
            <>
              <option value="CastingSpeed">CastingSpeed</option>
              <option value="Superheat">Superheat</option>
              <option value="RollingForce">RollingForce</option>
            </>
          )}
        </FilterSelect>

        <FilterSelect
          label="Defect"
          icon={ShieldAlert}
          value={filters.defectType ?? ""}
          onChange={(v) => setFilter("defectType", v || undefined)}
          active={!!filters.defectType}
        >
          <option value="">All defects</option>
          {referenceData?.defects.map((item) => (
            <option key={item.id} value={item.code}>
              {item.code} — {item.name}
            </option>
          ))}
        </FilterSelect>

        <FilterSelect
          label="Risk class"
          icon={Filter}
          value={filters.riskClass ?? ""}
          onChange={(v) => setFilter("riskClass", v || undefined)}
          active={!!filters.riskClass}
        >
          <option value="">All risk classes</option>
          {referenceData?.riskClasses.map((item) => (
            <option key={item.id} value={item.code}>
              {item.name} ({item.count})
            </option>
          ))}
        </FilterSelect>

        <FilterSelect
          label="Shift / Crew"
          icon={Filter}
          value={filters.shiftCode ?? ""}
          onChange={(v) => setFilter("shiftCode", v || undefined)}
          active={!!filters.shiftCode}
        >
          <option value="">All shifts/crews</option>
          {referenceData?.shifts.map((item) => (
            <option key={item.id} value={item.code}>
              {item.name} ({item.count})
            </option>
          ))}
        </FilterSelect>

        <FilterSelect
          label="Genealogy"
          icon={GitMerge}
          value={filters.linkMode ?? "DownstreamChildren"}
          onChange={(v) => setFilter("linkMode", v as any)}
          active={filters.linkMode !== "DownstreamChildren" && !!filters.linkMode}
        >
          <option value="SameMaterial">Same material only</option>
          <option value="DownstreamChildren">Downstream children</option>
          <option value="UpstreamParents">Upstream parents</option>
          <option value="FullGenealogy">Full genealogy</option>
        </FilterSelect>
      </div>

      {/* Filter row — Group 3: Time range */}
      <div className="piq-filter-row">
        <span className="piq-filter-group-label">Time range</span>

        <FilterInput
          label="From UTC"
          icon={CalendarDays}
          type="datetime-local"
          value={toLocalInput(filters.fromUtc)}
          onChange={(v) => setFilter("fromUtc", toUtcValue(v))}
          active={!!filters.fromUtc}
        />

        <FilterInput
          label="To UTC"
          icon={CalendarDays}
          type="datetime-local"
          value={toLocalInput(filters.toUtc)}
          onChange={(v) => setFilter("toUtc", toUtcValue(v))}
          active={!!filters.toUtc}
        />
      </div>
    </section>
  );
}

// ── Helpers ───────────────────────────────────────────────────
function toLocalInput(value?: string): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const offsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}

function toUtcValue(value: string): string | undefined {
  if (!value) return undefined;
  return new Date(value).toISOString();
}
