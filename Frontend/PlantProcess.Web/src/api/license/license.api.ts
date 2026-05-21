import { apiClient } from "../http";

export type LicenseTier = "Light" | "Pro" | "ProPlus" | "Enterprise";

export interface LicenseLimits {
  tier: LicenseTier;
  maxUsers: number | null;
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
  hasUnlimitedDataSources: boolean;
  hasUnlimitedScheduledJobs: boolean;
  hasUnlimitedDashboards: boolean;
}

export interface LicenseFeatureStatus {
  feature: string;
  isEnabled: boolean;
  requiredTier: string;
  message: string;
}

export interface LicenseStatus {
  tier: LicenseTier;
  displayName: string;
  isTrial: boolean;
  environment: string;
  source: string;
  effectiveFromUtc: string;
  limits: LicenseLimits;
  features: LicenseFeatureStatus[];
  allowedConnectorProviderTypes: string[];
  blockedConnectorProviderTypes: string[];
}

export const licenseApi = {
  getCurrent: () => apiClient.get<LicenseStatus>("/admin/license/current"),

  getFeatures: () =>
    apiClient.get<LicenseFeatureStatus[]>("/admin/license/features"),

  getLimits: () => apiClient.get<LicenseLimits>("/admin/license/limits"),
};