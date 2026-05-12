import { NavLink, Outlet } from "react-router-dom";
import {
  AlertTriangle,
  BarChart3,
  Cpu,
  DatabaseZap,
  Factory,
  GitBranch,
  LayoutDashboard,
  Moon,
  Network,
  Radar,
  Search,
  ShieldCheck,
  Sparkles,
  Sun,
} from "lucide-react";
import { plantProcessApi } from "../api/plantProcessApi";
import { usePlantProcessTheme } from "../state/ThemeContext";

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
];

export function AppLayout() {
  const { theme, isDark, toggleTheme } = usePlantProcessTheme();

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-glow" />

        <div className="brand">
          <div className="brand-icon">
            <Factory size={28} />
          </div>

          <div className="brand-copy">
            <h1>PlantProcess IQ</h1>
            <p>Manufacturing intelligence</p>
          </div>
        </div>

        <div className="sidebar-status-card">
          <div className="status-orb">
            <Radar size={18} />
          </div>
          <div>
            <strong>Digital Plant Layer</strong>
            <span>Canonical analytics model online</span>
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

      <main className="main">
        <header className="topbar">
          <div className="topbar-left">
            <div className="topbar-kicker">
              <Cpu size={15} />
              Process-to-Quality Intelligence Platform
            </div>

            <h2>Industrial Analytics Command Center</h2>

            <p>
              Digital plant data, genealogy, process history, quality events,
              risk prediction and correlation intelligence.
            </p>
          </div>

          <div className="topbar-actions">
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
              AI-ready analytics layer
            </div>

            <div className="topbar-badge topbar-badge--strong">
              <BarChart3 size={16} />
              Interactive workspace
            </div>
          </div>
        </header>

        <Outlet />
      </main>
    </div>
  );
}