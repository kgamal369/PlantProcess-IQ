// M1-05 supervisor API - wraps the /api/supervisor backend endpoints.
import { apiClient } from "@/api/http";

export type SupervisorReport = {
  id: string;
  itemKey: string;
  title: string;
  body: string;
  createdAtUtc: string;
};

export type SupervisorRunResult = {
  id: string;
  itemKey: string;
  title: string;
  body: string;
  findings: number;
  significant: number;
};

export function listSupervisorReports(): Promise<SupervisorReport[]> {
  return apiClient.get<SupervisorReport[]>("/api/supervisor/reports");
}

export function runSupervisor(): Promise<SupervisorRunResult> {
  return apiClient.post<SupervisorRunResult>("/api/supervisor/run");
}