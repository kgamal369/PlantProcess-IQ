# PPIQ - MASTER DESIGN SYNCHRONISATION: CHAPTER AMENDMENT PACK

**Produced 11 August 2026. Applies the accepted AI/ML/LLM target architecture to Chapters 2, 3, 4 and 6.**
**Authority: the target architecture accepted in `PPIQ_AI_ML_LLM_Target_Architecture_Optimisation.md`, with the two ruled corrections applied.**
**Implementation prohibited. These are chapter amendments, not code.**

> **How to apply.** Each amendment names its chapter and section, gives the replacement text or the added contract, and states what stale text must be **removed**, not annotated. Where a value is parametric it names its benchmark and is left open.

---

## A2 - CHAPTER 2 AMENDMENTS

### C2-1 Capability classification - operational scoring latency

**Section 3.10, capability classification. ADD to the Core entry for actionable prediction latency:**

> The actionable-latency guarantee is delivered by **hard-reserved online scoring capacity**, not by pool ordering or job priority. A lane whose capacity can be consumed by training work cannot carry a latency guarantee, because the guarantee would depend on what else happened to be running.

**Reason.** The Core claim previously rested on a scheduler that could violate it.

### C2-2 Glossary additions

**ADD:**

| Term | Definition |
|---|---|
| **Semantic Contract Manifest** | An immutable, content-addressed record of exactly which canonical versions were in force when an artifact was produced. A reproducibility pin over `definition_versions`, the relationship publication and the registry state. **It is not an authoring authority and has no lifecycle** |
| **Intelligence and engine families** | The seven analytical families MF-01 to MF-07, sub-typed as learned model, retrieval and index, statistical engine, practice engine, and orchestration and governance |
| **Lane** | A physical execution context within a logical job class, carrying its own `max_concurrency` and `resource_capacity` |

**REMOVE from any glossary or prose:** the collective term "seven ML models" for MF-01 to MF-07. Three of the seven are not models, and the term invites a customer question with no good answer.

**No product scope change. No capability added or removed.**

---

## A3 - CHAPTER 3 AMENDMENTS

### C3-1 `feature_snapshots` - the training read path

**Section 4.5.12. AMEND the `feature_snapshots` definition:**

`storage_uri` points at a **typed columnar artifact** carrying the snapshot population. The artifact is the **authoritative training input**. ADD columns:

| Column | Type | Notes |
|---|---|---|
| `artifact_format` | varchar(32) NOT NULL | The columnar format in force. Selected by **B-03**, replaceable without contract change |
| `artifact_content_hash` | varchar(64) NOT NULL | Over the artifact bytes |
| `artifact_byte_size` | bigint NOT NULL | Sizing and retention input |
| `semantic_manifest_id` | uuid NULL FK | See C3-4 |

**ADD to the section text:**

> **PostgreSQL JSONB is not the training read path.** `feature_store` owns current governed state, lineage, row-level security and incremental refresh. The sealed columnar artifact owns high-throughput training input. Training reads the artifact and never queries `feature_store`.
>
> **The snapshot materialiser is the sole exception and is exempt by definition**: reading `feature_store` is precisely how it seals the artifact. No other component in the training or encoding path may read live feature state.

**Reason.** Deserialising millions of JSONB objects per epoch is bounded by round-trips and JSON parsing rather than by the model. Columnar reads give typed access, projection pushdown and page-cache residency.

**Benchmark: B-03** selects the format and confirms the throughput ratio.

### C3-2 `feature_snapshot_rows` - demoted to an audit sample

**Section 4.5.12. AMEND:**

`feature_snapshot_rows` is an **optional audit sample** with a declared sampling rate, retained so a spot-check can run in SQL without reading object storage. It is **not** the authoritative snapshot content.

**REMOVE** any statement or implication that it holds the full snapshot population, and any partitioning guidance predicated on full-population volume.

**Reason.** A full second copy of every training population inside the operational database, carried into replication, backup and restore.

