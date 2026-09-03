# Genericity violation inventory

Backlog origin: T-206. Release: M2.

Product-generic files scanned: 1055
Grandfathered fingerprints: 132

Handed to the vocabulary-sweep owner for term-list construction and removal.

## Backend/PlantProcess.Application/Dashboarding/Contracts/DashboardMetadataDtos.cs  (13)

- [UA-08] L136 `string? defecttype,`  fp=13e0ab5300f6
- [UA-08] L137 `string? riskclass,`  fp=0b1dd4f57cb6
- [UA-08] L138 `string? shiftcode,`  fp=11b3299a44fc
- [UA-08] L296 `const string productfamily`  fp=86c86d42a2c7
- [UA-08] L296 `= "productfamily";`  fp=1a2ab4aa3146
- [UA-08] L297 `const string gradeorrecipe`  fp=fa7500e976e9
- [UA-08] L297 `= "gradeorrecipe";`  fp=738cf464115f
- [UA-08] L298 `const string shiftcode`  fp=88bdbbd9c7d3
- [UA-08] L298 `= "shiftcode";`  fp=57e1c4c9d75c
- [UA-08] L299 `const string defecttype`  fp=f35aa434021a
- [UA-08] L299 `= "defecttype";`  fp=0e7e33ed58ce
- [UA-08] L304 `const string riskclass`  fp=5690acdb6668
- [UA-08] L304 `= "riskclass";`  fp=8d1a42afa540

## Backend/PlantProcess.Application/Dashboarding/Services/Metadata/DashboardMetadataService.cs  (8)

- [UA-08] L288 `("defecttype",`  fp=1161b9935283
- [UA-08] L289 `("riskclass",`  fp=f7fb4ac56836
- [UA-08] L290 `("shiftcode",`  fp=c1120f9447f6
- [UA-08] L326 `, "defecttype",`  fp=aeed9ebca81e
- [UA-08] L326 `, "shiftcode" }`  fp=7b1c226aec27
- [UA-08] L334 `, "shiftcode",`  fp=e3cb478873a2
- [UA-08] L350 `{ "riskclass",`  fp=a003bba54dee
- [UA-08] L350 `, "productfamily" }`  fp=a7240f70a995

## Backend/PlantProcess.Application/Analytics/Value/ValueImpactEngine.cs  (5)

- [UA-02] L34 `coilid`  fp=0e14e14a23c8
- [UA-02] L34 `"coil:"`  fp=417edd624219
- [UA-04] L48 `"scrap_cost_per_ton"`  fp=5dfc849867ef
- [UA-04] L48 `"downgrade_delta_per_ton"`  fp=83d8bef5cdac
- [UA-04] L74 `"grade_premium_per_ton"`  fp=df115765f9dc

## Backend/PlantProcess.Application/Dashboarding/Contracts/DashboardDtos.cs  (5)

- [UA-08] L12 `string? defecttype,`  fp=f4381654ed1d
- [UA-08] L13 `string? riskclass,`  fp=7e2cf97cfd69
- [UA-08] L16 `string? shiftcode,`  fp=65f934d5b21c
- [UA-08] L68 `string? productfamily,`  fp=d735213d3e31
- [UA-08] L69 `string? gradeorrecipe,`  fp=ecd44f854e22

## Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardAggregateExecutor.cs  (5)

- [UA-08] L60 `public string? productfamily {`  fp=c993637f6532
- [UA-08] L61 `public string? gradeorrecipe {`  fp=773bd6b7bf23
- [UA-08] L63 `public string? shiftcode {`  fp=54c53de0b4ea
- [UA-08] L64 `public string? defecttype {`  fp=e2808b8a2c88
- [UA-08] L66 `public string? riskclass {`  fp=111eb21365be

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

## Backend/PlantProcess.Api/Endpoints/Analytics/AnalysisJobDefinitionEndpoints.cs  (3)

- [UA-03] L789 `isnullorwhitespace(grain) ? "coil"`  fp=4ee3b031ba6a
- [UA-08] L1150 `string? defecttype,`  fp=374691023662
- [UA-08] L1174 `string defecttype,`  fp=dfb814fb32cb

## Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.Contracts.001.cs  (3)

- [UA-08] L68 `string? productfamily,`  fp=f74e399985bb
- [UA-08] L69 `string? gradeorrecipe,`  fp=a7fad8798513
- [UA-08] L215 `string? riskclass,`  fp=51a0a1356620

## Backend/PlantProcess.Application/Dashboarding/Services/Widgets/WidgetQueryExpressionService.cs  (3)

- [UA-08] L339 `, "defecttype")`  fp=37e34890a697
- [UA-08] L340 `, "riskclass")`  fp=ecf7bb06ea60
- [UA-08] L341 `, "shiftcode")`  fp=54d9c6788d04

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

