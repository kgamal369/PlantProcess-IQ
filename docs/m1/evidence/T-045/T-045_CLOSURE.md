# T-045 CLOSURE EVIDENCE

**Analytical page certification. Closed 12 August 2026.**

Every number in this document was measured on this machine against
`ppiq_presentation`, or read from a run transcript. Where something is an
inference rather than a measurement it says so. Where I was wrong during the
work, the correction is recorded rather than the original claim.

---

## 1. COMMIT CHAIN

| Commit | Pack | What it is |
|---|---|---|
| `6f424969` | A | nine definition convergences, deterministic presentation parameter (pre-existing at start) |
| `5313d93c` | B | the Class-1 / Class-2 result source seam and three native sources |
| `d66d8c44` | C | MI_SEV terminology honesty, 811, terminology build gate |
| `d54615ec` | D | truthful analytical page bindings, synthetic-aware model state, 812 |
| `f4ea75e3` | E | live presentation parameter convergence, one derivation rule, 813 |
| `0214324a` | F | repair of the integration suites Pack B broke, widened build gate, 814 |
| `77b49c00` | G | 800 no longer reverses 812, three operational widgets converged, 815 |

---

## 2. WHAT WAS BUILT

**The seam.** `IWidgetResultSource` with three native sources. The discriminator
is the measure code and nothing else - no widget branch, no dashboard branch, no
page branch. Class 1 is untouched: the dispatcher is a dictionary lookup placed
immediately before the aggregate block and not one Class-1 line was moved.

**`findingStatus`** reads `correlation_results`. Zero rows produce a renderable
row stating `NO_SUPPORTED_FINDINGS_CURRENTLY_PUBLISHED`. It never says no
correlation exists: that is a claim about the plant, and this measure only knows
what the published result store contains.

**`scoringCoverage`** reads `risk_scores` and `material_units`. It exposes
`scoredPopulation`, `referencePopulation`, `coverageAgainstReference`,
`syntheticPopulation`, `scoringSource` and `modelState`. The denominator is named
`referencePopulation`, never `eligiblePopulation`, because nothing here resolves
scoring eligibility.

**`analysisReadiness`** binds the canonical authority: `IAnalysisReadinessService`
-> `AdvancedReadiness.Evaluate` -> `ReadinessGate`. Five DF8 dimensions, overall
state is the worst dimension, folded by the gate itself rather than reimplemented.
`IMlReadinessService` is deliberately not used.

**The validator stayed generic.** It asks the registry
`MeasureProvidesOwnColumns(measureCode)`, in the shape of the existing
`MeasureRequiresParameterCode`. A build gate asserts it names none of the three
measure codes, so a future native source is a registry entry and not a validator
edit.

---

## 3. THREE MEASURED FINDINGS THAT CHANGED THE FROZEN SPEC

**`risk_scores` DOES carry provenance columns.** The handover's frozen claim that
it has none is wrong. `RiskScore` declares `ModelVersion` and inherits
`IsSynthetic`, `SourceSystem` and `SourceRecordId`, all mapped explicitly in
`RiskScoreConfiguration`. `SCORING_SOURCE_UNKNOWN` remains a possible answer but
for a different reason - a run that wrote nothing into the columns, not a schema
that lacks them - and `syntheticPopulation` is therefore computable.

**`analysisReadiness` could not be parameterised as specified.**
`AdvancedAnalysisRequest` requires a `Grain`; `ml_outcome_definitions.grain` is
NOT NULL and belongs to the outcome, not the widget; and the widget query DTOs
carry no grain field. Hardcoding one would have put plant vocabulary in engine
code. Resolved by `IAnalysisOutcomeTargetResolver` plus an Npgsql implementation
registered beside `IFeatureVectorLoader`. The outcome key travels on the existing
parameter carrier.

