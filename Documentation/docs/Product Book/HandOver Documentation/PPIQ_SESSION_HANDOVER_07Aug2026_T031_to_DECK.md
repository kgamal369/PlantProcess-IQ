# PPIQ SESSION HANDOVER
**Session:** 05-Aug 23:21 dump -> 07-Aug 17:30
**Scope covered:** T-031 closure work, T-069, T-070, T-071, and the customer presentation deck
**Read this before touching anything. Everything below was measured, not assumed.**

---

## 0. HOW TO USE THIS DOCUMENT

The next session must NOT re-investigate what is written here. Every claim below
is either a measured result, a quoted document ruling, or an explicitly labelled
open question. Where something is unknown it says so.

**The single most important rule Karim set this session, and it governs everything:**

> **SOURCE-LEVEL PASS is not RUNTIME PASS, and RUNTIME PASS is not VISUAL ACCEPTANCE.**

A pack whose self-checks are green proves only that the file on disk says what
you intended. It does not prove the code runs, that the CSS reaches the browser,
or that a human finds the result acceptable. This session produced a concrete
example of each failure mode. They are documented in section 6.

---

## 1. STANDING RULES AND WORKING AGREEMENTS

### 1.1 Delivery format (absolute)
- **Every deliverable is a PowerShell 5.1 apply pack.** Never a zip. Never
  "paste this into DevTools". Never an ad-hoc command.
- **Contract:** preflight -> backup -> anchored replace -> self-check -> gate ->
  auto-revert. Report-only by default; nothing written without `-Apply`.
