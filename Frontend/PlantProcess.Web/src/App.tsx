// ============================================================
// FILE: Frontend/PlantProcess.Web/src/App.tsx
//
// Phase 2 E2E Stability Update:
//
// - Uses the existing ErrorBoundary only.
// - Keeps one global app boundary plus route-level page boundaries.
// - Mounts exactly one global Sonner toast host.
// - Keeps canonical routes:
//     /dashboard
//     /materials
//     /risk
//     /data-quality
//     /correlations
//     /ml-readiness
//     /demo-lifecycle
//     /admin
//     /admin-preview
//     /brand
//     /commercial/license
//
// - Adds compatibility aliases required by old E2E/direct-route tests:
//     /quality                -> /data-quality
//     /correlation            -> /correlations
//     /material-investigation -> /materials
//     /commercial-license     -> /commercial/license
//
// Product guard:
// - This file does not change product positioning.
// - It only stabilizes shell routing, refresh survival, and toast mounting.
// ============================================================

import { lazy, Suspense, type ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";

import { AppLayout } from "./components/AppLayout";
import { ErrorBoundary } from "./components/standard/ErrorBoundary";
import { SkeletonWidgetGrid } from "./components/skeletons/Skeleton";
import { AppToastHost } from "./notifications/AppToastHost";

import { AuthProvider, useAuth } from "./state/AuthContext";
import { DashboardFilterProvider } from "./state/DashboardFilterContext";
import { DashboardGridLayoutProvider } from "./state/DashboardGridLayoutContext";
import { DashboardSelectionProvider } from "./state/DashboardSelectionContext";
import { DemoModeProvider } from "./state/DemoModeContext";
import { ThemeProvider } from "./state/ThemeContext";
import { LicenseProvider } from "./state/LicenseContext";
import "./index.css";

// â”€â”€ Lazy pages â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

const DashboardPage = lazy(() =>
  import("./pages/DashboardPage").then((m) => ({ default: m.DashboardPage }))
);

const MaterialInvestigationPage = lazy(() =>
  import("./pages/MaterialInvestigationPage").then((m) => ({
    default: m.MaterialInvestigationPage,
  }))
);

const RiskDashboardPage = lazy(() =>
  import("./pages/RiskDashboardPage").then((m) => ({
    default: m.RiskDashboardPage,
  }))
);

const DataQualityPage = lazy(() =>
  import("./pages/DataQualityPage").then((m) => ({
    default: m.DataQualityPage,
  }))
);

const CorrelationPage = lazy(() =>
  import("./pages/CorrelationPage").then((m) => ({
    default: m.CorrelationPage,
  }))
);

const AdminPage = lazy(() =>
  import("./pages/AdminPage").then((m) => ({
    default: m.AdminPage,
  }))
);

const AdminPreviewPage = lazy(() =>
  import("./pages/AdminPreview/AdminPreviewWorkspacePage").then((m) => ({
    default: m.AdminPreviewWorkspacePage,
  }))
);

const DemoLifecyclePage = lazy(() =>
  import("./pages/DemoLifecycle/DemoLifecyclePage").then((m) => ({
    default: m.DemoLifecyclePage,
  }))
);

const CommercialLicensePage = lazy(() =>
  import("./pages/CommercialLicense/CommercialLicensePage").then((m) => ({
    default: m.CommercialLicensePage,
  }))
);

const BrandIdentityPage = lazy(() =>
  import("./pages/BrandIdentity/BrandIdentityPage").then((m) => ({
    default: m.BrandIdentityPage,
  }))
);

const SuggestionsPage = lazy(() =>
  import("./pages/Phase78/Phase78Pages").then((m) => ({
    default: m.Phase78SuggestionsPage,
  }))
);

const DynamicPage = lazy(() =>
  import("./pages/Phase78/Phase78Pages").then((m) => ({
    default: m.Phase78DynamicPage,
  }))
);

const WidgetScriptCompilerPage = lazy(() =>
  import("./pages/Phase78/Phase78Pages").then((m) => ({
    default: m.Phase78WidgetScriptCompilerPage,
  }))
);

const MlReadinessPage = lazy(() =>
  import("./pages/MlReadiness/MlReadinessPage").then((m) => ({
    default: m.MlReadinessPage,
  }))
);

// â”€â”€ Boundary helper â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

function withPageBoundary(
  routePath: string,
  fallbackTitle: string,
  element: ReactNode
) {
  return (
    <ErrorBoundary routePath={routePath} fallbackTitle={fallbackTitle}>
      {element}
    </ErrorBoundary>
  );
}

// â”€â”€ Bootstrap screen â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

function BootstrapScreen() {
  const { isBootstrapping, bootstrapError, retryBootstrap } = useAuth();

  if (isBootstrapping) {
    return (
      <div
        style={{
          minHeight: "100vh",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          gap: "1rem",
          background:
            "linear-gradient(180deg,#050b18 0%,#081426 52%,#050b18 100%)",
          color: "#eaf6ff",
          fontFamily: "Inter,ui-sans-serif,system-ui,sans-serif",
        }}
      >
        <div
          style={{
            width: 48,
            height: 48,
            borderRadius: 14,
            overflow: "hidden",
            boxShadow: "0 0 28px rgba(0,212,255,0.3)",
          }}
        >
          <img
            src="/brand/sou-icon.svg"
            alt="SOU"
            style={{ width: "100%", height: "100%" }}
          />
        </div>

        <div style={{ textAlign: "center" }}>
          <p style={{ margin: "0 0 0.3rem", fontSize: 18, fontWeight: 700 }}>
            PlantProcess <span style={{ color: "#00d4ff" }}>IQ</span>
          </p>
          <p style={{ margin: 0, fontSize: 13, color: "#5a7a9a" }}>
            Connecting to backendâ€¦
          </p>
        </div>

        <div
          style={{
            width: 200,
            height: 3,
            borderRadius: 2,
            background: "rgba(0,212,255,0.12)",
            overflow: "hidden",
          }}
        >
          <div
            style={{
              height: "100%",
              width: "40%",
              background: "linear-gradient(90deg,#00d4ff,#0a84ff)",
              borderRadius: 2,
              animation: "piq-shimmer 1.4s ease-in-out infinite",
            }}
          />
        </div>

        <style>{`
          @keyframes piq-shimmer {
            0% { transform: translateX(-250%); }
            100% { transform: translateX(350%); }
          }
        `}</style>
      </div>
    );
  }

  if (bootstrapError) {
    return (
      <div
        style={{
          minHeight: "100vh",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          gap: "1.25rem",
          background:
            "linear-gradient(180deg,#050b18 0%,#081426 52%,#050b18 100%)",
          color: "#eaf6ff",
          fontFamily: "Inter,ui-sans-serif,system-ui,sans-serif",
          padding: "2rem",
          textAlign: "center",
        }}
      >
        <div
          style={{
            width: 48,
            height: 48,
            borderRadius: 14,
            overflow: "hidden",
            opacity: 0.5,
          }}
        >
          <img
            src="/brand/sou-icon.svg"
            alt="SOU"
            style={{ width: "100%", height: "100%" }}
          />
        </div>

        <div>
          <p style={{ margin: "0 0 0.5rem", fontSize: 18, fontWeight: 700 }}>
            Backend connection failed
          </p>

          <p
            style={{
              margin: "0 0 1.5rem",
              fontSize: 13,
              color: "#5a7a9a",
              maxWidth: 480,
              lineHeight: 1.6,
            }}
          >
            {bootstrapError}
          </p>

          <button
            type="button"
            onClick={retryBootstrap}
            style={{
              padding: "0.55rem 1.5rem",
              borderRadius: 8,
              border: "1px solid rgba(0,212,255,0.25)",
              background: "rgba(0,212,255,0.08)",
              color: "#00d4ff",
              fontSize: 13,
              fontWeight: 600,
              cursor: "pointer",
            }}
          >
            Retry connection
          </button>
        </div>
      </div>
    );
  }

  return null;
}

// â”€â”€ Route loading fallback â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

function RouteLoadingFallback() {
  return (
    <div className="ppiq-suspense-shell">
      <SkeletonWidgetGrid widgetCount={6} />
    </div>
  );
}

// â”€â”€ Routes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

function AppRoutes() {
  const { isBootstrapping, bootstrapError } = useAuth();

  if (isBootstrapping || bootstrapError) {
    return <BootstrapScreen />;
  }

  return (
    <ErrorBoundary
      routePath="app"
      fallbackTitle="The application could not start"
    >
      <DemoModeProvider>
        <DashboardFilterProvider>
          <DashboardSelectionProvider>
            <DashboardGridLayoutProvider>
              <Suspense fallback={<RouteLoadingFallback />}>
                <Routes>
                  <Route element={<AppLayout />}>
                    <Route
                      index
                      element={<Navigate to="/dashboard" replace />}
                    />

                    {/* Canonical dashboard route */}
                    <Route
                      path="/dashboard"
                      element={withPageBoundary(
                        "/dashboard",
                        "The dashboard is refreshing",
                        <DashboardPage />
                      )}
                    />

                    {/* Material investigation */}
                    <Route
                      path="/materials/:materialUnitId"
                      element={withPageBoundary(
                        "/materials/:materialUnitId",
                        "Material details is refreshing",
                        <MaterialInvestigationPage />
                      )}
                    />

                    <Route
                      path="/materials"
                      element={withPageBoundary(
                        "/materials",
                        "The material investigation view is refreshing",
                        <MaterialInvestigationPage />
                      )}
                    />

                    {/* Compatibility alias for older E2E/deep links */}
                    <Route
                      path="/material-investigation"
                      element={<Navigate to="/materials" replace />}
                    />

                    <Route
                      path="/material-investigation/:materialUnitId"
                      element={<Navigate to="/materials/:materialUnitId" replace />}
                    />

                    {/* Risk */}
                    <Route
                      path="/risk"
                      element={withPageBoundary(
                        "/risk",
                        "Risk dashboard is refreshing",
                        <RiskDashboardPage />
                      )}
                    />

                    {/* Data quality */}
                    <Route
                      path="/data-quality"
                      element={withPageBoundary(
                        "/data-quality",
                        "Data quality view is refreshing",
                        <DataQualityPage />
                      )}
                    />

                    {/* Compatibility alias for older E2E/deep links */}
                    <Route
                      path="/quality"
                      element={<Navigate to="/data-quality" replace />}
                    />

                    {/* Correlations */}
                    <Route
                      path="/correlations"
                      element={withPageBoundary(
                        "/correlations",
                        "Correlation analysis is refreshing",
                        <CorrelationPage />
                      )}
                    />

                    {/* Compatibility alias for older E2E/deep links */}
                    <Route
                      path="/correlation"
                      element={<Navigate to="/correlations" replace />}
                    />

                    {/* ML readiness */}
                    <Route
                      path="/ml-readiness"
                      element={withPageBoundary(
                        "/ml-readiness",
                        "ML readiness view is refreshing",
                        <MlReadinessPage />
                      )}
                    />

                    {/* Demo lifecycle */}
                    <Route
                      path="/demo-lifecycle"
                      element={withPageBoundary(
                        "/demo-lifecycle",
                        "Demo lifecycle view is refreshing",
                        <DemoLifecyclePage />
                      )}
                    />

                    {/* Admin preview workspace */}
                    <Route
                      path="/admin-preview"
                      element={withPageBoundary(
                        "/admin-preview",
                        "Admin preview is refreshing",
                        <AdminPreviewPage />
                      )}
                    />

                    {/* Admin area, including nested admin tabs */}
                    <Route
                      path="/admin/*"
                      element={withPageBoundary(
                        "/admin",
                        "The admin area is refreshing",
                        <AdminPage />
                      )}
                    />

                    {/* Brand identity */}
                    <Route
                      path="/brand"
                      element={withPageBoundary(
                        "/brand",
                        "The brand page is refreshing",
                        <BrandIdentityPage />
                      )}
                    />

                    {/* Phase 7 dynamic routes */}
                    <Route
                      path="/suggestions"
                      element={withPageBoundary(
                        "/suggestions",
                        "Suggestions are refreshing",
                        <SuggestionsPage />
                      )}
                    />

                    <Route
                      path="/pages/:slug"
                      element={withPageBoundary(
                        "/pages/:slug",
                        "Dynamic page is refreshing",
                        <DynamicPage />
                      )}
                    />

                    <Route
                      path="/widget-script-compiler"
                      element={withPageBoundary(
                        "/widget-script-compiler",
                        "Widget compiler is refreshing",
                        <WidgetScriptCompilerPage />
                      )}
                    />

                    {/* Commercial license */}
                    <Route
                      path="/commercial/license"
                      element={withPageBoundary(
                        "/commercial/license",
                        "The license page is refreshing",
                        <CommercialLicensePage />
                      )}
                    />

                    {/* Compatibility alias for older E2E/deep links */}
                    <Route
                      path="/commercial-license"
                      element={<Navigate to="/commercial/license" replace />}
                    />

                    {/* Default */}
                    <Route
                      path="*"
                      element={<Navigate to="/dashboard" replace />}
                    />
                  </Route>
                </Routes>
              </Suspense>
            </DashboardGridLayoutProvider>
          </DashboardSelectionProvider>
        </DashboardFilterProvider>
      </DemoModeProvider>
    </ErrorBoundary>
  );
}

// â”€â”€ Root App â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <LicenseProvider>
          <AppToastHost />
          <AppRoutes />
        </LicenseProvider>
      </AuthProvider>
    </ThemeProvider>
  );
}
