// T-068 corrective. The stable analysis-options contract.
//
// The Toolbox used to read GET /ml/foundation/outcomes, which returns the rows
// of ml_outcome_definitions unmapped. That is compatibility-era transport: it
// exposes a physical table's column names to React, and it is not the surface
// the product intends to keep.
//
// GET /api/analysis-jobs/definition-options is the stable one, and it already
// carries everything this page needs under engineOutcomes. This module is the
// only place that knows either the route or the response's nesting; everything
// downstream sees AnalysisOutcomeOption and nothing else.

import { apiClient } from "./http";
import type { AnalysisJobDefinitionOptions } from "./product-core/shared-types";

/** What the Analysis Toolbox needs from an outcome, and nothing more. */
export interface AnalysisOutcomeOption {
  outcomeKey: string;
  displayName: string;
  outcomeType: string;
  /** Declared by the outcome's own definition. Empty when it declares none. */
  grain: string;
}

/**
 * Normalised at this boundary on purpose.
 *
 * The endpoint nests outcomes beside defect types, parameters, engine jobs and
 * a data window - none of which the Toolbox has any business knowing about.
 * Flattening here keeps that transport shape out of the page, so a later change
 * to the envelope is one edit in this file.
 *
 * Throws on transport failure; the caller renders an honest error state.
 */
export async function getAnalysisOutcomeOptions(): Promise<AnalysisOutcomeOption[]> {
  const options = await apiClient.get<AnalysisJobDefinitionOptions>(
    "/analysis-jobs/definition-options"
  );

  const rows = options?.engineOutcomes;
  if (!Array.isArray(rows)) return [];

  return rows.map((row) => ({
    outcomeKey: (row?.outcomeKey ?? "").trim(),
    displayName: (row?.displayName ?? "").trim(),
    outcomeType: (row?.outcomeType ?? "").trim(),
    // Absent stays absent. The page disables the run rather than guessing.
    grain: (row?.grain ?? "").trim(),
  }));
}
