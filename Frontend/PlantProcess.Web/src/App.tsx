// ============================================================
// FILE: Frontend/PlantProcess.Web/src/App.tsx
// Added: AuthProvider wrapping entire app tree.
// Added: Bootstrap loading/error screen before rendering routes.
// ============================================================

import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";
import { AppLayout } from "./components/AppLayout";
import { LoadingPanel } from "./components/AsyncState";
import { DashboardFilterProvider } from "./state/DashboardFilterContext";
import { DashboardSelectionProvider } from "./state/DashboardSelectionContext";
import { DashboardGridLayoutProvider } from "./state/DashboardGridLayoutContext";
import { DemoModeProvider } from "./state/DemoModeContext";
import { ThemeProvider } from "./state/ThemeContext";
import { AuthProvider, useAuth } from "./state/AuthContext";
import { ErrorBoundary } from "./components/ErrorBoundary";

import "./index.css";

// ── Lazy pages ────────────────────────────────────────────────
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
  import("./pages/DataQualityPage").then((m) => ({ default: m.DataQualityPage }))
);
const CorrelationPage = lazy(() =>
  import("./pages/CorrelationPage").then((m) => ({ default: m.CorrelationPage }))
);
const AdminPage = lazy(() =>
  import("./pages/AdminPage").then((m) => ({ default: m.AdminPage }))
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
const MlReadinessPage = lazy(() =>
  import("./pages/MlReadiness/MlReadinessPage").then((m) => ({
    default: m.MlReadinessPage,
  }))
);

// ── Bootstrap screen (shown while auto-login runs) ────────────
function BootstrapScreen() {
  const { isBootstrapping, bootstrapError, retryBootstrap } = useAuth();

  if (isBootstrapping) {
    return (
      <div style={{
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: "1rem",
        background: "linear-gradient(180deg,#050b18 0%,#081426 52%,#050b18 100%)",
        color: "#eaf6ff",
        fontFamily: "Inter,ui-sans-serif,system-ui,sans-serif",
      }}>
        <div style={{
          width: 48, height: 48, borderRadius: 14, overflow: "hidden",
          boxShadow: "0 0 28px rgba(0,212,255,0.3)",
        }}>
          <img src="/brand/sou-icon.svg" alt="SOU" style={{ width: "100%", height: "100%" }} />
        </div>
        <div style={{ textAlign: "center" }}>
          <p style={{ margin: "0 0 0.3rem", fontSize: 18, fontWeight: 700 }}>
            PlantProcess <span style={{ color: "#00d4ff" }}>IQ</span>
          </p>
          <p style={{ margin: 0, fontSize: 13, color: "#5a7a9a" }}>
            Connecting to backend…
          </p>
        </div>
        <div style={{
          width: 200, height: 3, borderRadius: 2,
          background: "rgba(0,212,255,0.12)",
          overflow: "hidden",
        }}>
          <div style={{
            height: "100%", width: "40%",
            background: "linear-gradient(90deg,#00d4ff,#0a84ff)",
            borderRadius: 2,
            animation: "piq-shimmer 1.4s ease-in-out infinite",
          }} />
        </div>
        <style>{`
          @keyframes piq-shimmer {
            0%   { transform: translateX(-250%); }
            100% { transform: translateX(350%); }
          }
        `}</style>
      </div>
    );
  }

  if (bootstrapError) {
    return (
      <div style={{
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: "1.25rem",
        background: "linear-gradient(180deg,#050b18 0%,#081426 52%,#050b18 100%)",
        color: "#eaf6ff",
        fontFamily: "Inter,ui-sans-serif,system-ui,sans-serif",
        padding: "2rem",
        textAlign: "center",
      }}>
        <div style={{
          width: 48, height: 48, borderRadius: 14, overflow: "hidden",
          opacity: 0.5,
        }}>
          <img src="/brand/sou-icon.svg" alt="SOU" style={{ width: "100%", height: "100%" }} />
        </div>
        <div>
          <p style={{ margin: "0 0 0.5rem", fontSize: 18, fontWeight: 700 }}>
            Backend connection failed
          </p>
          <p style={{
            margin: "0 0 1.5rem", fontSize: 13, color: "#5a7a9a",
            maxWidth: 480, lineHeight: 1.6,
          }}>
            {bootstrapError}
          </p>
          <button
            onClick={retryBootstrap}
            style={{
              padding: "0.55rem 1.5rem",
              borderRadius: 8, border: "1px solid rgba(0,212,255,0.25)",
              background: "rgba(0,212,255,0.08)", color: "#00d4ff",
              fontSize: 13, fontWeight: 600, cursor: "pointer",
            }}
          >
            Retry connection
          </button>
        </div>
      </div>
    );
  }

  // Auth ready — render nothing (children take over)
  return null;
}

// ── Inner app (rendered only when auth is ready) ──────────────
function AppRoutes() {
  const { isBootstrapping, bootstrapError } = useAuth();

  if (isBootstrapping || bootstrapError) {
    return <BootstrapScreen />;
  }

  return (
  <ErrorBoundary routePath="app" fallbackTitle="The application could not start">
    <DemoModeProvider>
      <DashboardFilterProvider>
        <DashboardSelectionProvider>
          <DashboardGridLayoutProvider>
            <Suspense
              fallback={
                <LoadingPanel text="Loading PlantProcess IQ workspace." />
              }
            >
            <Routes>
              <Route element={<AppLayout />}>
                <Route index element={<Navigate to="/dashboard" replace />} />

                <Route
                  path="/dashboard"
                  element={
                    <ErrorBoundary routePath="/dashboard" fallbackTitle="The dashboard could not load">
                      <DashboardPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/materials/:materialUnitId"
                  element={
                    <ErrorBoundary routePath="/materials/:materialUnitId" fallbackTitle="Material details could not load">
                      <MaterialInvestigationPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/materials"
                  element={
                    <ErrorBoundary routePath="/materials" fallbackTitle="The material list could not load">
                      <MaterialInvestigationPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/risk"
                  element={
                    <ErrorBoundary routePath="/risk" fallbackTitle="Risk dashboard could not load">
                      <RiskDashboardPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/data-quality"
                  element={
                    <ErrorBoundary routePath="/data-quality" fallbackTitle="Data quality view could not load">
                      <DataQualityPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/correlations"
                  element={
                    <ErrorBoundary routePath="/correlations" fallbackTitle="Correlation analysis could not load">
                      <CorrelationPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/ml-readiness"
                  element={
                    <ErrorBoundary routePath="/ml-readiness" fallbackTitle="ML readiness view could not load">
                      <MlReadinessPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/demo-lifecycle"
                  element={
                    <ErrorBoundary routePath="/demo-lifecycle" fallbackTitle="Demo lifecycle view could not load">
                      <DemoLifecyclePage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/admin-preview"
                  element={
                    <ErrorBoundary routePath="/admin-preview" fallbackTitle="Admin preview could not load">
                      <AdminPreviewPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/admin/*"
                  element={
                    <ErrorBoundary routePath="/admin" fallbackTitle="The admin area could not load">
                      <AdminPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/brand"
                  element={
                    <ErrorBoundary routePath="/brand" fallbackTitle="The brand page could not load">
                      <BrandIdentityPage />
                    </ErrorBoundary>
                  }
                />

                <Route
                  path="/commercial/license"
                  element={
                    <ErrorBoundary routePath="/commercial/license" fallbackTitle="The license page could not load">
                      <CommercialLicensePage />
                    </ErrorBoundary>
                  }
                />

                <Route path="*" element={<Navigate to="/dashboard" replace />} />
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

// ── Root App ──────────────────────────────────────────────────
export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </ThemeProvider>
  );
}
