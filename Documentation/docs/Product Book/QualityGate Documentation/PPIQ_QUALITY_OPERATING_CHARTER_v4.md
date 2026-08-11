# PPIQ QUALITY OPERATING CHARTER
## Version 4.0 - 10 August 2026
### Six functions. Governance of major rework, not execution of it.

**Instruments:** `PPIQ_Quality_Instruments_v4.xlsx` - twelve sheets, all usable as forms
**Contract form:** `PPIQ_Feature_Quality_Contract_TEMPLATE.md` - Worker 2 fills, Worker 1 reviews
**Judgements:** `PPIQ_Rework_Judgements_v3.xlsx` | **Findings:** `..._Register_v2.xlsx` | **Gates:** `..._Matrix_v1_1.xlsx`

---

## PART 1 - THE BOUNDARY, IN NUMBERS

The correction is accepted, and the clearest way to show I have absorbed it is to apply it retrospectively to my own work.

I have issued 13 rework judgements totalling **162 hours**. Under this charter:

| | Hours | Owner |
|---|---:|---|
| R-05, one gate contract and meta-gate | **14** | **Worker 1 - mine, function 2** |
| The other twelve judgements | **148** | Worker 2, Karim, or a designer |

**I execute 9 percent of the rework I demanded.** For the other 91 percent I write the requirement, the acceptance criteria and the regression gate, then hand off and verify. The `Judgement Ownership` sheet carries this per judgement, including what my part is on each.

That ratio is the charter working. If it ever inverts, the boundary has failed.

---

## PART 2 - THE SIX FUNCTIONS

| # | Function | Judge | Execute |
|---|---|:-:|:-:|
| 1 | **Quality Design Review** - review the design before implementation | yes | - |
| 2 | **Test and verification engineering** - every class of test and gate | yes | **yes** |
| 3 | **Code hygiene and static quality** - linters, analyzers, boundaries, scanning | yes | **yes** |
| 4 | **Minor remediation** - local, low-risk, no architecture or workflow change | yes | **yes** |
| 5 | **QA/QC product assessment** - thirteen dimensions, seven verdicts | yes | - |
| 6 | **Quality governance** - reject, block, require rework, Quality Stop, certify | yes | - |

Functions 2, 3 and 4 are the only places I both judge and build. Everywhere else I produce a requirement and somebody else produces the change.

**The handoff protocol for functions 5 and 6:**

```
ASSESS -> DIAGNOSE -> PROVE THE GAP -> DEFINE THE REQUIRED OUTCOME
-> STATE WHY LOCAL FIXING IS INSUFFICIENT -> ISSUE THE REQUIREMENT
-> DEFINE ACCEPTANCE CRITERIA -> HAND OFF -> VERIFY -> INSTALL THE GATE
```

The gate is installed **after** verification, never before. A gate around an implementation that has not yet been corrected only locks in the defect.

---

## PART 3 - QUALITY STARTS BEFORE CODE

This is the largest addition in v4 and the one with the best return.

Until now the loop was: Worker 2 builds, I find the design was wrong, we lose the implementation. The corrected loop is:

```
Requirement -> Quality Design Review -> Implementation -> QC -> Rework if needed -> Verify -> Gate
```

**The instrument is the Feature Quality Contract.** Worker 2 fills nine sections before writing code: functional, performance, security, UX, reliability, data trust, observability, testability, and my review block. I review the document, not the code. Verdict: **APPROVED**, **APPROVED WITH CONDITIONS**, or **DESIGN REWORK REQUIRED** - and the third means implementation does not start.

The twelve review areas and what triggers rework are on the `Quality Design Review` sheet. The most commonly missed, in my experience of this codebase's defect record, are: failure states (only the happy path described), observability (logging deferred to implementation), and extension model (a new instance requires a code branch, which is RC-1 being created in advance).

> **A design review that finds a missing failure state costs an hour. Finding it after implementation costs the implementation.** Every hour spent here is the cheapest hour in this system.

**One honest caveat:** this instrument only pays if it is applied to *new* work. Applying it retroactively to T-043 or T-071 to T-075 would be theatre. Start it at the next feature that has not begun.

---

## PART 4 - NO MORE "PERFORMANCE IS GOOD"

The `SLO Budget` sheet carries 21 rows across five operation classes with P50, P95 and P99, plus the reference condition each is measured under.

**Every row is marked either `Proposal` or `Unproven`, and the distinction matters:**

- **Proposal** - derived from archetype intent or convention. Karim rules it, then a baseline measurement makes it a gate.
- **Unproven** - a target with *no measurement behind it at all*. Ten rows are in this state, including every Jobs row, because **the hundred-job concurrency claim has never been tested** and every Analytics row, because **no engine run has ever completed**.

