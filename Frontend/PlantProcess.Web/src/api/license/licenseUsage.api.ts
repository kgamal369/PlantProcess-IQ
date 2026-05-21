import { apiClient } from "../http";
import type { LicenseStatus } from "./license.api";

export interface LicenseUsageCounters {
  activeSources: number;
  activeDatasets: number;
  activeJobs: number;
  activeDashboards: number;
  activeWidgets: number;
  schemaViews: number;
  activeSchemaViews: number;
  mappingDefinitions: number;
  activeMappings: number;
  kpiDefinitions: number;
  stagingRecords: number;
  importBatches: number;
  correlationResults: number;
  modelRegistryEntries: number;
}

export interface LicenseUsageLimits {
  maxDataSources: number | null;
  maxScheduledJobs: number | null;
  maxDashboards: number | null;
  minRefreshIntervalMinutes: number | null;
  maxPreviewRows: number | null;
  allowsSqlEditor: boolean;
  allowsKpiBuilder: boolean;
  allowsWidgetScriptLayer: boolean;
  allowsScheduledCorrelation: boolean;
  allowsMlLearningJobs: boolean;
  allowsBrandedReports: boolean;
}

export interface LicenseUsageRemaining {
  dataSources: number | null;
  scheduledJobs: number | null;
  dashboards: number | null;
}

export interface LicenseUsageResponse {
  generatedAtUtc: string;
  tier: string;
  usage: LicenseUsageCounters;
  limits: LicenseUsageLimits;
  remaining: LicenseUsageRemaining;
}

export interface CommercialReadinessCheck {
  code: string;
  name: string;
  isReady: boolean;
}

export interface CommercialReadinessDimension {
  status: string;
  checks: CommercialReadinessCheck[];
}

export interface CommercialReadinessResponse {
  generatedAtUtc: string;
  license: LicenseStatus;
  dimension5: CommercialReadinessDimension;
  dimension8: CommercialReadinessDimension;
  evidence: {
    activeSources: number;
    activeJobs: number;
    stagingRecords: number;
    activeSchemaViews: number;
    activeMappings: number;
    activeDashboards: number;
    activeWidgets: number;
    correlationResults: number;
    modelRegistryEntries: number;
  };
}

export const licenseUsageApi = {
  getUsage: () => apiClient.get<LicenseUsageResponse>("/admin/license/usage"),

  getCommercialReadiness: () =>
    apiClient.get<CommercialReadinessResponse>(
      "/admin/license/commercial-readiness"
    ),
};