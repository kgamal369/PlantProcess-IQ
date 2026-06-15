import { apiClient } from "../http/apiClient";

export interface DemoReadinessInputs {
  sourcesLinked: number;
  sourcesExpected: number;
  stagingPopulated: boolean;
  mappingsPublished: boolean;
  jobsRunnable: number;
  jobsExpected: number;
  demoPagesPresent: boolean;
}

export interface DemoReadinessReport {
  isReady: boolean;
  status: "green" | "blocked";
  blockers: string[];
  inputs: DemoReadinessInputs;
}

export const demoReadinessApi = {
  get: () => apiClient.get<DemoReadinessReport>("/admin/demo-readiness"),
};