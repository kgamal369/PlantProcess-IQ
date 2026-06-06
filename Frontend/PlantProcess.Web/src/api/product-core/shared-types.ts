// PlantProcess IQ Phase 5B-1 domain type module.
// Generated from productCoreApiClient.implementation.ts exported DTO/filter/read-model declarations.
// Runtime API behavior remains in productCoreApiClient.implementation.ts.


// PlantProcess IQ Phase 5B-1
// Exported product-core API DTO/filter/read-model types extracted from productCoreApiClient.implementation.ts.
// Runtime behavior stays in the implementation file; this module is type-only.
export type SortDirection = "asc" | "desc";

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  sortBy?: string;
  sortDirection?: SortDirection;
}

export interface ReferenceItem {
  id: string;
  code: string;
  name: string;
  group?: string;
  count: number;
}

// ============================================================
// Phase 3 Connector Foundation DTOs
// ============================================================

export interface ProviderTypeRecord {
  providerType: string;
  displayName: string;
  description: string;
  isAvailableNow: boolean;
  requiresSecretReference: boolean;
  supportsSchemaDiscovery: boolean;
  supportsSnapshotImport: boolean;
  supportsIncrementalImport: boolean;
}

export interface ConnectionProfileRecord {
  id: string;
  sourceSystemDefinitionId: string;
  sourceSystemCode: string;
  sourceSystemName: string;
  connectionProfileCode: string;
  connectionProfileName: string;
  providerType: string;
  connectionMode: string;
  hostName: string | null;
  port: number | null;
  databaseName: string | null;
  schemaName: string | null;
  fileRootPath: string | null;
  apiBaseUrl: string | null;
  secretReference: string | null;
  connectionOptionsJson: string;
  isActive: boolean;
  readOnlyEnforced: boolean;
  description: string | null;
  lastTestedAtUtc: string | null;
  lastTestStatus: string | null;
  lastTestMessage: string | null;
  isSynthetic: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateConnectionProfileRequest {
  sourceSystemDefinitionId: string;
  connectionProfileCode: string;
  connectionProfileName: string;
  providerType: string;
  connectionMode?: string | null;
  hostName?: string | null;
  port?: number | null;
  databaseName?: string | null;
  schemaName?: string | null;
  fileRootPath?: string | null;
  apiBaseUrl?: string | null;
  secretReference?: string | null;
  connectionOptionsJson?: string | null;
  readOnlyEnforced?: boolean | null;
  description?: string | null;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
}

export interface CsvPreviewRequest {
  csvText: string;
  delimiter?: string | null;
  hasHeader?: boolean | null;
  maxRows?: number | null;
}

export interface CsvPreviewResult {
  delimiter: string;
  hasHeader: boolean;
  headers: string[];
  rows: Record<string, string | null>[];
}
