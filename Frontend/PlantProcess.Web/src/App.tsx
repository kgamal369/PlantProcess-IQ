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
import { RoutedInteractiveWorkspacePage } from "./pages/Dashboard/InteractiveWorkspacePage";
import { DashboardSelectionProvider } from "./state/DashboardSelectionContext";
import { ThemeProvider } from "./state/ThemeContext";
import { LicenseProvider } from "./state/LicenseContext";
import "./index.css";
import { StandardButton } from "@/components/standard";


// --- Lazy pages ---

const SourceImportPrepPage = lazy(() =>
  import("./pages/SourceImportPrepPage").then((m) => ({ default: m.default }))
);
/* PPIQ-SCENE5678 (M1-01): the canvas and the toolbox were built, gated and
   committed, and never routed. Both pages sat on disk with no way to reach
   them from the running application. PPIQ T-032: the canvas is no longer a page of its own - Chapter 4 section 5.2.1 rules ONE authoring shell serving five purposes, and this route opens it in S1 mode. */
const SharedAuthoringShell = lazy(() =>
  import("./authoring/SharedAuthoringShell").then((m) => ({ default: m.default }))
);
const AnalysisToolboxPage = lazy(() =>
  import("./pages/Analysis/AnalysisToolboxPage").then((m) => ({ default: m.default }))
);
const AnalysisJobConfigPage = lazy(() =>
  import("./pages/AnalysisJobConfigPage").then((m) => ({ default: m.default }))
);
const DashboardPage = lazy(() =>
  import("./pages/Dashboard/DashboardPage").then((m) => ({ default: m.DashboardPage }))

);
const MaterialInvestigationPage = lazy(() =>
  import("./pages/MaterialInvestigationPage").then((m) => ({
    default: m.MaterialInvestigationPage,
  }))
);

const RiskDashboardPage = lazy(() =>
  import("./pages/RiskDashboard/RiskDashboardPage").then((m) => ({
    default: m.RiskDashboardPage,
  }))
);


const DataQualityPage = lazy(() =>
  import("./pages/DataQuality/DataQualityPage").then((m) => ({
    default: m.DataQualityPage,
  }))

);

const CorrelationPage = lazy(() =>
  import("./pages/Correlation/CorrelationPage").then((m) => ({
    default: m.CorrelationPage,
  }))

);

const DataIntegrationLayout = lazy(() =>
  import("./pages/DataIntegration/DataIntegrationLayout").then((m) => ({ default: m.DataIntegrationLayout }))
);
const ConnectionsRoute = lazy(() =>
  import("./pages/DataIntegration/DataIntegrationRoutes").then((m) => ({ default: m.ConnectionsRoute }))
);
const TableRegistryRoute = lazy(() =>
  import("./pages/DataIntegration/DataIntegrationRoutes").then((m) => ({ default: m.TableRegistryRoute }))
);
const ImportingRoute = lazy(() =>
  import("./pages/DataIntegration/DataIntegrationRoutes").then((m) => ({ default: m.ImportingRoute }))
);
const JobsMonitorRoute = lazy(() =>
  import("./pages/DataIntegration/DataIntegrationRoutes").then((m) => ({ default: m.JobsMonitorRoute }))
);
const AuthorMappingPage = lazy(() =>
  import("./pages/DataIntegration/AuthorMappingPage").then((m) => ({ default: m.AuthorMappingPage }))
);
const SupervisorReportPage = lazy(() =>
  import("./pages/DataIntegration/SupervisorReportPage").then((m) => ({ default: m.SupervisorReportPage }))
);
const AlertingPage = lazy(() =>
  import("./pages/DataIntegration/AlertingPage").then((m) => ({ default: m.AlertingPage }))
);
const ConnectorTruthPage = lazy(() =>
  import("./pages/DataIntegration/ConnectorTruthPage").then((m) => ({ default: m.ConnectorTruthPage }))
);
const AdminTabRedirect = lazy(() =>
  import("./pages/DataIntegration/AdminTabRedirect").then((m) => ({ default: m.AdminTabRedirect }))
);

const AdminPage = lazy(() =>
  import("./pages/Admin/AdminPage").then((m) => ({
    default: m.AdminPage,
  }))
);

