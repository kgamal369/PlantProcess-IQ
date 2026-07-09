// ============================================================
// FILE: src/pages/DataIntegration/DataIntegrationRoutes.tsx
// M1-06: the four extracted tabs, now first-class routes.
// Each reads the layout's single load and adapts it to the tab component's
// existing prop contract. The tab components themselves are UNCHANGED.
// ============================================================
import { DbConfigurationTab } from "../Admin/AdminDbConfigurationTab";
import { SchemaConfigurationTab } from "../Admin/AdminSchemaConfigurationTab";
import { ImportingDataTab } from "../Admin/AdminImportingDataTab";
import { JobsMonitorTab } from "../Admin/AdminJobsMonitorTab";
import { useDataIntegration } from "./DataIntegrationLayout";

export function ConnectionsRoute() {
  const { data, refresh } = useDataIntegration();
  return <DbConfigurationTab data={data.db} onRefresh={refresh} />;
}

export function TableRegistryRoute() {
  const { data } = useDataIntegration();
  return <SchemaConfigurationTab data={data.schema} />;
}

export function ImportingRoute() {
  const { data, refresh } = useDataIntegration();
  return (
    <ImportingDataTab
      data={data.twoStage}
      schemaConfig={data.schema}
      jobs={data.jobs}
      onRefresh={refresh}
    />
  );
}

export function JobsMonitorRoute() {
  const { data, refresh } = useDataIntegration();
  return <JobsMonitorTab data={data.jobs} onRefresh={refresh} />;
}