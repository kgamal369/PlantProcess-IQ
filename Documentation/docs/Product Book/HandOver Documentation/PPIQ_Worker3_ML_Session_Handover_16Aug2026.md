# PPIQ Worker 3 — Deep Session Handover
## ML lane + Assistant C# lane, 15–16 August 2026

**Read this document in full before touching anything.** It is written so the next
session does not repeat a single investigation, test run, or debugging cycle that has
already been paid for.

---

# SECTION 0 — START HERE

## 0.1 What you are

You are **Worker 3** in a multi-worker structure. Confirm this at session start.

| Worker | Owns |
|---|---|
| Karim | Product owner, architecture authority, all rulings, runs every pack himself |
| Worker 1 (Claude) | QA/QC authority, testing, micro-remediation |
| Worker 2 (Claude) | Implementation owner — frontend, backend features, presentation lane |
| **Worker 3 (Claude)** | **PlantProcess IQ Engine, AI, ML, LLM implementation — this is you** |

Worker 2 was **live in the Backend throughout this session**, editing
`PlantProcess.Application/Dashboarding/**`, `WidgetResultSources.cs` and frontend
chart files. Never touch their files. Every pack this session asserted the ownership
boundary before writing.

## 0.2 The state at the end of this session

Fourteen commits landed on `main` from the Worker 3 lane. Two lanes of work:

**Python ML lane** — `ML/` — went from 81 tests to **503 tests**, all passing.
**C# Assistant lane** — `Backend/PlantProcess.Application/Assistant/**` — four new
isolated layers, 169 probes, none registered into production.

## 0.3 Do not do these things

- Do not re-run the 503 Python tests to "check". They pass. Section 6 records every run.
- Do not re-investigate the pack tooling defects. Section 1.4 records all five classes
  and the tools that now catch each one.
- Do not reopen any closed task. Section 8 lists what is closed and at which commit.
- Do not touch deployment, server, Caddy, compose, Jenkins or the app URL. Section 9
  explains why: **no knowledge of them exists from this session.**
- Do not write a commit hash you have not seen in text Karim sent. See W3-027.

## 0.4 The immediate next action

**T-182 — B-01..B-09 benchmark harness.** Last task in the frozen SAFE-NOW lane.
Not started. Requires Karim's explicit word before beginning.

Expect it to face the same honest limit T-181 faced: benchmarks that need a running
serving runtime cannot be measured in an isolated build, and must be reported as
capability-unavailable rather than filled with plausible numbers.

---

# SECTION 1 — ACTIVITY LOG, FINDINGS, TIPS AND TRICKS

## 1.1 The sequence of work

The session executed the frozen Worker 3 SAFE-NOW lane in order, one task to full
closure before the next:

```
T-175 -> T-176 (2 packs) -> T-173 -> T-174 -> T-170 -> T-172
      -> T-178 (+ corrective) -> T-179 -> T-180 -> T-181 -> T-137
```

The first six are Python (`ML/`). The last four are C#
(`Backend/PlantProcess.Application/Assistant/`). T-178 is Python.

## 1.2 The single most important lesson of the session

**A totality check is not a correctness check.**

In T-178 I wrote a test asserting that all 512 combinations of nine checks landed in
one of four states. It passed against a **wrong** classification rule. Karim caught it
and issued a corrective ruling. The corrected test now restates the ruled precedence
independently of the implementation and compares against it, so it fails if the
implementation drifts in either direction.

Generalise this: a test that asserts a result is *well-formed* proves far less than a
test that asserts the result is *the specific right one*. When writing a test, ask what
wrong implementation would still pass it.

## 1.3 Second most important: guards must judge code, not prose

This defect class occurred **six times** in one session, each time in a new shape:

| # | Task | The guard matched | Should have matched |
|---|---|---|---|
| 1 | T-175 | `lightgbm` in a lock-file **comment** | a `lightgbm==` pin line |
| 2 | T-176 | causality words in a **docstring explaining their absence** | code |
| 3 | T-173 | `mean_recall`, a **computed local** | a module-level literal |
| 4 | T-174 | `fixture` in the **provenance label** `fixture_declared_typed_contract` | fixture knowledge |
| 5 | T-179 | `Process.` inside `PlantProcess.` | `Process.Start` |
| 6 | T-179 | `prompt` in the doc comment *"No prompt, no gateway"* | code |

**The rule now applied everywhere:** strip comments and docstrings before scanning;
judge module-level literals rather than any name; assemble forbidden needles from
fragments so a guard cannot match itself; and prefer a precise needle
(`Process.Start`) over a broad one (`Process.`).

