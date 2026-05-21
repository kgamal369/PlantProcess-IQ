import { apiClient } from "../http";
import type { LicenseStatus } from "../license";

export interface DemoLifecycleStep {
  order: number;
  code: string;
  title: string;
  status: string;
  requiredLicenseTier: string;
  description: string;
  evidenceEndpoint: string;
}

export interface DemoConnectorStatus {
  providerType: string;
  displayName: string;
  implementationStatus: string;
  licenseStatus: string;
  isAvailableNow: boolean;
  isAllowedByLicense: boolean;
  isSafeForDemo: boolean;
  message: string;
}

export interface DemoConnectorTruth {
  connectors: DemoConnectorStatus[];
}

export interface DemoStagingSummary {
  activeConnectionProfiles: number;
  activeDatasets: number;
  stagingRecordCount: number;
  importBatchCount: number;
  lastSnapshotUtc: string | null;
  status: string;
  message: string;
}

export interface DemoSchemaMappingSummary {
  schemaViewCount: number;
  activeSchemaViewCount: number;
  mappingDefinitionCount: number;
  activeMappingDefinitionCount: number;
  kpiDefinitionCount: number;
  status: string;
  message: string;
}

export interface DemoJobStatus {
  jobId: string;
  jobCode: string;
  jobName: string;
  jobType: string;
  isEnabled: boolean;
  lastRunStatus: string;
  lastRunStartedAtUtc: string | null;
  lastRunFinishedAtUtc: string | null;
  lastRunDurationMs: number | null;
  licenseStatus: string;
  operationalRole: string;
}

export interface DemoJobChain {
  totalJobs: number;
  enabledJobs: number;
  failedOrTimeoutJobs: number;
  jobs: DemoJobStatus[];
}

export interface DemoDashboardOutputSummary {
  dashboardCount: number;
  activeDashboardCount: number;
  widgetCount: number;
  activeWidgetCount: number;
  status: string;
  message: string;
}

export interface DemoMlReadinessSummary {
  modelStatus: string;
  trainingStatus: string;
  currentIntelligence: string;
  requiredBeforeTraining: string;
  parameterObservationCount: number;
  qualityEventCount: number;
  genealogyEdgeCount: number;
  correlationResultCount: number;
  modelRegistryCount: number;
  readinessStatus: string;
  warnings: string[];
}

export interface DemoReportCloseSummary {
  reportPurpose: string;
  dataDiagnosticBridge: string;
  disclaimer: string;
  includedSections: string[];
}

export interface DemoLifecycle {
  generatedAtUtc: string;
  demoMode: string;
  license: LicenseStatus;
  steps: DemoLifecycleStep[];
  connectorTruth: DemoConnectorTruth;
  stagingSummary: DemoStagingSummary;
  schemaMapping: DemoSchemaMappingSummary;
  jobChain: DemoJobChain;
  dashboardOutput: DemoDashboardOutputSummary;
  mlReadiness: DemoMlReadinessSummary;
  reportClose: DemoReportCloseSummary;
}

export const demoLifecycleApi = {
  getLifecycle: () => apiClient.get<DemoLifecycle>("/demo/lifecycle"),
};