**Conditional on B-03** confirming artifact read performance, and on C6-3 placing the artifact store in the backup set.

### C3-3 `sequence_manifests` - new table

**Section 4.5.12. ADD `ppiq_plant.sequence_manifests`:**

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `tenant_id` | uuid NOT NULL | |
| `subject_kind` | varchar(32) NOT NULL | |
| `subject_id` | uuid NOT NULL | |
| `channel_set_version` | integer NOT NULL | Encoder compatibility |
| `time_from_utc`, `time_to_utc` | timestamptz NOT NULL | |
| `sample_count` | integer NOT NULL | |
| `channel_count` | smallint NOT NULL | |
| `completeness` | numeric(9,6) NOT NULL | Observed fraction |
| `content_hash` | varchar(64) NOT NULL | |
| `storage_uri` | varchar(1000) NOT NULL | Chunk or chunk set in object storage |
| `chunk_index` | integer NULL | Where a subject spans chunks |
| `feature_snapshot_id` | uuid NULL FK | Participation in a sealed snapshot |
| `semantic_manifest_id` | uuid NULL FK | See C3-4 |

UNIQUE `(tenant_id, subject_kind, subject_id, channel_set_version, chunk_index)`.

**The numeric payload is never stored in PostgreSQL.** Immutable chunked typed arrays, compressed and partitioned, live in object storage and are read as bounded chunks.

**Reason.** The sequence product is the largest data product in the system. Array columns carry per-row overhead, defeat compression, and put the largest byte volume through WAL, replication, backup and restore.

**Benchmark: B-04** sets chunk size and compression.

### C3-4 `semantic_manifests` - new table

**Section 4.5.11 area. ADD `ppiq_meta.semantic_manifests`:**

| Column | Type | Notes |
|---|---|---|
| `manifest_id` | uuid **PRIMARY KEY** | Surrogate identity; the handle artifacts reference |
| `tenant_id` | uuid NOT NULL | |
| `manifest_hash` | varchar(64) NOT NULL | Content hash over the referenced versions |
| `definition_versions` | jsonb NOT NULL | Array of `{definition_id, version_number}` |
| `relationship_source_definition_id` | uuid NOT NULL | |
| `relationship_source_definition_version` | integer NOT NULL | |
| `registry_snapshot_hash` | varchar(64) NOT NULL | Over `registry_dimensions`, `registry_measures`, `registry_intelligence_sources` in force |
| `configuration_hash` | varchar(64) NULL | Governed configuration affecting semantics |
| `created_at_utc` | timestamptz NOT NULL | |
| `created_by_run_id` | uuid NULL | |

**UNIQUE `(tenant_id, manifest_hash)`.**

**No status column. No lifecycle. No update trigger, because nothing updates it.**

> **What it is.** A commit over the semantic contract. It records which canonical versions were in force; it does not govern them. `definition_versions`, the relationship publication and `model_registry` retain their authority unchanged.
>
> **Tenant-safe identity.** The primary key is the surrogate `manifest_id`; content addressing is expressed by the tenant-scoped unique constraint. Identical content in two tenants correctly produces two rows, because a manifest is tenant-owned evidence and a shared global row would be a cross-tenant object.

**Reason.** Without a pin, answering "which contract produced this artifact" requires reconstructing five version references from timestamps. With it, comparison is one value and a changed answer is a manifest diff.

### C3-5 Manifest references on run and artifact tables

**Section 4.5.12. ADD `semantic_manifest_id uuid NULL FK -> semantic_manifests(manifest_id)` to:**

`model_registry`, `feature_snapshots`, `sequence_manifests`, `compute_runs`, `model_training_runs`, `prediction_runs`, `practice_learning_runs`, `scenario_runs`, and the evidence-bearing result tables.

**ADD to the section text:**

