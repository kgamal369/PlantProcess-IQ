# T-168 CLOSURE EVIDENCE

**Task:** T-168 Versioned C# to Python ML job protocol and isolated runtime harness
**Worker:** 3, SAFE-NOW lane
**Date:** 12 August 2026

---

## 1. Task ID and exact frozen requirement

**T-168.** Build a versioned execution contract between the .NET control plane and the Python compute plane. Governance and orchestration remain in .NET; ML numerical computation may run in the replaceable Python ML runtime. The job specification carries identity and context. Python returns a structured versioned result manifest.

**The rule that governs the whole task:** stdout and stderr are diagnostics. **The structured result manifest is authority.** C# must never scrape arbitrary Python console text and treat it as truth.

**Required falsification cases:** success, honest structured refusal, Python crash, timeout, cancellation, malformed result manifest, missing result manifest, protocol-version mismatch, checkpoint and resume, deterministic repeatability, and console output claiming success without a valid manifest.

**Prohibited:** database access, production DI registration, Docker, Jenkins, presentation wiring.

---

## 2. Files created and modified

**Pack 1, the Python runtime project.** 15 additions.

```
ML/.gitattributes                         ML/.gitignore
ML/README.md                              ML/pyproject.toml
ML/requirements.lock                      ML/src/ppiq_ml/__init__.py
ML/src/ppiq_ml/runtime/__init__.py        ML/src/ppiq_ml/runtime/protocol.py
ML/src/ppiq_ml/runtime/job_spec.py        ML/src/ppiq_ml/runtime/result_manifest.py
ML/src/ppiq_ml/runtime/checkpoint.py      ML/src/ppiq_ml/runtime/runner.py
ML/tests/test_protocol_contract.py        ML/tests/test_runner_behaviour.py
ML/tests/test_isolation.py
```

**Pack 2, the C# protocol project and solution entry.** 7 additions, 1 modification.

```
Backend/PlantProcess.ML.Runtime/PlantProcess.ML.Runtime.csproj
Backend/PlantProcess.ML.Runtime/MlJobProtocol.cs
Backend/PlantProcess.ML.Runtime/JobSpec.cs
Backend/PlantProcess.ML.Runtime/ResultManifest.cs
Backend/tests/PlantProcess.ML.Runtime.Tests/PlantProcess.ML.Runtime.Tests.csproj
Backend/tests/PlantProcess.ML.Runtime.Tests/ProtocolContractTests.cs
Backend/tests/PlantProcess.ML.Runtime.Tests/CrossLanguageCompatibilityTests.cs
Backend/PlantProcessIQ.sln                                            modified
```

**Pack 3, real cross-language execution.** 6 additions, 1 modification.

```
ML/src/ppiq_ml/runtime/cli.py
ML/tests/handlers/__init__.py
ML/tests/handlers/fixture_handlers.py
ML/tests/test_isolation.py                                            modified
Backend/PlantProcess.ML.Runtime/PythonJobRunner.cs
Backend/tests/PlantProcess.ML.Runtime.Tests/PythonEnvironment.cs
Backend/tests/PlantProcess.ML.Runtime.Tests/EndToEndProtocolTests.cs
```

---

## 3. Proof that no ownership boundary was crossed

Every pack read `git status` before applying and refused if an owned file was already dirty. Between eleven and eighteen dirty paths belonging to others were present at each run, across `Documentation/docs/`, `tools/run/`, `Backend/PlantProcess.Domain/` and `Frontend/PlantProcess.Web/`. **None was staged, reset, checked out or restored.**

| Prohibition | Evidence |
|---|---|
| No database access | Python isolation test fails the build on any import of psycopg, sqlalchemy, asyncpg, pyodbc, sqlite3 or a network library. The pack self-check scans `PythonJobRunner.cs` for Npgsql, DbContext, SELECT and ppiq_plant |
| No production DI | The self-check scans for `AddScoped` and `IServiceCollection`. Nothing is registered anywhere |
| No Docker | No Dockerfile or docker-compose file touched by any pack |
| No Jenkins | No Jenkinsfile touched |
| No presentation wiring | No route, page, component or seed touched |
| Solution edit | `dotnet sln add` only, never text surgery. Refused if the solution was dirty, hashed before and after, restored and hash-verified on failure |

---

## 4. Implementation summary

### The two planes

```
C# control plane                          Python compute plane
  JobSpec.json  ------------------------>  cli.py reads and validates
  waits, bounded by the declared budget    handler computes
  reads result_manifest.json  <----------  runner.py always writes a manifest
  decides
```

**Communication is job-based, not chatty REST.** One process, one specification, one manifest.

### Job outcome and analysis outcome are different axes

