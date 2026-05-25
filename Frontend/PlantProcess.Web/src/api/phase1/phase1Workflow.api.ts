import { API_BASE_URL } from "../apiConfig";

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "GET",
    headers: { Accept: "application/json" },
    credentials: "include",
  });

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    throw new Error(text || `GET ${path} failed with ${response.status}`);
  }

  return (await response.json()) as T;
}

async function postJson<TResponse, TBody = unknown>(
  path: string,
  body: TBody
): Promise<TResponse> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    credentials: "include",
    body: JSON.stringify(body ?? {}),
  });

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    throw new Error(text || `POST ${path} failed with ${response.status}`);
  }

  return (await response.json()) as TResponse;
}

export type ConnectorProviderTruthRow = {
  sortOrder: number;
  providerType: string;
  displayName: string;
  description: string;
  isImplemented: boolean;
  isDemoCertified: boolean;
  isAvailableNow: boolean;
  requiresSecretReference: boolean;
  supportsConnectionTest: boolean;
  supportsSchemaDiscovery: boolean;
  supportsSnapshotImport: boolean;
  supportsIncrementalImport: boolean;
  statusLabel: string;
  limitation: string;
  activeConnectionProfiles: number;
  totalConnectionProfiles: number;
  activeSourceDatasets: number;
  totalSourceDatasets: number;
};

export type ConnectorTruthMatrixResponse = {
  generatedAtUtc: string;
  operatingRule: string;
  providers: ConnectorProviderTruthRow[];
};

export type SourceScheduleRow = {
  sourceDatasetDefinitionId: string;
  connectionProfileId: string;
  connectionProfileCode: string;
  connectionProfileName: string;
  providerType: string;
  sourceSystemDefinitionId: string;
  sourceSystemCode: string;
  sourceSystemName: string;
  datasetCode: string;
  datasetName: string;
  datasetKind: string;
  sourceSchemaName: string | null;
  sourceObjectName: string;
  primaryTimestampField: string | null;
  incrementalCursorField: string | null;
  lastCursorValue: string | null;
  refreshIntervalSeconds: number;
  nextRunAtUtc: string | null;
  isDatasetActive: boolean;
  isConnectionActive: boolean;
  isDueNow: boolean;
  description: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
};

export type SourceScheduleBoardResponse = {
  generatedAtUtc: string;
  totalDatasets: number;
  dueNowDatasets: number;
  rows: SourceScheduleRow[];
};

export type RunDueSourceImportsResponse = {
  completedAtUtc: string;
  maxDatasetsPerRun: number;
  maxRowsPerDataset: number;
  durationMs: number;
  datasetsProcessed: number;
  totalRowsImported: number;
  datasetsFailedCount: number;
  datasetResults: Array<{
    datasetId: string;
    datasetCode: string;
    rowsImported: number;
    errorMessage: string | null;
  }>;
};

export type StagingSummaryRow = {
  importBatchId: string;
  sourceSystemDefinitionId: string;
  importBatchCode: string;
  importType: string;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  sourceObjectName: string | null;
  fileName: string | null;
  rowCount: number | null;
  errorMessage: string | null;
  stagingRecordCount: number;
  pendingCount: number;
  mappedCount: number;
  failedCount: number;
  skippedCount: number;
};

export type StagingSummaryResponse = {
  generatedAtUtc: string;
  message: string;
  rows: StagingSummaryRow[];
};

export type StagingRecordRow = {
  id: string;
  importBatchId: string;
  sourceObjectName: string;
  rowNumber: number;
  rawJson: string;
  isProcessed: boolean;
  processedAtUtc: string | null;
  processingStatus: string;
  processingError: string | null;
  canonicalEntityId: string | null;
  canonicalEntityName: string | null;
  sourceSystem: string | null;
  sourceRecordId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
};

export type StagingRecordsResponse = {
  generatedAtUtc: string;
  count: number;
  rows: StagingRecordRow[];
};

export type SchemaMappingWorkbenchResponse = {
  generatedAtUtc: string;
  message: string;
  datasets: Array<{
    id: string;
    datasetCode: string;
    datasetName: string;
    datasetKind: string;
    providerType: string;
    sourceSchemaName: string | null;
    sourceObjectName: string;
    isActive: boolean;
  }>;
  sourceFields: Array<{
    id: string;
    sourceDatasetDefinitionId: string;
    fieldName: string;
    displayName: string;
    sourceDataType: string;
    ordinal: number;
    isNullable: boolean;
    sampleValue: string | null;
    isPrimaryKeyCandidate: boolean;
    isTimestampCandidate: boolean;
    isActive: boolean;
  }>;
  canonicalTargets: Array<{
    entityName: string;
    fieldName: string;
    dataType: string;
    isRequired: boolean;
    description: string;
  }>;
  mappings: Array<{
    id: string;
    mappingCode: string;
    mappingName: string;
    sourceObjectName: string;
    targetEntityName: string;
    mappingJson: string;
    mappingVersion: string;
    isActive: boolean;
    description: string | null;
  }>;
  schemaViews: Array<{
    id: string;
    schemaViewCode: string;
    schemaViewName: string;
    viewKind: string;
    primarySourceDatasetDefinitionId: string | null;
    sourceDatasetIdsJson: string;
    isApproved: boolean;
    isActive: boolean;
    lastValidationStatus: string | null;
    lastValidationMessage: string | null;
  }>;
};

