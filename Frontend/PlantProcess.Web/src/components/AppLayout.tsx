import { useEffect, useState } from "react";
// ============================================================
// FILE: Frontend/PlantProcess.Web/src/components/AppLayout.tsx
// Update: reads real logged-in user from AuthContext
// ============================================================

import { NavLink, Outlet, useLocation } from "react-router-dom";
/* PPIQ-T071: the dock is mounted by the LAYOUT, not by a route, so the
   conversation outlives every child-route change. Chapter 4 5.7.1. */
import { AssistantDockProvider } from "./assistant/AssistantDockContext";
import { AssistantDock } from "./assistant/AssistantDock";
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
import { productApi } from "../api/productApiClient";
import { apiClient } from "../api/http";
import { useAuth } from "../state/AuthContext";
import { usePlantProcessTheme } from "../state/ThemeContext";
import { AppToaster } from "../notifications/Toaster";
import { LogPanel } from "./logging/LogPanel";
import { JourneyRail } from "./journey/JourneyRail";

import "./AppLayout.css";
import { StandardButton } from "@/components/standard";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
// - Navigation definition -
const NAV_DATA_INTEGRATION = [
  { to: "/data-integration/alerting", label: "Plant Data Log", desc: "Threshold alerts on imported observations", icon: AlertTriangle },
  { to: "/data-integration/supervisor", label: "Engine Supervisor", desc: "Weekly engine review of jobs, models and coefficients", icon: BrainCircuit },
  { to: "/data-integration/author-mapping", label: "Load to Plant Data", desc: "Author a mapping and project staged rows", icon: Network },
  { to: "/data-integration/connections",     label: "Connections",     desc: "DB links: connect and test plant sources", icon: DatabaseZap },
  { to: "/data-integration/registry",        label: "Table Registry",  desc: "Map source tables to the canonical model", icon: Network },
  { to: "/data-integration/prepare",         label: "Prepare Import",  desc: "Pick columns, keys and watermark",         icon: GitBranch },
  { to: "/data-integration/importing",       label: "Importing",       desc: "Stage-1 and Stage-2 pipeline",             icon: PlayCircle },
  { to: "/data-integration/jobs",            label: "Jobs Monitor",    desc: "Run, pause, resume and inspect history",   icon: Cpu },
  { to: "/data-integration/connector-truth", label: "Connector Truth", desc: "Per-connector sync and schema drift",      icon: ShieldCheck },
];

/* PPIQ-SCENE5678 (M1-01): the two no-code surfaces get real nav entries. */
const NAV_ANALYTICS = [
  { to: "/prep/canvas",       label: "Join Canvas",      desc: "Wire staged tables into a published mapping", icon: GitBranch },
  { to: "/analysis/toolbox",  label: "Analysis Toolbox", desc: "Compose a governed analysis from blocks",     icon: Cpu },
  { to: "/dashboard",   label: "Command Dashboard",      desc: "Interactive intelligence workspace",    icon: LayoutDashboard },
  {
    to: "/dashboard/widgets/schema-drift",
    label: "Widget Drift",
    desc: "Schema contract + heatmap filters",
    icon: BarChart3,
  },
  { to: "/materials",   label: "Material Investigation", desc: "Genealogy, quality and risk drilldown", icon: Search },
  { to: "/risk",        label: "Risk Intelligence",      desc: "Quality risk score and contributors",   icon: ShieldCheck },
  { to: "/data-quality",label: "Data Quality",           desc: "Readiness and validation findings",     icon: AlertTriangle },
  { to: "/correlations",label: "Correlations",           desc: "Process-to-quality analytics",          icon: GitBranch },
];

const NAV_INTELLIGENCE = [
  { to: "/value/scenario", label: "Value Scenario", desc: "Projected vs tracked ROI", icon: BarChart3 },
  { to: "/value/executive", label: "Value Exec", desc: "Bounded EUR ROI", icon: BarChart3 },
  { to: "/ml-readiness",  label: "ML Readiness",    desc: "Labels, features and training gates",  icon: BrainCircuit },
];

const NAV_ASSISTANT = [
  { to: "/suggestions", label: "Suggestions", desc: "Guarded recommendations", icon: BrainCircuit },
  { to: "/assistant/configuration", label: "Assistant Configuration", desc: "HMI grounding controls", icon: Settings2 },
];

