import { NavLink, Route, Routes } from "react-router-dom";
import {
  BadgeEuro,
  BarChart3,
  BrainCircuit,
  CalendarCheck,
  CheckCircle2,
  DatabaseZap,
  Factory,
  FileText,
  GitBranch,
  Mail,
  MapPin,
  MonitorCheck,
  Network,
  ShieldCheck,
  Workflow,
} from "lucide-react";

import { BrandProofSection } from "./components/BrandProofSection";
import ProductScreenshotShowcase from "./components/proof/ProductScreenshotShowcase";
import PricingLicenseMatrix from "./components/proof/PricingLicenseMatrix";
import PositioningTruthBlock from "./components/proof/PositioningTruthBlock";
import ConnectorHonestyBlock from "./components/proof/ConnectorHonestyBlock";
import RequestDemoForm from "./components/proof/RequestDemoForm";

const demoMailto =
  "mailto:info@plantprocessiq.com?subject=PlantProcess%20IQ%20Demo%20Request&body=Hello%20Karim%2C%0A%0AI%20would%20like%20to%20book%20a%2020-minute%20PlantProcess%20IQ%20demo.%0A%0ACompany%3A%0APlant%20type%3A%0AData%20sources%3A%0AMain%20quality%20problem%3A%0APreferred%20time%3A%0A";

const screenshotCards = [
  {
    title: "Command dashboard",
    image: "/screenshots/plantprocess-dashboard.png",
    description: "Cross-filtered quality, risk, and process intelligence.",
  },
  {
    title: "Admin DB configuration",
    image: "/screenshots/admin-db-configuration.png",
    description: "Source links, import cycles, and staging/dump visibility.",
  },
  {
    title: "Schema mapping workspace",
    image: "/screenshots/schema-mapping.png",
    description: "Customer tables mapped into the generic canonical model.",
  },
  {
    title: "Jobs monitor",
    image: "/screenshots/jobs-monitor.png",
    description: "Source snapshot, canonical import, quality, risk, and ML jobs.",
  },
  {
    title: "ML readiness workspace",
    image: "/screenshots/ml-readiness.png",
    description: "Honest readiness gates before real model training starts.",
  },
  {
    title: "Investigation report",
    image: "/screenshots/investigation-report.png",
    description: "Data Diagnostic output for customer decision-making.",
  },
];

const lifecycleSteps = [
  ["Connect", "Configure file and database source connectors honestly."],
  ["Stage", "Copy raw source-shaped data into a safe staging/dump layer."],
  ["Map", "Use schema views and mapping rules to fit the generic canonical model."],
  ["Monitor", "Track jobs, refresh cycles, failures, duration, and next run."],
  ["Analyze", "Use dashboards, quality, risk, correlation, and investigation workflows."],
  ["Report", "Export a customer-grade Data Diagnostic report with limitations stated clearly."],
];

function Layout({ children }: { children: React.ReactNode }) {
  return (
    <div className="site-shell">
      <header className="site-header">
        <NavLink to="/" className="brand-link">
          <span className="sou-mark">
            <img src="/brand/sou-icon.svg" alt="SOU" width={38} height={38} />
          </span>
          <span className="brand-text">
            PlantProcess <strong>IQ</strong>
          </span>
        </NavLink>

        <nav className="nav-links">
          <NavLink to="/">Home</NavLink>
          <NavLink to="/product">Product</NavLink>
          <NavLink to="/pricing">Pricing</NavLink>
          <NavLink to="/security">Security</NavLink>
          <NavLink to="/contact">Contact</NavLink>
        </nav>

        <a className="header-cta" href={demoMailto}>
          Request founder demo
        </a>
      </header>

      {children}

      <footer className="site-footer">
        <div>
          <strong>PlantProcess IQ</strong>
          <span>Process-to-quality intelligence for manufacturing plants.</span>
        </div>
        <div>
          <span>Düsseldorf, Germany</span>
          <span>EU / MENA industrial focus</span>
        </div>
      </footer>
    </div>
  );
}

