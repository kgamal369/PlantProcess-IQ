
import type { ReactNode } from "react";
import { NavLink, Route, Routes, useParams } from "react-router-dom";
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
import { licensePlans, provenAtScale } from "./content/phase1WebsiteProof";
import "./styles/phase10.css";

type ProductCode = "plantprocess-iq" | "mes" | "qes" | "yard" | "energy";

type EcosystemProduct = {
  code: ProductCode;
  name: string;
  shortName: string;
  eyebrow: string;
  headline: string;
  description: string;
  benefit: string;
  licenseDetail: string;
  bestFor: string[];
  workflow: string[];
  proofPoints: string[];
};

const productEcosystem: EcosystemProduct[] = [
  {
    code: "plantprocess-iq",
    name: "PlantProcess IQ",
    shortName: "PPIQ",
    eyebrow: "Process-to-Quality Intelligence",
    headline: "Connect Your Plant Data. Understand Your Process.",
    description:
      "PlantProcess IQ connects to your existing plant systems read-only, unifies their data into one canonical model, and takes your team from seeing the data to reasoning about it: correlation, AI/ML suggestions, and a grounded AI assistant that answers with citations.",
    benefit:
      "Replace weeks of manual quality forensics across scattered systems with one evidence-grade workspace that shows suspected contributors to quality and downtime, with the population and the math shown.",
    licenseDetail:
      "Available from Standard through Enterprise. Correlation, AI/ML suggestions and the grounded assistant unlock progressively by tier.",
    bestFor: ["Quality teams", "Process engineers", "Plant managers", "Data and IT teams"],
    workflow: ["Connect (read-only)", "Stage", "Unify", "Analyse and Correlate", "Suggest (AI/ML)", "Ask (Grounded AI)"],
    proofPoints: [
      "Read-only: never writes to your systems",
      "One canonical model for any industry, via configuration only",
      "Evidence-grade: every number carries its population and method",
      "Correlation, suggestions and grounded AI implemented, validation ongoing",
    ],
  },
  {
    code: "mes",
    name: "SOU MES",
    shortName: "MES",
    eyebrow: "Execution Backbone",
    headline: "A practical MES direction for production execution, order tracking and operational visibility.",
    description:
      "The MES product line focuses on production execution, material tracking, order progress and operator-facing workflows. It is positioned as a separate execution system, not mixed into PlantProcess IQ.",
    benefit:
      "Create a clear path from shop-floor execution to management visibility while keeping quality intelligence as a complementary layer.",
    licenseDetail:
      "MES is an Enterprise / project-scoped product. Licensing depends on plant lines, execution scope, integrations and rollout model.",
    bestFor: ["Manufacturing execution", "Order tracking", "Operator workflows", "Production booking"],
    workflow: ["Receive orders", "Track execution", "Book production", "Handle exceptions", "Expose status"],
    proofPoints: [
      "Separate from PlantProcess IQ",
      "Execution-focused",
      "Integration-ready",
      "Project-scoped rollout",
    ],
  },
  {
    code: "qes",
    name: "SOU QES",
    shortName: "QES",
    eyebrow: "Quality Execution",
    headline: "Quality execution workflows for inspections, samples, decisions and nonconformance follow-up.",
    description:
      "QES focuses on operational quality execution: inspection plans, sample checks, lab handover, decision records and structured nonconformance workflows.",
    benefit:
      "Move quality teams from scattered spreadsheets and informal follow-ups into a controlled workflow with clear decision history.",
    licenseDetail:
      "QES can be sold as Pro Plus or Enterprise depending on inspection volume, lab integration and approval workflow complexity.",
    bestFor: ["QA leads", "Lab teams", "Inspection teams", "Nonconformance management"],
    workflow: ["Plan inspection", "Capture sample", "Record result", "Decide", "Escalate", "Close loop"],
    proofPoints: [
      "Inspection workflow discipline",
      "Decision traceability",
      "Lab integration direction",
      "Quality governance",
    ],
  },
  {
    code: "yard",
    name: "SOU Yard & Warehouse Management",
    shortName: "Yard",
    eyebrow: "Material Logistics",
    headline: "Yard and warehouse visibility for material location, movements, inventory and operational bottlenecks.",
    description:
      "The Yard product direction supports material movement planning, inventory visibility, location truth, equipment constraints and dispatch coordination.",
    benefit:
      "Improve confidence in where materials are, what is available, what is blocked and what movement should happen next.",
    licenseDetail:
      "Usually Enterprise scoped because yard logic depends on site topology, equipment, transport routes and integration requirements.",
    bestFor: ["Yard managers", "Warehouse teams", "Logistics coordinators", "Material planners"],
    workflow: ["Locate", "Reserve", "Move", "Confirm", "Handle exception", "Optimize"],
    proofPoints: [
      "Site topology aware",
      "Inventory truth",
      "Movement confirmation",
      "Exception handling",
    ],
  },
  {
    code: "energy",
    name: "SOU Energy Management",
    shortName: "Energy",
    eyebrow: "Energy Intelligence",
    headline: "Energy monitoring and process-aware consumption intelligence for manufacturing operations.",
    description:
      "Energy Management connects process context with consumption signals to show where energy is used, wasted or becoming a process-risk indicator.",
    benefit:
      "Help plant teams connect consumption patterns with equipment, products, shifts and operating conditions.",
    licenseDetail:
      "Pro Plus for site-level dashboards; Enterprise for multi-line integration, energy KPIs and governed reporting.",
    bestFor: ["Energy managers", "Operations leaders", "Process engineers", "Sustainability teams"],
    workflow: ["Collect meters", "Map context", "Calculate KPIs", "Compare shifts", "Detect outliers", "Report"],
    proofPoints: [
      "Process-context energy view",
      "KPI-ready",
      "Outlier investigation",
      "Sustainability reporting direction",
    ],
  },
];

