# PPIQ RULE - WORKER 3 IMPLEMENTATION MANDATE

**Status: BINDING RULE**
**Ruled by Karim - 12 August 2026**
**Governs: PlantProcess IQ Engine + AI + ML + LLM implementation, including the related backend and frontend work assigned to the Worker 3 lane**
**Backlog authority: `PPIQ_Backlog_v2_10_0_12Aug2026.xlsx` and `.md`**

> **ROLE CHANGE.** Worker 3 moves from architecture design into implementation. The AI/ML/LLM architecture is **FROZEN**. Do not start another architecture redesign cycle unless implementation evidence exposes a concrete contradiction or defect.

**The objective chain from now on:**

```
frozen architecture -> isolated implementation -> canonical integration
  -> runtime evidence -> benchmarks -> production acceptance
```

---

## 1. IMMEDIATE OBJECTIVE

Worker 3 does **not** wait for Worker 1, Worker 2 or M2a before starting. A dedicated **SAFE-NOW** lane exists so the real Engine, AI, ML and LLM foundations can be built without interfering with the presentation target.

### SAFE-NOW: what is permitted

**MAY** create new isolated modules, contracts, runtimes, kernels, libraries, test fixtures, benchmark harnesses and deterministic validation.

### SAFE-NOW: what is forbidden

**MUST NOT**:

- register anything into the current production DI graph
- create production database migrations
- modify presentation schemas or data
- replace the current analysis runtime
- modify active presentation routes or pages
- modify the current Assistant dock or runtime wiring
- perform frontend cutover

Worker 1 and Worker 2 are still protecting and completing the presentation lane. **Some presentation backing data and intelligence is intentionally temporary** and will converge later through M2a. **Do not "fix" those temporary areas from this lane.**

---

## 2. T-177 FIRST - PRODUCTION STATISTICAL-METHOD KERNEL

**Do not start with the neural encoder, LLM integration or production database schema.** T-177 is deliberately first: it gives real Engine capability immediately and has almost no dependency on Worker 1, Worker 2 or the future canonical database.

Build a **schema-independent statistical kernel**.

**Preserve the proven pairings:**

- Numeric x Numeric
- Binary x Numeric
- Categorical x Categorical

**Add:**

- **Numeric x Categorical**, using assumption-aware **one-way ANOVA** with a **Kruskal-Wallis fallback** when parametric assumptions are not supported

**The result contract must expose the real analytical evidence:** method used, aligned population, group sizes, effect size, p-value, q-value or FDR, and an explicit terminal or exclusion reason.

### The exclusion taxonomy must never collapse

These three are distinct and must never share a refusal reason:

| Reason | Meaning |
|---|---|
| **constant / zero variance** | A measured property of the data |
| **unsupported method / pairing** | A limitation of the product, never blamed on the data |
| **insufficient sample / groups** | A measured population shortfall, reported with the number |

**Validation uses known-answer deterministic fixtures.** Deliberately violate ANOVA assumptions and **prove the method switches correctly to Kruskal-Wallis**.

> **Critical boundary.** T-177 is the kernel only. **Do not wire it into the current presentation correlation engine.** T-146 and T-147 production convergence come later, after the relevant Worker 2 / M1 hand-off.

---

## 3. SAFE-NOW EXECUTION ORDER

| # | Task | Scope |
|---|---|---|
| 01 | **T-177** | Production statistical-method kernel |
| 02 | **T-171** | Capability Profiler + eligibility/refusal kernel |
| 03 | **T-168** | Versioned C# to Python ML job protocol and runtime harness |
| 04 | **T-169** | Typed columnar training-artifact library + B-03 harness |
| 05 | **T-175** | MF-04 supervised-outcome runtime + mandatory simple baseline |
| 06 | **T-176** | Calibration, explanation stability + three-dimensional promotion |
| 07 | **T-173** | MF-02 VectorSimilarityIndex + Exact-Flat recall baseline |
| 08 | **T-174** | MF-03 novelty runtime + honest refusal |
| 09 | **T-170** | Chunked sequence artifact + bounded loader |
| 10 | **T-172** | MF-01 Process Encoder behind a replaceable contract |
| 11 | **T-178** | Remediation eligibility + `can_accept` decision kernel |
| 12 | **T-179** | Deterministic Assistant Tool Planner |
| 13 | **T-180** | Permission-first hybrid retrieval + evidence packer |
| 14 | **T-181** | Deterministic answer verifier + Q-01 to Q-11 harness |
| 15 | **T-137** | ModelServingRuntime + model-gateway adapter |
| 16 | **T-182** | B-01 to B-09 benchmark harness and result manifest |

