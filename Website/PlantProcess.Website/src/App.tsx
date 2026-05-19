import { Navigate, NavLink, Route, Routes } from "react-router-dom";
import {
  BarChart3,
  Boxes,
  BrainCircuit,
  CheckCircle2,
  DatabaseZap,
  Factory,
  FileText,
  GitBranch,
  Lock,
  Mail,
  MapPin,
  Network,
  ShieldCheck,
  Truck,
  Workflow
} from "lucide-react";

const pricingTiers = [
  {
    name: "Light",
    price: "Starting from €1.2k–€2k / year",
    description: "For one engineer validating PlantProcess IQ with limited CSV/Excel data.",
    features: ["1 user", "CSV / Excel starter data", "2–3 pages", "Basic dashboards", "Manual investigation"]
  },
  {
    name: "Pro",
    price: "Starting from €8k–€12k / year",
    description: "For engineering teams preparing real plant data discovery and demos.",
    features: ["Up to 5 users", "CSV, Excel, PostgreSQL", "Jobs monitor", "Data quality", "Basic PDF report"],
    highlighted: true
  },
  {
    name: "Pro Plus",
    price: "€25k–€40k / year",
    description: "For advanced plant intelligence pilots with stronger configuration and reporting.",
    features: ["Up to 20 users", "More sources", "Correlation jobs", "Advanced reports", "Priority support"]
  },
  {
    name: "Enterprise",
    price: "Custom",
    description: "For larger private-cloud/on-prem deployments with custom governance.",
    features: ["Custom users", "Private deployment", "RBAC/audit", "Custom connectors", "Enterprise support"]
  }
];

const industries = ["Steel", "Aluminum", "Paper", "Automotive", "Tire", "Pharma", "Food", "Chemicals"];

function Layout({ children }: { children: React.ReactNode }) {
  return (
    <div className="site-shell">
      <header className="site-header">
        <NavLink to="/" className="brand-link">
          <span className="sou-mark">SOU</span>
          <span className="brand-text">
            SOU <strong>Industrial Software</strong>
          </span>
        </NavLink>

        <nav className="nav-links">
          <NavLink to="/product">PlantProcess IQ</NavLink>
          <NavLink to="/services">MES & Yard</NavLink>
          <NavLink to="/pricing">Pricing</NavLink>
          <NavLink to="/security">Security</NavLink>
          <NavLink to="/about">About</NavLink>
          <NavLink to="/contact">Contact</NavLink>
        </nav>

        <NavLink to="/contact" className="header-cta">
          Request Demo
        </NavLink>
      </header>

      <main>{children}</main>

      <footer className="site-footer">
        <div>
          <strong>SOU Industrial Software</strong>
          <span>PlantProcess IQ · MES consulting · Yard Management solutions</span>
        </div>
        <div className="footer-links">
          <NavLink to="/product">Product</NavLink>
          <NavLink to="/services">Services</NavLink>
          <NavLink to="/pricing">Pricing</NavLink>
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
          <span className="status-pill">
            <ShieldCheck size={16} />
            SOU manufacturing intelligence and MES expertise
          </span>

          <h1>
            Process-to-quality intelligence, MES thinking and yard visibility for <span>manufacturing plants.</span>
          </h1>

          <p>
            SOU helps plants connect fragmented production, process, inspection, lab and ERP data.
            Our flagship product, PlantProcess IQ, is a read-only intelligence layer for quality
            investigation, genealogy, risk scoring and evidence-based reporting.
          </p>

          <div className="hero-actions">
            <NavLink className="button button-primary" to="/contact">
              Request Founder Demo
            </NavLink>
            <NavLink className="button button-secondary" to="/product">
              Explore PlantProcess IQ
            </NavLink>
          </div>
        </div>

        <div className="hero-panel">
          <div className="panel-topline">
            <span>PlantProcess IQ</span>
            <span>Read-only intelligence layer</span>
          </div>

          <div className="signal-grid">
            <div>
              <strong>Connect</strong>
              <span>MES · L2 · inspection · lab · ERP · Excel · PostgreSQL</span>
            </div>
            <div>
              <strong>Map</strong>
              <span>Dump/staging copy · schema mapping · canonical model · jobs monitor</span>
            </div>
            <div>
              <strong>Investigate</strong>
              <span>Dashboards · data quality · correlation · risk score · PDF report</span>
            </div>
          </div>

          <div className="mock-chart">
            <BarChart3 size={92} />
          </div>
        </div>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="Problem"
          title="The data is already there. It is just too fragmented to investigate fast."
          description="Plant teams often need days of SQL, Excel and manual system checking to understand one defect, delay, downgrade or claim."
        />

        <div className="feature-grid four">
          <FeatureCard icon={<DatabaseZap />} title="Many source systems" description="MES, Level 2, process historian, inspection devices, lab systems, ERP, downtime logs and Excel exports." />
          <FeatureCard icon={<GitBranch />} title="Broken genealogy" description="Heat, batch, piece, coil, product, sample and defect identifiers do not always connect cleanly." />
          <FeatureCard icon={<Workflow />} title="Manual investigation" description="Engineers spend time joining tables instead of understanding the process window and suspected contributors." />
          <FeatureCard icon={<FileText />} title="Weak reporting" description="Findings often stay in screenshots, SQL notes and Excel files instead of customer-grade evidence reports." />
        </div>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="How it works"
          title="From source data to investigation evidence"
          description="PlantProcess IQ follows a controlled four-step path."
        />

        <div className="process-flow">
          <Step number="01" title="Connect to data" text="Create read-only source links or import CSV/Excel/PostgreSQL exports into staging." />
          <Step number="02" title="Configure schema" text="Map customer-specific tables and fields into a generic manufacturing model." />
          <Step number="03" title="Monitor jobs" text="Track import, mapping, data-quality and risk-scoring jobs from one operational page." />
          <Step number="04" title="Investigate and report" text="Use dashboards, correlation analysis, risk indicators and PDF reports to explain suspected contributors." />
        </div>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="SOU offering"
          title="One company, three industrial software paths"
          description="PlantProcess IQ is the product. MES and Yard Management are separate SOU implementation/service offerings."
        />

        <div className="feature-grid three">
          <FeatureCard icon={<BrainCircuit />} title="PlantProcess IQ" description="Manufacturing quality-intelligence layer for genealogy, data quality, correlation, risk scoring and investigation reports." />
          <FeatureCard icon={<Factory />} title="MES consulting & implementation" description="Manufacturing Execution System architecture, workflows, production tracking, quality integration and shopfloor data modeling." />
          <FeatureCard icon={<Truck />} title="Yard Management" description="Material yard visibility, location logic, movement tracking, stock overview and integration with production/order processes." />
        </div>
      </section>
    </Layout>
  );
}

