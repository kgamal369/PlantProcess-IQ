/* PPIQ-PHASE6 exec/ops contracts. Component prop shapes; map your API into these. */

export interface AbstainState {
  abstained: boolean;
  reason?: string;
  missingInputs?: string[];
}

/** One drill-through term behind a value figure. */
export interface ValueTerm {
  key: string;
  label: string;
  sourceValue: number | string;
  unit?: string;
  inputJson?: unknown;          // the raw input the engine used (for the drill)
}

/** Mirrors ValueImpactResult (Low/Expected/High + provenance + abstain). */
export interface ValueImpactResult {
  currency: string;             // e.g. "EUR"
  low: number;
  expected: number;
  high: number;
  populationN?: number;
  terms: ValueTerm[];           // attributable production-stop minutes, etc.
  abstain: AbstainState;
}

/** Entitlements derived from a VERIFIED signed license (entitlementSource). */
export interface Entitlements {
  verified: boolean;
  tier: string;                 // e.g. "Essentials" | "Professional" | "Enterprise"
  features: string[];           // entitlement keys the license grants
  seats?: number;
  sources?: number;             // source-count cap
}

/** A monitored import/analytics job and its latest run. */
export interface JobRow {
  id: string;
  name: string;
  lastRunAt?: string;           // ISO
  outcome?: "success" | "failed" | "running" | "never" | string;
  durationMs?: number;
  rowsAffected?: number;
  nextRunAt?: string;           // ISO
  error?: string;               // surfaced when outcome === "failed"
}