> **The column is nullable for legacy records only.** A run recorded before the manifest existed remains valid and readable. **Every new governed AI/ML execution must resolve a Semantic Contract Manifest.** A run that cannot resolve one is refused rather than recorded without one.

### C3-6 Index generation record

**Section 4.5.12. ADD to the vector index generation record:**

| Column | Type | Notes |
|---|---|---|
| `index_policy` | varchar(64) NOT NULL | The selected family and its parameters |
| `recall_at_k` | numeric(9,6) NOT NULL | Measured against exact Flat on the representative sample |
| `recall_probe_size` | integer NOT NULL | The sample size the measurement used |
| `recall_floor` | numeric(9,6) NOT NULL | The declared floor for this installation |

**ADD:** a build whose measured `recall_at_k` falls below `recall_floor` does not become the served index.

**Reason.** An approximate index whose recall has never been measured is an unquantified error source presented to the customer as a plant fingerprint.

**Benchmark: B-06** sets the family per size class.

---

## A4 - CHAPTER 4 AMENDMENTS

### C4-1 Admission - separate concurrency from resource cost

**Section 5.3.2, mechanism 4. REPLACE the admission predicate.**

**REMOVE:**

```
admit while  sum(compute_weight of running) + compute_weight(candidate) <= parallelism
```

**REPLACE WITH:**

```
admit  iff  running_count < max_concurrency
       AND  sum(compute_weight of running) + compute_weight(candidate) <= resource_capacity
```

| Quantity | Meaning | Unit |
|---|---|---|
| `max_concurrency` | How many runs may be in flight in this lane | count |
| `resource_capacity` | How much of the lane's scarce resource exists | abstract units |
| `compute_weight` | How much of that resource one run consumes | abstract units |

**REMOVE** any text using one number for both quantities, including the statement that a weight-4 training job occupies a parallelism-1 pool alone.

**Reason.** As written, `parallelism = 1` with a weight-4 training job gives `4 <= 1`, which is false at every moment. **A weight-4 training job was unadmittable.** One number was expressing two different quantities.

**Benchmark: B-01** sets `resource_capacity` and `compute_weight` per lane.

### C4-2 The `ml` class resolves to three lanes

**Section 5.3.2, pool table. The six logical job classes are unchanged.** `ml` resolves to three physical lanes:

| Lane | `max_concurrency` | `resource_capacity` | Pre-emptible | Admits |
|---|---|---|---|---|
| `ml.training` | B-01 | B-01 | **Yes** | Encoder and supervised training, calibration, SHAP batch, index build and rebuild |
| `ml.batch_scoring` | B-01 | B-01 | Yes | Scheduled scoring, backfill, rescore after activation. **Batch-class capacity** |
| **`ml.online_scoring`** | B-01 | **hard-reserved, B-02** | **No** | **`event` and `micro_batch` scoring and its required serving functions only** |

**Rules:**

- **The online reservation is never available to `ml.training` or `ml.batch_scoring` admission.** The same principle as the existing `interactive` reservation.
- **Warm models.** Artifacts for every active serving identity `(tenant_id, model_code, outcome_code, grain_code, model_version)` are resident, reference-counted, with a declared eviction policy.
- **Cold-start is bounded.** A newly activated model is warmed before it serves; first-score-after-activation latency is measured.
- **`latest-only` is unchanged** and now operates inside a lane that training cannot block.

**Reason.** `predictions.actionable_deadline_utc` and `delivery_latency_seconds` exist, and section 5.8.8 makes actionable latency Core. A design in which a nightly training run can delay an event-triggered score defeats a Core requirement using the product's own scheduler.

**Benchmarks: B-01** for lane sizing, **B-02** for the reservation fraction.

### C4-3 Pre-emption

**Section 5.3.2, mechanism 9. ADD:**

> `ml.training` runs are checkpointed per stage. A training run **yields at its next checkpoint** when a reserved lane needs capacity, and resumes from that checkpoint. Nothing is lost except elapsed time, which is the correct trade against an expiring prediction.
>
> Where the runtime cannot pre-empt, the lane falls back to admission-time reservation only, **and this is recorded rather than assumed**.

