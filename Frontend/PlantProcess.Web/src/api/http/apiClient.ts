import { API_BASE_URL } from "../apiConfig";

export type QueryPrimitive = string | number | boolean | null | undefined;
export type QueryParams = Record<string, QueryPrimitive>;

export class ApiError extends Error {
  public readonly status: number;
  public readonly responseText: string;

  constructor(message: string, status: number, responseText: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.responseText = responseText;
  }
}

export function buildQuery(params?: QueryParams): string {
  if (!params) return "";

  const searchParams = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (value === null || value === undefined || value === "") continue;
    searchParams.set(key, String(value));
  }

  const query = searchParams.toString();
  return query ? `?${query}` : "";
}

async function requestJson<T>(
  method: string,
  path: string,
  body?: unknown
): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers: body === undefined
      ? undefined
      : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body)
  });

  const text = await response.text();

  if (!response.ok) {
    throw new ApiError(
      `API request failed: ${method} ${path} returned ${response.status}`,
      response.status,
      text
    );
  }

  if (!text) return undefined as T;

  return JSON.parse(text) as T;
}

export const apiClient = {
  get<T>(path: string, params?: QueryParams) {
    return requestJson<T>("GET", `${path}${buildQuery(params)}`);
  },

  post<T>(path: string, body?: unknown) {
    return requestJson<T>("POST", path, body);
  },

  put<T>(path: string, body?: unknown) {
    return requestJson<T>("PUT", path, body);
  },

  patch<T>(path: string, body?: unknown) {
    return requestJson<T>("PATCH", path, body);
  },

  delete<T>(path: string) {
    return requestJson<T>("DELETE", path);
  }
};
