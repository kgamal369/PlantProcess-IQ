import { apiClient } from "../http";
import type { Phase10Tier } from "@/license/phase10License";

export interface Phase10LifecycleRequest {
  tier: Phase10Tier;
  expiresAtUtc: string | null;
  nowUtc: string;
  graceDays: number | null;
  warningDays: number | null;
  usageCount: number | null;
  licensedLimit: number | null;
}

export interface Phase10OfflineEnvelope {
  licenseId: string;
  tenantId: string;
  tier: Phase10Tier;
  issuedAtUtc: string;
  expiresAtUtc: string;
  algorithm: string;
  publicKeyId: string;
  payloadHash: string;
  signature: string;
}

export const phase10LicenseApi = {
  getDemo: () => apiClient.get("/admin/license/phase10/demo"),
  evaluateLifecycle: (body: Phase10LifecycleRequest) =>
    apiClient.post("/admin/license/phase10/lifecycle/evaluate", body),
  createDemoEnvelope: (tier: Phase10Tier) =>
    apiClient.post("/admin/license/phase10/offline-activation/create-demo-envelope", {
      licenseId: "lic-phase10-ui-demo",
      tenantId: "tenant-ui-demo",
      tier,
      issuedAtUtc: null,
      expiresAtUtc: null,
    }),
  verifyOfflineEnvelope: (body: Phase10OfflineEnvelope) =>
    apiClient.post("/admin/license/phase10/offline-activation/verify", body),
};