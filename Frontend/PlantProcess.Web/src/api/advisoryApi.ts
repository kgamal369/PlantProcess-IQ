// ============================================================
// FILE: src/api/advisoryApi.ts
// Step 2: renamed from phase15Advisory.ts (Naming Golden Rule).
// Backend endpoint URLs are server routes and are intentionally unchanged.
// ============================================================
import { apiClient } from "@/api/http";

export type SupportStatus = "Supported" | "InsufficientSupport" | "OutOfEnvelope" | "BlockedByHonestyGuard" | number;
export type EvidenceStrength = "None" | "Weak" | "Moderate" | "Strong" | number;
export type RecommendationStatus = "Draft" | "ApprovalRequired" | "Approved" | "Dismissed" | "Blocked" | number;
export type ApprovalDecision = 1 | 2;
export type ValueKind = "Potential" | "Realized" | number;

export interface EvidenceReference { evidenceId: string; evidenceType: string; sourceSystem: string; description: string; confidence: number; strength: EvidenceStrength; provenance: string[]; }
export interface MoneyRange { currencyCode: string; minValue: number; expectedValue: number; maxValue: number; isValid?: boolean; }
export interface ParameterAdjustment { parameterCode: string; displayName: string; currentValue: number; proposedValue: number; minimumObservedValue: number; maximumObservedValue: number; unit?: string | null; isInsideObservedEnvelope?: boolean; }
export interface ScenarioRequest { tenantId: string; plantId: string; findingId: string; scenarioName: string; seed: number; adjustments: ParameterAdjustment[]; evidence: EvidenceReference[]; }
export interface ScenarioProjectionPoint { metricCode: string; label: string; baselineValue: number; projectedValue: number; delta: number; unit?: string | null; }
export interface ScenarioResponse { scenarioId: string; findingId: string; supportStatus: SupportStatus; supportMessage: string; projectionOnlyStatement: string; seed: number; projectedValueImpact?: MoneyRange | null; projectionPoints: ScenarioProjectionPoint[]; evidence: EvidenceReference[]; isActionableProjection?: boolean; }
export interface RecommendationParameterWindow { parameterCode: string; displayName: string; recommendedMinimum: number; recommendedMaximum: number; unit?: string | null; basis: string; }
export interface RecommendationCandidate { recommendationId: string; findingId: string; title: string; advisoryText: string; status: RecommendationStatus; evidenceStrength: EvidenceStrength; confidence: number; expectedImpact?: MoneyRange | null; parameterWindows: RecommendationParameterWindow[]; evidence: EvidenceReference[]; provenance: string[]; honestyCaveat: string; requiresHumanApproval: boolean; hasWriteBackPath: boolean; }
export interface RecommendationGenerationRequest { tenantId: string; plantId: string; scenarioRequest: ScenarioRequest; }
export interface RecommendationGenerationResponse { requestId: string; scenarioId: string; scenarioSupportStatus: SupportStatus; message: string; recommendations: RecommendationCandidate[]; guardrails: string[]; }
export interface ApprovalCommand { recommendationId: string; approverUserId: string; decision: ApprovalDecision; comment: string; decidedAtUtc: string; }
export interface ApprovalResult { recommendationId: string; status: RecommendationStatus; message: string; approvalRecordId: string; decidedAtUtc: string; }
export interface ValueWindow { windowId: string; metricCode: string; label: string; fromUtc: string; toUtc: string; value: number; unit?: string | null; }
export interface ValueRealizationLedgerEntry { ledgerEntryId: string; tenantId: string; plantId: string; recommendationId: string; findingId: string; baselineWindow: ValueWindow; actualWindow: ValueWindow; realizedValue: MoneyRange; attributionCaveat: string; provenance: string[]; createdAtUtc: string; }
export interface ValueRealizationRequest { tenantId: string; plantId: string; recommendationId: string; findingId: string; currencyCode: string; euroPerUnitImprovement: number; baselineWindow: ValueWindow; actualWindow: ValueWindow; provenance: string[]; }
export interface ValueRealizationResponse { status: string; message: string; attributionCaveat: string; ledgerEntry?: ValueRealizationLedgerEntry | null; baselineVsActualDelta: number; violations: string[]; }
export interface RoiSummary { tenantId: string; plantId: string; potentialValue: MoneyRange; realizedValue: MoneyRange; paybackPeriodMonths: number; recommendationCount: number; approvedRecommendationCount: number; realizedLedgerEntryCount: number; evidencePackReference: string; }
export interface RoiValueBucket { bucketCode: string; label: string; valueKind: ValueKind; currencyCode: string; expectedValue: number; source: string; }
export interface CfoEvidencePack { evidencePackId: string; currencyCode: string; potentialExpectedValue: number; realizedExpectedValue: number; paybackPeriodMonths: number; recommendationIds: string[]; ledgerEntryIds: string[]; provenance: string[]; caveats: string[]; }
export interface RoiCfoDashboardResponse { status: string; message: string; generatedAtUtc: string; summary: RoiSummary; buckets: RoiValueBucket[]; ledgerEntries: ValueRealizationLedgerEntry[]; evidencePack: CfoEvidencePack; caveats: string[]; }
export interface ScenarioHealth { status: string; marker: string; phase: string; task: string; mode: string; projectionOnly: boolean; automaticWriteBack: boolean; outOfEnvelopeAbstain: boolean; }
export interface ScenarioContract { marker: string; contract: string; safetyRules: string[]; routes: string[]; }
export interface RecommendationHealth { status: string; marker: string; phase: string; task: string; mode: string; expectedEImpactRange: boolean; confidenceEvidenceProvenance: boolean; humanApprovalRequired: boolean; automaticWriteBack: boolean; }
export interface RecommendationContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }
export interface ValueRealizationHealth { status: string; marker: string; phase: string; task: string; mode: string; baselineVsActual: boolean; attributionCaveatRequired: boolean; linksToRecommendation: boolean; }
export interface ValueRealizationContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }
export interface ValueLedgerResponse { generatedAtUtc: string; count: number; entries: ValueRealizationLedgerEntry[]; }
export interface RoiCfoDashboardHealth { status: string; marker: string; phase: string; task: string; mode: string; separatesPotentialVsRealized: boolean; exportEvidencePack: boolean; attributionCaveatVisible: boolean; }
export interface RoiCfoDashboardContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }


