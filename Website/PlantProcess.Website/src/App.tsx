import { useEffect, useState, type ReactNode } from "react";
import { NavLink, Navigate, Route, Routes, useLocation, useParams } from "react-router-dom";
import {
  ArrowRight,
  BadgeDollarSign,
  BarChart3,
  BrainCircuit,
  Building2,
  Check,
  ChevronDown,
  CircleDollarSign,
  Clock3,
  DatabaseZap,
  Factory,
  FileSearch,
  Gauge,
  GitBranch,
  Globe2,
  Layers3,
  LockKeyhole,
  Mail,
  MapPin,
  Menu,
  Radar,
  ScanLine,
  ShieldCheck,
  Sparkles,
  TrendingDown,
  Workflow,
  X,
} from "lucide-react";

import { HeroTopology } from "./components/graphics/HeroTopology";
import { GoldenThread } from "./components/graphics/GoldenThread";
import { SignalVsNoise } from "./components/graphics/SignalVsNoise";
import { TrustEngine } from "./components/graphics/TrustEngine";
import { FounderAuthority } from "./components/sections/FounderAuthority";
import { ProofOfValueJourney } from "./components/sections/ProofOfValueJourney";
import { RolePaths } from "./components/sections/RolePaths";
import RequestDemoForm from "./components/proof/RequestDemoForm";
import { licensePlans, websiteConnectors } from "./content/phase1WebsiteProof";
import "./styles/phase10.css";

type PackCode = "quality" | "reliability" | "energy" | "yard";
type RoleCode = "operations" | "quality" | "engineering" | "security" | "value";

type Pack = {
  code: PackCode;
  title: string;
  eyebrow: string;
  headline: string;
  description: string;
  outcomes: string[];
  icon: typeof Factory;
};

const packs: Pack[] = [
  {
    code: "quality",
    title: "Quality / Surface",
    eyebrow: "Defect intelligence",
    headline: "Trace recurring defects back through the production journey.",
    description: "Harmonize plant defect vocabulary, reconstruct material genealogy, compare process conditions and create an evidence-ranked investigation path.",
    outcomes: ["Defect-driver investigation", "Surface-event genealogy", "Claim and downgrade analysis", "Population and evidence drill-through"],
    icon: ScanLine,
  },
  {
    code: "reliability",
    title: "Reliability / Downtime",
    eyebrow: "Production impact",
    headline: "Separate equipment stops from the production losses they create.",
    description: "Connect downtime records to equipment, material and process context so operations can prioritize recurring events by impact, not frequency alone.",
    outcomes: ["Downtime cascade analysis", "Production-impact minutes", "Recurring event patterns", "Action and alert workflows"],
    icon: Gauge,
  },
  {
    code: "energy",
    title: "Energy Intelligence",
    eyebrow: "Process-aware energy",
    headline: "Explain consumption in the context of product, process and operating conditions.",
    description: "Relate energy KPIs to production context, shifts, equipment and quality outcomes—without turning the platform into a separate energy-control system.",
    outcomes: ["Energy per product", "Shift and grade comparison", "Outlier investigation", "Bounded value modelling"],
    icon: BarChart3,
  },
  {
    code: "yard",
    title: "Yard / Logistics",
    eyebrow: "Material-flow intelligence",
    headline: "Expose location, age, movement and buffer constraints across the material journey.",
    description: "Use the same read-only core to investigate inventory truth, coil location, stock age and material-flow bottlenecks.",
    outcomes: ["Material location truth", "Stock-age visibility", "Movement bottlenecks", "Buffer and dispatch intelligence"],
    icon: Layers3,
  },
];

