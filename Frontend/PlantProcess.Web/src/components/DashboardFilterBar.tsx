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
  Sliders,
  Thermometer,
  Wrench,
} from "lucide-react";
import { productApi, type DashboardReferenceData } from "../api/productApiClient";
import { useDashboardFilters } from "../state/DashboardFilterContext";
import { composeEffectiveFilters, toRequestFilters } from "../state/effectiveQueryFilters";
import type { DeclaredDimensionReference, DeclaredDimensionFilter } from "../api/productApiClient";
import "./DashboardFilterBar.css";
import { StandardButton } from "@/components/standard";

import { StandardP2Input, StandardP2Select } from "@/components/standard/StandardP2Controls";
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
      <StandardP2Select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="piq-filter-select__native"
      >
        {children}
      </StandardP2Select>
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
      <StandardP2Input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="piq-filter-input__native"
      />
    </label>
  );
}

// A published dimension gets its values by executing the same governed query
// engine as every widget, under the effective workspace population MINUS its
// own selection. No option list is compiled in the client.
function DeclaredDimensionSelect({
  dimension, structural, declared, value, onChange,
}: {
  dimension: DeclaredDimensionReference;
  structural: Record<string, unknown>;
  declared: DeclaredDimensionFilter[];
  value: string;
  onChange: (value: string) => void;
}) {
  const [values, setValues] = useState<Array<{ value: string; label: string }>>([]);
  const [unavailable, setUnavailable] = useState(false);

  useEffect(() => {
    let ignore = false;
    if (!dimension.isExecutable) { setUnavailable(true); setValues([]); return; }

    const effective = composeEffectiveFilters(structural, declared, {
      omitDeclaredCode: dimension.code,
    });

    productApi.queryDashboardWidget({
      widgetType: "chart", chartType: "bar",
      dimensionCode: dimension.code, measureCode: "observationCount",
      parameterCode: null,
      filters: toRequestFilters(effective),
      options: { maxRows: 500, rawRowLimit: 500, sortDirection: "asc", includeWarnings: false },
    }).then((result) => {
      if (ignore) return;
      const keyColumn = result.columns.find((c) => c.code === dimension.code)
        ?? result.columns.find((c) => c.code !== "value" && c.code !== "dimensionLabel");
      const labelColumn = result.columns.find((c) => c.code === "dimensionLabel");
      const byValue = new Map<string, string>();
      result.rows.forEach((row) => {
        const raw = keyColumn ? row[keyColumn.code] : row[dimension.code];
        const rawValue = String(raw ?? "");
        if (!rawValue) return;
        const label = labelColumn ? row[labelColumn.code] : raw;
        byValue.set(rawValue, String(label ?? rawValue));
      });
      setValues(Array.from(byValue.entries()).map(([itemValue, itemLabel]) => ({
        value: itemValue, label: itemLabel,
      })));
      setUnavailable(false);
    }).catch(() => {
      if (!ignore) { setValues([]); setUnavailable(true); }
    });

    return () => { ignore = true; };
  }, [dimension.code, dimension.isExecutable, structural, declared]);

  return (
    <FilterSelect
      label={dimension.label}
      icon={Filter}
      value={value}
      onChange={onChange}
      active={!!value}
    >
      <option value={""}>{"All " + dimension.label}</option>
      {unavailable && (
        <option value="__unavailable__" disabled>
          {"Not executable" + (dimension.refusalCode ? " (" + dimension.refusalCode + ")" : "")}
        </option>
      )}
      {!unavailable && values.map((item) => (
        <option key={item.value} value={item.value}>{item.label}</option>
      ))}
    </FilterSelect>
  );
}

// ── Main component ────────────────────────────────────────────
// T-094. THREE STATES, NOT TWO. A reference request that FAILED and a tenant
// that has simply published nothing are different sentences, and showing the
// same line for both tells the user their catalogue is broken when it is empty.
type ReferenceState = "loading" | "ready" | "unavailable";

export function DashboardFilterBar() {
  const {
    filters, setFilter, clearAllFilters, activeFilterCount,
    declaredFilters, setDeclaredFilter,
  } = useDashboardFilters();

  const [referenceData, setReferenceData] =
    useState<DashboardReferenceData | null>(null);
  const [referenceState, setReferenceState] = useState<ReferenceState>("loading");

  const declaredValueOf = (code: string) =>
    declaredFilters.find((filter) => filter.code === code)?.value ?? "";

  const materialSearchLabel = useMemo(() => {
    if (!filters.materialCode) return "Material";
    return `Material: ${filters.materialCode}`;
  }, [filters.materialCode]);

  useEffect(() => {
    let ignore = false;
    setReferenceState("loading");
    productApi
      .getDashboardReferenceData(filters)
      .then((data) => {
        if (ignore) return;
        setReferenceData(data);
        setReferenceState("ready");
      })
      .catch(() => {
        if (ignore) return;
        setReferenceData(null);
        setReferenceState("unavailable");
      });
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
        <StandardButton
          className={`piq-filter-clear ${hasActiveFilters ? "piq-filter-clear--visible" : ""}`}
          onClick={clearAllFilters}
          type="button"
          title="Clear all filters"
          isDisabled={!hasActiveFilters}
        >
          <RotateCcw size={12} />
          Clear all
        </StandardButton>
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
          value={filters.parameterCode ?? ""}
          onChange={(v) => setFilter("parameterCode", v || undefined)}
          active={!!filters.parameterCode}
        >
          <option value="">All parameters</option>
          {referenceData?.parameters.length ? (
            referenceData.parameters.map((item) => (
              <option key={item.id} value={item.code}>
                {item.code} — {item.name}
              </option>
            ))
          ) : (
            /* PPIQ-SCENE5678: no hardcoded, industry-specific fallback. Those
               three codes did not exist in the data, so choosing one emptied
               every widget on the page. An unselectable line says so. */
            <option value="__unavailable__" disabled>
              Parameter catalogue unavailable
            </option>
          )}
        </FilterSelect>

        {/* T-094. The three hardcoded selects that used to stand here named a
            plant vocabulary the product had compiled in. What a user may filter
            on is now whatever this tenant PUBLISHED, so the controls are a
            projection of the published declarations and adding a dimension is a
            declaration, not a frontend change. */}
        {referenceState === "ready" && referenceData?.declaredDimensions.map((dimension) => (
          <DeclaredDimensionSelect
            key={dimension.code}
            dimension={dimension}
            structural={filters as Record<string, unknown>}
            declared={declaredFilters}
            value={declaredValueOf(dimension.code)}
            onChange={(v) => setDeclaredFilter(dimension.code, v || undefined)}
          />
        ))}

        {referenceState === "ready" && referenceData?.declaredDimensions.length === 0 && (
          /* A successful answer that names nothing. This is NOT a failure and
             must not read as one: the tenant has published no dimensions yet. */
          <span className="piq-filter-group-label" data-testid="declared-dimensions-empty">
            No published dimensions
          </span>
        )}

        {referenceState === "unavailable" && (
          /* The request itself failed. Different sentence, different cause. */
          <span className="piq-filter-group-label" data-testid="declared-dimensions-unavailable">
            Reference catalogue unavailable
          </span>
        )}

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
