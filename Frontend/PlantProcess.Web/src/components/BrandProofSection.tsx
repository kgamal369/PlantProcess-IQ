import {
  ExternalLink,
  FileText,
  Network,
  ShieldCheck,
  Sparkles,
} from "lucide-react";
import { plantProcessBrand } from "../brand/plantProcessBrand";

export function BrandProofSection() {
  return (
    <section className="website-section brand-proof-website" id="brand-proof">
      <div className="section-heading">
        <div>
          <div className="eyebrow">
            <Sparkles size={15} />
            Brand proof
          </div>
          <h2>Industrial, technical, calm, and evidence-first.</h2>
        </div>
      </div>

      <div className="brand-proof-website-grid">
        <article>
          <Sparkles size={24} />
          <h3>Positioning</h3>
          <p>{plantProcessBrand.shortPositioning}</p>
        </article>

        <article>
          <ShieldCheck size={24} />
          <h3>Non-goals</h3>
          <ul>
            {plantProcessBrand.notClaims.map((claim) => (
              <li key={claim}>{claim}</li>
            ))}
          </ul>
        </article>

        <article>
          <Network size={24} />
          <h3>Architecture diagram</h3>
          <p>Read-only source → staging → canonical model → analytics → report.</p>
          <a href="/brand/plantprocess-iq-architecture.svg" target="_blank" rel="noreferrer">
            Open diagram <ExternalLink size={14} />
          </a>
        </article>

        <article>
          <FileText size={24} />
          <h3>Engineer brief</h3>
          <p>A concise technical brief for engineers before a discovery call.</p>
          <a href="/brand/plantprocess-iq-engineer-brief.html" target="_blank" rel="noreferrer">
            Open brief <ExternalLink size={14} />
          </a>
        </article>
      </div>
    </section>
  );
}