# ============================================================
# SOU / PlantProcess IQ - Public Website Frontend Scaffold V2
# Creates a real React + Vite + TypeScript website structure.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\tools\scaffold-website-frontend-v2.ps1 -Force
# ============================================================

param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

function Write-File {
    param(
        [string]$Path,
        [string]$Content
    )

    $folder = Split-Path $Path -Parent

    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }

    if ((Test-Path $Path) -and (-not $Force)) {
        Write-Host "SKIP: $Path" -ForegroundColor Yellow
        return
    }

    Set-Content -Path $Path -Value $Content -Encoding UTF8
    Write-Host "WRITE: $Path" -ForegroundColor Green
}

Write-Host ""
Write-Host "============================================================"
Write-Host " SOU / PlantProcess IQ Website Scaffold V2"
Write-Host " Target: $WebsiteRoot"
Write-Host "============================================================"
Write-Host ""

New-Item -ItemType Directory -Force -Path $WebsiteRoot | Out-Null

# ------------------------------------------------------------
# package.json
# ------------------------------------------------------------
Write-File -Path (Join-Path $WebsiteRoot "package.json") -Content @'
{
  "name": "sou-plantprocess-website",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview --host 0.0.0.0",
    "check:ts": "tsc -b --verbose",
    "lint": "echo \"Website lint placeholder - ESLint can be added after V1 content stabilizes\"",
    "test": "echo \"Website tests placeholder - Vitest can be added after V1 content stabilizes\"",
    "validate": "npm run build && npm run lint && npm run test"
  },
  "dependencies": {
    "lucide-react": "^1.14.0",
    "react": "^19.2.6",
    "react-dom": "^19.2.6",
    "react-router-dom": "^7.15.0"
  },
  "devDependencies": {
    "@types/node": "^24.12.3",
    "@types/react": "^19.2.14",
    "@types/react-dom": "^19.2.3",
    "@vitejs/plugin-react": "^6.0.1",
    "typescript": "~6.0.2",
    "vite": "^8.0.12"
  }
}
'@

# ------------------------------------------------------------
# HTML / Config
# ------------------------------------------------------------
Write-File -Path (Join-Path $WebsiteRoot "index.html") -Content @'
<!doctype html>
<html lang="en" data-theme="dark">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta
      name="description"
      content="SOU delivers PlantProcess IQ, MES consulting and Yard Management solutions for manufacturing plants."
    />
    <title>SOU | PlantProcess IQ | Manufacturing Intelligence, MES & Yard Management</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
'@

Write-File -Path (Join-Path $WebsiteRoot "vite.config.ts") -Content @'
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    host: "0.0.0.0",
    port: 5180
  },
  preview: {
    host: "0.0.0.0",
    port: 4180
  }
});
'@

Write-File -Path (Join-Path $WebsiteRoot "tsconfig.json") -Content @'
{
  "files": [],
  "references": [
    { "path": "./tsconfig.app.json" },
    { "path": "./tsconfig.node.json" }
  ]
}
'@

Write-File -Path (Join-Path $WebsiteRoot "tsconfig.app.json") -Content @'
{
  "compilerOptions": {
    "target": "ES2022",
    "useDefineForClassFields": true,
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "allowJs": false,
    "skipLibCheck": true,
    "strict": true,
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "verbatimModuleSyntax": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx"
  },
  "include": ["src"]
}
'@

Write-File -Path (Join-Path $WebsiteRoot "tsconfig.node.json") -Content @'
{
  "compilerOptions": {
    "target": "ES2023",
    "lib": ["ES2023"],
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "types": ["node"],
    "skipLibCheck": true,
    "strict": true,
    "noEmit": true
  },
  "include": ["vite.config.ts"]
}
'@

Write-File -Path (Join-Path $WebsiteRoot ".env.example") -Content @'
VITE_WEBSITE_API_BASE_URL=http://localhost:5080
'@

# ------------------------------------------------------------
# Docker
# ------------------------------------------------------------
Write-File -Path (Join-Path $WebsiteRoot "Dockerfile") -Content @'
FROM node:24-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

ARG VITE_WEBSITE_API_BASE_URL
ENV VITE_WEBSITE_API_BASE_URL=$VITE_WEBSITE_API_BASE_URL

RUN npm run build

FROM nginx:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
'@

Write-File -Path (Join-Path $WebsiteRoot "nginx.conf") -Content @'
server {
    listen 80;
    server_name _;

    root /usr/share/nginx/html;
    index index.html;

    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml image/svg+xml;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /health {
        access_log off;
        return 200 "sou-plantprocess-website-ok\n";
        add_header Content-Type text/plain;
    }
}
'@

# ------------------------------------------------------------
# React root
# ------------------------------------------------------------
Write-File -Path (Join-Path $WebsiteRoot "src\main.tsx") -Content @'
import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { App } from "./App";
import "./styles/global.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </React.StrictMode>
);
'@

Write-File -Path (Join-Path $WebsiteRoot "src\App.tsx") -Content @'
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
'@