- **Every pack ships with its full run block**, in execution order, opening with:
  ```
  cd C:\Workspace\PlantProcess-IQ
  Move-Item "$env:USERPROFILE\Downloads\<file>.ps1" tools\packs\ -Force
  Unblock-File tools\packs\<file>.ps1
  ```
  Destination: `tools\packs\` for apply packs, `tools\run\` for runners.
- **Exception:** for a single-line source edit, tell him the exact line and the
  exact replacement rather than shipping a pack. He used this successfully twice
  this session (the `RegionMap` -> `WorldMap` fix and the tagline string).
- **Encoding:** pure ASCII, UTF-8 no BOM via
  `[System.IO.File]::WriteAllText(path, text, New-Object System.Text.UTF8Encoding($false))`.
  **Match the target file's own line endings** - do not apply a global default.
- No `&&` in PowerShell. Cuddled `} else {`. No em-dashes or curly quotes.

### 1.2 Working discipline he demanded (02-Aug, still in force)
- Re-read the actual code before every review; never work from notes.
- Build a mechanical guard whenever a defect class is mechanical.
- State arithmetic openly; never fit a number to a budget.
- **Name your own defects before he finds them.**
- When a finding falls outside every bucket the task text defines,
  **NAME THE GAP AND ASK FOR A RULING** rather than inventing a bucket.

### 1.3 Backlog adherence
`PPIQ_Backlog_v2_9_1_03Aug2026.md` / `.xlsx` is the only scope. If it is not
written in the backlog, do not do it. Tasks run in dependency order; a task is
complete including sign-off before the next starts.

### 1.4 Presentation-readiness mode (declared 06/07-Aug)
Fix proven blockers, verify in the browser, avoid new investigation loops,
parallelise non-conflicting work, keep moving. Do not stop for a ruling unless a
genuine architecture contradiction or a presentation risk appears.

### 1.5 Website quality doctrine (PERMANENT, all M1-P5 website work)
> **The strongest existing section is the minimum acceptable standard for the
> whole public site.** Anything below it must be raised when it becomes visible,
> regardless of who created it or when.

- Functional correctness is NOT the website definition of done. Typography,
  hierarchy, spacing, graphics, motion, responsive behaviour and perceived
  sophistication are all acceptance criteria.
- One coherent visual system. A new primitive must EXTEND the design system,
  never bypass it.
- **Every wire must connect source port -> connection -> destination port. No
  line may terminate in empty space.**
- Motion is part of the system, not decoration. Respect reduced motion.
- **Closing test:** compare the new surface with the strongest existing surface.
  If it makes the company look less sophisticated, it is not done.
- **Fix the system, not the screenshot.**

---

## 2. WHERE THE WORK STANDS

| Task | Status | Notes |
|---|---|---|
| **T-030** | **DONE** (he ruled: not reopened) | Terminology in its evidence may be corrected cheaply; do not create a subtask |
| **T-031** | **CORE IMPLEMENTATION COMPLETE**, 4 items deferred | See 2.1 |
| **T-069** | **DONE**, all 4 deliverables | Website five-product architecture |
| **T-070** | Partially done | Route audit green; visual work done; frozen text not formally closed |
| **T-071** | **CODE COMPLETE AND COMMITTED**, human smoke outstanding | See 2.4 |
| **DECK** | Delivered as `/deck` route | Presentation surface, unpublished |

### 2.1 T-031 status, recorded exactly as he worded it
```
T-031 CORE IMPLEMENTATION COMPLETE

Certification              PASS
10 dimensions              PASS
Divergence detection       PASS
Rollback cleanliness       PASS

Deferred closure items:
- unconditional CI truth-gate integration
- backup/restore proof
- final schema-qualified src_* dependency check
- src_* retirement
```
**These four are NEITHER COMPLETED NOR WAIVED.** They return later as one compact
T-031 closure bundle. Written to `docs/m1/evidence/T-031_STATUS.md`. A self-check
asserts that file never contains the string `T-031 DONE`.

### 2.2 T-069 - the five-product website architecture
All four frozen deliverables complete:
1. Products mega-menu in the header
2. `/products` portfolio page
3. `LegacyProductRoute` removed, canonical routes generated from the registry
4. Validator rewritten to assert the correct architecture

### 2.3 T-070 - presentation route polish
Route audit installed and green. Industry-generic pass done. Page grid fixed.
The SOU/PPIQ route split done. The frozen text's own closure was never formally
declared - **treat T-070 as open**.

### 2.4 T-071 - the persistent assistant dock
Committed as `4fe116c9`, 8 files, +484/-60. Tests all green. **The frozen
Validation still requires a human check that was never run:** open the dock on at
least five different pages and confirm the conversation persists; confirm the
collapsed state obscures no control.

---

## 3. THE T-031 STORY - WHAT WAS ACTUALLY WRONG AND HOW IT WAS FIXED

This is the most valuable technical content in this handover. Do not re-derive it.

### 3.1 The doctrine that settles the layer question (do not re-investigate)
**Chapter 3 section 4.5.2a rule 4, verbatim:**
> `src_*` is a source-shaped donor schema, **`dump_store` is the current
> transitional physical name of staging**, and `ppiq_staging` is the final
> staging name. ... `src_*` is temporary donor state, not a product layer.
> **It is not staging and must never be called staging.**

Karim ruled this permanent: **do not investigate the meaning of these layers again.**

```
src_*         = source-shaped DONOR / emulator state. NOT staging.
dump_store    = current transitional physical STAGING layer.
canonical     = current plant model.
ppiq_staging  = final staging name; do NOT rename to it unless the frozen
                backlog explicitly owns that rename.
```

### 3.2 The root cause, proven from source
**The donor was a CAPTURE-mode emission; canonical was a FLEET-V2 emission.**

`Backend/tools/generate_fleet_v2_donor.py`:
- `--mode` choices are `capture` and `fleet-v2`, and **capture is the DEFAULT**.
- Capture is frozen at scale 1 and refuses any other scale. It exists as the
  regression test for retirement gate condition 1.
- Fleet-v2 defaults to `SCALE_DEFAULT` 3, the T-015 target of 1,890 heats and
  17,010 coils.
- Line ~102 holds the captured baseline `DEFECTS` list, commented
  `# FAULT: the Pareto is flat - six codes inside a three-point spread`:
  `PINHOLE 351, SCALE 347, EDGE_CRACK 341, ROLLED_IN 335, SCRATCH 319, WAVINESS 294`.
  **Those were the live staging numbers exactly.**
- Line ~2574 branches: `if mode == "fleet-v2"` uses `FLEET_DEFECTS` (14 codes,
  26/15/12 Pareto via `largest_remainder`); the `else` branch uses the legacy list.

**One flag apart, two different plants.** That single fact explained all three
certification failures.

### 3.3 What T-024 actually did (checked, not assumed)
`tools/run/Invoke-PpiqT024Canonical.ps1`:
- Gate **G2** emits `--mode capture` to a TEMP FILE only, comparing against pinned
  `$ExpectedCaptureSha = 11EDF4B275A106C86D75EA3147D47B56F7763AD9EE2D258487953B7155939AD7`
  (generator SHA pinned as `CB4C097D70D49B0F8875F76D8D81BBA28C651BC332D1DCA50E23FD1558F12DE1`).
  **It never loads it.**
- Gate **G5** emits `--mode fleet-v2 --emit canonical` and loads THAT.
- Therefore T-024 never loaded a donor at all. The live `src_*` was the
  capture-mode baseline left by the earlier generator-build tasks.
- **Consequence: overwriting `src_*` is safe**, because retirement gate condition
  1 is proved from the generator's frozen CODE against a pinned hash, in a temp
  file, never from the live rows.

### 3.4 The reset path - there was none, and one was not needed
Searched the whole tree. **Zero `TRUNCATE` anywhere touching `dump_store`,
`src_`, or the stage functions. No purge, no reset, no full-reload route.**

But: **`ppiq_run_stage1_delta_import` already contains the full-load semantic.**
`130_phase03_two_stage_delta_import_architecture.sql` line 925:
```sql
IF v_last_before IS NULL THEN v_index_condition := 'TRUE';
```
**A NULL watermark makes stage 1 copy the whole donor table.** So no new
procedure, function or endpoint was needed - clearing the population and nulling
`last_index_value_text` is enough. On success stage 1 re-arms the watermark via
`last_index_value_text = coalesce(v_last_after, last_index_value_text)`.

**False friends discovered:**
- `ppiq_run_two_stage_full_cycle` means "run BOTH stages", **not** "full reload".
  Calling it appends another delta.
- `ppiq_register_dump_source` is idempotent and NON-destructive:
  `CREATE TABLE IF NOT EXISTS %I.%I (LIKE ... INCLUDING ALL)`, and its UPDATE
  branch sets every field EXCEPT `last_index_value_text`.
- No shadow-table trap: the dump name is built as `schema || '__' || table` but
  `ppiq_identifier` collapses `'_+' -> '_'`, resolving to the same
  `src_meltshop_pg_heats` already registered.

### 3.5 Stage 2 is NOT staging-only
`ppiq_run_stage2_canonical_refresh` writes `public.material_units` (3 inserts),
`public.genealogy_edges` (2) and `public.quality_events` (1), tagging provenance
`phase3-dump:src_hsm_oracle_shape.hsm_coils` and siblings.

**Karim's ruling: never run stage 2 on the main presentation database.** The
canonical Fleet-v2 population is the authority T-024/T-025 used. On
`ppiq_presentation` run ONLY: backup -> staging reset -> stage-1 full load ->
staging verification.

### 3.6 The schema-widening trap (caught before it broke a run)
In fleet-v2 mode the donor emission also runs `FLEET_ALTERS + FLEET_ALTERS_T017 +
FLEET_ALTERS_T018 + FLEET_ALTERS_T020`, adding sulphur, phosphorus and aluminium
columns to the donor schemas.

`dump_store` tables were created `LIKE` the OLD source, and
`ppiq_register_dump_source` uses `CREATE TABLE IF NOT EXISTS`, so it will NOT
widen them. Stage 1 builds its column list from `information_schema` on the
SOURCE, so it would insert three columns the dump table lacks and fail.

**Fix:** the dump tables must be DROPPED and RE-REGISTERED, registry-driven,
using each registry row's own arguments. Karim ruled this belongs in the RESET
(which backs staging up first), not in the donor re-emitter.

### 3.7 The foreign-key ordering trap
The generator's `ORDER` list starts with `src_meltshop_pg.heats` and the emitted
SQL deletes PARENT FIRST, while `110_phase1_demo_source_shapes.sql` declares
three FKs inside the donor schemas (`REFERENCES src_meltshop_pg.heats`,
`src_caster_oracle_shape.cast_sequence`, `src_hsm_oracle_shape.hsm_coils`).

The first ever load worked only because the tables were EMPTY and the DELETEs
were no-ops. Against a populated donor it fails on `lf_treatment_heat_no_fkey`.

**Fix:** one multi-table `TRUNCATE TABLE a, b, c...` over every base table
discovered in the registry's source schemas, before applying the emission. It
satisfies FKs among the listed tables and REFUSES if anything outside the set
references them. The emitted parent-first DELETEs then become no-ops.

**Honest consequence, stated in the runner:** the pre-clear COMMITS, so if the
emission then fails the donor is empty and must be restored from the backup.

### 3.8 THE BACKUP DEFECT - critical, do not repeat
Both runners originally called:
```powershell
pg_dump -n <schema> -t public.source_table_dump_registry ...
```
**pg_dump lets `-t` WIN over `-n`.** Every file held ONLY the registry table.
Both reported 0.01 MB and were printed as proof. The 09:24 and 10:04 "staging
backups" contain NO `dump_store` data.

**Fix:** a `DumpPart` helper takes TWO SEPARATE dumps and size-checks each. The
staging dump carries a 200,000-byte floor when staging has rows; the registry
dump 1,000. **A dump that selected nothing now fails its own check.**

### 3.9 The final measured state (07-Aug 10:29, GREEN)
```
Donor re-emission     GREEN
  heats 1,890 | coils 17,010 | pass measurements 119,070
  pickle orders 15,295 | qa 45,885 | defects 5,961 | downtime 630
  zero ROLLED_IN, zero donor defect codes absent from the catalogue
  zero donor coils absent from canonical
  zero canonical coils absent from the donor
  zero donor coils whose canonical parent edge disagrees
  Real backups: donor schemas 2.116 MB, registry 0.008 MB

Shape check         3 of 10 dump tables stale (hsm_coils 2 cols,
                    downtime_events 1, heats 4) - only those rebuilt
Staging reset       202,491 rows, matched the donor on all ten tables
Certification       10 dimensions measured, 10 passing, 0 red
  staging coils absent from donor        0
  staging coils absent from canonical    0
  canonical coils absent from staging    0   (was 11,340)
  canonical defect codes not in staging  3   (legacy catalogue rows, reported not gated)
Injected divergence baseline 0 -> 1, PROVEN RED, rollback residue 0
```

### 3.10 The certification design (do not redesign)
Subject: **`dump_store` STAGING versus canonical PLANT.** `src_*` is the donor and
is never a side of the comparison.

- **Direction is gated STAGING -> CANONICAL only.** The reverse is reported and
  never gated, because staging was a 1x subset of a 3x canonical population;
  gating the reverse repeats the T-030 verdict error.
- **Row counts are NEVER compared** - 4.5.2a rules the layers are shaped
  differently by design.
- **Fail closed:** a null or empty result is `NOT COMPUTABLE` and is RED, never
  treated as zero. Preconditions refuse on empty layers.
- If every dimension passes but the injected divergence does NOT turn it red,
  that is its own RED exit. **A gate that cannot fail is not a gate.**
- The assertion now lives IN THE DATABASE: a `DO` block measures the baseline,
  injects, re-measures, and `RAISE EXCEPTION` when the count did not increase, so
  psql exits non-zero AND the transaction aborts - the failure path IS the
  rollback path. The runner keys off `$LASTEXITCODE`, not a parsed sentinel.

**Ten measured dimensions:** grades, equipment identities, defect vocabulary,
downtime semantics, chemistry vocabulary, QA definition set, QA units, genealogy,
time horizon, planted phenomena.

**Chemistry is DISCOVERED** from `information_schema` (`%_pct` columns on the
staging heats table), never hard-coded. **Planted phenomena** is tested as the
top-five defect Pareto ordering matching between layers. **Time horizon** is
containment with a stated one-day tolerance, not equality.

### 3.11 The authoritative vocabulary (from the generator, not from assumption)
`generate_fleet_v2_donor.py` holds:
- `CANON_PARAMETERS` ~30 codes: `CARBON_PCT, MANGANESE_PCT, SILICON_PCT,
  SULPHUR_PCT, PHOSPHORUS_PCT, ALUMINIUM_PCT, TAP_TEMP_C, OXYGEN_NM3, POWER_KWH,
  LF_ARGON_NM3, LF_CALCIUM_M, LF_FINAL_TEMP_C, CASTING_SPEED_MPM, SUPERHEAT_C,
  MOULD_LEVEL_AVG, FDT_C, CT_C, THICKNESS_MM, WIDTH_MM, ROLL_FORCE_KN,
  ROLL_GAP_MM, ROLL_SPEED_MPS, ROLL_TEMP_C, ACID_CONC_PCT, BATH_TEMP_C,
  QA_WIDTH_MM, QA_THK_MM, QA_ROUGHNESS_UM ...`
- `CANON_EQUIPMENT`: EAF-01/02, LF-01/02, CCM-01/02, HSM-01, PKL-01/02,
  PARSYTEC-01/02
- `FLEET_DEFECTS`: 14 codes, SCALE dominant at 26%, down to the two NEGATIVE
  CONTROLS `OIL_SPOT` and `SENSOR_ARTEFACT`
- `GRADES`: HSLA-420, DX51D, S235JR, S355MC, IF-LOW-C, DP600
- **Line 1950 declares the QA mapping:**
  `qa_param = {"WIDTH": "QA_WIDTH_MM", "THK": "QA_THK_MM", "ROUGHNESS": "QA_ROUGHNESS_UM"}`

**Canonical column names** (snake_case of EF entity properties):
`material_units(material_code, material_unit_type, product_family,
grade_or_recipe, site_id, production_start_utc/end_utc)`;
`defect_catalogs(defect_code, defect_name, defect_category, industry_template)`;
`quality_events(material_unit_id, defect_catalog_id, event_at_utc, event_type,
severity, decision)`; `equipment(equipment_code, equipment_name, equipment_type,
site_id, area_id)`; `parameter_definitions(parameter_code, parameter_name,
value_type, unit_of_measure, parameter_category, expected_min/max_value)`;
`downtime_events(material_unit_id, equipment_id, started_at_utc, ended_at_utc,
downtime_type, stopped_minutes, production_impact_minutes, reason_code)`;
`genealogy_edges(parent_material_unit_id, child_material_unit_id,
relationship_type, contribution_weight, is_transition)`. Base entity adds
`id, created_at_utc, is_synthetic, source_system, source_record_id, is_deleted`.

### 3.12 The CI landing-site ruling
Do NOT force the certification into stage 3. `ci-test-db.sh` builds an ephemeral
database from the EF script plus every numbered SQL plus the seeds -
`110_phase1_demo_source_shapes.sql` contains **zero `INSERT INTO`**, so it creates
the five `src_*` schemas EMPTY, while canonical is populated by the legacy demo
seeds. A certification there would compare an empty staging against a legacy
canonical and fail permanently for the wrong reason. **No CI stage builds
`ppiq_presentation` at all.**

**Ruled design (NOT YET BUILT):** one dedicated UNCONDITIONAL M1 presentation
truth-gate stage that builds an EPHEMERAL presentation DB from the authoritative
path: create DB -> install numbered SQL -> generate donor -> materialise
`dump_store` -> stage 1 -> **stage 2 (only here)** -> fresh canonical -> run the
cross-layer certification -> inject divergence inside a transaction, prove RED ->
roll back and dispose. **No `when {}` clause.** Never use the developer's
persistent `ppiq_presentation` as the CI subject.

### 3.13 `Rebuild-PresentationDb.ps1` is NOT the authority for Fleet-v2
Step 1 is `pg_restore --clean` of `deploy\.ppiq-snapshots\ppiq_app_20260713_203359.dump`
- a **13-July fixture that predates T-024's 04-August Fleet-v2 canonical
replacement**. Its only reference to the dump is step 3, a provenance rewrite of
`material_units.source_system` from `phase3-dump:src_*` to system names.

**Ruled: do not wire the staging reset into it and do not replace the fixture
inside T-031.** Record the limitation. It would full-load the obsolete donors the
fixture restores.

---

## 4. WEBSITE WORK - T-069, T-070 AND THE DECK

### 4.1 The five-product architecture (T-069)
**Chapter 6 6.2.1:** SOU has FIVE separate products - PlantProcess IQ, MES, QES,
Yard and Warehouse Management, Energy Management. **PPIQ is the flagship. PPIQ is
not the company and not a container around the other four.**

**What was there before:** `LegacyProductRoute` (5 lines) mapped
`mes -> /packs/reliability`, `qes -> /packs/quality`, `yard -> /packs/yard`,
`energy -> /packs/energy`. That map WAS the wrong architecture, stated in code.

**Decisive discovery:** a registry-driven product page renderer already existed
and was completely unwired - `src/pages/products/ProductPage.tsx` (182 lines),
`src/routes/phase7Products.routes.tsx`, `src/content/products/model.ts`,
`index.generated.ts`. Nothing imported any of it. **And the two registry entries
were not products:** `mes.ts` is id `mes-integration`, headline
*"We read your MES. We don't replace it."* - PPIQ's integration stance, not an
MES product.

**Ruling:** do NOT repurpose PPIQ capability content into standalone-product
content. A CLEAN registry was created: `src/content/portfolio/souProducts.ts`.

### 4.2 The route conflict, resolved
His shorthand said `/products/yard` and `/products/energy`. **Chapter 6.2.12 names
`/products/yard-warehouse-management` and `/products/energy-management` as
canonical** with the short forms as compatibility redirects. Resolved by carrying
both: `slug` (chapter canonical) plus `aliasSlugs`. **He confirmed Chapter 6.2.12
is authoritative over his shorthand.**

### 4.3 Files created / changed on the website
| File | What |
|---|---|
| `src/content/portfolio/souProducts.ts` | NEW. The single authority: five products, `productPath()`, derived `productAliasRedirects`, `flagshipProduct`, `findProductBySlug`, `stackLayers` |
| `src/pages/products/ProductsPortfolioPage.tsx` | NEW. `/products`, later given the full visual pass |
| `src/pages/products/PortfolioProductPage.tsx` | NEW. Shell for the four non-flagship products |
| `src/pages/SouHomePage.tsx` | NEW. The SOU company home |
| `src/pages/DeckPage.tsx` | NEW. The presentation route |
| `src/pages/worldPaths.ts` | NEW. Real world geometry, 272 paths |
| `src/App.tsx` | Mega-menu, canonical routes, `LegacyProductRoute` deleted, `/deck` route, Presentation nav entry |
| `src/components/graphics/ArchitectureFlowScroll.tsx` | Rebuilt in the hero vocabulary |
| `src/components/graphics/GoldenThreadScroll.tsx` | Rebuilt industry-generic |
| `src/pages/NewHomePage.tsx` | Imports `motion-roi.css`; industry list; sample source table |
| `src/styles/new-landing.css` | Archflow rules, portfolio, page grid, deck |
| `src/styles/phase10.css` | Mega-menu styles, hover bridge |
| `src/styles/motion-roi.css` | Archflow block moved out |
| `scripts/validate-commercial-v2.mjs` | 20 architecture assertions replace the two that pinned the wrong model |
| `playwright.verify.config.ts`, `tests/verify/*.spec.ts` | The runtime gate |

### 4.4 The route split (final state)
```
/                          -> SouHomePage       (company)
/products                  -> ProductsPortfolioPage
/products/plantprocess-iq  -> NewHomePage       (the full PPIQ narrative, moved intact)
/products/mes|qes|yard-warehouse-management|energy-management -> PortfolioProductPage
/product                   -> Navigate to /products/plantprocess-iq
every aliasSlug            -> Navigate to its canonical route
/packs/:code               -> survives as a PPIQ concept, NOT in the Products menu
/deck                      -> DeckPage (presentation, in the header as "Presentation")
```
**`LegacyProductRoute` is deleted and `/products/:code` is gone as an
architectural route.** All five canonical routes and every alias are GENERATED
from the registry - no product path is hand-written.

### 4.5 The industry-generic pass (T-070)
The public narrative must not say steel. Changed:
- `HEAT -> RAW INPUT`, `SLAB -> IN PROCESS`, `COIL -> FINISHED UNIT`
- `superheat window -> process window`, `L2_CASTER -> LINE 2 CONTROL`
- `l2_caster_heats -> line2_process_batches`
- industry list now leads Oil & Gas, Water & Utilities, Food & Beverage
- `Heat-to-product genealogy -> Input-to-product genealogy`

**Deliberately kept:** `FounderAuthority.tsx` keeps "Level 2 automation - Flat
steel" and the PSI years. That is a real CV and the honesty rule protects it.
`brand/plantProcessBrand.ts` keeps "Not steel-only".

**STILL OUTSTANDING:** `src/components/graphics/GoldenThread.tsx` (a DIFFERENT
component from GoldenThreadScroll) still reads "Meltshop heat", "Caster slab",
"Casting speed - width - cooling". Its anchors contain non-ASCII middots so it
needs a verified pass. **Determine which route renders it before editing.**

---

## 5. TEST RESULTS - DO NOT RE-RUN THESE

### 5.1 Website runtime gate (Playwright, `npm run verify:website`)
Installed by pack `T-069-W3`. Config points at the ALREADY-RUNNING dev server on
5180 and deliberately does not spawn its own.

| Run | Result |
|---|---|
| First run | Failed: Chromium binary missing. Fixed by `npx playwright install chromium` |
| Homepage gate | **11 passed, 0 failed** |
| + products suite | **16 passed** |
| + route audit | 39 passed / 11 failed -> both causes were MY test bugs -> **50 passed** |
| + identity split | **59 passed** |
| + visibility and menu | **61 passed** |

**What the 61 certify:** computed `max-width` exactly `1236px` served from
`new-landing.css`; the diagram narrower than the viewport; no label escapes its
node; the real separator renders and no HTML entity appears anywhere; every
connector endpoint lands on a drawn port; GoldenThread, ecosystem and ROI present
and painted; zero horizontal overflow at 1440/834/390; `/` shows five teasers and
ZERO PPIQ-only surfaces; the PPIQ route holds archflow, ROI and thread with no
HEAT/SLAB/COIL; all five product routes resolve; no dead links on six routes; the
Products menu opens on focus and Escape closes it with focus returned; the
computed opacity of every on-screen `.rv` element is above 0.9.

### 5.2 Commercial validator (`npm run validate:commercial:v2`)
- Before: 10 failures. Two were T-069's; **eight were pre-existing.**
- After the rewrite: **all 20 new architecture assertions PASS.**
- **8 failures remain and are NOT ours** - seven marketing copy strings
  (`Stop the Losses.`, `The Crime Scene`, `Tracing the Footprints`,
  `The Trial & Verdict`, `Execution & ROI`, `The model explains. The engine
  computes.`, `Start a Proof of Value`) plus `trust language Read-only by design`.
  They are absent from the sources the validator reads. **This is a website copy
  decision, not a code defect.**
- **A defect found: the validator asserted the canonical tagline lives in
  `App.tsx`. It never did** - it is in `src/brand/tagline.ts`, and the real string
  is `Connect Your Plant Data. Understand Your Process.` (the validator was
  missing the word **Data**). That check had been red since the phase-7 brand pack.

### 5.3 Frontend app suites (T-071)
```
assistantPersistence.test.tsx      2 passed
src/test/architecture             81 passed / 20 files
full frontend suite               513 passed, 3 failed
npx tsc -b                        clean, no output
```
**The 3 failures are `JourneyRail.certification` step-mapping assertions belonging
to the parallel M1-P2 authoring track.**
```
JourneyRail.certification: 3 known parallel-track failures
T-071 contribution: 0
```

### 5.4 Website build
`npm run build` = `tsc -b && vite build`, vite 8.1.5. Final observed:
1808 modules, `index-DCuqkzBM.css` 76.91 kB gz 15.08, `index-GMDLgMTw.js`
429.04 kB gz 132.84. **The CSS bundle hash and size are the runtime evidence that
a stylesheet change actually reached the build** - see 6.2.

---

## 6. FAILURE MODES DISCOVERED THIS SESSION - THE MOST VALUABLE SECTION

### 6.1 Guard-versus-content collision (EIGHT occurrences, all mine)
A self-check that matches text the pack ITSELF produced, or asserts a string that
never existed. Every one auto-reverted correctly, so nothing broke - but each
cost a run.

| # | Guard | Why it fired |
|---|---|---|
| 1 | `-not $raw.Contains('capability pack')` | My own header comment said it |
| 2 | `-not ($d -match 'autonomous')` | My own comment said "no autonomous control" |
| 3 | `independence:` count expected 5 | The interface field makes 6 |
| 4 | entity regex `"[^"]*&[a-z]+;` | Spanned out of `className="outt"` to a valid text entity |
| 5 | port count expected 8 | A `.map()` renders four ports from ONE literal (7 total) |
| 6 | `-not $q.Contains('1060px')` | The old value was also in the TEST TITLE |
| 7 | `goto("/")` expected 2 | The pack's own appended test adds a third |
| 8 | `-not $d.Contains('$')` | Matches every JSX template literal `${...}` |

**RULE: simulate every guard against the emitted output before shipping.**
**RULE: long prose fragments never match - the text wraps across lines.** Use
short single-line fragments.
**RULE: every survival guard must be grep-verified against the real file.**

### 6.2 Source-green but runtime-dead (the W1 false positive)
W1 applied with **36 self-checks green** and the CSS never reached the browser.
`motion-roi.css` is imported by NOTHING. The tell was the build: the JS bundle
hash moved and **the CSS bundle hash and byte size did not move at all** after a
stylesheet rewrite.

**This also corrected the root-cause arithmetic:** `.ppiq-archflow .af-t
{ font-size: 12px }` lived in that dead file, so the old labels were never styled
- they rendered at the SVG default 16 USER UNITS, then scaled 1.7x to ~27px.

**And the same dead file held the only styling for `.ppiq-goldenthread`,
`.ppiq-ecosystem` and `.ppiq-roi`** - three MORE homepage sections rendering
unstyled. All three are mounted: `GoldenThreadScroll` NewHomePage line 161,
`IntegrationEcosystem` 230, `RoiCalculator` 232.

### 6.3 Presence is not visibility
59 assertions passed against a page **nobody could see**. `.rv` sets `opacity:0`
and only becomes visible when an IntersectionObserver adds `.in`. That observer
lives in `NewHomePage`'s `useEffect`; `SouHomePage` used `.rv` and never mounted
it. **`toHaveCount()` and `boundingBox()` both succeed on an `opacity:0`
element.** The suite now reads computed opacity.

### 6.4 Clipping is not overflow
`scrollWidth - clientWidth` is ZERO when content is CLIPPED rather than
scrolling. Three homepage sections had their left edge cut off at wide viewports
while every overflow test passed. The guards now measure visible bounds and
compare each section's left edge against `.wrap`.

### 6.5 PowerShell variable traps
- **`$Home` is a READ-ONLY automatic variable.** Assigning it kills the pack
  instantly, before a single preflight line prints. Avoid:
  `$Home $Host $PSHome $Error $Args $Input $PID $PWD $True $False $Null $Matches $This $_`
- **Variable names are CASE-INSENSITIVE, so `$Css` and `$css` are the same
  variable.** One pack held `$Css` as a path and `$css = ReadAllText($Css)` as
  content - the path was overwritten by the file's own text.
- **NEVER blanket-rename case-insensitively** - the collision you are repairing
  guarantees the rename hits both members of the pair. It turned `$home = ''`
  into `$HomePath = ''`, wiping the path one line before use.
- **THE CHECK THAT CATCHES ALL OF THESE:** a data-flow scan of the pack's own
  PowerShell with here-strings excluded. Collect the set assigned from
  `Join-Path`, the set assigned from `ReadAllText`, and the set assigned `''`;
  lowercase all three and assert the intersections are EMPTY. Then assert every
  `$var` read resolves to an EARLIER assignment.
- **Brace/paren counting is meaningless on files whose strings contain partial
  code** (`'element={<Navigate to=...'`, `'souProducts.map((product)'`). Use
  `node --check` for JS, `PSParser::Tokenize` for PowerShell.
- Escaping: a `"` inside a double-quoted PowerShell string needs a BACKTICK, not
  a backslash. Safer: use a single-quoted here-string and avoid escaping entirely.

### 6.6 Other concrete traps
- **`pg_dump -t` wins over `-n`.** Two dumps, never one, each size-checked.
- **HTML entities:** JSX decodes them in TEXT but NOT inside strings. `<text>Level
  2 &middot; L2 DB</text>` renders; `["Predictions &middot; AI+ML", 122]` prints
  the entity literally.
- **psql prints command tags** (`ROLLBACK`) to stdout. Never parse the last line;
  emit a tagged value and match it, or better, key off the exit code.
- **A mega-menu panel with a gap** (`top: calc(100% + 14px)`) closes on
  `onMouseLeave` when the pointer crosses the dead space. A transparent
  `::before` bridge fixes it.
- **The app's bottom-right corner is occupied** by the language pill, the theme
  pill and the JOB LOG bar. Anything new there needs `bottom` above ~150px and a
  high z-index. The assistant dock was invisible for exactly this reason.
- **`lucide-react` is pinned at 0.383.0.** Do not import an icon without checking
  it is already imported somewhere. `Activity`, `Warehouse`, `Zap` were NOT
  available; `BrainCircuit`, `Factory`, `ScanLine`, `Layers3`, `Gauge` were.
- **These web files are CRLF.** An earlier extraction made them look LF and a
  pack "normalised" four files the wrong way. Always read the target's own
  convention and write it back.
- **`tools/packs/` appears to be gitignored** - `git add` of pack files was
  refused. Pack effects live in the runners, so nothing is lost, but confirm
  whether that ignore is intentional.

---

## 7. T-071 - THE ASSISTANT DOCK

### 7.1 Architecture (final)
```
AppLayout
  +- AssistantDockProvider     owns config, turns, busy, status, askAssistant
       +- .piq-workspace > Outlet
       +- AssistantDock         presentation only, suppressed on /assistant
```
- `AssistantDockContext` is the SINGLE owner of the conversation and the ONE
  `assistantApi.askAssistant` call, lifted verbatim from `AssistantRuntimePage`.
- `AssistantRuntimePage` is now a consumer; it keeps `<AssistantChat>` and holds
  no state.
- `/assistant` suppresses the global dock via
  `location.pathname.startsWith("/assistant")` - one conversation, never two
  expanded surfaces.
- **NO browser storage.** The conversation lives for the authenticated layout's
  lifetime and dies on logout. T-071 is navigation persistence, not cross-login
  persistence.
- `AppLayout` line ~372 wraps `<Outlet />` in `.piq-workspace` inside `<main>`.

### 7.2 The architecture assertion MOVED, not deleted
`assistantChain.test.ts` asserted `AssistantRuntimePage.tsx` contains
`assistantApi.askAssistant(`. That call moved, so the assertion was repointed at
the provider. **The rule it protects is unchanged: every assistant request goes
through the existing api client, from exactly one place.**

### 7.3 The persistence test
Nested routes: a parent route element holds the provider with routes A and B as
children - the same lifetime topology `AppLayout` has. The turn is created
through the PUBLIC contract by typing into the real `AssistantChat` with
`assistantApi` mocked at the module boundary. **No test-only setter was added to
production code.** The surviving turn count IS the proof the provider was not
remounted.

**Judgement call recorded:** the parent route element is the provider itself
rather than the full `AppLayout`, to avoid dragging `AppLayout`'s nav fetches and
`LogPanel` into jsdom. Same lifetime topology.

### 7.4 Three defects the suites caught
- Raw `<button>` in the dock violated the PPIQ-T11 ratchet -> `StandardButton`.
- `assistantDock.test.ts` needed `// @vitest-environment node` on line 1
  (PPIQ-T14). **The persistence test deliberately KEEPS jsdom** - a router
  navigation proof without a DOM proves nothing.
- The mock returned `answer.answer`; `AssistantChat.tsx` line 146 renders
  `answer.text`. Fixed to the real shape and the assertion was KEPT, not weakened.

### 7.5 KNOWN OPEN DEFECT (T-071 caused it)
The dock panel footer shows:
```
Assistant configuration not reachable: 401 Unauthorized
```
Before T-071, `getAssistantConfig()` ran only when the user navigated to
`/assistant`, by which time auth was established. The provider now mounts with
`AppLayout`, so the call fires on the FIRST RENDER of every authenticated page,
before the token is available.

**Proposed fix, NOT built:** make the config fetch LAZY - run it on first expand
of the dock, with one retry. Removes the 401, avoids a request on every page
load, needs no knowledge of the auth plumbing. `ask()` already degrades safely via
`config?.allowedTools ?? []`.

### 7.6 The CSS fix that made it visible
`.piq-dock` changed from `bottom: 20px; z-index: 60` to
`bottom: 150px; z-index: 2000`. **This may still be uncommitted.**

---

## 8. DEPLOYMENT, SERVER AND PIPELINE

### 8.1 Local run contract (verified)
```
API:  .\scripts\run\start-api.ps1 -Profile presentation
      -> http://localhost:5063 | WEB_PORT=5173 | DB=ppiq_presentation@5432
Web (app):     port 5173
Web (website): port 5180 / 5181
DB connection: -h 127.0.0.1 -p 5432 -U ppiq_dev -w
               $env:PGPASSWORD = 'ppiq_dev_local_only'
```
**`start-api.ps1` line 3 defaults to `-Profile local`, which resolves to
`ppiq_app` - the tenant-NULL database.** The demo MUST launch with
`-Profile presentation`.

### 8.2 BLOCKER ENCOUNTERED AND ITS DIAGNOSIS
```
System.InvalidOperationException: PendingModelChangesWarning:
The model for context 'PlantProcessDbContext' has pending changes.
```
The API refused to start. **Nothing in T-071 touched the backend** - the model
change came from the parallel authoring track (`Definitions/` for T-039).

**Recommended sequence (he fixed it himself; the exact fix was not reported
back - CONFIRM WITH HIM):**
1. Back up `ppiq_presentation` with `pg_dump -Fc` FIRST and check the file is
   megabytes, not kilobytes.
2. `dotnet ef migrations list --startup-project ..\PlantProcess.Api`
3. `dotnet ef migrations add <Name> --startup-project ..\PlantProcess.Api` -
   this only WRITES a file.
4. **Read the generated `Up()` before applying.** Empty or `AddColumn`/
   `CreateTable` only = safe. Any `DropColumn`/`DropTable`/`AlterColumn` on a
   populated table = stop.
5. Emergency fallback only if unsafe: suppress the pending-changes check at
   `Program.cs` ~line 800.

**No Migrations folder existed in the 05-Aug dump** - migrations were added after
that date by the parallel track.

### 8.3 CI pipeline facts (measured from the Jenkinsfile)
- Stage 3 runs `dotnet test Backend` inside an SDK sibling against an EPHEMERAL
  database from `deploy/scripts/ci-test-db.sh`.
- That script runs the EF idempotent script, then every
  `Backend/database/scripts/*.sql`, then `Backend/database/seed/*.sql`.
- **The e2e stage deliberately carries no `when {}` clause**, and
  `CiPipelineTruthGateTests` asserts it, because a skippable gate is not a gate.
- **`tools/ci/validate-real-ui-gates.cjs` is an ORPHAN.** A content grep for
  `validate-real-ui-gates` returns ZERO references anywhere, and the root
  `Jenkinsfile` contains ZERO occurrences of `test:visual`,
  `test:phase56:e2e`, `test:a11y` - the three commands that gate requires.
- **`Frontend/PlantProcess.Web/tools/phase56/apply-phase5-phase6-full-ui-migration.cjs`
  still inserts `stage('2b. Phase 5/6 UI quality gates')` into the root
  Jenkinsfile**, whose only test commands are three `--list` enumerations.

**Neither of these two findings has a backlog task. They have survived three
repository dumps. They need a ruling, not silent action.**

### 8.4 Repository encoding state (measured 05-Aug dump, 2,409 files)
- 963 non-ASCII lines across 296 source files; mostly box-drawing U+2500 (649)
  and em-dash U+2014 (455).
- **SEVEN files carry true MOJIBAKE** (a UTF-8 em-dash read as latin-1), all
  dated June/July: `Website/PlantProcess.Website/src/App.tsx` (159 bytes - **still
  visible on the live site as `outcomesâ€"without`**),
  `tools/realization/continue-phase03-phase04-from-t016.cjs` (double-encoded,
  worst), `tools/realization/pack-r12-phase01-phase02-closure.cjs`,
  `scripts/test/validate-current-green.ps1`,
  `Backend/database/scripts/420_p3_value_evidence_hmi.sql`,
  `430_phase3_phase4_certification_mapping_health.sql`, and the
  journey-certification scorer.
- **This is a real visible content defect and it is NOT in any task.**

### 8.5 Audit-signal report (unchanged across three dumps)
56 signals: dev seed 16, hardcoded IP 15, `--list` 8, TODO 7, bootstrap admin 3,
gate-closing 3, catchError 3, `__DefaultConnection` 1.
**Four of the twelve CRIT are the scanner reading its OWN rule table** -
`Get-AuditSignalsForContent` (line ~712) still has no path exclusion. The one-line
fix has been skipped across three dumps.

---

## 9. THE PRESENTATION DECK

### 9.1 What it is
`/deck`, reachable from the header as **"Presentation"** (between Pricing and
About). Karim's stated intent: **for the presentation only, not published, to be
removed afterwards.**

Four tabs: **Me | Application | Example tutorial | Pricing and licence**.

### 9.2 To remove it completely after the presentation
```
cd C:\Workspace\PlantProcess-IQ
.\tools\packs\apply-DECK-01-presentation-route.ps1 -Revert
```

### 9.3 His verified biography (use exactly this)
```
2013 - 2018  EZDK flat steel plant, Alexandria, Egypt
             Level 2 for the whole plant: EAF, LF, continuous casting, HSM
2018 - 2020  PSI Metals, Brussels, Belgium
             Project engineer: Tata Steel and ArcelorMittal digitalisation
2020 - 2024  SMS Group, DIGITAL department, Duesseldorf
             MES and QES: SSAB Sweden, NorthStar BlueScope, Nucor Steel,
             Big River Steel
2024 - 2026  SMS Group, LEVEL 2 department, Duesseldorf
             Sabic Hadeed (Saudi Arabia), JSW Piombino (Italy),
             Suez Steel rail system, Nippon Steel (2024)
```
Age 37. BSc and MSc in Electrical and Computer Engineering.
**Thirteen plants across eight countries.**

### 9.4 The map
`src/pages/worldPaths.ts` holds REAL geometry: the public `world.geo.json`
country outlines (180 countries), projected to an equirectangular 1000x500
viewBox and simplified with Douglas-Peucker at 0.9 tolerance = 272 SVG paths.
Every plant is plotted from its true longitude and latitude.

**It is checked in on purpose.** Karim asked for `react-simple-maps` + a CDN
TopoJSON; both were declined with reasons: the library is an unverified
`npm install` against React 19.2, and a CDN fetch means the map is BLANK if the
presentation room has no internet.

**Two earlier map attempts failed and should not be repeated:** hand-drawn
continent polygons (looked amateur) and three labelled region boxes (readable but
not a map).

### 9.5 The licence section - THE MOST IMPORTANT CORRECTION
**The first calculator was fabricated.** Weights (0.6/page, 3.5/link, 0.02/GB),
thresholds (90, 220) and even the concept of "licence units" were invented. It
LOOKED precise and was not.

**It was rebuilt on the real model** from Chapter 6 sections 6.3.4a and 6.1.9.8,
which Karim pasted into the session. **Every figure now comes from the document.**

**Tier envelopes (6.3.4a):**
| Dimension | Light | Pro | Pro Plus | Enterprise |
|---|---|---|---|---|
| Named users | 5 | 25 | 100 | unlimited |
| Pages | 15 | 100 | 400 | unlimited |
| Jobs | 5 | 25 | 100 | unlimited |
| DB-links | 1 | 3 | 10 | unlimited |
| Retained volume | 250 GB | 1 TB | 5 TB | 20 TB+ |
| Min refresh | 60 min | 3 min | 1 min | 15 s |
| Concurrent sessions | 3 | 15 | 50 | 200+ |
| Objects per link | 25 | 150 | 500 | unlimited |
| Ingest rate | 10 rows/s | 100 rows/s | 1,000 rows/s | above |
| Statistics + SQL | - | yes | yes | yes |
| ML, practice, prediction, value, assistant | - | - | yes | yes |
| SSO, air-gap, HA | - | - | - | yes |

**Tier selection is DERIVED, not chosen (6.3.6 STEP 1):** the LOWEST tier
satisfying EVERY dimension. **One dimension over promotes the tier** - the same
worst-dimension rule the sizing model uses. **There are no weights in this
model.** The deck shows which dimension is currently BINDING rather than
inventing a weight.

**NO PRICE APPEARS ON THE PAGE, and this is a documented rule (6.3.8):**
> "Internal only. Commercial Admin and authorised sales. **Never exposed to a
> plant user or to the public website.** The website links to Contact Sales
> rather than reproducing its logic."

A self-check refuses the write if any currency figure or the word `margin`
appears.

### 9.6 WHAT KARIM SAID IS STILL NOT GOOD ENOUGH
> *"this not deep detailed not advanced not professional not high tech not
> accurate way"*

**He is right, and the gap is precisely identifiable.** The deck shows a tier
name from slider positions. The document contains a real engineering model that
is NOT implemented:

- Five workload drivers `D1..D5` (6.1.9.3): ingest load, query load, compute
  load, scan load, retained bytes - each with an explicit formula.
- Nine resource formulas (6.1.9.4) producing CPU, RAM, storage, network,
  connections from those drivers.
- The class derivation (6.1.9.5) by worst-driver.
- **6.1.9.7's acceptance criterion: "Every figure shows its formula and its
  inputs. No unexplained number."**

**THE NEXT SESSION SHOULD BUILD THE REAL CALCULATOR** if he asks for it: inputs
-> D1..D5 with formulas shown -> resource formulas with their inputs -> class with
the promoting driver named -> hardware. That is what makes an engineer in the room
believe it. It was not attempted because there were hours to the presentation and
a half-finished version would be worse than what exists.

### 9.7 Deck packs in order (all applied unless noted)
```
DECK-01  the /deck route
DECK-02  four tabs, header entry
DECK-03  corrected career, region map, engine in the pipeline
DECK-04  real-map attempt + the in-house expert section  [needed the one-line
         RegionMap -> WorldMap fix at DeckPage.tsx line 158]
DECK-05  five strength sections, each with its own graphic
DECK-06  real world geometry (needs worldPaths.ts in src/pages first)
DECK-07  the real licence model                         [MUST run before 08]
DECK-08  tier bands under every slider                  [FAILS unless 07 applied]
```

### 9.8 The five strong points, in his own words, now each a full section
1. **Generic and configurable** - every widget, page, DB-link and job added or
   changed by his own engineers. Any industry.
2. **One hub for every source** - centralised, read-only toward all of them. The
   drawn problem: every machine and gauge has its OWN screen showing only itself.
3. **An engine, not a BI tool** - the in-house expert who knows the plant
   fingerprint. No need to hire an expert or lose days troubleshooting.
4. **Early cause, late defect** - a small drift at stage 1 passes every check and
   the defect appears at stage 4; correlation runs backwards and names the cause.
5. **Predict, then advise** - this raw material with this speed has ended badly
   before; flag the piece as at risk AND suggest a downstream correction while
   there is still time.
Plus the grounded assistant on top, answering from the plant model AND the
engine's findings.

---

## 10. OPEN ITEMS, RANKED

### 10.1 Needs a ruling
| # | Item |
|---|---|
| 1 | The T-031 four deferred items - when does the closure bundle run? |
| 2 | `tools/ci/validate-real-ui-gates.cjs` is orphaned and the phase56 script still injects a stage into the Jenkinsfile. No backlog task exists. |
| 3 | The 8 pre-existing commercial-validator copy strings - author them or amend the validator? |
| 4 | `tools/packs/` gitignore - intentional? |
| 5 | The mojibake in 7 files including the live `App.tsx` - which task owns it? |

### 10.2 Known defects, unfixed
| # | Item |
|---|---|
| 1 | **T-071 401 on dock mount** - fix is lazy config fetch (7.5) |
| 2 | `GoldenThread.tsx` still says "Meltshop heat" / "Caster slab" |
| 3 | The audit scanner's four self-matches - a one-line path exclusion |
| 4 | The deck licence section is not the real `6.1.9` calculator (9.6) |
| 5 | 3 JourneyRail.certification failures - parallel track, not ours |

### 10.3 Verifications never performed
| # | Item |
|---|---|
| 1 | **T-071's human smoke** - the dock on five pages, conversation persists, collapsed state obscures nothing |
| 2 | Whether the DECK-07/08 pair applied cleanly after the ordering fix |
| 3 | The exact EF migration Karim applied to unblock the API |

---

## 11. THINGS THAT WILL SAVE THE NEXT SESSION TIME

1. **Uploaded files frequently arrive unreadable.** The uploads directory
   returned I/O errors repeatedly this session. **Ask him to PASTE text into the
   chat.** He did this for his biography and for Chapter 6.3, and both were
   immediately usable.
2. **A repository dump may be extracted to `/home/claude/dump`** for grepping.
   Beware: the extractor normalises line endings, so the dump's LF is NOT
   evidence of the real file's convention.
3. **The `<memory_listing>` has files per task.** Read
   `ppiq-pack-authoring.md` BEFORE writing any pack.
4. **He responds extremely well to being told what is wrong before he finds it**,
   and badly to a confident claim that turns out unverified. Every time a defect
   was named first, the session moved faster.
5. **He works in Arabic and English.** Technical detail in English is fine;
   explanations he asks to be simplified should be in Arabic.
6. **Do not offer to "start fresh" on something already built.** Read the code.
   Twice this session the thing he wanted already existed and was unwired
   (`ProductPage.tsx`, the Phase-7 routes).

---

## 12. COMMIT STATE AT HANDOVER

Committed this session:
- T-069 full website architecture
- T-070 route audit spec
- T-071 assistant dock (`4fe116c9`, 8 files)

**Possibly uncommitted - CHECK FIRST:**
- `AssistantDock.css` (the `bottom: 150px; z-index: 2000` fix)
- All DECK-0x work on `DeckPage.tsx`, `worldPaths.ts`, `App.tsx`,
  `new-landing.css`
- The one-line validator tagline fix

`git status --short` also shows parallel-track files:
`SharedAuthoringShell.tsx`, `authoring-shell.css`,
`authoringCentreRegion.test.tsx`, `Show-PpiqT040CentreState.ps1`, and several
`tools/packs/T-040-*` logs. **Those belong to the other worker. Do not stage
them.** He works with exact-file staging only.
