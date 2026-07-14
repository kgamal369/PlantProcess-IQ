// M1-04: step-4 mapping author API. Wraps endpoints that already exist.
import { apiClient } from "@/api/http";

export type ImportBatch = {
  id: string;
  sourceSystemDefinitionId: string;
  sourceObjectName: string;
  sourceSystem?: string | null;
  status?: string | null;
  startedAtUtc?: string | null;
};

export type CreateMappingBody = {
  sourceSystemDefinitionId: string;
  mappingCode: string;
  mappingName: string;
  sourceObjectName: string;
  targetEntityName: string;
  mappingJson: string;
  mappingVersion: string;
  description: string;
  isSynthetic: boolean;
  sourceSystem: string | null;
  sourceRecordId: string | null;
};

export type CreateMappingResponse = { id: string };
export type ExecuteResult = Record<string, unknown>;

export function listImportBatches(): Promise<ImportBatch[]> {
  return apiClient.get<ImportBatch[]>("/integration/import-batches");
}

export function createMappingDefinition(body: CreateMappingBody): Promise<CreateMappingResponse> {
  return apiClient.post<CreateMappingResponse>("/integration/mapping-definitions", body);
}

export function executeMapping(mappingId: string, importBatchId: string): Promise<ExecuteResult> {
  const q = new URLSearchParams({ importBatchId, stopOnFirstError: "false" }).toString();
  return apiClient.post<ExecuteResult>(`/integration/mapping-definitions/${mappingId}/execute?${q}`);
}