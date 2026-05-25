// ============================================================
// FILE: Frontend/PlantProcess.Web/e2e/helpers/auth.ts
//
// Purpose:
//   Robust Playwright auth helper for PlantProcess IQ.
//
// Critical auth rule:
//   Do not default E2E to admin / ChangeMe123!.
//   That credential can be treated as bootstrap admin and rejected
//   with 403 once a real admin exists.
// ============================================================

import { expect, type APIRequestContext } from "@playwright/test";

export const apiBaseUrl =
  process.env.PLAYWRIGHT_API_URL ||
  process.env.VITE_API_BASE_URL ||
  "http://localhost:5063";

export const smokeUserName =
  process.env.PPIQ_SMOKE_USERNAME ||
  process.env.VITE_SMOKE_USERNAME ||
  "e2eadmin";

export const smokePassword =
  process.env.PPIQ_SMOKE_PASSWORD ||
  process.env.VITE_SMOKE_PASSWORD ||
  "E2EAdmin123!";

type LoginPayload = {
  label: string;
  data: Record<string, string>;
};

function normalizeApiUrl(path: string): string {
  if (path.startsWith("http://") || path.startsWith("https://")) {
    return path;
  }

  return `${apiBaseUrl}${path.startsWith("/") ? path : `/${path}`}`;
}

async function safeText(response: { text: () => Promise<string> }) {
  try {
    return await response.text();
  } catch {
    return "<unable to read response body>";
  }
}

export async function login(request: APIRequestContext): Promise<string> {
  const loginUrl = `${apiBaseUrl}/auth/login`;

  const attempts: LoginPayload[] = [
    {
      label: "Configured E2E real admin, PascalCase",
      data: {
        UserName: smokeUserName,
        Password: smokePassword,
      },
    },
    {
      label: "Configured E2E real admin, camelCase",
      data: {
        userName: smokeUserName,
        password: smokePassword,
      },
    },
  ];

  const failures: string[] = [];

  for (const attempt of attempts) {
    const response = await request.post(loginUrl, {
      data: attempt.data,
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
      },
    });

    if (!response.ok()) {
      failures.push(
        [
          `Attempt: ${attempt.label}`,
          `URL: ${loginUrl}`,
          `User: ${attempt.data.UserName ?? attempt.data.userName}`,
          `HTTP: ${response.status()}`,
          `Body: ${await safeText(response)}`,
        ].join("\n")
      );

      continue;
    }

    const body = await response.json();

    const token =
      body.accessToken ||
      body.token ||
      body.jwt ||
      body.bearerToken ||
      body?.data?.accessToken ||
      body?.data?.token;

    expect(
      token,
      `Login response from ${loginUrl} must contain accessToken or token. Body:\n${JSON.stringify(
        body,
        null,
        2
      )}`
    ).toBeTruthy();

    expect(
      String(token).length,
      `Login token returned from ${loginUrl} is unexpectedly short.`
    ).toBeGreaterThan(20);

    return String(token);
  }

  throw new Error(
    [
      "PlantProcess IQ E2E login failed.",
      "",
      `API base URL: ${apiBaseUrl}`,
      `Smoke user: ${smokeUserName}`,
      "",
      "Failures:",
      failures.join("\n\n---\n\n"),
      "",
      "Most likely fix:",
      "1. Stop old backend/frontend processes on ports 5063 and 5173.",
      "2. Let Playwright start both servers from playwright.config.ts.",
      "3. Ensure E2E uses a real admin user, not bootstrap admin:",
      "   e2eadmin / E2EAdmin123!",
    ].join("\n")
  );
}

export async function authenticatedGet(
  request: APIRequestContext,
  url: string
) {
  const token = await login(request);

  return request.get(normalizeApiUrl(url), {
    headers: {
      Accept: "application/json",
      Authorization: `Bearer ${token}`,
    },
  });
}

export async function authenticatedPost<TBody = unknown>(
  request: APIRequestContext,
  url: string,
  body: TBody
) {
  const token = await login(request);

  return request.post(normalizeApiUrl(url), {
    data: body,
    headers: {
      Accept: "application/json",
      Authorization: `Bearer ${token}`,
    },
  });
}

export async function authHeaders(
  request: APIRequestContext
): Promise<Record<string, string>> {
  const token = await login(request);

  return {
    Accept: "application/json",
    Authorization: `Bearer ${token}`,
  };
}