const AdminPreviewPage = lazy(() =>
  import("./pages/AdminPreview/AdminPreviewWorkspacePage").then((m) => ({
    default: m.AdminPreviewWorkspacePage,
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

);const DynamicPage = lazy(() =>
  import("./pages/DynamicPage/DynamicPage").then((m) => ({
    default: m.DynamicPage,
  }))
);


const WidgetScriptCompilerPage = lazy(() =>
  import("./pages/PlatformOps/DemoAnalyticsPages").then((m) => ({
    default: m.DemoAnalyticsWidgetScriptCompilerPage,
  }))
);


const PageBuilderPage = lazy(() =>
  import("./pages/PageBuilder/PageBuilderPage").then((m) => ({
    default: m.PageBuilderPage,
  }))
);

const AnalyticsWidgetsPage = lazy(() =>
  import("./pages/Analytics/AnalyticsWidgetsPage").then((m) => ({
    default: m.AnalyticsWidgetsPage,
  }))
);


const MlReadinessPage = lazy(() =>
  import("./pages/MlReadiness/MlReadinessPage").then((m) => ({
    default: m.MlReadinessPage,
  }))
);


// --- Boundary helper ---

const AdvancedAnalysisPage = lazy(() =>
  import("./pages/Analytics/AdvancedAnalysisPage").then((m) => ({ default: m.AdvancedAnalysisPage }))
);


const InspectionJobsPage = lazy(() =>
  import("./pages/Analytics/InspectionJobsPage").then((m) => ({ default: m.InspectionJobsPage }))
);


const MappingHealthPage = lazy(() =>
  import("./pages/MappingHealth/MappingHealthPage").then((module) => ({
    default: module.MappingHealthPage,
  }))
);

const HonestyCertificationPage = lazy(() =>
  import("./pages/Advisory/HonestyCertificationPage").then((m) => ({
    default: m.HonestyCertificationPage,
  }))
);


const BenchmarkingPage = lazy(() =>
  import("./pages/Advisory/BenchmarkingPage").then((m) => ({
    default: m.BenchmarkingPage,
  }))
);


const RoiCfoDashboardPage = lazy(() =>
  import("./pages/Advisory/RoiCfoDashboardPage").then((m) => ({
    default: m.RoiCfoDashboardPage,
  }))
);


const ValueRealizationPage = lazy(() =>
  import("./pages/Advisory/ValueRealizationPage").then((m) => ({
    default: m.ValueRealizationPage,
  }))


);const SuggestionRecommendationPage = lazy(() =>
  import("./pages/Phase8/SuggestionRecommendationPage").then((m) => ({
    default: m.SuggestionRecommendationPage,
  }))
);
const AssistantRuntimePage = lazy(() =>
  import("./pages/Phase8/AssistantRuntimePage").then((m) => ({
    default: m.AssistantRuntimePage,
  }))
);
const AssistantConfigurationPage = lazy(() =>
  import("./pages/Phase8/AssistantConfigurationPage").then((m) => ({
    default: m.AssistantConfigurationPage,
  }))
);

const RecommendationsPage = lazy(() =>
  import("./pages/Advisory/RecommendationsPage").then((m) => ({
    default: m.RecommendationsPage,
  }))
);


const ScenarioSimulationPage = lazy(() =>
  import("./pages/Advisory/ScenarioSimulationPage").then((m) => ({
    default: m.ScenarioSimulationPage,
  }))
);


const EdgeCollectorPage = lazy(() =>
  import("./pages/EdgeCollector/EdgeCollectorPage").then((m) => ({
    default: m.EdgeCollectorPage,
  }))
);


const HistorianConnectorPage = lazy(() =>
  import("./pages/HistorianConnector/HistorianConnectorPage").then((m) => ({

    default: m.HistorianConnectorPage,
  }))
);

const I18nRtlReadinessPage = lazy(() =>
  import("./pages/I18nRtlReadinessPage").then((m) => ({ default: m.I18nRtlReadinessPage }))
);

const ExecutivePersonaDashboardPage = lazy(() =>
  import("./pages/Phase9/ExecutivePersonaDashboardPage").then((m) => ({
    default: m.ExecutivePersonaDashboardPage,
  }))
);
const PersonaAccessMatrixPage = lazy(() =>
  import("./pages/Phase9/PersonaAccessMatrixPage").then((m) => ({
    default: m.PersonaAccessMatrixPage,
  }))
);
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

// --- Bootstrap screen ---

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
            "linear-gradient(180deg,var(--ppiq-color-bg-deep) 0%,#081426 52%,var(--ppiq-color-bg-deep) 100%)",
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
            PlantProcess <span style={{ color: "var(--ppiq-color-accent-cyan)" }}>IQ</span>
          </p>
          <p style={{ margin: 0, fontSize: 13, color: "#5a7a9a" }}>
            Connecting to backend...
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
              background: "linear-gradient(90deg,var(--ppiq-color-accent-cyan),#0a84ff)",
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
            "linear-gradient(180deg,var(--ppiq-color-bg-deep) 0%,#081426 52%,var(--ppiq-color-bg-deep) 100%)",
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
          <StandardButton
            type="button"
            onClick={retryBootstrap}
            style={{
              padding: "0.55rem 1.5rem",
              borderRadius: 8,
              border: "1px solid rgba(0,212,255,0.25)",
              background: "rgba(0,212,255,0.08)",
              color: "var(--ppiq-color-accent-cyan)",
              fontSize: 13,
              fontWeight: 600,
              cursor: "pointer",
            }}
          >
            Retry connection
          </StandardButton>
        </div>
      </div>
    );
  }

  return null;
}

