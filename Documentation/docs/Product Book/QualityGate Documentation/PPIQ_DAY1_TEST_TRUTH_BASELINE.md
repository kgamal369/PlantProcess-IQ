# PPIQ DAY 1 - TEST TRUTH AND HYGIENE BASELINE
## 10 August 2026 | Charter v5 Amendment A1.5, Day 1

**Method:** suites executed directly, not through the pipeline, exactly as ruled. Every number below is from your run, not from an export.
**Role note:** nothing in this document was fixed by me. Day 1 is inventory and classification. Fixes are proposed with an owner and a bucket.

---

## PART 1 - THE HEADLINE NUMBERS

| | Backend | Frontend |
|---|---|---|
| Build | succeeded, **28 warnings** | succeeded |
| Tests total | **799** | **667** |
| Passed | 700 | 664 |
| **Failed** | **1** | **3** |
| Skipped | **98** | 0 |
| Duration | 40.4 s | **680.6 s (11.3 min)** |

**Effective coverage of the backend suite is 700 of 799, or 87.6 percent.** That is a better picture than the 27 July record suggested and better than I assumed. The skip mechanism is real but it is not hiding the majority of the suite.

---

## PART 2 - FOUR FINDINGS YOU DID NOT KNOW ABOUT

The three JourneyRail failures are known and owned; I have left them alone. These four are not in any handover I have read.

### F-D1-01 | The performance test project contains no tests

```
warning : No test is available in PlantProcess.PerformanceTests.dll.
```

I checked the tree. **The project directory contains exactly one file: the `.csproj`.** No source. No tests. It builds, it is referenced by the solution, and `dotnet test` reports **"PlantProcess.PerformanceTests test succeeded"**.

This is phantom coverage in its purest form: a project named after the quality dimension you score lowest, reporting success, containing nothing. It is also the reason nobody noticed there is no performance testing - the project name says otherwise.

> **Disposition:** `IMPROVEMENT REQUIRED` | **Bucket A** (my lane, test estate) | **Severity:** Medium
> **Proposal:** delete the project now, or keep it and put the first real journey benchmark in it during Day 3. Deleting is honest; keeping an empty one is not. **Your call, because deleting a project touches the solution file.**

### F-D1-02 | Fluent Assertions 8.10.0 is not licensed for commercial use

Your own test output says it, once per project:

> *"You may use Fluent Assertions free of charge for non-commercial use only. An active subscription is required to use Fluent Assertions for commercial use."*

**Confirmed in the tree: `FluentAssertions 8.10.0` in five test projects** - Application.UnitTests, Domain.Tests, Api.IntegrationTests, Infrastructure.IntegrationTests, PerformanceTests.

Version 8 is the Xceed commercial-licence line. PPIQ is a product you intend to sell to industrial customers, which makes this a commercial use. This is not a code-quality finding. **It is a legal exposure that surfaces in a customer's procurement or due-diligence review**, and it appears in your build log on every run.

> **Disposition:** `RELEASE BLOCKER` for a commercial release | **Bucket C** (needs your ruling) | **Severity:** High
> **Three options:** buy the subscription; pin back to the last MIT-licensed 7.x line; or migrate the assertions. **I do not recommend one - this is a commercial decision, not a quality one.** But it must not reach a customer unresolved.

### F-D1-03 | The tenant isolation proof never runs

```
RlsTenantIsolationTests.Forced_rls_isolates_rows_by_app_current_tenant [SKIP]
  Test DB role is superuser or BYPASSRLS, so FORCE RLS is bypassed.
```

Tenant isolation is a **hard-override category**. The database has 15 `FORCE ROW LEVEL SECURITY` statements and 15 policies, which is the correct posture. **The test that proves they work is skipped because the local test role bypasses them.**

The skip message is honest and well written - it even names the remedy, script 510. That does not change the fact that the strongest security invariant in the product has no passing evidence on this machine.

> **Disposition:** `IMPROVEMENT REQUIRED` | **Bucket A** (test environment, my lane) | **Severity:** High
> **Proposal:** create the non-superuser test role per script 510 and re-run. If that turns the skip into a pass, the hard override gets its first piece of real evidence. If it turns into a failure, that is far more important than anything else in this document.