function HomePage() {
  return (
    <Layout>
      <section className="hero-section">
        <div className="hero-copy">
          <div className="status-pill">
            <Factory size={16} />
            Generic manufacturing intelligence layer
          </div>

          <h1>
            Connect plant data. Understand your process.
            <span> Act with evidence.</span>
          </h1>

          <p>
            PlantProcess IQ is a read-only intelligence layer for manufacturing
            plants. It connects plant data, maps it into a generic
            process-to-quality model, detects data readiness gaps, highlights
            suspected contributors, and prepares customer-grade Data Diagnostic
            reports.
          </p>

          <div className="hero-actions">
            <a className="button button-primary" href={demoMailto}>
              <CalendarCheck size={18} />
              Request 20-minute demo
            </a>

            <a className="button button-secondary" href="#how-it-works">
              <Workflow size={18} />
              See how it works
            </a>
          </div>
        </div>

        <div className="hero-real-screenshot">
          <div className="screenshot-browser-bar">
            <span />
            <span />
            <span />
            <strong>PlantProcess IQ Command Center</strong>
          </div>

          <img
            src="/screenshots/plantprocess-dashboard.png"
            alt="PlantProcess IQ dashboard screenshot"
            onError={(event) => {
              event.currentTarget.style.display = "none";
            }}
          />

          <div className="screenshot-fallback">
            <MonitorCheck size={42} />
            <strong>Replace with real deployed app screenshot</strong>
            <p>
              Save your real screenshot here:
              <br />
              <code>/public/screenshots/plantprocess-dashboard.png</code>
            </p>
          </div>
        </div>
      </section>

      <section className="website-section" id="how-it-works">
        <div className="section-heading">
          <div>
            <div className="eyebrow">
              <Workflow size={15} />
              How it works
            </div>
            <h2>One lifecycle: connect → stage → map → monitor → analyze → report.</h2>
          </div>
        </div>

        <div className="lifecycle-flow">
          {lifecycleSteps.map(([title, description], index) => (
            <article className="flow-card" key={title}>
              <span>{index + 1}</span>
              <h3>{title}</h3>
              <p>{description}</p>
            </article>
          ))}
        </div>
      </section>

      <ProductScreenshotShowcase />

      <section className="website-section screenshot-section">
        <div className="section-heading">
          <div>
            <div className="eyebrow">
              <MonitorCheck size={15} />
              Screenshot gallery
            </div>
            <h2>Proof screens for engineers before they book a demo.</h2>
          </div>
        </div>

        <div className="screenshot-gallery-grid">
          {screenshotCards.map((card) => (
            <article className="screenshot-card" key={card.title}>
              <img
                src={card.image}
                alt={card.title}
                onError={(event) => {
                  event.currentTarget.style.display = "none";
                }}
              />

              <div className="screenshot-card-fallback">
                <MonitorCheck size={22} />
                <strong>{card.title}</strong>
                <span>{card.description}</span>
                <code>{card.image}</code>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section className="website-section" id="demo-lifecycle">
        <div className="section-heading">
          <div>
            <div className="eyebrow">
              <GitBranch size={15} />
              Demo lifecycle
            </div>
            <h2>The demo follows the real product flow, not a fake isolated page.</h2>
          </div>
        </div>

        <div className="proof-grid">
          <article className="proof-card">
            <DatabaseZap size={24} />
            <h3>1. Configure source</h3>
            <p>Show read-only source setup, provider honesty, and refresh cycle.</p>
          </article>

          <article className="proof-card">
            <Network size={24} />
            <h3>2. Map schema</h3>
            <p>Show how plant-specific tables become generic canonical data.</p>
          </article>

          <article className="proof-card">
            <BarChart3 size={24} />
            <h3>3. Analyze</h3>
            <p>Show dashboards, risk, correlation, investigation, and job status.</p>
          </article>

          <article className="proof-card">
            <FileText size={24} />
            <h3>4. Report</h3>
            <p>Close with a Data Diagnostic report and ML readiness status.</p>
          </article>
        </div>
      </section>

      <PositioningTruthBlock />
      <ConnectorHonestyBlock />
      <PricingLicenseMatrix />
      <DataDiagnosticSection />
      <MlHonestySection />
      <BrandProofSection />
      <RequestDemoForm />
    </Layout>
  );
}

function ProductPage() {
  return (
    <Layout>
      <section className="page-hero">
        <div className="eyebrow">
          <Factory size={15} />
          Product
        </div>
        <h1>Generic process-to-quality intelligence for plants.</h1>
        <p>
          PlantProcess IQ is designed for different database types, different
          inspection devices, different process structures, and different KPIs —
          without hard-coding one plant or one industry.
        </p>
      </section>

      <section className="website-section">
        <div className="feature-grid">
          {[
            [
              "Generic source layer",
              "CSV and Excel are starter-friendly; database connectors are shown honestly according to certification status.",
            ],
            [
              "Staging / dump copy",
              "Keeps a latest safe copy of customer source-shaped data before canonical mapping.",
            ],
            [
              "Schema mapping",
              "Maps plant-specific tables into a generic manufacturing data model.",
            ],
            [
              "Jobs monitor",
              "Shows import, refresh, mapping, quality, risk, correlation, and readiness jobs.",
            ],
            [
              "Dashboard builder",
              "Creates pages and widgets from configured canonical data.",
            ],
            [
              "ML readiness",
              "Prepares labels and feature vectors before real training starts.",
            ],
          ].map(([title, text]) => (
            <article className="feature-card" key={title}>
              <CheckCircle2 size={22} />
              <h3>{title}</h3>
              <p>{text}</p>
            </article>
          ))}
        </div>
      </section>

      <ProductScreenshotShowcase />
      <PositioningTruthBlock />
      <ConnectorHonestyBlock />
      <BrandProofSection />
    </Layout>
  );
}

function PricingPage() {
  return (
    <Layout>
      <section className="page-hero">
        <div className="eyebrow">
          <BadgeEuro size={15} />
          License architecture
        </div>
        <h1>Start with a diagnostic. Grow into a licensed platform.</h1>
        <p>
          Pricing is staged to match product maturity: data diagnostic first,
          pilot next, then annual license after workflow proof.
        </p>
      </section>

      <PricingLicenseMatrix />

      <p className="pricing-note">
        Pricing labels are demo-stage positioning. Final pilot/SOW pricing
        depends on scope, deployment model, connectors, support, and data volume.
      </p>

      <DataDiagnosticSection />
      <RequestDemoForm />
    </Layout>
  );
}

function SecurityPage() {
  return (
    <Layout>
      <section className="page-hero">
        <div className="eyebrow">
          <ShieldCheck size={15} />
          Security and data handling
        </div>
        <h1>Read-only by design. No plant control. No write-back.</h1>
        <p>
          PlantProcess IQ is positioned as a safe evidence layer. It reads data,
          stages it, maps it, analyzes it, and reports findings without replacing
          operational systems.
        </p>
      </section>

      <section className="website-section">
        <div className="proof-grid">
          <article className="proof-card">
            <ShieldCheck size={24} />
            <h3>Read-only source contract</h3>
            <p>Source links should be configured as read-only wherever possible.</p>
          </article>

          <article className="proof-card">
            <DatabaseZap size={24} />
            <h3>Staging before canonical</h3>
            <p>Raw source-shaped data lands in staging before canonical mapping.</p>
          </article>

          <article className="proof-card">
            <FileText size={24} />
            <h3>Audit-friendly reporting</h3>
            <p>Reports include data readiness, evidence, and limitation statements.</p>
          </article>
        </div>
      </section>

      <PositioningTruthBlock />
      <ConnectorHonestyBlock />
    </Layout>
  );
}

function ContactPage() {
  return (
    <Layout>
      <section className="page-hero">
        <div className="eyebrow">
          <Mail size={15} />
          Contact
        </div>
        <h1>Book a founder-led PlantProcess IQ walkthrough.</h1>
        <p>
          Send a short note with your plant type, quality problem, data sources,
          and preferred time.
        </p>
      </section>

      <section className="website-section contact-grid">
        <article className="contact-card">
          <Mail size={24} />
          <h3>Email</h3>
          <a href={demoMailto}>info@plantprocessiq.com</a>
        </article>

        <article className="contact-card">
          <MapPin size={24} />
          <h3>Location</h3>
          <p>Düsseldorf, Germany — EU / MENA industrial focus.</p>
        </article>

        <article className="contact-card">
          <CalendarCheck size={24} />
          <h3>Demo request</h3>
          <a className="button button-primary" href={demoMailto}>
            Open email request
          </a>
        </article>
      </section>

      <RequestDemoForm />
    </Layout>
  );
}

function DataDiagnosticSection() {
  return (
    <section className="website-section diagnostic-section">
      <div>
        <div className="eyebrow">
          <FileText size={15} />
          Data Diagnostic offer
        </div>

        <h2>The first realistic paid offer before pilot/license.</h2>

        <p>
          A short paid diagnostic can validate data availability, source
          structure, mapping effort, quality gaps, risk/correlation potential,
          and ML readiness before committing to a larger pilot.
        </p>
      </div>

      <div className="diagnostic-card">
        <strong>Typical deliverable</strong>
        <ul>
          <li>Source and connector inventory</li>
          <li>Schema mapping coverage</li>
          <li>Data quality findings</li>
          <li>Risk/correlation evidence</li>
          <li>ML readiness score</li>
          <li>Recommended pilot scope</li>
        </ul>
      </div>
    </section>
  );
}

function MlHonestySection() {
  return (
    <section className="website-section ml-truth-section">
      <div>
        <div className="eyebrow">
          <BrainCircuit size={15} />
          ML preview honesty
        </div>

        <h2>ML workspace is readiness-first, not a production ML claim.</h2>

        <p>
          Today the product uses rule-based risk scoring, correlation analysis,
          and suspected contributor ranking. ML training starts only after enough
          validated labeled historical quality data exists.
        </p>
      </div>

      <div className="ml-truth-card">
        <BrainCircuit size={30} />
        <strong>No trained production model active</strong>
        <span>No AI prediction claim. No guaranteed root cause claim.</span>
      </div>
    </section>
  );
}

export function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/product" element={<ProductPage />} />
      <Route path="/services" element={<ProductPage />} />
      <Route path="/pricing" element={<PricingPage />} />
      <Route path="/security" element={<SecurityPage />} />
      <Route path="/about" element={<ProductPage />} />
      <Route path="/contact" element={<ContactPage />} />
    </Routes>
  );
}

export default App;