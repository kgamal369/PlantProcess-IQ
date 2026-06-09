# OpenAPI -> typed client (T-019)

The TypeScript types in `src/api/generated/schema.d.ts` are generated from the
API's OpenAPI document and must be kept in sync with it.

## One-time / on API change

1. Start the API locally (it serves the spec at `/swagger/v1/swagger.json`):
       dotnet run --project ../../Backend/PlantProcess.Api
2. Export the spec and generate the client:
       npm run openapi:export      # writes openapi/openapi.json
       npm run openapi:generate    # writes src/api/generated/schema.d.ts
3. Commit both `openapi/openapi.json` and `src/api/generated/schema.d.ts`.

## CI

`npm run openapi:check` regenerates from the committed spec and fails if the
committed types differ - so the client can never silently drift from the API.
It is dormant (passes) until `openapi/openapi.json` is committed.

## Usage

    import type { paths, components } from "@/api/generated/schema";
    type LoginResponse = components["schemas"]["LoginResponse"];