### C4-4 Promotion is a three-dimensional gate

**Section 5.6.5, model governance. REPLACE promotion on quality alone.**

A candidate is promoted only when it passes all three groups on the **same governed recent holdout** as the incumbent.

**QUALITY:** discrimination or error above the incumbent or within a declared non-inferiority margin; **calibration** at or below the declared error ceiling; out-of-time performance on a window after the training window; subgroup and regime stability with no variant below its floor; missingness robustness; **explanation stability**, contributor rank correlation across bootstrap resamples above a floor.

**SERVING:** p50, p95 and p99 inference latency; throughput; artifact size; RAM and VRAM; warm-up time.

**TRAINING:** training duration against the weekly window; peak memory against lane capacity; snapshot read throughput.

**ADD two rules:**

> **A better-discriminating, worse-calibrated model is not an improvement** for a product whose output is a risk band a human acts on.
>
> **An unstable explanation is worse than none**, because the product presents contributors as evidence.

`model_registry.metrics` and `acceptance_floor` are `jsonb` and carry these dimensions. **No schema change is required.**

**Benchmark: B-05.**

### C4-5 Encoder promotion inequality

**Sections 5.6.2 to 5.6.3. ADD:**

```
promote_encoder  iff  metric_lift            >= declared_min_lift
                 AND  p95_latency_delta      <= declared_latency_budget
                 AND  artifact_size          <= declared_size_class
                 AND  explanation_stability  >= floor
```

> **If engineered features match the encoder within the lift threshold, the engineered features ship.** Deep learning being available is not a reason to deploy it.

**Benchmark: B-05.**

### C4-6 The modality boundary - governance, not modality

**Section 5.8.6. REMOVE the boundary sentence:**

> ~~No statistic, score or value is ever computed from text.~~

**REPLACE WITH:**

> **No free-form or model-generated text output may become a feature, a score, a statistic or a value.** Text and images may enter a learned result **only** through an explicitly authored model definition carrying the full training contract: a versioned immutable snapshot, declared leakage controls, held-out validation, a `model_registry` entry, calibration and drift monitoring.
>
> Retrieval-derived and LLM-derived content is **evidence only**: it may corroborate a deterministic result and may never originate one.

**Two paths, stated explicitly:**

**Path A, evidence modality.** Operator notes, shift logs, maintenance text, documents. Indexed, retrieved, cited. Never a feature, never a score, never a plant fact the LLM originated.

**Path B, governed multimodal ML.** Future capability, no implementation scope added.

```
text or image -> authored model definition -> immutable training snapshot
  -> leakage controls -> held-out validation -> model_registry entry
  -> calibration and drift -> learned output with claim class and provenance
```

**Reason.** As previously written, 5.8.6 and 5.8.7 contradict each other: 5.8.7 registers vision models in `model_registry` under full activation, retirement and drift rules, and a registered model forbidden from producing any learned result is not a model. **The hazard was never the modality. It is ungoverned output entering a score**, and the replacement names that hazard precisely.

**No implementation scope is added.** Both modalities remain interface-designed, future implementation.

### C4-7 New section 5.7.9 - Assistant runtime architecture

**ADD after 5.7.8. Sections 5.7.1 to 5.7.8 are unchanged: the dock, page context, the honesty contract, the panel, states, configuration, prohibitions and acceptance all stand.** This adds the runtime between the question and the model.

#### 5.7.9.1 The pipeline

