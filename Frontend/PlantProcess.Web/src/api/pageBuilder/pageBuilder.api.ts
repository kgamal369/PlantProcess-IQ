import { apiClient } from "../http/apiClient";

export type PageVisibility = "Private" | "Shared" | "Public";

export interface PageDefinitionDto {
  id: string;
  tenantId: string;
  slug: string;
  title: string;
  ownerUserName: string;
  visibility: PageVisibility;
  // PPIQ T-041. The server always returns this now. It is optional here only so
  // a response cached from before the column existed still parses.
  audienceRoles?: string[];
  version: number;
  layoutJson: unknown;
  widgetBindingsJson: unknown;
  updatedAtUtc: string;
}

export interface UpsertPageDefinitionRequest {
  slug: string;
  title: string;
  visibility: PageVisibility;
  // PPIQ T-041. OMITTING this field and sending an empty array are different
  // requests: the server preserves an existing audience for the first and
  // rejects the second. So a caller that has an audience sends it, and a caller
  // that has none leaves it out rather than sending [].
  audienceRoles?: string[];
  layoutJson: unknown;
  widgetBindingsJson: unknown;
  expectedVersion?: number | null;
}

export const pageBuilderApi = {
  listMine: () => apiClient.get<PageDefinitionDto[]>("/pages"),
  getBySlug: (slug: string) => apiClient.get<PageDefinitionDto>(`/pages/${slug}`),
  create: (request: UpsertPageDefinitionRequest) => apiClient.post<PageDefinitionDto>("/pages", request),
  update: (slug: string, request: UpsertPageDefinitionRequest) => apiClient.put<PageDefinitionDto>(`/pages/${slug}`, request),
  delete: (slug: string) => apiClient.delete<{ deleted: boolean }>(`/pages/${slug}`),
};
