# PlantProcess IQ — Product Roadmap & Execution Bible (master · v6)
## The demo is a dataset, never a layer · one generic product for any industry · two meetings already in motion

> **Status:** Canonical · v6 supersedes v5 · updated 3 Jul 2026.
> **What changed in v6:** (1) the calendar caught up — the 1-Jul procurement meeting HAPPENED and was won (customer engaged, asked for the website to circulate internally); the next meeting is **8 Jul**, a real technical evaluation with a sales + technical engineer, i.e. the **V2 audience arriving six days early**. The plan is re-cut around that date. (2) The **Golden Rule** is now written into the spine (§0.2): no demo code/page/path/mode in the product, ever — and the 3-Jul audit found live violations, now backloged for eradication. (3) The **user journey is fully specified** (§1.2, 14 steps) including the job-cadence governance model Karim locked on 3 Jul. (4) The **3-Jul session ledger** (§0.4) records what was fixed and machine-verified. Task rows live in **Product Backlog v13** (with a new per-task meeting **Target**: done / 8-Jul / 14-Jul / post-deal); this document is the strategy, the sequence, and the gates.

---

# PART 0 · THE PRODUCT, THE DEMO, THE GOLDEN RULE

## 0.1 · One generic product, three live prospects

PlantProcess IQ is ONE generic, industry-agnostic product installable to any industry via configuration only. The pipeline is: connect read-only → stage → unify into one canonical model → analyse & correlate → suggest (AI/ML) → ask (grounded AI). Current prospects — **mineral water, cars, food** — each with different datasets and process parameters. Nothing in the product may be faked, tailored, or hardcoded per customer or per dataset. Two schemas only: `ppiq_meta` (front-end configuration + users/roles) and `ppiq_plant` (the customer's data after staging).

## 0.2 · THE GOLDEN RULE (permanent, non-negotiable)

- The application must **never** contain demo-specific code, a demo page, a demo path, a demo *mode*, presentation-specific code, or dataset/customer-specific code. Any of those is a **defect**.
- The **only** "demo" is the emulated flat-steel data-source fleet — **external** to the app — which exists for exactly three purposes: (1) Karim's self-testing of the real customer workflow; (2) validating the system live in presentations; (3) populated dashboards instead of empty screens.
- Presentations run the **real journey**: green-field app → connect to the emulated sources through DB Configuration → stage → map → load → dashboards → analysis — indistinguishable from a real customer install.
- **3-Jul audit finding (honest):** the frontend currently violates this rule — hardcoded scenario fixtures (`plantProcessDemoScenario`) feeding UI state, a `DemoModeContext` wired into the app shell, a `/demo-lifecycle` route, in-app demo-reset machinery, demo-named files wrapping generic code, a hardcoded "Demo Plant" name, and a CI guard too narrow to catch any of it. Eradication is V1-35…V1-39 + the strengthened gate V1-37; demo-reset moves out of the app into `tools/` against the emulation fleet.

## 0.3 · The versions and the two real meetings

| Version | Aim | Audience | Date | Exit you can demonstrate |
|---|---|---|---|---|
| **V1 · Journey Real & Clean** | The 7-step journey runs live end-to-end; zero dead buttons; no internal codes; no demo machinery *behavior*; honest job states; correlation produces real results | **Sales + technical engineer** (they will click every button and question every value) | **8 Jul 2026** | The full journey walked live on the emulation dataset through the HMI, surviving the engineer's clicks |
| **V2 · Justifiable & Complete** | Every number justified: Value Engine, universal provenance, L4 statistics live, generic per-view projector, job governance, docs with screenshots | CEOs + engineers (evaluation completion) | **14 Jul 2026** | Re-run any job with an added constraint, drill any number to its evidence, honest abstain never a fabricated value |
| **V3 · Generic & Deep-Hardened** | Real production: multi-industry proof, RLS/encryption, accessibility/RTL, scale | (not demonstrated) | after the deal | Unchanged product installed for a 2nd and 3rd industry, only mappings differ |
| **V4 · Enterprise & Reference** | HA/DR, OT edge agent, ~1000-job load test, compliance, pilot → contract → case study | (not demonstrated) | after the deal | Signed contract + published case study |

> **The decisive shift vs v5:** the 8-Jul audience is technical. V1 is no longer "amaze procurement"; it is "**survive the engineer's clicks**". Numbers-fully-justified remains the 14-Jul completion bar; for 8-Jul, the shield on any probing question is the honesty machinery already live — population stated, method named, *suspected contributor not root cause*, and an explicit abstain instead of a fabricated value.

## 0.4 · The 3-Jul session ledger (machine-verified, recorded as DONE rows V1-31…V1-34)

1. **Website content refresh** — aim/features/value-chain/pricing/diagram aligned to the real product; pricing rebuilt to the locked deposit+subscription model; validators + tsc + Vite build green on Karim's machine (11:12).
2. **Header nav styling drift** — `.site-nav` was never styled (only `.nav-links` was); fixed at source with brand-token pill links.
3. **BOM hygiene test** — `deploy\.ppiq-backups` added to the skip-list (backups are not active files; the old rule broke the suite on every backup, at war with backup-before-edit); stray BOM stripped. Targeted test green.
4. **The "Test Journey not implemented" root cause found and fixed** — three competing Administrator surfaces existed; the routed one was a shallow demo-orchestration page while the four functional journey tabs (DB Configuration 1,150 lines · Schema Configuration 955 · Importing Data · Jobs Monitor) were mounted **nowhere**. `/admin` now mounts all four + Connector Truth + an honest read-only License card. PPIQ-T### eyebrows and DEVELOPMENT/DEMO badges are gated behind dev-only flags. Verified on Karim's machine: tsc clean, Vite build clean, vitest **202/202** (13:05).

---

# PART 1 · THE JOURNEY IS THE PRODUCT (and the 8-Jul demo)

## 1.1 · The demo format for 8 Jul

The presentation **is** the real journey, exactly as a customer would live it: start green-field → **(1–2)** register the emulated sources in DB Configuration, test-connect, pick tables/columns → **(3–5)** schedule Stage-1 DB-Link jobs (incremental: first run pays the full table, every cycle after copies only the watermark tail — 15,000 then +29 then +2), watch them in Jobs Monitor, staging fills → **(6)** no-code mapping: business-key linking (MaterialId = PieceId = ItemId), views, filters for unlogical sensor values → **(7–9)** Loading jobs project staging → canonical; only now does data become assignable → **(10–11)** build pages, drag widgets, bind to canonical views, adjust behavior via the script layer (group-by-shift without a new endpoint) → **(12)** configure an inspection job on a defect/downtime/KPI over a duration window; the engine computes ranked suspected contributors with population + method + q-values → **(13)** suggestions materialize into the workflow page → **(14)** the grounded assistant answers questions **from** engine results with resolvable citations, or it doesn't answer.

## 1.2 · Job governance (locked 3 Jul — the 110-jobs-don't-kill-the-server model)

| Job type | Min interval (floor) | Companion guards |
|---|---|---|
| DB Link (Stage-1) | 2 min | incremental watermark tail-copy (already proven) |
| Loading (Stage-2, **one job per mapping view**) | 3 min (must exceed DB Link) | no-op fast path: exit in ms when staging watermarks unmoved |
| Engine | 20 min | per-type max-parallelism |
| AI+ML | 60 min | per-type max-parallelism |
| All types | Run-now button always available | respects per-SOURCE single-scan lock; refuses politely if already running |

Floors are **configuration**, enforced in the backend (endpoint + scheduler) with the UI validating from the same API values. Floors limit frequency, not concurrency — hence per-type max-parallelism, a per-source single-scan lock (a customer's MES never sees two concurrent scans from us), and start jitter so 60 jobs don't align on one tick. A stuck-run reaper (max-runtime per type → Failed("timeout")) is the minimum shipped for 8-Jul (V1-41); the 347 zombie "Running" correlation rows on local are the live evidence. The queue design must scale toward the V4 ~1000-job load test. Full package: V2-30…V2-33; Stage-2 eradication into the generic per-view projector: V2-28 (one open decision inside the row: reprojection policy when a mapping view is *edited* — full vs forward-only).

## 1.3 · The surprise-question posture for a technical audience

Everything from v5 §1.4 stands, with the technical additions: *"Why is this correlation value what it is?"* → method named, population shown, q<0.05 under FDR or it isn't shown. *"What happens if I schedule this job every second?"* → the floor refuses, and here is why (source-load protection). *"Show me the raw staging row behind this canonical fact."* → provenance drill-through. *"Is this AI or arithmetic?"* → deterministic engines compute and rank; the assistant only explains, with citations, and cannot render an uncited number. **The close, every time:** suspected contributor, not guaranteed root cause — read-only, no OT control.

---

# PART 2 · THE HONEST SCHEDULE (3 → 8 Jul, solo)

Backlog v13 arithmetic: **8-Jul bucket = 71.3h across 26 tasks**; available ≈ 40–50h even using the weekend. The bucket therefore has three rings — cut from the outside in, never from Ring 1:

- **Ring 1 · non-negotiable (~41h):** the journey walk live J1→J7 under the new Administrator (V1-17…V1-24, ~15.7h of live verification + fixing what the click-through exposes) · action-matrix update + full click-through (V1-14, 3h) · stuck-run reaper + zombie cleanup (V1-41, 6h) · **correlation run-to-result completes E2E** (V1-42, 8h — the #1 AI-scrutiny item; today results tables are empty despite 358 attempted runs) · demo-machinery eradication A (V1-35, 8h — fabricated fixtures must not be reachable in front of an engineer).
- **Ring 2 · strongly should (~16h):** fault-injection gate on the demo route (V1-15, 6h) · assistant provider verified live or honest framing locked (V1-43, 4h) · plant name from config, duplicate admin removal, launcher fix (V1-38/39/40, 6h).
- **Ring 3 · if time remains (~14.6h):** readiness one-click (V1-13) · dry-run recording (V1-16) · technical deck annex (V1-28) · collateral placeholder fills (V1-25/26/27/29).

If Ring 3 falls, the meeting still works: the recorded 1-Jul deck plus the live journey carry it. If Ring 2 falls, framing discipline covers it. **Ring 1 cannot fall.** V1 leftovers (env hardening V1-01…V1-08, eradication B renames V1-36, gate V1-37, Playwright boot V1-12) complete by 14-Jul alongside V2. Operational discipline for the meeting itself is unchanged from 1-Jul: present from LOCAL, do not touch Caddy or `.env` on demo day, recorded video as insurance.

---

# PART 3 · V2 BY 14 JUL — EVERY NUMBER JUSTIFIED

The four phases stand as in v5 (P3 value & justification · P4 completeness/performance · P5 role/license/AI-boundary · P6 docs+web), now **plus the governance/genericity block**: V2-28 generic per-view projector (24h, reshaped) · V2-29 Generic-Only CI gate · V2-30…33 job governance (28h). V2 remaining = **220.9h**; two weeks solo doesn't hold it, so the same rule as v5 applies with a sharper edge: **never cut the value/justification work or the genericity eradication — cut depth to V3**. Candidates to push if needed: V2-10 visual regression, V2-13 MFA, V2-22 API reference.

# PART 4 · V3 + V4 — unchanged from v5 (production-real, post-deal)

Multi-industry proof (the unchanged product installed for industry #2 and #3 — now concretely: mineral water or food dataset as the second emulation fleet), RLS/encryption, accessibility/RTL, historian GA, one-command install (V3); HA/DR, OT edge agent, ~1000-job load test (the governance queue from V2-31 must already be designed for it), SOC 2 window, pilot → contract → case study (V4). The price ladder (v5 §2.3) is unchanged: €30–40k pilot crediting toward €120k/yr on conversion; €250k unlocked by the reference, not by code.

---

*The six lenses still hold a veto each and the headline is the lowest. The doctrine is the target; the delta is the truth; the customer wins every disagreement. Task rows, hours, and per-task acceptance live in **Product Backlog v13**; topology in **Identity & Topology v4** (pending the e2eadmin/local corrections noted 2-Jul); the destination spec is **Doctrine v8** with the Golden Rule of §0.2 binding over all of it.*
