// ============================================================
// FILE: Frontend/PlantProcess.Web/src/components/AppLayout.tsx
// Update: reads real logged-in user from AuthContext
// ============================================================

import { NavLink, Outlet } from "react-router-dom";
import {
  AlertTriangle,
  BarChart3,
  BrainCircuit,
  CircleUserRound,
  Cpu,
  DatabaseZap,
  Factory,
  GitBranch,
  LayoutDashboard,
  LogOut,
  Moon,
  Network,
  PlayCircle,
  Search,
  Settings2,
  ShieldCheck,
  Sparkles,
  Sun,
} from "lucide-react";
import { plantProcessApi } from "../api/plantProcessApi";
import { useAuth } from "../state/AuthContext";
import { usePlantProcessTheme } from "../state/ThemeContext";
import { DemoModeControl } from "./demo/DemoModeControl";
import "./AppLayout.css";

// ── Navigation definition ─────────────────────────────────────
const NAV_ANALYTICS = [
  { to: "/dashboard",   label: "Command Dashboard",      desc: "Interactive intelligence workspace",    icon: LayoutDashboard },
  { to: "/materials",   label: "Material Investigation", desc: "Genealogy, quality and risk drilldown", icon: Search },
  { to: "/risk",        label: "Risk Intelligence",      desc: "Quality risk score and contributors",   icon: ShieldCheck },
  { to: "/data-quality",label: "Data Quality",           desc: "Readiness and validation findings",     icon: AlertTriangle },
  { to: "/correlations",label: "Correlations",           desc: "Process-to-quality analytics",          icon: GitBranch },
];

const NAV_INTELLIGENCE = [
  { to: "/ml-readiness",  label: "ML Readiness",    desc: "Labels, features and training gates",  icon: BrainCircuit },
  { to: "/demo-lifecycle",label: "Demo Lifecycle",  desc: "Connector to ML result workflow",       icon: PlayCircle },
];

const NAV_SYSTEM = [
  { to: "/admin-preview", label: "Admin Preview",  desc: "License, roles, ML scripts, report",   icon: BarChart3 },
  { to: "/admin",         label: "Administrator",  desc: "DB config, schema mapping and jobs",    icon: Settings2 },
  { to: "/brand",         label: "Brand",          desc: "Identity, positioning and proof",       icon: Sparkles },
];

function getRuntimeEnvironment(): "Demo" | "Development" | "Staging" | "Production" {
  const mode = import.meta.env.MODE?.toLowerCase();
  if (mode === "production") return "Demo";
  if (mode === "development") return "Development";
  if (mode === "staging") return "Staging";
  return "Demo";
}

function NavItem({
  to,
  label,
  desc,
  icon: Icon,
}: {
  to: string;
  label: string;
  desc: string;
  icon: React.ElementType;
}) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) => isActive ? "piq-nav-link active" : "piq-nav-link"}
    >
      <span className="piq-nav-link__icon" aria-hidden="true">
        <Icon size={16} />
      </span>
      <span className="piq-nav-link__copy">
        <span className="piq-nav-link__label">{label}</span>
        <span className="piq-nav-link__desc">{desc}</span>
      </span>
    </NavLink>
  );
}