**One task to closure before moving to the next**, unless there is a genuine technical reason to split a dependency.

**Do not casually jump between tasks and leave several 90 percent complete.** PPIQ has a **no-PARTIAL rule**. If something cannot close, identify the exact remainder rather than claiming Done.

---

## 4. T-171 - CAPABILITY PROFILER

Industry-neutral. It determines what the available data can actually support: statistics, similarity, novelty, supervised prediction, practice learning and remediation.

It evaluates measured facts: population sufficiency, label and outcome availability, genealogy strength, context dimensions and **collapsed dimensions**.

- **A dimension with one effective level is not an error.** It is removed from the eligible analytical dimensions.
- **Missing support is not a fake 0.**
- If a method is not applicable, return a truthful terminal state such as `NOT_APPLICABLE`.

**No database access and no presentation wiring in this task.**

---

## 5. THE C# TO PYTHON BOUNDARY IS DECIDED

T-168 does **not** ask whether PPIQ should use ML.NET or Python. **That design question is closed.**

- **Governance and orchestration remain in .NET.**
- **ML numerical computation may run in the replaceable Python ML runtime.**

Build a **versioned execution contract**. The job specification carries identity and context: tenant, site, job, model family, artifact and snapshot identity, semantic-manifest handle where available, seed, code identity, resources, cancellation and checkpoint information, and output location.

**Python returns a structured versioned result manifest.**

> **Do not make C# scrape arbitrary Python console text and treat it as truth.**
>
> **stdout and stderr are diagnostics. The structured result manifest is authority.**

**Test at least:** success, honest refusal, crash, timeout, cancellation, checkpoint and resume, and deterministic repeatability.

**Do not register this into the current scheduler or production DI.**

---

## 6. TRAINING DATA LAW

**Never design ML training to read millions of live JSONB feature rows directly as its training path.**

`feature_store` is governed current state. Production training must ultimately use:

```
feature_store -> Snapshot Materialiser -> sealed immutable typed artifact -> trainer
```

- **T-169** builds the schema-independent typed columnar artifact abstraction **now**.
- **T-184** integrates the production Snapshot Materialiser **later**, after the canonical M2a authority exists.

Parquet, Arrow and equivalents are **implementation candidates behind the contract**. Do not make a storage library the product architecture.

**Training must never bypass the sealed snapshot once G-48 is active.**

---

## 7. SEQUENCE DATA LAW

**Do not store large sequence or time-series numeric payloads as PostgreSQL arrays.**

**T-170** builds: immutable chunked typed arrays, a chunk index, hashes, a compression seam, and bounded streaming or memory-mapped reads where supported.

**T-185** later persists only the sequence manifest and metadata in PostgreSQL; numeric payloads stay in artifact or object storage.

- **A large sequence must be readable without loading the complete payload into RAM.**
- **Missing or corrupted chunks must produce a typed failure or refusal, never silent partial data.**

---

## 8. ML FAMILY RULES

**The seven MF families are not seven mandatory ML models. They are intelligence and engine families.**

For learned models: **simple baseline first. A more complex model must earn promotion.**

| Family | Rule |
|---|---|
| **MF-04** | Must include a simple baseline before LightGBM or another challenger |
| **MF-01 Process Encoder** | **Optional.** Training successfully does not mean it deserves production |
| **MF-02** | Uses the product contract `VectorSimilarityIndex`. FAISS, HNSW, IVF, PQ are **implementation candidates, not the product contract**. **Exact Flat is the permanent recall reference baseline.** An ANN candidate failing the recall floor **is not allowed to serve regardless of how fast it is** |
| **MF-03 Novelty** | Baseline-first and honest-refusal semantics |

---

## 9. MODEL PROMOTION IS THREE-DIMENSIONAL

**Never promote a model only because one ML metric improved.** T-176 judges at least:

**QUALITY** - discrimination or error, calibration, out-of-time performance, subgroup and regime stability, missingness robustness, explanation stability.

**SERVING** - p50, p95, p99, throughput, artifact size, RAM and VRAM, warm-up.

**TRAINING** - duration, peak memory, snapshot throughput.

- **A better discrimination score with materially worse calibration can be rejected.**
- **An explanation-unstable model can be rejected.**
- **A costly encoder can lose to a simple baseline** when the measured lift does not justify its operating cost.