### F-D1-04 | An architecture test and a deliberate design are in genuine conflict

```
DomainArchitectureTests.All_real_domain_entities_should_inherit_base_entity [FAIL]
  found at least one item {PlantProcess.Domain.Entities.Definitions.DefinitionVersion}
```

**This is not sloppy code.** I read the entity. Its header says:

> *"ONE IMMUTABLE VERSION OF ONE DEFINITION. Immutable is the whole point and it is enforced by what this class does NOT expose: there is no setter and no update method."*

`BaseEntity` supplies audit fields, timestamps, soft delete and concurrency. **An immutable version row arguably should not have soft delete or an update timestamp** - they contradict its whole purpose. So T-039 built it deliberately without the base, and the architecture rule from an earlier phase has no exemption for that shape.

Two defensible answers, and I am not choosing:

- **The rule is right:** `DefinitionVersion` gets `BaseEntity`, and immutability is enforced by the absence of setters rather than by the absence of a base class.
- **The entity is right:** the rule gains a declared exemption for immutable version rows, written as an allowlist with a reason rather than a silent carve-out.

> **Disposition:** `REFACTOR REQUIRED` or a rule amendment | **Bucket C** (domain model = structural override, not mine) | **Severity:** Medium
> **What I will not do:** make the test green. Adding `DefinitionVersion` to an exclusion list to clear a red would be exactly the evasion your own T-075 session caught and rejected.

---

## PART 3 - SUITE CLASSIFICATION

Per the A1.5 taxonomy.

| Class | Count | Detail |
|---|---:|---|
| **VALID** | ~1,364 | 700 backend + 664 frontend passing, executed directly |
| **BROKEN** | **1** | `DomainArchitectureTests` - see F-D1-04, a real conflict not a rotten test |
| **STALE** | **3** | The JourneyRail route-to-step expectations. **Known, owned, in your backlog. Untouched.** |
| **SKIPPED** | **98** | Classified below |
| **LOW VALUE** | **1 project** | `PerformanceTests` - zero tests |
| **FLAKY (latent)** | **8 cases** | `pageBuilderBridge` - see Part 5 |
| **NOT INVOKED** | ~28 scripts | Unchanged from the earlier census; Day 1 does not wire them |

### The 98 skips, by cause

| Cause | Approx | Reachable in CI? |
|---|---:|---|
| API integration host not configured | ~60 | Yes - CI has the host |
| Journey API host not configured | 8 | Yes |
| `PPIQ_TEST_PG_CONNSTRING` not set | 2 | Yes |
| `PPIQ_AUDIT_TRIGGER_TEST_CONNECTION` not set | 5 | Yes, with script 096 |
| `PPIQ_ACCEPTANCE_EMPTY_CONNECTION` not set | 1 | Needs the acceptance DB |
| **Test role bypasses RLS** | **1** | **No - see F-D1-03** |

**Correction to my earlier reading:** these are honest, well-labelled environment gates, and most name their own remedy. This is a better skip design than I credited it with. The residual risk is still the one I named - the probe cannot distinguish *absent* from *broken* - but the messages themselves are good.

---

## PART 4 - THE 28 BUILD WARNINGS

18 of the 28 are one class, and the class matters:

```
CS8604: Possible null reference argument for parameter 'parameters' in
        RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(...)
```

Concentrated in `Phase2OperationEndpoints.cs` (10) and `Phase2InvestigationEndpoints.cs` (7), plus `AlertEndpoints.cs`.

**Why this is not just noise:** every one of these is a **raw SQL execution path with a possibly-null parameter array**. That is adjacent to the injection surface your safe-SQL layer exists to protect, and these calls appear to sit outside it. Whether they are reachable with user input is the question Day 3 answers.

The rest: 1 obsolete `CorrelationService` still referenced from `DependencyInjection.cs:112` (this is the F-052 three-engine finding showing up in the compiler), 1 unreachable code, 2 async-without-await, 3 nullable returns in `AuthLifecycleTests`.

