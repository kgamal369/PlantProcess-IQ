import { Filter } from "lucide-react";

import type { DashboardReferenceData, DashboardWidgetFilters } from "../../../../api/productApiClient";

import type { WidgetBuilderState, RelativeDateUnit } from "./WidgetBuilderWizardContent.types";
import { toInputDateTime, fromInputDateTime } from "./WidgetBuilderWizardContent.helpers";

export function WizardSection({
  icon,
  title,
  description,
  children,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <section className="wizard-section">
      <div className="wizard-section-header">
        <span>{icon}</span>
        <div>
          <h3>{title}</h3>
          <p>{description}</p>
        </div>
      </div>
      {children}
    </section>
  );
}

export function FilterStep({
  state,
  referenceData,
  onPatch,
  onPatchFilters,
}: {
  state: WidgetBuilderState;
  referenceData: DashboardReferenceData | null;
  onPatch: (patch: Partial<WidgetBuilderState>) => void;
  onPatchFilters: (patch: Partial<DashboardWidgetFilters>) => void;
}) {
  return (
    <WizardSection
      icon={<Filter size={18} />}
      title="4. Filters"
      description="Filters are stored inside the widget definition and applied every time the backend executes the widget query."
    >
      <div className="form-grid">
        <label>
          Site
          <select
            value={state.filters.siteId ?? ""}
            onChange={(event) =>
              onPatchFilters({ siteId: event.target.value || null })
            }
          >
            <option value="">All sites</option>
            {(referenceData?.sites ?? []).map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Area
          <select
            value={state.filters.areaId ?? ""}
            onChange={(event) =>
              onPatchFilters({ areaId: event.target.value || null })
            }
          >
            <option value="">All areas</option>
            {(referenceData?.areas ?? []).map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Equipment
          <select
            value={state.filters.equipmentId ?? ""}
            onChange={(event) =>
              onPatchFilters({ equipmentId: event.target.value || null })
            }
          >
            <option value="">All equipment</option>
            {(referenceData?.equipment ?? []).map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Material type
          <input
            value={state.filters.materialUnitType ?? ""}
            onChange={(event) =>
              onPatchFilters({ materialUnitType: event.target.value || null })
            }
            placeholder="Example: Coil, Batch, Slab, Roll"
          />
        </label>

        <label>
          Defect type
          <select
            value={state.filters.defectType ?? ""}
            onChange={(event) =>
              onPatchFilters({ defectType: event.target.value || null })
            }
          >
            <option value="">All defects</option>
            {(referenceData?.defects ?? []).map((item) => (
              <option key={item.code} value={item.code}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Risk class
          <select
            value={state.filters.riskClass ?? ""}
            onChange={(event) =>
              onPatchFilters({ riskClass: event.target.value || null })
            }
          >
            <option value="">All risk classes</option>
            {(referenceData?.riskClasses ?? []).map((item) => (
              <option key={item.code} value={item.code}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Source system
          <select
            value={state.filters.sourceSystem ?? ""}
            onChange={(event) =>
              onPatchFilters({ sourceSystem: event.target.value || null })
            }
          >
            <option value="">All source systems</option>
            {(referenceData?.sourceSystems ?? []).map((item) => (
              <option key={item.code} value={item.code}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Shift / crew
          <select
            value={state.filters.shiftCode ?? ""}
            onChange={(event) =>
              onPatchFilters({ shiftCode: event.target.value || null })
            }
          >
            <option value="">All shifts</option>
            {(referenceData?.shifts ?? []).map((item) => (
              <option key={item.code} value={item.code}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Date filter mode
          <select
            value={state.dateMode}
            onChange={(event) =>
              onPatch({
                dateMode: event.target.value as WidgetBuilderState["dateMode"],
              })
            }
          >
            <option value="none">No date filter</option>
            <option value="relative">Relative lookback</option>
            <option value="absolute">Absolute date range</option>
          </select>
        </label>

        {state.dateMode === "relative" ? (
          <>
            <label>
              Last
              <input
                type="number"
                min={1}
                value={state.relativeDateValue}
                onChange={(event) =>
                  onPatch({ relativeDateValue: Number(event.target.value) })
                }
              />
            </label>

            <label>
              Unit
              <select
                value={state.relativeDateUnit}
                onChange={(event) =>
                  onPatch({
                    relativeDateUnit: event.target.value as RelativeDateUnit,
                  })
                }
              >
                <option value="days">Days</option>
                <option value="weeks">Weeks</option>
                <option value="months">Months</option>
              </select>
            </label>
          </>
        ) : null}

        {state.dateMode === "absolute" ? (
          <>
            <label>
              From
              <input
                type="datetime-local"
                value={toInputDateTime(state.filters.fromUtc)}
                onChange={(event) =>
                  onPatchFilters({
                    fromUtc: fromInputDateTime(event.target.value),
                  })
                }
              />
            </label>

            <label>
              To
              <input
                type="datetime-local"
                value={toInputDateTime(state.filters.toUtc)}
                onChange={(event) =>
                  onPatchFilters({
                    toUtc: fromInputDateTime(event.target.value),
                  })
                }
              />
            </label>
          </>
        ) : null}
      </div>
    </WizardSection>
  );
}