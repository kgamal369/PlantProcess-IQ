import { apiClient } from "../http";
import { API_BASE_URL } from "../apiConfig";

export type DemoResetScope = "data-only" | "full" | "identities-only";

export interface DemoResetStep {
  code: string;
  label: string;
  status: string;
  percentComplete: number;
  exceptionDetail?: string | null;
}

export interface DemoResetJob {
  jobId: string;
  status: string;
  scope: DemoResetScope;
  operatorName: string;
  percentComplete: number;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  failureReason?: string | null;
  steps: DemoResetStep[];
}

export interface DemoResetAccepted {
  jobId: string;
  statusUrl: string;
  status: string;
  scope: DemoResetScope;
  acceptedAtUtc: string;
}

export interface Suggestion {
  id: string;
  title: string;
  reasoning: string;
  category: string;
  score: number;
  targetRoute: string;
}

export interface SuggestionsResponse {
  generatedAtUtc: string;
  context: string;
  materialUnitId?: string | null;
  evidence: Record<string, unknown>;
  recommendations: Suggestion[];
}

export interface DynamicPageSection {
  code: string;
  title: string;
  body: string;
}

export interface DynamicPageResponse {
  slug: string;
  title: string;
  description: string;
  sections: DynamicPageSection[];
}

export interface SavedInvestigationRequest {
  name: string;
  description?: string | null;
  schedule: "none" | "daily" | "weekly";
  notifyOnChange: boolean;
  materialUnitId?: string | null;
  materialCode?: string | null;
  filters: Record<string, unknown>;
}

export interface SavedInvestigationResponse {
  id: string;
  name: string;
  status: string;
  schedule: string;
  createdAtUtc: string;
  visibleInLoadList: boolean;
}

export const phase78Api = {
  startDemoReset(scope: DemoResetScope) {
    return apiClient.post<DemoResetAccepted>("/demo-lifecycle/reset", { scope });
  },

  getDemoResetProgress(jobId: string) {
    return apiClient.get<DemoResetJob>(`/demo-lifecycle/reset/${jobId}/progress`);
  },

  getSuggestions(materialUnitId?: string | null, context = "current-investigation") {
    return apiClient.get<SuggestionsResponse>("/api/suggestions", {
      materialUnitId: materialUnitId ?? undefined,
      context,
    });
  },

  getDynamicPage(slug: string) {
    return apiClient.get<DynamicPageResponse>(`/api/pages/${encodeURIComponent(slug)}`);
  },

  saveInvestigation(request: SavedInvestigationRequest) {
    return apiClient.post<SavedInvestigationResponse>("/api/investigations", request);
  },

  resetProgressUrl(jobId: string) {
    return `${API_BASE_URL}/demo-lifecycle/reset/${jobId}/progress`;
  },
};
