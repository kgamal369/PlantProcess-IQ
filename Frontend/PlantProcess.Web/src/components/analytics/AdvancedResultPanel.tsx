/* eslint-disable react-refresh/only-export-components */
import type { ReactNode } from "react";
import { StandardButton } from "@/components/standard";

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
  <div style={{ display: "flex", justifyContent: "space-between", gap: 12, padding: "3px 0", fontSize: 13 }}>
    <span style={{ color: "#6b7280" }}>{label}</span><span style={{ fontWeight: 600 }}>{value}</span>
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
      <section data-testid="advanced-result-blocked" style={{ border: "1px dashed #c0563b", borderRadius: 10, padding: 14, background: "#fdf3f0" }}>
        <strong style={{ color: "#c0563b" }}>{blocked ? "Result blocked" : "Result cannot be displayed"}</strong>
        <p style={{ margin: "6px 0 0", fontSize: 13, color: "#6b7280" }}>
          An advanced result missing a valid method, a positive sample size, the honesty caveat, or with a Blocked
          readiness state is withheld by design.
        </p>
        {blocked && result.blockedReasons && result.blockedReasons.length > 0 ? (
          <ul style={{ margin: "8px 0 0", paddingLeft: 18, fontSize: 12, color: "#c0563b" }}>
            {result.blockedReasons.map((reason, i) => <li key={i}>{reason}</li>)}
          </ul>
        ) : null}
      </section>
    );
  }

  return (
    <section data-testid="advanced-result" style={{ border: "1px solid #d8dee9", borderRadius: 10, padding: 14, background: "#fff" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
        <strong style={{ fontSize: 14 }}>{result.findingId ?? "Finding"}</strong>
        <span style={{ fontSize: 12, color: "#3b6fb5", fontWeight: 700 }}>{result.method}</span>
      </div>
      <div style={{ marginTop: 8 }}>
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
        <ul style={{ margin: "8px 0 0", paddingLeft: 18, fontSize: 12, color: "#b9770e" }}>
          {result.dataQualityWarnings.map((w, i) => <li key={i}>{w}</li>)}
        </ul>
      ) : null}
      {result.evidenceHandle ? (
        <StandardButton
          type="button"
          data-testid="advanced-result-evidence"
          onClick={() => onOpenEvidence?.(result.evidenceHandle as ProvenanceHandleRef)}
          style={{ marginTop: 10, fontSize: 12, color: "#3b6fb5", background: "none", border: "none", padding: 0, cursor: "pointer", textDecoration: "underline" }}
        >
          View evidence ({result.evidenceHandle.kind})
        </StandardButton>
      ) : null}
      <p style={{ margin: "10px 0 0", fontSize: 12, fontStyle: "italic", color: "#6b7280" }}>{caveat}</p>
    </section>
  );
}