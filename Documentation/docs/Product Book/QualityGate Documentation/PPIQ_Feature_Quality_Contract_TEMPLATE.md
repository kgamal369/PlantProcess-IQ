# PPIQ FEATURE QUALITY CONTRACT
### Fill this before implementation begins. Worker 1 reviews it, not the code.

> **A feature with an undefined non-functional contract is not ready to build.**
> Worker 1's verdict on this document is APPROVED, APPROVED WITH CONDITIONS, or DESIGN REWORK REQUIRED.
> DESIGN REWORK REQUIRED means implementation does not start. It is not a criticism; it is the cheapest moment to change anything.

---

```
Feature / task id ......  T-___
Title ..................
Author .................
Date ...................
Milestone ..............  M1 / M2a / M2b / M3
Page archetype .........  Operational Dashboard / Analytical Investigation /
                          Configuration and Admin / Authoring and Builder /
                          Job and Process Monitoring / Assistant and Intelligence /
                          Data Onboarding / not a page
```

---

## 1. FUNCTIONAL

**What does it do?** One paragraph, in the user's words, not the implementation's.

**Inputs** - every input, its source, and whether it is trusted.

**Outputs** - every output and where it is displayed or stored.

**States** - tick every state that applies and describe what the user sees in each. An unticked state that actually occurs is a defect found later.

- [ ] Loading
- [ ] Empty
- [ ] Error
- [ ] Stale
- [ ] Filtered
- [ ] Selected
- [ ] Running
- [ ] Failed
- [ ] Completed
- [ ] Validating / invalid
- [ ] Saving / saved / unsaved changes

**Permissions** - which permission string, which roles, which tenant scope.

**Acceptance criteria** - each one observable, each one naming the surface it is observed on.

---

## 2. PERFORMANCE

Cite the SLO Budget row this operation belongs to, or declare a new one with its reason.

```
Operation class ........  Interactive UI / API / Jobs / Analytics / Frontend
SLO row cited ..........
P50 ....................
P95 ....................
P99 ....................
Reference condition ....  dataset size, widget count, concurrency
Maximum supported ......  e.g. widgets per workspace, rows per query, population per run
Behaviour above the max.  Honest refusal with a named reason, never degradation
```

**If no SLO row fits, say so.** A new operation class is a legitimate answer; an absent number is not.

---

## 3. SECURITY

Complete the lightweight threat model. Extend the PPIQ starting inventory in the instruments workbook.

```
Assets touched .........
Trust boundaries crossed
Attacker profile .......  unauthenticated / low-privilege user / other tenant /
                          malicious source / insider with server access
Abuse cases ............  how is the intended function misused
Privilege escalation ...  how could a Viewer reach this
Tenant crossover .......  which predicate or policy prevents it
Injection surface ......  where untrusted input reaches an interpreter
Secrets ................  what could leak, and into what
Insecure default .......  what is dangerous out of the box
Audit events ...........  what is recorded, immutably
Route added to the access matrix?   yes / no / not a route
```

---

## 4. UX

```
Persona ................
Task ...................  stated as the user would state it
Archetype ..............
Step budget ............  maximum interactions to complete the task
Discoverability ........  how does the persona find this without being told
Error recovery .........  what happens after a mistake, and how do they get back
Progressive disclosure .  what is hidden until needed, and how it is revealed
Content share ..........  archetype minimum met
Interactive above fold .  archetype maximum not exceeded
```

---

## 5. RELIABILITY

```
Timeout ................  value, and what the user sees
Cancellation ...........  is it cancellable, and how fast
Retry ..................  automatic or manual, how many, with what backoff
Idempotency ............  key, and what a duplicate submission produces
Partial failure ........  what is kept, what is rolled back
Concurrent update ......  last-write-wins, optimistic version, or lock
Lost dependency ........  what the user sees when a source or the database is gone
Restart ................  what survives a process restart
```

---

## 6. DATA TRUST

Skip only if the feature displays no analytical figure.

```
Provenance .............  run id, batch id or evidence handle carried and displayed
Freshness ..............  as-of timestamp, and how the user knows it is stale
Snapshot identity ......  can this exact result be recomputed later
Population .............  n displayed with every statistic
Methodology ............  method, window and grain displayed
Abstention .............  what happens when evidence is insufficient
Synthetic data .........  is any emulated or seeded data displayed, and is it labelled
```

---

## 7. OBSERVABILITY

> If this fails at 02:00 at a customer plant with nobody on site, can we understand why?

```
Structured log events ..  names of the events emitted
Correlation id .........  carried from request to job to result
Reason codes ...........  enumerated, not free text. List them
Lifecycle events .......  queued / started / progressed / terminal
Metrics ................  counters and histograms emitted
Health signal ..........  how an operator knows this subsystem is healthy
Alertable conditions ...  what an operator should alert on
```

---

## 8. TESTABILITY

```
Unit ...................  which behaviours, at which seam
Integration ............  which boundary, against what
Contract ...............  which endpoint, which envelope
Browser / runtime ......  which assertion proves it actually renders
Failure path ...........  how each failure state is forced in a test
Golden journey .........  does this feature sit on one, and which
Regression gate ........  which Q-gate holds this after it ships
```

**A behaviour that cannot be forced to fail in a test is a behaviour with no test**, however many assertions surround it.

---

## 9. WORKER 1 REVIEW

```
Reviewed by ............
Date ...................
Verdict ................  APPROVED / APPROVED WITH CONDITIONS / DESIGN REWORK REQUIRED
```

**Conditions** (if any) - each named, each with an owner and a date.

**Reasons** (if DESIGN REWORK REQUIRED) - which sections are incomplete and what would complete them.

```
Gate that will judge this at delivery ..........
Acceptance criteria Worker 1 will verify against
```

---

*A contract that is filled honestly and reviewed in an hour is the cheapest quality instrument in this system. A contract skipped is paid for at implementation cost, and occasionally at customer cost.*
