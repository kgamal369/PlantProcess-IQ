# PlantProcess IQ — Product Roadmap & Execution Bible (master · v4)
## From laptop software → a product you sell · €35k → €60k → €120k → €250k + first contract

> **v4 update (26 Jun 2026).** v4 keeps the entire strategy, value model, and milestone structure of v3 unchanged. It applies surgical updates where the **technical substrate moved**: the M1 "environment from scratch" is now realized as a **green end-to-end server deploy pipeline** (build #96) with a working, loginable demo UI, `sysadmin` identity, and Enterprise license active. The PART 2 persona snapshot is date-stamped and the deploy/identity-dependent criteria are nudged where the substrate genuinely improved — **but the headline (A3, 61) is deliberately held**, because it is gated on the value engine + live HMI signal, which is unbuilt. Both the LOCAL development environment and the SERVER release environment are now first-class and documented side by side (see the Identity & Topology v4 reference); neither replaces the other.

> **The shift this document makes.** Until now PPIQ has been judged the way you judge *software*: does it build, do the unit tests pass. From here it is judged the way a customer judges a **product**: through six independent professional lenses — and it only sells when **all six can sign**, with the headline being the **lowest** lens, never an average that hides a failing one. This document merges the full roadmap, the validated execution plan with real environment files, **and** the buyer-evaluation framework (how a customer actually scores you, and the exact objections the demo must survive). Every task carries a **binary acceptance gate** and an **evidence artifact** — because *what is not measured is not achieved*, which is the specific reason the 6-June plan fell short.

> **Honest framing on euros.** These are customer-side willingness-to-pay / deal-size, anchored to your Market-Positioning workbook ("Now" €5k–€40k; "After Phase 5" €40k–€120k) — not quotes or valuation. The value jumps are mostly **not code**: €35k→€60k = *a demo that runs live*; €120k→€250k = *one reference customer*. Code is necessary at every step, never the whole lever.

> **v3.1 adjustment (market benchmark + pilot model).** The €35k→€60k→€120k→€250k figures are *market value*, not the invoice. **You propose a €30–40k paid pilot now (lean €30k for the first customer), crediting toward a €120k/yr license**; €120k is what you convert to after *proven ROI on the customer's data*, and the pilot/contract runs right after the M1 presentation (M2 overlaps it, customer-funded). PPIQ is a **Tier-2 specialist**, read-only — a lower price ceiling than prescriptive peers SST/Fero — so the path to value is **proof + service + a reference, not more features**. (Full analysis: `PPIQ_Market_Benchmark_Revaluation.md`.)

---

# PART 0 · SOFTWARE → PRODUCT (read first)

A product is not a bigger codebase. It is a thing a buyer can **evaluate, trust, deploy, and pay for**, defended across roles that each hold a veto:

| The role | What they decide | Their veto |
|---|---|---|
| **Developer / Maintainer** | Can this be extended and is it honest? | a red suite, a dead button, a tombstone shim |
| **Security / IT / Procurement** | Can we approve it with no OT or data-egress risk? | a write-back to control, a token in localStorage, an exposed DB port |
| **Process / Quality Engineer** (daily user) | Will it beat my spreadsheets and can I trust it? | a dead button, a fabricated number, a broken genealogy thread |
| **Reliability / Ops / Plant-Admin** | Does it keep running and let me configure without the vendor? | a silent overwrite, an unmonitored job, no runbook |
| **Executive Sponsor** (economic buyer) | Will it pay back, and does each role see the right scope? | value asserted-not-computed, an uncited number, an editable license |
| **Brand / Website** (first impression) | Before login, does the site make a serious buyer request a demo? | a forbidden claim, a thin product page, a dead CTA |

**The product ships when all six sign. The headline score is the lowest of the six. You are selling the floor, not the average.** This is the single mental shift from "software" to "product," and the whole roadmap is organized around lifting the floor.

---

# PART 1 · HOW A CUSTOMER ACTUALLY JUDGES THIS (the buyer-evaluation framework)

### 1.1 · The scoring doctrine (the rules a serious buyer's review follows)

- **Bands /100:** Critical `<55` (missing/broken/dishonest) · Needs-work `55–69` (works happy-path, fails an edge/induced fault/skeptical click) · Solid `70–84` (complete, stable, honest for the demo scope) · Strong `85+` (production-grade, automated-tested, documented, robust under adversarial use).
- **Evidence is mandatory.** Any band above Needs-work needs file:line or a reproducible run. "Looks done" is not evidence. **A criterion with no reproducible evidence cannot exceed Needs-work.**
- **Honesty outranks capability.** An honest "collecting data" beats a confident wrong answer. Any forbidden commercial claim is automatically Critical. Any uncited number from the assistant is Critical.
- **The demo path is sacred** — every button on it works, or the path is not demo-ready (the no-"85%" rule: a gate item is 95–100% or it is not done).
- **Read-only & OT-safety are absolute** — any write-back path to a control system is an automatic Critical, regardless of all other strengths.
- **Induce the fault** — failure-handling criteria are tested by *causing* the condition (a 500, a schema change, a bad join, a concurrent edit, a tier toggle), not by reading a claim.
- **Score the lowest persona** — ship only when all six can sign.

### 1.2 · The 16-gate ledger (every capability reconciles to a gate)

G1 Source integration · G2 Mapping & genealogy · G3 Workflow (HMI) · G4 Intelligence · G5 Access & value · G6 UI/UX · G7 Demo · G8 Website · G9 Operations · G10 Quality bar · G11 OT-safe acquisition · G12 Identity & security · G13 Data boundary & model governance · G14 Accessibility & i18n · G15 Coverage & honesty · G16 Compliance. **Baseline ≈46, ceiling ≈84.** Your 15-Jun headline = **61** (A3), gate aggregate low-60s.

### 1.3 · THE SURPRISE-QUESTION GATE — the buyer Q&A the demo must survive

*This is the most important section for selling. A skeptical buyer will ask these live; the demo is not ready until each is answered honestly, on the build. Rehearse every answer.*

**General**
- **"Briefly, what is it and why should I buy it?"** → A read-only, evidence-grade layer that connects your fragmented plant data and shows *suspected* drivers of quality/downtime with the population and the math shown — faster than spreadsheets, without touching your control systems.
- **"What's the workflow?"** → Link a source → map it to one canonical schema → build pages/widgets in the HMI → run learning jobs → read dashboards → export. All from the HMI, no code.
- **"Does my configurator need to be a programmer?"** → For the common case, no — a no-code visual mapper + templates; safe-SQL only for the complex long tail. *(Evidence: tiered authoring; `320_p3_business_key_reconciliation.sql`.)*
- **"Can I add/modify a page, widget, chart, job, or AI binding from my HMI, or only in source?"** → From the HMI; a script layer adjusts widget behaviour so there is no endpoint-per-widget. *(Evidence: `p3T15WidgetSchemaContract.ts`.)*
- **"How much data before the AI gives a mature answer?"** → Simple dashboards/KPIs work day one; advanced findings arrive as readiness gates turn ready; a backfill collapses the timeline. *(Evidence: `AdvancedReadinessGateSurface.cs`.)*

**License**
- **"What tiers exist and what exactly does each grant?"** → Light / Pro / ProPlus / Enterprise, differing by features + user/source caps. *(Evidence: `Phase10LicenseTier`.)*
- **"How do *you* control the tier?"** → An Ed25519-**signed token**, not an editable DB row — a customer cannot `UPDATE` their own tier. *(Evidence: `VerifiedEd25519LicenseService.cs`; `Phase5_LicenseTierTamperTests.cs:8`.)*
- **"One-time or renewed with an expiry?"** → Signed token carries issue/expiry; on expiry → warnings → configurable read-only grace, data never destroyed.
- **"Does a tier limit users or source connections?"** → Yes — enforced by the signed seat/source caps.

**User / Role / Admin**
- **"What user types and privileges?"** → Owner / Plant-Admin / Data-Engineer / Engineer / Operator / Viewer-Executive, each scoped by role + entitlement. *(Evidence: `PlantAccessControl.cs`.)*
- **"How do I add/manage them, and the user limit?"** → Via admin governance; the count is enforced by the signed seat cap.
- **"Where are passwords stored and how?"** → Argon2id-hashed (64 MB), never plaintext. *(Evidence: `AuthStore.cs:150`.)*
- **"Two concurrent sessions on two devices?"** → Supported; sessions are token-based with rotation/revocation.
- **"If two users edit the same page and one saves, what does the other see?"** → An optimistic-concurrency **conflict dialog**, not a silent overwrite. *(Evidence: `PageVersionConflictContractTests.cs`; the ConflictDialog is now wired.)*

**AI / ML (the trust objections)**
- **"Does it work on AI/ML, or simple math?"** → Deterministic engines compute and rank; the assistant only **explains**. *(Evidence: `AdvancedCorrelationComputeService.cs`.)*
- **"Does the assistant do the analysis?"** → No — engines do the math; the assistant explains with citations and **cannot render an uncited number**. *(Evidence: `GroundingService.cs:42`.)*
- **"Do you own the engine, or send my data to GPT/Claude?"** → Engines run in-tenant; the assistant model is self-hosted by default, or a zero-retention private endpoint receiving **only** the question + scoped evidence; a per-tenant no-egress toggle exists. *(Evidence: `PrivateModelGatewayContracts.cs:13`.)*
- **"GPT-3 erred often; how can I depend on your assistant?"** → Because the assistant does not compute or rank — deterministic engines do, and every assistant claim carries a resolvable evidence handle or is not rendered.
- **"Where does it run — not every laptop can host a big model?"** → Defined deployment topologies/sizing; the engines are deterministic statistics, not a giant LLM doing arithmetic.

**The close, every time:** "Suspected contributor, not guaranteed root cause — read-only, no OT control." That honesty *is* the differentiator versus prescriptive tools.

---

# PART 2 · CURRENT STATE (origin ≈ €35k) — the full inventory

Headline = lowest persona = **A3 · 61 (Needs work)**. **A2 Security 72** · A5 Exec 67 · A4 Ops 65 · A1 Dev 63 · A6 Brand 63 · **A3 Engineer 61**. *(This is a static-code + test-suite review; live-HMI-only criteria are capped at what code + automated tests prove — Rule 1.)*

> **26 Jun 2026 substrate update (read with the scores below).** Since this snapshot, the deploy/identity substrate that several A1/A2/A4 criteria depend on went green: a full **server deploy pipeline GREEN end-to-end** (pull → tests → migrate → seed → build → recreate-in-place → health-gate + rollback → presentation smoke), the app-project rename (`plantprocessiq`→`ppiq-app`) that stopped `--remove-orphans` from reaping Jenkins, **`sysadmin` first-run provisioning** (a support-only owner; no customer-named admin in the image), the **dev-seed/test-users removed from the production path**, the dev license key **demo-gated** (`PPIQ_PRESENTATION`), and a **loginable demo UI** with host-derived URLs/CORS. Net effect on the snapshot: **A1 Dev 63 → ~66** (criteria 2, 9, 10 substantiated on the server), **A2 Security 72 → ~74** (criterion 10 bootstrap-admin→sysadmin; criterion 7 dev-seed-absent-in-prod), **A4 Ops 65 → ~67** (criterion 9 runbook-to-login now automatic via the pipeline). **The HEADLINE (A3 61) does NOT move** — it is gated on the value engine + live HMI signal (Wave B), still unbuilt. A green *pipeline* is infrastructure, not product value; the product-value bottleneck is unchanged. Re-score formally with live-HMI evidence when M1 is demonstrated.

### A1 · Developer / Maintainer — 63
| # Criterion | Band | Evidence | Gate that lifts it |
|---|---|---|---|
| 1 Hygiene & clean code | 58 | `button-inventory.csv` 41/207 non-standard | 0 non-standard demo-path buttons + hygiene-check green |
| 2 Stability under change | 56 | CI 540/540 but **83 red on clean machine** *(server: backend ~567 + frontend 202 run green in the pipeline)* | `ppiq.ps1 test` exits 0 twice on a fresh local checkout |
| 3 Repo structure | 60 | 3 compose trees, duplicate `demo.yml`, malformed port | one compose base; grep finds no duplicate |
| 4 Naming & structure | 70 | single tier model (T08) + shims | remove `Lite/Light` shim |
| 5 Tests execute not enumerate | 78 | `CiPipelineTruthGateTests.cs:32–80` | green-twice run seals it |
| 6 Generic any-DB | 74 | `ConnectorBehaviourCertification.cs:14` | all 6 linked live via HMI |
| 7 Extensibility | 72 | `IDataSourceConnector.cs`; `LicenseFeature` | add-a-connector demo recorded |
| 8 Structural consistency | 70 | `P2Close.errorBoundaryDiscipline.test.ts` | `useOptimisticSave` standardized |
| 9 Pipeline & deploy | 72 → ~78 | `Jenkinsfile`; `DeployRedPathProofTests.cs`; **server pipeline GREEN #96, two-project split (`ppiq-app`/`plantprocessiq`), in-place deploy + health-gate + rollback** | break-test→push→deploy skips (recorded) |
| 10 Switchable profiles | 62 | `PpiqTestModeOptions.cs`; **server identity = DB-backed `app_users` + `sysadmin`; local = 5 dev-seed users** | login all 5 roles + force Light→Enterprise, no DB edit |
| 11 No silent failure | 70 | `P2Close.dataFetchBoundaryFaults.test.tsx` | demo-path 0 unhandled rejections (recorded) |
| 12 Endpoint resilience | 62 | `ApplicationProblems.cs` | demo endpoints under fault → typed/loading |

### A2 · Security / IT / Procurement — 72 (your strongest — clears the veto fastest)
| # Criterion | Band | Evidence | Gate that lifts it |
|---|---|---|---|
| 1 Read-only absolute | 78 | `SafeSqlValidator.cs:19–81` + corpus | live source-side write rejected (recorded) |
| 2 OT-safe topology | 72 | `OtSafeEdgeCollectorGateway.cs:6` | deployed collector pushing one-way |
| 3 Source-load protection | 70 | `SourceLoadProtectionPolicy.cs` + `V2Policy_*` | enforced on a live query path |
| 4 Token & session | 80 | `AuthContext.inMemory.contract.test.tsx:22` | live MFA step-up |
| 5 Secrets handling | 74 | `P01P02StartupGuard.cs:57–63` | remove dev-only key string from source |
| 6 Tenant isolation | 74 | `RlsTenantIsolationTests.cs` | cross-tenant 403 shown live |
| 7 Per-endpoint authz | 76 | `T06RouteAuthorizationCoverageTests.cs` | prod-build scan shows 0 dev/proof endpoints |
| 8 AI data boundary | 72 | `PrivateModelGatewayContracts.cs:13` | ZDR private-endpoint deploy proof |
| 9 Audit & encryption | 70 | `096_harden_audit_log_immutability.sql` | immutability green in CI + at-rest on |
| 10 Deploy hardening | 72 | `docker-compose.demo.yml:26` (loopback); **bootstrap admin → permanent `sysadmin`; health endpoints anonymous; deploy health-gated with rollback** | runbook-only login + restore drill |

### A3 · Process / Quality Engineer — 61 (HEADLINE)
| # Criterion | Band | Evidence | Gate that lifts it |
|---|---|---|---|
| 1 Zero dead buttons | 58 | 41 non-standard | every demo-path button works (live+recorded) |
| 2 E2E no crash | 60 | `Phase2LifecycleProofEndpoints.cs:235` | full chain runs live, no stall |
| 3 Uniform styling | 58 | 41 non-standard | visual-regression green |
| 4 Widget customization | 64 | `p3T15WidgetSchemaContract.ts` | live drag-drop + bind-no-endpoint + script |
| 5 Genealogy golden thread | 62 | `Phase3GoldenThreadAndAttributionTests.cs:124` | both directions on C-0044170 live |
| 6 Population stated | 72 | `AdvancedFindingTransparency.cs:12` | population shown on a live finding |
| 7 Correlation/ML honest | 70 | `AdvancedCorrelationComputeService.cs:56–59` | learning job recomputes live |
| 8 Blended provenance | 64 | `GenealogyEdge.cs:21` | transition coil 70/30 live |
| 9 Performance at scale | 60 | `PlantProcess.PerformanceTests.csproj` | ≈630/≈5,600 no lag (measured) |
| 10 Interactivity + heatmap | 64 | `InteractiveCharts.tsx:336` | live filter/sort + heatmap |
| 11 Correct results | 62 | `Phase4DemoCorrectnessAndJobsTests.cs` | live spot-check vs known truth |

### A4 · Reliability / Ops & Plant-Admin — 65
| # Criterion | Band | Evidence | Gate that lifts it |
|---|---|---|---|
| 1 Source onboarding HMI | 64 | `ConnectorAdminEndpoints.cs:53` | live DB-Link, test-before-save |
| 2 Mapping & schema config | 64 | `320_…sql` | live no-code map + KPI-as-view |
| 3 Import jobs & delta | 70 | `Phase2LifecycleProofEndpoints.cs:235` | live re-sync imports only delta |
| 4 Jobs Monitor | 62 | `JobRegistrationService.cs:186` | every job outcome/duration live |
| 5 Schema-drift | 70 | `ConnectorSchemaDriftEndpoints.cs` | live add/rename column → typed event |
| 6 Fail loudly | 74 | `MappingFaultClassifier.cs:8` + rollback test | live bad join → typed error + rollback |
| 7 Readiness meter | 64 | `AdvancedReadinessGateSurface.cs:11` | live "X of Y heats" countdown |
| 8 Backfill & protection | 64 | `SourceLoadProtectionPolicy.cs` | live throttled resumable backfill |
| 9 Operational resilience | 60 | `deploy-canonical.sh`; **clean server stack reaches working login automatically via the green pipeline (sysadmin auto-provisioned); deploy rolls back to `:previous` on health-gate fail** | runbook login + restore drill (restore still open) |
| 10 Concurrency | up | `PageVersionConflictContractTests.cs` + ConflictDialog wired | live two-user race → dialog |

### A5 · Executive Sponsor (economic buyer) — 67
| # Criterion | Band | Evidence | Gate that lifts it |
|---|---|---|---|
| 1 Quantified value (€) | 66 | `ValueEndpoints.cs:12` | bounded € range reproduces live, drillable |
| 2 Role-scoped view/edit | 64 | `PlantAccessControl.cs:64,142` | per-role scope live across roles |
| 3 License tiers live | 72 | `Phase5_LicenseTierTamperTests.cs:8` | live Enterprise→Pro toggle |
| 4 Trustworthy AI | 74 | `GroundingService.cs:42` | live claim→evidence-handle audit |
| 5 Honest boundary | 72 | `AdvancedFindingTransparency.cs` | live confounder naming |
| 6 Speed of insight | 64 | `MlReadinessEndpoints.cs` | day-one dashboard live |
| 7 Price-to-value | 62 | `P15ValueRealizationService.cs:34` | live € figure > stated price |
| 8 Cross-device/browser | 62 | `playwright.golive.config.ts` | 3 browsers + 2 sizes (recorded) |
| 9 Distinctiveness | 70 | `SuggestionEngine.cs:11` | genealogy/value contrast live |
| 10 Trust posture/brand | 70 | `plantProcessBrand.ts` | tied to website gaps |

### A6 · Brand / Website (first impression) — 63
| # Criterion | Band | Evidence | Gate that lifts it |
|---|---|---|---|
| 1 Tagline & voice | 74 | `tagline.ts:5–6` | fix secondary-tagline drift |
| 2 Honesty-lint | 74 | `website-phase10-guard.cjs:17–26` | guard green in CI on deployed copy |
| 3 Palette fidelity | 80 | 12/12 hexes | status-color audit pass |
| 4 Typography | 72 | `plantProcessBrand.ts` | min-size/scale audit |
| 5 Logo system | 64 | `plantprocess-iq-logo.svg` | full variant set |
| 6 Website UX | 64 | `playwright.golive.config.ts` | every-size/browser render |
| **7 All five products** | **52 (Critical)** | only `mes`,`yardWarehouse` | 5 product pages to equal depth |
| 8 In-app brand | 70 | `plantProcessBrand.ts` | white-space-leak audit |
| 9 Reports/PDF brand | up | branded light PDF now used | rendered branded PDF attached |
| 10 CTA & lead capture | 58 | `RequestDemoForm.tsx:6–7` | CTA delivers a real lead |

### 2.1 · Strength points (your real moat — what makes this a *product*, not a demo)
1. **Doctrine-grade analytics actually implemented + tested** — Spearman/MI/Lasso/VIF/bootstrap under BH-FDR + stratification. Most solo products fake this.
2. **Evidence-grade, read-only, no-causal-claim posture** — a strategic differentiator vs SST/Fero (lower risk, faster, no OT control), and a trust asset.
3. **Security depth, every prior finding closed + tested** — clears the procurement veto faster than most startups ever do.
4. **Honesty machinery** — uncited-number block, population/exclusions, abstain, suspected-contributor. *Sellable* — it's what a skeptical engineer trusts.
5. **Self-policing CI truth-gate** — "green deploy on red code" made structurally impossible.
6. **Coherent brand system** — exact palette, anti-drift tagline, industrial voice.
7. **You** — a 2,000-file evidence-grade platform, solo. The roadmap points that capacity at the bottleneck (demo + reference), not more features.

### 2.2 · Weakness points (be blunt)
1. **Non-reproducible environment** — the master weakness; everything fragile flows from it.
2. **Live-unproven** — strong on inspection, unproven in motion (~18 composite-point gap).
3. **Solo vendor / bus-factor** — uncloseable by code; a real buyer objection.
4. **No references** — the single biggest missing sales asset.
5. **Definition-of-done discipline** — "compiles + unit test" treated as done; it isn't.
6. **Breadth over depth on the website** — 5 products half-built instead of 1 sharp.
7. **Time leverage** — manual test/deploy setup steals the hours you need for demo + sales.

---

# PART 3 · THE VALUE MODEL & THE MEASUREMENT SYSTEM

### 3.1 · How value actually moves (keep this in front of you)
| From → To | The lever that does the work | Code's role |
|---|---|---|
| €35k → €60k | **A demo that runs live** + minimal collateral | Necessary, not the value itself |
| €60k → €120k | **Certification + automated testing + whole website + docs + funnel** | Large, but paired with non-code |
| €120k → €250k | **One reference customer + viability story** | Necessary to *support* them; the contract is the unlock |
| €250k → €1M+ | **A company** (team, certs, SLA org, references) or a partner | Necessary but far from sufficient — out of this window |

**Anti-pattern to avoid:** relapsing into feature-building when the bottleneck is *demo* (M1), *proof + funnel* (M2), or *reference + viability* (M3).

### 3.2 · The measurement principle (the anti-6-June rule)
Every task has: **What · Why · Acceptance gate (binary) · Evidence.** A task with no gate is not on the plan. A gate that is not green is not done — regardless of code written. The price you can state is a function of which gates are green (Part 7), not of effort.

---

# PART 4 · MILESTONE 1 — 5 days → first presentation → €60k

**One objective:** drive PPIQ end-to-end, **live**, on a stable environment that does not betray you, with a recorded backup and leave-behind collateral. No new features.

> **✅ M1 TECHNICAL SUBSTRATE — DONE on the server (26 Jun 2026).** The "stable environment that does not betray you" now exists as a reproducible green deploy: `git push` → Jenkins `plantprocessiq-deploy` builds, tests, migrates, seeds, deploys the stack **in place** on the isolated `ppiq-app` project, health-gates with rollback, auto-provisions `sysadmin`, activates the Enterprise license, and serves a **loginable demo UI** at `https://app.178.105.152.180.sslip.io`. This realizes the M1-1…M1-7 "environment from scratch" goal **on the server** (see the M1 task statuses below and the Identity & Topology v4 reference). What remains for the €60k presentation is the *live demo content + collateral* (M1-8…M1-20): the recorded dry-run, the demo-path button polish, the two "wow" moments (C-0044170 genealogy + transition coil), the deck/brief/video, the lead-capture inbox, the pilot offer with your real per-plant price, and the founder-credibility pack. **Both environments remain first-class:** daily development/testing stays on the LOCAL laptop; releases go to the SERVER. Operationally, never delete `/var/lib/ppiq-preserve/.env` on the server (Postgres password coupling — Identity & Topology v4 §2.4).

## 4A · The itemized task list (what · why · gate · evidence)

> **Status legend (26 Jun 2026):** ✅ = done/realized on the server substrate · ◐ = partially done · ☐ = open. The server pipeline closed the environment/identity/license items; the live-demo-content and collateral items remain.

| ID | What | Why it blocks €60k | Acceptance gate (binary) | Evidence | Status |
|---|---|---|---|---|---|
| **M1-1** | Converge to ONE compose base + overlays; delete 2 duplicate trees | sprawl is why envs/deploys are fragile | `grep` finds no duplicate compose; one base | dir listing | ◐ server uses the two-project model (`ppiq-app`/`plantprocessiq`); local convergence still pending |
| **M1-2** | Commit `.env.dev` (local-only, non-secret creds) | creds vanish because they live in your shell | a fresh clone has working creds, no manual setup | the file | ◐ local `.env.dev` exists; server uses persisted `/var/lib/ppiq-preserve/.env` |
| **M1-3** | `000_schemas.sql` — `ppiq_meta` + `ppiq_plant` | your required app-metadata vs customer-data split | `\dn` lists both schemas after migrate | psql output | ◐ schemas exist; EF-in-`public` gap remains |
| **M1-4** | Demo sources compose + fixtures (8 sources, 630 heats/5,600 coils) | the emulated customer DB the demo is built from | `ppiq.ps1 up-sources` → 8 healthy; counts match | docker ps + counts | ◐ green locally; server demo-sources currently disabled |
| **M1-5** | Seed one user per role (config) + dev Ed25519 license per tier | your required user-auth + license-auth | login for all 5 roles; each tier token verifies | screenshots + verify | ✅ server: `sysadmin` provisioned + Enterprise activated (licenseJws); local: 5 roles |
| **M1-6** | `ppiq.ps1` one entrypoint | ends manual setup; reproducible runs | each verb runs from clean checkout, no hand-set vars | terminal log | ◐ local entrypoint; server uses the Jenkins pipeline |
| **M1-7** | Commit test config so `dotnet test` boots host + finds DB | permanent fix for the 83 failures | `ppiq.ps1 test` exits 0 **twice** on clean machine | two green runs | ◐ server pipeline runs tests green; local clean-machine green still to confirm |
| **M1-8** | App boots under Playwright (no early `webServer` exit) | no boot = no live demo = no €60k | `ppiq.ps1 e2e` starts app + runs ≥1 spec | playwright report | ☐ (e2e gated off in the server pipeline) |
| **M1-9** | One-click readiness green + record dry-run video | the pilot acceptance artifact + insurance | readiness passes; MP4 of the walk exists | the recording | ☐ |
| **M1-10** | Fix ONLY demo-path buttons (subset of 41) | a dead button mid-demo is fatal | every 9-step button performs its action | recorded run | ☐ |
| **M1-11** | Verify C-0044170 thread + transition coil live | the two "wow" moments | both directions + 70/30 render live | recorded run | ☐ |
| **M1-12** | Point website CTA at a real inbox | a lead must actually reach you | form submit → email/DB row | received lead | ☐ (also blocked by Spamhaus/mail-relay — Identity v4 §17.6) |
| **M1-13** | Pitch deck (10–12 slides) | frames the live demo | rehearsed once; ≤12 slides; on-brand | the file | ☐ |
| **M1-14** | One-page brief (light-surface PDF) | the leave-behind | renders with brand header/footer | the file | ☐ |
| **M1-15** | Demo video 2–3 min | insurance + follow-up + LinkedIn | MP4 exists; no dead click | the file | ☐ |
| **M1-16** | Pilot offer doc — **€30–40k pilot crediting toward a €120k/yr license**; deliverable = proven ROI on their data (backlog M1-T20) | a buyer must know exactly what they're buying | one page with the pilot price, the credited license figure, the 8-week ROI deliverable + the data-ask | the file | ☐ needs your real per-plant price |
| **M1-17** | Sharpen IQ product page; others "coming soon" | one sharp page beats five thin | IQ complete; others stubbed | the page | ◐ |
| **M1-18** | Company LinkedIn page + founder post w/ video | cheapest down-payment on vendor-viability | page live; post queued | the URLs | ☐ |
| **M1-19** | **Illustrative ROI model for the presentation** (backlog M1-T23) | converts a finding into euros for the exec — what justifies the pilot | a conservative €/yr figure with stated assumptions, tied to a live demo finding, labelled *projected-not-proven* | slide + handout | ☐ needs your real per-plant price |
| **M1-20** | **Founder domain-credibility pack** — your 14 yrs at PSI/SMS/EzzSteel (backlog M1-T24) | the single biggest non-code lever a solo founder has; currently missing | one-pager + a slide naming specific plants/processes, placed before the offer slide | the file | ☐ |

**Why this set:** every item makes the demo *run* (M1-1…M1-11), a lead *reach you* (M1-12), the value *legible in euros* (M1-19), or *you* presentable and trusted (M1-13…M1-18, M1-20). Nothing is a feature — the benchmark confirmed you are already at the Tier-2 feature bar. The 40 non-demo buttons, the 4 products, perf-at-scale → deferred to M2 on purpose.

## 4B · The environment from scratch — ACTUAL FILES (build M1-1 → M1-7 first)

> **The principle that ends the pain:** deterministic, committed, **non-secret** credentials for local + CI; real secrets only on the server. Your creds vanish because they live in your shell — move local-only ones into version control (they only ever point at local containers, so they are not secrets), keep real secrets in a git-ignored `.env` on the VPS. A clean checkout + one command then always boots an identical, seeded, credentialed system.

### Step 1 — directory + delete duplicates (M1-1)
```
deploy/
  compose/
    docker-compose.yml            # base: app DB
    docker-compose.sources.yml    # 8 emulated customer sources
    docker-compose.local.yml      # local overlay (host ports)
    docker-compose.ci.yml         # CI overlay (no host ports)
    docker-compose.server.yml     # server overlay (Caddy, restart)
    .env.dev                      # committed local creds (NOT secret)
    .env.server.example           # template; real .env only on the VPS
  scripts/   ppiq.ps1   gen-dev-license.ps1
  fixtures/  demo/   users/seed-roles.json   license/{dev_public.pem,dev_private.pem,*.token}
```
Delete `Infrastructure/deploy/docker-compose.demo.yml`, `deploy/live/*`, the duplicate `deploy/compose/docker-compose.demo.yml`.
**Done-when:** `grep -rl "docker-compose.demo.yml" .` returns nothing.

### Step 2 — `.env.dev` (M1-2) — committed, local-only
```dotenv
# LOCAL + CI ONLY — only ever point at local containers, so not secrets; committed so the
# environment never "vanishes". Real prod secrets live ONLY in /var/lib/ppiq-preserve/.env (git-ignored).
POSTGRES_USER=ppiq_dev
POSTGRES_PASSWORD=ppiq_dev_local_only
POSTGRES_DB=ppiq_app
POSTGRES_PORT=5432
ConnectionStrings__PlantProcessDb=Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only
PPIQ_TEST_CONNECTION_STRING=Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only
PPIQ_FORCE_EXTERNAL_API_TEST_HOST=1
PlantProcess__Auth__SigningKey=DEV_ONLY_local_signing_key_min_64_chars_padding_padding_padding_padding
PLANTPROCESS_ALLOWED_ORIGINS=http://localhost:5173
# Seeded role users (app hashes with Argon2id on load; bootstrap disabled)
PlantProcess__Auth__Users__0__UserName=admin
PlantProcess__Auth__Users__0__Password=DevAdmin123!
PlantProcess__Auth__Users__0__Role=Admin
PlantProcess__Auth__Users__0__IsBootstrapAdmin=false
PlantProcess__Auth__Users__1__UserName=exec
PlantProcess__Auth__Users__1__Password=DevExec123!
PlantProcess__Auth__Users__1__Role=Executive
PlantProcess__Auth__Users__2__UserName=engineer
PlantProcess__Auth__Users__2__Password=DevEng123!
PlantProcess__Auth__Users__2__Role=Engineer
PlantProcess__Auth__Users__3__UserName=operator
PlantProcess__Auth__Users__3__Password=DevOp123!
PlantProcess__Auth__Users__3__Role=Operator
PlantProcess__Auth__Users__4__UserName=viewer
PlantProcess__Auth__Users__4__Password=DevView123!
PlantProcess__Auth__Users__4__Role=Viewer
PPIQ_LICENSE_PUBLIC_KEY_PATH=deploy/fixtures/license/dev_public.pem
PPIQ_LICENSE_TOKEN_PATH=deploy/fixtures/license/dev_enterprise.token
```
> Safe: `P01P02StartupGuard` accepts a `DEV_ONLY_` key only in Development, rejects it in Production.
**Done-when:** a fresh clone has these with no manual step.

### Step 3 — `docker-compose.yml` base (app DB)
```yaml
name: plantprocessiq
services:
  app-db:
    image: postgres:16
    env_file: [.env.dev]
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - ppiq_app_data:/var/lib/postgresql/data
      - ../../Backend/database/scripts:/docker-entrypoint-initdb.d:ro   # 000_schemas.sql runs first
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 5s
      timeout: 3s
      retries: 20
volumes:
  ppiq_app_data:
```
`docker-compose.local.yml`:
```yaml
services:
  app-db:
    ports: ["127.0.0.1:${POSTGRES_PORT:-5432}:5432"]
```
**Done-when:** `docker compose -f docker-compose.yml -f docker-compose.local.yml up -d app-db` → healthy.

### Step 4 — `000_schemas.sql` two schemas (M1-3)
```sql
CREATE SCHEMA IF NOT EXISTS ppiq_meta;     -- app metadata: dashboards, widgets, jobs, pages, users, roles, license
CREATE SCHEMA IF NOT EXISTS ppiq_plant;    -- customer data AFTER staging transform (RLS per tenant)
COMMENT ON SCHEMA ppiq_meta  IS 'PPIQ configuration / metadata';
COMMENT ON SCHEMA ppiq_plant IS 'Customer plant data after staging transform';
```
**Done-when:** after migrate, `\dn` lists both.

### Step 5 — demo sources + fixtures (M1-4)
`docker-compose.sources.yml` brings up the 8 systems on offset ports (Melt PG 15432, Caster Oracle 11521, HSM Oracle 11522, PKL MSSQL 11433, Downtime MySQL 13306, Parsytec MySQL 13307, Yard + QA Excel as mounts), each seeded from `deploy/fixtures/demo/`.
**Done-when:** `ppiq.ps1 up-sources` → 8 healthy; a count query returns ≈630 heats / ≈5,600 coils.

### Step 6 — dev license (M1-5) — `gen-dev-license.ps1`
```powershell
& {
  $dir = "deploy/fixtures/license"; New-Item -ItemType Directory -Force -Path $dir | Out-Null
  openssl genpkey -algorithm ed25519 -out "$dir/dev_private.pem"
  openssl pkey -in "$dir/dev_private.pem" -pubout -out "$dir/dev_public.pem"
  foreach ($tier in 'light','pro','proplus','enterprise') {
    # Mint dev_$tier.token {tenant,tier,seat_cap,source_cap,env_cap,issue,expiry,feature_flags}
    # via your VerifiedEd25519LicenseService dev-mint path, signed by dev_private.pem.
    Write-Host "Mint dev_$tier.token signed by dev_private.pem"
  }
}
```
> Wire the mint to your existing `VerifiedEd25519LicenseService` so the token schema matches the verifier. Commit `dev_public.pem` + the four `*.token`.
**Done-when:** each tier token verifies; `Phase5_LicenseTierTamperTests` passes; tier toggle works in the running app.

### Step 7 — `ppiq.ps1` one entrypoint (M1-6, M1-7)
```powershell
param([Parameter(Mandatory)][ValidateSet('up','up-sources','migrate','seed','test','e2e','demo','reset','down')]$cmd)
$ErrorActionPreference='Stop'
$root = Split-Path (Split-Path $PSScriptRoot)
$compose = "$root/deploy/compose"
function LoadEnv { Get-Content "$compose/.env.dev" | ForEach-Object {
  if ($_ -match '^\s*([^#=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2], 'Process') } } }
function Dc([string]$a){ docker compose -f "$compose/docker-compose.yml" -f "$compose/docker-compose.local.yml" $a.Split(' ') }
LoadEnv
switch ($cmd) {
  'up'         { Dc 'up -d app-db' }
  'up-sources' { docker compose -f "$compose/docker-compose.sources.yml" up -d }
  'migrate'    { Push-Location "$root/Backend"; dotnet run --project PlantProcess.Api -- --migrate; Pop-Location }
  'seed'       { Push-Location "$root/Backend"; dotnet run --project PlantProcess.Api -- --seed-demo; Pop-Location }
  'test'       { Push-Location "$root/Backend"; dotnet test --nologo; $code=$LASTEXITCODE; Pop-Location; exit $code }
  'e2e'        { Push-Location "$root/Frontend/PlantProcess.Web"; npm run e2e; Pop-Location }
  'demo'       { & $PSCommandPath up; & $PSCommandPath up-sources; & $PSCommandPath migrate; & $PSCommandPath seed;
                 Push-Location "$root/Frontend/PlantProcess.Web"; Start-Process npm 'run dev'; Pop-Location;
                 Push-Location "$root/Backend"; dotnet run --project PlantProcess.Api; Pop-Location }
  'reset'      { Dc 'down -v'; docker compose -f "$compose/docker-compose.sources.yml" down -v; & $PSCommandPath demo }
  'down'       { Dc 'down'; docker compose -f "$compose/docker-compose.sources.yml" down }
}
```
**Done-when (M1-6):** `ppiq.ps1 reset` then `ppiq.ps1 demo` boots the full seeded stack from clean.
**Done-when (M1-7):** `ppiq.ps1 test` exits 0 **twice** — the 83 failures gone because host flag + DB + key + admin all come from `.env.dev`.

**Why this is the right professional pattern:** one canonical compose + overlays + one entrypoint + committed fixtures + committed local-only creds = the industry-standard reproducible-environment pattern. Identical local/CI/server topology (only overlay + `.env` change), zero manual setup, deterministic data + users + license, one-command clean rebuild. "Easy to deploy to server and customer" falls out — the customer gets the `server` overlay + their own `.env`.

## 4C · Presentation & collateral pack (each with a gate)
| Item | Contents | Gate |
|---|---|---|
| Pitch deck (M1-13) | Problem → why BI fails → evidence-grade approach → **LIVE DEMO** → value → honest boundary → read-only/OT-safe → pilot offer + price | rehearsed once; ≤12 slides; on-brand |
| One-page brief (M1-14) | what it is · read-only/evidence-grade differentiator · pilot scope · data-ask | renders as light-surface PDF w/ brand header/footer |
| Demo video (M1-15) | the 9-step script | 2–3 min MP4; no dead click |
| Pilot offer (M1-16) | scope · 6–8 wk · deliverables · € · the data-dump ask | one page; a number + the ask |
| Website (M1-12, M1-17) | sharp IQ page · real CTA · others "coming soon" | CTA delivers a lead; IQ complete |
| LinkedIn (M1-18) | company page + founder post + video | page live; post queued |
| Animation (optional) | 30–60s genealogy / one-way data flow | *skip if it threatens the demo* |

**Docs for M1 (minimum):** the one-page brief + the pilot offer. Full admin/security/API docs are M2 — don't build them now.

## 4D · The price you propose (pilot now, license as the destination)
| Gates green | What you propose |
|---|---|
| Live demo recorded (M1-9…11) + ROI model (M1-T23) + credibility pack (M1-T24) + collateral | **A €30–40k paid pilot** (lean €30k for the first customer) **crediting toward a €120k/yr annual license** on conversion |
| Demo still cannot boot live | **€15k–€35k "paid to evaluate"** |

**Do not propose €120k as the M1 ask — it is the license destination, not the presentation price.** State the €120k license value openly (it anchors you at the correct Tier-2 level), but ask only for the **pilot** now. The pilot's deliverable is *proven ROI on their data* (M2-T29) — that is what converts the pilot to the license and turns this customer into your reference. For the first customer the reference is worth more than the fee, so lean to €30k. (€60k is the *demonstrated market value* / what a **second** pilot fetches — not the first invoice.)

## 4E · The 5-day schedule (each day ends on a gate)
| Day | Tasks | Exit gate |
|---|---|---|
| 1 | M1-1…M1-6 (env) | `ppiq.ps1 reset && ppiq.ps1 demo` boots the seeded stack |
| 2 | M1-7, M1-8 | `ppiq.ps1 test` green twice; `e2e` runs ≥1 spec |
| 3 | M1-9, M1-10, M1-11 | recorded clean dry-run exists; 9-step script no dead click |
| 4 | M1-12, M1-13, M1-14, M1-16, M1-17 | CTA delivers a lead; deck + brief + offer exist |
| 5 | M1-15, M1-18 + rehearse 3× | you can run the demo cold; backup video in hand |

## 4F · Rehearse these surprise questions for THIS presentation
From §1.3 — the buyer will ask. The five you are most likely to get and must nail cold: *"why should I buy it,"* *"do you send my data to GPT/Claude,"* *"is it AI or simple math,"* *"can I change pages/widgets myself,"* *"how do you control the license."* Your honest answers are in §1.3; rehearse them until they're reflex, and end on the read-only / suspected-contributor close.

---

# PART 5 · MILESTONE 2 — mid-July → €120k (every task gated)

> **v3.1 re-weighting.** M2 now overlaps the **contract process** — it is not a clean phase after a finished M1. Its spine is **converting the presentation customer into a signed paid pilot (M2-T28)** and **proving ROI on their real data (M2-T29)** — that proven ROI is what unlocks the €120k *license*. The hardening below happens *around* that engagement, customer-funded. Two re-priorities from the benchmark: **pull the human-approved advisory write-back forward** from M3 into this window (it is the read-only *ceiling-raiser*), and **de-prioritize building QES + Energy product pages to equal depth** (brand polish, not willingness-to-pay — keep IQ sharp).

Goal: the demo **rock-solid, certified, auto-tested for days, documented, with an outreach funnel.**

### Track A · Codebase
| Task | Why | Gate |
|---|---|---|
| Full suite green twice on clean machine | stability proof | `test && e2e` exit 0 twice |
| Build 3 missing products (IQ, QES, Energy) to MES/Yard depth | closes the one Critical | each has description+benefit+graphics+licensing; honesty-lint green |
| Real lead-capture backend | the measurable exit fires | form → DB row + email; e2e asserts |
| e2e action-matrix + visual-regression on demo path | no-dead-button + uniform styling proven | matrix enumerates every demo control; visual-regression green |
| Wire 902/904 policies into real paths; perf-at-scale; clear buttons; one compose | enforced + finished | over-budget query throttled live; tables virtualize; 0 non-standard buttons |

### Track B · Other technical (non-code)
| Task | Why | Gate |
|---|---|---|
| **Automated AI test pipeline** (nightly suite + soak + AI-triage PRs + dashboard) | tested for days unattended | nightly runs full suite+e2e; 24–72h soak passes; dashboard trend |
| Recorded *certified* dry-run | the pilot acceptance artifact | one-click readiness green on video |
| Docs suite (Admin/Operator, Security whitepaper, Deploy runbook, API ref) | procurement/IT say yes | each exists + reviewed once |
| Demo dataset polish + scripted reset | repeatable demos | any prospect sees the same clean story via one command |

### Track C · Administration & business
| Task | Why | Gate |
|---|---|---|
| Incorporate (UG/GmbH) + company page + imprint | first viability brick; lets you invoice | entity registered; page live |
| Pricing & license tiers documented | answers buyer's license questions | published tiers page (grants/renewal/caps) |
| Pilot contract + MNDA + DPA templates | you can sign when M1 lands | lawyer-reviewed templates exist |
| Outreach pipeline (8–12 plants via your PSI network) | top of funnel for M3's reference | named list + sent sequence; ≥3 conversations opened |
| Secure one design partner | fastest path to a reference | one plant agrees to a reduced/free pilot with data |

### 5.1 · The automated AI test pipeline (your explicit ask)
1. **Nightly full-suite job** (Jenkins cron): `up → migrate → seed → test → e2e`, blocking, ephemeral CI stack, pass/fail trend.
2. **Soak job** (rolling 24–72h): hold the stack up under a synthetic load driver on the demo path; assert no memory growth, no unhandled rejections, no crashed jobs — your "tested for days" evidence.
3. **AI triage loop**: an agent reads the latest CI failure + failing test + surrounding code and **drafts a fix as a PR with the diff + explanation, for your review** — never auto-merged; flags + quarantines flaky tests.
4. **Health dashboard**: suite + e2e pass-rate, soak result, open-flaky count.
> Honest scope: **agent-assisted maintenance + a soak/regression harness**, not autonomous self-healing. Real value (you stop being the only thing between a regression and a broken demo); honest framing (a human approves every fix).

**€120k is defensible when:** demo certified + auto-tested + documented + website whole + a real funnel. Still **not referenced** → pilot band, not enterprise. Don't claim "production-grade reliability."

---

# PART 6 · MILESTONE 3 — 1 Jan 2027 → €250k + first contract (every task gated)

> **v3.1 timing.** Under the pilot-then-license model the **first contract closes far earlier** — ~2–3 months after the M1 presentation, not at 1 Jan 2027. So 1 Jan 2027 / €250k marks "**referenceable + a viability story in place = ready to command €250k**" (one reference live, expansion or a second deal in motion), *not* the first signature. The pilot → proven-ROI → contract → case-study arc has moved up into the M1→M2 window; M3 is where it compounds into a price you can charge.

**Honest headline: €250k is unlocked by the reference customer, not by code.** Build toward being *supportable and viable enough* to sign.

### Track A · Codebase
| Task | Why | Gate |
|---|---|---|
| HA basics: backup/restore drill + rollback to `:previous` | survives "what if it breaks" | restore drill performed + recorded; rollback tested |
| One historian/edge connector to GA + deployable edge agent | breadth toward incumbents; OT-safe tangible | connector passes behavioural tests live; agent pushing one-way |
| Human-approved advisory write-back (business workflow, never OT) + perf budgets at real scale | the value lever, kept safe | write-back requires approval + audit; load test at reference scale passes |

### Track B · Other technical (non-code)
| Task | Why | Gate |
|---|---|---|
| **Start SOC 2 Type II NOW** | 6–12 month window; start late = gated a year | observation window opened with auditor/tool |
| ISO 27001 mapping + sensitive-data catalog | procurement readiness | mapping + catalog exist |
| Full operator/admin/security docs + status page + runbooks | customer runs it; you support it | docs published; status page live |
| One-command customer-install package | deploy becomes a script | clean machine reaches a working install via one command |

### Track C · Administration & business (the unlock)
| Task | Why | Gate |
|---|---|---|
| **Land first paid pilot → signed contract** | THE €250k unlock | a signed contract exists |
| Case study / reference (measured before/after) | the next buyer needs to see it | a quotable, numbers-backed case study exists |
| Viability: hire/co-founder OR escrow+BCP OR OEM/partner | lowers the bus-factor objection | one is in place + documented |
| Support SLA offering (even small) | lets you charge a contract price | published SLA tiers |
| Decide funding/partnership path | the €250k→€1M trajectory | a written decision + next step |

**€250k with one signed reference is credible.** The **€500k–€1.5M** band stays a *company* achievement (HA + external certs + SLA org + multiple references) — reachable later with a team or partner, out of this window.

---

# PART 7 · HOW YOU'LL KNOW YOU HIT EACH NUMBER (the value gates)

You will not "feel close." You are at a number when its gates are green:

| Target | The gates that define it (all green) |
|---|---|
| **€60k (M1)** | M1-8…M1-11 (recorded live demo) · M1-13…M1-16 (deck/brief/video/offer) · M1-12 (real CTA) · M1-18 (presence) |
| **€120k (M2)** | Track A green twice · automated pipeline + a soak passed · docs suite · 5 products · ≥3 conversations · 1 design partner |
| **€250k (M3)** | a **signed contract** · a published case study · SOC 2 window open · a viability arrangement |

**The anti-6-June discipline:** a task with no gate is not on the plan; a gate that is not green is not done — regardless of code written. Track the gates on the companion HTML (`PPIQ_Product_Roadmap_v3.html`); flip each to green only when its evidence exists, and the targets stop being aspirations and become a checklist.

---

*Euros are customer-side willingness-to-pay anchored to your Market-Positioning workbook, not quotes or valuation. Evidence references are from the 15-Jun-2026 export and this session. The plan is deliberately conservative on commercial dimensions (viability, references, support, certs) because they are earned, not built — inflating them is the 6-June mistake. The product ships when all six buyer lenses can sign; the headline is the lowest. Where this plan and a live customer disagree, the customer wins.*
