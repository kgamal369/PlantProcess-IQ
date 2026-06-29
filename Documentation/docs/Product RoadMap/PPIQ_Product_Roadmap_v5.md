# PlantProcess IQ — Product Roadmap & Execution Bible (master · v5)
## Software → a product you sell · the demo is a dataset, never a layer · four versions, two meetings, one deal

> **Status:** Canonical · v5 supersedes v4 · updated 26 Jun 2026.
> **What changed in v5:** the milestone spine is now **V1 / V2 / V3 / V4** (reconciled with the old M1/M2/M3 — see the map in §0.3); a hard statement of **what the demo is and is not** (§0.2); each version now carries an explicit **aim, audience, and date**; the sequence is reconciled to **Doctrine v8** (16 gates · 4 waves) and to the **26-Jun realization scorecard** (headline A3 = 61). The task rows live in **Product Backlog v7**; this document is the strategy, the sequence, and the gates.

---

# PART 0 · THE PRODUCT, THE DEMO, AND THE FOUR VERSIONS (read first)

## 0.1 · Software → product — the six lenses, the floor is the score

A product is not a bigger codebase. It is a thing a buyer can **evaluate, trust, deploy, and pay for**, defended across six roles that each hold a veto:

| The lens | What they decide | Their veto |
|---|---|---|
| **Developer / Maintainer** | Can this be extended and is it honest? | a red suite · a dead button · a tombstone shim |
| **Security / IT / Procurement** | Can we approve it with no OT or data-egress risk? | a write-back to control · a token in localStorage · an exposed DB port |
| **Process / Quality Engineer** (daily user) | Will it beat my spreadsheets, can I trust it? | a dead button · a fabricated number · a broken genealogy thread |
| **Reliability / Ops / Plant-Admin** | Does it keep running and let me configure without the vendor? | a silent overwrite · an unmonitored job · no runbook |
| **Executive Sponsor** (economic buyer) | Will it pay back, and does each role see the right scope? | value asserted-not-computed · an uncited number · an editable license |
| **Brand / Website** (first impression) | Before login, does the site make a serious buyer request a demo? | a forbidden claim · a thin product page · a dead CTA |

**The product ships when all six sign. The headline score is the lowest of the six — you are selling the floor, not the average.** The whole roadmap is organized around lifting the floor in the order that makes the product safe → valuable → provable → enterprise-ready.

## 0.2 · The demo is NOT the software — it is an emulated customer dataset

This is a permanent rule and it shapes every task below.

- **The software is ONE generic product.** It has no demo layer, no demo page, no demo-only screen — any of those is a defect. It owns a single PostgreSQL database of exactly **two schemas**:
  - **`ppiq_meta`** — metadata: front-end **page / component / dashboard configuration** and **users / roles**.
  - **`ppiq_plant`** — the **customer's data after staging**.
- **The demo is only an emulation of an imaginary customer's *external* data sources** (the eight demo source databases — the "demo dataset"). The standard product **connects** to those emulated sources exactly as it would to a real customer's systems, **stages** their data into `ppiq_plant`, and every dashboard, job, and engine runs **generically** — identical to a real install.
- **The demo dataset exists for exactly two reasons:**
  1. **Self-validation.** Karim tests the real workflow, import, dashboards, jobs, and engines by *simulating* a customer connection — so features are proven against realistic data, not a fixture.
  2. **Sales demonstration.** A customer must never be shown an empty dashboard or a never-run job; the emulated data lets the live demo show real functionality.
- **Therefore "make a demo" means** the real generic application running on emulated source data **through the HMI** — never a hardcoded demo app, page, or screen. *(Doctrine v8 §14 Golden Rule; §0.2 "the demo is the first real release on prepared data, built entirely through the HMI.")*

## 0.3 · The four versions at a glance

| Version | Aim (in one line) | **Audience** | **By** | Exit you can demonstrate |
|---|---|---|---|---|
| **V1 · Demonstrable & Stable** | Stable, healthy, basic features perfect — **amaze them into granting the technical meeting** | the customer's **Purchasing / Procurement** dept | **1 Jul 2026** | A live concept demo on the demo dataset, an impressive website, deck, ROI slide, sales docs — and nothing breaks |
| **V2 · Justifiable & Complete** | All jobs / workflow / AI / engine / analysis / ROI on the demo dataset, **every number justified** | **CEOs + technical engineers** doing a real evaluation | **14 Jul 2026** | Re-run any job with an added constraint, apply any filter, answer "why this value, why not higher/lower" — and the result is correct |
| **V3 · Generic & Deep-Hardened** | Finish for **real production**, not for a meeting | (not demonstrated) — real operation | after the deal | Multi-industry, RLS/encryption, generic mapper, accessibility, scale |
| **V4 · Enterprise & Reference** | The enterprise posture and the revenue arc | (not demonstrated) — operation + procurement | after the deal | HA/DR, OT edge agent, ~1000-job synchronization, compliance, pilot → contract → case study |

