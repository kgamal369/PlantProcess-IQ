import { apiClient } from "@/api/http";

export interface EdgeCollectorHealth {
  status: string;
  component: string;
  marker: string;
  mode: string;
  noInboundOtAccessRequired: boolean;
  opensInboundListener: boolean;
  supportsRegistration: boolean;
  supportsHeartbeat: boolean;
  supportsBatchPush: boolean;
  supportsQueueStatus: boolean;
}

export interface EdgeCollectorContract {
  contract: string;
  marker: string;
  safetyRules: string[];
  routes: string[];
}

export interface EdgeCollectorProfile {
  profileCode: string;
  displayName: string;
  direction: string;
  writesToSource: boolean;
  requiresInboundOtFirewallRule: boolean;
}

export interface EdgeCollectorProfilesResponse {
  profiles: EdgeCollectorProfile[];
}

export interface EdgeCollectorRegisterRequest {
  collectorId: string;
  displayName: string;
  siteName: string;
  networkZone: string;
  agentVersion: string;
  pushEndpointUrl: string;
  readOnlyCollection: boolean;
  outboundOnly: boolean;
  opensInboundListener: boolean;
  sourceProfiles?: string[];
}

export interface EdgeCollectorHeartbeatRequest {
  collectorId: string;
  agentVersion: string;
  observedAtUtc: string;
  status: string;
  localQueueDepth: number;
  failedPushCount: number;
  lastSuccessfulPushUtc?: string | null;
  lastError?: string | null;
}

export interface EdgeCollectorSample {
  sourceProfile: string;
  tagPath: string;
  timestampUtc: string;
  numericValue?: number | null;
  textValue?: string | null;
  unit?: string | null;
  quality: string;
}

export interface EdgeCollectorPushBatchRequest {
  collectorId: string;
  batchId: string;
  createdAtUtc: string;
  readOnlyCollection: boolean;
  outboundOnly: boolean;
  sequenceNumber: number;
  samples: EdgeCollectorSample[];
}

export interface EdgeCollectorQueueStatusRequest {
  collectorId: string;
  queueDepth: number;
  oldestItemAgeSeconds: number;
  failedPushCount: number;
  lastBatchSize: number;
  lastError?: string | null;
}

export interface EdgeCollectorCommandResult {
  isSuccess: boolean;
  message?: string;
  collectorId?: string;
  registeredAtUtc?: string;
  serverReceivedAtUtc?: string;
  acceptedAtUtc?: string;
  queueDepth?: number;
  status?: string;
  acceptedSamples?: number;
  readOnlyCollection?: boolean;
  outboundOnly?: boolean;
  noInboundOtAccessRequired?: boolean;
}

export interface EdgeCollectorState {
  collectorId: string;
  displayName: string;
  siteName: string;
  networkZone: string;
  agentVersion: string;
  readOnlyCollection: boolean;
  outboundOnly: boolean;
  opensInboundListener: boolean;
  sourceProfiles: string[];
  registeredAtUtc: string;
  lastHeartbeatUtc?: string | null;
  lastPushUtc?: string | null;
  localQueueDepth: number;
  failedPushCount: number;
  acceptedSamples: number;
  status: string;
  lastError?: string | null;
}

export interface EdgeCollectorStatusResponse {
  generatedAtUtc: string;
  collectorCount: number;
  collectors: EdgeCollectorState[];
}

export const edgeCollectorApi = {
  health() {
    return apiClient.get<EdgeCollectorHealth>("/api/v5/edge-collector/health");
  },

  contract() {
    return apiClient.get<EdgeCollectorContract>("/api/v5/edge-collector/contract");
  },

  profiles() {
    return apiClient.get<EdgeCollectorProfilesResponse>("/api/v5/edge-collector/profiles");
  },

  register(request: EdgeCollectorRegisterRequest) {
    return apiClient.post<EdgeCollectorCommandResult>("/api/v5/edge-collector/register", request);
  },

  heartbeat(request: EdgeCollectorHeartbeatRequest) {
    return apiClient.post<EdgeCollectorCommandResult>("/api/v5/edge-collector/heartbeat", request);
  },

  pushBatch(request: EdgeCollectorPushBatchRequest) {
    return apiClient.post<EdgeCollectorCommandResult>("/api/v5/edge-collector/push-batch", request);
  },

  queueStatus(request: EdgeCollectorQueueStatusRequest) {
    return apiClient.post<EdgeCollectorCommandResult>("/api/v5/edge-collector/queue-status", request);
  },

  status() {
    return apiClient.get<EdgeCollectorStatusResponse>("/api/v5/edge-collector/status");
  },
};
