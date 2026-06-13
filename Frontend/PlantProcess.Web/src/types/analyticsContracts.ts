/* PPIQ-PHASE5 analytics contracts.
 * These are the COMPONENT prop shapes. Map your productCoreApiClient responses
 * (CorrelationDtos / CorrelationComputeDtos / GenealogyEdge) into these in your
 * page/container, not the other way around. If a field name differs in your DTO,
 * change the mapper - keep the component contract stable. */

export type CorrelationMethod = "Spearman" | "MutualInformation" | "Lasso" | string;

export interface CorrelationProvenance {
  handleId: string;            // opaque id your drill endpoint resolves to method inputs
  inputsUrl?: string;          // optional direct URL to the method-inputs JSON
}

export interface CorrelationEvidence {
  method: CorrelationMethod;
  qValue: number;              // Benjamini-Hochberg FDR q
  populationN: number;         // N the result was computed over
  stratification?: string;     // e.g. "by line, by grade"
  vif?: number | null;         // collinearity note where relevant
  provenance: CorrelationProvenance;
}

export interface CorrelationResult {
  id: string;
  driver: string;              // the suspected contributing parameter
  outcome: string;             // the quality/defect outcome
  coefficient: number;         // strength (e.g. Spearman rho)
  evidence: CorrelationEvidence;
}

/** Abstain envelope used everywhere the engine can decline to answer. */
export interface AbstainState {
  abstained: boolean;
  reason?: string;
  missingInputs?: string[];
}

/** Genealogy node/edge for the bidirectional thread. */
export interface ThreadNode {
  id: string;
  kind: "coil" | "heat" | "cast" | "slab" | "process" | string;
  label: string;
  detailUrl?: string;          // optional drill to entity detail
  meta?: Record<string, string | number>;
}
export interface ThreadEdge {
  from: string;                // node id
  to: string;                  // node id
  direction: "up" | "down";    // up = toward source, down = toward affected
  parameter?: string;          // process parameter carried along the edge
}