> **Reconciliation with the old plan.** v4's **M1** (5-day demo) ≈ **V1**; v4's **M2** (proof + funnel + pilot) ≈ **V2** plus the early business arc; v4's **M3** (reference + viability) ≈ **V4 · P12**. The price ladder (§2.3) is unchanged; it now hangs off V1→V2 (the deal) and V4 (the reference).

> **The two-meeting model.** V1 is a **teaser for Procurement** — it buys you the room with the people who actually evaluate. V2 is the **real evaluation** with engineers and CEOs. Do not over-build V1 for an audience that will not stress-test it, and do not under-build V2 for an audience that will.

---

# PART 1 · HOW A CUSTOMER JUDGES THIS (the buyer-evaluation framework)

## 1.1 · The scoring doctrine (the rules a serious review follows)

Scored **/100** per criterion: **<55 Critical · 55–69 Needs work · 70–84 Solid · 85+ Strong.** Binding rules: **(1)** no score without a **live demonstration** through the HMI on the demo dataset — a claim in a doc or a chat is worth zero; **(2)** **honesty outranks capability** — any forbidden claim or uncited number is Critical; **(3)** the **demo path is sacred** — a gate item is 95–100% or it is not done; **(4)** **read-only and OT-safety are absolute**; **(5)** **evidence is mandatory** (file:line or a reproducible run); **(6)** **induce the fault** — for any failure-handling criterion, trigger the condition and watch the behaviour; **(7)** **score the lowest persona.** *(Full rubric: Aspects of Review v4.)*

## 1.2 · Current state (26-Jun realization scorecard)

Headline = lowest persona = **A3 · 61 (Needs work)**. Side by side: **A2 Security 74** · A5 Exec 67 · A4 Ops 67 · A1 Dev 66 · A6 Brand 63 · **A3 Engineer 61**. Gate-ledger overall **≈ 50–52/100**; projected ceiling **≈ 84/100**.

> **The decisive fact for sequencing:** the **headline does not move on the 26-Jun deploy work** — a green pipeline is infrastructure, not product value. A3 is gated on the **Value Engine + L4 statistics**, which is **V2** work. So **V1 wins the meeting; V2 moves the score.** Plan accordingly.

## 1.3 · The 16-gate ledger

Every criterion reconciles to one of Doctrine v8's 16 gates (G1–G16), each mapped to a wave (A security → B value → C acquisition/demo → D experience/compliance) and one measurable proof. The review closes by scoring the ledger and confirming it agrees with the persona scores. *(The companion scorecard renders the full ledger with current status.)*

## 1.4 · THE SURPRISE-QUESTION GATE — the buyer Q&A the demo must survive

A skeptical buyer asks these **live**; the demo is not ready until each is answered **honestly, on the build**. Tags: **[P]** procurement will likely ask in V1 · **[E]** engineers/CEOs will press in V2.

**General**
- **[P] "Briefly, what is it and why should I buy it?"** → A read-only, evidence-grade layer that connects your fragmented plant data and shows *suspected* drivers of quality/downtime with the population and the math shown — faster than spreadsheets, without touching your control systems.
- **[P] "What's the workflow?"** → Link a source → map it to one canonical schema → build pages/widgets in the HMI → run learning jobs → read dashboards → export. All from the HMI, no code.
- **[E] "Does my configurator need to be a programmer?"** → For the common case, no — a no-code visual mapper + templates; safe-SQL only for the long tail.
- **[E] "Can I add/modify a page, widget, chart, job, or AI binding from my HMI, or only in source?"** → From the HMI; a script layer adjusts widget behaviour, so there is no endpoint-per-widget.
- **[E] "How much data before the AI gives a mature answer?"** → Simple dashboards/KPIs work day one; advanced findings arrive as readiness gates turn ready; a backfill collapses the timeline.

**License**
- **[E] "What tiers exist and what exactly does each grant?"** → Light / Pro / ProPlus / Enterprise, differing by features + user/source caps.
- **[E] "How do *you* control the tier?"** → An Ed25519-**signed token**, not an editable DB row — a customer cannot `UPDATE` their own tier.
- **[E] "One-time or renewed with an expiry?"** → The signed token carries issue/expiry; on expiry → warnings → configurable read-only grace; data is never destroyed.
- **[E] "Does a tier limit users or source connections?"** → Yes — enforced by the signed seat/source caps.