export type SchemaViewPreviewResponse = {
  isSuccess: boolean;
  message: string;
  rowCount: number;
  durationMs: number;
  columns: Array<{
    columnName: string;
    dataType: string;
    ordinal: number;
  }>;
  rows: Record<string, unknown>[];
};

export type ImportJobConfigurationBoardResponse = {
  generatedAtUtc: string;
  message: string;
  mappingCandidates: Array<{
    mappingDefinitionId: string;
    mappingCode: string;
    mappingName: string;
    sourceObjectName: string;
    targetEntityName: string;
    isMappingActive: boolean;
    existingJobDefinitionId: string | null;
    existingJobCode: string | null;
    hasEnabledJob: boolean;
    existingScheduleExpression: string | null;
    lastRunStatus: string | null;
    nextRunAtUtc: string | null;
  }>;
  existingImportJobs: Array<{
    jobDefinitionId: string;
    jobCode: string;
    jobName: string;
    jobType: string;
    targetId: string | null;
    targetType: string | null;
    scheduleExpression: string;
    isEnabled: boolean;
    lastRunStatus: string;
    lastRunStartedAtUtc: string | null;
    lastRunCompletedAtUtc: string | null;
    lastRunDurationMs: number | null;
    lastFailureReason: string | null;
    nextRunAtUtc: string | null;
    description: string | null;
    createdAtUtc: string;
    updatedAtUtc: string | null;
  }>;
};

export const phase1WorkflowApi = {
  getConnectorTruth: () =>
    getJson<ConnectorTruthMatrixResponse>("/admin/phase1/connector-truth"),

  getSourceScheduleBoard: () =>
    getJson<SourceScheduleBoardResponse>("/admin/phase1/source-schedule-board"),

  runDueSourceImports: (maxDatasetsPerRun = 25, maxRowsPerDataset = 5000) =>
    postJson<RunDueSourceImportsResponse>("/admin/phase1/run-due-source-imports", {
      maxDatasetsPerRun,
      maxRowsPerDataset,
    }),

  scheduleSourceDatasetNow: (sourceDatasetDefinitionId: string) =>
    postJson<SourceScheduleRow>(
      `/admin/phase1/source-datasets/${sourceDatasetDefinitionId}/schedule-now`,
      {}
    ),

  updateDatasetCursor: (
    sourceDatasetDefinitionId: string,
    lastCursorValue: string | null
  ) =>
    postJson<SourceScheduleRow>(
      `/admin/phase1/source-datasets/${sourceDatasetDefinitionId}/cursor`,
      { lastCursorValue }
    ),

  getStagingSummary: (sourceObjectName?: string | null) => {
    const query = sourceObjectName
      ? `?sourceObjectName=${encodeURIComponent(sourceObjectName)}`
      : "";

    return getJson<StagingSummaryResponse>(`/admin/phase1/staging/summary${query}`);
  },

  getStagingRecords: (params?: {
    importBatchId?: string | null;
    sourceObjectName?: string | null;
    processingStatus?: string | null;
    take?: number | null;
  }) => {
    const search = new URLSearchParams();

    if (params?.importBatchId) search.set("importBatchId", params.importBatchId);
    if (params?.sourceObjectName) search.set("sourceObjectName", params.sourceObjectName);
    if (params?.processingStatus) search.set("processingStatus", params.processingStatus);
    if (params?.take) search.set("take", String(params.take));

    const query = search.toString() ? `?${search}` : "";

    return getJson<StagingRecordsResponse>(`/admin/phase1/staging/records${query}`);
  },

  getSchemaMappingWorkbench: () =>
    getJson<SchemaMappingWorkbenchResponse>(
      "/admin/phase1/schema-mapping/workbench"
    ),

  previewSchemaView: (sqlText: string, maxRows = 100, timeoutSeconds = 10) =>
    postJson<SchemaViewPreviewResponse>(
      "/admin/phase1/schema-mapping/preview-view",
      { sqlText, maxRows, timeoutSeconds }
    ),

  getImportJobConfigurationBoard: () =>
    getJson<ImportJobConfigurationBoardResponse>(
      "/admin/phase1/import-jobs/configuration-board"
    ),

  createImportJobFromMapping: (body: {
    mappingDefinitionId: string;
    jobCode?: string | null;
    jobName?: string | null;
    scheduleExpression?: string | null;
    isEnabled: boolean;
    description?: string | null;
    isSynthetic: boolean;
  }) =>
    postJson(
      "/admin/phase1/import-jobs/from-mapping",
      body
    ),
};