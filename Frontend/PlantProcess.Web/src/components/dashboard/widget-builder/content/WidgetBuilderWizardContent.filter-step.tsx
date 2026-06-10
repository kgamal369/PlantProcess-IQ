import { Filter } from "lucide-react";

import type { DashboardReferenceData, DashboardWidgetFilters } from "../../../../api/productApiClient";

import type { WidgetBuilderState, RelativeDateUnit } from "./WidgetBuilderWizardContent.types";
import { toInputDateTime, fromInputDateTime } from "./WidgetBuilderWizardContent.helpers";

import { StandardP2Input, StandardP2Select } from "@/components/standard/StandardP2Controls";
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
          <StandardP2Select
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
          </StandardP2Select>
        </label>

        <label>
          Area
          <StandardP2Select
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
          </StandardP2Select>
        </label>

        <label>
          Equipment
          <StandardP2Select
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
          </StandardP2Select>
        </label>

        <label>
          Material type
          <StandardP2Input
            value={state.filters.materialUnitType ?? ""}
            onChange={(event) =>
              onPatchFilters({ materialUnitType: event.target.value || null })
            }
            placeholder="Example: Coil, Batch, Slab, Roll"
          />
        </label>

        <label>
          Defect type
          <StandardP2Select
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
          </StandardP2Select>
        </label>

        <label>
          Risk class
          <StandardP2Select
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
          </StandardP2Select>
        </label>

        <label>
          Source system
          <StandardP2Select
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
          </StandardP2Select>
        </label>

        <label>
          Shift / crew
          <StandardP2Select
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
          </StandardP2Select>
        </label>

        <label>
          Date filter mode
          <StandardP2Select
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
          </StandardP2Select>
        </label>

        {state.dateMode === "relative" ? (
          <>
            <label>
              Last
              <StandardP2Input
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
              <StandardP2Select
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
              </StandardP2Select>
            </label>
          </>
        ) : null}

        {state.dateMode === "absolute" ? (
          <>
            <label>
              From
              <StandardP2Input
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
              <StandardP2Input
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