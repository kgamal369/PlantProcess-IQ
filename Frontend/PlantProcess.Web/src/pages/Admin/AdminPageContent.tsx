
// Consolidated Administrator workspace.
//
// This is the single admin surface for the user journey:
//   DB Configuration  -> create/test DB-Link connections, pick tables/columns (journey steps 1-2)
//   Schema Config     -> no-code mapping, views, business keys, SQL view editor, KPI defs (step 6)
//   Importing Data    -> Stage-1/Stage-2 pipeline view + canonical refresh scheduling (steps 3, 7)
//   Jobs Monitor      -> all job types with run-now / pause / resume / history (steps 4, 8)
//   Connector Truth   -> honest per-connector status
//
// It replaces the previous shallow orchestration page (Connector Truth | Import Jobs placeholder |
// Tier Override) which left the four functional tabs above orphaned and unreachable.

import { useCallback, useEffect, useState } from "react";
import { RefreshCw } from "lucide-react";
import {
  StandardButton,
  StandardCard,
  StandardTabs,
  type StandardTabItem,
} from "@/components/standard";
import {
  productApi,
  type AdminJobsMonitor,
  type DbConfigurationSummary,
  type SchemaConfigurationSummary,
  type TwoStageImportModel,
} from "../../api/productApiClient";
import { DbConfigurationTab } from "./AdminDbConfigurationTab";
import { SchemaConfigurationTab } from "./AdminSchemaConfigurationTab";
import { ImportingDataTab } from "./AdminImportingDataTab";
import { JobsMonitorTab } from "./AdminJobsMonitorTab";
import { DemoAnalyticsWorkflowTruthPage } from "../PlatformOps/DemoAnalyticsPages";
import "../PlatformOps/demo-analytics.css";

type AdminWorkspaceData = {
  db: DbConfigurationSummary | null;
  schema: SchemaConfigurationSummary | null;
  twoStage: TwoStageImportModel | null;
  jobs: AdminJobsMonitor | null;
};

const EMPTY: AdminWorkspaceData = { db: null, schema: null, twoStage: null, jobs: null };

export function AdminPageContent() {
  const [data, setData] = useState<AdminWorkspaceData>(EMPTY);
  const [isLoading, setIsLoading] = useState(true);
  const [tab, setTab] = useState("db-configuration");

  const load = useCallback(async () => {
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
    void load();
  }, [load]);

  const tabs: StandardTabItem[] = [
    {
      id: "db-configuration",
      label: "DB Configuration",
      content: <DbConfigurationTab data={data.db} onRefresh={load} />,
    },
    {
      id: "schema-configuration",
      label: "Schema Configuration",
      content: <SchemaConfigurationTab data={data.schema} />,
    },
    {
      id: "importing-data",
      label: "Importing Data",
      content: (
        <ImportingDataTab
          data={data.twoStage}
          schemaConfig={data.schema}
          jobs={data.jobs}
          onRefresh={load}
        />
      ),
    },
    {
      id: "jobs-monitor",
      label: "Jobs Monitor",
      content: <JobsMonitorTab data={data.jobs} onRefresh={load} />,
    },
    {
      id: "connector-truth",
      label: "Connector Truth",
      content: <DemoAnalyticsWorkflowTruthPage />,
    },
    {
      id: "license",
      label: "License",
      content: (
        <StandardCard
          title="License tier"
          subtitle="The active tier is controlled by a signed license token. It is activated at install or by support and cannot be edited from this screen."
        >
          <p>
            To change the plant license tier, activate a new signed license key. The effective
            tier and its feature gates apply immediately across the application.
          </p>
        </StandardCard>
      ),
    },
  ];

  return (
    <main className="demo-analytics-page" data-admin-workspace="administrator">
      <header className="demo-analytics-header">
        <div className="demo-analytics-title">
          <h1>Administrator</h1>
          <p>
            Connect data sources, map them to the canonical model, schedule imports, and monitor
            every job from one place.
          </p>
        </div>
        <div className="demo-analytics-toolbar">
          <StandardButton
            variant="secondary"
            leadingIcon={<RefreshCw size={16} />}
            onClick={() => void load()}
            isLoading={isLoading}
          >
            Refresh
          </StandardButton>
        </div>
      </header>

      <StandardTabs
        items={tabs}
        value={tab}
        onChange={setTab}
        searchParam="adminTab"
        ariaLabel="Administrator workspace tabs"
        lazy
      />
    </main>
  );
}