// --- Route loading fallback ---

function RouteLoadingFallback() {
  return (
    <div className="ppiq-suspense-shell">
      <SkeletonWidgetGrid widgetCount={6} />
    </div>
  );
}

// --- Routes ---

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
        <DashboardFilterProvider>
          <DashboardSelectionProvider>
            <DashboardGridLayoutProvider>
              <Suspense fallback={<RouteLoadingFallback />}>
                <Routes>
                  <Route element={<AppLayout />}>
                    {/* M1-03 Surface-1: visual import-prep on live discovery */}
                    <Route path="/workspace/:dashboardCode" element={<RoutedInteractiveWorkspacePage />} />
                    <Route path="/data-integration" element={<DataIntegrationLayout />}>
                      <Route index element={<Navigate to="/data-integration/connections" replace />} />
                      <Route path="connections" element={<ConnectionsRoute />} />
                      <Route path="registry" element={<TableRegistryRoute />} />
                      <Route path="importing" element={<ImportingRoute />} />
                      <Route path="jobs" element={<JobsMonitorRoute />} />
                      <Route path="connector-truth" element={<ConnectorTruthPage />} />
                      <Route path="alerting" element={<AlertingPage />} />
                      <Route path="supervisor" element={<SupervisorReportPage />} />
                      <Route path="author-mapping" element={<AuthorMappingPage />} />
                    </Route> <Route
                    
                      path="/data-integration/prepare"
                      element={withPageBoundary(
                        "/data-integration/prepare",
                        "Import preparation is refreshing",
                        <SourceImportPrepPage />
                      )}
                    />
                    <Route
                      path="/prep/canvas"
                      element={withPageBoundary(
                        "/prep/canvas",
                        "The join canvas is refreshing",
                        <SharedAuthoringShell purpose="S1" />
                      )}
                    />
                    <Route
                      path="/analysis/toolbox"
                      element={withPageBoundary(
                        "/analysis/toolbox",
                        "The analysis toolbox is refreshing",
                        <AnalysisToolboxPage />
                      )}
                    /> {/* M1-05 Surface-3: analysis-job definition on live data */}
                    <Route
                      path="/investigate/analysis-jobs"
                      element={withPageBoundary(
                        "/investigate/analysis-jobs",
                        "Analysis job configuration is refreshing",
                        <AnalysisJobConfigPage />
                      )}
                    />                    {/* P4 §7.4 advanced analysis + inspection workflow */}
 <Route
   path="/investigate/advanced"
   element={withPageBoundary("/investigate/advanced", "Advanced analysis is refreshing", <AdvancedAnalysisPage />)}
 />
 <Route
   path="/investigate/inspect"
   element={withPageBoundary("/investigate/inspect", "Inspection jobs are refreshing", <InspectionJobsPage />)}
 />
 <Route
   index
   element={<Navigate to="/dashboard" replace />}
 /> {/* Canonical dashboard route */}
 <Route
   path="/dashboard"
   element={withPageBoundary(
     "/dashboard",
     "The dashboard is refreshing",
     <DashboardPage />
   )}
 /> {/* Material investigation */}
 <Route
   path="/materials/:materialUnitId"
   element={withPageBoundary(
     "/materials/:materialUnitId",
     "Material details is refreshing",
     <MaterialInvestigationPage />
   )}
 /> <Route
   path="/materials"
   element={withPageBoundary(
     "/materials",
     "The material investigation view is refreshing",
     <MaterialInvestigationPage />
   )}
 /> {/* Compatibility alias for older E2E/deep links */}
 <Route
   path="/material-investigation"
   element={<Navigate to="/materials" replace />}
 /> <Route
   path="/material-investigation/:materialUnitId"
   element={<Navigate to="/materials/:materialUnitId" replace />}
 /> {/* Risk */}
 <Route
   path="/risk"
   element={withPageBoundary(
     "/risk",
     "Risk dashboard is refreshing",
     <RiskDashboardPage />
   )}
 /> {/* Data quality */}
 <Route
   path="/data-quality"
   element={withPageBoundary(
     "/data-quality",
     "Data quality view is refreshing",
     <DataQualityPage />
   )}
 /> {/* Compatibility alias for older E2E/deep links */}
 <Route
   path="/quality"
   element={<Navigate to="/data-quality" replace />}
 /> {/* Correlations */}
 <Route
   path="/correlations"
   element={withPageBoundary(
     "/correlations",
     "Correlation analysis is refreshing",
     <CorrelationPage />
   )}
 /> {/* Compatibility alias for older E2E/deep links */}
 <Route
   path="/correlation"
   element={<Navigate to="/correlations" replace />}
 /> {/* ML readiness */}
 <Route
   path="/ml-readiness"
   element={withPageBoundary(
     "/ml-readiness",
     "ML readiness view is refreshing",
     <MlReadinessPage />
   )}
 />                    {/* Mapping health + schema drift */}
 <Route
   path="/mapping-health"
   element={withPageBoundary(
     "/mapping-health",
     "Mapping health view is refreshing",
     <MappingHealthPage />
   )}
 />
 {/* Phase 7 i18n + Arabic RTL readiness */}
 <Route
   path="/i18n-rtl"
   element={withPageBoundary(
     "/i18n-rtl",
     "Internationalization readiness is refreshing",
     <I18nRtlReadinessPage />
   )}
 /> <Route
   path="/analytics-widgets"
   element={withPageBoundary(
     "/analytics-widgets",
     "Analytics widgets are loading",
     <AnalyticsWidgetsPage />
   )}
 /> {/* Pack G Phase 15 honesty certification */}
 <Route
   path="/advisory/honesty-certification"
   element={withPageBoundary(
     "/advisory/honesty-certification",
     "Honesty certification is refreshing",
     <HonestyCertificationPage />
   )}
 /> <Route
   path="/phase15/honesty-certification"
   element={<Navigate to="/advisory/honesty-certification" replace />}
 />
 {/* Pack G Phase 15 benchmarking */}
 <Route
   path="/advisory/benchmarking"
   element={withPageBoundary(
     "/advisory/benchmarking",
     "Benchmarking is refreshing",
     <BenchmarkingPage />
   )}
 /> <Route
   path="/phase15/benchmarking"
   element={<Navigate to="/advisory/benchmarking" replace />}
 />
 {/* Pack G Phase 15 ROI CFO dashboard */}
 <Route
   path="/advisory/roi-cfo-dashboard"
   element={withPageBoundary(
     "/advisory/roi-cfo-dashboard",
     "ROI/CFO dashboard is refreshing",
     <RoiCfoDashboardPage />
   )}
 /> <Route
   path="/phase15/roi-cfo-dashboard"
   element={<Navigate to="/advisory/roi-cfo-dashboard" replace />}
 />
 {/* Pack G Phase 15 value realization */}
 <Route
   path="/advisory/value-realization"
   element={withPageBoundary(
     "/advisory/value-realization",
     "Value realization is refreshing",
     <ValueRealizationPage />
   )}
 /> <Route
   path="/phase15/value-realization"
   element={<Navigate to="/advisory/value-realization" replace />}
 />
 
                    {/* Phase 8 AI suggestion and assistant HMI */}
                    <Route
                      path="/suggestions"
                      element={withPageBoundary(
                        "/suggestions",
                        "Suggestions are refreshing",
                        <SuggestionRecommendationPage />
                      )}
                    />
                    <Route path="/phase8/suggestions" element={<Navigate to="/suggestions" replace />} />
                    <Route
                      path="/assistant"
                      element={withPageBoundary(
                        "/assistant",
                        "Assistant is refreshing",
                        <AssistantRuntimePage />
                      )}
                    />
                    <Route path="/phase8/assistant" element={<Navigate to="/assistant" replace />} />
                    <Route
                      path="/assistant/configuration"
                      element={withPageBoundary(
                        "/assistant/configuration",
                        "Assistant configuration is refreshing",
                        <AssistantConfigurationPage />
                      )}
                    />
                    <Route path="/phase8/assistant-config" element={<Navigate to="/assistant/configuration" replace />} /> {/* Pack G Phase 15 recommendation generator */}
 <Route
   path="/advisory/recommendations"
   element={withPageBoundary(
     "/advisory/recommendations",
     "Recommendations are refreshing",
     <RecommendationsPage />
   )}
 /> <Route
   path="/phase15/recommendations"
   element={<Navigate to="/advisory/recommendations" replace />}
 />
 {/* Pack G Phase 15 scenario simulation */}
 <Route
   path="/advisory/scenario-simulation"
   element={withPageBoundary(
     "/advisory/scenario-simulation",
     "Scenario simulation is refreshing",
     <ScenarioSimulationPage />
   )}
 /> <Route
   path="/phase15/scenario-simulation"
   element={<Navigate to="/advisory/scenario-simulation" replace />}
 />
 {/* Pack F edge collector management UX */}
 <Route
   path="/edge-collector"
   element={withPageBoundary(
     "/edge-collector",
     "Edge collector management is refreshing",
     <EdgeCollectorPage />
   )}
 /> <Route
   path="/edge-agent"
   element={<Navigate to="/edge-collector" replace />}
 />
 {/* Pack E historian connector UI */}
 <Route
   path="/historian-connector"
   element={withPageBoundary(
     "/historian-connector",
     "Historian connector is refreshing",
     <HistorianConnectorPage />
   )}
 /> <Route
   path="/connectors/historian"
   element={<Navigate to="/historian-connector" replace />}
 />                    {/* Admin preview workspace */}
 <Route
   path="/admin-preview"
   element={withPageBoundary(
     "/admin-preview",
     "Admin preview is refreshing",
     <AdminPreviewPage />
   )}
 /> {/* Admin area, including nested admin tabs */}
 <Route
   path="/admin/*"
                      element={withPageBoundary(
                        "/admin",
                        "The admin area is refreshing",
                        <AdminTabRedirect />
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
                      path="/page-builder"
                      element={withPageBoundary(
                        "/page-builder",
                        "Page builder is refreshing",
                        <PageBuilderPage />
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
                    {/* Compatibility alias for older E2E/deep links */} <Route
                      path="/commercial-license"
                      element={<Navigate to="/commercial/license" replace />}
                    />
                    {/* P3-T14 value executive surface */}
                    <Route
                      path="/value/executive"
                      element={withPageBoundary(
                        "/value/executive",
                        "Value executive dashboard is refreshing",
                        <ValueExecutiveDashboardPage />
                      )}
                    />
                    {/* P3-T15 widget schema-drift root-cause proof */}
                    <Route
                      path="/dashboard/widgets/schema-drift"
                      element={withPageBoundary(
                        "/dashboard/widgets/schema-drift",
                        "Widget schema-drift proof is refreshing",
                        <P3T15WidgetSchemaDriftPage />
                      )}
                    />
                    {/* Default */}
                    <Route
                      path="*"
                      element={<Navigate to="/dashboard" replace />}
                    />
                                    <Route path="/value/scenario" element={<ValueScenarioPage />} />
                </Route>
                                    <Route
                      path="/executive"
                      element={withPageBoundary(
                        "/executive",
                        "Executive dashboard is refreshing",
                        <ExecutivePersonaDashboardPage />
                      )}
                    />
                    <Route path="/phase9/executive" element={<Navigate to="/executive" replace />} />
                    <Route
                      path="/access-matrix"
                      element={withPageBoundary(
                        "/access-matrix",
                        "Persona access matrix is refreshing",
                        <PersonaAccessMatrixPage />
                      )}
                    />
                       {/* M1-08: legacy path redirects (Naming Golden Rule) */}
                    <Route path="/phase9/access" element={<Navigate to="/access-matrix" replace />} />
                    <Route path="/assistant-config" element={<Navigate to="/assistant/configuration" replace />} />
                                      </Routes>
              </Suspense>
            </DashboardGridLayoutProvider>
          </DashboardSelectionProvider>
        </DashboardFilterProvider>
    </ErrorBoundary>
  );
}
// --- Root App ---
const ValueScenarioPage = lazy(() =>
  import("./pages/Phase7ValueScenario/Phase7ValueScenarioPage").then((module) => ({
    default: module.Phase7ValueScenarioPage,
  }))
);

const ValueExecutiveDashboardPage = lazy(() =>
  import("./pages/ValueExecutive/ValueExecutiveDashboardPage").then((m) => ({
    default: m.ValueExecutiveDashboardPage,
  }))
);

const P3T15WidgetSchemaDriftPage = lazy(() =>
  import("./pages/Dashboard/P3T15WidgetSchemaDriftPage").then((m) => ({
    default: m.P3T15WidgetSchemaDriftPage,
  }))
);
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