**Promotion decisions must be deterministic and reproducible from a frozen metrics document.**

---

## 10. ASSISTANT AND LLM LAW - DO NOT CHANGE

> **THE LLM DOES NOT CHOOSE TOOLS.**

- Do not introduce an LLM-generated ToolPlan.
- Do not introduce a second architecture such as ClaimPlan or ClaimEnvelope.
- **T-179 owns a deterministic Assistant Tool Planner.**

**Pipeline direction:**

```
permission / tenant context
  -> intent / entity resolution
  -> DETERMINISTIC TOOL PLANNER
  -> structured Layer A / Layer B tools
  -> permission-scoped retrieval
  -> evidence packing / context budget
  -> ModelServingRuntime
  -> LLM
  -> deterministic answer verification
  -> cited response or refusal
```

- **Facts prefer structured tools.** Documents and evidence may use retrieval.
- **Permission filtering occurs before ranking and before LLM exposure.**
- The LLM does not calculate plant truth.
- The LLM does not replace an Engine refusal.
- The LLM does not convert correlation into causation.
- The LLM does not upgrade a claim class.
- **Numeric claims require evidence.**
- **Verification is deterministic and non-LLM.**

---

## 11. MODELSERVINGRUNTIME LAW

`ModelServingRuntime` must be **replaceable**. vLLM or another serving engine may be evaluated as an implementation candidate; **none is PPIQ's product contract**.

Self-hosted, private and BYOM deployment must remain possible. **Provider and model changes are governed configuration decisions.** Only the **minimum scoped evidence and context** needed for the request crosses the model boundary.

**Do not wire T-137 into the current presentation Assistant dock while it is owned by the M1 lane.**

---

## 12. REMEDIATION AND PLANT-CONTROL LAW

**PPIQ is advisory.** Never build an automatic customer-source, PLC or MES control-write path as part of this work.

**T-178 is a pure decision kernel.** A remediation candidate may become `actionable`, `evidence_only`, `exploratory` or `suppressed`. **Safety or eligibility failure must suppress the recommendation where required.**

Accept, Reject and Defer remain a **human decision boundary**. **No autonomous plant write-back.**

---

## 13. DATABASE RULES WHILE IN SAFE-NOW

- **DO NOT** create production AI/ML tables.
- **DO NOT** bind new code to `ppiq_presentation`.
- **DO NOT** make the presentation schema a permanent API.
- **DO NOT** make temporary M1 tables a dependency of the new engine.

M2a-P1 and M2a-P2 finalise the canonical schema, definition store and relationship model. **After that hand-off, T-183 and the canonical persistence tasks begin.**

The **Semantic Contract Manifest** uses **tenant-safe identity**; the architecture requires tenant-aware manifest identity rather than a globally unique content hash alone. The manifest is **immutable publication identity and reproducibility authority**.

**Do not re-create the retired competing `SemanticModelVersion` lifecycle authority.**

---

## 14. KERNEL IS NOT INTEGRATION

**This separation is deliberate. Do not collapse the pairs because "integration is easy".**

| Now | Later |
|---|---|
| **T-169** artifact library | **T-184** Snapshot Materialiser production integration |
| **T-170** sequence artifact library | **T-185** sequence manifest and object-storage integration |
| **T-168** isolated C# to Python runtime | **T-187** production scheduler, lane and model-registry integration |
| **T-177** statistical kernel | **T-146 / T-147** production correlation convergence |
| **T-179, T-180, T-181, T-137** Assistant kernels and runtime | **T-138** canonical Assistant cutover |

---

## 15. DO NOT INTERFERE WITH WORKER 1 OR WORKER 2

Until the relevant hand-off is explicitly reached, **do not modify**:

- current M1 presentation routes or pages
- the current presentation database or seeds
- T-045 and T-046 implementation or runtime wiring
- the active Assistant dock integration and DI
- presentation-specific adapters currently keeping the demo alive
- M2a final schema, relationship and definition-store work owned by other lanes

**If a defect is discovered in one of those areas: record the finding and the exact evidence. Do not silently fix their owned file.**

If a task requires changing their file: **stop the integration part**, finish anything still possible inside the isolated contract, and **report the dependency and required hand-off**.

---

## 16. REPOSITORY ISOLATION IS MANDATORY

Multiple workers modify the same repository.

**Before beginning every task:** inspect `git status`. Know which dirty paths belong to somebody else.

**During work:** stage exact owned files only.

**Never use `git add .`. Never use `git add -A`.**