## Frontend/PlantProcess.Web/src/components/DashboardFilterBar.tsx  (3)

- [UA-08] L246 `("defecttype",`  fp=b220bab40a3b
- [UA-08] L261 `("riskclass",`  fp=fa346b61b900
- [UA-08] L276 `("shiftcode",`  fp=f99312d6a2f5

## Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.implementation.generated.tsx  (3)

- [UA-02] L691 `heat_id`  fp=6d9a7daad695
- [UA-06] L691 `heat_id`  fp=02dc97429a29
- [UA-08] L829 `, "productfamily",`  fp=85f36925139b

## Frontend/PlantProcess.Web/src/pages/Analysis/analysisOutcomeRegistry.ts  (3)

- [UA-03] L22 `row.grain ?? ""`  fp=0af18a4f7d0e
- [UA-03] L39 `.grain ?? ""`  fp=be164e607476
- [UA-03] L50 `first.grain ?? ""`  fp=0803ef4428a7

## Frontend/PlantProcess.Web/src/state/DashboardFilterContext.tsx  (3)

- [UA-08] L42 `, "defecttype",`  fp=755fce0804bc
- [UA-08] L42 `, "riskclass",`  fp=06f94e791b7b
- [UA-08] L43 `, "shiftcode",`  fp=9b84341fdca3

## Frontend/PlantProcess.Web/src/state/widgetSelectionMap.ts  (3)

- [UA-08] L12 `: "shiftcode",`  fp=4ca16bbcceaa
- [UA-08] L13 `: "defecttype",`  fp=a0913040ad8c
- [UA-08] L15 `: "riskclass",`  fp=846c3156b16f

## Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.Helpers.017.BuildCanonicalTargets.cs  (2)

- [UA-08] L23 `, "productfamily",`  fp=6ea9b41a9492
- [UA-08] L24 `, "gradeorrecipe",`  fp=641728e627d0

## Backend/PlantProcess.Api/Endpoints/Analytics/Phase2InvestigationEndpoints.cs  (2)

- [UA-08] L526 `string? defecttype,`  fp=bde8ac1a5414
- [UA-08] L555 `string defecttype,`  fp=48da0a73894b

## Backend/PlantProcess.Api/Endpoints/Materials/MaterialEndpoints.cs  (2)

- [UA-08] L267 `string? productfamily,`  fp=47459ee325c0
- [UA-08] L268 `string? gradeorrecipe,`  fp=ccd0fd3fe0c2

## Backend/PlantProcess.Api/VisualMapper/V5VisualMapperEndpoints.cs  (2)

- [UA-07] L676 `contains("coil"`  fp=a07ee1aec206
- [UA-07] L679 `contains("heat"`  fp=339af6116e4b

## Backend/PlantProcess.Application/Analytics/Contracts/MaterialFeatureVector.cs  (2)

- [UA-08] L15 `string? productfamily,`  fp=bf30ab700f27
- [UA-08] L16 `string? gradeorrecipe,`  fp=78ed6179ed16

## Backend/PlantProcess.Application/Connectors/Certification/ConnectorBehaviourCertification.cs  (2)

- [UA-02] L418 `coil_id`  fp=c9b2d8a63e9c
- [UA-06] L418 `coil_id`  fp=7e495ba70c97

## Backend/PlantProcess.Application/Contracts/Materials/CreateMaterialCommand.cs  (2)

- [UA-08] L9 `string? productfamily,`  fp=4f1e7624b29c
- [UA-08] L10 `string? gradeorrecipe,`  fp=4c59ac0c2614

## Backend/PlantProcess.Application/Contracts/PlantLayout/PlantLayoutDtos.cs  (2)

- [UA-08] L63 `string? productfamily,`  fp=e08fde251d28
- [UA-08] L64 `string? gradeorrecipe,`  fp=373a96111949

## Backend/PlantProcess.Application/Dashboarding/Services/Queries/WidgetResultSources.cs  (2)

- [UA-08] L1924 `("riskclass",`  fp=9bde0138c610
- [UA-08] L2292 `("gradeorrecipe",`  fp=5b5dcf06f0bd

## Backend/PlantProcess.Application/Integration/Services/Mapping/MappingExecutionService.cs  (2)

- [UA-08] L247 `, "productfamily")`  fp=fb771aea0c80
- [UA-08] L248 `, "gradeorrecipe")`  fp=f6fc16697214

## Backend/PlantProcess.Application/Reporting/InvestigationReportDtos.cs  (2)

- [UA-08] L7 `string? productfamily,`  fp=eccc58bf280a
- [UA-08] L8 `string? gradeorrecipe,`  fp=11d25309d67c

## Backend/PlantProcess.Domain/Entities/Materials/MaterialUnit.cs  (2)

- [UA-08] L16 `public string? productfamily {`  fp=452b6c5a136a
- [UA-08] L18 `public string? gradeorrecipe {`  fp=6106def6f426