`outcome` is `succeeded, refused, failed, cancelled, timed_out`. `analysis_terminal_state` carries the Layer B state. **A job can succeed while the analysis it ran honestly refuses to produce a finding.** Collapsing these would make an honest refusal look like a job failure, and the two need opposite operational responses.

### A crash is failed, never refused

A refusal is a governed decision with a code and a sentence. An unhandled exception is a bug. Conflating them would let a defect read as an honest decision.

### The manifest is the only authority

`PythonJobRunner` decides the outcome from the manifest alone. Exit code, stdout and stderr are carried for diagnosis. A process that exits zero with no manifest is `Failed`. A process killed on timeout is `TimedOut`, because a terminated process cannot report on itself. A manifest bearing a different job id is not evidence about this execution.

### Python never decides

The runtime reports artifact, metrics, calibration, latency, hashes, warnings and terminal state. **Whether a model becomes production champion is a .NET governance decision**, and nothing in `ML/` can make it.

---

## 5. Tests executed, with counts

Read from TRX counters and from parsed unittest output. Never from an exit code.

```
Python       total=34  passed=34  failed=0  skipped=0
Protocol     total=35  passed=35  failed=0  skipped=0
Analytics    total=84  passed=84  failed=0  skipped=0
                       153 tests, 0 failed, 0 skipped
```

The whole solution builds with both new projects registered.

---

## 6. The eleven required falsification cases

Every one drives an actual Python process and is judged by the manifest.

| # | Case | Result |
|---|---|---|
| 1 | Successful execution | `Succeeded`. Metrics, artifacts, seed and code identity survive the boundary |
| 2 | Honest structured refusal | `Refused` with `EligibilityNotMet` and its sentence. Asserted to be **neither failure nor success** |
| 3 | Python process crash | `Failed` with `ZeroDivisionError` in the reason, and **never `Refused`** |
| 4 | Timeout | 2 second budget against a 600 second sleep. Process killed, **no manifest written**, this side records the timeout |
| 5 | Cancellation | C# creates the cancellation file mid-run; Python returns `cancelled` |
| 6 | Malformed result manifest | A truncated write is refused with a named code, not partially trusted |
| 7 | Missing result manifest | Absent manifest is `Failed` whatever the exit code said |
| 8 | Protocol-version mismatch | `ppiq.mljob/99` refused **before the payload is interpreted**, and Python still records why |
| 9 | Checkpoint and resume | Run 1 reaches stage 1 with no resume. Run 2 reaches stage 2 and reports `resumed_from_checkpoint = stage-1` |
| 10 | Deterministic repeatability | Two runs agree on outcome, metrics, seed, artifact hash and runtime version |
| 11 | Console claiming success | Prints `SUCCESS model trained, auc 0.99, promoted to champion` on stdout and `INFO all gates passed` on stderr, then fails. **Manifest says `Failed`, metrics empty** |

Plus a twelfth: a manifest bearing a different job id is rejected as evidence about this execution.

---

## 7. Known-answer and falsification evidence

**Cross-language fixtures are real bytes, not hand-written approximations.** Six C# tests parse JSON produced by executing the Python runtime and captured verbatim. If either side gains or renames a field, a test fails rather than a production job.

One test walks every property of a Python-emitted job spec and asserts the C# writer emits it, so the two schemas cannot drift apart silently.

Every enum value round-trips through its wire form in both directions.

---

## 8. Proof of honest refusal handling

Eight execution-side refusal codes, distinct from statistical-method reasons and from capability shortfalls. A refusal must carry a code and a sentence; a success must carry neither. Both directions are asserted.

The CLI writes a refusal manifest even when the job specification itself cannot be interpreted, using a best-effort read of the output directory, so a protocol mismatch still leaves evidence.

---

## 9. Determinism evidence

Two identical executions agree on outcome, refusal code, metrics to twelve places, seed, code identity, artifact content hash and runtime version. Timing fields are excluded by design. The runtime holds no clock-dependent logic beyond timestamps and no RNG in the protocol layer.

---

## 10. Benchmark output

T-168 owns no B-01 to B-09 benchmark. None claimed.

---

## 11. git status before commit

Each pack printed the full working tree before and after. Other-worker paths were present and untouched at every run.

---

## 12. Staged file inventory

Pack 1: 15 additions. Pack 2: 7 additions and 1 modification. Pack 3: 6 additions and 1 modification.

Staged with `git add -- <exact path>` per file. Never `git add .`, never `git add -f`.

**From Pack 2 onward the apply packs stage nothing.** The commit guard stages and commits in one run, and refuses if the index is already non-empty. See section 14, finding W3-015.

---

## 13. Commit hashes

