# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: api-smoke.spec.ts >> PlantProcess IQ backend API smoke >> login returns access token
- Location: e2e\api-smoke.spec.ts:5:3

# Error details

```
Error: Login failed against http://localhost:5063/auth/login with HTTP 403

expect(received).toBeTruthy()

Received: false
```

# Test source

```ts
  1  | import { APIRequestContext, expect } from "@playwright/test";
  2  | 
  3  | export const apiBaseUrl =
  4  |   process.env.PLAYWRIGHT_API_URL ||
  5  |   process.env.VITE_API_BASE_URL ||
  6  |   "http://localhost:5063";
  7  | 
  8  | export const smokeUserName =
  9  |   process.env.PPIQ_SMOKE_USERNAME ||
  10 |   process.env.VITE_SMOKE_USERNAME ||
  11 |   "admin";
  12 | 
  13 | export const smokePassword =
  14 |   process.env.PPIQ_SMOKE_PASSWORD ||
  15 |   process.env.VITE_SMOKE_PASSWORD ||
  16 |   "ChangeMe123!";
  17 | 
  18 | export async function login(request: APIRequestContext): Promise<string> {
  19 |   const response = await request.post(`${apiBaseUrl}/auth/login`, {
  20 |     data: {
  21 |       UserName: smokeUserName,
  22 |       Password: smokePassword
  23 |     }
  24 |   });
  25 | 
  26 |   expect(
  27 |     response.ok(),
  28 |     `Login failed against ${apiBaseUrl}/auth/login with HTTP ${response.status()}`
> 29 |   ).toBeTruthy();
     |     ^ Error: Login failed against http://localhost:5063/auth/login with HTTP 403
  30 | 
  31 |   const body = await response.json();
  32 | 
  33 |   const token = body.accessToken || body.token;
  34 | 
  35 |   expect(token, "Login response must contain accessToken or token").toBeTruthy();
  36 | 
  37 |   return token;
  38 | }
  39 | 
  40 | export async function authenticatedGet(
  41 |   request: APIRequestContext,
  42 |   url: string
  43 | ) {
  44 |   const token = await login(request);
  45 | 
  46 |   return request.get(`${apiBaseUrl}${url}`, {
  47 |     headers: {
  48 |       Authorization: `Bearer ${token}`
  49 |     }
  50 |   });
  51 | }
```