import { getJson, postJson } from "@/api/legacy/legacyApiHardening";

export type CanonicalSchemaViewRow = {
  id: string;
  view_code: string;
  view_name: string;
  view_kind: string;
  target_entity: string;
  physical_schema: string;
  physical_view_name: string;
  sql_text: string;
  output_schema_json: string;
  mapping_json: string;
  source_dataset_ids: string;
  attached_scope_type: string | null;
  attached_scope_code: string | null;
  is_registered: boolean;
  is_approved: boolean;
  is_active: boolean;
  is_system_seed: boolean;
  last_validated_at_utc: string | null;
  last_validation_status: string | null;
  last_validation_message: string | null;
  last_executed_at_utc: string | null;
  last_execution_status: string | null;
  last_execution_message: string | null;
  last_execution_row_count: number | null;
  created_by: string | null;
  created_at_utc: string;
  updated_at_utc: string | null;
};

export type RegisterCanonicalSchemaViewRequest = {
  viewCode: string;
  viewName: string;
  viewKind: string;
  targetEntity: string;
  sqlText: string;
  physicalSchema?: string | null;
  physicalViewName?: string | null;
  outputSchemaJson?: string | null;
  mappingJson?: string | null;
  sourceDatasetIdsJson?: string | null;
  attachedScopeType?: string | null;
  attachedScopeCode?: string | null;
  isSystemSeed: boolean;
};

export type JoinColumnSelection = {
  side: "left" | "right";
  column: string;
  alias?: string | null;
};

export type CrossSourceJoinRequest = {
  leftSchema: string;
  leftTable: string;
  rightSchema: string;
  rightTable: string;
  leftJoinColumn: string;
  rightJoinColumn: string;
  columns: JoinColumnSelection[];
  maxRows?: number | null;
};

export type MaterializeJoinRequest = {
  viewCode: string;
  viewName: string;
  join: CrossSourceJoinRequest;
  targetEntity?: string | null;
  physicalSchema?: string | null;
  physicalViewName?: string | null;
  mappingJson?: string | null;
  sourceDatasetIdsJson?: string | null;
  attachedScopeType?: string | null;
  attachedScopeCode?: string | null;
};

export type KpiViewRequest = {
  viewCode: string;
  viewName: string;
  kpiCode: string;
  kpiName?: string | null;
  kpiCategory?: string | null;
  sqlText: string;
  physicalSchema?: string | null;
  physicalViewName?: string | null;
  unit?: string | null;
  valueExpression?: string | null;
  dimensionExpression?: string | null;
  filterExpression?: string | null;
  aggregationType?: string | null;
  kpiOptionsJson?: string | null;
  mappingJson?: string | null;
  attachedScopeType?: string | null;
  attachedScopeCode?: string | null;
  isSynthetic: boolean;
};

export type ExecuteMappingRequest = {
  executionMode?: string | null;
  previewOnly: boolean;
  stopOnFirstError: boolean;
};

export type PreviewResult = {
  isSuccess: boolean;
  message: string;
  sqlText: string;
  rowCount: number;
  columns: { columnName: string; dataType: string; ordinal: number }[];
  rows: Record<string, unknown>[];
};

export type MappingExecutionResult = {
  viewCode: string;
  targetEntity: string;
  qualifiedName: string;
  status: string;
  message: string;
  rowCount: number;
  durationMs: number;
};

export const schemaMappingApi = {
  getCatalog: () =>
    getJson<CanonicalSchemaViewRow[]>("/admin/schema-mapping/catalog"),

  getReadiness: () =>
    getJson<unknown>("/admin/schema-mapping/readiness"),

  registerView: (request: RegisterCanonicalSchemaViewRequest) =>
    postJson<CanonicalSchemaViewRow>(
      "/admin/schema-mapping/catalog/register",
      request
    ),

  resolve: (request: { viewCode?: string | null; targetEntity?: string | null }) =>
    postJson<unknown>("/admin/schema-mapping/resolve", request),

  previewJoin: (request: CrossSourceJoinRequest) =>
    postJson<PreviewResult>("/admin/schema-mapping/joins/preview", request),

  materializeJoin: (request: MaterializeJoinRequest) =>
    postJson<CanonicalSchemaViewRow>(
      "/admin/schema-mapping/joins/materialize",
      request
    ),

  createKpiView: (request: KpiViewRequest) =>
    postJson<CanonicalSchemaViewRow>("/admin/schema-mapping/kpi-views", request),

  executeMapping: (viewCode: string, request: ExecuteMappingRequest) =>
    postJson<MappingExecutionResult>(
      `/admin/schema-mapping/execute/${encodeURIComponent(viewCode)}`,
      request
    ),
};