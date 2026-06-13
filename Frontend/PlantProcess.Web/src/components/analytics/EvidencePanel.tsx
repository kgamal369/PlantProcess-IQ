/* PPIQ-PHASE5 EvidencePanel - makes the L4 honesty posture visible on every
 * correlation result. Uses the design-system StandardButton and the canonical
 * PopulationBadge. data-testid hooks let the e2e assert on it. */
import type { ReactNode } from "react";
import { StandardButton } from "../standard";
import { PopulationBadge } from "../standard/PopulationBadge";
import type { CorrelationEvidence } from "../../types/analyticsContracts";

const T = { panel: "#0B1730", line: "#16243D", cyan: "#00D4FF", warn: "#FFB020", ok: "#2CE6A2", white: "#EAF6FF", steel: "#8EA7C1", navy: "#050B18" };

/** Dev-time grounding guard: throws if a number renders without provenance. */
export function assertProvenance(evidence: CorrelationEvidence): void {
  if (!evidence || !evidence.provenance || !evidence.provenance.handleId) {
    throw new Error("Grounding violation: correlation number rendered without a provenance handle.");
  }
}

function Chip({ label, value, color }: { label: string; value: ReactNode; color: string }) {
  return (
    <span style={{ display: "inline-flex", gap: 6, alignItems: "baseline", background: T.navy, border: `1px solid ${T.line}`, borderRadius: 6, padding: "3px 8px", fontSize: 12, fontFamily: "'JetBrains Mono', monospace" }}>
      <span style={{ color: T.steel }}>{label}</span>
      <span style={{ color, fontWeight: 700 }}>{value}</span>
    </span>
  );
}

export function EvidencePanel({
  evidence,
  onOpenProvenance,
}: {
  evidence: CorrelationEvidence;
  onOpenProvenance?: (handleId: string) => void;
}) {
  if (import.meta.env?.DEV) assertProvenance(evidence);
  const q = evidence.qValue;
  const qColor = q <= 0.05 ? T.ok : q <= 0.1 ? T.warn : T.steel;

  return (
    <div data-testid="evidence-panel" style={{ background: T.panel, border: `1px solid ${T.line}`, borderRadius: 10, padding: 10, marginTop: 8 }}>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 6, alignItems: "center" }}>
        <Chip label="method" value={<span data-testid="evidence-method">{evidence.method}</span>} color={T.cyan} />
        <Chip label="BH-FDR q" value={<span data-testid="evidence-q">{q.toFixed(4)}</span>} color={qColor} />
        <PopulationBadge n={evidence.populationN} />
        {evidence.stratification ? <Chip label="strata" value={evidence.stratification} color={T.steel} /> : null}
        {typeof evidence.vif === "number" ? <Chip label="VIF" value={evidence.vif.toFixed(2)} color={evidence.vif >= 5 ? T.warn : T.steel} /> : null}
      </div>

      <div data-testid="evidence-suspected" style={{ marginTop: 8, color: T.warn, fontWeight: 700, fontSize: 12, fontFamily: "'JetBrains Mono', monospace" }}>
        SUSPECTED CONTRIBUTOR - NOT A PROVEN CAUSE
      </div>
      <div style={{ color: T.steel, fontSize: 12, marginTop: 2 }}>
        A correlation shows two things move together; it does not establish that one causes the other.
        Confounders, sampling and timing can all produce it.
      </div>

      <div style={{ marginTop: 8 }}>
        <StandardButton
          variant="ghost"
          size="sm"
          data-testid="provenance-handle"
          data-handle={evidence.provenance.handleId}
          onClick={() => onOpenProvenance?.(evidence.provenance.handleId)}
        >
          View method inputs
        </StandardButton>
      </div>
    </div>
  );
}

export default EvidencePanel;