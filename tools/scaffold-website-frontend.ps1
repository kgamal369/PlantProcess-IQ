# ============================================================
# PlantProcess IQ - Public Website Frontend Scaffold
# Creates: Website/PlantProcess.Website
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\tools\scaffold-website-frontend.ps1
#
# Optional:
#   powershell -ExecutionPolicy Bypass -File .\tools\scaffold-website-frontend.ps1 -Install
#   powershell -ExecutionPolicy Bypass -File .\tools\scaffold-website-frontend.ps1 -Force
# ============================================================

param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
    [switch]$Install,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

function Write-ScaffoldFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $folder = Split-Path $Path -Parent

    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }

    if ((Test-Path $Path) -and (-not $Force)) {
        Write-Host "SKIP existing file: $Path" -ForegroundColor Yellow
        return
    }

    Set-Content -Path $Path -Value $Content -Encoding UTF8
    Write-Host "WRITE: $Path" -ForegroundColor Green
}

Write-Host ""
Write-Host "============================================================"
Write-Host " PlantProcess IQ - Website Frontend Scaffold"
Write-Host " Target: $WebsiteRoot"
Write-Host "============================================================"
Write-Host ""

New-Item -ItemType Directory -Force -Path $WebsiteRoot | Out-Null

# ------------------------------------------------------------
# package.json
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "package.json") -Content @'
{
  "name": "plantprocess-website",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview --host 0.0.0.0",
    "check:ts": "tsc -b --verbose",
    "lint": "echo \"Website lint placeholder - add ESLint in next polishing step\"",
    "test": "echo \"Website tests placeholder - add Vitest after content stabilizes\"",
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
# index.html
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "index.html") -Content @'
<!doctype html>
<html lang="en" data-theme="dark">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta
      name="description"
      content="PlantProcess IQ connects fragmented plant data into one read-only process-to-quality intelligence layer for manufacturing plants."
    />
    <title>PlantProcess IQ | Process-to-quality intelligence</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
'@

# ------------------------------------------------------------
# TypeScript / Vite config
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "vite.config.ts") -Content @'
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

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "tsconfig.json") -Content @'
{
  "files": [],
  "references": [
    { "path": "./tsconfig.app.json" },
    { "path": "./tsconfig.node.json" }
  ]
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "tsconfig.app.json") -Content @'
{
  "compilerOptions": {
    "tsBuildInfoFile": "./node_modules/.tmp/tsconfig.app.tsbuildinfo",
    "target": "ES2022",
    "useDefineForClassFields": true,
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "allowJs": false,
    "skipLibCheck": true,
    "esModuleInterop": true,
    "allowSyntheticDefaultImports": true,
    "strict": true,
    "forceConsistentCasingInFileNames": true,
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

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "tsconfig.node.json") -Content @'
{
  "compilerOptions": {
    "tsBuildInfoFile": "./node_modules/.tmp/tsconfig.node.tsbuildinfo",
    "target": "ES2023",
    "lib": ["ES2023"],
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "types": ["node"],
    "allowSyntheticDefaultImports": true,
    "skipLibCheck": true,
    "strict": true,
    "noEmit": true
  },
  "include": ["vite.config.ts"]
}
'@

# ------------------------------------------------------------
# Env
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot ".env.example") -Content @'
# Public website API base URL
# Local:
# VITE_WEBSITE_API_BASE_URL=http://localhost:5080
#
# Demo server:
# VITE_WEBSITE_API_BASE_URL=https://website-api.plantprocessiq.com

VITE_WEBSITE_API_BASE_URL=http://localhost:5080
'@

# ------------------------------------------------------------
# Docker
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "Dockerfile") -Content @'
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

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "nginx.conf") -Content @'
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
        return 200 "plantprocess-website-ok\n";
        add_header Content-Type text/plain;
    }
}
'@

# ------------------------------------------------------------
# Source root
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\main.tsx") -Content @'
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

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\App.tsx") -Content @'
import { AppRoutes } from "./routes/AppRoutes";

export function App() {
  return <AppRoutes />;
}
'@

# ------------------------------------------------------------
# Data
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\data\siteContent.ts") -Content @'
export const brand = {
  productName: "PlantProcess IQ",
  shortName: "PPIQ",
  primaryTagline: "Connect Your Plant Data. Understand Your Process.",
  secondaryTagline: "Process-to-quality intelligence for manufacturing plants.",
  promise:
    "Connect plant data. Discover quality drivers. Score risk earlier. Explain suspected contributors. Act with evidence."
};