const NAV_SYSTEM = [
  { to: "/advisory/honesty-certification", label: "Honesty Cert.", desc: "Approval + no overclaim gate", icon: DatabaseZap },
  { to: "/advisory/benchmarking", label: "Benchmarking", desc: "Cross-plant privacy bands", icon: DatabaseZap },
  { to: "/advisory/roi-cfo-dashboard", label: "ROI/CFO Value", desc: "Potential vs realized value", icon: DatabaseZap },
  { to: "/advisory/value-realization", label: "Value Realization", desc: "Baseline vs actual ledger", icon: DatabaseZap },
  { to: "/advisory/recommendations", label: "Recommendations", desc: "Expected - impact + approval", icon: DatabaseZap },
  { to: "/advisory/scenario-simulation", label: "What-if Simulation", desc: "Projected outcome under an alternative operating scenario", icon: DatabaseZap },
  { to: "/edge-collector", label: "Edge Collector", desc: "OT-safe one-way push status", icon: DatabaseZap },
  { to: "/historian-connector", label: "Historian Connector", desc: "Register, test, browse and map tags", icon: DatabaseZap },
  { to: "/admin-preview", label: "Admin Preview",  desc: "License, roles, ML scripts, report",   icon: BarChart3 },
  { to: "/admin",         label: "Administrator",  desc: "Site identity, license and access",     icon: Settings2 },
  { to: "/brand",         label: "Brand",          desc: "Identity, positioning and proof",       icon: Sparkles },
];