**User / Role / Admin**
- **[E] "What user types and privileges?"** → Owner / Plant-Admin / Data-Engineer / Engineer / Operator / Viewer-Executive, each scoped by role + entitlement.
- **[P] "Do you ship a customer-usable admin inside the install image?"** → No — the automated install provisions only the permanent **`sysadmin`** support account (SOU-only; the customer never sees it); the customer's own admin is created manually at commissioning. Nothing customer-facing is baked into the image.
- **[E] "Where are passwords stored and how?"** → Argon2id-hashed, never plaintext.
- **[E] "Two concurrent sessions on two devices? / If two users edit the same page and one saves, what does the other see?"** → Sessions are token-based with rotation/revocation; a concurrent edit raises an optimistic-concurrency **conflict dialog**, not a silent overwrite.

**AI / ML (the trust objections — expect these in V2)**
- **[E] "Does it work on AI/ML, or simple math?"** → Deterministic engines compute and rank; the assistant only **explains**.
- **[E] "Does the assistant do the analysis?"** → No — engines do the math; the assistant explains with citations and **cannot render an uncited number**.
- **[E] "Do you own the engine, or send my data to GPT/Claude?"** → Engines run in-tenant; the assistant model is self-hosted by default, or a zero-retention private endpoint receiving **only** the question + scoped evidence; a per-tenant no-egress toggle exists.
- **[E] "GPT-3 erred often; how can I depend on your assistant?"** → Because the assistant does not compute or rank — deterministic engines do, and every claim carries a resolvable evidence handle or is not rendered.

**The close, every time:** "Suspected contributor, not guaranteed root cause — read-only, no OT control." That honesty *is* the differentiator versus prescriptive tools.

---

# PART 2 · THE VALUE MODEL & MEASUREMENT

## 2.1 · How value actually moves (keep this in front of you)

| From → To | The lever that does the work | Code's role |
|---|---|---|
| Win the meeting (V1) | **A demo that runs live + stable env + collateral** | Necessary, not the value itself |
| Win the evaluation (V2) | **Every number justified + Value Engine + AI/ML realistic + docs** | The core technical proof |
| First contract | **One reference + viability story (pilot → proven ROI)** | Supports it; the contract is the unlock |
| Scale | **A company** (team, certs, SLA org, references) or a partner | Necessary but far from sufficient |

**Anti-pattern to avoid:** relapsing into feature-building when the bottleneck is the *demo* (V1), the *justification* (V2), or the *reference* (V4).

## 2.2 · The measurement principle (the anti-relapse rule)

Every task has **What · Why · Acceptance gate (binary) · Evidence.** A task with no gate is not on the plan. A gate that is not green is not done — regardless of code written. The price you can state is a function of which gates are green (§6), not of effort.

## 2.3 · The price ladder (pilot now, license as the destination)

| When | What you propose |
|---|---|
| After V1 wins the meeting **and** V2 survives the evaluation | a **€30–40k paid pilot** (lean **€30k** for the first customer) **crediting toward a €120k/yr license** on conversion |
| If the demo cannot yet boot live | a **€15–35k "paid to evaluate"** architecture-and-vision engagement |

**Do not propose €120k as the early ask** — state the €120k license *value* openly (it anchors the correct tier), but ask only for the **pilot**. The pilot's deliverable is **proven ROI on their data** — that is what converts the pilot to the license and turns this customer into your reference. **€60k** is the *demonstrated market value* (what a **second** pilot fetches), not the first invoice. **€250k** is unlocked later by the **reference customer**, not by code.

---

# PART 3 · V1 — DEMONSTRABLE & STABLE  ·  26 Jun → 1 Jul  ·  audience: Procurement

## 3.1 · Aim

1. **Stable and healthy.** Basic features work perfectly; **no crash, no error, no dead button, no failed API/DB connection anywhere on the demo path** — from any cause (user, license, Jenkins, Docker config, DB connection string). You do **not** get embarrassed in front of the customer's Purchasing department.
2. **Amaze them.** Procurement should be **extremely impressed** and genuinely want to buy and test — convinced by the **benefit and return of value**, the **UI/UX experience**, the **website**, the **sales documentation**, and the **presentation**. The single deliverable of V1 is **a next-meeting appointment with the CEOs and the technical engineers who will really evaluate the product.**