**`AnalysisReadinessDto.IndependentHeats` is steel vocabulary inside the canonical
readiness contract.** A Rule 1 violation in a file this task does not own.
Recorded, not fixed. The genericity guard uses word boundaries so it does not
fail on a name the seam cannot change.

---

## 4. MEASURED RESULTS

### 4.1 The ML outcome census

8 definitions, all active, all status `Active`. Grains: coil 4, generic 3,
location 1. **Two are usable** - registered AND carrying outcome values:

| Outcome | Grain | Type | Values |
|---|---|---|---|
| `defect.class` | coil | multinomial | 5,961 |
| `defect.severity` | coil | ordinal | 5,961 |

Six carry ZERO values: `defect.position`, `defect.rate_per_m2`,
`downtime.cascade_minutes`, `kpi.energy_per_ton`, `kpi.prime_yield`,
`kpi.throughput`.

**Note the tie.** Both usable keys hold exactly 5,961 values, so the
`outcome_key ASC` tie-break is what decides. Without it a replay could silently
rebind the page - which is precisely what happened to the presentation parameter
before T-045 Pack A.

Deterministic choice: **`defect.class` at grain `coil`**. A real target exists,
so nothing was invented.

### 4.2 The three Class-2 measures at runtime

| Measure | Result |
|---|---|
| `findingStatus` | 8 columns, 1 row, `NO_SUPPORTED_FINDINGS_CURRENTLY_PUBLISHED`, count 0 |
| `scoringCoverage` | 8 columns, 1 row. scope `OverallQualityRisk`, scored 500, reference 35,915, coverage 0.013922, **synthetic 500 of 500**, source `SCORING_SOURCE_SYNTHETIC`, model `MODEL_NOT_READY` |
| `analysisReadiness` | 11 columns, 5 rows. Independent heats 1311 >= 60 Ready; Outcome events 5847 >= 40 Ready; **Minority-class balance 2.0% < 3.0% BLOCKED**; Freshness 0.00 <= 1.00 Ready; Completeness 98.0% >= 95.0% Ready. **Overall Blocked, canRun False** |

### 4.3 Class 1 is unchanged and D1 holds

| Probe | Result |
|---|---|
| `materialCount` by `materialUnitType` | five-column envelope intact. Coil 17,012 / Slab 17,011 / Heat 1,892 |
| `observationCount` (D1 regression) | **301,560 against a trusted SQL population of 301,560** |
| `processStepDuration` | still HTTP 422 `aggregate_population_limit_exceeded`, as expected for an unmigrated measure |

### 4.4 Tests

| Suite | Result |
|---|---|
| `PlantProcess.Architecture.Tests` | 66 passed, 0 failed |
| D1 regression + downtime semantics, **executed against real PostgreSQL** | 6 passed, **0 skipped** |
| Whole-backend build | 0 errors |

The zero in that skipped column is the point. A full `dotnet test` reports these
suites as Skipped without `PPIQ_TEST_PG_CONNSTRING`, and a skipped suite is not
a pass.

### 4.5 Replay and convergence

`Invoke-T045-ReplayProof-v2.ps1`, all sections green:

- **A.** The eight-script chain 790 through 815 replayed twice. **0 rows moved on
  pass 1 and 0 on pass 2.** The chain settles and replaying an already-converged
  database changes nothing.
- **B.** All 28 widget codes converge identically across the four authoritative
  writers, on title, chart, dimension, measure and parameter. `RI_EQUIP` retired
  from every writer.
- **C.** All 28 seed-against-live comparisons match. `RI_EQUIP` present and
  inactive.
- **D.** `FDT_C` derived by the seeder rule; all four PA widgets bind it; zero
  widgets bind a parameter without observations.

---

## 5. THE PAGES, AS THEY NOW STAND

