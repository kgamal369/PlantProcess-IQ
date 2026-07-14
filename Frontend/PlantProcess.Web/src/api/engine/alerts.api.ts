// M1-06 alerts API - wraps the /api/alerts backend endpoints.
import { apiClient } from "@/api/http";

export type AlertRule = {
  id: string;
  ruleName: string;
  parameterCode: string;
  comparator: string;
  limitValue: number;
  severity: string;
  isActive?: boolean;
  createdAtUtc?: string;
};

export type PlantDataLogRow = {
  id: string;
  ruleName: string;
  parameterCode: string;
  materialCode: string | null;
  observedValue: number | null;
  comparator: string;
  limitValue: number;
  severity: string;
  message: string;
  loggedAtUtc: string;
};

export type CreateRuleBody = {
  ruleName: string;
  parameterCode: string;
  comparator: string;
  limitValue: number;
  severity?: string;
};

export type EvaluateResult = { logged: number };

export function listRules(): Promise<AlertRule[]> {
  return apiClient.get<AlertRule[]>("/api/alerts/rules");
}
export function createRule(body: CreateRuleBody): Promise<AlertRule> {
  return apiClient.post<AlertRule>("/api/alerts/rules", body);
}
export function evaluateAlerts(): Promise<EvaluateResult> {
  return apiClient.post<EvaluateResult>("/api/alerts/evaluate");
}
export function listLog(): Promise<PlantDataLogRow[]> {
  return apiClient.get<PlantDataLogRow[]>("/api/alerts/log");
}