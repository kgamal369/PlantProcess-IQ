# Genericity violation inventory

Backlog origin: T-206. Release: M2.

Product-generic files scanned: 1015
Grandfathered fingerprints: 49

Handed to the vocabulary-sweep owner for term-list construction and removal.

## Backend/PlantProcess.Application/Analytics/Value/ValueImpactEngine.cs  (5)

- [UA-02] L34 `coilid`  fp=0e14e14a23c8
- [UA-02] L34 `"coil:"`  fp=417edd624219
- [UA-04] L48 `"scrap_cost_per_ton"`  fp=5dfc849867ef
- [UA-04] L48 `"downgrade_delta_per_ton"`  fp=83d8bef5cdac
- [UA-04] L74 `"grade_premium_per_ton"`  fp=df115765f9dc

## Frontend/PlantProcess.Web/src/pages/Admin/CanonicalSchemaMappingPanel.implementation.tsx  (4)

- [UA-02] L43 `coil_id`  fp=7037759ea9a4
- [UA-06] L43 `coil_id`  fp=9f9161edae31
- [UA-02] L48 `slab_id`  fp=50307addb9d9
- [UA-06] L48 `slab_id`  fp=9d5ada8607cb

## Backend/PlantProcess.Analytics.Engine/Postgres/PostgresCanonicalFeatureSource.cs  (3)

- [UA-03] L31 `isnullorwhitespace(request.grain) ? "coil"`  fp=5aa8a733c1ca
- [UA-02] L47 `heat_id`  fp=26f5f46482d8
- [UA-06] L47 `heat_id`  fp=c942630143b6

## Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs  (3)

- [UA-06] L422 `piece_id`  fp=675366b8aac7
- [UA-02] L423 `coil_id`  fp=7fa764fa8dbd
- [UA-06] L423 `coil_id`  fp=65dbfdd60ddf

## Backend/PlantProcess.Infrastructure/Analytics/NpgsqlFeatureVectorLoader.cs  (3)

- [UA-02] L39 `heat_id`  fp=bb074978c608
- [UA-06] L39 `heat_id`  fp=09fe1887ea5b
- [UA-07] L112 `indexof("grade"`  fp=d1a9cf9f24f7

## Backend/PlantProcess.Infrastructure/Analytics/NpgsqlValueImpactRepository.cs  (3)

- [UA-02] L20 `coilid`  fp=62385824b8f3
- [UA-02] L59 `coil_id`  fp=e13cd2ac9078
- [UA-06] L59 `coil_id`  fp=cb9c9ad7a9c4

## Frontend/PlantProcess.Web/src/api/p3T15WidgetSchemaContract.ts  (3)

- [UA-01] L337 `"caster 1"`  fp=bba6753b9d4d
- [UA-01] L339 `"caster 2"`  fp=afe67781f205
- [UA-01] L341 `"mill 1"`  fp=2aaddad9b29b

## Frontend/PlantProcess.Web/src/pages/Analysis/analysisOutcomeRegistry.ts  (3)

- [UA-03] L22 `row.grain ?? ""`  fp=0af18a4f7d0e
- [UA-03] L39 `.grain ?? ""`  fp=be164e607476
- [UA-03] L50 `first.grain ?? ""`  fp=0803ef4428a7

## Backend/PlantProcess.Api/VisualMapper/V5VisualMapperEndpoints.cs  (2)

- [UA-07] L676 `contains("coil"`  fp=a07ee1aec206
- [UA-07] L679 `contains("heat"`  fp=339af6116e4b

## Backend/PlantProcess.Application/Connectors/Certification/ConnectorBehaviourCertification.cs  (2)

- [UA-02] L418 `coil_id`  fp=c9b2d8a63e9c
- [UA-06] L418 `coil_id`  fp=7e495ba70c97

## Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.implementation.generated.tsx  (2)

- [UA-02] L691 `heat_id`  fp=6d9a7daad695
- [UA-06] L691 `heat_id`  fp=02dc97429a29

## Frontend/PlantProcess.Web/src/types/analyticsContracts.ts  (2)

- [UA-05] L37 `"coil" | "heat"`  fp=b93390fcfad3
- [UA-05] L37 `"cast" | "slab"`  fp=a406e79afde3

## Backend/PlantProcess.Analytics.Engine/Postgres/PostgresAnalysisFindingSink.cs  (1)

- [UA-03] L18 `isnullorwhitespace(request.grain) ? "coil"`  fp=3c91b91cb5c9

## Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs  (1)

- [UA-03] L19 `isnullorwhitespace(grain) ? "coil"`  fp=0726c315db93

## Backend/PlantProcess.Api/Endpoints/Analytics/AnalysisJobDefinitionEndpoints.cs  (1)

- [UA-03] L789 `isnullorwhitespace(grain) ? "coil"`  fp=4ee3b031ba6a

## Backend/PlantProcess.Api/Endpoints/Analytics/ValueEndpoints.cs  (1)

- [UA-02] L18 `coilid`  fp=52dbc2399255

## Backend/PlantProcess.Application/Analytics/Advanced/AdvancedAnalysisContracts.cs  (1)

- [UA-02] L22 `heatid`  fp=73cfdd6a121d

## Backend/PlantProcess.Application/Analytics/Value/Demo/Phase7WorkedCaseFixtures.cs  (1)

- [UA-02] L14 `coilid`  fp=d431488a6980

## Backend/PlantProcess.Application/Analytics/Value/ValueContracts.cs  (1)

- [UA-02] L43 `coilid`  fp=221afee90cec

## Backend/PlantProcess.Infrastructure/Analytics/DotNetAdvancedCorrelationEngine.cs  (1)

- [UA-03] L34 `isnullorwhitespace(request.grain) ? "coil"`  fp=28f21a59cb11

## Backend/PlantProcess.Infrastructure/Analytics/PostgresCorrelationComputeEngine.cs  (1)

- [UA-03] L25 `isnullorwhitespace(request.grain) ? "coil"`  fp=f9dd01288ab8

## Frontend/PlantProcess.Web/src/api/analysisOptions.ts  (1)

- [UA-03] L39 `.grain ?? ""`  fp=4180754fa028

## Frontend/PlantProcess.Web/src/api/p3T14ValueExecutive.ts  (1)

- [UA-02] L24 `coilid`  fp=b71af9baf727

## Frontend/PlantProcess.Web/src/api/value/value.api.ts  (1)

- [UA-02] L22 `coilid`  fp=525e79f0b0d0

## Frontend/PlantProcess.Web/src/pages/Analysis/AnalysisToolboxPage.tsx  (1)

- [UA-03] L115 `raw.grain ?? ""`  fp=641d061d55f0

## Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/Phase7ValueScenarioPage.tsx  (1)

- [UA-02] L24 `coilid`  fp=6b217992b0b3