const trustPillars = [
  {
    title: "Read-only source layer",
    text: "PlantProcess IQ reads source data into a controlled staging layer. It does not write back to MES, SCADA, PLC, Level 2 or customer source systems.",
    icon: <DatabaseZap size={24} />,
  },
  {
    title: "Data handling",
    text: "The platform separates raw source-shaped data, mapping logic, canonical records, read models and customer-facing reports.",
    icon: <Network size={24} />,
  },
  {
    title: "Deployment models",
    text: "Supported deployment directions include local demo, private customer environment and enterprise-controlled rollout. Production rollout requires customer security review.",
    icon: <MonitorCheck size={24} />,
  },
  {
    title: "AI honesty",
    text: "Deterministic engines compute and rank; the AI assistant only explains, with citations. Results are suspected contributors with the population and method shown, never automatic root-cause proof, and never process-control commands.",
    icon: <BrainCircuit size={24} />,
  },
  {
    title: "Enterprise controls",
    text: "RBAC, audit logs, tenant-aware access and signed license-feature gates are treated as product controls, not decorative UI.",
    icon: <ShieldCheck size={24} />,
  },
];

const howItWorksSteps = [
  {
    step: "01",
    title: "Connect, read-only",
    text: "Register your existing systems as data sources: databases, files, historians. PlantProcess IQ never writes to them.",
  },
  {
    step: "02",
    title: "Stage",
    text: "A two-stage import copies source data into a controlled staging layer, incrementally, with every run monitored and logged.",
  },
  {
    step: "03",
    title: "Unify",
    text: "A no-code mapping workbench joins the staged tables into one canonical plant model: materials, process steps, quality events, downtime, KPIs, genealogy.",
  },
  {
    step: "04",
    title: "Analyse and correlate",
    text: "Deterministic engines run disciplined statistics on the unified data and rank suspected contributors to quality and downtime, population and method shown.",
  },
  {
    step: "05",
    title: "Suggest",
    text: "An AI/ML suggestion engine turns findings into evidence-ranked, workflow-bound recommendations your team can accept, track, and close.",
  },
  {
    step: "06",
    title: "Ask",
    text: "A grounded AI assistant answers questions about your plant data with citations. It explains what the engines computed; it cannot invent a number.",
  },
];

