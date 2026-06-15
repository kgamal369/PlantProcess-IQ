import { apiClient } from "../http/apiClient";

export type PageVisibility = "Private" | "Shared" | "Public";

export interface PageDefinitionDto {
  id: string;
  tenantId: string;
  slug: string;
  title: string;
  ownerUserName: string;
  visibility: PageVisibility;
  version: number;
  layoutJson: unknown;
  widgetBindingsJson: unknown;
  updatedAtUtc: string;
}

export interface UpsertPageDefinitionRequest {
  slug: string;
  title: string;
  visibility: PageVisibility;
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
