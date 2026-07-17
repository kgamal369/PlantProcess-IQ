# PlantProcess IQ — Product Roadmap v8
**Issued:** 10-Jul-2026 · **Supersedes:** Roadmap v7 (dead: its M1 date passed 08-Jul; its M2 date is now the demo date) · **Companion:** Backlog v21 (147 tasks; M1 = 13 / 53 h / 10 Critical) · **Anchors:** the THREE PRODUCT RULES and the 15-STEP CANONICAL JOURNEY (Karim, 10-Jul — saved as permanent doctrine; by END OF M2 both hold at 100%)

**What changed the plan (10-Jul static trace of the full repo):**
1. **Architecture A exists.** A generic, throttled, cursor-tracked, scheduled import engine that reads from *remote* customer databases is built, registered in DI, driven by `Workers/Worker.cs`, and mapped at `/admin/workflow-foundation/*` — for all six source families including Oracle. The HMI is wired to Architecture B instead: a same-database SQL copy over local demo schemas. **Journey step 3 likely already exists; M1-01 verifies it before anything is built.**
2. **Rule 1 is violated by the installer.** `scripts/110` + `111` create five demo-named schemas (`src_meltshop_pg`, …) on **every customer database** via the numbered migration path. Deleted whole, with all of Architecture B, in M2.
3. **Journey step 14 — the supervisor — exists nowhere.** Not in code, not in any backlog version. It is M2's flagship and Thursday's **only** "next release" sentence.

---

## M1 — THE JOURNEY, LIVE · demo **Thursday 16-Jul** (2nd customer meeting: CEO + technical engineer)

**Mission:** walk all 15 journey steps on the real product, from an empty plant schema, with data arriving **only** through a DB-link — 14 steps live, step 14 framed deliberately. Nothing broken, nothing hedged except the one sentence that is scripted.

**Budget:** 13 tasks · **53 h** · 6 days (Fri 10 → Wed 15) ≈ 8.8 h/day. Demo Thursday morning.

### The journey on Thursday, step by step

| # | Journey step | Thursday state | Backlog |
|---|---|---|---|
| 1 | DB-link to customer sources | **live** (test-connect, masked creds, live discovery) | exists |
| 2 | DB-link → scheduled/monitored job | **live** (`SYSTEM_DELTA_IMPORT_JOB`, schedule board) | M1-01/-10 |
| 3 | Incremental import → staging | **live** — Architecture A, remote, throttled, cursor-tracked | M1-01/-05 |
| 4 | 1st no-code UI: prepare/filter/link/map | **live** (discovery + subset + mapping) | M1-05 |
| 5 | Prep file → loading job | **live** (`import-jobs/from-mapping`; per-view = M2) | M1-05 |
| 6 | Loaded into plant schema | **live, generic** — the projector, no ladder | **M1-06 (keystone)** |
| 7 | 2nd no-code UI: pages/widgets/KPIs | **live** (exists; re-import changes the number on screen) | M1-12 proves |
| 8–10 | 3rd no-code UI: analysis jobs + result pages | **live with a real ranked finding** (q-value, population) | M1-07/-08 |
| 11–13 | ML+AI tier on the same UI, license-gated | **live** | M1-08/-12 |
| 14 | **The supervisor** | **framed** — scripted verbatim, the differentiation slide | M1-13 |
| 15 | Chatbot answering **from the engine**, cited | **live** — cited answer, honest refusal | M1-03/-09 |

### Sequence (dependency-true; the old bugs-first rule is retired — it scheduled M1-05 before its own blocker)

**Day 0, Fri 10 — buy information (M1-P1, 5 h):**
`M1-01` Architecture-A live proof (the experiment; decides the week) → `M1-04` evidence pack (`git ls-files loadA.sql.gz` — the demo dataset may exist on one laptop only; the reset script never runs `seed/`) → `M1-02` browser check, 9 surfaces (nothing from 09-Jul has been seen; blocks the rehearsal) → `M1-03` `AddAssistant()` (pack pre-built) → `M1-11` readiness poll.

**Sat 11 – Tue 14 — build (M1-P2, 41 h):**
`M1-05` Surface-1 rewired to Architecture A (6 h) → `M1-06` **GENERIC PROJECTOR**: `StagingRecords` + saved mapping → canonical (16 h — protect this block) → `M1-07` parameter-observations pack (4 h, through the pipeline) → `M1-08` ReadinessGate verification (2 h) → `M1-09` chunk producer + `/api/assistant/reindex` (10 h) → `M1-10` Jobs Monitor, four job types (3 h).

**Wed 15 — prove (M1-P3, 7 h):**
`M1-12` dress rehearsal ×2, recorded, ≤25 min, video captured from take 2 → `M1-13` script + deck, supervisor line word-for-word.

**Thu 16 — demo.** Frozen stack. Glitch → cut to video, keep narrating. Close every analysis with: *"suspected contributor, not guaranteed root cause — read-only, no OT control."*

### Exit criteria (hard)
- The 15-step walk completes twice on record, ≤25 min, zero dead ends, starting from an **empty** plant schema, with every canonical row traceable to a DB-link import (Rule 2, demonstrated).
- The 9-surface browser checklist: 9/9 PASS with screenshots.
- One correlation finding rendered with population/method/q-value; the planted CRACK_LONG driver ranks top; the SCRATCH control shows none.
- `/assistant`: one cited answer whose citation opens; one honest refusal. Never a 500, never a fabricated citation.
- The supervisor sentence is in the script verbatim and nothing else in the demo is hedged.
- **Not attempted in M1 (deliberate):** i18n (say "German is in the next release" — costs nothing with an engineer; demo schemas in the product cost the deal), per-view loading jobs, one-assistant-surface refactor, Admin content, e2e realignment. All in M2 with dates.

