import { NavLink, Outlet } from "react-router-dom";
import {
  AlertTriangle,
  BarChart3,
  BrainCircuit,
  Cpu,
  DatabaseZap,
  Factory,
  GitBranch,
  LayoutDashboard,
  Moon,
  Network,
  PlayCircle,
  Radar,
  Search,
  Settings2,
  ShieldCheck,
  Sparkles,
  Sun,
} from "lucide-react";

import { plantProcessApi } from "../api/plantProcessApi";
import { usePlantProcessTheme } from "../state/ThemeContext";
import { AppCommandHeader } from "./brand/AppCommandHeader";
import { ProductBrand } from "./brand/ProductBrand";
import { SOUBrand } from "./brand/SOUBrand";
import { DemoModeControl } from "./demo/DemoModeControl";

const navItems = [
  {
    to: "/dashboard",
    label: "Command Dashboard",
    description: "Interactive intelligence workspace",
    icon: LayoutDashboard,
  },
  {
    to: "/materials",
    label: "Material Investigation",
    description: "Genealogy, quality and risk drilldown",
    icon: Search,
  },
  {
    to: "/risk",
    label: "Risk Intelligence",
    description: "Quality risk score and contributors",
    icon: ShieldCheck,
  },
  {
    to: "/data-quality",
    label: "Data Quality",
    description: "Readiness and validation findings",
    icon: AlertTriangle,
  },
  {
    to: "/correlations",
    label: "Correlations",
    description: "Process-to-quality analytics",
    icon: GitBranch,
  },
  {
    to: "/ml-readiness",
    label: "ML Readiness",
    description: "Labels, feature vectors and training gates",
    icon: BrainCircuit,
  },
  {
    to: "/demo-lifecycle",
    label: "Demo Lifecycle",
    description: "Connector to ML result workflow",
    icon: PlayCircle,
  },
  {
    to: "/admin-preview",
    label: "Admin Preview",
    description: "License, roles, ML, scripts, report",
    icon: ShieldCheck,
  },
  {
    to: "/admin",
    label: "Administrator",
    description: "DB config, schema mapping and jobs",
    icon: Settings2,
  },
  {
    to: "/brand",
    label: "Brand",
    description: "Identity, positioning and proof assets",
    icon: Sparkles,
  },
];

function getRuntimeEnvironment(): "Demo" | "Development" | "Staging" | "Production" {
  const mode = import.meta.env.MODE?.toLowerCase();

  if (mode === "production") {
    return "Demo";
  }

  if (mode === "development") {
    return "Development";
  }

  if (mode === "staging") {
    return "Staging";
  }

  return "Demo";
}

export function AppLayout() {
  const { theme, isDark, toggleTheme } = usePlantProcessTheme();
  const runtimeEnvironment = getRuntimeEnvironment();

  return (
    <div className="app-shell app-shell--premium">
      <aside className="sidebar sidebar--branded">
        <div className="sidebar-glow" />

        <div className="sidebar-company-brand" aria-label="SOU company identity">
          <SOUBrand compact />
          <div className="sidebar-company-brand__copy">
            <span className="sidebar-company-brand__name">SOU</span>
            <span className="sidebar-company-brand__tagline">
              Manufacturing Intelligence
            </span>
          </div>
        </div>

        <div className="sidebar-product-brand" aria-label="PlantProcess IQ product identity">
          <ProductBrand showSubtitle />
        </div>

        <div className="sidebar-status-card sidebar-status-card--premium">
          <div className="status-orb">
            <Radar size={18} />
          </div>

          <div>
            <strong>Digital Plant Layer</strong>
            <span>Canonical analytics model online</span>
          </div>
        </div>

        <div className="sidebar-context-strip" aria-label="Application context">
          <div className="sidebar-context-strip__item">
            <Factory size={15} />
            <span>Generic Plant Model</span>
          </div>

          <div className="sidebar-context-strip__item">
            <ShieldCheck size={15} />
            <span>Demo License</span>
          </div>
        </div>

        <nav className="nav" aria-label="PlantProcess IQ navigation">
          {navItems.map((item) => {
            const Icon = item.icon;

            return (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  isActive ? "nav-link active" : "nav-link"
                }
              >
                <div className="nav-icon">
                  <Icon size={19} />
                </div>

                <div className="nav-copy">
                  <span>{item.label}</span>
                  <small>{item.description}</small>
                </div>
              </NavLink>
            );
          })}
        </nav>

        <div className="sidebar-bottom">
          <div className="sidebar-metric-card">
            <DatabaseZap size={18} />

            <div>
              <strong>Backend API</strong>
              <span>{plantProcessApi.apiBaseUrl}</span>
            </div>
          </div>

          <div className="sidebar-metric-card">
            <Network size={18} />

            <div>
              <strong>Mode</strong>
              <span>Phase 8–10 Interactive MVP</span>
            </div>
          </div>
        </div>
      </aside>

      <main className="main main--command">
        <AppCommandHeader
          licenseTier="Demo"
          environment={runtimeEnvironment}
          plantName="Demo Plant"
          userName="Admin"
        />

        <section className="topbar topbar--premium" aria-label="Application overview">
          <div className="topbar-left">
            <div className="topbar-kicker">
              <Cpu size={15} />
              Process-to-Quality Intelligence Platform
            </div>

            <h2>Industrial Analytics Command Center</h2>

            <p>
              Digital plant data, genealogy, process history, quality events,
              risk scoring and correlation intelligence in one evidence-based
              manufacturing workspace.
            </p>
          </div>

          <div className="topbar-actions">
            <DemoModeControl />
            
            <button
              className="theme-toggle-button"
              onClick={toggleTheme}
              type="button"
              title={`Switch to ${isDark ? "light" : "dark"} mode`}
            >
              <span className="theme-toggle-button__icon">
                {isDark ? <Sun size={16} /> : <Moon size={16} />}
              </span>

              <span className="theme-toggle-button__text">
                {isDark ? "Morning Mode" : "Night Mode"}
              </span>

              <span className="theme-toggle-button__state">
                {theme}
              </span>
            </button>

            <div className="topbar-badge">
              <Sparkles size={16} />
              Rule-based intelligence layer
            </div>

            <div className="topbar-badge topbar-badge--strong">
              <BarChart3 size={16} />
              Interactive workspace
            </div>
          </div>
        </section>

        <section className="app-page-workspace">
          <Outlet />
        </section>
      </main>
    </div>
  );
}