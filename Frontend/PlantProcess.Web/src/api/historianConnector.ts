import { apiClient } from "@/api/http";

export interface HistorianProviderInfo {
  providerType: string;
  displayName: string;
  availability: string;
  readOnly: boolean;
  writeMethodsExposed: boolean;
  description: string;
  aliases: string[];
  routes: string[];
}

export interface HistorianHealth {
  status: string;
  component: string;
  marker: string;
  providerType: string;
  mode: string;
  supportsConnectionTest: boolean;
  supportsTagBrowse: boolean;
  supportsBoundedRead: boolean;
  supportsMappingHints: boolean;
  liveVendorHandshake: string;
}

export interface HistorianConnectionTestRequest {
  providerType?: string;
  endpointUrl?: string;
  namespaceUri?: string;
  securityMode?: string;
  readOnly?: boolean;
  requireLiveHandshake?: boolean;
  seedTags?: string[];
}

export interface HistorianConnectionTestResult {
  isSuccess: boolean;
  message: string;
  providerType: string;
  endpointUrl?: string;
  namespaceUri?: string;
  securityMode?: string;
  readOnly?: boolean;
  testedAtUtc?: string;
  sampleTags?: string[];
  liveHandshake?: string;
}

export interface HistorianBrowseTagsRequest {
  endpointUrl?: string;
  namespaceUri?: string;
  pathPrefix?: string;
  maxTags?: number;
}

export interface HistorianTagDto {
  tagPath: string;
  displayName: string;
  unit: string;
  dataType: string;
  suggestedCanonicalGroup: string;
  isTimestampCandidate: boolean;
  isQualityCandidate: boolean;
  isProcessMeasurementCandidate: boolean;
}

export interface HistorianBrowseTagsResponse {
  providerType: string;
  endpointUrl?: string;
  namespaceUri?: string;
  mode: string;
  tags: HistorianTagDto[];
}

export interface HistorianReadWindowRequest {
  tagPaths?: string[];
  fromUtc?: string;
  toUtc?: string;
  maxPointsPerTag?: number;
}

export interface HistorianPointDto {
  tagPath: string;
  timestampUtc: string;
  value: number;
  unit: string;
  quality: string;
}

export interface HistorianReadWindowResponse {
  providerType: string;
  mode: string;
  readOnly: boolean;
  fromUtc: string;
  toUtc: string;
  maxPointsPerTag: number;
  tagCount: number;
  pointCount: number;
  points: HistorianPointDto[];
}

export interface HistorianMappingHintsRequest {
  tagPaths?: string[];
  materialKeyTag?: string;
  timestampTag?: string;
  qualityTag?: string;
}

export interface HistorianMappingHintDto {
  tagPath: string;
  sourceDataType: string;
  suggestedCanonicalGroup: string;
  suggestedFieldName: string;
  isTimestampCandidate: boolean;
  isBusinessKeyCandidate: boolean;
  isQualityCandidate: boolean;
  isProcessMeasurementCandidate: boolean;
}

export interface HistorianMappingHintsResponse {
  providerType: string;
  mode: string;
  materialKeyTag?: string;
  timestampTag?: string;
  qualityTag?: string;
  hints: HistorianMappingHintDto[];
}

export const historianConnectorApi = {
  health() {
    return apiClient.get<HistorianHealth>("/api/v5/historian-connector/health");
  },

  provider() {
    return apiClient.get<HistorianProviderInfo>("/api/v5/historian-connector/provider");
  },

  testConnection(request: HistorianConnectionTestRequest) {
    return apiClient.post<HistorianConnectionTestResult>("/api/v5/historian-connector/test-connection", request);
  },

  browseTags(request: HistorianBrowseTagsRequest) {
    return apiClient.post<HistorianBrowseTagsResponse>("/api/v5/historian-connector/browse-tags", request);
  },

  readWindow(request: HistorianReadWindowRequest) {
    return apiClient.post<HistorianReadWindowResponse>("/api/v5/historian-connector/read-window", request);
  },

  mappingHints(request: HistorianMappingHintsRequest) {
    return apiClient.post<HistorianMappingHintsResponse>("/api/v5/historian-connector/mapping-hints", request);
  },
};
