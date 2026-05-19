import { API_BASE_URL } from "../apiConfig";

export type QueryPrimitive = string | number | boolean | null | undefined;
export type QueryParams = Record<string, QueryPrimitive>;

export const ACCESS_TOKEN_KEY = "plantprocess.auth.accessToken";
export const AUTH_USER_KEY = "plantprocess.auth.user";

export interface AuthenticatedUser {
  userName: string;
  displayName?: string | null;
  role: "Admin" | "DataManager" | "Engineer" | "Viewer" | string;
  expiresAtUtc: string;
  scopes?: string[];
}

export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  userName: string;
  displayName?: string | null;
  role: string;
  scopes?: string[];
}

export class ApiError extends Error {
  public readonly status: number;
  public readonly responseText: string;
  public readonly path: string;

  constructor(message: string, status: number, responseText: string, path: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.responseText = responseText;
    this.path = path;
  }
}

export function setAccessToken(token: string | null) {
  if (!token) {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    return;
  }

  localStorage.setItem(ACCESS_TOKEN_KEY, token);
}

export function getAccessToken() {
  return localStorage.getItem(ACCESS_TOKEN_KEY);
}

export function setAuthenticatedUser(user: AuthenticatedUser | null) {
  if (!user) {
    localStorage.removeItem(AUTH_USER_KEY);
    return;
  }

  localStorage.setItem(AUTH_USER_KEY, JSON.stringify(user));
}

export function getAuthenticatedUser(): AuthenticatedUser | null {
  const raw = localStorage.getItem(AUTH_USER_KEY);
  if (!raw) return null;

  try {
    return JSON.parse(raw) as AuthenticatedUser;
  } catch {
    localStorage.removeItem(AUTH_USER_KEY);
    return null;
  }
}

export function clearAuthentication() {
  setAccessToken(null);
  setAuthenticatedUser(null);
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

function dispatchAuthFailure(error: ApiError) {
  if (error.status === 401 || error.status === 403) {
    window.dispatchEvent(
      new CustomEvent("plantprocess:auth-failure", {
        detail: {
          status: error.status,
          path: error.path,
          responseText: error.responseText,
        },
      })
    );
  }
}

async function requestJson<T>(
  method: string,
  path: string,
  body?: unknown,
  timeoutMs = 30_000
): Promise<T> {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), timeoutMs);
  const token = getAccessToken();

  const headers: Record<string, string> = {
    Accept: "application/json",
  };

  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
  }

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers,
      signal: controller.signal,
      body: body === undefined ? undefined : JSON.stringify(body),
    });

    const text = await response.text();

    if (!response.ok) {
      const error = new ApiError(
        text || `API request failed: ${method} ${path} returned ${response.status}`,
        response.status,
        text,
        path
      );

      dispatchAuthFailure(error);
      throw error;
    }

    if (!text) return undefined as T;

    return JSON.parse(text) as T;
  } finally {
    window.clearTimeout(timeout);
  }
}

export const apiClient = {
  get<T>(path: string, params?: QueryParams, timeoutMs?: number) {
    return requestJson<T>("GET", `${path}${buildQuery(params)}`, undefined, timeoutMs);
  },

  post<T>(path: string, body?: unknown, timeoutMs?: number) {
    return requestJson<T>("POST", path, body, timeoutMs);
  },

  put<T>(path: string, body?: unknown, timeoutMs?: number) {
    return requestJson<T>("PUT", path, body, timeoutMs);
  },

  patch<T>(path: string, body?: unknown, timeoutMs?: number) {
    return requestJson<T>("PATCH", path, body, timeoutMs);
  },

  delete<T>(path: string, timeoutMs?: number) {
    return requestJson<T>("DELETE", path, undefined, timeoutMs);
  },

  async login(userName: string, password: string, requestedRole?: string | null) {
    const response = await requestJson<LoginResponse>("POST", "/auth/login", {
      userName,
      password,
      requestedRole,
    });

    setAccessToken(response.accessToken);
    setAuthenticatedUser({
      userName: response.userName,
      displayName: response.displayName,
      role: response.role,
      expiresAtUtc: response.expiresAtUtc,
      scopes: response.scopes ?? [],
    });

    return response;
  },

  logout() {
    clearAuthentication();
  },

  setAccessToken,
  getAccessToken,
  setAuthenticatedUser,
  getAuthenticatedUser,
  clearAuthentication,
};