# ------------------------------------------------------------
# CSS
# ------------------------------------------------------------
Write-File -Path (Join-Path $WebsiteRoot "src\styles\global.css") -Content @'
:root {
  --bg: #050B18;
  --panel: #0B1730;
  --panel-2: #102A43;
  --blue: #0A84FF;
  --cyan: #00D4FF;
  --green: #2CE6A2;
  --warning: #FFB020;
  --critical: #FF4D6D;
  --text: #EAF6FF;
  --muted: #8EA7C1;
  --border: rgba(142, 167, 193, 0.16);
  --border-cyan: rgba(0, 212, 255, 0.24);
  --shadow: 0 24px 80px rgba(0, 0, 0, 0.38);
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  color: var(--text);
  background: var(--bg);
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  min-width: 320px;
  min-height: 100vh;
  background:
    radial-gradient(circle at 18% 8%, rgba(0, 212, 255, 0.16), transparent 30%),
    radial-gradient(circle at 82% 4%, rgba(10, 132, 255, 0.16), transparent 26%),
    linear-gradient(180deg, #050B18 0%, #071124 54%, #050B18 100%);
}

a {
  color: inherit;
}

.site-shell {
  width: min(1180px, calc(100% - 40px));
  margin: 0 auto;
}

.site-header {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  gap: 18px;
  padding: 18px 0;
  backdrop-filter: blur(20px);
}

.brand-link {
  display: flex;
  align-items: center;
  gap: 12px;
  text-decoration: none;
  min-width: max-content;
}

.sou-mark {
  display: grid;
  place-items: center;
  width: 46px;
  height: 46px;
  border-radius: 15px;
  color: #00101a;
  background: linear-gradient(135deg, var(--cyan), var(--blue));
  font-weight: 950;
  letter-spacing: -0.08em;
  box-shadow: 0 0 36px rgba(0, 212, 255, 0.32);
}

.brand-text {
  font-size: 18px;
  font-weight: 850;
}

.brand-text strong {
  color: var(--cyan);
}

.nav-links {
  display: flex;
  justify-content: center;
  gap: 5px;
  flex: 1;
}

.nav-links a {
  color: var(--muted);
  text-decoration: none;
  font-size: 14px;
  font-weight: 750;
  padding: 10px 11px;
  border-radius: 10px;
}

.nav-links a.active,
.nav-links a:hover {
  color: var(--text);
  background: rgba(0, 212, 255, 0.08);
}

.header-cta,
.button {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  min-height: 44px;
  padding: 11px 16px;
  border-radius: 12px;
  border: 1px solid transparent;
  text-decoration: none;
  font-weight: 850;
}

.header-cta,
.button-primary {
  color: white;
  background: var(--blue);
  box-shadow: 0 0 32px rgba(10, 132, 255, 0.28);
}

.button-secondary {
  color: var(--text);
  background: rgba(11, 23, 48, 0.72);
  border-color: var(--border-cyan);
}

.hero-section {
  display: grid;
  grid-template-columns: 1.06fr 0.94fr;
  gap: 42px;
  align-items: center;
  padding: 86px 0 72px;
}

.page-hero {
  padding: 76px 0 46px;
}

.hero-copy h1,
.page-hero h1 {
  margin: 0 0 22px;
  max-width: 980px;
  font-size: clamp(42px, 6vw, 76px);
  line-height: 0.98;
  letter-spacing: -0.075em;
}

.hero-copy h1 span {
  color: var(--cyan);
}

.hero-copy p,
.page-hero p,
.section-header p,
.feature-card p,
.pricing-card p,
.warning-note span,
.site-footer span {
  color: var(--muted);
  line-height: 1.7;
}

.hero-copy p,
.page-hero p {
  max-width: 800px;
  margin: 0 0 30px;
  font-size: 18px;
}

.status-pill,
.eyebrow,
.tier-badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  width: max-content;
  margin-bottom: 18px;
  color: var(--green);
  border: 1px solid rgba(44, 230, 162, 0.32);
  background: rgba(44, 230, 162, 0.08);
  border-radius: 999px;
  padding: 8px 12px;
  font-size: 13px;
  font-weight: 850;
}

.eyebrow {
  color: var(--cyan);
  border-color: rgba(0, 212, 255, 0.28);
  background: rgba(0, 212, 255, 0.08);
}

.hero-actions {
  display: flex;
  gap: 14px;
  flex-wrap: wrap;
}

.hero-panel,
.feature-card,
.pricing-card,
.step-card,
.warning-note,
.contact-card,
.industry-card {
  border: 1px solid var(--border);
  background: linear-gradient(180deg, rgba(11, 23, 48, 0.94), rgba(8, 18, 38, 0.94));
  border-radius: 24px;
  box-shadow: var(--shadow);
}

.hero-panel {
  padding: 24px;
}

.panel-topline {
  display: flex;
  justify-content: space-between;
  color: var(--muted);
  font-size: 13px;
  margin-bottom: 18px;
}

.signal-grid {
  display: grid;
  gap: 12px;
}

.signal-grid div {
  padding: 16px;
  border-radius: 16px;
  background: rgba(16, 42, 67, 0.64);
  border: 1px solid rgba(0, 212, 255, 0.12);
}

