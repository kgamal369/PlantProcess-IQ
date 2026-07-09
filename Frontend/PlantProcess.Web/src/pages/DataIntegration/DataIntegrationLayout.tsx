// ============================================================
// FILE: src/pages/DataIntegration/DataIntegrationLayout.tsx
// M1-06: the Data Integration area.
//
// Connecting a database is not an administration task. This layout owns the
// single Promise.allSettled load that AdminPageContent used to own, and hands
// each child route its slice via <Outlet context>. One fetch, real URLs.
//
// ImportingDataTab needs three slices (twoStage + schema + jobs), which is why
// the data lives here rather than in four independent route components.
// ============================================================
import { useCallback, useEffect, useState } from "react";
import { Outlet, useOutletContext } from "react-router-dom";
import { RefreshCw } from "lucide-react";
import { StandardButton, StandardPageHeader } from "@/components/standard";
import {
  productApi,
  type AdminJobsMonitor,
  type DbConfigurationSummary,
  type SchemaConfigurationSummary,
  type TwoStageImportModel,
} from "../../api/productApiClient";

export type DataIntegrationData = {
  db: DbConfigurationSummary | null;
  schema: SchemaConfigurationSummary | null;
  twoStage: TwoStageImportModel | null;
  jobs: AdminJobsMonitor | null;
};

export type DataIntegrationContext = {
  data: DataIntegrationData;
  isLoading: boolean;
  refresh: () => Promise<void>;
};

const EMPTY: DataIntegrationData = { db: null, schema: null, twoStage: null, jobs: null };

/** Child routes read the shared load through this hook. */
export function useDataIntegration(): DataIntegrationContext {
  return useOutletContext<DataIntegrationContext>();
}

export function DataIntegrationLayout() {
  const [data, setData] = useState<DataIntegrationData>(EMPTY);
  const [isLoading, setIsLoading] = useState(true);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    const [db, schema, twoStage, jobs] = await Promise.allSettled([
      productApi.getAdminDbConfigurationSummary(),
      productApi.getAdminSchemaConfigurationSummary(),
      productApi.getAdminTwoStageImportModel(),
      productApi.getAdminJobsMonitor(),
    ]);
    setData({
      db: db.status === "fulfilled" ? db.value : null,
      schema: schema.status === "fulfilled" ? schema.value : null,
      twoStage: twoStage.status === "fulfilled" ? twoStage.value : null,
      jobs: jobs.status === "fulfilled" ? jobs.value : null,
    });
    setIsLoading(false);
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const context: DataIntegrationContext = { data, isLoading, refresh };

  return (
    <div data-testid="data-integration-area">
      <StandardPageHeader
        title="Data Integration"
        subtitle="Connect plant sources, map them to the canonical model, run imports and watch every job."
        description="Everything needed to get plant data into PlantProcess IQ. Connections are read-only toward your source systems at all times."
        actions={
          <StandardButton
            variant="secondary"
            leadingIcon={<RefreshCw size={16} />}
            onClick={() => void refresh()}
            isLoading={isLoading}
          >
            Refresh
          </StandardButton>
        }
      />
      <Outlet context={context} />
    </div>
  );
}