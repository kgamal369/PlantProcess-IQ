import { apiClient } from "../http";

export interface MlReadinessMetric {
  code: string;
  name: string;
  currentValue: number;
  requiredValue: number;
  unit: string;
  isReady: boolean;
  status: string;
  message: string;
}

export interface MlReadinessScore {
  generatedAtUtc: string;
  overallStatus: string;
  scorePercent: number;
  canStartTraining: boolean;
  trainingStatus: string;
  honestPositioning: string;
  metrics: MlReadinessMetric[];
  blockers: string[];
  nextActions: string[];
}

export interface QualityTrainingLabel {
  materialUnitId: string;
  materialCode: string;
  materialUnitType: string;
  labelCode: string;
  hasDefect: boolean;
  isRejected: boolean;
  isDowngraded: boolean;
  isReworked: boolean;
  primaryDefectCode: string | null;
  primaryDefectName: string | null;
  primaryDefectCategory: string | null;
  qualityEventCount: number;
  upstreamObservationCount: number;
  genealogyEdgeCount: number;
  firstQualityEventAtUtc: string | null;
  lastObservationAtUtc: string | null;
}

export interface QualityTrainingLabelPreview {
  generatedAtUtc: string;
  requestedLimit: number;
  returnedCount: number;
  labels: QualityTrainingLabel[];
}

export interface MlJobReadiness {
  jobId: string;
  jobCode: string;
  jobName: string;
  jobType: string;
  isEnabled: boolean;
  lastRunStatus: string;
  scheduleExpression: string;
  readinessStatus: string;
  reason: string;
}

export interface ModelRegistryLifecycle {
  id: string;
  modelCode: string;
  modelName: string;
  modelType: string;
  modelVersion: string;
  riskType: string;
  isActive: boolean;
  lifecycleState: string;
  governanceMessage: string;
  registeredAtUtc: string;
}

export interface CorrelationLifecycle {
  id: string;
  correlationType: string;
  subjectCode: string;
  outcomeCode: string;
  score: number | null;
  lifecycleState: string;
  governanceMessage: string;
  calculatedAtUtc: string;
}

export interface MlWorkspaceReadiness {
  generatedAtUtc: string;
  readiness: MlReadinessScore;
  labelPreview: QualityTrainingLabelPreview;
  mlJobs: MlJobReadiness[];
  modelRegistry: ModelRegistryLifecycle[];
  correlations: CorrelationLifecycle[];
  currentIntelligence: string;
  futureMlLifecycle: string;
  disclaimer: string;
}

export const mlReadinessApi = {
  getScore: () => apiClient.get<MlReadinessScore>("/analytics/ml-readiness/score"),

  getLabelPreview: (limit = 50) =>
    apiClient.get<QualityTrainingLabelPreview>(
      `/analytics/ml-readiness/labels/preview?limit=${limit}`
    ),

  getJobs: () => apiClient.get<MlJobReadiness[]>("/analytics/ml-readiness/jobs"),

  ensureJobs: () =>
    apiClient.post<{ generatedAtUtc: string; message: string; jobs: MlJobReadiness[] }>(
      "/analytics/ml-readiness/jobs/ensure",
      {}
    ),

  getWorkspace: (labelPreviewLimit = 25) =>
    apiClient.get<MlWorkspaceReadiness>(
      `/analytics/ml-readiness/workspace?labelPreviewLimit=${labelPreviewLimit}`
    ),
};