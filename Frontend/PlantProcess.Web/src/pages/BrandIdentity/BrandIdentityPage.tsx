import {
  BadgeCheck,
  Building2,
  FileText,
  Globe2,
  MonitorCheck,
  ShieldCheck,
} from "lucide-react";
import { plantProcessBrand } from "../../brand/plantProcessBrand";
import { BrandProofPanel } from "../../components/brand/BrandProofPanel";
import { DemoEnvironmentBanner } from "../../components/demo/DemoEnvironmentBanner";
import "./brand-identity.css";

export function BrandIdentityPage() {
  return (
    <main className="brand-identity-page">
      <DemoEnvironmentBanner />

      <section className="brand-hero">
        <div>
          <p className="brand-eyebrow">Dimension 7 — Brand Identity</p>
          <h1>{plantProcessBrand.productName}</h1>
          <p>{plantProcessBrand.tagline}</p>
          <p>{plantProcessBrand.longPositioning}</p>
        </div>

        <div className="brand-hero-card">
          <span>Market position</span>
          <strong>Industrial intelligence layer</strong>
          <small>{plantProcessBrand.founderLocation} · {plantProcessBrand.marketFocus}</small>
        </div>
      </section>

      <BrandProofPanel />

      <section className="brand-section">
        <div className="brand-section-heading">
          <p className="brand-eyebrow">Public launch readiness</p>
          <h2>Launch message checklist</h2>
        </div>

        <div className="brand-check-grid">
          <BrandCheck
            icon={<Globe2 size={22} />}
            title="Generic manufacturing"
            text="Positioned across industries, not only flat steel."
          />
          <BrandCheck
            icon={<ShieldCheck size={22} />}
            title="Read-only safety"
            text="No write-back, no production control, no replacement claims."
          />
          <BrandCheck
            icon={<MonitorCheck size={22} />}
            title="Evidence-first demo"
            text="Demo follows connect, stage, map, monitor, analyze, report."
          />
          <BrandCheck
            icon={<FileText size={22} />}
            title="Credibility assets"
            text="Engineer brief and architecture diagram are available."
          />
        </div>
      </section>

      <section className="brand-section">
        <div className="brand-section-heading">
          <p className="brand-eyebrow">Approved vocabulary</p>
          <h2>Use these terms in website, app, demos, and reports.</h2>
        </div>

        <div className="brand-chip-grid">
          {plantProcessBrand.approvedLanguage.map((item) => (
            <span className="brand-chip brand-chip--good" key={item}>
              {item}
            </span>
          ))}
        </div>
      </section>

      <section className="brand-section brand-section--warning">
        <div className="brand-section-heading">
          <p className="brand-eyebrow">Forbidden / risky language</p>
          <h2>Never use these claims before real implementation proof.</h2>
        </div>

        <div className="brand-chip-grid">
          {plantProcessBrand.forbiddenLanguage.map((item) => (
            <span className="brand-chip brand-chip--bad" key={item}>
              {item}
            </span>
          ))}
        </div>
      </section>

      <section className="brand-section">
        <div className="brand-section-heading">
          <p className="brand-eyebrow">Connector honesty</p>
          <h2>Connector labels must remain consistent in app, website, and demo.</h2>
        </div>

        <div className="brand-connector-grid">
          {plantProcessBrand.connectors.map((connector) => (
            <article key={connector.name}>
              <BadgeCheck size={20} />
              <h3>{connector.name}</h3>
              <strong>{connector.status}</strong>
              <p>{connector.message}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="brand-section brand-final-proof">
        <Building2 size={34} />
        <h2>Final brand statement</h2>
        <p>
          PlantProcess IQ is an industrial, evidence-first, read-only manufacturing
          intelligence layer for process-to-quality investigation and Data Diagnostic
          reporting. It is calm, technical, trustworthy, and honest about current
          capabilities.
        </p>
      </section>
    </main>
  );
}

function BrandCheck({
  icon,
  title,
  text,
}: {
  icon: React.ReactNode;
  title: string;
  text: string;
}) {
  return (
    <article className="brand-check-card">
      {icon}
      <h3>{title}</h3>
      <p>{text}</p>
    </article>
  );
}

export default BrandIdentityPage;