> **A budget with no baseline behind it is a hypothesis, not a target.** Until a row is measured, a PERFORMANCE FAIL verdict may cite it as a design expectation but never as a breach. This is the same discipline as not writing the gate before the diagnosis.

One row is anchored to a real measured target: the associative re-shade at 300 ms P95, which M2-42 already states. That is the only row I would defend today, and R-11 exists because the current eight-call shape cannot meet it.

---

## PART 5 - OBSERVABILITY IS A QUALITY STANDARD

New verdict in the vocabulary: **`OPERABILITY FAIL`**.

> A critical subsystem that works but cannot be diagnosed at 02:00 at a customer plant with nobody on site fails this standard, regardless of its test results.

PPIQ is self-hosted and may sit in a plant nobody can reach. That makes this a first-class requirement, not an operational nicety.

Assessed against eleven capabilities on the `Observability Standard` sheet. Two things are worth saying plainly:

**The foundation is unusually good.** Run ids and batch ids are already first-class concepts - the deck honesty lint requires every claim to name one, and `rejected_by_safe_sql` is a status rather than an exception. Most products bolt reason codes on afterwards; PPIQ has them in its vocabulary already.

**What is unverified is whether they correlate end to end.** Six of eleven capabilities are UNVERIFIED and one - **alertable conditions** - is MISSING outright: no set of conditions an operator should alert on is defined anywhere. That is a gap I can name today without any repository access, because it is an absence of a document rather than a property of code.

---

## PART 6 - UX REJECTIONS CITE A PERSONA FAILING A TASK

The `Persona Task Standards` sheet defines seven tasks with measurable criteria. This is the defence against taste, and it is stronger than the archetype budgets because it is empirical rather than derived.

| Persona | Task | Pass criterion |
|---|---|---|
| Plant engineer | Find why the defect rate rose last shift | Insight under 3 min, first correct click at or above 70 percent, can state the evidence |
| Shift supervisor | Understand production risk in 30 seconds | Correct assessment in 30 s, no scrolling, as-of read correctly |
| Process engineer | Author a cross-source join without help | Completes unaided, every refusal actionable, join retrievable later |
| Data or BI analyst | Rebind a widget to shift and confirm it persisted | Under 5 interactions and 90 s, survives reload, analyst is certain |
| Administrator | Add a source without fear of breaking production | Preview before commit, confidence 4 of 5, zero accidental destructive actions |
| Infrastructure engineer | Diagnose a 02:00 job failure without a log file | Cause identified from the interface alone, remedy stated |
| CEO or buyer | State the conclusion and its evidence | Repeats the finding, names its provenance, distinguishes abstention from failure |

> **Admissible:** *"The page fails the plant engineer investigation task: first correct click 2 of 7, and no participant could state the evidence behind the finding."*
> **Not admissible:** *"The page is too busy."*

None of these has been run with a real plant professional. That is the next thing that would move this from a standard to a measurement.

---

## PART 7 - SECURITY: THREAT MODEL BEFORE SCANNER

The order is now explicit:

```
Threat model -> secure design -> implementation -> security tests -> scanners -> DAST
```

Not `code -> CodeQL -> green -> secure`. **A scanner finding is evidence. The security architecture judgement is the verdict.**

The `Threat Model Template` sheet carries eleven sections with a PPIQ starting inventory in each, so a lightweight model for a sensitive feature is a half-hour of extension rather than a blank page. The inventory is already populated from what the documents establish - the plant data, the authored relational model, the licence tokens, the connector trust boundary, the safe-SQL injection surface, the browser storage leak, the bootstrap admin default, and the audit bypass through direct SQL function execution that already wrote 260 rows.

That last one is worth noting: **the threat model I would have written before implementation would have caught F-004.** An unaudited write path is an abuse case, not a defect discovered by measurement.

---

## PART 8 - QUALITY STOP: ONE ISSUED, ONE ARMED

I am exercising this authority once, immediately, because an authority that is granted and never used is decoration. I am also using it sparingly, because a QA authority that stops everything on day one loses the right to stop anything later.

### QS-01 - ACTIVE

> **Scope:** registry-declared capabilities in the widget, dimension, measure and analysis areas.
> **Instruction:** do not add new registry-declared capabilities in these areas until the fail-fast composition root lands.

**Evidence:** five measured incidents from one structural cause - a measure published with no implementation; dimensions still unbound; the outcome loader ignoring the declared statistical type; 260 of 320 results under undeclared keys; the widget card inferring its category column. All five answered HTTP 200. All five were found by a person, late.

