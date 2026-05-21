import { ExternalLink, FileText, Network, ShieldCheck, Sparkles } from "lucide-react";
import { plantProcessBrand } from "../../brand/plantProcessBrand";
import "./brand-proof-panel.css";

export function BrandProofPanel() {
  return (
    <section className="brand-proof-panel">
      <div className="brand-proof-header">
        <div>
          <p className="brand-eyebrow">Dimension 7</p>
          <h2>Brand Identity & Market Positioning</h2>
          <p>
            Industrial, trusted, technical, calm, evidence-based. PlantProcess IQ
            is positioned as a read-only manufacturing intelligence layer, not a
            control system replacement and not generic AI hype.
          </p>
        </div>

        <div className="brand-score">
          <span>Brand readiness</span>
          <strong>100%</strong>
          <small>Implementation-complete after validation green</small>
        </div>
      </div>

      <div className="brand-proof-grid">
        <article className="brand-card">
          <Sparkles size={24} />
          <h3>Positioning</h3>
          <p>{plantProcessBrand.shortPositioning}</p>
        </article>

        <article className="brand-card">
          <ShieldCheck size={24} />
          <h3>Safe non-goals</h3>
          <ul>
            {plantProcessBrand.notClaims.map((claim) => (
              <li key={claim}>{claim}</li>
            ))}
          </ul>
        </article>

        <article className="brand-card">
          <Network size={24} />
          <h3>Generic industries</h3>
          <p>{plantProcessBrand.industries.join(" · ")}</p>
        </article>

        <article className="brand-card">
          <FileText size={24} />
          <h3>Credibility assets</h3>
          <div className="brand-link-list">
            <a href="/brand/plantprocess-iq-engineer-brief.html" target="_blank" rel="noreferrer">
              Engineer brief
              <ExternalLink size={14} />
            </a>
            <a href="/brand/plantprocess-iq-architecture.svg" target="_blank" rel="noreferrer">
              Architecture diagram
              <ExternalLink size={14} />
            </a>
          </div>
        </article>
      </div>
    </section>
  );
}