```
  user question + page context envelope
        |
  [1] PERMISSION AND TENANT CONTEXT     resolved once, carried throughout
  [2] INTENT AND ENTITY RESOLUTION      glossary, synonyms, registry codes
  [3] DETERMINISTIC TOOL PLANNER        question shape -> declared tool set
        |
        +-- [4a] STRUCTURED TOOLS       Layer A exact, Layer B intelligence
        +-- [4b] EVIDENCE RETRIEVAL     hybrid, permission filter BEFORE ranking
        |
  [5] EVIDENCE PACKING                  dedup, rank, token budget, handles retained
  [6] MODEL GATEWAY                     serving mode, egress plan, minimum payload
  [7] LLM (ModelServingRuntime)         phrasing only
  [8] ANSWER VERIFICATION               deterministic, does not call the LLM
        |
  answer + citations   |   refusal with its reason
```

#### 5.7.9.2 The deterministic tool planner

**The LLM does not choose tools.** A planner maps resolved intent plus entity types to a declared tool set from a registry. Tool-selection accuracy is measured against a labelled question set (Q-01). A model choosing tools freely produces a different plan on a rephrasing and cannot be gated. Where intent is ambiguous the planner **asks** rather than guessing, and the ambiguity is recorded.

#### 5.7.9.3 Hybrid retrieval, and the order that matters

| Stage | Rule |
|---|---|
| **Permission filter** | **Applied before ranking, not after.** Filtering after ranking lets a high-scoring forbidden chunk displace a permitted one, so the answer silently loses evidence the user was entitled to. `assistant_chunks.role_scope` is the mechanism |
| Lexical retrieval | Full-text, for exact codes, identifiers and rare terms where embeddings underperform |
| Semantic retrieval | Embedding search over permitted chunks |
| Fusion | Reciprocal rank fusion over both lists |
| Re-ranking | **Optional, ships only if B-08 shows citation correctness improves enough to pay for its latency** |

**Structured tools take precedence over retrieval for facts and analytical results.** A number never comes from a retrieved chunk when a tool can compute it.

#### 5.7.9.4 Evidence packing under a token budget

Deduplicate by content hash. Rank by tool-result priority, then fusion score; engine output outranks a document. **Hard token budget with a reserved answer allowance**, because context overflow silently drops evidence and produces a sentence the guard then rejects. Every packed item retains its evidence handle, or step 8 cannot verify it. **Truncation is recorded and disclosed.**

**Benchmark: B-07.**

#### 5.7.9.5 Gateway and egress

The gateway routes by serving mode and enforces the egress plan, unchanged. **ADD:** the payload sent to an external provider is the **minimum scoped evidence** needed for the phrasing task, never a whole retrieval set and never raw canonical rows. **A provider or model change is a governed release event**, recorded with a reason, because it changes answer behaviour with no code change.

#### 5.7.9.6 `ModelServingRuntime`

A replaceable abstraction: load, unload, generate, stream, health, capability reporting. Candidate runtimes are benchmarked as implementations (B-09). **No serving library is the product contract.**

#### 5.7.9.7 Answer verification

The no-fabrication guard of 5.7.3, given an operational definition. **The verifier is deterministic and does not call the LLM**, because a model checking its own output is not a guard.

| Check | On failure |
|---|---|
| Every numeric claim resolves to a handle in the supplied evidence | Reject before display |
| No claim class upgraded by language: an association is not phrased as a cause | Reject before display |
| No refusal replaced by a phrased answer | Reject before display |
| Transport failure red, refusal amber | Never conflated |

#### 5.7.9.8 Assistant quality gates

| ID | Gate |
|---|---|
| Q-01 | Tool-selection accuracy |
| Q-02 | Groundedness: fraction of claims with a resolving handle |
| Q-03 | Citation correctness: the handle supports the claim |
| Q-04 | Unsupported-claim rate |
| Q-05 | **Refusal correctness**, including unit-sanity probes |
| Q-06 | **Causal-overreach rate**: association phrased as cause |
| Q-07 | Multilingual fidelity |
| Q-08 | Time to first token |
| Q-09 | Total answer latency, p95, against the under-2-minute ceiling |
| Q-10 | Serving throughput |
| Q-11 | Memory and VRAM per concurrent session |

