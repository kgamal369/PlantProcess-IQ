import { useMemo } from "react";
import { StandardTable } from "../standard/StandardTable";

import "./specificationTable.css";

// T-047. A SPECIFICATION ROW STATES ITS OWN VERDICT.
//
// Every bound comes from the row, which came from ProductSpecification. There
// is no threshold in this file and there must never be one: a limit typed into
// a frontend is a limit no plant ever agreed to.
//
// FOUR STATES, NOT THREE. Below, within and above are comparisons; unobserved
// is the absence of one. A parameter nobody measured is not conforming, and
// colouring it green would be the most damaging thing this table could do.

export const SPECIFICATION_ROLES = [
  "gradeOrRecipe", "parameterCode", "minValue", "targetValue", "maxValue",
  "unitOfMeasure", "actualValue", "observationCount", "provenance",
] as const;

export type ConformanceState = "within" | "below" | "above" | "unobserved";

/**
 * A one-sided specification is complete and common: a maximum with no floor
 * cannot be breached from below, so a null bound is simply not tested.
 */
export function conformanceOf(
  actual: number | null | undefined,
  minimum: number | null | undefined,
  maximum: number | null | undefined
): ConformanceState {
  if (actual === null || actual === undefined || Number.isNaN(actual)) {
    return "unobserved";
  }

  if (minimum !== null && minimum !== undefined && actual < minimum) {
    return "below";
  }

  if (maximum !== null && maximum !== undefined && actual > maximum) {
    return "above";
  }

  return "within";
}

function numberOrNull(value: unknown): number | null {
  if (value === null || value === undefined || value === "") { return null; }
  const parsed = Number(value);
  return Number.isNaN(parsed) ? null : parsed;
}

export function SpecificationTable({ rows }: { rows: Record<string, unknown>[] }) {
  const headline = rows.length ? String(rows[0].state ?? "") : "NO_SPECIFICATIONS_RECORDED";

  const entries = useMemo(
    () =>
      rows
        .filter((row) => row.gradeOrRecipe !== null && row.gradeOrRecipe !== undefined)
        .map((row) => {
          const actual = numberOrNull(row.actualValue);
          const minimum = numberOrNull(row.minValue);
          const maximum = numberOrNull(row.maxValue);

          return {
            scope: String(row.gradeOrRecipe),
            parameter: String(row.parameterCode ?? ""),
            unit: row.unitOfMeasure === null || row.unitOfMeasure === undefined
              ? ""
              : String(row.unitOfMeasure),
            minimum,
            target: numberOrNull(row.targetValue),
            maximum,
            actual,
            observations: Number(row.observationCount ?? 0),
            state: conformanceOf(actual, minimum, maximum),
          };
        }),
    [rows]
  );

  if (headline === "NO_SPECIFICATIONS_RECORDED" || entries.length === 0) {
    return (
      <div className="empty-insight" role="status" data-testid="specification-state">
        <strong>No specifications recorded</strong>
        <p>No product specification has been declared for this installation yet.</p>
      </div>
    );
  }

  return (
    <div className="chart-shell" data-testid="specification-table">
      <StandardTable
          columns={[
            { key: "scope", header: "Scope", accessor: "scope" },
            { key: "parameter", header: "Parameter", accessor: "parameter" },
            { key: "minimum", header: "Minimum", cell: (row) => row.minimum ?? "-" },
            { key: "target", header: "Target", cell: (row) => row.target ?? "-" },
            { key: "maximum", header: "Maximum", cell: (row) => row.maximum ?? "-" },
            {
              key: "actual",
              header: "Observed",
              // T-250/F6: StandardTable owns the cell shell; this renderer owns the
              // observed-state semantics the existing tests assert on. Nothing in
              // the StandardTable public contract changed to make this work.
              cell: (row) => (
                <span
                  className={"specification-value specification-state--" + row.state}
                  data-testid="specification-observed"
                  data-state={row.state}
                  title={
                    row.state === "unobserved"
                      ? "No observation recorded for this scope and parameter"
                      : row.observations + " observation(s)"
                  }
                >
                  {row.actual ?? "not observed"}
                </span>
              ),
            },
            { key: "unit", header: "Unit", accessor: "unit" },
          ]}
          data={entries}
          getRowKey={(row) => row.scope + "/" + row.parameter}
        />
    </div>
  );
}