const beyondBiCards = [
  {
    icon: <GitBranch size={24} />,
    title: "Correlation engine",
    text: "Disciplined statistics on your unified data: correlation methods with readiness gates, false-discovery control and noise rejection. Ranked suspected contributors, never asserted causes.",
  },
  {
    icon: <Workflow size={24} />,
    title: "AI/ML suggestions",
    text: "Deterministic, evidence-ranked recommendations with confidence scoring and a closed-loop outcome workflow, so suggestions are tracked to results, not forgotten.",
  },
  {
    icon: <BrainCircuit size={24} />,
    title: "Grounded AI assistant",
    text: "Ask in plain language; get answers with resolvable citations. The assistant explains the engines' results and cannot render an uncited number. Self-hosted and no-egress options for sensitive plants.",
  },
];

const comingSoonCodes: ReadonlySet<ProductCode> = new Set<ProductCode>(["mes", "qes", "yard", "energy"]);
const isComingSoon = (code: ProductCode): boolean => comingSoonCodes.has(code);

function statusBadge(code: ProductCode): ReactNode {
  if (isComingSoon(code)) {
    return <span className="status-pill status-pill--soon">Coming soon</span>;
  }
  return <span className="status-pill status-pill--live">Available now</span>;
}

function productIcon(code: ProductCode): ReactNode {
  switch (code) {
    case "plantprocess-iq":
      return <BrainCircuit size={30} />;
    case "mes":
      return <Workflow size={30} />;
    case "qes":
      return <CheckCircle2 size={30} />;
    case "yard":
      return <GitBranch size={30} />;
    case "energy":
      return <BarChart3 size={30} />;
    default:
      return <Factory size={30} />;
  }
}

function Layout({ children }: { children: ReactNode }) {
  return (
    <div className="site-shell phase10-shell">
      <header className="site-header phase10-header">
        <NavLink to="/" className="brand-link" aria-label="PlantProcess IQ home">
          <span className="sou-mark">
            <img src="/brand/sou-icon.svg" alt="SOU Industrial Software" width={38} height={38} />
          </span>
          <span className="brand-text">
            <strong>PlantProcess IQ</strong>
            <small>SOU Industrial Software</small>
          </span>
        </NavLink>

        <nav className="site-nav phase10-nav" aria-label="Main website navigation">
          <NavLink to="/">Home</NavLink>
          <NavLink to="/product">PPIQ</NavLink>
          <NavLink to="/products/mes">MES</NavLink>
          <NavLink to="/products/qes">QES</NavLink>
          <NavLink to="/products/yard">Yard</NavLink>
          <NavLink to="/products/energy">Energy</NavLink>
          <NavLink to="/pricing">Pricing</NavLink>
          <NavLink to="/security">Security</NavLink>
          <NavLink to="/contact">Contact</NavLink>
        </nav>

        <a className="website-button website-button--primary header-cta" href="#request-demo">
          Request demo
        </a>
      </header>

      <main>{children}</main>

      <footer className="site-footer phase10-footer">
        <div>
          <strong>PlantProcess IQ</strong>
          <p>Process-to-quality intelligence: read-only, evidence-grade, for any industry via configuration only.</p>
        </div>
        <div className="footer-contact">
          <span><Mail size={16} /> info@plantprocessiq.com</span>
          <span><MapPin size={16} /> Duesseldorf, Germany / MENA industrial network</span>
          <span><FileText size={16} /> Engineer brief and demo pack ready for customer conversations</span>
        </div>
      </footer>
    </div>
  );
}

function EcosystemGraphic({ product }: { product: EcosystemProduct }) {
  return (
    <div className="ecosystem-graphic" aria-label={`${product.name} workflow graphic`}>
      <div className="ecosystem-graphic__center">
        {productIcon(product.code)}
        <strong>{product.shortName}</strong>
      </div>

      {product.workflow.map((step, index) => (
        <div className={`ecosystem-node ecosystem-node--${index + 1}`} key={step}>
          <span>{String(index + 1).padStart(2, "0")}</span>
          <strong>{step}</strong>
        </div>
      ))}
    </div>
  );
}

