export type FindingCoverageEvidence = {
  population: number;
  included: number;
  excluded: number;
  completeness: number;
  excludedReasons: Record<string, number>;
};

export type FindingValueEvidence = {
  currency: string;
  low: number;
  mid: number;
  high: number;
  isAbstained: boolean;
  abstainReason?: string | null;
  provenanceHandle: string;
  honestyCaveat: string;
};

export function FindingEvidencePanel({
  coverage,
  value,
}: {
  coverage: FindingCoverageEvidence;
  value: FindingValueEvidence;
}) {
  const excludedReasonText = Object.entries(coverage.excludedReasons)
    .map(([reason, count]) => `${reason}: ${count}`)
    .join(" · ");

  return (
    <aside
      aria-label="Finding evidence"
      style={{
        border: "1px solid rgba(0,212,255,0.24)",
        borderRadius: 16,
        padding: 14,
        display: "grid",
        gap: 10,
        background: "rgba(7,20,38,0.72)",
      }}
    >
      <strong>Evidence transparency</strong>
      <div>
        Coverage: based on {coverage.included.toLocaleString()} of {coverage.population.toLocaleString()} records (
        {(coverage.completeness * 100).toFixed(1)}%); {coverage.excluded.toLocaleString()} excluded.
      </div>
      {excludedReasonText && <div>Excluded reasons: {excludedReasonText}</div>}
      {value.isAbstained ? (
        <div>No € basis — abstained: {value.abstainReason}</div>
      ) : (
        <div>
          Value range: {value.currency} {value.low.toLocaleString()} – {value.high.toLocaleString()} (mid{" "}
          {value.mid.toLocaleString()})
        </div>
      )}
      <div>Provenance: {value.provenanceHandle}</div>
      <em>{value.honestyCaveat}</em>
    </aside>
  );
}

export default FindingEvidencePanel;