## Backend/PlantProcess.Domain/Entities/Process/ProductSpecification.cs  (2)

- [UA-08] L21 `public string? productfamily {`  fp=15906d3ae619
- [UA-08] L23 `public string gradeorrecipe {`  fp=6c990ed81d5b

## Frontend/PlantProcess.Web/src/components/dashboard/SavedDashboardWidget.tsx  (2)

- [UA-08] L100 `, "riskclass",`  fp=fc93573d62a7
- [UA-08] L281 `["gradeorrecipe",`  fp=b001c43f201a

## Frontend/PlantProcess.Web/src/types/analyticsContracts.ts  (2)

- [UA-05] L37 `"coil" | "heat"`  fp=b93390fcfad3
- [UA-05] L37 `"cast" | "slab"`  fp=a406e79afde3

## Backend/PlantProcess.Analytics.Engine/Postgres/PostgresAnalysisFindingSink.cs  (1)

- [UA-03] L18 `isnullorwhitespace(request.grain) ? "coil"`  fp=3c91b91cb5c9

## Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs  (1)

- [UA-03] L19 `isnullorwhitespace(grain) ? "coil"`  fp=0726c315db93

## Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs  (1)

- [UA-08] L722 `string defecttype,`  fp=1960b40b2a69

## Backend/PlantProcess.Api/Endpoints/Analytics/RiskScoreEndpoints.cs  (1)

- [UA-08] L238 `string? riskclass,`  fp=e0d8acc4d4e4

## Backend/PlantProcess.Api/Endpoints/Analytics/ValueEndpoints.cs  (1)

- [UA-02] L18 `coilid`  fp=52dbc2399255

## Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs  (1)

- [UA-08] L553 `string? productfamily,`  fp=3f119e012dbd

## Backend/PlantProcess.Application/Analytics/Advanced/AdvancedAnalysisContracts.cs  (1)

- [UA-02] L22 `heatid`  fp=73cfdd6a121d

## Backend/PlantProcess.Application/Analytics/Contracts/CorrelationDtos.cs  (1)

- [UA-08] L5 `string defecttype,`  fp=92d98c507106

## Backend/PlantProcess.Application/Analytics/Contracts/RiskPredictionDtos.cs  (1)

- [UA-08] L18 `string riskclass,`  fp=58d4d15e1d7a

## Backend/PlantProcess.Application/Analytics/Contracts/StoreRiskScoreCommand.cs  (1)

- [UA-08] L9 `string? riskclass,`  fp=0a0bcee7af22

## Backend/PlantProcess.Application/Analytics/Value/Demo/Phase7WorkedCaseFixtures.cs  (1)

- [UA-02] L14 `coilid`  fp=d431488a6980

## Backend/PlantProcess.Application/Analytics/Value/ValueContracts.cs  (1)

- [UA-02] L43 `coilid`  fp=221afee90cec

## Backend/PlantProcess.Domain/Entities/Analytics/RiskScore.cs  (1)

- [UA-08] L13 `public string? riskclass {`  fp=b49c7b955a7b

## Backend/PlantProcess.Domain/Entities/Configuration/Route.cs  (1)

- [UA-08] L13 `public string? productfamily {`  fp=2e3694cf010f

## Backend/PlantProcess.Infrastructure/Analytics/DotNetAdvancedCorrelationEngine.cs  (1)

- [UA-03] L34 `isnullorwhitespace(request.grain) ? "coil"`  fp=28f21a59cb11

## Backend/PlantProcess.Infrastructure/Analytics/PostgresCorrelationComputeEngine.cs  (1)

- [UA-03] L25 `isnullorwhitespace(request.grain) ? "coil"`  fp=f9dd01288ab8

## Frontend/PlantProcess.Web/src/api/analysisOptions.ts  (1)

- [UA-03] L42 `.grain ?? ""`  fp=4180754fa028

## Frontend/PlantProcess.Web/src/api/p3T14ValueExecutive.ts  (1)

- [UA-02] L24 `coilid`  fp=b71af9baf727

## Frontend/PlantProcess.Web/src/api/value/value.api.ts  (1)

- [UA-02] L22 `coilid`  fp=525e79f0b0d0

## Frontend/PlantProcess.Web/src/components/materials/GenealogyThreadPanel.tsx  (1)

- [UA-08] L97 `, "riskclass"]`  fp=7afe7a11b208

## Frontend/PlantProcess.Web/src/pages/Analysis/AnalysisToolboxPage.tsx  (1)

- [UA-03] L115 `raw.grain ?? ""`  fp=641d061d55f0

## Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx  (1)

- [UA-08] L317 `: "productfamily",`  fp=8019f534d678

## Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/Phase7ValueScenarioPage.tsx  (1)

- [UA-02] L24 `coilid`  fp=6b217992b0b3