function ProductCard({ product }: { product: EcosystemProduct }) {
  const href = product.code === "plantprocess-iq" ? "/product" : `/products/${product.code}`;

  return (
    <NavLink className="ecosystem-product-card" to={href}>
      <span className="ecosystem-product-card__icon">{productIcon(product.code)}</span>
      <span className="section-kicker">{product.eyebrow}</span>
      <strong>
        {product.name}
        {statusBadge(product.code)}
      </strong>
      <p>{product.description}</p>
      <span className="card-link-text">Open product page</span>
    </NavLink>
  );
}

function HowItWorksSection() {
  return (
    <section className="website-section ppiq-flow-section" id="how-it-works">
      <div className="section-kicker">How it works</div>
      <div className="section-heading-row">
        <div>
          <h2>From scattered plant systems to one line of reasoning.</h2>
          <p>
            One read-only pipeline, configured entirely from the application, no code:
            connect, stage, unify, analyse and correlate, suggest, ask.
          </p>
        </div>
      </div>

      <div className="ppiq-flow">
        {howItWorksSteps.map((item) => (
          <article className="ppiq-flow__step" key={item.step}>
            <span className="ppiq-flow__number">{item.step}</span>
            <strong>{item.title}</strong>
            <p>{item.text}</p>
          </article>
        ))}
      </div>

      <p className="ppiq-flow__note">
        Every stage is read-only toward your systems. Your MES, SCADA, Level 2 and databases
        are never written to, never controlled, never slowed down.
      </p>
    </section>
  );
}

function ProvenAtScaleSection() {
  return (
    <section className="website-section ppiq-scale-section" id="proven-at-scale">
      <div className="section-kicker">Proven at scale</div>
      <div className="section-heading-row">
        <div>
          <h2>Validated on a full sample plant, not a toy dataset.</h2>
          <p>
            Every capability is exercised against a complete emulated plant: six live source
            systems across four database engines, imported, unified and analysed through the
            same generic pipeline a real customer would use.
          </p>
        </div>
      </div>

      <div className="ppiq-scale-grid">
        {provenAtScale.map((stat) => (
          <article className="ppiq-scale-card" key={stat.label}>
            <strong>{stat.value}</strong>
            <span>{stat.label}</span>
          </article>
        ))}
      </div>
    </section>
  );
}

function BeyondBiSection() {
  return (
    <section className="website-section ppiq-beyond-section" id="beyond-bi">
      <div className="section-kicker">Beyond conventional BI</div>
      <div className="section-heading-row">
        <div>
          <h2>Standard BI stops at seeing. PlantProcess IQ takes you to reasoning.</h2>
          <p>
            Dashboards show you what happened. The intelligence layer on top tells you what
            most likely contributed, suggests what to do about it, and answers your questions
            with citations.
          </p>
        </div>
      </div>

      <div className="ppiq-beyond-grid">
        {beyondBiCards.map((card) => (
          <article className="trust-pillar-card" key={card.title}>
            {card.icon}
            <h3>{card.title}</h3>
            <p>{card.text}</p>
          </article>
        ))}
      </div>

      <p className="ppiq-flow__note">
        Honest status: the correlation engine, suggestion engine and grounded assistant are
        implemented; validation on the full sample plant is ongoing. Results are always
        suspected contributors with the population shown, never guaranteed root cause.
      </p>
    </section>
  );
}

