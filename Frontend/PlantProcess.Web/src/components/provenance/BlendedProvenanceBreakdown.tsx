export type BlendedProvenanceContributor = {
  parentMaterialId: string;
  weight: number;
  contributionBasis: string;
  provenanceConfidence: string;
};

export type BlendedProvenanceEvidence = {
  materialId: string;
  isTransition: boolean;
  contributors: BlendedProvenanceContributor[];
  honestyCaveat: string;
};

export function BlendedProvenanceBreakdown({ evidence }: { evidence: BlendedProvenanceEvidence }) {
  const total = evidence.contributors.reduce((sum, item) => sum + item.weight, 0);

  return (
    <section
      aria-label="Blended provenance breakdown"
      style={{
        border: "1px solid rgba(0,212,255,0.22)",
        borderRadius: 18,
        padding: 16,
        background: "rgba(7,20,38,0.78)",
        display: "grid",
        gap: 12,
      }}
    >
      <div>
        <strong>Material provenance</strong>
        <div>
          {evidence.materialId} · {evidence.isTransition ? "transition / blended" : "single-source"}
        </div>
      </div>

      <ul style={{ display: "grid", gap: 8, paddingInlineStart: 20 }}>
        {evidence.contributors.map((item) => (
          <li key={item.parentMaterialId}>
            {item.parentMaterialId}: {(item.weight * 100).toFixed(1)}% · {item.contributionBasis} ·{" "}
            {item.provenanceConfidence}
          </li>
        ))}
      </ul>

      <div>Total weight: {(total * 100).toFixed(1)}%</div>
      <em>{evidence.honestyCaveat}</em>
    </section>
  );
}

export default BlendedProvenanceBreakdown;