**Never reset, checkout, restore or overwrite another worker's dirty path.**

**Before committing:** inspect `git status` and `git diff --cached`. **The staged set must contain only files owned by the task.** The backlog makes this a permanent parallel-worker law.

---

## 17. NON-NEGOTIABLE ARCHITECTURE AND PRODUCT RULES

- PPIQ must remain **industry-generic**. No steel or customer-specific core ML logic.
- **Customer systems remain read-only.**
- **Layer A** owns deterministic exact BI facts. **Layer B** owns statistical and learned intelligence.
- The LLM explains, retrieves and cites; **it does not become the analytical engine**.
- **Refusal is a valid product result.**
- **Source-code presence is not proof that runtime wiring works.**
- **Row count and population are not provenance.**
- **Tenant isolation is absolute.** Every tenant-owned uniqueness rule includes tenant identity correctly.
- Every production result must be **reproducible from governed inputs and version identities**.
- **No seeded or fabricated intelligence presented as real.**
- **No automatic retraining** just because more data arrived. **No online weight updates.**
- **No multi-agent architecture.**
- **No GNN, RL or autonomous plant control** unless separately justified and approved in a future architecture revision.

---

## 18. BENCHMARKS ARE MEASUREMENTS, NOT ARCHITECTURE DISCUSSION

**B-01 to B-09 are intentionally benchmark-open.** T-182 creates the common benchmark and result-manifest harness now.

- **Do not guess the final values.**
- **Do not hardcode thresholds because they look reasonable.**
- Measure them later against the correct environment and data.

They cover resource capacity, online isolation, snapshot throughput, sequence settings, encoder value, ANN recall and performance, retrieval quality and model-serving performance.

> **A benchmark result can tell us one candidate loses. It does not reopen the whole architecture.**

---

## 19. DEFINITION OF DONE FOR EVERY WORKER-3 TASK

**A task is not Done because the code compiles.** Before asking for closure, provide:

1. Task ID and the exact frozen requirement
2. Files created and modified
3. Why no Worker 1 or Worker 2 ownership boundary was crossed
4. Implementation summary
5. **Tests executed - actual execution, not test enumeration**
6. Exact important results
7. Known-answer and falsification evidence where applicable
8. Proof of honest refusal and error handling
9. Determinism and reproducibility evidence where applicable
10. Resource or benchmark output when the task owns one
11. `git status` before commit
12. `git diff --cached --stat` and the staged file inventory
13. Commit hash
14. Any remaining finding or dependency

**Do not mark a task Done while an acceptance condition is still only assumed.**

---

## 20. WHEN SOMETHING UNEXPECTED IS DISCOVERED

Classify **before** changing scope:

| Class | Meaning | Action |
|---|---|---|
| **A** | Implementation defect | Fix it if it is inside the owned task |
| **B** | Frozen-design clarification | Implement the least-surprising interpretation consistent with Revision 7 / 7.1 and **record the clarification** |
| **C** | Genuine architecture contradiction | **Stop only the affected slice** and provide concrete evidence. Do not redesign the platform |
| **D** | Dependency owned by Worker 1, Worker 2 or M2a | Record it and continue another SAFE-NOW task rather than modifying their lane |

> **Do not use an implementation inconvenience as justification to invent a new architecture.**

---

## 21. FIRST DELIVERY - T-177

Before writing production integration code:

1. Inspect the existing statistical implementations and tests
2. Identify the proven behaviour that must be preserved
3. Establish the isolated new-module and file boundary
4. Build known-answer fixtures first or alongside the kernel
5. Implement Numeric x Categorical ANOVA and Kruskal-Wallis selection
6. Implement explicit effect, evidence and result contracts
7. **Falsify the exclusion taxonomy**
8. Execute the tests
9. Prove there is no DI registration, database mutation or presentation dependency
10. Stage only owned files
11. Send closure evidence and the commit

**Do not wire T-177 into the current production or presentation engine.**

Once T-177 is accepted, proceed directly to T-171, then follow the Worker 3 lane sequence.

---

> **The goal is not to produce a demo-looking AI system.**
>
> **The goal is to build the real governed PPIQ Engine, AI, ML and LLM substrate now, in parallel, so that when M2a hands over the canonical persistence and relationship authority we integrate proven components rather than starting AI/ML development at that point.**

---

*Rule frozen 12 August 2026. Governs all Worker 3 implementation work under Backlog v2.10.0. Supersedes the Worker 3 architecture-design mandate, which is complete.*