function HomePage() {
  return (
    <>
      <section className="page-hero phase10-hero">
        <div className="hero-copy">
          <div className="section-kicker">Process-to-quality intelligence</div>
          <h1>Connect Your Plant Data. Understand Your Process.</h1>
          <p>
            Your plant already produces the data: quality systems, process automation,
            downtime logs, lab files. PlantProcess IQ connects to them read-only, unifies
            them into one model, and shows your team the suspected drivers of quality and
            downtime, with the evidence attached.
          </p>

          <div className="hero-actions">
            <a className="website-button website-button--primary" href="#request-demo">
              Request a demo
            </a>
            <NavLink className="website-button website-button--secondary" to="/security">
              Read trust model
            </NavLink>
          </div>
        </div>

        <div className="hero-command-card">
          <Factory size={34} />
          <strong>Any industry, via configuration only</strong>
          <p>Food and beverage, steel, aluminum, paper, tires, pharma, chemicals, automotive: one generic model, zero code change per plant.</p>
          <div className="hero-mini-grid">
            <span>Read-only</span>
            <span>Evidence-first</span>
            <span>Configurable</span>
            <span>License-gated</span>
          </div>
        </div>
      </section>

      <HowItWorksSection />
      <BeyondBiSection />
      <ProvenAtScaleSection />

      <section className="website-section product-ecosystem-section">
        <div className="section-kicker">Product ecosystem</div>
        <div className="section-heading-row">
          <div>
            <h2>Five product stories, one industrial brand architecture.</h2>
            <p>
              PlantProcess IQ is available today as the flagship quality-intelligence product.
              SOU MES, QES, Yard and Energy are on the roadmap - their pages describe the
              intended direction, not a shipping product yet.
            </p>
          </div>
        </div>

        <div className="ecosystem-card-grid">
          {productEcosystem.map((product) => (
            <ProductCard product={product} key={product.code} />
          ))}
        </div>
      </section>

      <ProductScreenshotShowcase />
      <ConnectorHonestyBlock />
      <PositioningTruthBlock />
      <BrandProofSection />
      <RequestDemoForm />
    </>
  );
}

