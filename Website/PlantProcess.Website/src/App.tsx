import { NavLink, Route, Routes } from "react-router-dom";
import type { ReactNode } from "react";
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
  XCircle,
} from "lucide-react";
import { BrandProofSection } from "./components/BrandProofSection";
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

const connectorRows = [
  ["CSV snapshot", "Available", "Safe for demo and starter diagnostic imports."],
  ["Excel snapshot", "Visible / proof required", "Show only after end-to-end proof passes."],
  ["PostgreSQL read-only", "Available for Pro+", "Use for read-only database integration demos."],
  ["Microsoft SQL Server", "Planned", "Do not mark available until tested connector proof exists."],
  ["Oracle", "Planned", "Important for steel plants, but not available until tested."],
  ["MySQL", "Planned", "Useful for inspection/QMS systems; keep honest as planned."],
];

const pricingPlans = [
  {
    name: "Light",
    label: "Starter validation",
    price: "€1.2k–€2k / year",
    description: "For one engineer validating the concept with limited CSV/Excel data.",
    features: ["1 user", "CSV/Excel starter source", "2–3 pages", "Basic dashboard", "Manual investigation"],
  },
  {
    name: "Pro",
    label: "Engineering team",
    price: "€8k–€12k / year",
    description: "For teams running real discovery with controlled database connections.",
    features: ["Up to 5 users", "CSV / Excel / PostgreSQL", "Jobs monitor", "Data quality scan", "Basic PDF report"],
    highlighted: true,
  },
  {
    name: "Pro Plus",
    label: "Pilot-ready intelligence",
    price: "€25k–€40k / year",
    description: "For serious pilots with more sources, correlation, ML readiness, and stronger reports.",
    features: ["Up to 20 users", "More sources", "Correlation jobs", "ML readiness workspace", "Full genealogy report"],
  },
  {
    name: "Enterprise",
    label: "Private deployment",
    price: "Custom",
    description: "For larger plants needing private cloud/on-prem, governance, and custom support.",
    features: ["Custom users", "Private deployment", "RBAC/audit roadmap", "Custom connectors", "Enterprise support"],
  },
];

const lifecycleSteps = [
  ["Connect", "Configure CSV, Excel, PostgreSQL or future source connectors."],
  ["Stage", "Copy raw data into a safe read-only staging/dump layer."],
  ["Map", "Use SQL/JOIN views and mapping rules to fit the generic model."],
  ["Monitor", "Track jobs, refresh cycle, failures, duration, and next run."],
  ["Analyze", "Dashboard, quality, risk, correlation, and investigation workflow."],
  ["Report", "Export a Data Diagnostic report with ML readiness status."],
];

function Layout({ children }: { children: React.ReactNode }) {
  return (
    <div className="site-shell">
      <header className="site-header">
        <NavLink to="/" className="brand-link">
          {/* Use the actual SOU icon SVG — file exists at /brand/sou-icon.svg */}
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
            plants. It connects plant data, maps it into a generic process-to-quality
            model, detects data readiness gaps, highlights suspected contributors,
            and prepares customer-grade Data Diagnostic reports.
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

      <section className="website-section no-replacement-block">
        <div>
          <div className="eyebrow">
            <XCircle size={15} />
            Clear positioning
          </div>

          <h2>Not MES. Not SCADA. Not Level 2. Not BI-only.</h2>

          <p>
            PlantProcess IQ sits above existing MES, L2, SCADA, historians,
            inspection systems, QMS, ERP, and plant databases. It does not write
            back to production systems. It creates an evidence layer for process,
            quality, genealogy, risk, correlation, and readiness.
          </p>
        </div>

        <div className="replacement-grid">
          {["MES", "SCADA", "L2 automation", "ERP", "BI tools"].map((item) => (
            <div key={item}>
              <XCircle size={18} />
              <span>Not replacing {item}</span>
            </div>
          ))}
        </div>
      </section>

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

      <ConnectorHonestySection />

      <DataDiagnosticSection />

      <MlHonestySection />

      <BrandProofSection />

      <section className="website-section demo-cta-section" id="demo">
        <div>
          <div className="eyebrow">
            <CalendarCheck size={15} />
            Demo CTA
          </div>

          <h2>Run a 20-minute investigation-first walkthrough.</h2>

          <p>
            Best demo story: connector → import job → schema mapping → dashboard
            widget → quality investigation → ML readiness → customer report.
          </p>
        </div>

        <a className="button button-primary" href={demoMailto}>
          <Mail size={17} />
          Request founder demo by email
        </a>
      </section>
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
            ["Generic source layer", "CSV, Excel, PostgreSQL now; MSSQL, Oracle, MySQL planned after tested proof."],
            ["Staging / dump copy", "Keeps a latest safe copy of customer source data before canonical mapping."],
            ["Schema mapping", "Maps plant-specific tables into a generic manufacturing data model."],
            ["Jobs monitor", "Shows import, refresh, mapping, quality, risk, correlation, and ML readiness jobs."],
            ["Dashboard builder", "Creates pages and widgets from configured canonical data."],
            ["ML readiness", "Prepares labels and feature vectors before real training starts."],
          ].map(([title, text]) => (
            <article className="feature-card" key={title}>
              <CheckCircle2 size={22} />
              <h3>{title}</h3>
              <p>{text}</p>
            </article>
          ))}
        </div>
      </section>
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
          Pricing is staged to match product maturity: Data Diagnostic first,
          then pilot, then annual license after proof.
        </p>
      </section>

      <section className="website-section" id="pricing">
        <div className="pricing-grid">
          {pricingPlans.map((plan) => (
            <article
              className={`pricing-card ${plan.highlighted ? "highlighted" : ""}`}
              key={plan.name}
            >
              <div className="pricing-card-top">
                <h3>{plan.name}</h3>
                <span>{plan.label}</span>
              </div>

              <strong>{plan.price}</strong>
              <p>{plan.description}</p>

              <ul>
                {plan.features.map((feature) => (
                  <li key={feature}>
                    <CheckCircle2 size={16} />
                    {feature}
                  </li>
                ))}
              </ul>
            </article>
          ))}
        </div>

        <p className="pricing-note">
          Pricing labels are demo-stage positioning. Final pilot/SOW pricing
          depends on scope, deployment model, connectors, support, and data volume.
        </p>
      </section>

      <DataDiagnosticSection />
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
            <p>Raw data lands in staging before any canonical mapping.</p>
          </article>

          <article className="proof-card">
            <FileText size={24} />
            <h3>Audit-friendly reporting</h3>
            <p>Reports include data readiness and limitation statements.</p>
          </article>
        </div>
      </section>
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
    </Layout>
  );
}

function ConnectorHonestySection() {
  return (
    <section className="website-section" id="connectors">
      <div className="section-heading">
        <div>
          <div className="eyebrow">
            <DatabaseZap size={15} />
            Connector status honesty
          </div>
          <h2>Only tested connectors are marked available.</h2>
        </div>
      </div>

      <div className="connector-table">
        {connectorRows.map(([name, status, note]) => (
          <div className="connector-row" key={name}>
            <strong>{name}</strong>
            <span>{status}</span>
            <p>{note}</p>
          </div>
        ))}
      </div>
    </section>
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
          A short paid diagnostic can validate data availability, source structure,
          mapping effort, quality gaps, risk/correlation potential, and ML readiness
          before committing to a larger pilot.
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