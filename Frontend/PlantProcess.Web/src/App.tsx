import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";
import { AppLayout } from "./components/AppLayout";
import { LoadingPanel } from "./components/AsyncState";
import { DashboardFilterProvider } from "./state/DashboardFilterContext";
import { DashboardSelectionProvider } from "./state/DashboardSelectionContext";
import { DashboardGridLayoutProvider } from "./state/DashboardGridLayoutContext";
import { ThemeProvider } from "./state/ThemeContext";
import { DemoModeProvider } from "./state/DemoModeContext";
import "./index.css";

const DashboardPage = lazy(() =>
  import("./pages/DashboardPage").then((module) => ({
    default: module.DashboardPage,
  }))
);

const MaterialInvestigationPage = lazy(() =>
  import("./pages/MaterialInvestigationPage").then((module) => ({
    default: module.MaterialInvestigationPage,
  }))
);

const RiskDashboardPage = lazy(() =>
  import("./pages/RiskDashboardPage").then((module) => ({
    default: module.RiskDashboardPage,
  }))
);

const DataQualityPage = lazy(() =>
  import("./pages/DataQualityPage").then((module) => ({
    default: module.DataQualityPage,
  }))
);

const CorrelationPage = lazy(() =>
  import("./pages/CorrelationPage").then((module) => ({
    default: module.CorrelationPage,
  }))
);

const AdminPage = lazy(() =>
  import("./pages/AdminPage").then((module) => ({
    default: module.AdminPage,
  }))
);

const DemoLifecyclePage = lazy(() =>
  import("./pages/DemoLifecycle/DemoLifecyclePage").then((module) => ({
    default: module.DemoLifecyclePage,
  }))
);

export default function App() {
  return (
    <ThemeProvider>
      <DemoModeProvider>
        <DashboardFilterProvider>
          <DashboardSelectionProvider>
            <DashboardGridLayoutProvider>
              <Suspense
                fallback={
                  <LoadingPanel text="Loading PlantProcess IQ workspace..." />
                }
              >
                <Routes>
                  <Route element={<AppLayout />}>
                    <Route index element={<Navigate to="/dashboard" replace />} />
                    <Route path="/dashboard" element={<DashboardPage />} />
                    <Route path="/materials" element={<MaterialInvestigationPage />} />
                    <Route path="/risk" element={<RiskDashboardPage />} />
                    <Route path="/data-quality" element={<DataQualityPage />} />
                    <Route path="/correlations" element={<CorrelationPage />} />
                    <Route path="/admin/*" element={<AdminPage />} />
                    <Route path="/demo-lifecycle" element={<DemoLifecyclePage />} />
                  </Route>
                </Routes>
              </Suspense>
            </DashboardGridLayoutProvider>
          </DashboardSelectionProvider>
        </DashboardFilterProvider>
      </DemoModeProvider>
    </ThemeProvider>
  );
}