function ProductPage({ fixedCode }: { fixedCode?: ProductCode }) {
  const params = useParams<{ code?: string }>();
  const requestedCode = fixedCode ?? params.code;
  const product = productEcosystem.find((item) => item.code === requestedCode) ?? productEcosystem[0];
  const comingSoon = isComingSoon(product.code);
  const isPpiq = product.code === "plantprocess-iq";

  return (
    <>
      <section className="page-hero product-detail-hero">
        <div>
          <div className="section-kicker">{product.eyebrow}</div>
          <h1>{product.headline}</h1>
          <p>{product.description}</p>
          {comingSoon && (
            <div className="status-banner status-banner--soon" role="status">
              <strong>In development - not yet available.</strong>{" "}
              PlantProcess IQ is the product you can run today. {product.name} is on the roadmap;
              register your interest below and we will share progress and an early-access path.
            </div>
          )}

          <div className="hero-actions">
            <a className="website-button website-button--primary" href="#request-demo">
              Discuss {product.shortName}
            </a>
            <NavLink className="website-button website-button--secondary" to="/pricing">
              View pricing
            </NavLink>
          </div>
        </div>

        <EcosystemGraphic product={product} />
      </section>

      <section className="website-section product-detail-section">
        <div className="product-detail-grid">
          <article>
            <span className="section-kicker">Business benefit</span>
            <h2>{product.benefit}</h2>
            <ul className="phase10-check-list">
              {product.proofPoints.map((point) => (
                <li key={point}><CheckCircle2 size={18} /> {point}</li>
              ))}
            </ul>
          </article>

          <article>
            <span className="section-kicker">Best fit</span>
            <h2>Designed for teams who own the workflow.</h2>
            <ul className="phase10-pill-list">
              {product.bestFor.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </article>

          <article>
            <span className="section-kicker">{comingSoon ? "Planned commercial model" : "License detail"}</span>
            <h2>Commercial packaging stays honest.</h2>
            <p>{product.licenseDetail}</p>
            {comingSoon && (
              <p className="status-note">This is the intended licensing direction once {product.name} ships. Nothing here is purchasable yet.</p>
            )}
            <a className="website-button website-button--secondary" href="#request-demo">
              {comingSoon ? "Register interest" : "Ask for fit check"}
            </a>
          </article>
        </div>
      </section>

      {isPpiq && (
        <>
          <HowItWorksSection />
          <BeyondBiSection />
          <ProvenAtScaleSection />
        </>
      )}

      <RequestDemoForm />
    </>
  );
}

function PricingPage() {
  return (
    <>
      <section className="page-hero pricing-hero">
        <div>
          <div className="section-kicker">Pricing and license architecture</div>
          <h1>One deposit, one subscription. Standard, Pro Plus, or Enterprise.</h1>
          <p>
            A one-time deposit covers installation, source connection and setup, scaled by
            the number of data sources and pages. The monthly subscription covers the license
            tier, from connected dashboards up to the full grounded AI layer. Annual pre-pay
            earns a discount.
          </p>
        </div>

        <div className="pricing-proof-card">
          <BadgeEuro size={36} />
          <strong>Value-chain aligned tiers</strong>
          <p>Standard connects and shows. Pro Plus analyses and suggests. Enterprise reasons with you.</p>
        </div>
      </section>

      <PricingLicenseMatrix />

      <section className="website-section phase10-feature-matrix">
        <div className="section-kicker">Feature and usage tiers</div>
        <div className="phase10-matrix">
          <div className="phase10-matrix__row phase10-matrix__head">
            <span>Tier</span>
            <span>Best for</span>
            <span>Deposit</span>
            <span>Subscription</span>
            <span>Data sources</span>
          </div>

          {licensePlans.map((plan) => (
            <div className="phase10-matrix__row" key={plan.code}>
              <strong>{plan.name}</strong>
              <span>{plan.idealFor}</span>
              <span>{plan.deposit}</span>
              <span>{plan.monthlyPrice}</span>
              <span>{plan.sources}</span>
            </div>
          ))}
        </div>
      </section>

      <RequestDemoForm />
    </>
  );
}

function SecurityPage() {
  return (
    <>
      <section className="page-hero security-hero">
        <div>
          <div className="section-kicker">Security, trust and AI honesty</div>
          <h1>Built for industrial credibility: read-only first, governed data, controlled rollout.</h1>
          <p>
            PlantProcess IQ must earn trust before it touches real customer production data.
            The public website states what the system does, what it does not do, and where
            enterprise controls belong.
          </p>
        </div>

        <div className="trust-stack-card">
          <ShieldCheck size={38} />
          <strong>Trust contract</strong>
          <span>Read-only | auditable | license-gated | evidence-first</span>
        </div>
      </section>

      <section className="website-section trust-pillar-section">
        <div className="trust-pillar-grid">
          {trustPillars.map((pillar) => (
            <article className="trust-pillar-card" key={pillar.title}>
              {pillar.icon}
              <h2>{pillar.title}</h2>
              <p>{pillar.text}</p>
            </article>
          ))}
        </div>
      </section>

      <ConnectorHonestyBlock />
      <PositioningTruthBlock />
      <RequestDemoForm />
    </>
  );
}

function ContactPage() {
  return (
    <>
      <section className="page-hero contact-hero">
        <div>
          <div className="section-kicker">Request demo / lead capture</div>
          <h1>Book a practical discovery call around your plant data problem.</h1>
          <p>
            The best first conversation is specific: source systems, quality pain, plant type,
            current investigation effort and who owns the decision.
          </p>
        </div>

        <div className="contact-proof-card">
          <CalendarCheck size={38} />
          <strong>20-minute fit check</strong>
          <span>Problem, data sources, demo fit, next step</span>
        </div>
      </section>

      <RequestDemoForm />
    </>
  );
}

export function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/product" element={<ProductPage fixedCode="plantprocess-iq" />} />
        <Route path="/services" element={<ProductPage fixedCode="plantprocess-iq" />} />
        <Route path="/products/:code" element={<ProductPage />} />
        <Route path="/pricing" element={<PricingPage />} />
        <Route path="/security" element={<SecurityPage />} />
        <Route path="/about" element={<ProductPage fixedCode="plantprocess-iq" />} />
        <Route path="/contact" element={<ContactPage />} />
      </Routes>
    </Layout>
  );
}

export default App;