.signal-grid strong {
  display: block;
  margin-bottom: 7px;
}

.signal-grid span {
  color: var(--muted);
  font-size: 14px;
}

.mock-chart {
  display: grid;
  place-items: center;
  min-height: 210px;
  margin-top: 16px;
  color: var(--cyan);
  border-radius: 18px;
  background:
    linear-gradient(rgba(0, 212, 255, 0.06) 1px, transparent 1px),
    linear-gradient(90deg, rgba(0, 212, 255, 0.06) 1px, transparent 1px),
    rgba(0, 212, 255, 0.035);
  background-size: 28px 28px;
}

.content-section {
  padding: 42px 0;
}

.section-header {
  max-width: 860px;
  margin-bottom: 24px;
}

.section-header h2 {
  margin: 0 0 14px;
  font-size: clamp(30px, 4vw, 48px);
  line-height: 1.04;
  letter-spacing: -0.055em;
}

.feature-grid,
.pricing-grid,
.industry-grid,
.contact-grid,
.process-flow {
  display: grid;
  gap: 18px;
}

.feature-grid.four,
.pricing-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.feature-grid.three,
.process-flow {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.feature-grid.two,
.contact-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.industry-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.feature-card,
.pricing-card,
.step-card,
.contact-card,
.industry-card {
  padding: 22px;
}

.feature-card:hover,
.pricing-card:hover,
.step-card:hover,
.contact-card:hover,
.industry-card:hover {
  border-color: var(--border-cyan);
  box-shadow: 0 0 42px rgba(0, 212, 255, 0.14);
}

.feature-icon {
  display: grid;
  place-items: center;
  width: 46px;
  height: 46px;
  color: var(--cyan);
  border: 1px solid var(--border-cyan);
  border-radius: 15px;
  background: rgba(0, 212, 255, 0.08);
  margin-bottom: 18px;
}

.feature-icon svg {
  width: 24px;
  height: 24px;
}

.feature-card h3,
.pricing-card h3,
.step-card h3,
.contact-card h3 {
  margin: 0 0 10px;
  font-size: 19px;
}

.feature-card p,
.pricing-card p,
.step-card p,
.contact-card p {
  margin: 0;
  font-size: 15px;
}

.step-card span {
  display: inline-flex;
  margin-bottom: 14px;
  color: var(--cyan);
  font-weight: 950;
}

.pricing-card.highlighted {
  border-color: rgba(0, 212, 255, 0.52);
  background: linear-gradient(180deg, rgba(10, 132, 255, 0.18), rgba(11, 23, 48, 0.94));
}

.pricing-card strong {
  display: block;
  color: var(--cyan);
  font-size: 23px;
  margin-bottom: 12px;
}

.pricing-card ul {
  padding-left: 18px;
  color: var(--muted);
  line-height: 1.8;
}

.industry-card {
  min-height: 110px;
  display: flex;
  align-items: end;
  font-size: 19px;
  font-weight: 850;
}

.warning-note {
  display: grid;
  gap: 8px;
  margin: 42px 0;
  padding: 24px;
  border-color: rgba(255, 176, 32, 0.36);
  background: rgba(255, 176, 32, 0.06);
}

.warning-note strong {
  color: var(--warning);
}

.compare-section {
  padding: 28px 0 60px;
}

.site-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  padding: 36px 0 46px;
  border-top: 1px solid var(--border);
}

.site-footer div:first-child {
  display: grid;
  gap: 4px;
}

.footer-links {
  display: flex;
  gap: 16px;
}

.footer-links a {
  color: var(--muted);
  text-decoration: none;
}

.footer-links a:hover {
  color: var(--cyan);
}

@media (max-width: 1050px) {
  .hero-section,
  .feature-grid.four,
  .feature-grid.three,
  .pricing-grid,
  .industry-grid,
  .process-flow {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .hero-section {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 760px) {
  .site-shell {
    width: min(100% - 28px, 1180px);
  }

  .site-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .nav-links {
    justify-content: flex-start;
    flex-wrap: wrap;
  }

  .hero-section,
  .page-hero {
    padding: 48px 0 34px;
  }

  .feature-grid.four,
  .feature-grid.three,
  .feature-grid.two,
  .pricing-grid,
  .industry-grid,
  .contact-grid,
  .process-flow {
    grid-template-columns: 1fr;
  }

  .site-footer {
    align-items: flex-start;
    flex-direction: column;
  }
}
'@

# ------------------------------------------------------------
# README
# ------------------------------------------------------------
Write-File -Path (Join-Path $WebsiteRoot "README.md") -Content @'
# SOU / PlantProcess IQ Public Website

This is the public marketing website frontend for SOU Industrial Software and PlantProcess IQ.

## Includes

- SOU company positioning
- PlantProcess IQ product positioning
- MES consulting/service offering
- Yard Management service offering
- Pricing page
- Security and non-goals page
- About/founder page
- Contact/demo page

## Run locally

```powershell
cd Website\PlantProcess.Website
npm install
npm run build
npm run dev