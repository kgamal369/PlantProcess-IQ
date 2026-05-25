import { positioningTruths } from "../../content/phase1WebsiteProof";

export function PositioningTruthBlock() {
  return (
    <section className="website-section positioning-truth-section" id="positioning">
      <div className="section-kicker">Positioning truth</div>

      <div className="truth-hero">
        <h2>Not MES. Not SCADA. Not Level 2. Not BI-only.</h2>
        <p>
          PlantProcess IQ is a manufacturing intelligence layer above existing
          plant systems. It helps teams connect source-shaped data, stage it,
          map it into a generic model, investigate quality signals, and produce
          explainable reporting.
        </p>
      </div>

      <div className="truth-grid">
        {positioningTruths.map((item) => (
          <article key={item.title}>
            <strong>{item.title}</strong>
            <p>{item.text}</p>
          </article>
        ))}
      </div>
    </section>
  );
}

export default PositioningTruthBlock;