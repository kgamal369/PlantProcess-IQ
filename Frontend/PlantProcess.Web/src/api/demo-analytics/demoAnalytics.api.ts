
import { apiClient } from "../http";





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

export const demoAnalyticsApi = {


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

};