export type BenchmarkVisibility = "Visible" | "SuppressedMinimumCohort" | "SuppressedTenantIsolation" | number;
export interface BenchmarkRequest { tenantId: string; plantId: string; metricCode: string; industryCode: string; minimumCohortSize: number; }
export interface BenchmarkBand { bandCode: string; p10: number; p25: number; p50: number; p75: number; p90: number; cohortSize: number; visibility: BenchmarkVisibility; }
export interface BenchmarkResponse { metricCode: string; industryCode: string; visibility: BenchmarkVisibility; message: string; band?: BenchmarkBand | null; privacyGuards: string[]; }
export interface BenchmarkMetricCard { metricCode: string; label: string; plantValue: number; industryMedian: number; percentileEstimate: number; benchmarkVisibility: BenchmarkVisibility; interpretation: string; }
export interface BestPracticeReference { practiceId: string; industryCode: string; title: string; description: string; evidenceLevel: string; safetyCaveat: string; }
export interface BenchmarkDashboardResponse { status: string; message: string; generatedAtUtc: string; tenantId: string; plantId: string; industryCode: string; benchmarks: BenchmarkResponse[]; metricCards: BenchmarkMetricCard[]; bestPractices: BestPracticeReference[]; privacyGuards: string[]; }
export interface BenchmarkingHealth { status: string; marker: string; phase: string; task: string; mode: string; anonymizedAggregateOnly: boolean; minimumCohortEnforced: boolean; noCrossTenantRowExposure: boolean; }
export interface BenchmarkingContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }

export interface HonestyCertificationCase { caseCode: string; title: string; expectedBehavior: string; actualBehavior: string; passed: boolean; violations: string[]; }
export interface HonestyCertificationReport { marker: string; status: string; message: string; generatedAtUtc: string; passedCases: number; failedCases: number; cases: HonestyCertificationCase[]; requiredGuardrails: string[]; }
export interface HonestyCertificationHealth { status: string; marker: string; phase: string; task: string; mode: string; noCausalLanguage: boolean; weakEvidenceBlocked: boolean; approvalRequired: boolean; automaticWriteBackBlocked: boolean; }
export interface HonestyCertificationContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }
export const advisoryApi = {
  health() { return apiClient.get<ScenarioHealth>("/api/p15/advisory/scenarios/health"); },
  contract() { return apiClient.get<ScenarioContract>("/api/p15/advisory/scenarios/contract"); },
  scenarioHealth() { return apiClient.get<ScenarioHealth>("/api/p15/advisory/scenarios/health"); },
  scenarioContract() { return apiClient.get<ScenarioContract>("/api/p15/advisory/scenarios/contract"); },
  sampleRequest() { return apiClient.get<ScenarioRequest>("/api/p15/advisory/scenarios/sample-request"); },
  simulate(request: ScenarioRequest) { return apiClient.post<ScenarioResponse>("/api/p15/advisory/scenarios/simulate", request); },
  recommendationHealth() { return apiClient.get<RecommendationHealth>("/api/p15/advisory/recommendations/health"); },
  recommendationContract() { return apiClient.get<RecommendationContract>("/api/p15/advisory/recommendations/contract"); },
  recommendationDemoRequest() { return apiClient.get<RecommendationGenerationRequest>("/api/p15/advisory/recommendations/demo-request"); },
  generateRecommendations(request: RecommendationGenerationRequest) { return apiClient.post<RecommendationGenerationResponse>("/api/p15/advisory/recommendations/generate", request); },
  approveRecommendation(command: ApprovalCommand) { return apiClient.post<ApprovalResult>("/api/p15/advisory/recommendations/approve", command); },
  valueRealizationHealth() { return apiClient.get<ValueRealizationHealth>("/api/p15/value-realization/health"); },
  valueRealizationContract() { return apiClient.get<ValueRealizationContract>("/api/p15/value-realization/contract"); },
  valueRealizationDemoRequest() { return apiClient.get<ValueRealizationRequest>("/api/p15/value-realization/demo-request"); },
  calculateValueRealization(request: ValueRealizationRequest) { return apiClient.post<ValueRealizationResponse>("/api/p15/value-realization/calculate", request); },
  valueRealizationLedger() { return apiClient.get<ValueLedgerResponse>("/api/p15/value-realization/ledger"); },
  roiCfoDashboardHealth() { return apiClient.get<RoiCfoDashboardHealth>("/api/p15/roi-cfo-dashboard/health"); },
  roiCfoDashboardContract() { return apiClient.get<RoiCfoDashboardContract>("/api/p15/roi-cfo-dashboard/contract"); },
  roiCfoDashboardSummary() { return apiClient.get<RoiCfoDashboardResponse>("/api/p15/roi-cfo-dashboard/summary"); },
  roiCfoEvidencePack() { return apiClient.get<CfoEvidencePack>("/api/p15/roi-cfo-dashboard/evidence-pack"); },  benchmarkingHealth() { return apiClient.get<BenchmarkingHealth>("/api/p15/benchmarking/health"); },
  benchmarkingContract() { return apiClient.get<BenchmarkingContract>("/api/p15/benchmarking/contract"); },
  benchmarkingDemoRequest() { return apiClient.get<BenchmarkRequest>("/api/p15/benchmarking/demo-request"); },
  benchmarkingSummary() { return apiClient.get<BenchmarkDashboardResponse>("/api/p15/benchmarking/summary"); },
  benchmarkingSuppressionPreview() { return apiClient.get<BenchmarkResponse>("/api/p15/benchmarking/suppressed-demo"); },
  runBenchmark(request: BenchmarkRequest) { return apiClient.post<BenchmarkResponse>("/api/p15/benchmarking/benchmark", request); },
  honestyCertificationHealth() { return apiClient.get<HonestyCertificationHealth>("/api/p15/honesty-certification/health"); },
  honestyCertificationContract() { return apiClient.get<HonestyCertificationContract>("/api/p15/honesty-certification/contract"); },
  runHonestyCertification() { return apiClient.get<HonestyCertificationReport>("/api/p15/honesty-certification/run"); },

};