| Widget | Binding |
|---|---|
| `PA_KAVG` / `PA_KOBS` / `PA_TREND` / `PA_TABLE` | `FDT_C`, derived not literal |
| `PA_BYP` | observation volume by parameter |
| `CF_RATE` | Published Statistical Findings - `findingStatus` |
| `CF_TOP` | Findings Readiness (DF8) - `analysisReadiness` |
| `RI_KPI` | Average Risk Score **(Scored Population Only)** |
| `RI_TREND` | Scoring Coverage and Provenance - `scoringCoverage` |
| `RI_EQUIP` | **retired** - risk carries no equipment attribution |
| `RI_TABLE` | Risk by Material Type |
| `MI_RATE` | Analysis Readiness (DF8) - `analysisReadiness` |
| `MI_SEV` | Defect Mix by Material Type |

Every Class-2 binding is `chartType = 'table'`: a Class-2 result carries 8 to 11
columns and no `value`/`categoryKey`, so the chart paths cannot lay it out.

---

## 6. DEFECTS IN MY OWN WORK, NAMED

| Defect | Where it was caught | Mechanical guard now in place |
|---|---|---|
| Trailing comma in a generated PowerShell array | first console run | generator lint |
| Anchors compared LF against CRLF; `Set-FileText` produced CR CR LF | preflight, 10 of 14 anchors missed | one normalisation pass plus a per-anchor LF assertion |
| Guard corrected in a working copy, not in the shipped payload | the architecture test failed | the simulator now verifies only strings extracted from the generated pack |
| Pack B left `PlantProcess.Infrastructure.IntegrationTests` uncompilable through four commits | his own `dotnet build` | the pack build stage now compiles every project |
| Instrument reported "no findings" while three measures were refused | reading the transcript | a non-execution is a finding; plus a runtime freshness gate |
| `modelState` reported `MODEL_VERSION_RECORDED` over a 500-of-500 synthetic population | the runtime measurement | `ModelStateOf` - synthetic-only can never rise above not-ready |
| The replay proof asserted per-script idempotence, a stronger claim than the contract makes | its own five failures | the proof asserts chain convergence over two passes |

---

## 7. REMAINING DEBT, REPORTED AND SPLIT

Not absorbed into T-045, per the timebox rule.

| Item | Owner |
|---|---|
| `QM_BREAK` and `QM_TABLE` both ask `defectCount by gradeOrRecipe`. Pack G removed their duplication against `QM_SEV` and left them duplicating each other. Both were certified PASS by T-044 on this dimension, so the duplication pre-existed live. Quality Monitoring is an operational page. | T-044 recertification |
| D9: active `PAGE_*` page-builder dashboards a customer could navigate into. Eight `downtime_by_line_*` / `yield_by_grade_*` pairs surfaced in the 815 sweep. | presentation hygiene, before any demonstration |
| `DomainArchitectureTests` - `Definitions.DefinitionVersion` does not inherit `BaseEntity` | T-039 |
| `WidgetDefinitionVersioningTests` - `relation "ppiq_definition_versions" does not exist` in the target database | T-039 / environment |
| `AnalysisReadinessDto.IndependentHeats` is steel vocabulary in a generic contract | readiness contract owner |
| `'defect.class'` is a literal in the four seeders rather than derived from `ml_outcome_definitions` | presentation tooling |
| Seven measures still execute on the pre-D1 capped path | D1 remediation |

---

## 8. WHAT THIS DOCUMENT DOES NOT CLAIM

**A rebuild from an empty database was not performed.** The replay proof shows
that replaying every correction moves nothing and that source matches live. It
does not show that an empty install reaches the same place. That needs
`Rebuild-PresentationDb` against a scratch database with migrations applied, and
it is destructive.

**Legibility was not asserted by a machine.**
`Invoke-T045-PageSurfaceCheck.ps1` executes every widget on the four pages and
asserts that each one runs, keeps the shape its class promises, and carries no
forbidden vocabulary. No assertion in it can tell you whether an 8-to-11-column
table renders readably at the width a customer sees. That is the human check
recorded in `T-045_BROWSER_WALK.md`.