const roleCopy: Record<RoleCode, { title: string; headline: string; text: string; points: string[]; icon: typeof Factory }> = {
  operations: {
    title: "For Operations",
    headline: "Find the losses that deserve attention before the next shift compounds them.",
    text: "PlantProcess IQ connects operational events, material context and quality outcomes into one prioritized investigation path.",
    points: ["Active-risk and freshness visibility", "Downtime and production-impact context", "Alerts linked to materials and jobs", "Action status and evidence trail"],
    icon: Factory,
  },
  quality: {
    title: "For Quality",
    headline: "Move from a failed coil to the upstream evidence in one governed thread.",
    text: "Trace final inspection events through coil, slab and heat while preserving the population, method and source lineage behind every finding.",
    points: ["Defect taxonomy from plant sources", "Heat-to-product genealogy", "Signal-versus-null comparison", "Evidence-ranked suspected contributors"],
    icon: ScanLine,
  },
  engineering: {
    title: "For Process Engineering",
    headline: "Replace weeks of spreadsheet stitching with a repeatable investigation.",
    text: "Compare process parameters and outcomes without losing material genealogy, statistical context or the exact population analysed.",
    points: ["Configured mappings—not bespoke code", "Governed analysis jobs", "Method, effect and q-value shown", "Reproducible findings and controls"],
    icon: Workflow,
  },
  security: {
    title: "For IT & OT",
    headline: "Approve intelligence without introducing a control-system command path.",
    text: "The source boundary is read-only. PlantProcess IQ never sends setpoints, recipes or commands back to MES, SCADA, PLC or Level 2.",
    points: ["Read-only source access", "Masked credentials and governed imports", "Cloud, on-prem and air-gapped paths", "Audit, role and license controls"],
    icon: ShieldCheck,
  },
  value: {
    title: "For CFO & Procurement",
    headline: "Fund a measurable plant outcome—not another open-ended software programme.",
    text: "Start with one costly defect or loss mechanism, prove whether a signal exists, expose the assumptions and scale only when the business case holds.",
    points: ["Focused Proof of Value", "Transparent installation scope", "Bounded value assumptions", "Clear pilot-to-scale decision"],
    icon: BadgeDollarSign,
  },
};

function ScrollToTop() {
  const location = useLocation();
  useEffect(() => window.scrollTo({ top: 0, behavior: "instant" }), [location.pathname]);
  return null;
}