export const navigationItems = [
  { label: "Product", href: "/product" },
  { label: "Industries", href: "/industries" },
  { label: "Pricing", href: "/pricing" },
  { label: "Security", href: "/security" },
  { label: "Demo", href: "/demo" },
  { label: "Contact", href: "/contact" }
];

export const productPillars = [
  {
    title: "Connect fragmented plant data",
    description:
      "Bring MES, Level 2, inspection, lab, ERP, Excel and CSV data into one controlled read-only intelligence layer."
  },
  {
    title: "Map data into a generic model",
    description:
      "Use schema configuration, source datasets, mappings and jobs to normalize different plant structures into one canonical layer."
  },
  {
    title: "Investigate process-to-quality signals",
    description:
      "Explore defects, process parameters, genealogy, risk indicators, data-quality issues and suspected contributors."
  },
  {
    title: "Prepare evidence-based reports",
    description:
      "Generate customer-grade investigation/readiness outputs that help engineers and managers act with confidence."
  }
];

export const industries = [
  "Steel",
  "Aluminum",
  "Paper",
  "Automotive",
  "Tire",
  "Pharma",
  "Food",
  "Chemicals"
];

export const pricingTiers = [
  {
    name: "Light",
    price: "€1.2k–€2k / year",
    description: "For one engineer validating the concept with limited data sources.",
    features: [
      "1 user",
      "CSV / Excel starter data",
      "2 custom pages",
      "Basic dashboards",
      "Manual investigation workflow"
    ]
  },
  {
    name: "Pro",
    price: "€8k–€12k / year",
    description: "For engineering teams preparing real plant data discovery.",
    features: [
      "Up to 5 users",
      "CSV, Excel, PostgreSQL",
      "More dashboards and widgets",
      "Risk and data-quality areas",
      "Demo/pilot-ready reporting"
    ],
    highlighted: true
  },
  {
    name: "Pro Plus",
    price: "€25k–€40k / year",
    description: "For advanced multi-area plant intelligence pilots.",
    features: [
      "Up to 20 users",
      "Unlimited pages",
      "Advanced correlation jobs",
      "Priority implementation support",
      "Expanded reporting and configuration"
    ]
  },
  {
    name: "Enterprise",
    price: "Custom",
    description: "For larger deployments requiring strict security, audit and integration scope.",
    features: [
      "Custom users",
      "Custom deployment model",
      "Security and RBAC hardening",
      "Connector roadmap planning",
      "Enterprise support model"
    ]
  }
];
'@

# ------------------------------------------------------------
# API
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\api\websiteApi.ts") -Content @'
export type DemoRequestPayload = {
  name: string;
  email: string;
  company?: string;
  message?: string;
};

const websiteApiBaseUrl =
  import.meta.env.VITE_WEBSITE_API_BASE_URL?.replace(/\/$/, "") ?? "";