## 3.2 · What V1 delivers (the demonstrable surface)

- A **stable environment** that never breaks in a controlled demo: the deploy is green and the demo-path screens degrade safely under the obvious faults a viewer could trigger (a bad login, a role clicking a gated control, a tier-locked feature).
- A **live concept demo on the demo dataset**: connect an emulated source, and walk the **genealogy golden thread** both directions on coil **C-0044170** (defect → melt and back) on the customer's own key names — *the* "it speaks my plant" moment.
- The **website**, an **on-brand pitch deck**, a **ROI slide** (with your real per-plant number), a **founder credibility pack**, a **one-page product brief**, and a **2–3 minute demo video** as insurance.
- A **recorded clean dry-run** so a live glitch never sinks the meeting.

## 3.3 · The exit gates (all green = V1 done)

- The genealogy walk runs **live, both directions**, on the demo dataset, no dead click, no stall.
- A **one-click readiness check** passes; the app **boots clean** under the e2e harness.
- **Zero dead buttons** on the demo path (clicked through and recorded).
- The **recorded dry-run video** exists; the **deck, brief, ROI slide, and offer** exist.
- The **website CTA** delivers a real lead (requires the outbound-mail relay — see the honest note).

## 3.4 · The honest schedule and the lever

V1's **remaining effort is ~73h** (Backlog v7, Version Summary), dominated by the **genealogy foundation** (~17h, barely started) and the **stability hardening** (~22h: persistent Caddy routes, the `.env`/DB-password coupling, provisioning try/catch, the mail relay). **Five calendar days (26 Jun → 1 Jul) does not hold 73h of solo work.** Two honest paths:

- **Hit 1 July (recommended for a Procurement audience):** scope V1 to **amaze + controlled stability** — finish the genealogy walk, the collateral, the dead-button sweep, and the recorded video; keep at least the **persistent-Caddy** fix (highest live-crash risk). **Defer** the full induced-fault matrix and the mail-relay to the **V1→V2 window** — Procurement will not restart your containers or use the email CTA in the room, and engineers (who will) are the V2 audience. **Operational discipline during the demo:** do not restart Caddy or regenerate `.env`; run from the recorded video as backup.
- **Make it bulletproof first:** include all hardening in V1 and accept **~1.5–2 weeks**, slipping the meeting.

Pick the first unless the genealogy foundation is already further along than 5% — in which case 1 July is reachable.

---

# PART 4 · V2 — JUSTIFIABLE & COMPLETE  ·  by 14 Jul  ·  audience: CEOs + engineers

## 4.1 · Aim

1. **Demonstrate everything on the demo dataset** — all jobs, the full workflow, the AI assistant, the engines, the analysis tools, and the **ROI engine** — with real data to show, not empty screens.
2. **Survive a real evaluation.** Expect them to ask **"why is this value this — why not higher, why not lower?"**, **"show me that"**, **"apply this filter"**, **"re-run this job after I add a constraint"** — and the result must be **correct as expected**. **Every value, number, graph, dashboard, and line must be justified; nothing unjustified.**

## 4.2 · What V2 delivers (the four phases — task rows in Backlog v7)

- **P3 · Value engine & justified numbers (the score-mover).** Build the **Value Engine** (bounded euro ranges + an explicit *abstain* path + drill-through to inputs — the €28k–€56k worked case reproduces live); a **universal provenance gate** so every number/graph/dashboard/line carries a resolvable evidence handle or is not rendered; **verify and expose the L4 statistics live** (Spearman/MI/Lasso/VIF/bootstrap under BH-FDR + stratification) and resolve the doctrine-vs-build inconsistency; sharpen AI/ML calibration; the evidence-ranked suggestion engine; the assistant eval harness.
- **P4 · Completeness + performance/speed.** Full suite green twice on a clean machine; standardize all buttons app-wide; the e2e **action-matrix** enumerating every demo control; visual-regression; **performance-at-scale** (≈630 heats / ≈5,600 coils, virtualized, measured); the real lead-capture backend.
- **P5 · Role / license / AI-boundary depth.** MFA step-up for admins; license caps + expiry/grace; the **real production Ed25519 signing key** (replace the demo dev key); full RBAC across six roles; the AI no-egress data-boundary wired into dispatch.
- **P6 · Documentation with screenshots + web.** User / install / admin / security docs **with software screenshots**; API reference; the pricing & license-tiers page; the naming-golden-rule pass.

## 4.3 · The exit gates

