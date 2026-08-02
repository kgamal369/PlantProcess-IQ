# AMENDMENT 1 TO CONCEPT, SCOREBOARD AND DELIVERY ROADMAP v2.0

**2 August 2026**
**Amends:** `PPIQ_Concept_Scoreboard_Roadmap_v2_02Aug2026.md`
**Companion:** `PPIQ_Backlog_v2_FreezeCandidate_02Aug2026`

This amendment closes review corrections 1 and 5 and the wording correction. Where it disagrees with Roadmap v2.0, this document wins.

---

## A1. THE M2a EXIT CRITERION WAS SELF-CONTRADICTORY

**The defect.** Roadmap v2.0 section 3.3.1 set the M2a exit as the customer running **J1 to J15** on canonical data with no presentation shortcut. But Chapter 5 tutorial T8 covers J13 and J14, and it includes **acting on a prediction**. Prediction, drivers, comparables, the nine-check remediation gate and the Accept / Reject / Defer loop are all M2b work by the same document's own scope table. The Definition of Done was therefore unreachable inside M2a's 400 hours, and any team working to it would have concluded in month two that it had failed when in fact the criterion was wrong.

**This is a real error and it is corrected, not softened.** The wrong response would be to pull the 230 hours forward: that makes M2a 630 hours and pushes the on-site date out by more than half.

### The corrected exit criteria

| Milestone | Exit criterion |
|---|---|
| **M1 - 410 h** | From a clean laptop boot, with no database console, the six beats run end to end and two consecutive rehearsals complete with no surprise. |
| **M2a - 400 h** | The customer installs PPIQ on their own infrastructure, connects their own sources read-only, and runs **J1 to J12 plus every J13 to J15 surface** on the canonical database with no presentation shortcut and no demonstration-only code path. **The prediction and remediation steps of J13 and J14 are present as surfaces and legitimately report readiness-blocked or no-history-yet.** The Continuity comparison against the M1 snapshots shows no visible-contract change. |
| **M2b - 230 h** | **Full functional J1 to J15 acceptance, including acting on a prediction.** A prediction is produced from real history, carries drivers and comparables, generates a remediation candidate that passes or fails a named check, is accepted or rejected by a human, and its outcome is captured and evaluated. |
| **M3 - 204 h reserved** | The product installs at a second customer without anyone remembering how a laptop was configured. Chapter 6 is frozen because C1 to C4 have replaced the reference assumptions. |

**What this means in the room during the soft test.** When the customer reaches the Early Warning surface in week one, the honest and designed outcome is a readiness gate reporting a measured value beside its threshold - for example, outcome events 12 where Ready requires 40 or more. That is the product working, not the product missing. It is the same behaviour the demonstration rehearses in M1, which is why the surfaces must be final in M1 even though the engines arrive in M2b.

---

## A2. WORDING CORRECTION ON THE M2b JUSTIFICATION

Roadmap v2.0 argued the split by saying practice learning and prediction *cannot produce anything real in the first weeks of a pilot*. That is too absolute. A high-throughput plant can accumulate 60 independent units and 40 outcome events quickly, and the gate thresholds are counts, not calendar time.

**The corrected statement, and the one used from here on:**

> Practice learning and prediction **must not be relied upon to become statistically ready during the initial soft-test window.**

The decision is unchanged. The reasoning is now accurate: this is a planning risk about *when* the gate opens, not a claim that it cannot open.

---

## A3. PROGRAMME TOTALS AFTER THE OPTION C DECISION

Roadmap v2.0 and the management dashboard still spoke of *the next 800 hours*. That was the pre-decision figure and it is now stale.

| Milestone | Hours | Note |
|---|---:|---|
| M1 Customer presentation | **410** | 400 plus the demonstration script and the environment preparation, both missing from backlog v1 |
| M2a Deployable core | **400** | Ends with the on-site installation |
| M2b Intelligence completion | **230** | Governed update during the soft test |
| **Programme before M3** | **1,040** | |
| M3 Site, certification, commercial | **204 reserved** | Half is written by the customer during the pilot |
| **Total planned** | **1,244** | 138 tasks, none exceeding 12 hours |

**On M1 being 410 rather than 400.** Backlog v1 was missing the screen-by-screen demonstration script and the presentation environment preparation. Ten hours. I have not shaved those ten hours off other estimates to preserve a round number, because a plan whose phases sum to exactly the budget is a plan that was fitted rather than derived - which is the criticism I made of another review before producing an exactly-400 M1 and an exactly-400 M2a myself. If 400 is a hard envelope, close it visibly: move `ppiq_acceptance_empty` to M2a-P1 and drop the optional model-shim task, which is 7 hours and lands at 403.

