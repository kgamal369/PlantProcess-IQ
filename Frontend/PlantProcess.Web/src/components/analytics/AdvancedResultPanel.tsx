/* eslint-disable react-refresh/only-export-components */
import type { ReactNode } from "react";
import { StandardButton } from "@/components/standard";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
export type ProvenanceHandleRef = { kind: string; id: string };

export type AdvancedResult = {
  findingId?: string;
  method: string;
  effectSize?: number | null;
  qValue?: number | null;
  sampleSize: number;
  outcomeKey?: string | null;
  grain?: string | null;
  stabilityLower?: number | null;
  stabilityUpper?: number | null;
  stabilityConsistency?: number | null;
  isStable?: boolean | null;
  stratum?: string | null;
  survivesStratification?: boolean | null;
  excludedRecords?: number | null;
  dataQualityWarnings?: string[] | null;
  readiness?: string | null;
  blockedReasons?: string[] | null;
  honestyCaveat?: string | null;
  // T-101: every figure pulls a provenance handle; if absent the result is not a live finding.
  evidenceHandle?: ProvenanceHandleRef | null;
};

const DEFAULT_CAVEAT = "This is a diagnostic association, not a evidence-backed root-cause investigation.";

/** Mirrors AdvancedAnalysisResult.IsRenderable: missing method, sample size, readiness or caveat must not render. */
export function isRenderable(r: AdvancedResult): boolean {
  const caveat = r.honestyCaveat ?? DEFAULT_CAVEAT;
  const blocked = (r.readiness ?? "").toLowerCase() === "blocked";
  return r.method !== "NotApplicable" && r.sampleSize > 0 && caveat.trim().length > 0 && !blocked;
}

const row = (label: string, value: ReactNode) => (
  <div>
    <span>{label}</span><span>{value}</span>
  </div>
);
const fmt = (v: number | null | undefined, d = 3) => (typeof v === "number" && Number.isFinite(v) ? v.toFixed(d) : "-");

export function AdvancedResultPanel({
  result,
  onOpenEvidence,
}: {
  result: AdvancedResult;
  onOpenEvidence?: (handle: ProvenanceHandleRef) => void;
}) {
  const caveat = result.honestyCaveat ?? DEFAULT_CAVEAT;
  const blocked = (result.readiness ?? "").toLowerCase() === "blocked";

  if (!isRenderable(result)) {
    return (
      <section data-testid="advanced-result-blocked">
        <strong>{blocked ? "Result blocked" : "Result cannot be displayed"}</strong>
        <p>
          An advanced result missing a valid method, a positive sample size, the honesty caveat, or with a Blocked
          readiness state is withheld by design.
        </p>
        {blocked && result.blockedReasons && result.blockedReasons.length > 0 ? (
          <ul>
            {result.blockedReasons.map((reason, i) => <li key={i}>{reason}</li>)}
          </ul>
        ) : null}
      </section>
    );
  }

  return (
    <section data-testid="advanced-result">
      <div>
        <strong>{result.findingId ?? "Finding"}</strong>
        <span>{result.method}</span>
      </div>
      <div>
        {row("Effect size", fmt(result.effectSize))}
        {row("q-value (FDR)", fmt(result.qValue))}
        {row("Sample size", String(result.sampleSize))}
        {row("Stability", `${fmt(result.stabilityLower)} ... ${fmt(result.stabilityUpper)} (consistency ${fmt(result.stabilityConsistency, 2)})`)}
        {result.isStable != null ? row("Stable", result.isStable ? "yes" : "no") : null}
        {result.survivesStratification != null ? row("Survives stratification", result.survivesStratification ? "yes" : "no") : null}
        {result.excludedRecords != null ? row("Excluded records", String(result.excludedRecords)) : null}
        {result.readiness ? row("Readiness", result.readiness) : null}
      </div>
      {result.dataQualityWarnings && result.dataQualityWarnings.length > 0 ? (
        <ul>
          {result.dataQualityWarnings.map((w, i) => <li key={i}>{w}</li>)}
        </ul>
      ) : null}
      {result.evidenceHandle ? (
        <StandardButton
          type="button"
          data-testid="advanced-result-evidence"
          onClick={() => onOpenEvidence?.(result.evidenceHandle as ProvenanceHandleRef)}
        >
          View evidence ({result.evidenceHandle.kind})
        </StandardButton>
      ) : null}
      <p>{caveat}</p>
    </section>
  );
}