export function AppLayout() {
  const { isDark, toggleTheme } = usePlantProcessTheme();
  const { user, logout } = useAuth();
  const env = getRuntimeEnvironment();

  const envClass =
    env === "Development" ? "piq-env-badge piq-env-badge--development" :
    env === "Demo"        ? "piq-env-badge piq-env-badge--demo" :
                            "piq-env-badge";

  const displayName = user?.displayName ?? user?.userName ?? "Admin";

  return (
    <div className="piq-shell">
      {/* ── Sidebar ── */}
      <aside className="piq-sidebar" aria-label="PlantProcess IQ navigation">

        {/* Brand header */}
        <div className="piq-brand-header">
          <div className="piq-brand-sou">
            <span className="piq-brand-sou__icon" aria-hidden="true">
              <img src="/brand/sou-icon.svg" alt="SOU" />
            </span>
            <span className="piq-brand-sou__text">
              <span className="piq-brand-sou__name">SOU</span>
              <span className="piq-brand-sou__tagline">Manufacturing Intelligence</span>
            </span>
          </div>
          <div className="piq-brand-divider" />
          <div className="piq-brand-product">
            <span className="piq-brand-product__name">
              PlantProcess&nbsp;<em>IQ</em>
            </span>
            <span className="piq-brand-product__sub">Process-to-Quality Intelligence</span>
          </div>
        </div>

        {/* Plant context */}
        <div className="piq-plant-strip">
          <span className="piq-plant-strip__dot" aria-hidden="true" />
          <Factory size={12} aria-hidden="true" style={{ opacity: 0.5 }} />
          <span className="piq-plant-strip__name">Demo Plant</span>
          <span className="piq-plant-strip__badge">DEMO</span>
        </div>

        {/* Navigation */}
        <nav className="piq-nav">
          <p className="piq-nav__section-label">Analytics</p>
          {NAV_ANALYTICS.map((item) => <NavItem key={item.to} {...item} />)}

          <p className="piq-nav__section-label">Intelligence</p>
          {NAV_INTELLIGENCE.map((item) => <NavItem key={item.to} {...item} />)}

          <p className="piq-nav__section-label">System</p>
          {NAV_SYSTEM.map((item) => <NavItem key={item.to} {...item} />)}
        </nav>

        {/* Bottom */}
        <div className="piq-sidebar-bottom">
          <div className="piq-sidebar-stat">
            <DatabaseZap size={11} aria-hidden="true" />
            <span>API</span>
            <code>{plantProcessApi.apiBaseUrl}</code>
          </div>
          <div className="piq-sidebar-stat">
            <Network size={11} aria-hidden="true" />
            <span>Phase 8–10 Interactive MVP</span>
          </div>
          <button className="piq-theme-btn" type="button" onClick={toggleTheme}
            title={`Switch to ${isDark ? "light" : "dark"} mode`}>
            {isDark ? <Sun size={14} /> : <Moon size={14} />}
            {isDark ? "Light mode" : "Dark mode"}
          </button>
        </div>
      </aside>

      {/* ── Main ── */}
      <main className="piq-main">

        {/* Command header */}
        <header className="piq-cmd-header">
          <div className="piq-cmd-header__left">
            <div className="piq-cmd-header__context">
              <span className="piq-cmd-header__ctx-pill">
                <Cpu size={12} aria-hidden="true" />
                <span>Plant</span>
                <strong>Demo Plant</strong>
              </span>
              <span className="piq-cmd-header__ctx-pill">
                <ShieldCheck size={12} aria-hidden="true" />
                <span>Status</span>
                <strong>Healthy</strong>
              </span>
            </div>
          </div>

          <div className="piq-cmd-header__right">
            <span className={envClass}>{env}</span>
            <span className="piq-tier-badge">Demo</span>
            <button className="piq-user-btn" type="button"
              onClick={logout} title="Logout" aria-label="Logout">
              <CircleUserRound size={14} aria-hidden="true" />
              {displayName}
              <LogOut size={12} aria-hidden="true" style={{ opacity: 0.6 }} />
            </button>
          </div>
        </header>

        {/* Demo mode bar */}
        <div className="piq-demo-bar">
          <DemoModeControl />
        </div>

        {/* Page header */}
        <div className="piq-topbar">
          <div>
            <p className="piq-topbar__kicker">
              <Cpu size={12} aria-hidden="true" />
              Process-to-Quality Intelligence Platform
            </p>
            <h2 className="piq-topbar__title">Industrial Analytics Command Center</h2>
            <p className="piq-topbar__sub">
              Digital plant data, genealogy, process history, quality events, risk scoring
              and correlation intelligence in one evidence-based manufacturing workspace.
            </p>
          </div>
          <div className="piq-topbar__actions">
            <span className="piq-topbar-badge">
              <ShieldCheck size={13} aria-hidden="true" />
              Rule-based intelligence
            </span>
            <span className="piq-topbar-badge piq-topbar-badge--highlight">
              <BarChart3 size={13} aria-hidden="true" />
              Interactive workspace
            </span>
          </div>
        </div>

        {/* Page content */}
        <div className="piq-workspace">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
