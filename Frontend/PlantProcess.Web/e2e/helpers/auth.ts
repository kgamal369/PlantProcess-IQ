import { APIRequestContext, expect } from "@playwright/test";

export const apiBaseUrl =
  process.env.PLAYWRIGHT_API_URL ||
  process.env.VITE_API_BASE_URL ||
  "http://localhost:5063";

export const smokeUserName =
  process.env.PPIQ_SMOKE_USERNAME ||
  process.env.VITE_SMOKE_USERNAME ||
  "admin";

export const smokePassword =
  process.env.PPIQ_SMOKE_PASSWORD ||
  process.env.VITE_SMOKE_PASSWORD ||
  "ChangeMe123!";

export async function login(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${apiBaseUrl}/auth/login`, {
    data: {
      UserName: smokeUserName,
      Password: smokePassword
    }
  });

  expect(
    response.ok(),
    `Login failed against ${apiBaseUrl}/auth/login with HTTP ${response.status()}`
  ).toBeTruthy();

  const body = await response.json();

  const token = body.accessToken || body.token;

  expect(token, "Login response must contain accessToken or token").toBeTruthy();

  return token;
}

export async function authenticatedGet(
  request: APIRequestContext,
  url: string
) {
  const token = await login(request);

  return request.get(`${apiBaseUrl}${url}`, {
    headers: {
      Authorization: `Bearer ${token}`
    }
  });
}