### Cut-line rule (carry v7's discipline)
If Sunday ends without M1-06 projecting a registered schema into canonical, Thursday demos the import into staging live and the canonical/analysis layers on the Session-A plant, with the honest line *"the loading job you're watching populated this last night."* The demo is never cancelled; its shape adapts. M1-01's outcome on Friday sets which shape is even in question.

---

## M2 — THE RULES AND THE JOURNEY AT 100% · by **Fri 28-Aug**

**Mission (the mandate, saved as doctrine):** the three product rules and all 15 journey steps hold with zero gaps. What the customer saw becomes what any customer in any industry can install, empty, and run.

**Budget:** 82 tasks · **441.5 h** · 7 phases + P0 keystones. At a sustainable 10 h/day ≈ 44 working days → 28-Aug with one buffer week.

### M2-P0 — the keystones (51 h, do first)
1. **`M2-01` THE SUPERVISOR (step 14, 24 h).** Job type `ENGINE_SUPERVISOR`, weekly, reviews the whole dataset and every engine job, re-tunes coefficients within configured bounds — **every adjustment a provenance row** (job, parameter, before, after, justification, evidence handle), dry-run mode, HMI surface, known-answer test (inject drift → supervisor corrects it). Design doc reviewed before code.
2. **`M2-02` DELETE Architecture B (12 h).** `110`, `111`, the Stage-1/Stage-2 SQL functions, `dump_store`, the registries, `TwoStageImportEndpoints`, `ProvisionBaselineAsync`'s ten tables — and with them, five watermark defects retired unfixed. Then the **migration-path generic gate**: build fails if anything under `scripts/` or `seed/` creates a demo-named object. Falsified once before trusted (the CI-truth-gate lesson).
3. **`M2-03` purge demo seeds (3 h).** `011_p4_demo_*`, `040_DEMO_ONLY` → emulation-fixtures location outside the product.
4. **`M2-04` per-view Loading jobs (12 h).** One job per mapping view, own watermark; monolith deleted.

### M2-P1 — assistant chain complete + DB truth (55.5 h)
One assistant surface · auto-refresh after analysis runs + stale purge + the `WebApplicationFactory` resolution test · e2e proof CI-gated (empty-index baseline; cited answer; refusal; blocked claim; viewer/engineer scoping) · evidence-row route · the `113/117` audit-table drop + type drift · plus v20's P1 carryovers (credential doctrine, logging files, Hetzner Caddy permanent fix, e2e full realignment — **land before the next `git push`: stage 5 now executes and blocks**).

### M2-P8 — the M1 carryovers (54.5 h)
i18n EN/DE **after** the UX/charm passes (never before — or every locale key is paid twice) · Administrator content · e2e realignment · action matrix · genealogy triage (865 diagnostics) · DemoAnalyticsPages deletion · `src/phase11` decision · phase-token gate hardening · LogPanel tests · Data-Integration polish · UX walkthrough · grounding eval.

### M2-P2…P7 — carried from v20 (280 h)
License/Ed25519 production keys · RBAC enforcing · remote license control · chatbot provider matrix + no-egress + eval harness · correctness harness (known answers justify every displayed value) · job governance (floors, parallelism, locks, run-now) · the three migration truth-gates — **truth-gate B seeded with the seven duplicate tables found 10-Jul and extended to `CREATE OR REPLACE FUNCTION`** (`ppiq_validate_genealogy_graph` in both `312` and `690`) · naming golden-rule internal sweep · performance at scale · docs with screenshots · MFA · visual regression.

### Exit criteria (hard)
- **Rule 1:** fresh install creates zero demo-named objects; the migration gate proves it and has been seen red once.
- **Rule 2:** on a clean install the plant schema is empty until a DB-link import runs; every canonical row carries import-batch provenance.
- **Rule 3:** all 15 steps executable end-to-end by a non-author following the runbook — including the supervisor's weekend run visibly improving a job's next result.
- A clean `git push` deploys to Hetzner green **through the now-blocking e2e stage**; the correctness harness gates CI; scorecard headline ≥ 70.

---

## M3 — SCALE-READY · by **15-Oct** *(carried from v7/v20, dates shifted by M2's true size)*
Bug-burn to zero known Criticals · **multi-industry proof**: automotive + food/beverage emulated fleets ingest through the *identical* 15-step journey with zero app changes — the on-the-record evidence for Rule 1 · engine/ML hardening at 1M rows, drift detection, 100-question eval with adversarial uncited-number probes · license/roles 100 %, SSO/SCIM, commissioning runbook executed by a non-author.

## M4 — THE CUSTOMER · contract-shaped *(unchanged from v7)*
25 tasks sequenced by the signed customer's sources, KPIs and rollout. First activity on signature: scoping workshop mapping their plant onto the generic journey.

---

## Standing rules carried into v8
- **The three product rules and the 15-step journey are doctrine** (saved 10-Jul). They outrank convenience, schedule, and any demo deadline. End of M2 = 100 %.
- **Honesty over spectacle.** One scripted "next release" sentence (the supervisor). Everything else demonstrated is real, or it is not demonstrated.
- **A guard satisfiable by its own prose or output is not a guard.** Five instances found (Jenkinsfile comment gates, the rollback echo, the mojibake repairer, the harness v1, `AddAssistant`). Every new gate is falsified once — seen red — before it is trusted.
- **Tested is not wired.** Grep for the registration, never for the class. Five instances of that one, too.
- **Headline scoring:** lowest persona, never averaged. Freeze discipline: every milestone ends with a build freeze and an evidence pack.