function ProductPage() {
  return (
    <Layout>
      <PageHero
        eyebrow="PlantProcess IQ"
        title="A read-only intelligence layer above your existing plant systems."
        description="PlantProcess IQ connects fragmented data into a generic process-to-quality model without replacing MES, SCADA, Level 2 or ERP."
      />

      <section className="content-section">
        <SectionHeader
          eyebrow="Features"
          title="What PlantProcess IQ can demonstrate"
          description="The first website must be honest: it sells the direction, the demo path and the value, without overclaiming unbuilt ML."
        />

        <div className="feature-grid three">
          <FeatureCard icon={<DatabaseZap />} title="DB Link Configuration" description="Configure data sources and decide what tables/files are imported into the app." />
          <FeatureCard icon={<Boxes />} title="Dump / staging copy" description="Store the latest customer source-state copy before mapping it to the canonical model." />
          <FeatureCard icon={<Workflow />} title="Schema Configuration" description="Use mapping and SQL-view concepts to connect plant-specific tables to generic entities." />
          <FeatureCard icon={<Network />} title="Jobs Monitor" description="Track import, mapping, scan, risk and future learning jobs with status and history." />
          <FeatureCard icon={<BarChart3 />} title="Dynamic dashboards" description="Custom pages, widgets, filters and KPI monitoring for process engineers." />
          <FeatureCard icon={<FileText />} title="Investigation PDF" description="Customer-grade report output for readiness, findings and pilot recommendations." />
        </div>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="Industries"
          title="Steel first demo. Generic platform by design."
          description="Steel can be the first story, but the platform language and architecture stay domain-neutral."
        />

        <div className="industry-grid">
          {industries.map((industry) => (
            <article className="industry-card" key={industry}>{industry}</article>
          ))}
        </div>
      </section>

      <NonGoals />
    </Layout>
  );
}

function ServicesPage() {
  return (
    <Layout>
      <PageHero
        eyebrow="SOU services"
        title="Beyond PlantProcess IQ: MES and Yard Management delivery."
        description="SOU can position PlantProcess IQ as a product and MES/Yard Management as project-based industrial software services."
      />

      <section className="content-section">
        <div className="feature-grid three">
          <FeatureCard
            icon={<Factory />}
            title="MES implementation support"
            description="Production workflow design, operation tracking, quality/event models, integration with automation and ERP, and manufacturing data architecture."
          />
          <FeatureCard
            icon={<Truck />}
            title="Yard Management System"
            description="Stockyard visibility, material movement, location tracking, logistics overview, yard-to-production integration and reporting."
          />
          <FeatureCard
            icon={<BrainCircuit />}
            title="PlantProcess IQ diagnostics"
            description="Readiness assessment, data mapping workshop, correlation diagnostic and process-to-quality investigation reporting."
          />
        </div>
      </section>

      <section className="warning-note">
        <strong>Important positioning:</strong>
        <span>
          SOU may sell MES and Yard Management separately, but PlantProcess IQ itself remains
          a read-only intelligence layer and must not be described as replacing MES, SCADA or Level 2.
        </span>
      </section>
    </Layout>
  );
}

