const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") || "http://localhost:5063";

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`);

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`API request failed: ${response.status} ${response.statusText} - ${text}`);
  }

  return response.json() as Promise<T>;
}

async function postJson<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`API request failed: ${response.status} ${response.statusText} - ${text}`);
  }

  return response.json() as Promise<T>;
}

export const plantProcessApi = {
  apiBaseUrl: API_BASE_URL,

  getHealth: () => getJson<any>("/health"),
  getDbHealth: () => getJson<any>("/db-health"),
  getDatabaseSummary: () => getJson<any>("/dev/database-summary"),
  getValidationReport: () => getJson<any>("/validation/sync-report"),
  getMaterialSample: (take = 10) => getJson<any[]>(`/dev/material-sample?take=${take}`),

  getDashboardOverview: () => getJson<any>("/analytics/dashboard/overview"),
  getRiskDashboard: () => getJson<any>("/analytics/dashboard/risk"),
  getQualityDashboard: () => getJson<any>("/analytics/dashboard/quality"),
  getDataQualityDashboard: () => getJson<any>("/analytics/dashboard/data-quality"),

  getMaterialFeatures: (materialUnitId: string) =>
    getJson<any>(`/analytics/features/${materialUnitId}`),

  calculateRisk: (materialUnitId: string) =>
    postJson<any>(`/risk-scores/materials/${materialUnitId}/calculate`, {
      riskType: "QualityRisk",
    }),

  getMaterialInvestigation: (materialUnitId: string) =>
    getJson<any>(`/reports/materials/${materialUnitId}/investigation`),

  getInvestigationPdfUrl: (materialUnitId: string) =>
    `${API_BASE_URL}/reports/materials/${materialUnitId}/investigation/pdf`,
};