Every Python and C# guard in the repository now follows this. Copy the pattern from
`tests/test_t178_boundary.py` (Python) or `AssistantPlannerIsolationTests.cs` (C#).

## 1.4 Pack tooling defect classes — five, all now caught by tooling

I ship PowerShell apply packs I **cannot execute** (no PowerShell in the container, no
package feed to install one). Every pack had its *payload* falsified and its
*PowerShell* never run. Five defect classes reached Karim's machine before tooling
closed them:

### Class A — `Start-Process -Wait` with redirected handles hangs
`dotnet build` finished in 35 seconds; the pack sat for 10+ minutes. MSBuild leaves
worker nodes alive, those nodes inherit the redirected handles, and `-Wait` blocks
until every inheritor closes them.
**Fix:** `-PassThru` without `-Wait`, then `$run.WaitForExit(ms)` with a bound; kill
the tree with `taskkill /T /F` on timeout. Plus `MSBUILDDISABLENODEREUSE=1` and
`-m:1 /nodeReuse:false`.
**Why it never bit the Python packs:** `python -m unittest` spawns no persistent
children that inherit handles.

### Class B — `Start-Process -PassThru` loses the process handle
`ExitTime` was null (v3), then `ExitCode` was null (v4). One root cause, two symptoms.
In v3 I fixed the *symptom* with a stopwatch and shipped v4 with the cause intact.
**Fix:** `try { $null = $run.Handle } catch { }` immediately after starting. Touching
`Handle` once caches it and keeps both readable.
**Worse consequence:** an unreadable exit code made the pack report
`PRE-EXISTING BUILD FAILURE ... in another lane` — a false accusation against
Worker 2 for my own defect. The pack now says *"that is a defect in this pack, not a
verdict on the code under test"*.

### Class C — `dotnet build` accepts exactly one project
`MSBUILD : error MSB1008: Only one project can be specified.` Passing two produced a
build that never ran, which the pack again misattributed to another lane.
**Fix:** one project per invocation, plus a `Test-InvocationDefect` helper that reads
the output for `MSB1008/MSB1001/MSB1003/MSB1011` and reports **PACK DEFECT** rather
than a verdict on anyone's code.

### Class D — a path variable naming the wrong layer
`$verificationDir` resolved to `.../Assistant/Retrieval` (T-181 v1), and
`$servingDir` to `.../Assistant/Verification` (T-137, caught locally). Cause: deriving
each generator from the previous one and a path-string replacement with the wrong
escaping level that silently matched nothing.
**Fix:** `verify_pack_wiring.py` reads the pack's own `Join-Path` assignments and
resolves them against a modelled repository. Layer names are **derived** from the
directories in play, not hardcoded — the first version listed three names and missed
`Serving`.

### Class E — guards matching prose (see 1.3)

## 1.5 The tooling built this session — reuse it, do not rebuild it

All under `/home/claude/` in the container. **These do not survive the session.**
Rebuild from the descriptions below if needed; each took real debugging to get right.

| Tool | What it checks | Validated against |
|---|---|---|
| **payload falsifier** | here-strings reproduce the tested tree byte-for-byte | caught the here-string trailing-newline defect |
| **guard simulator** | runs the pack's own self-checks in Python against its payload | caught the T-179 `prompt` comment guard, the T-181 `CapabilityUnavailable` file guard |
| **`lint_pack_powershell.py`** | PS 5.1 traps: `-Wait`+redirect, `ExitTime`, PassThru without Handle, two projects, int+string, `&&`, trailing comma, doubled regex backslash | reconstructed each historical defect and confirmed it fires |
| **`verify_pack_wiring.py`** | directory variables resolve to the layer their name claims | reconstructed T-181 v1 and T-137 defects |
| **xunit shim + reflection runner** | compiles and runs the real C# test files offline | see 1.6 |

**Critical insight about the falsifiers:** the payload falsifier and the guard
simulator are blind to different things. The payload one cannot see a bad guard; the
guard one cannot see bad variable wiring. Each was built *after* a defect it could not
have caught. If a new defect class appears, assume the existing tools are blind to it
and build the third check.

## 1.6 The xunit shim — the most dangerous tool, and why

The container has .NET SDK 8 (installed via `apt-get install dotnet-sdk-8.0` after
`apt-get update`; the first attempt 404s without the update) but **no NuGet access**.
So the real xunit package is unavailable, and C# test files are compiled against a
hand-written `Xunit` namespace shim plus a reflection runner.

**This shim was wrong twice, and both times it reported false green:**

1. **`Assert.Equal` semantics.** `ImmutableArray<T>` implements
   `IEquatable<ImmutableArray<T>>` and compares the **underlying array by reference**.
   xunit prefers `IEquatable<T>` over element-wise comparison, so two arrays with
   identical contents are unequal — and print identically, which is why the output was
   baffling. My shim fell back to `SequenceEqual` and passed 32/32 while the real
   xunit failed 10/24.
   **Fix in the shim:** try `IEquatable<T>` first, exactly as xunit does.
   **Fix in tests:** never `Assert.Equal` on two `ImmutableArray`. Every C# test file
   now has a private `AssertOrdered(IEnumerable<string>, IEnumerable<string>)` helper
   that compares `.ToArray()`. **Copy this helper into any new test file.**
   One probe was also passing for the wrong reason: `Assert.NotEqual` on two immutable
   arrays passes on reference inequality whatever the contents.

2. **Async tests were not awaited.** The runner called `method.Invoke` and discarded
   the returned `Task`. Fourteen of T-137's 28 probes are `async Task`; every async
   assertion would have passed regardless. Fixed by awaiting via
   `task.GetAwaiter().GetResult()`, and by adding a bare `catch (Exception)` because an
   awaited Task throws directly rather than wrapped in `TargetInvocationException` —
   without it, one async failure aborts the whole harness and reports nothing.
   **After the fix, all four Assistant tasks were re-run: 161/161**, confirming no
   earlier async probe had been silently passing.

**Rule for the next session:** when a local harness and the target machine disagree,
suspect the harness first. And when adding a new xunit assertion overload to the shim,
mirror xunit's real semantics, not a convenient approximation.

## 1.7 Mutation testing — added mid-session, now standard

A green suite proves nothing unless it can go red. From T-173 onward every task
included deliberate mutations of its core invariant.

**Three mutations were ineffective and had to be redone. Never count an ineffective
mutation as evidence:**

- `if (false && A || B)` — C# `&&` binds tighter than `||`, so `B` still evaluated.
  Replace the **whole condition** with `if (false)`.
- A broadened fallback loop was neutralised by an inner approval guard — defence in
  depth, but the mutation targeted the wrong line. Both layers had to be removed.
- A Python string replacement did not match; the assertion failed and I nearly read the
  resulting 58/58 as proof. **Always assert that the mutation applied.**

## 1.8 Miscellaneous tips discovered

- **PowerShell 5.1 casts the right operand to the left operand's type.**
  `$x.Count + ' files'` throws. Put the string first or `[string]$x.Count`.
- **Here-strings drop the newline before the terminator.** Emit `body + "\n'@\n"` or
  every written file lands one byte short. This silently broke 16 of 17 files.
- **A trailing comma in a PowerShell array literal is a parse error.**
- **`Path.read_text()` applies universal-newline translation** and hides CRLF. Use
  `read_bytes().decode()` when checking line endings.
- **The repository's C# convention is LF**, not CRLF, in all three target folders
  (Assistant 18 LF / 3 CRLF; Application.UnitTests 62/12; Architecture.Tests 24/1).
  Git reports `LF will be replaced by CRLF` on commit; committed blobs stay LF.
- **`git check-ignore -v <path>` before every commit.** The root `.gitignore` has an
  unanchored `runtime/` that swallowed `ML/src/ppiq_ml/runtime` in a previous session.
- **The container suite time is unreliable.** Two runs reported 90 s and 98 s; clean
  re-runs gave 19–22 s. Never ship on a single slow measurement; re-run.

---

# SECTION 2 — WHAT WAS IMPLEMENTED, IN DETAIL

## 2.1 Python ML lane — `ML/src/ppiq_ml/`

Final inventory (`find src/ppiq_ml -name "*.py"`):

```
__init__.py       1
artifacts        10   (pre-existing, T-169)
runtime           7   (pre-existing, T-168)
models           19   (mf03_novelty 7 + mf04_supervised 12)
governance        7   (T-176 Pack 1)
explanation       4   (T-176 Pack 2)
similarity        6   (T-173)
sequences         6   (T-170)
encoders          6   (T-172)
remediation       6   (T-178)
```

29 test files under `ML/tests/`.

### T-175 — `models/mf04_supervised/` (12 files)
Supervised outcome training behind the T-168 protocol.
Chain: typed fixture OutcomeDefinition + sealed T-169 artifact → leakage gate →
eligibility gate → **mandatory `PriorBaseline` first** → `GbdtTabularCandidate`
(LightGBM) → shared out-of-time holdout → T-168 ResultManifest.

Supports binary, multiclass, ordinal, continuous.

**Design points worth preserving:**
- Metrics are **standard library only** so known-answer fixtures can certify them.
- Undefined metrics are **omitted with a written reason**, never emitted as NaN —
  NaN would also produce JSON the .NET side cannot parse.
- The constant predictor scores **exactly 0.5000 AUC** and **−0.0003 R²**. These are
  the known answers that make every candidate number interpretable. (Karim ruled: do
  not describe −0.0003 as "exactly zero" — it is a measured value near zero.)
- Timings are kept **out of the hashed evidence record** so artifact hashes are
  deterministic across processes.
- LightGBM determinism: `deterministic=True, force_row_wise=True, num_threads=1, seed`.

### T-176 — `governance/` (7 files) + `explanation/` (4 files)
**Pack 1** — three independent dimensions (QUALITY / SERVING / TRAINING), no weighting,
no total, no arithmetic between dimensions. Four-clause encoder inequality as one
conjunction. `NOT_EVALUABLE` when a declared budget has no measurement.
**Pack 2** — real TreeSHAP via LightGBM's native `pred_contrib=True`, behind an
`ExplanationProvider` interface. **No `shap` package added** — TreeSHAP is an
algorithm, and the pinned booster computes it.

**The property that makes the evidence uncheatable:** base value plus contributions
reconstructs the model's raw output to **4.441e-15**. A fabricated table cannot do that.

### T-173 — `similarity/` (6 files)
`VectorSimilarityIndex` contract, sealed immutable generations identified by content,
`ExactFlatIndex` as the **permanent correctness oracle** (standard library only — what
defines a correct answer must not move because a package changed its summation order),
`PartitionedProbeIndex` as one replaceable approximate candidate, `recall_probe`.

**FAISS ruling:** FAISS is a candidate family, not the contract. A cp313 win_amd64
wheel exists (`faiss_cpu-1.15.0`), but the proof chain does not depend on it.

### T-174 — `models/mf03_novelty/` (7 files)
Mandatory `RobustDeviationBaseline` (median + MAD — the mean is moved by exactly the
outliers the model must find) and `NeighbourDensityCandidate` which **reuses T-173's
sealed exact index** rather than defining "nearest" a second time.

Four parts kept apart on every answer: score, threshold identity, population context,
refusal state. A result carrying both a refusal and scores is rejected at construction.

### T-170 — `sequences/` (6 files)
Chunked immutable typed numeric arrays. Footer chunk index (the index cannot be written
until offsets are known; putting it first would mean buffering the payload).

**A real design defect found and fixed mid-build:** the payload content hash originally
depended on the chunk size, which would have made a B-04 comparison across chunk sizes
compare two different payloads. Fixed with **one running digest per channel**.

Also fixed: `read_manifest` was hashing the entire file just to read the footer;
the compression ratio was measuring the index rather than the codec.

### T-172 — `encoders/` (6 files)
PyTorch temporal-convolution process encoder. **Placed at `encoders/` and not
`models/`** because the T-175 boundary guard restricts `models/` to lightgbm and numpy;
widening it would have meant editing a closed task. Karim ruled this **ACCEPTED
TOPOLOGY** (W3-022 resolved).

**Reproducibility, measured:** identities and embeddings identical across processes;
**artifact byte hash differs** (`7eee10627248` vs `1d4fa8eace89`). Karim's ruling
anticipated this — do not demand byte-identical serialized artifacts.

### T-178 — `remediation/` (6 files)
Nine frozen checks RM01..RM09 as a **declarative table**, four outcome states, and a
seven-condition `can_accept` authority.

**The corrective (`b5dbf232`)** replaced my too-broad rule. Canonical precedence:

```
1. RM04 fails                                        -> suppressed
2. all pass                                          -> actionable
3. RM05..RM09 ALL pass AND one of RM01/RM02/RM03 fail -> evidence_only
4. every other non-safety failure                    -> exploratory
```

Distribution over all 512: **1 actionable, 7 evidence_only, 248 exploratory,
256 suppressed.** My wrong rule would have given 255 evidence_only — the correction
moved **248 cases**.

## 2.2 C# Assistant lane — `Backend/PlantProcess.Application/Assistant/`

Four isolated namespaces, 16 source files, 9 test files. **None registered into
production DI. No route, no migration, no M1 dock change.**

### T-179 — `Planning/` (2 files, 32 probes)
`DeterministicToolPlanner`: permission → ambiguity → capability match → canonical order.

**The strongest guarantee is structural:** `PlanningRequest` has **no field for the
question text**. Equivalent meaning cannot change a plan because the wording is
unreachable. A reflection probe asserts no such field exists.

Zero-LLM proven three ways: no model path in source; static class with no constructor
and no fields; one public method taking `PlanningRequest`, returning `ToolPlan`.

### T-180 — `Retrieval/` (5 files, 43 probes)
**Permission enforced by the type system.** `HybridRanker.Rank` requires a
`PermittedCandidateSet`, which has **no public constructor** — the only producer is
`PermissionSafeCandidateFilter`. No ranking, scoring, packing or counting path can be
reached with an unfiltered pool.

The rejected-by-permission count exists on the internal set and is **deliberately
absent from the pack**, because a count of what a caller may not see is a disclosure.

**The mutation that proves the point:** with permission disabled, forbidden items never
appeared in the pack — but `Truncated` flipped `False → True` and the fingerprint
gained their handles. That is the leak-through-truncation channel, demonstrated.

Class before score; duplicates collapse retaining every handle; token budget less a
reserved answer allowance; truncation always disclosed.

### T-181 — `Verification/` (5 files, 58 probes)
Post-LLM verifier that **calls no model**. Typed claim ledger checked against the T-180
pack: value, unit, quantity kind, subject. **The rendered text is inspected too** — a
number no claim declares, a handle the pack lacks, phrasing above its class, an erased
refusal.

V1–V11 all implemented. **V4 is the 27 July review's fatal failure** — a rolling speed
answered in kilograms — now a named rejection (`UnitMismatch`).

Transport failure is its **own verdict** (`SystemFailure`), never an absence of
evidence, relationship or risk.

**Q-01..Q-07 measured. Q-08..Q-11 report `CapabilityUnavailable`** with unit,
aggregation, reason and owner, and **no value at all**.
`AllElevenGatesMeasuredAndPassing` is false and cannot be made true.

### T-137 — `Serving/` (4 files, 36 probes)
Replaceable `IModelServingRuntime` over an `IModelTransport` seam. No verb, path or
provider body shape assumed anywhere. Self-hosted/private/BYOM are first-class.

**Minimum-scoped payload by construction:** `ModelInvocationRequest` has **no field**
for tenancy, permission, omitted evidence or any fingerprint.

**Identity checked twice** — a response claiming a different release is refused.
**Fallback approval is a relation to a specific primary**, and a spy transport proves
the unapproved endpoint was *never contacted*.

**`ServingReadinessReport`** makes Karim's four-state distinction a machine-readable
value: ImplementationGreen / RuntimeStarted / BenchmarkMeasured / ProductionCertified.
T-137 attains **exactly one of four**.

---

# SECTION 3 — IDENTITY, TOPOLOGY AND ROADMAP

## 3.1 Repository topology as it now stands

```
C:\Workspace\PlantProcess-IQ\
  Backend\
    PlantProcessIQ.sln
    PlantProcess.Application\
      Assistant\
        (existing M1 surface - FROZEN, never touched this session)
        Planning\       <- T-179  2 files
        Retrieval\      <- T-180  5 files
        Verification\   <- T-181  5 files
        Serving\        <- T-137  4 files
      Dashboarding\     <- WORKER 2 ACTIVE. Never touch.
    PlantProcess.Api\, PlantProcess.Domain\, PlantProcess.Infrastructure\
    tests\
      PlantProcess.Application.UnitTests\Assistant\{Planning,Retrieval,Verification,Serving}\
      PlantProcess.Architecture.Tests\Assistant*IsolationTests.cs
  ML\
    pyproject.toml, requirements.lock, .gitattributes (* text=auto eol=lf)
    src\ppiq_ml\{artifacts,runtime,models,governance,explanation,similarity,
                 sequences,encoders,remediation}\
    tests\   (29 files)
  tools\packs\   <- UNTRACKED, generated. See W3-020.
```

**New .cs files are picked up by SDK-style globbing.** No `.csproj` or `.sln` edit was
needed for any of the four C# tasks. This is why the ownership boundary held with
Worker 2 live in the same projects.

## 3.2 The layer dependency direction (do not reverse)

```
sequences ──> encoders            (T-170 feeds T-172)
similarity ─> models/mf03_novelty (T-173 feeds T-174)
artifacts ──> models/mf04         (T-169 feeds T-175)
governance <── explanation        (explanation imports governance, never the reverse)

Planning ──> Retrieval ──> Verification
                       └─> Serving
```

**Why the governance direction matters:** if the promotion kernel imported an
explanation producer, a decision would depend on which library was installed.

## 3.3 Dependencies — the full lock

`ML/requirements.lock`:

| package | version | added by | notes |
|---|---|---|---|
| pyarrow | 25.0.1 | T-169 (prior) | |
| tzdata | 2026.3 | T-169 (prior) | |
| lightgbm | 4.7.0 | **T-175** | `py3-none-win_amd64` wheel, interpreter-independent |
| numpy | 2.4.4 | **T-175** | |
| torch | 2.13.0 | **T-172** | ~122 MB; Karim's machine reports `2.13.0+cpu` |

Declared but **not** locked: `scikit-learn`, `shap` — neither is used.

**Karim's environment:** Python 3.13.2 at `C:\Python313\python.exe`;
`torch.get_num_threads()` returns **14** where the container returns 1. The encoder
calls `torch.set_num_threads(1)` before allocating — **this pinning is load-bearing**
and is what makes reproducibility hold on a machine whose default differs.

Packages installed **user-scoped** to `%APPDATA%\Roaming\Python\Python313` because
system site-packages is not writable. **A Jenkins agent running as a different account
will not see them.** This is a real CI item and is recorded here, not fixed.

## 3.4 Roadmap position

Frozen Worker 3 SAFE-NOW execution order, with status:

```
T-169  Sealed typed artifact contract          CLOSED (prior session)  a887c554
T-175  MF-04 supervised runtime                CLOSED  2413c6e1
T-176  Promotion kernel + TreeSHAP             CLOSED  08b54b61 + 0a61ccfb
T-173  MF-02 similarity + exact oracle         CLOSED  00a14286
T-174  MF-03 novelty + honest refusal          CLOSED  db033796
T-170  Chunked sequence artifacts              CLOSED  5a216483
T-172  MF-01 process encoder (PyTorch)         CLOSED  d4321856
T-178  Remediation eligibility + can_accept    CLOSED  b8792516 + b5dbf232
T-179  Deterministic Assistant tool planner    CLOSED  d2bf1834
T-180  Permission-first retrieval + packer     CLOSED  87428c51
T-181  Answer verifier + Q-01..Q-11 harness    CLOSED  eaa82b8b
T-137  ModelServingRuntime                     CLOSED  56a2c37c
T-182  B-01..B-09 benchmark harness            NOT STARTED  <- NEXT
```

**Kernel/integration pairs that must NOT be collapsed:**
`T-169/T-184`, `T-170/T-185`, `T-168/T-187`, `T-177/T-146+T-147`,
`T-179+T-180+T-181+T-137/T-138`.

---

# SECTION 4 — REALIZATION SCOREBOARD

## 4.1 What is real

| Capability | State |
|---|---|
| Sealed typed columnar artifacts, two formats, two hashes | real, tested |
| C#↔Python job protocol with refusal semantics | real, tested |
| MF-01 process encoder (PyTorch) | real, tested, not promoted |
| MF-02 similarity index + exact oracle | real, tested |
| MF-03 novelty + honest refusal | real, tested |
| MF-04 supervised training + mandatory floor | real, tested |
| Three-dimensional promotion kernel | real, tested |
| TreeSHAP explanation evidence | real, tested |
| Chunked sequence artifacts + bounded loader | real, tested |
| Remediation eligibility + can_accept | real, tested |
| Assistant tool planner | real, tested |
| Permission-first retrieval + evidence packer | real, tested |
| Answer verifier + Q-01..Q-07 | real, tested |
| Model serving runtime + gateway adapter | real, tested |

## 4.2 What is NOT real — read this before making any claim

**Every item above is SAFE-NOW.** Specifically:

- **Nothing is registered in production DI.** Guards assert this in four C# tasks.
- **Nothing reads customer data.** No path exists from any of it to `ppiq_presentation`,
  `ppiq_app` or any feature store.
- **No threshold has been measured.** Every constant in every eligibility gate,
  promotion budget and recall floor is a **declared** value with a docstring saying so.
- **No model has been served.** T-137 has never contacted a real provider.
- **Q-08..Q-11 have never been measured** and correctly report as unavailable.
- **The product still cannot show a completed engine run on customer data.** This
  session did not move that.

The 27 July review's highest-risk finding — the Assistant answering a predictive
question it should have refused, and a rolling speed in kilograms — **is not fixed**.
The component that would catch both now exists (T-181), sitting *beside* the running
system. Closing the gap is **T-138's cutover**, not any task in this lane.

## 4.3 Session quality record

- Python tests: **81 → 503**
- C# Assistant probes: **0 → 169** (local combined harness)
- Commits: **14**, zero reverted, zero false-green, zero cross-lane contamination
- Dependencies added: **3**, each pinned in the task that used it
- My own defects found before reaching Karim's tree: **~20**
- My defects that reached his machine: **5 pack classes + 1 C# assertion class**
- **One fabricated commit hash** — see W3-027. The worst failure of the session.

## 4.4 Open findings

| ID | Class | Status |
|---|---|---|
| W3-020 | `tools/packs/backup/` and `trx/` are untracked generated output needing a `.gitignore` rule | VALID DEFERRED, do not act during this lane |
| W3-022 | MF-01 at `encoders/` while MF-03/MF-04 at `models/` | **RESOLVED — accepted topology** |
| W3-023 | Remediation precedence totalisation | **RESOLVED — corrective `b5dbf232`** |
| W3-024 | ~30 pre-existing compiler warnings incl. `CS8629` in `DashboardWidgetQueryService.cs:689` | Worker 2 lane, non-blocking |
| W3-025 | Pack tooling: bounded process execution + static pack linting | recorded, applied from T-179 v3 onward |
| W3-026 | T-180 pack's summary line says "T-179 probes total" | cosmetic, next pack |
| W3-027 | **Reporting-process debt: a hash appears only when Karim sent it** | standing rule |

---

# SECTION 5 — PER-TASK DISCOVERIES

Each entry: what was discovered while implementing, and what remains missing.

**T-175** — Discovered: LightGBM's `pred_contrib` exists (used later in T-176 P2). NaN
in metrics breaks .NET JSON parsing. Timings in a hashed record break determinism.
Missing: production SM-06 binding (owners: T-090/T-128/T-147).

**T-176 P1** — Discovered: `evidence_only` is the narrow case, not the broad one — the
lesson that later needed Karim's corrective in T-178. An unmeasured budget must make
the whole decision unevaluable.
Missing: real thresholds; nothing calls `decide()` outside tests.

**T-176 P2** — Discovered: the reconstruction identity (base + contributions = raw
output) is what makes explanation evidence uncheatable. `shap` is unnecessary.
Missing: attributions from a fitted T-175 model would require exposing the booster,
i.e. reopening T-175. Deliberately not done.

**T-173** — Discovered: nearest-rank percentile uses a **ceiling**, not a rounded
half-step (my code was wrong, the test was right). Tie-blind ranking is meaningless on
industrial data because sensors quantise.
Missing: FAISS as a second candidate — available, deliberately not wired.

**T-174** — Discovered: the median/MAD choice is not stylistic; the mean is moved by
exactly the units the model must find. A quantile threshold taken from the population
being scored answers "which units are most unusual", **never** "did something unusual
occur" — that needs a reference the units were not drawn from, which is production
drift work.

**T-170** — Discovered: three real design defects (chunk-size-dependent payload hash,
`read_manifest` hashing the whole file, compression ratio measuring the index). Two
invented assertion constants (`peak < payload/4`, `ratio < 0.5`) failed and were
replaced with falsifiable properties rather than relaxed.
Missing: `sequence_manifests` persistence is T-185.

**T-172** — Discovered: torch artifact byte hashes differ across processes while
embeddings and identities do not. `pyproject.toml` already declared the extra, but the
extra is not a lock.
Missing: encoder is optional and unpromoted; B-05 lift is measured by whatever consumes
the embeddings.

**T-178** — Discovered: the frozen precedence table does not cover all 512
combinations; my completion was wrong and Karim's corrective was required. Five of the
seven `can_accept` conditions refuse a decision that eligibility still calls actionable
— that is the concrete meaning of "cannot be reconstructed from fewer fields".
Missing: persistence and API are T-142..T-145.

**T-179** — Discovered: a request type with no field for question text is stronger than
any promise to ignore it. Two tools with identical capability are collapsed as
equivalent — a fixture that ignores this tests a plan the planner never produces.

**T-180** — Discovered: a type with no public constructor turns a discipline into a
compile-time guarantee. Permission leaks through counts, ordering, truncation and
fingerprints without a forbidden row ever being displayed.

**T-181** — Discovered: the ledger must not be permission for the prose to differ. A
citation that exists is not a citation that supports. A transport failure needs its own
verdict, not a rejection.
Missing: text inspection is deliberately narrow (numbers and handles only) and is
documented as narrow rather than presented as complete coverage.

**T-137** — Discovered: my harness was discarding async Tasks (see 1.6). Two mutations
were neutralised by C# precedence and by defence in depth.
Missing: everything except ImplementationGreen.

---

# SECTION 6 — EVERY TEST RUN AND ITS RESULT

**Do not re-run these. They are recorded so the new session does not pay for them again.**

## 6.1 Python suite progression (target machine, via pack gates)

| After | Tests | Result |
|---|---|---|
| baseline (T-169) | 81 | OK |
| T-175 | 141 | OK, 0 skipped |
| T-176 Pack 1 | 195 | OK, 0 skipped |
| T-176 Pack 2 | 230 | OK, 0 skipped |
| T-173 | 297 | OK, 0 skipped |
| T-174 | 340 | OK, 0 skipped |
| T-170 | 402 | OK, 0 skipped |
| T-172 | 448 | OK, 0 skipped |
| T-178 | 501 | OK, 0 skipped |
| T-178 corrective | **503** | OK, 0 skipped |

Container confirmation at end of session: **503 tests, OK**.

## 6.2 C# probe counts (target machine, via TRX)

| Task | Unit probes | Isolation probes | Total |
|---|---|---|---|
| T-179 | 24 | 8 | **32** |
| T-180 | 33 | 10 | **43** |
| T-181 | 49 | 9 | **58** |
| T-137 | 28 | 8 | **36** |

Local combined harness (all four, corrected runner): **169 probes, 169 passed**.

## 6.3 Key measured evidence (do not re-measure)

**T-175 four outcome kinds:**
```
binary      floor auc 0.5000 brier 0.2198  | candidate auc 0.9816 brier 0.0446
multiclass  floor log_loss 0.9646 acc 0.52 | candidate log_loss 0.6062 acc 0.80
ordinal     floor rank error 0.600         | candidate rank error 0.200
continuous  floor rmse 1.5921 r2 -0.0003   | candidate rmse 0.3887 r2 0.9404
```

**T-176 decisions:**
```
better discrimination, worse calibration -> challenger_rejected (quality)
unstable explanations                    -> challenger_rejected (quality)
encoder inside lift threshold            -> simpler_alternative_retained
clean challenger                         -> challenger_approved
excellent quality, failed serving        -> challenger_rejected (serving)
```

**T-176 P2 TreeSHAP:** max reconstruction error **4.441e-15**; stable runs
rank 1.0000 / topk 1.0000; unstable runs rank −0.5000 / topk 0.0000.

**T-173 recall (240 vectors, 24 cells, 60 queries, k=10, floor 0.90):**
```
probes 1  recall 0.6933  p95 0.154 ms  NOT ELIGIBLE (and the fastest build)
probes 3  recall 1.0000  p95 0.197 ms  eligible
probes 8  recall 1.0000  p95 0.289 ms  eligible
probes 24 recall 1.0000  p95 0.591 ms  eligible
```
Second population (165 vectors, dim 24, Euclidean): 10/1 → 0.9667; 10/3 → 1.0;
30/1 k=10 → 0.5958 NOT ELIGIBLE.

**T-174 novelty:** both families put all three planted units on top; baseline threshold
1.8596 flagging 4/93; candidate threshold 0.0096 flagging 4/93.
Refusals: 8 units → `too_few_reference_units` (30/8); 60 constant rows →
`degenerate_population` (1/0); 4 distinct → `too_few_distinct_units` (12/4).

**T-170 B-04 (8 channels, 24 000 steps, float64, 1 536 000 bytes):**
```
codec     chunk  chunks  stored      ratio    read s   peak read
stored    500    48      1 548 969   1.0000   0.189    304 609
stored    2000   12      1 539 725   1.0000   0.188    1 176 834
stored    6000   4       1 537 643   1.0000   0.202    3 511 962
deflate   500    48      1 381 649   1.1222   0.200    333 024
deflate   2000   12      1 336 479   1.1525   0.196    1 287 569
deflate   6000   4       1 327 034   1.1589   0.204    4 036 458
```
One payload identity across all six settings.

**T-172 encoder:** training input id `8f5633d9…`, artifact identity `b2e28571…`,
embedding dim 6, 24 windows, final loss 0.143437, artifact 9485 bytes,
encode p50/p95/p99 = 0.270 / 0.389 / 0.591 ms. Cross-process identical except byte hash.

**T-178 distribution over 512:** actionable 1, evidence_only 7, exploratory 248,
suppressed 256.

## 6.4 Mutation test results

| Task | Mutation | Caught by |
|---|---|---|
| T-179 | exact-value filter disabled | ProbeE |
| T-180 | permission check disabled | P1, P2, P3, **P4** (leak via Truncated + fingerprint) |
| T-180 | truncation never disclosed | all six budget probes |
| T-180 | duplicates not collapsed | E3, E4, collapse-is-not-truncation |
| T-181 | cited-value check disabled | V3 |
| T-181 | refusal preservation disabled | V7 ×2, Q-05 |
| T-181 | phrase policy disabled | V5, V6 ×3, Q-06, 2 harness gates |
| T-137 | response-identity check disabled | S2 ×2 (async — proved the runner fix) |
| T-137 | fallback approval removed (both layers) | S4 |

---

# SECTION 7 — KARIM'S RULES, RULINGS AND WAY OF THINKING

## 7.1 Authority order (learned the hard way)

```
Backlog v2.10.1  >  frozen architecture  >  current HEAD  >  executed tests  >  handover notes
```

**I got this wrong once**, treating a previous handover as if it outranked the backlog,
and built T-175 as a stdlib-only task when the backlog required a LightGBM candidate.
**Always read the authoritative backlog entry directly before writing anything.**

## 7.2 Standing rules

- **Read source documents directly.** Never rely on summaries or memory.
- **One task to full closure before the next.** No PARTIAL.
- **Name your own defects before Karim finds them.** Standing rule.
- **A refusal, gap or zero-result is a valid measured outcome**, never a failure to mask.
- **False PASS is never acceptable.** Plausible partial values are forbidden.
- **Never `git add .` or `git add -A`.** Exact-file staging only.
- **Packs stage nothing.** Staging and commit are a separate deliberate act.
- **Gates never judge on exit code alone** — parse TRX counts or output files.
- **Never pipe native stderr through PowerShell `ErrorActionPreference Stop`** — use
  `Start-Process` with file redirection.
- **Invented constants in assertions are not acceptable.** When an assertion constant
  has no basis, replace it with a falsifiable property, do not relax it.
- **Do not build reusable infrastructure inside a task** unless asked.
- **Dirty or conceptually outdated code is deleted or fixed, never built upon.**

## 7.3 How Karim communicates

- Rulings arrive in **fenced code blocks with decisions on their own lines**. Those
  blocks are the contract — implement them exactly.
- Options labelled **A/B** are preferred when a decision is needed.
- Technical content in English; conversational and directional content in **Arabic
  (Egyptian dialect)**. Respond in kind.
- He dislikes long governance documents. Improvements come as one-line amendments.
- He runs every pack himself and verifies manually before committing.
- **He checks claims against git.** The fabricated hash was caught within minutes.

## 7.4 Rulings issued this session — carry these forward

- **T-175:** LightGBM candidate is mandatory, not deferrable to T-176.
- **T-176:** T-176 owns calibration, explanation stability, TreeSHAP, three-dimensional
  promotion. Not the candidate.
- **T-176 P2:** a constant plus synthetic vectors proves the stability kernel, not the
  candidate. A real producer was required.
- **T-173:** FAISS is a candidate family, not the contract. Wheel availability must not
  block the task.
- **T-172:** PyTorch is **mandatory**. "The encoder is optional to promote/deploy, not
  optional to implement/test." Do not demand byte-identical serialized artifacts.
- **T-178:** the corrective precedence (Section 2.1). W3-023 was **not** deferrable.
- **T-180:** must consume the planner contract, never redesign it.
- **T-181:** do not claim Q-08..Q-11 measured.
- **T-137:** distinguish implementation green / runtime started / benchmark measured /
  production certified.
- **Terminology:** MF-01..MF-07 are seven **intelligence and engine families**, not
  seven ML models. MF-07 Practice Engine is later work under T-131..T-133/T-136. Do not
  collapse these.

## 7.5 Worker 3 SAFE-NOW constraints (must not do)

Register into production DI · create production DB migrations · modify presentation
schemas or data · replace the current analysis runtime · modify active presentation
routes or pages · modify the Assistant dock or runtime wiring · perform frontend
cutover · edit files in Worker 1 or Worker 2's active slice.

---

# SECTION 8 — BACKLOG STATUS

## 8.1 Closed this session

| Task | Title | Commit(s) | Files | Tests after |
|---|---|---|---|---|
| T-175 | MF-04 supervised runtime + mandatory baseline | `2413c6e1` | 17 (16+lock) | 141 |
| T-176 | Promotion kernel | `08b54b61` | 11 | 195 |
| T-176 | Real TreeSHAP candidate | `0a61ccfb` | 6 | 230 |
| T-173 | MF-02 similarity + exact oracle | `00a14286` | 9 | 297 |
| T-174 | MF-03 novelty + honest refusal | `db033796` | 9 | 340 |
| T-170 | Chunked sequence artifacts | `5a216483` | 9 | 402 |
| T-172 | MF-01 process encoder | `d4321856` | 9 (8+lock) | 448 |
| T-178 | Remediation eligibility + can_accept | `b8792516` | 8 | 501 |
| T-178 | Canonical precedence corrective | `b5dbf232` | 2 modified | 503 |
| T-179 | Deterministic tool planner | `d2bf1834` | 4 | 32 probes |
| T-180 | Permission-first retrieval + packer | `87428c51` | 7 | 43 probes |
| T-181 | Answer verifier + Q-01..Q-11 | `eaa82b8b` | 8 | 58 probes |
| T-137 | ModelServingRuntime | `56a2c37c` | 6 | 36 probes |

**All hashes above were sent by Karim in terminal output. None is inferred.**
The T-137 file/insertion counts were never seen (output truncated by
`Select-Object -First 3`) and are therefore **not recorded**.

## 8.2 Worker 2 commits observed in the same window

`4b431463` T-047 final · `56ade951` T-046-R1 · `f966719f` T-044-R1 ·
`dd9a6b04`/`39ce59ef`/`283aae2c`/`c56008c0` T-045-R1 ·
`a16b7b31`/`413df51a`/`124276dc`/`dc0b0e16`/`00745822`/`bd3dbb93` T-047 packs ·
`fc146483` T-046. **Informational only.**

## 8.3 Next

**T-182 — B-01..B-09 benchmark harness.** Not started. Await Karim's word.

---

# SECTION 9 — DEPLOYMENT, SERVER AND PIPELINE

## 9.1 I HAVE NO DATA. READ THIS CAREFULLY.

**This session touched nothing related to deployment, the server, or CI.**
Specifically, I did **not**:

- modify the `Jenkinsfile` or any pipeline configuration
- observe any Jenkins run, green or red
- touch the server, Caddy, Docker Compose, or any deploy script
- test or visit the app URL
- change any hosting, DNS or domain configuration

**Any statement in this section beyond the four facts below would be fabricated.**
The previous handover made the same declaration for the same reason, and it was right
to. A confident deployment section is the most damaging thing this document could
contain, because the next session would inherit it as fact.

## 9.2 The four things that are actually known

1. **A one-line `Jenkinsfile` hotfix exists from a previous session** at commit
   `ef588f0c` — it adds `apt-get install python3` and asserts 3.11, carrying a
   `PPIQ CI W3-016` message. Its text was confirmed present in the 13 August export.
   **No Jenkins run has been observed since.** It is not known whether it works.

2. **Python packages are installed user-scoped** on Karim's machine
   (`%APPDATA%\Roaming\Python\Python313`) because system site-packages is not writable.
   **A CI agent running as a different account will not see lightgbm, numpy or torch.**
   This is a genuine and unaddressed CI risk.

3. **`torch` is ~122 MB.** Any CI agent that must install it will pay that cost. No
   caching strategy has been designed or tested.

4. **`W3-024`:** the solution builds with ~30 compiler warnings including `CS8629` in
   `DashboardWidgetQueryService.cs:689`. Pre-existing, Worker 2's lane, non-blocking.

## 9.3 What must be done, by someone with access

Before T-138 or any cutover, someone must actually run the pipeline and report:
whether the agent can install the three pinned packages; whether the 503-test Python
suite runs in CI; whether the four Assistant test filters run; and whether the app URL
serves. **None of that is known today.**

---

# SECTION 10 — MODIFICATIONS TO MAKE THE PIPELINE GREEN / THE APP URL WORK

## 10.1 None. Zero. Nothing.

**No modification was made this session to make any pipeline green or any URL work.**

I make **no claim** that the pipeline is green. I make **no claim** that the app URL
works. Neither was observed, tested or touched.

The only pipeline-adjacent change in the whole lane's history is the one-line
`Jenkinsfile` hotfix at `ef588f0c` from a **previous** session, described in 9.2, whose
effect has never been observed.

If the next session is asked about deployment status, the correct answer is: *"no
deployment knowledge exists in the Worker 3 lane; ask whoever owns the server."*

---

# SECTION 11 — THE LAST POINT REACHED, AND WHERE TO START

## 11.1 Exact stopping point

T-137 committed at **`56a2c37c`**. `git diff --cached --name-status` empty — index clean.
Nothing in flight. No pack awaiting a run. No open question except the T-137 file and
insertion counts, which were never captured.

Karim's last complete instruction was to hold T-182 pending his word.

## 11.2 The keyword to open the new session

> "Worker 3, ML and Assistant lane. T-137 closed at `56a2c37c`. Read the handover in
> full before anything. Do not re-run the 503 Python tests or the 169 C# probes. Next
> task is T-182, B-01..B-09 benchmark harness — do not start it until I say so."

## 11.3 First three actions in the new session

1. **Confirm the role** — Worker 3, ML/Engine lane.
2. **Read this document in full.** Then read the frozen backlog entry for T-182
   **directly** from `PPIQ_Backlog_v2_10_1_12Aug2026.md`. Do not rely on this document
   for the task contract — it is a summary, and Section 7.1 explains what happens when
   a summary is treated as authoritative.
3. **`git status` and `git log --oneline -20`** to confirm the tree matches Section 8.

## 11.4 What T-182 will most likely require

B-01..B-09 is the common benchmark framework. Hooks already exist and must be
**consumed, not redesigned**:

- **B-03** — `ML/src/ppiq_ml/artifacts/b03.py` (T-169)
- **B-04** — `ML/src/ppiq_ml/sequences/b04.py` (T-170)
- **B-05** — `ML/src/ppiq_ml/encoders/b05.py` (T-172)
- **B-07 / B-08** — `Backend/.../Assistant/Retrieval/RetrievalBenchmarkHooks.cs` (T-180)

Each produces a measurement record with **no verdict field**, by design. T-182 must
preserve that: measuring and deciding are separate, and promotion belongs to T-176.

**Expect an honesty problem.** Any benchmark requiring a running serving runtime cannot
be measured in an isolated build. T-181 solved this with `CapabilityUnavailable` plus
unit, aggregation, reason and owner, and **no value**. T-137 solved it with a
four-state readiness record. Do the same. Do not fill a gate with a plausible number.

## 11.5 The sentence to keep in view

The engine is real and is connected to nothing. Twelve tasks of genuine capability
exist beside a running product that cannot yet use any of it. That is the correct state
for a SAFE-NOW lane — and it stays correct only as long as every report says so
plainly instead of letting a green suite imply a running system.

---

*End of handover. Written 16 August 2026 at the close of the Worker 3 ML and Assistant
session. Every commit hash in this document was sent by Karim in terminal output; none
is inferred. Sections 9 and 10 declare an absence of knowledge rather than describing
one.*