> **Disposition:** `IMPROVEMENT REQUIRED` | **Bucket A/B split** | Warnings-as-errors on new code only would stop the count growing without a cleanup project.

---

## PART 5 - TWO THINGS I CANNOT TOUCH, AND WHY

**The 8 `act()` warnings in `pageBuilderBridge.test.tsx`.** State updates outside `act()` are latent flakiness - they pass today and fail under timing pressure in CI. This is a test correction, local, well under thirty minutes, squarely in my lane.

**I am not touching it.** That file is in Worker 2's T-042 tree. The collision rule says a five-minute fix is not worth conflicting source histories. **Recorded and handed to him.**

**The signing key length.** One integration-test host boot crashed:

```
P01/P02 startup guard rejected weak runtime secret 'SigningKey'. Minimum length is 64.
...  Auth bound from PlantProcess:Auth  1 users, signingKeyLen=40
```

The key is 40 characters, the guard demands 64, and yet **two of the three host boots in the same run succeeded**. That inconsistency is more interesting than the length itself - it suggests two different configuration paths, one of which does not reach the guard. Authentication configuration is on the structural override list, so this is a finding, not a fix.

---

## PART 6 - THE FRONTEND SUITE TAKES 11.3 MINUTES

```
Duration 680.60s (environment 328.32s, setup 130.71s, tests 104.19s, import 64.35s)
```

**Only 104 seconds of that is running tests.** 328 seconds is jsdom environment construction and 131 is setup - **67 percent of the wall clock is overhead.**

The heaviest files are the authoring shells: `s2ShellSave` 17.2 s for 8 tests, `s2QueryBinding` 10.4 s for 17, `authoringKeyboard` 6.8 s for 11. These are full-shell renders in jsdom.

This is not a correctness problem. It is a **velocity problem that becomes a quality problem**, because an 11-minute local suite is a suite developers stop running before committing. Likely causes: a fresh jsdom environment per file, and a global setup doing heavy work 103 times.

> **Disposition:** `IMPROVEMENT REQUIRED` | **Bucket A**, Day 2 | **Severity:** Medium
> **Proposal:** measure first. If environment reuse or a lighter setup cuts it materially, that is a contained change in my lane. If it needs the test architecture rearranged, it becomes a finding.

Also noted: the main bundle is **775 kB (226 kB gzipped)**, over Vite's 500 kB warning. Recorded for Day 3 performance, not acted on.

---

## PART 7 - WHAT I PROPOSE FOR DAY 2

Nothing here changes product behaviour. All of it is inside functions 2, 3 and 4.

| # | Item | Est | Bucket |
|---|---|---|---|
| 1 | Create the non-superuser test role per script 510; re-run the RLS isolation proof | 30 min | A |
| 2 | Measure and reduce the frontend suite overhead | 45 min | A |
| 3 | Delete the 8 orphan stylesheets already named, after confirming each against comment-stripped source | 30 min | A |
| 4 | Remove unused imports and dead helpers surfaced by the build | 30 min | A |
| 5 | Write the skip ledger: every skip registered with its cause and its CI reachability | 45 min | A |

**Waiting on you:** F-D1-01 (delete the empty project?), F-D1-02 (Fluent Assertions licence), F-D1-04 (rule or entity?), and the `act()` handover to Worker 2.

---

## PART 8 - THE HONEST SUMMARY

**The test estate is in better condition than I said it was.** 87.6 percent of the backend suite executes, the skip messages are well written and name their own remedies, and 102 of 103 frontend files pass. My earlier "58 percent skipped" framing was wrong twice over - wrong mechanism, and wrong magnitude.

**What is actually weak is different from what I predicted:** an empty project impersonating performance coverage, a commercial licence warning printed on every run that nobody reads, the tenant isolation proof never executing, and eleven minutes of overhead that will quietly stop people from running the suite at all.

**None of it is the thing I would have gone looking for.** That is the argument for Day 1 existing.

---

*Executed directly per Charter v5 A1.5. No CI work performed. No files modified. Four findings, three of them requiring your ruling before anything moves.*