**Why work here compounds:** each new declared capability added before the binding lands is another instance of a defect class with a five-incident record, and each costs an investigation rather than a build.

**Correction:** R-02, 8 hours, Worker 2.
**Exit:** the composition root is merged, falsified once by removing a handler and observing the refusal, and Q-052 is green in the pipeline. **The stop lifts automatically at that point. It does not need a meeting.**

### QS-02 - CONDITIONAL, not yet active

> **Scope:** new routes and features that depend on user identity.
> **Trigger:** the first identity-dependent route scheduled in M2a, unless authentication is scheduled first.

Every route added before authentication exists is a route to be retrofitted, and the retrofit is where authorization defects are introduced. **Correction:** R-06, 24 hours. **Exit:** authentication merged and Q-073 green with every route declared.

**Karim may lift either stop by ruling.** The lift is recorded with its reason and does not become a passed gate.

---

## PART 9 - RELEASE CERTIFICATION IS NOT A TEST COUNT

Four certificates, one per milestone, each answering a different question. The `Release Certification` sheet carries what must be true for each and what currently blocks it.

| | Question |
|---|---|
| **M1** | Is the presentation safe, coherent and defensible? |
| **M2a** | Can this be installed, secured, operated and recovered at a customer? |
| **M2b** | Is the analytical claim true, evidenced and reproducible? |
| **M3** | Is it production grade, supportable and provable? |

Three outcomes only: **PASSED**, **PASSED WITH ACCEPTED EXCEPTIONS** (each named, owned and dated), **REJECTED**. The certificate is an evidence pack assembled from retained gate artefacts. *"713 tests green"* is not a certificate.

---

## PART 10 - GOLDEN ASSETS

Three assets that are worth more than thousands of isolated unit tests, and PPIQ has partial raw material for all three.

| Asset | What it catches | Raw material today |
|---|---|---|
| **Golden Dataset** | Calculation and methodology regressions, grounding errors, silent coefficient drift | The presentation dataset is well characterised - 2,441 independent heats, 91,417 outcome events, 38 widgets carrying data. **No expected-result record exists**, and it must include expected *abstentions* and their reasons, not only expected numbers |
| **Golden Journeys** | Integration regressions between subsystems no unit test can see | The fifteen-step rail and the consolidated test pass are most of the specification already. **Nothing is automated as a journey** |
| **Golden Environment** | Everything that only works because of accumulated local state | A clean-machine deploy acceptance script exists. **It is not rebuilt per release**, and the two databases have diverged |

**F-011 and F-013 would both have been caught the day they occurred** by a golden environment rebuilt per release. That is the argument for this asset in one sentence.

---

## PART 11 - THE GOVERNING RULE

> **No feature earns DONE because it exists.**
> **It earns DONE when its functionality, quality attributes, runtime behaviour, user experience, security and evidence meet the milestone-specific standard.**

Everything in this charter is machinery for applying that sentence consistently. SonarQube, Playwright, CodeQL, axe and k6 are instruments inside it, not the system itself.

---

## PART 12 - RULINGS OPEN

1. **SLO Budget** - accept the 21 rows as proposals, or rule the numbers now? The ten Unproven rows cannot be gated either way until measured.
2. **Quality Design Review** - start at the next unstarted feature? I recommend not applying it retroactively to in-flight work.
3. **QS-01** - accepted, or lifted by ruling? It costs 8 hours to exit.
4. **Carried from v3, still open:** R-07 palette or configurator; R-08 pipeline model or flat generator; R-06 authentication as one unit or split; R-11 reclassification.

---

## PART 13 - STATUS BLOCK

```
Model ................................. Six functions, governance without execution
Boundary applied to my own work ....... 14h of 162h is mine. 91 percent handed off
New verdicts .......................... OPERABILITY FAIL, ARCHITECTURE QUALITY FAIL,
                                        DESIGN REWORK REQUIRED, QUALITY STOP
Quality Stops ......................... QS-01 active, QS-02 armed
Instruments delivered ................. Design review, feature contract, SLO budget,
                                        persona tasks, threat model, observability
                                        standard, stop register, four certificates,
                                        golden assets, RACI
SLO rows measured ..................... 1 of 21
Persona tasks run with a real user .... 0 of 7
Still blocked ......................... Page-level UX verdicts, on screenshots
                                        Net-new effort, on backlog v2.9.2
                                        12 gate rows, on the inventory run
```

---

*Compiled 10 August 2026. The instruments are forms, not descriptions of forms - they are meant to be filled. The boundary is stated in hours rather than in principle, because a boundary that cannot be counted is not one.*
