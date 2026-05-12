import { useEffect, useMemo, useState } from "react";
import {
  CalendarDays,
  Database,
  Filter,
  RotateCcw,
  Search,
  SlidersHorizontal,
} from "lucide-react";
import { plantProcessApi, type DashboardReferenceData } from "../api/plantProcessApi";
import { useDashboardFilters } from "../state/DashboardFilterContext";

export function DashboardFilterBar() {
  const { filters, setFilter, clearAllFilters, activeFilterCount } =
    useDashboardFilters();

  const [referenceData, setReferenceData] =
    useState<DashboardReferenceData | null>(null);

  const materialSearchLabel = useMemo(() => {
    if (!filters.materialCode) return "Material code";
    return `Material: ${filters.materialCode}`;
  }, [filters.materialCode]);

  useEffect(() => {
    let ignore = false;

    plantProcessApi
      .getDashboardReferenceData(filters)
      .then((data) => {
        if (!ignore) setReferenceData(data);
      })
      .catch(() => {
        if (!ignore) setReferenceData(null);
      });

    return () => {
      ignore = true;
    };
  }, [filters.siteId]);

  return (
    <section className="filter-panel">
      <div className="filter-panel__header">
        <div>
          <div className="eyebrow">
            <SlidersHorizontal size={14} />
            QlikSense-style global filters
          </div>
          <h2>Interactive Manufacturing Intelligence Workspace</h2>
          <p>
            Filters are sent to the backend. The dashboard receives aggregated
            read-model data only.
          </p>
        </div>

        <button className="secondary-button" onClick={clearAllFilters}>
          <RotateCcw size={16} />
          Clear all ({activeFilterCount})
        </button>
      </div>

      <div className="filter-grid">
        <label className="field">
          <span>
            <Filter size={14} />
            Site
          </span>
          <select
            value={filters.siteId ?? ""}
            onChange={(event) => setFilter("siteId", event.target.value || undefined)}
          >
            <option value="">All sites</option>
            {referenceData?.sites.map((item) => (
              <option key={item.id} value={item.id}>
                {item.code} — {item.name}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>
            <Filter size={14} />
            Area
          </span>
          <select
            value={filters.areaId ?? ""}
            onChange={(event) => setFilter("areaId", event.target.value || undefined)}
          >
            <option value="">All areas</option>
            {referenceData?.areas.map((item) => (
              <option key={item.id} value={item.id}>
                {item.code} — {item.name}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>
            <Filter size={14} />
            Equipment
          </span>
          <select
            value={filters.equipmentId ?? ""}
            onChange={(event) => setFilter("equipmentId", event.target.value || undefined)}
          >
            <option value="">All equipment</option>
            {referenceData?.equipment.map((item) => (
              <option key={item.id} value={item.id}>
                {item.code} — {item.name}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>
            <Database size={14} />
            Source system
          </span>
          <select
            value={filters.sourceSystem ?? ""}
            onChange={(event) =>
              setFilter("sourceSystem", event.target.value || undefined)
            }
          >
            <option value="">All source systems</option>
            {referenceData?.sourceSystems.map((item) => (
              <option key={item.id} value={item.code}>
                {item.code} — {item.name}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>
            <Search size={14} />
            {materialSearchLabel}
          </span>
          <input
            value={filters.materialCode ?? ""}
            onChange={(event) =>
              setFilter("materialCode", event.target.value || undefined)
            }
            placeholder="Search material / batch / coil / lot"
          />
        </label>

        <label className="field">
          <span>
            <Filter size={14} />
            Parameter
          </span>
          <select
            value={filters.parameterCode ?? "CastingSpeed"}
            onChange={(event) =>
              setFilter("parameterCode", event.target.value || undefined)
            }
          >
            {referenceData?.parameters.length ? (
              referenceData.parameters.map((item) => (
                <option key={item.id} value={item.code}>
                  {item.code} — {item.name}
                </option>
              ))
            ) : (
              <>
                <option>CastingSpeed</option>
                <option>Superheat</option>
                <option>RollingForce</option>
              </>
            )}
          </select>
        </label>

        <label className="field">
          <span>
            <Filter size={14} />
            Defect / outcome
          </span>
          <select
            value={filters.defectType ?? ""}
            onChange={(event) =>
              setFilter("defectType", event.target.value || undefined)
            }
          >
            <option value="">All defects</option>
            {referenceData?.defects.map((item) => (
              <option key={item.id} value={item.code}>
                {item.code} — {item.name}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>
            <Filter size={14} />
            Risk class
          </span>
          <select
            value={filters.riskClass ?? ""}
            onChange={(event) =>
              setFilter("riskClass", event.target.value || undefined)
            }
          >
            <option value="">All risk classes</option>
            {referenceData?.riskClasses.map((item) => (
              <option key={item.id} value={item.code}>
                {item.name} ({item.count})
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>
            <Filter size={14} />
            Shift / Crew
          </span>
          <select
            value={filters.shiftCode ?? ""}
            onChange={(event) =>
              setFilter("shiftCode", event.target.value || undefined)
            }
          >
            <option value="">All shifts/crews</option>
            {referenceData?.shifts.map((item) => (
              <option key={item.id} value={item.code}>
                {item.name} ({item.count})
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>
            <CalendarDays size={14} />
            From UTC
          </span>
          <input
            type="datetime-local"
            value={toLocalInput(filters.fromUtc)}
            onChange={(event) =>
              setFilter("fromUtc", toUtcValue(event.target.value))
            }
          />
        </label>

        <label className="field">
          <span>
            <CalendarDays size={14} />
            To UTC
          </span>
          <input
            type="datetime-local"
            value={toLocalInput(filters.toUtc)}
            onChange={(event) =>
              setFilter("toUtc", toUtcValue(event.target.value))
            }
          />
        </label>

        <label className="field">
          <span>
            <Filter size={14} />
            Genealogy mode
          </span>
          <select
            value={filters.linkMode ?? "DownstreamChildren"}
            onChange={(event) => setFilter("linkMode", event.target.value as any)}
          >
            <option value="SameMaterial">Same material only</option>
            <option value="DownstreamChildren">Downstream children</option>
            <option value="UpstreamParents">Upstream parents</option>
            <option value="FullGenealogy">Full genealogy</option>
          </select>
        </label>
      </div>
    </section>
  );
}

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