| Pack | Contents | Commit |
|---|---|---|
| Pack 1 | ML Python runtime project, 15 files, 34 tests | 16ea041aa62bfc3ac5161cd7b3179eee278d99c3 |
| Pack 2 | C# protocol project, cross-language tests, solution entry | 94ab4e40c059a0b3e309a52db311a8ba70e953fa |
| Pack 3 | Real cross-language execution, eleven falsification cases | 723b94345ca01b3c54a820b8c2fb2e80a328ecad |

Where a commit above carries the message of another lane task, its files are correct
and complete in that commit and only the attribution is wrong. History was
deliberately not rewritten. See finding W3-015.

## 14. Remaining findings and dependencies

### W3-015 - cross-lane provenance anomaly, recorded not corrected

**Pack 1's fifteen files were committed inside another lane's commit.**

The apply pack staged the files; the commit guard then refused on an unrelated defect of its own, leaving them staged. Another worker's `git commit` ran before mine and took everything in the index, because `git commit` commits the index rather than only what the committer added.

**The content is correct.** All fifteen files are in HEAD, the runtime negation is present, all six runtime sources are there, and a fresh-clone simulation confirmed a new clone gets a working project. Only the attribution is wrong.

**History was deliberately not rewritten.** The carrying commit is on a shared branch; rewriting it to correct a message would be materially more dangerous than the wrong message.

**The structural cause was one-directional protection.** `git add -- <path>` controls what this lane adds. It does not protect what this lane has staged from another lane's commit. The two-step workflow, apply then commit, held that window open.

**Fixed from Pack 2 onward:** apply packs stage nothing, and the commit guard stages and commits in one run. The guard also refuses when the index is already non-empty, which protects the other lanes from the reverse case.

**The general fix is cross-lane and is not this lane's to make.** Every worker's commit would need to be pathspec-scoped, or every commit guard would need to verify the staged set before committing.

### W3-016 - the build agent now requires Python, Class D

`Backend/tests/PlantProcess.ML.Runtime.Tests` executes a real Python process. Jenkins runs `dotnet test Backend`, so **an agent without Python 3.11 or newer will fail these tests and block the build for every lane.**

This is inherent to T-168: proving a C# to Python boundary requires both runtimes. **The agent image change belongs to T-122 and T-187.** No Jenkinsfile or Dockerfile was touched.

The tests **fail rather than skip** when Python is absent, with a sentence naming the dependency, because a skipped test proves nothing.

### W3-017 - the root .gitignore swallowed the package directory

The repository root `.gitignore` carries an unanchored hygiene rule, `runtime/`, beside `logs/` and `*.pid`. It matches a `runtime` directory at any depth and silently excluded `ML/src/ppiq_ml/runtime`.

Resolved by a negation in `ML/.gitignore`, this lane's own file, verified against real git before shipping. **`git add -f` was deliberately not used**, because forcing past an ignore rule hides a collision instead of resolving it. The root file was not modified.

A `git check-ignore` gate now runs before staging in every pack.

### Pack machinery defects, all self-reported

| # | Defect | Caught by |
|---|---|---|
| W3-011 | Native stderr piped under `ErrorActionPreference Stop` raised a terminating error on correct output | The failure itself |
| W3-012 | Six files silently skipped by the ignore rule | The staged-count guard |
| W3-013 | The ownership check read this lane's own residue as a foreign change | The ownership guard |
| W3-014 | `-notmatch` against a string array refused a correct file | The commit guard |

**Three of the four are PowerShell semantics.** Standing rules adopted: any command output used in a scalar comparison is passed through `Out-String` first; no gate is judged on an exit code; no native stderr is piped through the error channel; and every check prints its passing case rather than asserting it.

### Carried forward

**Class D**, `tools/packs/` accumulates `backup/` and `trx/` and needs a `.gitignore` entry. `.gitignore` is shared and was not touched.

**Class D**, the canonical SM-06 binding remains a later integration dependency.

---

## Definition of Done checklist

| # | Item | State |
|---|---|---|
| 1 | Task ID and frozen requirement | Section 1 |
| 2 | Files created and modified | Section 2 |
| 3 | No ownership boundary crossed | Section 3 |
| 4 | Implementation summary | Section 4 |
| 5 | Tests executed, not enumerated | Section 5, 153 tests |
| 6 | Exact important results | Section 6, all eleven cases |
| 7 | Known-answer and falsification evidence | Section 7 |
| 8 | Honest refusal handling proven | Section 8 |
| 9 | Determinism evidence | Section 9 |
| 10 | Benchmark output | Section 10, none owned |
| 11 | git status before commit | Section 11 |
| 12 | Staged file inventory | Section 12 |
| 13 | Commit hashes | Section 13 |
| 14 | Remaining findings and dependencies | Section 14 |

---

*T-168 closure evidence, 12 August 2026. Seven findings raised, all self-reported.*
