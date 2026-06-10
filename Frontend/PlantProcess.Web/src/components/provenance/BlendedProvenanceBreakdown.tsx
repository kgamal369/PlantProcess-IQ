import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
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
    >
      <div>
        <strong>Material provenance</strong>
        <div>
          {evidence.materialId} · {evidence.isTransition ? "transition / blended" : "single-source"}
        </div>
      </div>

      <ul>
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