import { NavLink, Outlet } from "react-router-dom";
import {
  Activity,
  AlertTriangle,
  BarChart3,
  Factory,
  GitBranch,
  LayoutDashboard,
  Search,
  ShieldCheck,
} from "lucide-react";

const navItems = [
  { to: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { to: "/materials/investigation", label: "Investigation", icon: Search },
  { to: "/risk", label: "Risk", icon: ShieldCheck },
  { to: "/data-quality", label: "Data Quality", icon: AlertTriangle },
  { to: "/correlations", label: "Correlations", icon: GitBranch },
];

export function AppLayout() {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-icon">
            <Factory size={26} />
          </div>
          <div>
            <h1>PlantProcess IQ</h1>
            <p>Manufacturing intelligence</p>
          </div>
        </div>

        <nav className="nav">
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
                <Icon size={18} />
                <span>{item.label}</span>
              </NavLink>
            );
          })}
        </nav>

        <div className="sidebar-card">
          <Activity size={18} />
          <div>
            <strong>Backend</strong>
            <span>http://localhost:5063</span>
          </div>
        </div>
      </aside>

      <main className="main">
        <header className="topbar">
          <div>
            <h2>Process-to-Quality Intelligence Platform</h2>
            <p>
              Genealogy, process history, quality events, risk prediction and reporting.
            </p>
          </div>

          <div className="topbar-badge">
            <BarChart3 size={18} />
            Phase I Demo UI
          </div>
        </header>

        <Outlet />
      </main>
    </div>
  );
}