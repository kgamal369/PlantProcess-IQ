# Phase 5B-1 Product Core API Type Split

Generated: 2026-06-06T08:36:27.792Z

## What changed

- `productCoreApiClient.implementation.ts` no longer owns exported DTO/filter/read-model declarations.
- `product-core/types.ts` is now a thin barrel export.
- Product-core API types are split into domain modules under `src/api/product-core`.
- Runtime API behavior remains in `productCoreApiClient.implementation.ts`.

## Counts

- Original barrel/type file lines before domain split: 784
- New barrel lines: 90
- Domain module count: 6
- Exported type/interface declarations: 67

## Domain modules

| File | Group | Declarations | Lines |
|---|---|---:|---:|
| `admin-mapping-types.ts` | admin-mapping-types | 29 | 337 |
| `analytics-quality-types.ts` | analytics-quality-types | 3 | 52 |
| `dashboard-widget-types.ts` | dashboard-widget-types | 23 | 271 |
| `license-commercial-types.ts` | license-commercial-types | 1 | 12 |
| `material-process-types.ts` | material-process-types | 3 | 40 |
| `shared-types.ts` | shared-types | 8 | 106 |

## Validation

```powershell
node tools/phase5b/validate-product-core-type-split.cjs
powershell -ExecutionPolicy Bypass -File .\tools\phase56\Invoke-Phase5Phase6Validation.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ" -RunFrontendBuild
```

