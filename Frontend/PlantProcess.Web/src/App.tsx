import { Navigate, Route, Routes } from "react-router-dom";
import { AppLayout } from "./components/AppLayout";
import { DashboardPage } from "./pages/DashboardPage";
import { MaterialInvestigationPage } from "./pages/MaterialInvestigationPage";
import { RiskDashboardPage } from "./pages/RiskDashboardPage";
import { DataQualityPage } from "./pages/DataQualityPage";
import { CorrelationPage } from "./pages/CorrelationPage";
import { DashboardFilterProvider } from "./state/DashboardFilterContext";
import "./index.css";

export default function App() {
  return (
    <DashboardFilterProvider>
      <Routes>
        <Route element={<AppLayout />}>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/materials" element={<MaterialInvestigationPage />} />
          <Route path="/risk" element={<RiskDashboardPage />} />
          <Route path="/data-quality" element={<DataQualityPage />} />
          <Route path="/correlations" element={<CorrelationPage />} />
        </Route>
      </Routes>
    </DashboardFilterProvider>
  );
}