function PricingPage() {
  return (
    <Layout>
      <PageHero
        eyebrow="Pricing"
        title="Simple commercial packages for discovery, diagnostics and pilots."
        description="Use starting-from language on the website. Full pricing and scope should be confirmed in proposal."
      />

      <section className="content-section">
        <div className="pricing-grid">
          {pricingTiers.map((tier) => (
            <article className={tier.highlighted ? "pricing-card highlighted" : "pricing-card"} key={tier.name}>
              {tier.highlighted && <span className="tier-badge">Best for first serious demos</span>}
              <h3>{tier.name}</h3>
              <strong>{tier.price}</strong>
              <p>{tier.description}</p>
              <ul>
                {tier.features.map((feature) => (
                  <li key={feature}>{feature}</li>
                ))}
              </ul>
            </article>
          ))}
        </div>
      </section>

      <section className="compare-section">
        <SectionHeader
          eyebrow="Why not only BI or full MES?"
          title="PlantProcess IQ sits between generic BI and heavy MES projects."
          description="Power BI can visualize data, but it does not provide a manufacturing domain model, genealogy, data quality scan or process-to-quality investigation workflow. Full MES projects are powerful but slower and much more expensive."
        />
      </section>
    </Layout>
  );
}

function SecurityPage() {
  return (
    <Layout>
      <PageHero
        eyebrow="Security and data handling"
        title="Read-only access. Customer-approved data. No production control."
        description="The website must build trust through clear boundaries, not hype."
      />

      <section className="content-section">
        <div className="feature-grid four">
          <FeatureCard icon={<Lock />} title="Read-only by design" description="No writeback to MES, Level 2, SCADA, PLC or ERP in the current product positioning." />
          <FeatureCard icon={<ShieldCheck />} title="Customer-approved data" description="Use synthetic data for demos and customer-approved exports after NDA for diagnostics." />
          <FeatureCard icon={<CheckCircle2 />} title="RBAC and audit roadmap" description="Admin, Engineer, DataManager and Viewer roles are part of the saleability path." />
          <FeatureCard icon={<Factory />} title="Private/on-prem option" description="The product can be positioned for cloud demo, private cloud or on-prem pilot depending on customer requirements." />
        </div>
      </section>

      <NonGoals />
    </Layout>
  );
}

function AboutPage() {
  return (
    <Layout>
      <PageHero
        eyebrow="About SOU"
        title="Industrial software built from real plant automation and MES experience."
        description="SOU is positioned around manufacturing execution knowledge, plant data integration, process-quality analytics and practical engineering delivery."
      />

      <section className="content-section">
        <div className="feature-grid two">
          <FeatureCard
            icon={<Factory />}
            title="Industrial background"
            description="Experience across MES, Level 2 automation, production tracking, quality systems, databases, commissioning and plant data integration."
          />
          <FeatureCard
            icon={<BrainCircuit />}
            title="Product direction"
            description="PlantProcess IQ focuses on connecting plant data, discovering quality drivers, scoring risk earlier and acting with evidence."
          />
        </div>
      </section>
    </Layout>
  );
}

function ContactPage() {
  return (
    <Layout>
      <PageHero
        eyebrow="Contact"
        title="Request a founder demo or discuss an MES/Yard Management project."
        description="Use this page for PlantProcess IQ demos, diagnostic discussions, MES consulting and Yard Management opportunities."
      />

      <section className="contact-grid">
        <article className="contact-card">
          <Mail size={26} />
          <h3>Email</h3>
          <p>info@plantprocessiq.com</p>
        </article>

        <article className="contact-card">
          <MapPin size={26} />
          <h3>Location</h3>
          <p>Düsseldorf, Germany · EU/MENA industrial software focus</p>
        </article>
      </section>
    </Layout>
  );
}

function SectionHeader({ eyebrow, title, description }: { eyebrow: string; title: string; description: string }) {
  return (
    <div className="section-header">
      <span className="eyebrow">{eyebrow}</span>
      <h2>{title}</h2>
      <p>{description}</p>
    </div>
  );
}

function PageHero({ eyebrow, title, description }: { eyebrow: string; title: string; description: string }) {
  return (
    <section className="page-hero">
      <span className="eyebrow">{eyebrow}</span>
      <h1>{title}</h1>
      <p>{description}</p>
    </section>
  );
}

function FeatureCard({ icon, title, description }: { icon: React.ReactNode; title: string; description: string }) {
  return (
    <article className="feature-card">
      <div className="feature-icon">{icon}</div>
      <h3>{title}</h3>
      <p>{description}</p>
    </article>
  );
}

function Step({ number, title, text }: { number: string; title: string; text: string }) {
  return (
    <article className="step-card">
      <span>{number}</span>
      <h3>{title}</h3>
      <p>{text}</p>
    </article>
  );
}

function NonGoals() {
  return (
    <section className="warning-note">
      <strong>Non-goals:</strong>
      <span>
        PlantProcess IQ is not a replacement for MES, SCADA, Level 2 or ERP. It does not
        write back to production control and it does not claim guaranteed root cause.
      </span>
    </section>
  );
}

export function App() {
  return (
    <Routes>
      <Route index element={<HomePage />} />
      <Route path="/product" element={<ProductPage />} />
      <Route path="/services" element={<ServicesPage />} />
      <Route path="/pricing" element={<PricingPage />} />
      <Route path="/security" element={<SecurityPage />} />
      <Route path="/about" element={<AboutPage />} />
      <Route path="/contact" element={<ContactPage />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
