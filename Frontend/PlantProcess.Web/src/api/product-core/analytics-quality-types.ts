// PlantProcess IQ Phase 5B-1 domain type module.
// Generated from productCoreApiClient.implementation.ts exported DTO/filter/read-model declarations.
// Runtime API behavior remains in productCoreApiClient.implementation.ts.


// ============================================================
// Phase 2 Admin Area Foundation DTOs
// ============================================================

export interface AdminMetricCard {
  label: string;
  value: number;
  note: string;
  group: string;
}

export interface KpiDefinitionRecord {
  id: string;
  schemaViewDefinitionId: string | null;
  kpiCode: string;
  kpiName: string;
  kpiCategory: string;
  valueExpression: string;
  unit: string | null;
  dimensionExpression: string | null;
  filterExpression: string | null;
  aggregationType: string;
  kpiOptionsJson: string;
  isActive: boolean;
  description: string | null;
  isSynthetic: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateKpiDefinitionRequest {
  schemaViewDefinitionId?: string | null;
  kpiCode: string;
  kpiName: string;
  kpiCategory: string;
  valueExpression: string;
  unit?: string | null;
  dimensionExpression?: string | null;
  filterExpression?: string | null;
  aggregationType?: string | null;
  kpiOptionsJson?: string | null;
  description?: string | null;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
}
