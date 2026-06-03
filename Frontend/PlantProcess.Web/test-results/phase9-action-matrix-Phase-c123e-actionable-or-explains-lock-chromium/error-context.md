# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: phase9-action-matrix.spec.ts >> Phase 09 — P9-01 action-matrix coverage >> Source Objects / Admin: every visible control is labeled/actionable or explains lock
- Location: e2e\phase9-action-matrix.spec.ts:11:5

# Error details

```
Error: Source Objects / Admin renders expected business text

expect(locator).toContainText(expected) failed

Locator: locator('body')
Expected pattern: /source|schema|configuration|import/i
Received string:  "
    Backend connection failedBackend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetchRetry connection········
"

Call log:
  - Source Objects / Admin renders expected business text with timeout 20000ms
  - waiting for locator('body')
    18 × locator resolved to <body>…</body>
       - unexpected value "
    Backend connection failedBackend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetchRetry connection
    
  
"

```

```yaml
- region "Notifications alt+T"
- img "SOU"
- paragraph: Backend connection failed
- paragraph: "Backend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetch"
- button "Retry connection"
- region "Notifications alt+T"
```

```
Error: apiRequestContext._wrapApiCall: Target page, context or browser has been closed
```