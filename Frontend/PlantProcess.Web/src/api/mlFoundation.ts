// T-068. The outcome registry client.
//
// GET /ml/foundation/outcomes returns the rows of ml_outcome_definitions
// unmapped, so this contract is snake_case to match what the server actually
// sends. Renaming them here would invent a shape the API does not have.

import { apiClient } from "./http";

export interface MlOutcomeDefinitionDto {
  outcome_key: string;
  display_name: string | null;
  outcome_group: string | null;
  grain: string | null;
  outcome_type: string | null;
  unit: string | null;
  normalization: string | null;
  taxonomy_json: string | null;
  version: number | null;
  status: string | null;
}

/** Throws on transport failure; the caller renders an honest error state. */
export async function getOutcomeDefinitions(): Promise<MlOutcomeDefinitionDto[]> {
  const rows = await apiClient.get<MlOutcomeDefinitionDto[]>("/ml/foundation/outcomes");
  return Array.isArray(rows) ? rows : [];
}