**Q-05 and Q-06 decide credibility.** A speed question answered in a unit of mass, or an association phrased as a cause, destroys the intelligence claim in one sentence, and both are testable against a fixed probe set before any customer sees them.

### C4-8 Terminology

**Throughout Chapter 4. REPLACE** the collective "seven ML models" with **"seven intelligence and engine families"**, sub-typed: learned model (MF-01, MF-03, MF-04), retrieval and index (MF-02), statistical engine (MF-05, MF-06), practice engine (MF-07), plus orchestration and governance.

**Reason.** Three of the seven are not models. The sub-type is load-bearing: it determines refresh policy, lane assignment and whether a champion/challenger gate applies at all.

---

## A6 - CHAPTER 6 AMENDMENTS

### C6-1 Container inventory

**AMEND the container inventory. REPLACE** the single background-worker entry:

| Container | Role | Scaling | Notes |
|---|---|---|---|
| `ppiq-worker` | import, projection, analysis, report, **`ml.batch_scoring`** | 1 to N | Batch, backfill and rescore work |
| **`ppiq-ml-train`** | `ml.training` | 0 to N, GPU-capable | **Pre-emptible, checkpointed** |
| **`ppiq-ml-online`** | **`ml.online_scoring` only** | 1 to N, hard-reserved | Warm model cache. **No training imports, no batch admission** |

> **`ppiq-ml-online` runs operational event and micro-batch scoring and its required serving functions only.** Batch, backfill and rescore work runs on batch and training-class capacity.
>
> **Where a deployment physically shares hardware between lanes, online capacity is still hard-reserved**, and B-02 must prove the actionable-latency target holds while training and batch work are saturated. Sharing hardware is a sizing decision; it is never permission to consume the reservation.
>
> **`ppiq-ml-online` importing a trainer module is a build-time test failure.**

### C6-2 Resource model

**ADD** the hard reservation for `ml.online_scoring` alongside the existing `interactive` reservation. Both are subtracted from admissible capacity before any batch or training admission is considered.

**Benchmark: B-02.**

### C6-3 Backup set

**AMEND.** The **feature-snapshot artifact store** and the **sequence artifact store** are in the backup set, with their own retention driven by `feature_snapshots.retention_until_utc` and the sequence retention policy.

**Reason.** C3-1 and C3-2 make the artifact authoritative. An authoritative artifact outside the backup set is a reproducibility claim that does not survive a restore. The mechanism already exists for definition export artifacts.

### C6-4 Model gateway

**ADD:** minimum-scoped-payload rule for external providers. **A provider or model change is a governed release event.** The existing behaviour of refusing rather than falling back to an unapproved model is unchanged and reinforced.

### C6-5 Capacity model

**ADD** to the sizing inputs: snapshot read throughput (B-03), warm-model memory per active serving identity (B-05), and per-session VRAM for assistant serving (B-09).

---

## BENCHMARK PARAMETERS - NOT GUESSED

Every value below stays open until measured. **No amendment in this pack asserts a number.**

| ID | Question | Decides |
|---|---|---|
| B-01 | `max_concurrency`, `resource_capacity`, `compute_weight` per lane | C4-1, C4-2 |
| B-02 | Online reservation fraction | C4-2, C6-2 |
| B-03 | Columnar format; whether `feature_snapshot_rows` can be demoted | C3-1, C3-2 |
| B-04 | Sequence chunk size and compression | C3-3 |
| B-05 | Encoder lift versus serving cost | C4-5, C6-5 |
| B-06 | ANN family per size class | C3-6 |
| B-07 | Token budget and evidence-set size | C4-7 |
| B-08 | Whether re-ranking earns its latency | C4-7 |
| B-09 | Serving runtime and concurrency | C4-7, C6-5 |

---

*Chapter Amendment Pack, 11 August 2026. Twenty-two amendments across four chapters. Every amendment states what is removed, what replaces it, and why. Implementation prohibited.*