function getRuntimeEnvironment(): "Demo" | "Development" | "Staging" | "Production" {
  // Configured, never compiled-in. Set VITE_PPIQ_ENVIRONMENT to override.
  const configured = (import.meta.env.VITE_PPIQ_ENVIRONMENT as string | undefined)?.trim();
  if (configured === "Production" || configured === "Staging" || configured === "Development") {
    return configured;
  }
  const mode = import.meta.env.MODE?.toLowerCase();
  if (mode === "production") return "Production";
  if (mode === "development") return "Development";
  if (mode === "staging") return "Staging";
  return "Production";
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

/** PPIQ-NAVFIX: this hook used the ONLY raw fetch in the whole src tree, and it
 *  failed three ways at once, silently:
 *    - the URL was relative, so it resolved against the Vite dev server on 5173
 *      instead of the API on 5063. There is no proxy block in vite.config.ts, so
 *      Vite's history fallback answered with index.html and HTTP 200 - res.ok was
 *      true, the JSON parse threw on HTML, and the catch swallowed it.
 *    - it read its bearer token from a window global that is assigned NOWHERE in
 *      the codebase, so the request would have been unauthenticated anyway.
 *    - every failure path returned an empty list with no console line, so the
 *      WORKSPACES group rendered a header with nothing under it.
 *  It now goes through apiClient like every other call in the product, which
 *  carries the configured base URL and the auth interceptor. A failure is
 *  reported once to the console instead of vanishing. */
function useWorkspaceLinks(): NavEntry[] {
  const [links, setLinks] = useState<NavEntry[]>([]);
  useEffect(() => {
    let ignore = false;
    apiClient
      .get<unknown>("/analytics/dashboard/definitions")
      .then((body) => {
        if (ignore) return;
        const container = body as Record<string, unknown> | unknown[] | null;
        const arr = Array.isArray(container)
          ? container
          : ((container?.["items"] ??
              container?.["definitions"] ??
              container?.["dashboards"] ??
              container?.["results"] ??
              []) as unknown[]);
        const mapped = (arr as Array<Record<string, unknown>>)
          .map((d) => ({
            code: String(d["dashboardCode"] ?? d["dashboard_code"] ?? d["code"] ?? ""),
            name: String(d["name"] ?? d["dashboardCode"] ?? d["code"] ?? "Workspace"),
          }))
          .filter((d) => d.code)
          .sort((a, b) => a.name.localeCompare(b.name))
          .map((d) => ({
            to: "/workspace/" + d.code,
            label: d.name,
            desc: "Interactive analytics workspace",
            icon: LayoutDashboard,
          }));
        setLinks(mapped);
      })
      .catch((err) => {
        if (ignore) return;
        console.warn("[nav] workspace list unavailable:", err);
        setLinks([]);
      });
    return () => { ignore = true; };
  }, []);
  return links;
}
/** PPIQ-NAVFIX: one shape for every nav entry, so the Workspaces group can use
 *  the same collapsible NavGroup as its four neighbours instead of a hand-rolled
 *  block that could not fold and sat at a different indent. */
export type NavEntry = { to: string; label: string; desc: string; icon: React.ElementType };

function NavGroup({ title, items, emptyHint }: { title: string; items: ReadonlyArray<NavEntry>; emptyHint?: string }) {
  const location = useLocation();
  const containsCurrent = items.some((i) => location.pathname === i.to || location.pathname.startsWith(i.to + "/"));
  const [open, setOpen] = useState<boolean>(containsCurrent);
  return (
    <div className={"piq-nav-group" + (open ? " piq-nav-group--open" : "")}>
      <StandardButton
        variant="ghost"
        type="button"
        className="piq-nav-group__header"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        <span className="piq-nav-group__title">{title}</span>
        <span className="piq-nav-group__chevron" aria-hidden="true">{open ? "\u25BE" : "\u25B8"}</span>
      </StandardButton>
      <div className="piq-nav-group__items" hidden={!open}>
        {items.length > 0
          ? items.map((item) => <NavItem key={item.to} {...item} />)
          : emptyHint
            ? <p className="piq-nav-group__empty">{emptyHint}</p>
            : null}
      </div>
    </div>
  );
}
export function AppLayout() {
  const workspaceLinks = useWorkspaceLinks();
  const { isDark, toggleTheme } = usePlantProcessTheme();

  const [plantName, setPlantName] = useState<string>("Plant");

  useEffect(() => {
    let cancelled = false;

    apiClient
      .get<{ siteName?: string | null }>("/admin/site-identity")
      .then((identity) => {
        const nextName = identity?.siteName?.trim();
        if (!cancelled && nextName) setPlantName(nextName);
      })
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, []);
  const { user, logout } = useAuth();
  const env = getRuntimeEnvironment();

  const envClass =
    env === "Development" ? "piq-env-badge piq-env-badge--development" :
    env === "Demo"        ? "piq-env-badge piq-env-badge--demo" :
                            "piq-env-badge";

  const displayName = user?.displayName ?? user?.userName ?? "Admin";

  return (
    <div className="piq-shell">
      <AppToaster />
      {/* - Sidebar - */}
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
          <Factory size={12} aria-hidden="true" />
          <span className="piq-plant-strip__name">{plantName}</span>
          {import.meta.env.VITE_SHOW_ENVIRONMENT_BADGES === "1" ? (<span className="piq-plant-strip__badge">DEMO</span>) : null}
        </div>

        {/* Navigation */}
        <nav className="piq-nav">
          <NavGroup title="Data Integration" items={NAV_DATA_INTEGRATION} />
          <NavGroup title="Analytics" items={NAV_ANALYTICS} />
          <NavGroup title="Workspaces" items={workspaceLinks} emptyHint="No workspaces published yet" />

          <NavGroup title="Intelligence" items={NAV_INTELLIGENCE} />
          <NavGroup title="System" items={NAV_SYSTEM} />
        </nav>

        {/* Bottom */}
        <div className="piq-sidebar-bottom">
          <div className="piq-sidebar-stat">
            <DatabaseZap size={11} aria-hidden="true" />
            <span>API</span>
            <code>{productApi.apiBaseUrl}</code>
          </div>
          <div className="piq-sidebar-stat">
            <Network size={11} aria-hidden="true" />
            <span>Interactive workspace</span>
          </div>
          <StandardButton className="piq-theme-btn" type="button" onClick={toggleTheme}
            title={`Switch to ${isDark ? "light" : "dark"} mode`}>
            {isDark ? <Sun size={14} /> : <Moon size={14} />}
            {isDark ? "Light mode" : "Dark mode"}
          </StandardButton>
        </div>
      </aside>

      {/* - Main - */}
      <main className="piq-main">
        <JourneyRail />

        {/* Command header */}
        <header className="piq-cmd-header">
          <div className="piq-cmd-header__left">
            <div className="piq-cmd-header__context">
              <span className="piq-cmd-header__ctx-pill">
                <Cpu size={12} aria-hidden="true" />
                <span>Plant</span>
                <strong>{plantName}</strong>
              </span>
              <span className="piq-cmd-header__ctx-pill">
                <ShieldCheck size={12} aria-hidden="true" />
                <span>Status</span>
                <strong>Healthy</strong>
              </span>
            </div>
          </div>

          <div className="piq-cmd-header__right">
            {import.meta.env.VITE_SHOW_ENVIRONMENT_BADGES === "1" ? (<span className={envClass}>{env}</span>) : null}
            {import.meta.env.VITE_SHOW_ENVIRONMENT_BADGES === "1" ? (<span className="piq-tier-badge">Demo</span>) : null}
            <StandardButton className="piq-user-btn" type="button"
              onClick={logout} title="Logout" ariaLabel="Logout">
              <CircleUserRound size={14} aria-hidden="true" />
              {displayName}
              <LogOut size={12} aria-hidden="true" />
            </StandardButton>
          </div>
        </header>
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
        <AssistantDockProvider>
          <div className="piq-workspace">
            <Outlet />
          </div>
          <AssistantDock />
        </AssistantDockProvider>

        <LogPanel />
      </main>
    </div>
  );
}