- A **euro range reproduces live and drills through** to every input; removing an assumption flips to an honest *abstain*, never a fabricated number.
- **Zero uncited numerics** on the demo path (enforced by a CI gate); clicking any number/graph opens its evidence handle.
- A correlation/inspection job **recomputes live** with method named, q<0.05 under FDR, bootstrap stability shown, stratified, population stated.
- A buyer can **re-run a job with an added constraint / apply a filter** and the result is correct.
- The authorization matrix is green by identity + tier; the license tier toggles live (signed, tamper-evident).
- The docs carry **real software screenshots**.

## 4.4 · The honest schedule

V2's **remaining effort is ~160h** (Backlog v7). Two focused weeks of solo work is **less than that.** The deep, non-meeting-facing items were deliberately moved **down to V3** to protect the V2 aim. If V2 is still over two weeks, **cut more depth to V3** — never the value/justification work, which is the entire point of the evaluation.

---

# PART 5 · V3 + V4 — PRODUCTION-REAL (not demonstrated; required to operate)

These finish the product for **real operation** — the work a customer relies on but **will not test in a meeting.** Sequence them after the deal is in motion.

## 5.1 · V3 · Generic & Deep-Hardened

- **Deep security & ops** (not meeting-facing): RLS on all core tables; encryption at rest; Jenkins on a separate Docker network; the source-load-protection policy wired into the live query path; nightly full-suite CI; a CI health dashboard; EF default-schema → `ppiq_meta`/`ppiq_plant`.
- **Multi-industry & generic:** a second and third industry demo dataset (paper/board, food & beverage); the generic no-code mapping template library; the historian connector to GA; a one-command per-industry customer-install package.
- **Accessibility, i18n & scale:** WCAG 2.1 AA light theme; **Arabic RTL** + locale/units/timezone; i18n as resource files (fix mojibake); performance-at-scale validation + a soak job; the real-domain TLS cutover off sslip.io.

## 5.2 · V4 · Enterprise & Reference

- **Ops / HA + OT:** verified backup/restore drill; deploy rollback verification; a time-series store (hypertables, compression, retention); a **deployable OT-safe edge agent** (one-way push); human-approved advisory write-back (business workflow, never OT); performance budgets in CI; **load-test at reference-plant scale** — the "**~1000 jobs synchronized together**" requirement lands here.
- **Compliance + ops:** SOC 2 Type II observation window (start early — it is a 6–12 month gate); ISO 27001 mapping + sensitive-data catalog; GxP / 21 CFR Part 11 pack; a public status page; the agent-assisted AI triage loop; the certified dry-run; consolidated operator/admin/security docs.
- **Business / reference (the revenue arc):** incorporate (UG/GmbH) + imprint; pilot contract + MNDA + DPA templates; the target list + outreach sequence (your PSI network); **convert the presentation customer into a signed paid pilot → prove ROI on their real data → convert to a signed contract → produce the case study/reference**; a viability arrangement (hire / escrow+BCP / partner); a support SLA offering; the funding/partnership decision.

---

# PART 6 · HOW YOU'LL KNOW YOU HIT EACH VERSION (the gates)

You will not "feel close." You are at a version when its gates are green:

| Version | The gates that define it (all green) |
|---|---|
| **V1** | live genealogy walk recorded · one-click readiness · zero dead buttons on the demo path · deck + brief + ROI slide + offer + demo video · **a next-meeting appointment secured** |
| **V2** | a live drillable euro range · zero uncited numerics · a job re-runs correctly with an added constraint · authz matrix green by identity+tier · docs with screenshots · **survives the engineer/CEO evaluation** |
| **V3** | RLS/encryption on · accessibility (RTL + WCAG) · multi-industry datasets · historian GA · one-command install |
| **V4** | verified restore + HA basics · OT edge agent pushing one-way · ~1000-job load test passes · SOC 2 window open · **a signed contract + a published case study** |

**The price-when:** V1+V2 demonstrated → propose the **€30–40k pilot** (→ €120k license on conversion). One **signed reference** (V4) → **€250k** is credible. The €500k–€1.5M band stays a *company* achievement (HA + external certs + SLA org + multiple references), out of this window.

---

*The product ships when all six lenses can sign; the headline is the lowest. Euros are customer-side willingness-to-pay, not quotes or valuation. Task rows, efforts, and per-task acceptance live in **Product Backlog v7**; topology and identity live in **Identity & Topology v4** and the **Deploy Pipeline Handover**; the destination spec is **Doctrine v8**. Freeze the doctrine; move the delta. The doctrine is the target; the delta is the truth. Where this plan and a live customer disagree, the customer wins.*