---

## A4. SCORING BY AREA - FIVE COLUMNS

Roadmap v2.0 reported a single *After M2* column, which was ambiguous once M2 split. Several intelligence scores are only reachable after M2b and were being credited to M2a.

| Area | Today | After M1 | After M2a | After M2b | After M3 |
|---|---:|---:|---:|---:|---:|
| Platform and backend architecture | 62 | 68 | 85 | 86 | 90 |
| Connect and import (DF1-DF3) | 66 | 78 | 88 | 88 | 92 |
| Model the plant (DF4-DF6) | 45 | 64 | 86 | 88 | 92 |
| BI workspace and authoring (DF7) | 64 | **91** | 93 | 93 | 95 |
| Engine, statistics, readiness (DF8-DF9) | 58 | 82 | 86 | 90 | 93 |
| **AI, ML and intelligence (DF10-DF14)** | **18** | 46 | **52** | **78** | 88 |
| Assistant (DF15) | 50 | **86** | 87 | 90 | 93 |
| Administration, licence, security | 32 | 40 | **80** | 82 | 92 |
| Infrastructure, CI/CD, testing | 30 | 46 | **72** | 78 | 90 |
| Website and commercial | 70 | 86 | 86 | 88 | 94 |
| Dataset, demo and reproducibility | 55 | **90** | 86 | 86 | 88 |
| **Weighted product conformance** | **31** | **41** | **68** | **80** | **92** |
| **Six-beat presentation readiness** | **62** | **93** | - | - | - |

The row that the old single column hid is AI, ML and intelligence: it moves from 46 to only 52 across all of M2a, then jumps to 78 in M2b. Crediting that jump to M2a would have made the deployable core look better than it is and M2b look optional.

Dataset and reproducibility falls slightly from 90 to 86 after M2a. That is correct and not a regression: the synthetic presentation dataset is replaced by the customer's real data, which is messier by definition.

---

## A5. SCORING BY PERSONA - FIVE COLUMNS

| Persona | Today | After M1 | After M2a | After M2b | After M3 |
|---|---:|---:|---:|---:|---:|
| A1 Developer / maintainer | 62 | 66 | 82 | 84 | 90 |
| A2 Security / IT / procurement | 38 | 42 | 80 | 80 | 90 |
| A3 Process / quality engineer | 55 | **86** | 87 | 89 | 93 |
| A4 Reliability / operations | 32 | 36 | 74 | 76 | 88 |
| **A5 Executive sponsor** | 48 | 58 | **60** | **64** | **86** |
| A6 Brand / website | 72 | 88 | 88 | 90 | 94 |
| A11 UI / UX auditor | 60 | **90** | 91 | 91 | 93 |
| A12 AI and engine auditor | 42 | 70 | 74 | **86** | 92 |
| A13 Infrastructure engineer | **28** | **34** | 70 | 74 | 90 |
| **HEADLINE (lowest persona)** | **28** | **34** | **60** | **64** | **86** |
| Set by | A13 | A13 | **A5** | **A5** | A5 |

### The finding the five-column view exposes

**The binding constraint changes owner at M2a.** Today and after M1 the shipping headline is set by A13, the infrastructure engineer. After M2a it is set by **A5, the economic buyer**, and it stays there through M2b.

That matters for planning. Once the deployable core lands, no further engineering moves the headline - only the Value Engine in M3 does, because A5 is capped near 60 until a euro figure with resolvable evidence exists. Anyone watching the headline through M2b will see it move four points across 230 hours of the hardest work in the programme and draw the wrong conclusion.

Report it with its owner attached, every time: **headline 64, set by the economic buyer, moved only by M3.**

---

## A6. THE TWO SCOREBOARDS, RESTATED

| | Demonstration scoreboard | Shipping scoreboard |
|---|---|---|
| Measures | Six-beat presentation readiness | Lowest persona across nine |
| Today | 62 | 28 (A13) |
| After M1 | **93** | 34 (A13) |
| After M2a | - | 60 (A5) |
| After M2b | - | 64 (A5) |
| After M3 | - | **86** (A5) |
| Optimised by | M1 | M2a, then M3 |

Always name the scoreboard and, for the shipping headline, always name the persona that sets it.

---

*End of Amendment 1. Corrections 2, 3, 4 and 6 are applied inside the backlog itself and are recorded in its change log.*
