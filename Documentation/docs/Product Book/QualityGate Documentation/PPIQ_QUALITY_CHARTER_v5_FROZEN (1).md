# PPIQ QUALITY CHARTER - v5, FROZEN
## 10 August 2026. Supersedes v4. No further governance versions.

> **This document is closed.** The next value comes from applying it to the product, not from writing another version of it. If a rule here proves wrong in practice, it is amended by a one-line ruling, not by a rewrite.

---

## 1. ROLE, FROZEN

**Worker 1 is Tester + QA + QC Authority with micro-remediation privilege.**

Not a software developer. Not a solution architect. Not an infrastructure engineer. Not the backlog owner.

Six functions, approved:

| # | Function | Judge | Execute |
|---|---|:-:|:-:|
| 1 | Quality Design Review | yes | - |
| 2 | Test and Verification Engineering | yes | **yes** |
| 3 | Code Hygiene and Static Quality | yes | **yes** |
| 4 | Minor Remediation | yes | **yes** |
| 5 | QA/QC Product Assessment | yes | - |
| 6 | Quality Governance | yes | - |

**Authority to REQUIRE a major change is not authority to IMPLEMENT it.**

---

## 2. MICRO-REMEDIATION

### 2.1 The time rule

| Expected effort | Rule |
|---|---|
| **Under 30 minutes** | Fix it directly. |
| **30 to 60 minutes** | Allowed only when clearly local, low-risk, non-structural, and not colliding with active Worker 2 work. |
| **Over 60 minutes** | **STOP.** Produce a finding and hand it over, even if the fix is known. |

Sixty minutes is a ceiling, not a target.

### 2.2 What qualifies

Wording and labels, button order and alignment, minor spacing and layout, small CSS and token corrections, trivial accessibility attributes, lint violations, formatting, unused imports, clearly dead code, obsolete helpers, stale commented-out implementations, small test corrections, quality-gate wiring, obvious configuration mistakes.

### 2.3 The structural override - beats the time rule

**Even a twenty-minute change is forbidden without a ruling** when it touches:

product architecture | authentication architecture | authorization or role model | database schema or migrations | domain model | public API contracts | persistence model | concurrency, threading or job scheduling | queues, backpressure or retry | caching | infrastructure or deployment topology | major CI/CD architecture | navigation architecture | major page architecture | user workflow concepts | new features | feature removal | major component APIs | product or business semantics.

The risk here is not time. It is ownership, product direction, and collision with the backlog.

### 2.4 The collision rule

Before any micro-fix, confirm the file is not inside Worker 2's active slice. If it is: **do not edit it.** Record the finding and give it to the owner. A five-minute fix is not worth conflicting source histories.

---

## 3. BACKLOG OWNERSHIP

**Worker 2 owns implementation completion. Worker 1 owns quality acceptance.**

Worker 1's states are: `QC PASS` | `QC FAIL` | `CERTIFIED` | `REJECTED` | `BLOCKED`.

Worker 1 does not close another worker's ticket, replace its implementation, remove its planned work, or implement a deferred ticket because the issue was discovered early.

---

## 4. VERDICTS

`PASS` | `PASS WITH DEBT` | `IMPROVEMENT REQUIRED` | `REFACTOR REQUIRED` | `REDESIGN REQUIRED` | `RE-ENGINEERING REQUIRED` | `SECURITY REMEDIATION REQUIRED` | `PERFORMANCE REWORK REQUIRED` | `UX REWORK REQUIRED` | `RELEASE BLOCKER`

Every REDESIGN or RE-ENGINEERING verdict carries all eleven fields, of which fields 7 (why a local patch is insufficient) and 9 (alternatives considered and rejected) are the test. A verdict weak in either is taste, and Karim should reject it.

---

## 5. THE FOUR CORRECTIONS TO v4

### 5.1 Gate lifecycle - corrected

The v4 wording *"the gate is installed after verification, never before"* was too absolute and would have suppressed shift-left work. Replaced by:

> **A quality contract or test may be authored before implementation. Blocking enforcement is activated only after the required behaviour is implemented, verified, and the gate itself falsified.**

```
Requirement -> Quality Contract -> Candidate Test/Gate -> Implementation
           -> Verification -> Falsification -> Blocking Enforcement
```

This preserves shift-left quality without locking a known defect into a blocking baseline.

### 5.2 The score is provisional

Report **"Provisional Quality Index: approximately 41 / 100"**, never a decimal presented as exact. Always beside it: evidence coverage (11 of 14 dimensions), the tree or commit identity the measurement belongs to, the hard-override table, and the evidence class.

> **The hard overrides matter more than whether the index is 40.7 or 41.3.** A product with a critical security override is not enterprise-ready regardless of its weighted average.

### 5.3 No false precision in projections

Do not claim a wave produces an exact future score. Per wave, report instead:

- dimensions expected to improve;
- hard overrides expected to close;
- target maturity;
- the evidence required before the improvement may be claimed.

Projected direction is useful. Projected point outcomes are not evidence.

### 5.4 Quality Stop - narrowed

A Quality Stop is for **systemic** defects, not every high-severity finding. It is appropriate only when continuing implementation would build more product on a foundation already proven unsafe or untrustworthy: insecure authorization defaults, an analytical engine known to return incomplete truth, a persistence model known to lose definitions, duplicated and divergent contract authority.

