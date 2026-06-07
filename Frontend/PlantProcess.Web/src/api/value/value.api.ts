
import { apiClient } from "../http";

export type Phase7BandDto = {
  Low: number;
  Mid: number;
  High: number;
};

export type Phase7CostAssumptionDto = {
  Currency: string;
  CostPerTon: Phase7BandDto | null;
  DowngradeDeltaPerTon: Phase7BandDto | null;
  ScrapCostPerTon: Phase7BandDto | null;
  DowntimeCostPerMin: Phase7BandDto | null;
  GradePremiumPerTon: Phase7BandDto | null;
  EnergyPricePerMwh: Phase7BandDto | null;
};

export type Phase7ImpactRequest = {
  FindingRef: string;
  CoilId: string | null;
  DefectCode: string | null;
  DefectRateDelta: number;
  MonthlyVolumeTons: number;
  ProductionStopMinutes: number;
  YieldLossTons: number;
  UseScrapCost: boolean;
};

export type Phase7RealizationWindow = {
  MetricCode: string;
  StartUtc: string;
  EndUtc: string;
  Value: number;
  Unit: string;
};

export type Phase7RealizationRequest = {
  TrackingCode: string;
  SourceRecommendationId: string | null;
  SourceValueImpactId: string | null;
  BaselineWindow: Phase7RealizationWindow;
  ActualWindow: Phase7RealizationWindow;
  Direction: 1 | 2;
  ValuePerUnit: Phase7BandDto;
  PotentialValue: Phase7BandDto;
  InvestmentCost: number;
  Currency: string;
};

export const phase7ValueApi = {
  putCostAssumptions(body: Phase7CostAssumptionDto) {
    return apiClient.put<unknown>("/api/value/cost-assumptions", body);
  },

  calculateImpact(body: Phase7ImpactRequest) {
    return apiClient.post<Record<string, unknown>>("/api/value/impact", body);
  },

  getRealizationContract() {
    return apiClient.get<Record<string, unknown>>("/api/value/realization/contract");
  },

  calculateRealization(body: Phase7RealizationRequest) {
    return apiClient.post<Record<string, unknown>>("/api/value/realization/calculate", body);
  },

  recordRealization(body: Phase7RealizationRequest) {
    return apiClient.post<Record<string, unknown>>("/api/value/realization/record", body);
  },

  getRealizationLedger(take = 10) {
    return apiClient.get<Record<string, unknown>>("/api/value/realization/ledger", { take });
  },
};
