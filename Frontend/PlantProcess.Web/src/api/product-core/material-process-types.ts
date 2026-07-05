// PlantProcess IQ Phase 5B-1 domain type module.
// Generated from productCoreApiClient.implementation.ts exported DTO/filter/read-model declarations.
// Runtime API behavior remains in productCoreApiClient.implementation.ts.


export interface GenealogyAwareCorrelationResult {
  generatedAtUtc: string;
  parameterCode: string;
  parameterName: string;
  unitOfMeasure?: string;
  defectType: string;
  linkMode: string;
  genealogyDepth: number;
  baselineDefectRatePercent: number;
  totalObservationCount: number;
  totalMaterialCount: number;
  totalDefectLinkedObservationCount: number;
  bins: GenealogyAwareCorrelationBin[];
  message: string;
}

export interface GenealogyAwareCorrelationBin {
  binNo: number;
  binLabel: string;
  minValue: number;
  maxValue: number;
  observationCount: number;
  materialCount: number;
  defectLinkedObservationCount: number;
  defectRatePercent: number;
  liftVsBaseline?: number | null;
  confidence: string;
}

export interface MaterialInvestigationRequestOptions {
  maxDepth?: number;
  parameterPage?: number;
  parameterPageSize?: number;
}