function Layout({ children }: { children: ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();

  useEffect(() => setMobileOpen(false), [location.pathname]);

  return (
    <div className="site-shell phase10-shell">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <ScrollToTop />
      <header className="site-header phase10-header website-premium-header" data-testid="website-premium-header">
        <NavLink to="/" className="brand-link website-brand-link" aria-label="PlantProcess IQ home">
          <span className="website-brand-mark" aria-hidden="true"><Radar size={25} /></span>
          <span className="website-brand-text">
            <strong>PlantProcess IQ</strong>
            <small>SOU Industrial Software</small>
          </span>
        </NavLink>

        <button
          className="mobile-nav-button"
          type="button"
          aria-label={mobileOpen ? "Close navigation" : "Open navigation"}
          aria-expanded={mobileOpen}
          onClick={() => setMobileOpen((value) => !value)}
        >
          {mobileOpen ? <X size={22} /> : <Menu size={22} />}
        </button>

        <nav className={`website-main-nav ${mobileOpen ? "website-main-nav--open" : ""}`} aria-label="Main website navigation">
          <NavLink to="/product">Platform</NavLink>
          <a href="/#how-it-works">How it works</a>
          <div className="site-nav-menu" data-testid="website-solutions-menu">
            <button className="site-nav-trigger" type="button" aria-haspopup="true">
              Solutions <ChevronDown size={15} />
            </button>
            <div className="site-nav-popover" role="menu" aria-label="PlantProcess IQ solutions">
              <div className="nav-popover-heading">By decision owner</div>
              <NavLink to="/solutions/operations" role="menuitem"><Factory size={18} /><span><strong>Operations</strong><small>Loss, downtime and priority</small></span></NavLink>
              <NavLink to="/solutions/quality" role="menuitem"><ScanLine size={18} /><span><strong>Quality</strong><small>Defects, genealogy and proof</small></span></NavLink>
              <NavLink to="/solutions/engineering" role="menuitem"><Workflow size={18} /><span><strong>Process Engineering</strong><small>Parameters, methods and findings</small></span></NavLink>
              <NavLink to="/solutions/security" role="menuitem"><ShieldCheck size={18} /><span><strong>IT & OT</strong><small>Read-only architecture</small></span></NavLink>
              <NavLink to="/solutions/value" role="menuitem"><CircleDollarSign size={18} /><span><strong>CFO & Procurement</strong><small>Proof, scope and ROI</small></span></NavLink>
            </div>
          </div>
          <NavLink to="/proof">Proof</NavLink>
          <NavLink to="/security">Security</NavLink>
          <NavLink to="/pricing">Pricing</NavLink>
          <NavLink to="/about">About</NavLink>
        </nav>

        <a className="website-button website-button--primary website-header-cta" href="#request-demo">
          Start Proof of Value <ArrowRight size={17} />
        </a>
      </header>

      <main id="main-content">{children}</main>

      <footer className="site-footer phase10-footer">
        <div className="footer-brand">
          <span className="website-brand-mark" aria-hidden="true"><Radar size={22} /></span>
          <div><strong>PlantProcess IQ</strong><p>Read-only, evidence-grade process-to-quality intelligence.</p></div>
        </div>
        <div className="footer-links">
          <NavLink to="/product">Platform</NavLink>
          <NavLink to="/proof">Proof</NavLink>
          <NavLink to="/security">Security</NavLink>
          <NavLink to="/pricing">Pricing</NavLink>
          <NavLink to="/contact">Contact</NavLink>
        </div>
        <div className="footer-contact">
          <span><Mail size={15} /> info@plantprocessiq.com</span>
          <span><MapPin size={15} /> Düsseldorf, Germany</span>
          <span><Globe2 size={15} /> Europe · MENA · Global industrial projects</span>
        </div>
      </footer>
    </div>
  );
}

function HomePage() {
  return (
    <>
      <section className="commercial-hero">
        <div className="commercial-hero__glow commercial-hero__glow--one" aria-hidden="true" />
        <div className="commercial-hero__glow commercial-hero__glow--two" aria-hidden="true" />
        <div className="section-shell commercial-hero__layout">
          <div className="commercial-hero__copy">
            <div className="hero-proof-pill"><Sparkles size={16} /> Industrial intelligence with evidence attached</div>
            <h1>Connect Your Plant. Understand Your Process. <span>Stop the Losses.</span></h1>
            <p>
              Trace quality and performance losses from the final symptom back through the complete production journey—then identify the process conditions that deserve engineering attention.
            </p>
            <div className="hero-actions">
              <a className="website-button website-button--primary website-button--large" href="#request-demo">Start a Proof of Value <ArrowRight size={19} /></a>
              <a className="website-button website-button--secondary website-button--large" href="#how-it-works">See the detective story</a>
            </div>
            <div className="hero-proof-strip" aria-label="Core trust proof">
              <span><LockKeyhole size={17} /> Read-only by design</span>
              <span><GitBranch size={17} /> Full material genealogy</span>
              <span><FileSearch size={17} /> Evidence-ranked findings</span>
              <span><Building2 size={17} /> Cloud · on-prem · air-gapped</span>
            </div>
          </div>
          <HeroTopology />
        </div>
      </section>

      <section className="impact-ribbon" aria-label="Business impact narrative">
        <div className="section-shell impact-ribbon__grid">
          <article><TrendingDown size={25} /><span>Reduce exposure to</span><strong>Scrap · downgrade · claims</strong></article>
          <article><Clock3 size={25} /><span>Compress investigation</span><strong>From weeks of stitching to guided minutes</strong></article>
          <article><ShieldCheck size={25} /><span>Maintain the boundary</span><strong>Zero control write-back paths</strong></article>
        </div>
      </section>

      <section className="commercial-section story-section" id="how-it-works">
        <div className="section-shell">
          <div className="section-heading story-heading">
            <div><div className="section-kicker">The investigation</div><h2>Four acts. One expensive mystery solved with evidence.</h2></div>
            <p>The buyer is not watching software features. The buyer is watching their team regain control of a recurring loss.</p>
          </div>

          <div className="act act--crime">
            <div className="act__number">ACT 01</div>
            <div className="act__copy">
              <span className="act__label">The Crime Scene</span>
              <h3>The defect is visible. The origin is hidden.</h3>
              <p>MES, Level 2, historian, inspection, lab and spreadsheets each hold part of the truth. While teams stitch exports together, the plant may keep producing the same loss.</p>
            </div>
            <div className="crime-network" aria-hidden="true">
              {["MES", "L2", "SCADA", "Historian", "Inspection", "Excel"].map((item, index) => <span style={{ "--crime-index": index } as React.CSSProperties} key={item}>{item}</span>)}
              <div className="crime-network__center">?</div>
            </div>
          </div>

          <div className="act act--thread">
            <div className="act__number">ACT 02</div>
            <div className="act__copy">
              <span className="act__label">Tracing the Footprints</span>
              <h3>Walk backward from the failed product to the process conditions that shaped it.</h3>
              <p>PlantProcess IQ reconstructs the golden thread without asking the plant to remodel its source systems.</p>
            </div>
            <GoldenThread />
          </div>

          <div className="act act--trial">
            <div className="act__number">ACT 03</div>
            <div className="act__copy">
              <span className="act__label">The Trial & Verdict</span>
              <h3>The engine tests the suspect—and cleanly dismisses the noise.</h3>
              <p>CRACK_LONG shows the supported 9.3x validation relation. SCRATCH stays at the 1.0x null control. The system does not manufacture impressive numbers everywhere.</p>
            </div>
            <SignalVsNoise />
          </div>

          <div className="act act--execution">
            <div className="act__number">ACT 04</div>
            <div className="act__copy">
              <span className="act__label">Execution & ROI</span>
              <h3>Turn a trusted finding into the engineering decision that stops the next loss.</h3>
              <p>Review the evidence, monitor the risky condition, create the action, measure the outcome and scale only when the value is proven.</p>
            </div>
            <div className="execution-flow" aria-hidden="true">
              {["Finding", "Engineering review", "Alert / action", "Measured outcome", "Verified value"].map((item, index) => (
                <div key={item}><span>0{index + 1}</span><strong>{item}</strong>{index < 4 && <ArrowRight size={18} />}</div>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section className="commercial-section proof-scale-section" id="proof">
        <div className="section-shell">
          <div className="section-heading split-heading">
            <div><div className="section-kicker">Validation fleet</div><h2>Tested on an industrial-scale journey—not a toy spreadsheet.</h2></div>
            <p>Prepared source data enters through the same read-only import, mapping, genealogy and analysis path used for customer onboarding.</p>
          </div>
          <div className="scale-metrics">
            <article><strong>1,802</strong><span>Continuous-casting heats</span><small>Upstream process context</small></article>
            <article><strong>18,000+</strong><span>Coils mapped</span><small>Heat → slab → coil</small></article>
            <article><strong>34,000+</strong><span>Surface defects tracked</span><small>Inspection evidence</small></article>
            <article><strong>9.3x / 1.0x</strong><span>Signal versus control</span><small>Support and restraint</small></article>
          </div>
        </div>
      </section>

      <section className="commercial-section trust-engine-section">
        <div className="section-shell trust-engine-layout">
          <div className="trust-engine-copy">
            <div className="section-kicker">The trust architecture</div>
            <h2>The model explains. The engine computes.</h2>
            <p>Numbers are calculated by deterministic, governed services. The grounded assistant retrieves approved evidence, explains it with citations and refuses unsupported claims.</p>
            <ul className="icon-check-list">
              <li><Check size={17} /> Suspected contributors—not guaranteed root cause</li>
              <li><Check size={17} /> Population, method and run visible</li>
              <li><Check size={17} /> No uncited numeric claim</li>
              <li><Check size={17} /> No LLM arithmetic or ranking</li>
            </ul>
          </div>
          <TrustEngine />
        </div>
      </section>

      <RolePaths />

      <section className="commercial-section capability-section" id="capability-packs">
        <div className="section-shell">
          <div className="section-heading split-heading">
            <div><div className="section-kicker">One core · focused capability packs</div><h2>Expand the same evidence spine around the problems your plant owns.</h2></div>
            <p>PlantProcess IQ remains the read-only core. Quality, reliability, energy and logistics extend the investigation—not the control layer.</p>
          </div>
          <div className="capability-grid">
            {packs.map(({ code, title, eyebrow, headline, description, outcomes, icon: Icon }) => (
              <NavLink className="capability-card" to={`/packs/${code}`} key={code}>
                <div className="capability-card__icon"><Icon size={25} /></div>
                <span>{eyebrow}</span>
                <h3>{title}</h3>
                <strong>{headline}</strong>
                <p>{description}</p>
                <ul>{outcomes.slice(0, 3).map((item) => <li key={item}><Check size={14} /> {item}</li>)}</ul>
                <b>Explore capability →</b>
              </NavLink>
            ))}
          </div>
        </div>
      </section>

      <FounderAuthority />
      <ProofOfValueJourney />
      <RequestDemoForm />
    </>
  );
}

function PlatformPage() {
  return (
    <>
      <section className="subpage-hero subpage-hero--platform">
        <div className="section-shell subpage-hero__layout">
          <div>
            <div className="section-kicker">PlantProcess IQ Core</div>
            <h1>From fragmented plant records to a governed line of reasoning.</h1>
            <p>Connect read-only sources, stage source-shaped data, map the plant model, reconstruct genealogy, run governed analysis and explain the result with evidence.</p>
            <div className="hero-actions"><a className="website-button website-button--primary" href="#request-demo">Discuss your first use case</a><NavLink className="website-button website-button--secondary" to="/security">Review the trust boundary</NavLink></div>
          </div>
          <HeroTopology />
        </div>
      </section>

      <section className="commercial-section platform-stages-section">
        <div className="section-shell">
          <div className="section-heading"><div><div className="section-kicker">The platform spine</div><h2>Every capability shares one governed journey.</h2></div></div>
          <div className="platform-stages">
            {[
              ["01", "Connect", "Register approved plant sources, test connectivity and preserve the read-only boundary.", DatabaseZap],
              ["02", "Prepare", "Import only the delta, preserve source shape, and author mappings through the HMI.", Layers3],
              ["03", "Trace", "Link heat, slab, coil, event and equipment context into the golden thread.", GitBranch],
              ["04", "Analyse", "Run transparent methods with readiness, population, effect and FDR control.", BarChart3],
              ["05", "Explain", "Retrieve approved evidence, cite the result and refuse unsupported questions.", BrainCircuit],
              ["06", "Act", "Create alerts, review findings and connect action status to measurable outcomes.", Workflow],
            ].map(([step, title, text, Icon]) => {
              const StageIcon = Icon as typeof Factory;
              return <article key={String(step)}><span>{String(step)}</span><div><StageIcon size={22} /></div><h3>{String(title)}</h3><p>{String(text)}</p></article>;
            })}
          </div>
        </div>
      </section>

      <section className="commercial-section golden-thread-feature"><div className="section-shell"><div className="section-heading split-heading"><div><div className="section-kicker">Material genealogy</div><h2>The evidence follows the product—not the application screen.</h2></div><p>Every stage can retain source, batch, timestamp and canonical lineage so a finding remains drill-throughable.</p></div><GoldenThread /></div></section>
      <section className="commercial-section trust-engine-section"><div className="section-shell trust-engine-layout"><div className="trust-engine-copy"><div className="section-kicker">Intelligence governance</div><h2>Transparent math before generative explanation.</h2><p>The platform escalates from data to information, analysis, findings, suggestions and cited answers. The assistant never becomes a shortcut around evidence.</p></div><TrustEngine /></div></section>
      <RequestDemoForm />
    </>
  );
}

function ProofPage() {
  return (
    <>
      <section className="subpage-hero subpage-hero--proof"><div className="section-shell subpage-hero__layout"><div><div className="section-kicker">Signal and restraint</div><h1>A trustworthy engine proves the signal—and shows the null.</h1><p>The validation story is deliberately falsifiable: a supported CRACK_LONG relation, a neutral SCRATCH control, and every result tied to the analysed population and run.</p></div><SignalVsNoise /></div></section>
      <section className="commercial-section"><div className="section-shell"><div className="proof-narrative-grid"><article><span>01</span><h2>Prepared outside the product</h2><p>The validation pattern is planted in the emulated source—not in the application—so the product must discover it after import.</p></article><article><span>02</span><h2>Recovered through the real journey</h2><p>Source registration, delta import, mapping, genealogy and analysis use the same generic pathway intended for a customer plant.</p></article><article><span>03</span><h2>Controlled against noise</h2><p>The null control remains neutral. The evidence system is rewarded for restraint, not for finding a dramatic number everywhere.</p></article></div></div></section>
      <section className="commercial-section golden-thread-feature"><div className="section-shell"><div className="section-heading"><div><div className="section-kicker">Proof chain</div><h2>From source record to finding.</h2></div></div><GoldenThread /></div></section>
      <ProofOfValueJourney />
      <RequestDemoForm />
    </>
  );
}

function SecurityPage() {
  return (
    <>
      <section className="subpage-hero subpage-hero--security"><div className="section-shell subpage-hero__layout"><div><div className="section-kicker">OT-safe by design</div><h1>Industrial intelligence without a control-system command path.</h1><p>PlantProcess IQ reads approved data, processes it inside the governed deployment boundary, and produces decision support. No setpoint, recipe or command flows back.</p><div className="hero-actions"><a className="website-button website-button--primary" href="#request-demo">Request security review</a><a className="website-button website-button--secondary" href="#deployment">See deployment models</a></div></div><HeroTopology /></div></section>
      <section className="commercial-section security-pillars-section"><div className="section-shell"><div className="security-pillars-grid">
        {[
          ["Read-only source layer", "Source connectors read approved objects into controlled import batches. Credentials are masked on read-back.", LockKeyhole],
          ["Data and evidence boundary", "Source-shaped staging, canonical records, jobs, findings and assistant evidence remain distinct and traceable.", FileSearch],
          ["Identity and governance", "Role, tenant and signed entitlement checks apply to API and UI—not only to visible buttons.", ShieldCheck],
          ["AI honesty", "Deterministic engines calculate. The assistant explains with citations and refuses when the evidence is insufficient.", BrainCircuit],
        ].map(([title, text, Icon]) => { const PillarIcon = Icon as typeof Factory; return <article key={String(title)}><PillarIcon size={27} /><h2>{String(title)}</h2><p>{String(text)}</p></article>; })}
      </div></div></section>
      <section className="commercial-section deployment-section" id="deployment"><div className="section-shell"><div className="section-heading split-heading"><div><div className="section-kicker">One codebase · four topologies</div><h2>Deploy where the plant’s security posture requires.</h2></div><p>The deployment model changes the isolation boundary—not the product logic or evidence contract.</p></div><div className="deployment-grid">{[["SOU-hosted SaaS", "Fastest onboarding", "Logical tenant isolation"], ["Customer cloud", "Cloud mandate", "Logical or dedicated"], ["On-premises", "Data stays inside", "Physical single tenant"], ["Air-gapped", "High-security site", "Offline activation"]].map(([title, fit, isolation]) => <article key={title}><Building2 size={24} /><h3>{title}</h3><span>{fit}</span><strong>{isolation}</strong></article>)}</div></div></section>
      <section className="commercial-section trust-engine-section"><div className="section-shell trust-engine-layout"><div className="trust-engine-copy"><div className="section-kicker">Model governance</div><h2>Grounded explanation stays downstream of approved evidence.</h2><p>The assistant has no independent route to plant truth and no authority to calculate or rank findings.</p></div><TrustEngine /></div></section>
      <RequestDemoForm />
    </>
  );
}

function PricingPage() {
  return (
    <>
      <section className="subpage-hero subpage-hero--pricing"><div className="section-shell subpage-hero__layout"><div><div className="section-kicker">Commercial model</div><h1>Transparent investment. Scale after the value is proven.</h1><p>A one-time installation deposit reflects source complexity, mapping and required pages. The subscription reflects feature depth, users, computational demand, capability packs and deployment model.</p></div><div className="pricing-range-visual" aria-label="Commercial range"><div><span>Installation</span><strong>$12k–$50k</strong><small>One-time deposit</small></div><div><span>Subscription</span><strong>$6k–$25k</strong><small>Per month</small></div></div></div></section>
      <section className="commercial-section pricing-commercial-section"><div className="section-shell"><div className="pricing-grid">{licensePlans.map((plan) => <article className={`pricing-card pricing-card--${plan.code}`} key={plan.code}><div className="pricing-card__top"><span>{plan.name}</span>{plan.recommended && <b>Recommended</b>}</div><strong className="pricing-card__price">{plan.monthlyPrice}</strong><p>{plan.idealFor}</p><dl><div><dt>Installation</dt><dd>{plan.deposit}</dd></div><div><dt>Sources</dt><dd>{plan.sources}</dd></div><div><dt>Users</dt><dd>{plan.users}</dd></div></dl><ul>{plan.features.map((item) => <li key={item}><Check size={15} /> {item}</li>)}</ul><a className="website-button website-button--secondary" href="#request-demo">{plan.cta}</a></article>)}</div><p className="pricing-disclaimer">Final scope depends on source complexity, data volume, required pages, capability packs, support level and deployment model. Annual terms may be structured separately.</p></div></section>
      <section className="commercial-section roi-logic-section"><div className="section-shell roi-logic-layout"><div><div className="section-kicker">Why the economics work</div><h2>The software does not need to eliminate every loss. It needs to prevent one expensive recurrence faster.</h2><p>One severe downgrade batch, recurring quality claim or hour of constrained production can materially outweigh the annual software investment. The Proof of Value makes that comparison explicit on customer data.</p></div><div className="roi-equation" aria-label="ROI logic"><span>Avoided loss</span><b>+</b><span>Engineering time returned</span><b>+</b><span>Faster containment</span><b>−</b><span>Platform investment</span><strong>= Verified value case</strong></div></div></section>
      <ProofOfValueJourney />
      <RequestDemoForm />
    </>
  );
}

function AboutPage() {
  return (
    <>
      <section className="subpage-hero subpage-hero--about"><div className="section-shell subpage-hero__layout"><div><div className="section-kicker">SOU Industrial Software</div><h1>Industrial software built by people who have lived inside the data path.</h1><p>PlantProcess IQ grew from more than a decade of engineering Level 2 automation, MES, production models and industrial digitalization across international plants.</p></div><div className="about-orbit" aria-hidden="true"><span>Level 1</span><span>Level 2</span><span>MES</span><span>Quality</span><span>Analytics</span><div>13+ YEARS</div></div></div></section>
      <FounderAuthority />
      <section className="commercial-section principles-section"><div className="section-shell"><div className="section-heading"><div><div className="section-kicker">The operating principles</div><h2>Ambitious commercially. Restrained technically.</h2></div></div><div className="principles-grid">{["Configure from the HMI—do not hardcode the plant.", "Deterministic engines compute; the assistant explains.", "Every surfaced claim carries a resolvable evidence handle.", "Suspected contributor—never guaranteed root cause.", "Read-only is absolute toward control systems.", "The pilot proves the signal on the customer’s data."].map((item, index) => <article key={item}><span>0{index + 1}</span><p>{item}</p></article>)}</div></div></section>
      <RequestDemoForm />
    </>
  );
}

function ContactPage() {
  return (
    <><section className="subpage-hero subpage-hero--contact"><div className="section-shell subpage-hero__layout"><div><div className="section-kicker">Start with one expensive question</div><h1>Bring the defect, downtime mode or recurring loss your team cannot explain fast enough.</h1><p>We will define the minimum source scope, success metric, evidence standard and Proof-of-Value decision before asking you to scale.</p></div><div className="contact-brief"><Mail size={28} /><strong>20-minute fit check</strong><span>Problem · sources · success metric · security boundary · next step</span></div></div></section><RequestDemoForm /></>
  );
}

function PackPage() {
  const { code } = useParams<{ code: PackCode }>();
  const pack = packs.find((item) => item.code === code) ?? packs[0];
  const Icon = pack.icon;
  return <><section className="subpage-hero"><div className="section-shell subpage-hero__layout"><div><div className="section-kicker">{pack.eyebrow}</div><h1>{pack.headline}</h1><p>{pack.description}</p><a className="website-button website-button--primary" href="#request-demo">Discuss this capability</a></div><div className="pack-visual"><Icon size={44} /><strong>{pack.title}</strong><span>Capability pack on the PlantProcess IQ read-only core</span></div></div></section><section className="commercial-section"><div className="section-shell"><div className="outcomes-grid">{pack.outcomes.map((item, index) => <article key={item}><span>0{index + 1}</span><Check size={20} /><h2>{item}</h2></article>)}</div></div></section><ProofOfValueJourney /><RequestDemoForm /></>;
}

function RolePage() {
  const { code } = useParams<{ code: RoleCode }>();
  const role = roleCopy[code ?? "operations"] ?? roleCopy.operations;
  const Icon = role.icon;
  return <><section className="subpage-hero"><div className="section-shell subpage-hero__layout"><div><div className="section-kicker">{role.title}</div><h1>{role.headline}</h1><p>{role.text}</p><a className="website-button website-button--primary" href="#request-demo">Define your first outcome</a></div><div className="role-page-visual"><Icon size={48} /><strong>{role.title}</strong><span>One governed truth through your decision lens</span></div></div></section><section className="commercial-section"><div className="section-shell"><div className="role-page-points">{role.points.map((item, index) => <article key={item}><span>0{index + 1}</span><Check size={21} /><h2>{item}</h2></article>)}</div></div></section><ProofOfValueJourney /><RequestDemoForm /></>;
}

function LegacyProductRoute() {
  const { code } = useParams<{ code?: string }>();
  const map: Record<string, string> = { mes: "/packs/reliability", qes: "/packs/quality", yard: "/packs/yard", energy: "/packs/energy", "plantprocess-iq": "/product" };
  return <Navigate to={map[code ?? ""] ?? "/product"} replace />;
}

export function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/product" element={<PlatformPage />} />
        <Route path="/services" element={<Navigate to="/product" replace />} />
        <Route path="/proof" element={<ProofPage />} />
        <Route path="/security" element={<SecurityPage />} />
        <Route path="/pricing" element={<PricingPage />} />
        <Route path="/about" element={<AboutPage />} />
        <Route path="/contact" element={<ContactPage />} />
        <Route path="/packs/:code" element={<PackPage />} />
        <Route path="/solutions/:code" element={<RolePage />} />
        <Route path="/products/:code" element={<LegacyProductRoute />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Layout>
  );
}

export default App;
