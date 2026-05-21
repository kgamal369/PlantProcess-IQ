# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: route-smoke.spec.ts >> PlantProcess IQ route smoke regression >> opens /demo-lifecycle without browser errors
- Location: e2e\route-smoke.spec.ts:15:5

# Error details

```
Error: expect(locator).toContainText(expected) failed

Locator: locator('body')
Expected pattern: /lifecycle|connector|ML|PlantProcess IQ/i
Received string:  "·············
"

Call log:
  - Expect "toContainText" with timeout 15000ms
  - waiting for locator('body')
    33 × locator resolved to <body>…</body>
       - unexpected value "
    
    
  
"

```

```
Error: browserContext.close: Target page, context or browser has been closed
```