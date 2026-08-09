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
  // PPIQ T-042. The typed link to the operational workspace that carries this
  // page's widgets, and the publication state. Null backing means the page has
  // no workspace yet; null published means it is still a draft.
  backingDashboardDefinitionId?: string | null;
  publishedAtUtc?: string | null;
  // Present only in the projection that asked for deleted rows.
  isDeleted?: boolean;
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
  // PPIQ T-042. Optional for the same reason the audience is: omitting it
  // PRESERVES the stored link, and only sending it changes one.
  backingDashboardDefinitionId?: string | null;
  layoutJson: unknown;
  widgetBindingsJson: unknown;
  expectedVersion?: number | null;
}

export const pageBuilderApi = {
  // PPIQ T-042 S6. includeDeleted is for the WORKSPACE PROJECTION only, which
  // must tell a dashboard that never had a page from one whose page was
  // deleted. The Page Builder listing keeps the default and never sees them.
  listMine: (includeDeleted = false) =>
    apiClient.get<PageDefinitionDto[]>("/pages" + (includeDeleted ? "?includeDeleted=true" : "")),
  publish: (slug: string) => apiClient.post<PageDefinitionDto>(`/pages/${slug}/publish`, {}),
  unpublish: (slug: string) => apiClient.post<PageDefinitionDto>(`/pages/${slug}/unpublish`, {}),
  getBySlug: (slug: string) => apiClient.get<PageDefinitionDto>(`/pages/${slug}`),
  create: (request: UpsertPageDefinitionRequest) => apiClient.post<PageDefinitionDto>("/pages", request),
  update: (slug: string, request: UpsertPageDefinitionRequest) => apiClient.put<PageDefinitionDto>(`/pages/${slug}`, request),
  delete: (slug: string) => apiClient.delete<{ deleted: boolean }>(`/pages/${slug}`),
};
