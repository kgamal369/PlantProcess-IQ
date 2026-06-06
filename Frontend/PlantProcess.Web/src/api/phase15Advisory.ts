import { apiClient } from "@/api/http";

export type P15SupportStatus = "Supported" | "InsufficientSupport" | "OutOfEnvelope" | "BlockedByHonestyGuard" | number;
export type P15EvidenceStrength = "None" | "Weak" | "Moderate" | "Strong" | number;
export type P15RecommendationStatus = "Draft" | "ApprovalRequired" | "Approved" | "Dismissed" | "Blocked" | number;
export type P15ApprovalDecision = 1 | 2;
export type P15ValueKind = "Potential" | "Realized" | number;

export interface P15EvidenceReference { evidenceId: string; evidenceType: string; sourceSystem: string; description: string; confidence: number; strength: P15EvidenceStrength; provenance: string[]; }
export interface P15MoneyRange { currencyCode: string; minValue: number; expectedValue: number; maxValue: number; isValid?: boolean; }
export interface P15ParameterAdjustment { parameterCode: string; displayName: string; currentValue: number; proposedValue: number; minimumObservedValue: number; maximumObservedValue: number; unit?: string | null; isInsideObservedEnvelope?: boolean; }
export interface P15ScenarioRequest { tenantId: string; plantId: string; findingId: string; scenarioName: string; seed: number; adjustments: P15ParameterAdjustment[]; evidence: P15EvidenceReference[]; }
export interface P15ScenarioProjectionPoint { metricCode: string; label: string; baselineValue: number; projectedValue: number; delta: number; unit?: string | null; }
export interface P15ScenarioResponse { scenarioId: string; findingId: string; supportStatus: P15SupportStatus; supportMessage: string; projectionOnlyStatement: string; seed: number; projectedValueImpact?: P15MoneyRange | null; projectionPoints: P15ScenarioProjectionPoint[]; evidence: P15EvidenceReference[]; isActionableProjection?: boolean; }
export interface P15RecommendationParameterWindow { parameterCode: string; displayName: string; recommendedMinimum: number; recommendedMaximum: number; unit?: string | null; basis: string; }
export interface P15RecommendationCandidate { recommendationId: string; findingId: string; title: string; advisoryText: string; status: P15RecommendationStatus; evidenceStrength: P15EvidenceStrength; confidence: number; expectedImpact?: P15MoneyRange | null; parameterWindows: P15RecommendationParameterWindow[]; evidence: P15EvidenceReference[]; provenance: string[]; honestyCaveat: string; requiresHumanApproval: boolean; hasWriteBackPath: boolean; }
export interface P15RecommendationGenerationRequest { tenantId: string; plantId: string; scenarioRequest: P15ScenarioRequest; }
export interface P15RecommendationGenerationResponse { requestId: string; scenarioId: string; scenarioSupportStatus: P15SupportStatus; message: string; recommendations: P15RecommendationCandidate[]; guardrails: string[]; }
export interface P15ApprovalCommand { recommendationId: string; approverUserId: string; decision: P15ApprovalDecision; comment: string; decidedAtUtc: string; }
export interface P15ApprovalResult { recommendationId: string; status: P15RecommendationStatus; message: string; approvalRecordId: string; decidedAtUtc: string; }
export interface P15ValueWindow { windowId: string; metricCode: string; label: string; fromUtc: string; toUtc: string; value: number; unit?: string | null; }
export interface P15ValueRealizationLedgerEntry { ledgerEntryId: string; tenantId: string; plantId: string; recommendationId: string; findingId: string; baselineWindow: P15ValueWindow; actualWindow: P15ValueWindow; realizedValue: P15MoneyRange; attributionCaveat: string; provenance: string[]; createdAtUtc: string; }
export interface P15ValueRealizationRequest { tenantId: string; plantId: string; recommendationId: string; findingId: string; currencyCode: string; euroPerUnitImprovement: number; baselineWindow: P15ValueWindow; actualWindow: P15ValueWindow; provenance: string[]; }
export interface P15ValueRealizationResponse { status: string; message: string; attributionCaveat: string; ledgerEntry?: P15ValueRealizationLedgerEntry | null; baselineVsActualDelta: number; violations: string[]; }
export interface P15RoiSummary { tenantId: string; plantId: string; potentialValue: P15MoneyRange; realizedValue: P15MoneyRange; paybackPeriodMonths: number; recommendationCount: number; approvedRecommendationCount: number; realizedLedgerEntryCount: number; evidencePackReference: string; }
export interface P15RoiValueBucket { bucketCode: string; label: string; valueKind: P15ValueKind; currencyCode: string; expectedValue: number; source: string; }
export interface P15CfoEvidencePack { evidencePackId: string; currencyCode: string; potentialExpectedValue: number; realizedExpectedValue: number; paybackPeriodMonths: number; recommendationIds: string[]; ledgerEntryIds: string[]; provenance: string[]; caveats: string[]; }
export interface P15RoiCfoDashboardResponse { status: string; message: string; generatedAtUtc: string; summary: P15RoiSummary; buckets: P15RoiValueBucket[]; ledgerEntries: P15ValueRealizationLedgerEntry[]; evidencePack: P15CfoEvidencePack; caveats: string[]; }
export interface P15ScenarioHealth { status: string; marker: string; phase: string; task: string; mode: string; projectionOnly: boolean; automaticWriteBack: boolean; outOfEnvelopeAbstain: boolean; }
export interface P15ScenarioContract { marker: string; contract: string; safetyRules: string[]; routes: string[]; }
export interface P15RecommendationHealth { status: string; marker: string; phase: string; task: string; mode: string; expectedEImpactRange: boolean; confidenceEvidenceProvenance: boolean; humanApprovalRequired: boolean; automaticWriteBack: boolean; }
export interface P15RecommendationContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }
export interface P15ValueRealizationHealth { status: string; marker: string; phase: string; task: string; mode: string; baselineVsActual: boolean; attributionCaveatRequired: boolean; linksToRecommendation: boolean; }
export interface P15ValueRealizationContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }
export interface P15ValueLedgerResponse { generatedAtUtc: string; count: number; entries: P15ValueRealizationLedgerEntry[]; }
export interface P15RoiCfoDashboardHealth { status: string; marker: string; phase: string; task: string; mode: string; separatesPotentialVsRealized: boolean; exportEvidencePack: boolean; attributionCaveatVisible: boolean; }
export interface P15RoiCfoDashboardContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }


export type P15BenchmarkVisibility = "Visible" | "SuppressedMinimumCohort" | "SuppressedTenantIsolation" | number;
export interface P15BenchmarkRequest { tenantId: string; plantId: string; metricCode: string; industryCode: string; minimumCohortSize: number; }
export interface P15BenchmarkBand { bandCode: string; p10: number; p25: number; p50: number; p75: number; p90: number; cohortSize: number; visibility: P15BenchmarkVisibility; }
export interface P15BenchmarkResponse { metricCode: string; industryCode: string; visibility: P15BenchmarkVisibility; message: string; band?: P15BenchmarkBand | null; privacyGuards: string[]; }
export interface P15BenchmarkMetricCard { metricCode: string; label: string; plantValue: number; industryMedian: number; percentileEstimate: number; benchmarkVisibility: P15BenchmarkVisibility; interpretation: string; }
export interface P15BestPracticeReference { practiceId: string; industryCode: string; title: string; description: string; evidenceLevel: string; safetyCaveat: string; }
export interface P15BenchmarkDashboardResponse { status: string; message: string; generatedAtUtc: string; tenantId: string; plantId: string; industryCode: string; benchmarks: P15BenchmarkResponse[]; metricCards: P15BenchmarkMetricCard[]; bestPractices: P15BestPracticeReference[]; privacyGuards: string[]; }
export interface P15BenchmarkingHealth { status: string; marker: string; phase: string; task: string; mode: string; anonymizedAggregateOnly: boolean; minimumCohortEnforced: boolean; noCrossTenantRowExposure: boolean; }
export interface P15BenchmarkingContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }

export interface P15HonestyCertificationCase { caseCode: string; title: string; expectedBehavior: string; actualBehavior: string; passed: boolean; violations: string[]; }
export interface P15HonestyCertificationReport { marker: string; status: string; message: string; generatedAtUtc: string; passedCases: number; failedCases: number; cases: P15HonestyCertificationCase[]; requiredGuardrails: string[]; }
export interface P15HonestyCertificationHealth { status: string; marker: string; phase: string; task: string; mode: string; noCausalLanguage: boolean; weakEvidenceBlocked: boolean; approvalRequired: boolean; automaticWriteBackBlocked: boolean; }
export interface P15HonestyCertificationContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }
export const phase15AdvisoryApi = {
  health() { return apiClient.get<P15ScenarioHealth>("/api/p15/advisory/scenarios/health"); },
  contract() { return apiClient.get<P15ScenarioContract>("/api/p15/advisory/scenarios/contract"); },
  scenarioHealth() { return apiClient.get<P15ScenarioHealth>("/api/p15/advisory/scenarios/health"); },
  scenarioContract() { return apiClient.get<P15ScenarioContract>("/api/p15/advisory/scenarios/contract"); },
  sampleRequest() { return apiClient.get<P15ScenarioRequest>("/api/p15/advisory/scenarios/sample-request"); },
  simulate(request: P15ScenarioRequest) { return apiClient.post<P15ScenarioResponse>("/api/p15/advisory/scenarios/simulate", request); },
  recommendationHealth() { return apiClient.get<P15RecommendationHealth>("/api/p15/advisory/recommendations/health"); },
  recommendationContract() { return apiClient.get<P15RecommendationContract>("/api/p15/advisory/recommendations/contract"); },
  recommendationDemoRequest() { return apiClient.get<P15RecommendationGenerationRequest>("/api/p15/advisory/recommendations/demo-request"); },
  generateRecommendations(request: P15RecommendationGenerationRequest) { return apiClient.post<P15RecommendationGenerationResponse>("/api/p15/advisory/recommendations/generate", request); },
  approveRecommendation(command: P15ApprovalCommand) { return apiClient.post<P15ApprovalResult>("/api/p15/advisory/recommendations/approve", command); },
  valueRealizationHealth() { return apiClient.get<P15ValueRealizationHealth>("/api/p15/value-realization/health"); },
  valueRealizationContract() { return apiClient.get<P15ValueRealizationContract>("/api/p15/value-realization/contract"); },
  valueRealizationDemoRequest() { return apiClient.get<P15ValueRealizationRequest>("/api/p15/value-realization/demo-request"); },
  calculateValueRealization(request: P15ValueRealizationRequest) { return apiClient.post<P15ValueRealizationResponse>("/api/p15/value-realization/calculate", request); },
  valueRealizationLedger() { return apiClient.get<P15ValueLedgerResponse>("/api/p15/value-realization/ledger"); },
  roiCfoDashboardHealth() { return apiClient.get<P15RoiCfoDashboardHealth>("/api/p15/roi-cfo-dashboard/health"); },
  roiCfoDashboardContract() { return apiClient.get<P15RoiCfoDashboardContract>("/api/p15/roi-cfo-dashboard/contract"); },
  roiCfoDashboardSummary() { return apiClient.get<P15RoiCfoDashboardResponse>("/api/p15/roi-cfo-dashboard/summary"); },
  roiCfoEvidencePack() { return apiClient.get<P15CfoEvidencePack>("/api/p15/roi-cfo-dashboard/evidence-pack"); },  benchmarkingHealth() { return apiClient.get<P15BenchmarkingHealth>("/api/p15/benchmarking/health"); },
  benchmarkingContract() { return apiClient.get<P15BenchmarkingContract>("/api/p15/benchmarking/contract"); },
  benchmarkingDemoRequest() { return apiClient.get<P15BenchmarkRequest>("/api/p15/benchmarking/demo-request"); },
  benchmarkingSummary() { return apiClient.get<P15BenchmarkDashboardResponse>("/api/p15/benchmarking/summary"); },
  benchmarkingSuppressedDemo() { return apiClient.get<P15BenchmarkResponse>("/api/p15/benchmarking/suppressed-demo"); },
  runBenchmark(request: P15BenchmarkRequest) { return apiClient.post<P15BenchmarkResponse>("/api/p15/benchmarking/benchmark", request); },
  honestyCertificationHealth() { return apiClient.get<P15HonestyCertificationHealth>("/api/p15/honesty-certification/health"); },
  honestyCertificationContract() { return apiClient.get<P15HonestyCertificationContract>("/api/p15/honesty-certification/contract"); },
  runHonestyCertification() { return apiClient.get<P15HonestyCertificationReport>("/api/p15/honesty-certification/run"); },

};
