// ============================================================
// FILE: src/pages/DataIntegration/AdminTabRedirect.tsx
// M1-06: StandardTabs used searchParam="adminTab", so links of the form
//   /admin?adminTab=jobs-monitor
// exist in bookmarks, runbooks and the deck. Map every old tab id to its new
// route. Unknown ids fall through to the slimmed Administrator page.
// ============================================================
import { Navigate, useSearchParams } from "react-router-dom";
import { AdminPage } from "../Admin/AdminPage";

const MOVED: Record<string, string> = {
  "db-configuration": "/data-integration/connections",
  "schema-configuration": "/data-integration/registry",
  "importing-data": "/data-integration/importing",
  "jobs-monitor": "/data-integration/jobs",
  "connector-truth": "/data-integration/connector-truth",
};

export function AdminTabRedirect() {
  const [params] = useSearchParams();
  const tab = params.get("adminTab");
  const target = tab ? MOVED[tab] : undefined;
  if (target) return <Navigate to={target} replace />;
  return <AdminPage />;
}