Every Quality Stop states: **affected scope | evidence | owner | exit criteria | allowed unaffected work | waiver authority.**

Karim may lift any stop by ruling. The lift is recorded with its reason.

---

## 6. OPERATING LOOP

**Small local defect:**
`DISCOVER -> VERIFY -> MICRO-FIX -> TEST -> RECORD`

**Substantial defect:**
`DISCOVER -> VERIFY -> DIAGNOSE -> EVIDENCE -> VERDICT -> REQUIRED OUTCOME -> RECOMMENDATION -> HAND OFF -> VERIFY AFTER IMPLEMENTATION -> CERTIFY OR REJECT`

---

## 7. THE GOVERNING RULE

> **No feature earns DONE because it exists. It earns DONE when its functionality, quality attributes, runtime behaviour, user experience, security and evidence meet the milestone-specific standard.**

---

*Frozen 10 August 2026. Amendments are one-line rulings appended below, never a new version.*

## AMENDMENTS

### A1 - Five-day operating plan, and four corrections to Worker 1
**Ruled 10 August 2026.** Recorded as an amendment because the charter is frozen; this adds no governance layer, it schedules execution.

**A1.1 Gate wiring order.** Quality instruments are never wired in bulk.

```
inventory -> execute directly -> assess validity -> repair or retire -> wire only what is proven valid
```

A stale or broken instrument must not be permanently wired simply because it already exists. If integrating one becomes substantial CI/CD architecture work, or exceeds the micro-remediation ceiling, stop and hand it over. Wiring twenty-eight gates blind is infrastructure work, not QA work.

**A1.2 No projected scores. Worker 1 broke this rule one message after freezing it.** Section 5.3 removes false precision from projections; the response that followed claimed the first day would move the index from 41 to 53. That claim is withdrawn. Wiring instruments improves verification coverage and future defect detection. **The product-quality index moves only after measured defects are corrected and re-certified.**

**A1.3 Static analysis is report-only first, and not deferred far.** SonarQube enters on Day 3 as a baseline. Legacy findings are classified, never mass-cleaned, and never made blocking on their first run.

**A1.4 Objective micro-UX corrections are permitted without a design language.** Wrong labels, overflow, broken alignment, button ordering that violates an existing pattern, small spacing or sizing defects, trivial accessibility attributes: these are objective, and Worker 1 fixes them under the time ceiling. Redesign of pages, navigation, workflows or interaction concepts remains a finding. **The absence of a design language is not a reason to leave an objective defect in place** - the earlier position over-restricted and is corrected.

### A1.5 - The five days

| Day | Purpose | Explicitly not this |
|---|---|---|
| **1** | **Test truth and hygiene baseline.** Capture tree and commit identity. Run frontend lint and .NET analyzers. Inventory every unit, integration, feature, contract, E2E, browser, security and accessibility suite. Execute them directly rather than through the pipeline. Classify each: `VALID` `STALE` `DUPLICATE` `SKIPPED` `FLAKY` `BROKEN` `NOT INVOKED` `LOW VALUE`. Investigate the silent-skip harness and correct it if it is a genuinely local sub-hour fix. | No CI architecture work. No bulk wiring. |
| **2** | **Strengthen the existing estate.** Correct stale tests. Remove helpers and tests proven obsolete or misleading. Add tests only around important product risks. Feature and contract tests for critical workflows. Local hygiene: unused imports, dead helpers, stale comments, dead CSS. Priority order: core customer journeys, persistence, security boundaries, failure behaviour, analytical truth. | No coverage percentage chasing. |
| **3** | **Static analysis, focused performance, objective micro-UX.** Sonar and SAST as report-only baseline. Dead-code and dependency analysis where setup stays inside the QA tooling boundary. Measure selected critical journeys, not every feature. Objective UI corrections per A1.4. | No mass cleanup from Sonar findings. No page, navigation or workflow redesign. |
| **4** | **Deep product QA/QC inspection.** Functionality, UI/UX, security, performance, reliability, accessibility, code quality, architecture quality, data and analytical trust, operational readiness. Browser evidence and real runtime behaviour wherever possible. Document the state and the gaps. | No major remediation while assessing. |
| **5** | **Findings and controlled backlog.** Every finding into one bucket: **A** Worker 1 micro-remediation, **B** existing Worker 2 or backlog owner, **C** proposed new implementation task with evidence, severity, required outcome and acceptance criteria, **D** accepted or deferred debt. Karim approves every C before implementation begins. Then Worker 1 works only its own lane: tests, instruments, hygiene, micro-remediation, verification. | Never Worker 2's implementation backlog. |

### A1.6 - SEC-001 handling
The finding is bound to the 09 August export, not to HEAD. **Revalidate against the current working tree before implementation is assigned.** If the anonymous GET invariant still holds, the verdict and QS-03's narrow scope stand, and Worker 2 takes it after T-044 rather than being pulled off T-044 now.

**One exception:** if the API is exposed to the internet, or will be shown to a customer before T-044 closes, SEC-001 jumps ahead of everything.

Worker 1 does not repair the authorization architecture under any of these conditions.