export async function submitDemoRequest(payload: DemoRequestPayload) {
  if (!websiteApiBaseUrl) {
    throw new Error("VITE_WEBSITE_API_BASE_URL is not configured.");
  }

  const response = await fetch(`${websiteApiBaseUrl}/website/demo-requests`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(payload)
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Demo request failed with status ${response.status}`);
  }

  return response.json();
}
'@

# ------------------------------------------------------------
# Routes / Layout
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\routes\AppRoutes.tsx") -Content @'
import { Navigate, Route, Routes } from "react-router-dom";
import { PublicLayout } from "../layout/PublicLayout";
import { ContactPage } from "../pages/ContactPage";
import { DemoPage } from "../pages/DemoPage";
import { HomePage } from "../pages/HomePage";
import { IndustriesPage } from "../pages/IndustriesPage";
import { PricingPage } from "../pages/PricingPage";
import { ProductPage } from "../pages/ProductPage";
import { SecurityPage } from "../pages/SecurityPage";

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<PublicLayout />}>
        <Route index element={<HomePage />} />
        <Route path="/product" element={<ProductPage />} />
        <Route path="/industries" element={<IndustriesPage />} />
        <Route path="/pricing" element={<PricingPage />} />
        <Route path="/security" element={<SecurityPage />} />
        <Route path="/demo" element={<DemoPage />} />
        <Route path="/contact" element={<ContactPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\layout\PublicLayout.tsx") -Content @'
import { Menu, Network, X } from "lucide-react";
import { useState } from "react";
import { Link, NavLink, Outlet } from "react-router-dom";
import { navigationItems } from "../data/siteContent";

export function PublicLayout() {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="site-shell">
      <header className="site-header">
        <Link to="/" className="brand-link" aria-label="PlantProcess IQ home">
          <span className="brand-mark">
            <Network size={22} />
          </span>
          <span className="brand-text">
            PlantProcess <strong>IQ</strong>
          </span>
        </Link>

        <nav className="desktop-nav" aria-label="Main navigation">
          {navigationItems.map((item) => (
            <NavLink key={item.href} to={item.href}>
              {item.label}
            </NavLink>
          ))}
        </nav>

        <Link to="/demo" className="header-cta">
          Request Demo
        </Link>

        <button
          className="mobile-menu-button"
          type="button"
          aria-label="Toggle navigation"
          onClick={() => setIsOpen((current) => !current)}
        >
          {isOpen ? <X size={22} /> : <Menu size={22} />}
        </button>
      </header>

      {isOpen && (
        <nav className="mobile-nav" aria-label="Mobile navigation">
          {navigationItems.map((item) => (
            <NavLink key={item.href} to={item.href} onClick={() => setIsOpen(false)}>
              {item.label}
            </NavLink>
          ))}
        </nav>
      )}

      <main>
        <Outlet />
      </main>

      <footer className="site-footer">
        <div>
          <strong>PlantProcess IQ</strong>
          <span>Process-to-quality intelligence for manufacturing plants.</span>
        </div>
        <div className="footer-links">
          <Link to="/security">Security</Link>
          <Link to="/pricing">Pricing</Link>
          <Link to="/contact">Contact</Link>
        </div>
      </footer>
    </div>
  );
}
'@

# ------------------------------------------------------------
# Components
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\components\SectionHeader.tsx") -Content @'
type SectionHeaderProps = {
  eyebrow: string;
  title: string;
  description?: string;
};

export function SectionHeader({ eyebrow, title, description }: SectionHeaderProps) {
  return (
    <div className="section-header">
      <span className="eyebrow">{eyebrow}</span>
      <h2>{title}</h2>
      {description && <p>{description}</p>}
    </div>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\components\FeatureCard.tsx") -Content @'
import type { ReactNode } from "react";

type FeatureCardProps = {
  icon?: ReactNode;
  title: string;
  description: string;
};

export function FeatureCard({ icon, title, description }: FeatureCardProps) {
  return (
    <article className="feature-card">
      {icon && <div className="feature-icon">{icon}</div>}
      <h3>{title}</h3>
      <p>{description}</p>
    </article>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\components\PricingCard.tsx") -Content @'
type PricingCardProps = {
  name: string;
  price: string;
  description: string;
  features: string[];
  highlighted?: boolean;
};

export function PricingCard({
  name,
  price,
  description,
  features,
  highlighted
}: PricingCardProps) {
  return (
    <article className={highlighted ? "pricing-card pricing-card-highlighted" : "pricing-card"}>
      {highlighted && <span className="tier-badge">Recommended now</span>}
      <h3>{name}</h3>
      <strong>{price}</strong>
      <p>{description}</p>
      <ul>
        {features.map((feature) => (
          <li key={feature}>{feature}</li>
        ))}
      </ul>
    </article>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\components\CtaBand.tsx") -Content @'
import { Link } from "react-router-dom";

type CtaBandProps = {
  title: string;
  description: string;
  primaryLabel?: string;
  primaryHref?: string;
};

export function CtaBand({
  title,
  description,
  primaryLabel = "Request Founder Demo",
  primaryHref = "/demo"
}: CtaBandProps) {
  return (
    <section className="cta-band">
      <div>
        <span className="eyebrow">Next step</span>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>
      <Link to={primaryHref} className="button button-primary">
        {primaryLabel}
      </Link>
    </section>
  );
}
'@

# ------------------------------------------------------------
# Pages
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\pages\HomePage.tsx") -Content @'
import {
  BarChart3,
  DatabaseZap,
  FileText,
  GitBranch,
  ShieldCheck,
  Workflow
} from "lucide-react";
import { Link } from "react-router-dom";
import { CtaBand } from "../components/CtaBand";
import { FeatureCard } from "../components/FeatureCard";
import { SectionHeader } from "../components/SectionHeader";
import { brand, productPillars } from "../data/siteContent";

const icons = [
  <DatabaseZap size={24} />,
  <Workflow size={24} />,
  <GitBranch size={24} />,
  <FileText size={24} />
];

export function HomePage() {
  return (
    <>
      <section className="hero-section">
        <div className="hero-copy">
          <span className="status-pill">
            <ShieldCheck size={16} />
            Read-only manufacturing intelligence layer
          </span>
          <h1>
            Connect your plant data. <span>Understand your process.</span>
          </h1>
          <p>{brand.promise}</p>
          <div className="hero-actions">
            <Link className="button button-primary" to="/demo">
              Request Founder Demo
            </Link>
            <Link className="button button-secondary" to="/product">
              Explore Product
            </Link>
          </div>
        </div>

        <div className="hero-panel">
          <div className="panel-topline">
            <span>PlantProcess IQ</span>
            <span>Demo stack</span>
          </div>
          <div className="signal-grid">
            <div>
              <strong>Data sources</strong>
              <span>MES · L2 · Inspection · Lab · ERP · Excel</span>
            </div>
            <div>
              <strong>Core layer</strong>
              <span>Staging · Mapping · Genealogy · Jobs</span>
            </div>
            <div>
              <strong>Insights</strong>
              <span>Dashboards · Risk · Quality · Reports</span>
            </div>
          </div>
          <div className="mock-chart" aria-label="Decorative process signal chart">
            <BarChart3 size={92} />
          </div>
        </div>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="Product foundation"
          title="Built for fragmented plant reality"
          description="Every plant has different data structures, process routes, devices, defect logic and KPI priorities. PlantProcess IQ is designed as a generic configuration-driven layer."
        />
        <div className="feature-grid">
          {productPillars.map((pillar, index) => (
            <FeatureCard
              key={pillar.title}
              icon={icons[index]}
              title={pillar.title}
              description={pillar.description}
            />
          ))}
        </div>
      </section>

      <CtaBand
        title="Prepare your first serious customer demo."
        description="Use the website, app demo, data-quality workflow, risk dashboard and PDF report as one commercial golden path."
      />
    </>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\pages\ProductPage.tsx") -Content @'
import { Activity, Database, FileSearch, Gauge, Settings2, Shield } from "lucide-react";
import { CtaBand } from "../components/CtaBand";
import { FeatureCard } from "../components/FeatureCard";
import { SectionHeader } from "../components/SectionHeader";

export function ProductPage() {
  return (
    <>
      <section className="page-hero">
        <span className="eyebrow">Product</span>
        <h1>One intelligence layer above your plant systems.</h1>
        <p>
          PlantProcess IQ does not replace MES, SCADA, Level 2, ERP or BI. It connects
          fragmented plant data into a read-only layer for investigation, correlation,
          dashboards and evidence-based reporting.
        </p>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="Capabilities"
          title="From source data to evidence"
          description="The product path is intentionally simple: connect, stage, map, monitor, investigate and report."
        />
        <div className="feature-grid">
          <FeatureCard icon={<Database size={24} />} title="Source connections" description="Connect CSV, Excel and PostgreSQL now, with MSSQL, Oracle and MySQL planned after connector validation." />
          <FeatureCard icon={<Settings2 size={24} />} title="Schema configuration" description="Map different plant tables and source structures into the generic PlantProcess IQ canonical model." />
          <FeatureCard icon={<Activity size={24} />} title="Jobs monitor" description="Track import jobs, mapping jobs, data-quality scans, risk scoring and future correlation jobs." />
          <FeatureCard icon={<Gauge size={24} />} title="Dashboards" description="Use configurable pages, widgets, filters and plant KPIs to support process and quality investigation." />
          <FeatureCard icon={<FileSearch size={24} />} title="Material investigation" description="Trace material genealogy, process steps, observations, quality events and suspected contributors." />
          <FeatureCard icon={<Shield size={24} />} title="Read-only safety" description="Keep PlantProcess IQ as an evidence layer, not a writeback automation or control-system replacement." />
        </div>
      </section>

      <CtaBand
        title="Use the product story in demos."
        description="Show the customer how messy plant data becomes a controlled investigation workflow."
      />
    </>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\pages\IndustriesPage.tsx") -Content @'
import { CtaBand } from "../components/CtaBand";
import { SectionHeader } from "../components/SectionHeader";
import { industries } from "../data/siteContent";

export function IndustriesPage() {
  return (
    <>
      <section className="page-hero">
        <span className="eyebrow">Industries</span>
        <h1>Generic by design. Steel is the first proof, not the limit.</h1>
        <p>
          The platform uses generic concepts like material units, equipment, operations,
          process parameters, events, defects, genealogy and KPIs. Industry-specific details
          stay configurable.
        </p>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="Target sectors"
          title="Manufacturing plants with process-to-quality complexity"
          description="The same foundation can support different products, routes, inspection devices and quality logic."
        />
        <div className="industry-grid">
          {industries.map((industry) => (
            <article className="industry-card" key={industry}>
              {industry}
            </article>
          ))}
        </div>
      </section>

      <CtaBand
        title="Start with one strong pilot story."
        description="Use steel or another familiar process as the first proof, while keeping the model generic for other industries."
      />
    </>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\pages\PricingPage.tsx") -Content @'
import { CtaBand } from "../components/CtaBand";
import { PricingCard } from "../components/PricingCard";
import { SectionHeader } from "../components/SectionHeader";
import { pricingTiers } from "../data/siteContent";

export function PricingPage() {
  return (
    <>
      <section className="page-hero">
        <span className="eyebrow">Pricing</span>
        <h1>Commercial packaging for early discovery, demos and pilots.</h1>
        <p>
          Pricing is designed to compete with manual SQL/Excel investigation and generic BI,
          while staying honest about current product maturity and connector readiness.
        </p>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="License architecture"
          title="Start small, grow into plant-wide intelligence"
          description="The customer-facing pricing keeps the model simple: Light, Pro, Pro Plus and Enterprise."
        />
        <div className="pricing-grid">
          {pricingTiers.map((tier) => (
            <PricingCard key={tier.name} {...tier} />
          ))}
        </div>
      </section>

      <CtaBand
        title="Use pricing as a conversation starter."
        description="For now, the best target is founder-led discovery and demo-based selling, not automated checkout."
      />
    </>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\pages\SecurityPage.tsx") -Content @'
import { CheckCircle2, Lock, ShieldAlert, UserCheck } from "lucide-react";
import { CtaBand } from "../components/CtaBand";
import { FeatureCard } from "../components/FeatureCard";
import { SectionHeader } from "../components/SectionHeader";

export function SecurityPage() {
  return (
    <>
      <section className="page-hero">
        <span className="eyebrow">Security</span>
        <h1>Read-only by positioning. Production hardening before paid pilots.</h1>
        <p>
          PlantProcess IQ should be introduced as a safe intelligence layer. It must not
          claim production-grade deployment until security, RBAC, audit logging and connector
          handling are validated.
        </p>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="Trust model"
          title="The security story must be simple and honest"
          description="Customers need to hear that the platform respects existing plant systems and does not replace or control them."
        />
        <div className="feature-grid">
          <FeatureCard icon={<Lock size={24} />} title="Read-only integration" description="No writeback to MES, Level 2, SCADA, PLC or ERP in the current commercial positioning." />
          <FeatureCard icon={<UserCheck size={24} />} title="Role-based access" description="Admin, engineer, data manager and viewer access models can support pilot governance." />
          <FeatureCard icon={<ShieldAlert size={24} />} title="Production gate" description="No paid pilot until production secrets, CORS, auth, audit and data handling are validated." />
          <FeatureCard icon={<CheckCircle2 size={24} />} title="Connector honesty" description="Only tested connectors are marketed as available; planned connectors remain clearly marked as planned." />
        </div>
      </section>

      <CtaBand
        title="Do not oversell security before the gate."
        description="Use this page to build trust through clarity, not hype."
      />
    </>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\pages\DemoPage.tsx") -Content @'
import { useState } from "react";
import { submitDemoRequest } from "../api/websiteApi";

type FormState = {
  name: string;
  email: string;
  company: string;
  message: string;
};

const initialState: FormState = {
  name: "",
  email: "",
  company: "",
  message: ""
};

export function DemoPage() {
  const [form, setForm] = useState<FormState>(initialState);
  const [status, setStatus] = useState<"idle" | "submitting" | "success" | "error">("idle");
  const [error, setError] = useState("");

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setStatus("submitting");
    setError("");

    try {
      await submitDemoRequest({
        name: form.name,
        email: form.email,
        company: form.company || undefined,
        message: form.message || undefined
      });

      setStatus("success");
      setForm(initialState);
    } catch (exception) {
      setStatus("error");
      setError(exception instanceof Error ? exception.message : "Demo request failed.");
    }
  }

  return (
    <>
      <section className="page-hero">
        <span className="eyebrow">Founder demo</span>
        <h1>Request a focused PlantProcess IQ walkthrough.</h1>
        <p>
          The ideal first demo is a 10–15 minute golden path: messy plant data, controlled
          mapping, dashboard, risk/data-quality signal and customer-grade report.
        </p>
      </section>

      <section className="form-section">
        <form className="demo-form" onSubmit={handleSubmit}>
          <label>
            Name
            <input
              required
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
              placeholder="Your name"
            />
          </label>

          <label>
            Email
            <input
              required
              type="email"
              value={form.email}
              onChange={(event) => setForm({ ...form, email: event.target.value })}
              placeholder="name@company.com"
            />
          </label>

          <label>
            Company
            <input
              value={form.company}
              onChange={(event) => setForm({ ...form, company: event.target.value })}
              placeholder="Company / plant"
            />
          </label>

          <label>
            Message
            <textarea
              value={form.message}
              onChange={(event) => setForm({ ...form, message: event.target.value })}
              placeholder="Tell us about your plant data, quality issue, or demo interest."
            />
          </label>

          <button className="button button-primary" type="submit" disabled={status === "submitting"}>
            {status === "submitting" ? "Sending..." : "Request Demo"}
          </button>

          {status === "success" && (
            <p className="form-success">Demo request received. Thank you.</p>
          )}

          {status === "error" && (
            <p className="form-error">{error}</p>
          )}
        </form>
      </section>
    </>
  );
}
'@

Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\pages\ContactPage.tsx") -Content @'
import { Mail, MapPin } from "lucide-react";
import { FeatureCard } from "../components/FeatureCard";
import { SectionHeader } from "../components/SectionHeader";

export function ContactPage() {
  return (
    <>
      <section className="page-hero">
        <span className="eyebrow">Contact</span>
        <h1>Talk about plant data, quality investigation and demo readiness.</h1>
        <p>
          PlantProcess IQ is currently best positioned for founder-led discovery,
          engineering validation and controlled pilot preparation.
        </p>
      </section>

      <section className="content-section">
        <SectionHeader
          eyebrow="Contact"
          title="Start with a focused technical conversation"
          description="The best first discussion is about your plant systems, data sources, quality pain points and investigation workflow."
        />
        <div className="feature-grid two-columns">
          <FeatureCard icon={<Mail size={24} />} title="Email" description="Use the demo request form or connect directly once the public domain is ready." />
          <FeatureCard icon={<MapPin size={24} />} title="Location" description="Founder based in Düsseldorf, Germany. Suitable for EU/MENA industrial discovery conversations." />
        </div>
      </section>
    </>
  );
}
'@

# ------------------------------------------------------------
# Styles
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "src\styles\global.css") -Content @'
:root {
  --color-bg: #050B18;
  --color-panel: #0B1730;
  --color-panel-2: #102A43;
  --color-blue: #0A84FF;
  --color-cyan: #00D4FF;
  --color-enterprise-blue: #2F80ED;
  --color-green: #2CE6A2;
  --color-warning: #FFB020;
  --color-critical: #FF4D6D;
  --color-text: #EAF6FF;
  --color-muted: #8EA7C1;
  --border-cyan: rgba(0, 212, 255, 0.22);
  --border-muted: rgba(142, 167, 193, 0.16);
  --shadow-glow: 0 0 42px rgba(0, 212, 255, 0.16);
  --shadow-panel: 0 24px 80px rgba(0, 0, 0, 0.36);
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  color: var(--color-text);
  background: var(--color-bg);
  font-synthesis: none;
  text-rendering: optimizeLegibility;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

* {
  box-sizing: border-box;
}

html {
  background: var(--color-bg);
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

button,
input,
textarea {
  font: inherit;
}

.site-shell {
  width: min(1180px, calc(100% - 40px));
  margin: 0 auto;
}

.site-header {
  position: sticky;
  top: 0;
  z-index: 20;
  display: flex;
  align-items: center;
  gap: 20px;
  padding: 18px 0;
  backdrop-filter: blur(18px);
}

.brand-link {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  min-width: max-content;
  text-decoration: none;
}

.brand-mark {
  display: grid;
  place-items: center;
  width: 42px;
  height: 42px;
  border: 1px solid var(--border-cyan);
  border-radius: 14px;
  background: linear-gradient(135deg, rgba(0, 212, 255, 0.18), rgba(10, 132, 255, 0.14));
  color: var(--color-cyan);
  box-shadow: var(--shadow-glow);
}

.brand-text {
  font-size: 19px;
  font-weight: 800;
  letter-spacing: -0.03em;
}

.brand-text strong {
  color: var(--color-cyan);
}

.desktop-nav {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  flex: 1;
}

.desktop-nav a,
.mobile-nav a {
  text-decoration: none;
  color: var(--color-muted);
  font-size: 14px;
  font-weight: 700;
  padding: 10px 12px;
  border-radius: 10px;
}

.desktop-nav a.active,
.desktop-nav a:hover,
.mobile-nav a.active,
.mobile-nav a:hover {
  color: var(--color-text);
  background: rgba(0, 212, 255, 0.08);
}

.header-cta {
  display: inline-flex;
  align-items: center;
  min-width: max-content;
  padding: 10px 14px;
  border-radius: 10px;
  background: var(--color-blue);
  color: white;
  text-decoration: none;
  font-size: 14px;
  font-weight: 800;
  box-shadow: 0 0 28px rgba(10, 132, 255, 0.28);
}

.mobile-menu-button {
  display: none;
  align-items: center;
  justify-content: center;
  color: var(--color-text);
  background: rgba(11, 23, 48, 0.85);
  border: 1px solid var(--border-muted);
  border-radius: 10px;
  width: 42px;
  height: 42px;
}

.mobile-nav {
  display: grid;
  gap: 8px;
  padding: 14px;
  margin-bottom: 18px;
  border: 1px solid var(--border-muted);
  border-radius: 18px;
  background: rgba(11, 23, 48, 0.96);
}

.hero-section,
.page-hero {
  padding: 86px 0 70px;
}

.hero-section {
  display: grid;
  grid-template-columns: 1.08fr 0.92fr;
  gap: 42px;
  align-items: center;
}

.hero-copy h1,
.page-hero h1 {
  max-width: 900px;
  margin: 0 0 22px;
  font-size: clamp(44px, 6vw, 78px);
  line-height: 0.98;
  letter-spacing: -0.075em;
}

.hero-copy h1 span,
.page-hero h1 span {
  color: var(--color-cyan);
}

.hero-copy p,
.page-hero p,
.section-header p,
.feature-card p,
.pricing-card p,
.cta-band p,
.site-footer span {
  color: var(--color-muted);
  line-height: 1.7;
}

.hero-copy p,
.page-hero p {
  max-width: 760px;
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
  color: var(--color-green);
  border: 1px solid rgba(44, 230, 162, 0.3);
  background: rgba(44, 230, 162, 0.08);
  border-radius: 999px;
  padding: 8px 12px;
  font-size: 13px;
  font-weight: 800;
  letter-spacing: 0.01em;
}

.eyebrow {
  color: var(--color-cyan);
  border-color: rgba(0, 212, 255, 0.28);
  background: rgba(0, 212, 255, 0.08);
}

.hero-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
}

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 46px;
  padding: 12px 18px;
  border-radius: 12px;
  border: 1px solid transparent;
  text-decoration: none;
  font-weight: 850;
  cursor: pointer;
}

.button-primary {
  color: white;
  background: var(--color-blue);
  box-shadow: 0 0 32px rgba(10, 132, 255, 0.28);
}

.button-secondary {
  color: var(--color-text);
  border-color: var(--border-cyan);
  background: rgba(11, 23, 48, 0.7);
}

.hero-panel,
.feature-card,
.pricing-card,
.cta-band,
.demo-form,
.industry-card {
  border: 1px solid var(--border-muted);
  background: linear-gradient(180deg, rgba(11, 23, 48, 0.92), rgba(8, 18, 38, 0.92));
  border-radius: 24px;
  box-shadow: var(--shadow-panel);
}

.hero-panel {
  padding: 24px;
}

.panel-topline {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  color: var(--color-muted);
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
  color: var(--color-muted);
  font-size: 14px;
}

.mock-chart {
  display: grid;
  place-items: center;
  min-height: 220px;
  margin-top: 16px;
  color: var(--color-cyan);
  border-radius: 18px;
  background:
    linear-gradient(rgba(0, 212, 255, 0.06) 1px, transparent 1px),
    linear-gradient(90deg, rgba(0, 212, 255, 0.06) 1px, transparent 1px),
    rgba(0, 212, 255, 0.035);
  background-size: 28px 28px;
}

.content-section {
  padding: 44px 0;
}

.section-header {
  max-width: 820px;
  margin-bottom: 24px;
}

.section-header h2,
.cta-band h2 {
  margin: 0 0 14px;
  font-size: clamp(30px, 4vw, 48px);
  line-height: 1.04;
  letter-spacing: -0.055em;
}

.section-header p,
.cta-band p {
  margin: 0;
  font-size: 17px;
}

.feature-grid,
.pricing-grid,
.industry-grid {
  display: grid;
  gap: 18px;
}

.feature-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.feature-grid.two-columns {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.pricing-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.industry-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.feature-card,
.pricing-card,
.industry-card {
  padding: 22px;
}

.feature-card:hover,
.pricing-card:hover,
.industry-card:hover {
  border-color: var(--border-cyan);
  box-shadow: var(--shadow-glow);
}

.feature-icon {
  display: grid;
  place-items: center;
  width: 46px;
  height: 46px;
  color: var(--color-cyan);
  border: 1px solid var(--border-cyan);
  border-radius: 15px;
  background: rgba(0, 212, 255, 0.08);
  margin-bottom: 18px;
}

.feature-card h3,
.pricing-card h3 {
  margin: 0 0 10px;
  font-size: 19px;
}

.feature-card p,
.pricing-card p {
  margin: 0;
  font-size: 15px;
}

.pricing-card {
  position: relative;
}

.pricing-card-highlighted {
  border-color: rgba(0, 212, 255, 0.55);
  background: linear-gradient(180deg, rgba(10, 132, 255, 0.16), rgba(11, 23, 48, 0.94));
}

.pricing-card strong {
  display: block;
  margin-bottom: 12px;
  color: var(--color-cyan);
  font-size: 24px;
}

.pricing-card ul {
  padding-left: 18px;
  color: var(--color-muted);
  line-height: 1.8;
}

.tier-badge {
  margin-bottom: 14px;
}

.industry-card {
  min-height: 120px;
  display: flex;
  align-items: end;
  color: var(--color-text);
  font-weight: 850;
  font-size: 19px;
}

.cta-band {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  margin: 48px 0 70px;
  padding: 30px;
  border-color: rgba(0, 212, 255, 0.26);
}

.form-section {
  max-width: 760px;
  padding-bottom: 70px;
}

.demo-form {
  display: grid;
  gap: 16px;
  padding: 26px;
}

.demo-form label {
  display: grid;
  gap: 8px;
  color: var(--color-text);
  font-weight: 750;
}

.demo-form input,
.demo-form textarea {
  width: 100%;
  color: var(--color-text);
  background: rgba(5, 11, 24, 0.78);
  border: 1px solid var(--border-muted);
  border-radius: 12px;
  padding: 13px 14px;
  outline: none;
}

.demo-form textarea {
  min-height: 140px;
  resize: vertical;
}

.demo-form input:focus,
.demo-form textarea:focus {
  border-color: var(--color-cyan);
  box-shadow: 0 0 0 3px rgba(0, 212, 255, 0.12);
}

.form-success {
  color: var(--color-green);
}

.form-error {
  color: var(--color-critical);
}

.site-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  padding: 34px 0 44px;
  border-top: 1px solid var(--border-muted);
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
  color: var(--color-muted);
  text-decoration: none;
}

.footer-links a:hover {
  color: var(--color-cyan);
}

@media (max-width: 1050px) {
  .feature-grid,
  .pricing-grid,
  .industry-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .hero-section {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .site-shell {
    width: min(100% - 28px, 1180px);
  }

  .desktop-nav,
  .header-cta {
    display: none;
  }

  .mobile-menu-button {
    display: inline-flex;
    margin-left: auto;
  }

  .hero-section,
  .page-hero {
    padding: 52px 0 42px;
  }

  .feature-grid,
  .feature-grid.two-columns,
  .pricing-grid,
  .industry-grid {
    grid-template-columns: 1fr;
  }

  .cta-band,
  .site-footer {
    align-items: flex-start;
    flex-direction: column;
  }
}
'@

# ------------------------------------------------------------
# README
# ------------------------------------------------------------
Write-ScaffoldFile -Path (Join-Path $WebsiteRoot "README.md") -Content @'
# PlantProcess IQ Public Website

This is the public marketing website frontend for PlantProcess IQ.

## Purpose

This website supports:

- Market positioning
- Product explanation
- Pricing/license communication
- Demo requests
- Security/read-only positioning
- Customer discovery

## Local development

```powershell
cd Website\PlantProcess.Website
npm install
npm run dev