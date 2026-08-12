# PPIQ - AI / ML / LLM TARGET ARCHITECTURE OPTIMISATION PASS

**Produced 11 August 2026. Implementation prohibited.**
**Inputs: Chapters 1 to 6, the Layer B Rule, the Layer B Architecture Pack Revision 6.3. Neither input is treated as automatically correct.**
**Objective: the best possible PPIQ AI + ML + LLM architecture, with every mechanism justified by a measurable property.**

---

## 0. METHOD AND WHAT THIS PASS IS NOT

### 0.1 The test each mechanism had to pass

A mechanism stays only if it improves at least one of: model quality, calibration, correctness, evidence quality, robustness, latency, throughput, memory, training cost, serving cost, reproducibility, scalability, maintainability, operational safety. **A mechanism that improves none of these was removed or refused entry regardless of which document proposed it.**

### 0.2 What this pass rejected

Nine candidate additions were considered and refused. They are listed with reasons in section 12, because a design document that only records what was added hides half its reasoning.

### 0.3 Honest statement of the two places I changed my own position

**The absolute text boundary was mine and it was wrong.** Revision 6.3 stated that no statistic, score or value is ever computed from text. That rule is correct for free-form LLM output and too broad as a permanent boundary. Worse, it **contradicts Chapter 4 5.8.7**, which registers vision models in `model_registry` under the same activation, retirement and drift rules and therefore expects them to produce learned results. Section 8 resolves this.

**The `ml` pool defect is in Chapter 4, not only in the Pack.** Chapter 4 5.3.2 sets `ml` parallelism to 1 and states that a model-training job weighted 4 occupies its pool alone, while admission is `sum(weight of running) + weight(candidate) <= parallelism`. **A weight-4 candidate can never be admitted into a capacity of 1.** Section 1 fixes it.

---

## 1. DECISION MATRIX

Eleven material choices. Impact is stated against the criteria of 0.1.

### D-01 Resource architecture and lane isolation

| | Design |
|---|---|
| **Chapter 4 5.3.2** | Six pools. `ml` parallelism 1, admitting training and scoring together. `compute_weight` is both concurrency slot and resource cost |
| **Pack Rev 6.3** | Adopts Chapter 4 verbatim, including the defect |
| **PROPOSED** | Keep the six **logical job classes** unchanged. Split the `ml` class into **three physical lanes** with separate `max_concurrency` and `resource_capacity`. Online scoring gets reserved capacity, warm models and a latency budget independent of training occupancy. Training becomes checkpointable and pre-emptible |

| Impact | Assessment |
|---|---|
| Correctness | Fixes an unsatisfiable admission predicate. A weight-4 training job is currently unadmittable |
| **Operational safety** | **Highest impact in this pass.** Today a training run can delay a prediction whose value expires at an actionable deadline. The product's central operational claim is defeated by its own scheduler |
| Latency | p95 online scoring becomes independent of training occupancy |
| Throughput | Batch scoring no longer blocks behind encoder training |
| Complexity | Moderate. One new worker role, two extra lane definitions, a pre-emption hook |
| **Recommendation** | **Adopt. Amend Chapter 4 5.3.2 and Chapter 6 topology** |
| Benchmark | B-01, B-02 |

### D-02 Semantic reproducibility pin

| | Design |
|---|---|
| **Chapters** | Version authority is distributed: `definition_versions`, relationship `source_definition_id`/`version`, feature-set versions, snapshot ids, `model_registry` versions. **No single pin over the set** |
| **Pack Rev 6.3** | `SemanticModelVersion` with its own draft/validated/published/rolled-back lifecycle. A fourth authoring authority |
| **PROPOSED** | **Semantic Contract Manifest.** Immutable, content-addressed, no lifecycle. A commit over the canonical versions in force, not an authority over them |

| Impact | Assessment |
|---|---|
| **Reproducibility** | **The problem this solves is real.** Without a pin, "which contract produced this model" requires reconstructing five version references from timestamps |
| Maintainability | Removes a competing lifecycle. One authoring path stays canonical |
| Correctness | A manifest hash mismatch is a detectable condition; five drifting references are not |
| Complexity | Low. One table, one hash function, one FK from artifacts that need it |
| **Recommendation** | **Adopt the hybrid. Amend Chapter 3 to add the manifest; withdraw the Pack's competing lifecycle** |
| Benchmark | None. This is a correctness mechanism, not a performance one |

### D-03 Feature data path for training

| | Design |
|---|---|
| **Chapter 3 4.5.12** | `feature_store` with `features jsonb` in PostgreSQL; `feature_snapshots` with a `storage_uri`; `feature_snapshot_rows` holding the same content as PostgreSQL rows |
| **Pack Rev 6.3** | Adopts the canonical shape and says training pins a snapshot id, without stating the physical read path |
| **PROPOSED** | Keep `feature_store` as governance and current state. **State explicitly that PostgreSQL JSONB is not the training read path.** Sealing a snapshot writes a **typed columnar artifact** (Parquet or Arrow IPC) to object storage, and the Python loader reads that. `feature_snapshot_rows` becomes an optional audit sample, not the authoritative copy |

| Impact | Assessment |
|---|---|
| **Training cost** | **Largest single throughput win available.** Deserialising millions of JSONB objects per epoch is bounded by PostgreSQL round-trips and JSON parsing, not by the model |
| Memory | Columnar with projection pushdown reads only the features a model declares |
| **Storage** | `feature_snapshot_rows` currently duplicates snapshot content inside the database. Removing it as the authoritative copy reduces amplification materially |
| Reproducibility | Unchanged and slightly improved: the artifact carries a content hash |
| Governance | Unchanged. The database still owns current state, lineage and RLS |
| Complexity | Low. `feature_snapshots.storage_uri` already exists in Chapter 3; the amendment gives it a typed format and makes it authoritative |
| **Recommendation** | **Adopt. Amend Chapter 3 4.5.12** |
| Benchmark | B-03 |

### D-04 Sequence payload layout

| | Design |
|---|---|
| **Chapters** | No sequence store is defined |
| **Pack Rev 6.3** | DP-3 with `values float32[]`, `offsets_ms int32[]` and `mask uint8[]` as PostgreSQL arrays |
| **PROPOSED** | **Split contract.** PostgreSQL holds a manifest only. Object storage holds immutable chunked typed numeric arrays, compressed, partitioned, memory-mappable. The loader consumes bounded chunks |

| Impact | Assessment |
|---|---|
| **Storage** | This is the largest data product in the system. PostgreSQL arrays carry per-row overhead, defeat compression and bloat WAL, backups and replication |
| Throughput | Memory-mappable chunks let the loader stream without materialising a batch through the database |
| Latency | Serving never reads sequences; only training and encoding do. No serving impact |
| Operational safety | Keeps the largest byte volume out of the backup and restore path that Chapter 6 governs |
| Complexity | Low, and lower than the alternative once volumes grow |
| **Recommendation** | **Adopt. Amend the Pack; add a manifest table to Chapter 3** |
| Benchmark | B-04 |

### D-05 Model promotion criteria

| | Design |
|---|---|
| **Chapter 4 5.6.5** | Registry, metrics, acceptance floor. Champion/challenger on quality |
| **Pack Rev 6.3** | G-15 baseline comparison, G-16 subgroup stability, G-17 champion/challenger on quality metrics |
| **PROPOSED** | Promotion requires a **three-dimensional gate: quality, serving cost, training cost.** A model does not win on discrimination alone. An encoder is promoted only when measured downstream lift exceeds a declared threshold **and** its latency, memory and artifact-size deltas sit inside declared budgets |

| Impact | Assessment |
|---|---|
| Model quality | Unchanged upward pressure, plus calibration and explanation stability added as first-class criteria |
| **Serving cost** | **Prevents the common failure of shipping a heavy encoder for a lift that does not survive the latency budget** |
| Latency | p50/p95/p99 become promotion criteria rather than post-hoc discoveries |
| Reproducibility | Unchanged |
| Complexity | Low. `model_registry.metrics` and `acceptance_floor` are already `jsonb` and can carry these without schema change |
| **Recommendation** | **Adopt. Amend Chapter 4 5.6.5. No Chapter 3 schema change needed** |
| Benchmark | B-05 |

### D-06 ANN index policy

| | Design |
|---|---|
| **Chapters** | Not specified |
| **Pack Rev 6.3** | `VectorSimilarityIndex` abstraction, FAISS as initial implementation, OD-04 for the rebuild trigger |
| **PROPOSED** | Keep the abstraction. Add a **policy selector driven by measurement**, and **retain exact Flat search on a representative sample permanently** as the recall baseline |

| Impact | Assessment |
|---|---|
| **Correctness** | An approximate index without a measured recall baseline is an unquantified error source presented as a fingerprint |
| Latency, memory | The right family depends on population, dimension and RAM; a fixed choice is wrong at one end of the range |
| Maintainability | Policy in configuration beats a code change per customer size |
| Complexity | Low |
| **Recommendation** | **Adopt. Pack amendment only** |
| Benchmark | B-06 |

### D-07 Unstructured modality boundary

| | Design |
|---|---|
| **Chapter 4 5.8.6** | No statistic, score or value is ever computed from text |
| **Chapter 4 5.8.7** | Vision models register in `model_registry` and produce annotations with confidence, under the same activation and drift rules |
| **Pack Rev 6.3** | Adopted 5.8.6 as an absolute rule across all modalities |
| **PROPOSED** | **The boundary is governance, not modality.** Two paths: **evidence modality** (free-form, retrieval and citation only, never a feature or score) and **governed multimodal ML** (an authored model definition with the full training contract, permitted to produce a learned output with a claim class) |

| Impact | Assessment |
|---|---|
| **Correctness** | **Resolves a live contradiction between 5.8.6 and 5.8.7.** As written, an inspection-image model is both registered and forbidden from producing a result |
| Evidence quality | Strengthened. The rule now names the actual hazard, which is ungoverned output entering a score, rather than banning a modality |
| Operational safety | Unchanged. Nothing ungoverned can reach a feature under either wording |
| Scope | **No implementation scope added.** Both remain future capabilities with interfaces designed |
| **Recommendation** | **Adopt. Amend Chapter 4 5.8.6 wording and the Rule** |
| Benchmark | None now |

### D-08 Assistant and LLM runtime

| | Design |
|---|---|
| **Chapter 4 5.7** | Excellent UX, honesty contract, no-fabrication guard, tool and retrieval separation, serving modes, acceptance criteria |
| **Chapter 6** | Model gateway, self-hosted serving container, egress plan, tier gating |
| **Pack Rev 6.3** | Eleven Layer B tool contracts with an evidence envelope; delegates the rest to DF15 |
| **PROPOSED** | Keep all of the above. Add the **missing runtime layer**: deterministic tool planner, hybrid retrieval, permission filtering before context assembly, token-budgeted evidence packing, `ModelServingRuntime` abstraction, answer verification, and eleven measurable quality gates |

| Impact | Assessment |
|---|---|
| **Evidence quality** | The no-fabrication guard exists; **what is missing is how evidence is selected and budgeted before it reaches the model**, which is where groundedness is actually won or lost |
| Correctness | A deterministic planner makes tool selection testable; a model choosing tools freely is not |
| Latency | Time-to-first-token and p95 answer latency become gated rather than emergent |
| Serving cost | The runtime abstraction lets serving be benchmarked and replaced without a product change |
| Operational safety | Permission filtering before packing, not after retrieval, closes the leak path |
| Complexity | Moderate. This is the largest addition in the pass and it is justified because the Assistant is Core and currently has no runtime specification |
| **Recommendation** | **Adopt. New Chapter 4 subsections plus a Chapter 6 amendment** |
| Benchmark | B-07 to B-09 |

### D-09 Terminology

| | Design |
|---|---|
| **Pack Rev 6.3** | "Seven model families" and, in places, "seven ML models" |
| **PROPOSED** | **Seven intelligence and engine families**, sub-typed: statistical engine, practice engine, learned model families, retrieval and index, orchestration and governance |

| Impact | Assessment |
|---|---|
| Correctness | MF-06 is a statistical engine and MF-02 is a retrieval index. Neither is a model, and calling them models invites a customer question with no good answer |
| Maintainability | The sub-type drives refresh policy and pool assignment, so it is load-bearing rather than cosmetic |
| **Recommendation** | **Adopt. Pack and Rule amendment** |

### D-10 Feature snapshot row duplication

| | Design |
|---|---|
| **Chapter 3 4.5.12** | `feature_snapshot_rows` in PostgreSQL, partitioned by snapshot above Large |
| **PROPOSED** | Authoritative snapshot content moves to the columnar artifact. `feature_snapshot_rows` retained as an **optional audit sample** with a declared sampling rate, or omitted where the artifact hash suffices |

| Impact | Assessment |
|---|---|
| **Storage** | A full second copy of every training population inside the operational database, replicated and backed up |
| Training cost | See D-03 |
| Reproducibility | Unchanged. The artifact hash is the reproducibility mechanism |
| Risk | Low, provided the artifact store is in the backup set, which Chapter 6 already requires for definition export artifacts |
| **Recommendation** | **Adopt, conditional on B-03. Amend Chapter 3** |

### D-11 Encoder as a promoted component

| | Design |
|---|---|
| **Pack Rev 6.3** | MF-01 optional; skip-and-continue at commissioning |
| **PROPOSED** | Keep optional. Add an explicit **promotion rule expressed as an inequality**, so "is the encoder worth it" is answered by measurement rather than by preference |

Promotion rule, evaluated on the same governed holdout:

```
promote_encoder  iff  metric_lift            >= declared_min_lift
                 AND  p95_latency_delta      <= declared_latency_budget
                 AND  artifact_size          <= declared_size_class
                 AND  explanation_stability  >= floor
```

| Impact | Assessment |
|---|---|
| Serving cost | Prevents a heavy representation shipping for a lift that does not clear its own cost |
| Model quality | Neutral to positive; the encoder still ships when it earns it |
| **Recommendation** | **Adopt. Pack and Chapter 4 5.6.5 amendment** |
| Benchmark | B-05 |

---

## 2. D-01 IN FULL: LANES, CONCURRENCY AND RESOURCE CAPACITY

### 2.1 The defect, stated precisely

Chapter 4 5.3.2 defines admission as:

```
admit while   sum(compute_weight of running) + compute_weight(candidate) <= parallelism
ml pool:      parallelism = 1
training job: compute_weight = 4
```

`4 <= 1` is false at every moment. **A weight-4 training job is unadmittable.** The text "a model-training job weighted 4 occupies its pool alone" describes an intent that the predicate cannot express, because one number is being used for two different quantities: how many jobs may run, and how much resource each consumes.

### 2.2 The separation

| Quantity | Meaning | Unit |
|---|---|---|
| **`max_concurrency`** | How many runs may be in flight in this lane | count |
| **`resource_capacity`** | How much of the lane's scarce resource exists | abstract units |
| **`compute_weight`** | How much of that resource one run consumes | abstract units |

```
admit  iff  running_count < max_concurrency
       AND  sum(compute_weight of running) + compute_weight(candidate) <= resource_capacity
```

Both conditions, not one. This is the whole fix, and it is a two-column schema change.

### 2.3 The three ml lanes

The **logical job class stays `ml`**, so Chapter 4's six-class taxonomy is unchanged and no second scheduler taxonomy is created. What changes is that `ml` resolves to three physical lanes.

| Lane | `max_concurrency` | `resource_capacity` | Pre-emptible | Admits |
|---|---|---|---|---|
| **`ml.training`** | 1 | 8 | **Yes** | Encoder training, supervised training, calibration, SHAP batch, index build, index rebuild |
| **`ml.batch_scoring`** | 2 | 4 | Yes | Scheduled scoring, backfill scoring, rescoring after activation. **Batch-class capacity, never the online reservation** |
| **`ml.online_scoring`** | **reserved** | **reserved** | **No** | `event` and `micro_batch` scoring |

Initial capacities are placeholders pending B-01. **The structure is the architecture; the numbers are the benchmark.**

### 2.4 Why online scoring must leave the training lane

Chapter 3's `predictions` table carries `actionable_deadline_utc`, `met_actionable_deadline` and `delivery_latency_seconds NOT NULL`, and Chapter 4 5.8.8 makes actionable prediction latency **Core**. A design in which a nightly encoder training run can delay an event-triggered score **defeats the product's own Core requirement using its own scheduler**. That is the strongest argument in this pass for changing a chapter.

### 2.5 What online scoring gets

| Property | Rule |
|---|---|
| **Reserved resources** | A fixed share, never available to `ml.training` or `ml.batch_scoring` admission. The same principle as the `interactive` reservation |
| **Warm models** | Artifacts for every active serving identity are resident, keyed by `(tenant_id, model_code, outcome_code, grain_code, model_version)`, reference-counted, with a declared eviction policy |
| **Independent latency budget** | p95 measured and gated against the `actionable_deadline` contract, not against training occupancy |
| **Cold-start bound** | First-score-after-activation latency is measured and bounded; a newly activated model is warmed before it serves |
| **`latest-only` retained** | Chapter 4's policy is unchanged and now operates inside a lane that is not blocked by training |

### 2.6 Pre-emption

`ml.training` runs are checkpointed per stage already (Pack section 7.3). Pre-emption uses that: a training run yields at its next checkpoint when a higher-priority lane needs capacity, and resumes from the checkpoint. **Nothing is lost except elapsed time**, which is the correct trade for an expiring prediction.

Where the runtime cannot pre-empt, the lane falls back to admission-time reservation only, and this is recorded rather than assumed.

### 2.7 Chapter 6 consequence

Chapter 6 lists container 4 as background workers, one image, one worker role. The proposal splits the worker deployment:

| Container | Role | Scaling | Notes |
|---|---|---|---|
| `ppiq-worker` | import, projection, analysis, report, **`ml.batch_scoring`** | 1 to N | Batch, backfill and rescore work |
| **`ppiq-ml-train`** | `ml.training` | 0 to N, GPU-capable | Pre-emptible, checkpointed |
| **`ppiq-ml-online`** | **`ml.online_scoring` only** | 1 to N, hard-reserved | Warm model cache. **No training imports, no batch admission** |

**`ppiq-ml-online` runs operational event and micro-batch scoring and its required serving functions only.** It does not admit `ml.batch_scoring`. Batch, backfill and rescore work runs on batch and training-class capacity, on `ppiq-ml-train` or a dedicated batch worker.

**Where a deployment physically shares hardware between lanes, online capacity is still hard-reserved**, and B-02 must prove the actionable-latency target holds while training and batch work are saturated. Sharing hardware is a sizing decision; it is never permission to consume the reservation.

**`ppiq-ml-online` importing a trainer module is a build-time test failure**, which is the Serving Wall of Pack section 2.3 made physical.

---

## 3. D-02 IN FULL: THE SEMANTIC CONTRACT MANIFEST

### 3.1 What it is and is not

**Is:** an immutable, content-addressed record of exactly which canonical versions were in force at a moment. A commit hash over the semantic contract.

**Is not:** an authoring surface, a lifecycle, an approval step, or an authority over any version it references. It has no draft state, no publish action and no rollback. **A manifest is created, referenced and never modified.**

### 3.2 Proposed persistence, for Chapter 3

**`ppiq_meta.semantic_manifests`**

| Column | Type | Notes |
|---|---|---|
| `manifest_id` | uuid NOT NULL | **PK.** Surrogate identity, referenced by artifacts |
| `tenant_id` | uuid NOT NULL | |
| `manifest_hash` | varchar(64) NOT NULL | Content hash over the referenced versions |
| `definition_versions` | jsonb NOT NULL | Array of `{definition_id, version_number}` for every definition in force that the referencing artifact depends on |
| `relationship_source_definition_id` | uuid NOT NULL | |
| `relationship_source_definition_version` | integer NOT NULL | |
| `registry_snapshot_hash` | varchar(64) NOT NULL | Hash over `registry_dimensions`, `registry_measures`, `registry_intelligence_sources` rows in force |
| `configuration_hash` | varchar(64) | Governed configuration affecting semantics |
| `created_at_utc` | timestamptz NOT NULL | |
| `created_by_run_id` | uuid | The run that first needed it |

**UNIQUE `(tenant_id, manifest_hash)`.** Identical content within a tenant never creates a second row; identical content across two tenants correctly creates two rows, because a manifest is tenant-owned evidence and a shared global row would be a cross-tenant object.

No status column. No update trigger, because nothing updates it. **The content hash is the identity property; `manifest_id` is the reference handle.**

### 3.3 Who references it

`model_registry`, `feature_snapshots`, `compute_runs`, `prediction_runs`, `practice_learning_runs` and every evidence row gain **`semantic_manifest_id uuid NULL FK -> semantic_manifests(manifest_id)`**.

**The column is nullable for legacy records only.** A run recorded before the manifest existed remains valid and readable. **Every new governed AI/ML execution must resolve a Semantic Contract Manifest**: training, encoding, scoring, statistical, practice and effect runs all populate it, and a run that cannot resolve one is refused rather than recorded without one. Gate G-55 asserts both halves.

### 3.4 What it buys, measurably

| Property | Before | After |
|---|---|---|
| Reproducibility | Five version references reconstructed from timestamps | One hash |
| Change detection | Compare five values pairwise | Compare one hash |
| Explaining a changed answer | Manual reconstruction | Diff two manifests |
| Authoring authorities | Four, if the Pack's version object were built | **Three, unchanged from the chapters** |

### 3.5 The Pack change

`SM-01 SemanticModelVersion` loses its lifecycle and becomes the manifest. The Pack's `status` enum, `predecessor_version_id` and publication rules are withdrawn, because `definition_versions` already owns that lifecycle.

---

## 4. D-03 AND D-10 IN FULL: THE TRAINING DATA PATH

### 4.1 The two-role split, stated once

| Store | Owns | Never |
|---|---|---|
| **`ppiq_plant.feature_store`** (PostgreSQL, `features jsonb`) | Current governed state, lineage, RLS, incremental refresh by watermark, dirty tracking, ad-hoc governed query | **The training read path** |
| **Sealed columnar snapshot artifact** (object storage) | High-throughput typed training input | Current state. It is a frozen population, by definition stale the moment it is sealed |

### 4.2 The training contract

```
live governed feature state          feature_store, jsonb, incremental
        |
        |  seal
        v
immutable snapshot manifest          feature_snapshots, storage_uri, lineage_hash
        |
        |  materialise
        v
typed columnar artifact              Parquet or Arrow IPC, object storage
        |
        |  bounded read, projection pushdown
        v
Python data loader                   PyTorch / LightGBM input
```

**Training reads the artifact. Training never issues a query against `feature_store`.** This is a gate, not a guideline: G-48 below.

### 4.3 Why, in measurable terms

| Property | JSONB path | Columnar artifact |
|---|---|---|
| Per-row cost | JSON parse plus key lookup per feature per row per epoch | Typed column read, no parse |
| Projection | Whole document read, fields discarded in Python | Only declared columns read from disk |
| Compression | TOAST, row-oriented, poor ratios on numeric data | Columnar encodings, dictionary and run-length on categoricals |
| Concurrency | Consumes database connections and buffer cache that serving needs | No database involvement |
| Repeatability across epochs | Re-query every epoch | Memory-mappable, page cache resident |

The exact ratio is hardware and schema dependent and is **B-03**. The direction is not in doubt.

### 4.4 Format choice, left open with a defined benchmark

Parquet and Arrow IPC are both candidates. Parquet compresses better and is the better archival format; Arrow IPC is zero-copy and faster to load when it fits. **The selection is B-03 and the loser is not designed around**, because both satisfy the same logical contract: typed columns, a content hash, a storage URI, immutability.

### 4.5 `feature_snapshot_rows`

Chapter 3 defines it as a PostgreSQL table holding the snapshot content, partitioned by snapshot above Large. **This is a second full copy of every training population inside the operational database**, carried into replication, backup and restore.

Proposed: the artifact is authoritative. `feature_snapshot_rows` becomes an **optional audit sample** with a declared sampling rate, retained so a spot-check can be run in SQL without reading object storage. Where the artifact hash and the reproducibility gate suffice, it may be omitted entirely.

**This is conditional on B-03 confirming artifact read performance**, and on Chapter 6 confirming the artifact store is in the backup set. Chapter 6 already requires that for `definition_export_artifacts`, so the mechanism exists.

---

## 5. D-04 IN FULL: THE SEQUENCE PAYLOAD

### 5.1 The split contract

**PostgreSQL, `ppiq_plant.sequence_manifests`** - manifest only, no numeric payload.

| Column | Type | Notes |
|---|---|---|
| `subject_kind`, `subject_id` | varchar, uuid | Grain identity |
| `channel_set_version` | integer NOT NULL | Encoder compatibility, per Pack G-13 |
| `time_from_utc`, `time_to_utc` | timestamptz | |
| `sample_count` | integer NOT NULL | |
| `channel_count` | smallint NOT NULL | |
| `completeness` | numeric(9,6) NOT NULL | Observed fraction |
| `content_hash` | varchar(64) NOT NULL | |
| `storage_uri` | varchar(1000) NOT NULL | The chunk or chunk set |
| `chunk_index` | integer | Where a subject spans chunks |
| `feature_snapshot_id` | uuid NULL FK | The snapshot this participates in |

**Object storage** - immutable, chunked, typed, compressed numeric arrays, partitioned by tenant and time, memory-mappable where the format allows.

### 5.2 Why not PostgreSQL arrays

| Property | `float32[]` in PostgreSQL | Chunked artifact |
|---|---|---|
| Storage amplification | Per-row and per-array overhead, TOAST for anything sizeable | Typed, compressed, near-raw |
| Backup and restore | Every byte in the database backup and the restore window | Object-store lifecycle, independent retention |
| Replication | Every byte through WAL | None |
| Loader pattern | One giant row per subject, materialised in full | Bounded chunks, streamable |
| Random access | Whole array read | Offset read within a chunk |

**The sequence store is the largest data product in the system.** Putting it in the database moves the largest byte volume into the most expensive and most operationally sensitive tier.

### 5.3 What stays generic

The logical contract is unchanged: ordered timestamped values per channel, a mask, a completeness measure, a channel-set version. **Physical layout is selected by measured read throughput and storage amplification (B-04)** and is replaceable without touching the logical contract or any engine.

---

## 6. D-05 AND D-11 IN FULL: PROMOTION AS A THREE-DIMENSIONAL GATE

### 6.1 The gate

A candidate is promoted only when it passes all three groups on the **same governed recent holdout** as the incumbent.

**QUALITY**

| Metric | Rule |
|---|---|
| Discrimination or error | Above the incumbent, or within a declared non-inferiority margin |
| **Calibration** | Calibration error at or below the declared ceiling. **A better-discriminating, worse-calibrated model is not an improvement** for a product whose output is a risk band a human acts on |
| Out-of-time performance | Measured on a window after the training window, never a random split |
| Subgroup and regime stability | No variant level below the declared floor |
| Missingness robustness | Performance under the declared missingness policy |
| **Explanation stability** | Contributor rank correlation across bootstrap resamples above a floor. **An unstable explanation is worse than none**, because the product presents contributors as evidence |

**SERVING**

| Metric | Why it gates |
|---|---|
| p50, p95, p99 inference latency | The actionable-deadline contract is measured in seconds |
| Throughput | Units scored per second per lane |
| Artifact size | Cold-start and warm-cache occupancy |
| RAM and VRAM | `ml.online_scoring` reservation sizing |
| Warm-up time | First-score-after-activation bound |

**TRAINING**

| Metric | Why it gates |
|---|---|
| Training duration | The weekly 24 hour window |
| Peak memory | Lane capacity |
| Snapshot read throughput | Confirms D-03 holds at this data size |

### 6.2 The encoder inequality

```
promote_encoder  iff  metric_lift            >= declared_min_lift
                 AND  p95_latency_delta      <= declared_latency_budget
                 AND  artifact_size          <= declared_size_class
                 AND  explanation_stability  >= floor
```

**If engineered features match the encoder within the lift threshold, the engineered features ship.** Deep learning is available; that is not a reason to deploy it.

### 6.3 Persistence

No Chapter 3 schema change. `model_registry.metrics` and `acceptance_floor` are `jsonb` and carry these dimensions. **Chapter 4 5.6.5 gains the promotion criteria**; the registry already has the shape.

---

## 7. D-06 IN FULL: ANN POLICY

### 7.1 The selector

| Input | Drives |
|---|---|
| Population size | Flat versus graph versus inverted-file |
| Vector dimension | Memory per vector, quantisation viability |
| Available RAM | Whether the index is resident or memory-mapped |
| Required recall@k | The accuracy floor the policy must meet |
| p95 latency target | Search-time parameters |
| Index build time | Whether a full rebuild fits the weekly or governed window |
| Update pattern | Generational extension versus periodic rebuild |

### 7.2 The permanent correctness baseline

**Exact Flat search is retained on a representative sample, permanently, in every installation.** It is the only way to measure recall@k of the production index. An approximate index whose recall has never been measured is an unquantified error source presented to the customer as a plant fingerprint.

Recall@k is measured on every index build and stored on `index_generation`. **A build whose measured recall falls below the declared floor does not become the served index.**

### 7.3 What stays out of the contract

FAISS, HNSW, IVF, PQ, quantisation and GPU variants are **implementations selected by measurement**. `VectorSimilarityIndex` with build, seal, extend, search and recall_probe is the contract. No library name appears in it.

---

## 8. D-07 IN FULL: THE MODALITY BOUNDARY REDRAWN

### 8.1 The contradiction being fixed

| Chapter 4 5.8.6 | Chapter 4 5.8.7 |
|---|---|
| No statistic, score or value is ever computed from text | Image models register in `model_registry` with the vision family, under the same activation, retirement and drift rules; annotations carry a confidence and the model version that produced it |

**A registered model that may never produce a learned result is not a model.** The two clauses cannot both stand as written.

### 8.2 The correct boundary

The hazard was never the modality. It is **ungoverned output entering a score.** A free-form LLM summary has no training snapshot, no held-out validation, no calibration, no drift monitor and no leakage control. An authored vision model has all five.

**Proposed rule, replacing the 5.8.6 boundary sentence:**

> **No free-form or model-generated text output may become a feature, a score, a statistic or a value.** Text and images may enter a learned result **only** through an explicitly authored model definition carrying the full training contract: a versioned immutable snapshot, declared leakage controls, held-out validation, model registry entry, calibration and drift monitoring. Retrieval-derived and LLM-derived content is **evidence only**: it may corroborate a deterministic result and may never originate one.

### 8.3 The two paths

**Path A - Evidence modality.** Operator notes, shift logs, maintenance text, documents. Indexed, retrieved, cited. **Never a feature, never a score, never a plant fact the LLM originated.** Unchanged from Chapter 4 5.8.6's intent.

**Path B - Governed multimodal ML.** Future capability, no implementation scope added now.

```
text or image
  -> explicitly authored model definition        definition_kind = 'model'
  -> versioned immutable training snapshot       leakage controls, overlap_rows = 0
  -> held-out validation                         out-of-time, subgroup
  -> model_registry entry                        serving identity, status, serving_role
  -> calibration and drift monitoring            same as any model
  -> learned output with claim class + provenance
```

**No free-form LLM output becomes a model feature under either path.** That prohibition is absolute and is what 5.8.6 was reaching for.

### 8.4 Scope discipline

This reserves the correct architecture and adds **no implementation scope**. Both modalities remain `INTERFACE-DESIGNED / FUTURE IMPLEMENTATION`. What changes is that when they are built, the boundary they must respect is stated correctly rather than in a form that forbids the thing 5.8.7 designs.

---

## 9. D-08 IN FULL: ASSISTANT AND LLM RUNTIME ARCHITECTURE

Chapter 4 5.7 specifies the dock, the honesty contract, the no-fabrication guard and acceptance. Chapter 6 specifies the gateway, the serving container and the egress plan. **What no document specifies is the runtime between the question and the model**, which is where groundedness is won or lost.

### 9.1 The pipeline

```
  user question + page context envelope
        |
  [1] PERMISSION AND TENANT CONTEXT
        role, tier, tenant, site, entitlement          -> resolved once, carried throughout
        |
  [2] INTENT AND ENTITY RESOLUTION
        glossary, synonyms, registry codes              -> canonical codes, never free text
        |
  [3] DETERMINISTIC TOOL PLANNER
        question shape -> declared tool set             -> a plan, not a model choice
        |
        +---------------------------+
        v                           v
  [4a] STRUCTURED TOOLS       [4b] EVIDENCE RETRIEVAL
       Layer A exact facts          hybrid: full-text + embedding
       Layer B intelligence         permission filter BEFORE ranking
        |                           |
        +---------------------------+
        |
  [5] EVIDENCE PACKING
        dedup, rank, token budget, provenance retained
        |
  [6] MODEL GATEWAY
        serving mode, egress plan, minimum scoped payload
        |
  [7] LLM  (ModelServingRuntime)
        phrasing only
        |
  [8] ANSWER VERIFICATION
        every numeric claim resolves to a supplied evidence handle
        claim class not upgraded
        |
  [9] cited answer   |   or a refusal with its reason
```

### 9.2 Step 3, the deterministic tool planner

**The LLM does not choose tools.** A planner maps resolved intent plus entity types to a declared tool set from a registry. This is testable: tool-selection accuracy is measured against a labelled question set (Q-01). A model choosing tools freely produces a different plan on a rephrasing and cannot be gated.

Where intent is ambiguous, the planner **asks** rather than guessing, and the ambiguity is recorded.

### 9.3 Step 4b, hybrid retrieval and the order that matters

| Stage | Rule |
|---|---|
| **Permission filter** | **Applied before ranking, not after.** Filtering after ranking means a high-scoring forbidden chunk displaces a permitted one and the answer silently loses evidence it was entitled to. Chapter 3's `assistant_chunks.role_scope` is the mechanism |
| Lexical retrieval | Full-text, for exact codes, identifiers and rare terms where embeddings underperform |
| Semantic retrieval | Embedding search over permitted chunks |
| Fusion | Reciprocal rank fusion over both lists |
| Re-ranking | **Optional, and only if benchmarked.** A cross-encoder adds latency; it ships only if citation correctness improves enough to pay for it (B-08) |

**Structured tools take precedence over retrieval for facts and analytical results.** Retrieval supplies documents and context. A number never comes from a retrieved chunk when a tool can compute it.

### 9.4 Step 5, evidence packing under a token budget

| Rule | Reason |
|---|---|
| Deduplicate by content hash | The same finding retrieved through two paths wastes budget |
| Rank by tool-result priority, then fusion score | Engine output outranks a document |
| **Hard token budget with a reserved answer allowance** | Context overflow silently drops evidence, which produces an ungrounded sentence that the guard then rejects, wasting a whole round trip |
| Every packed item retains its evidence handle | Step 8 cannot verify what it cannot resolve |
| **Truncation is recorded and disclosed** | An answer built on a truncated evidence set says so |

### 9.5 Step 6, the model gateway and egress

Chapter 6's gateway already routes by serving mode and enforces the egress plan. Added rule: **the payload sent to an external provider is the minimum scoped evidence needed for the phrasing task**, never a whole retrieval set and never raw canonical rows.

Serving modes are unchanged: self-hosted default, private endpoint per tenant policy, customer model. **A provider or model change is a governed release event**, recorded with a reason, because it changes answer behaviour without any code change.

### 9.6 Step 7, `ModelServingRuntime`

A replaceable abstraction with load, unload, generate, stream, health and capability reporting. vLLM or an equivalent may be benchmarked as an implementation. **No serving library is the product contract**, for the same reason FAISS is not.

### 9.7 Step 8, answer verification

The no-fabrication guard of Chapter 4 5.7.3 runs before display. Its operational definition:

| Check | Failure behaviour |
|---|---|
| Every numeric claim resolves to a handle in the supplied evidence | Reject before display |
| No claim class is upgraded by language: an association is not phrased as a cause | Reject before display |
| No refusal is replaced by a phrased answer | Reject before display |
| A transport failure is red and a refusal is amber | Never conflated |

**The verifier is deterministic and does not call the LLM.** A model checking its own output is not a guard.

### 9.8 Assistant quality gates

| ID | Gate | Measured on |
|---|---|---|
| Q-01 | Tool-selection accuracy | Labelled question set |
| Q-02 | Groundedness: fraction of claims with a resolving handle | Sampled answers |
| Q-03 | Citation correctness: the handle supports the claim | Human-labelled sample |
| Q-04 | Unsupported-claim rate | Sampled answers |
| Q-05 | Refusal correctness: refuses when it should, answers when it should | Adversarial set including the unit-sanity probes |
| Q-06 | **Causal-overreach rate**: association phrased as cause | Adversarial set |
| Q-07 | Multilingual fidelity | Per supported language |
| Q-08 | Time to first token | p50, p95 |
| Q-09 | Total answer latency | p95, against the under-2-minute ceiling |
| Q-10 | Serving throughput | Concurrent sessions per node |
| Q-11 | Memory and VRAM per concurrent session | Sizing input |

**Q-05 and Q-06 are the two that decide credibility.** A speed question answered in a unit of mass, or an association phrased as a cause, destroys the intelligence claim in one sentence, and both are testable with a fixed probe set before any customer sees them.

---

## 10. TERMINOLOGY

**Do not describe MF-01 to MF-07 as seven ML models.** Three of the seven are not models.

| ID | Family | Sub-type | Refresh policy follows from the sub-type |
|---|---|---|---|
| MF-01 | Process encoder | **Learned model** | Governed refresh, frozen between |
| MF-02 | Similarity index | **Retrieval and index** | Generational extension, policy rebuild |
| MF-03 | Normal and novelty | **Learned model** | Weekly refit on rolling window |
| MF-04 | Supervised outcome | **Learned model** | Weekly retrain plus recalibration |
| MF-05 | Effect and envelope | **Statistical engine** | Weekly recompute, no training |
| MF-06 | Statistical intelligence (DF9) | **Statistical engine** | Weekly recompute, no training |
| MF-07 | Practice learning | **Practice engine** | Weekly recompute, governed signature version |

Plus **orchestration and governance**: the capability profiler, the model-count governor, the supervisor.

Correct collective term: **seven intelligence and engine families**. The sub-type is load-bearing, not cosmetic: it determines refresh policy, lane assignment and whether a champion/challenger gate applies at all.

---

## 11. GATES ADDED BY THIS PASS

| ID | Gate | Blocking |
|---|---|---|
| **G-48** | **Training reads no live feature state.** No training or encoding code path issues a query against `feature_store`; training input resolves only through a sealed snapshot artifact. **The snapshot materialiser is exempt by definition**: reading `feature_store` is precisely how it seals the artifact, and it is the only component permitted to do so | Build-time and runtime, **yes** |
| **G-49** | **Lane isolation.** No `ml.online_scoring` process imports a trainer module; online scoring capacity cannot be consumed by `ml.training` or `ml.batch_scoring` admission | Build-time and admission, **yes** |
| **G-50** | **Admission predicate satisfiable.** For every lane, `max(compute_weight of any admissible job) <= resource_capacity`. A configuration where a declared job can never be admitted fails the gate | Configuration validation, **yes** |
| **G-51** | **ANN recall floor.** Every index build measures recall@k against exact Flat on the representative sample; a build below the declared floor does not become the served index | Index build, **yes** |
| **G-52** | **Evidence budget integrity.** No packed evidence item lacks a resolvable handle; truncation is recorded and disclosed | Serving, **yes** |
| **G-53** | **Claim-class integrity in language.** No answer phrases a lower claim class as a higher one; measured by Q-06 against a fixed adversarial set | Release, **yes** |
| **G-54** | **Governed-model-only learned output.** No feature, score, statistic or value derives from free-form or model-generated text | Build-time and training, **yes** |
| **G-55** | **Manifest immutability and coverage.** A `semantic_manifests` row is never updated; identical content within a tenant never creates a second row; **every new governed AI/ML execution resolves a manifest**, legacy records excepted | Database trigger plus run admission, **yes** |

Total inventory becomes **G-01 to G-55**.

---

## 12. REJECTED ADDITIONS

Nine candidates considered and refused. Each would have increased feature count without clearing 0.1.

| Candidate | Why refused |
|---|---|
| **Graph neural networks over the genealogy graph** | The genealogy relation is already exploited by weighted attribution in feature engineering and by the genealogy-attributed correlation block. A GNN would need to beat that measurably on a labelled task nobody has run. **Revisit only with a benchmark showing lift over weighted roll-up** |
| **Reinforcement learning for setpoint recommendation** | Requires an environment model or online interaction with a live plant. The product is read-only toward the plant by ruling. **Structurally incompatible, not merely premature** |
| **Autonomous agents acting on the plant** | Same. The product produces evidence for human decision and never instructs |
| **Online weight updates** | Silent, unattributable drift. Full retrain on a governed rolling window is cheaper to reason about and reproducible |
| **Continuous retraining** | No reproducibility anchor, no champion/challenger window, no way to explain why an answer changed between two Mondays |
| **Multiple embedding models by default** | Doubles index cost and creates two similarity spaces with no rule for reconciling them. One space, replaceable by governed refresh |
| **Multi-agent orchestration in the Assistant** | The deterministic planner of 9.2 is testable; multi-agent is not, and tool-selection accuracy is a gate |
| **Automatic plant write-back** | Prohibited by ruling and by G-35 |
| **A knowledge graph layer above the relationship model** | `plant_relationships` plus `plant_relationship_paths` already is the graph, with one resolver and sixteen declared consumers. A second graph would be a competing join authority |

---

## 13. PROPOSED AMENDMENTS, BY DOCUMENT

### 13.1 Chapter 2 - product semantics

| # | Section | Change | Reason |
|---|---|---|---|
| C2-1 | 3.10 capability classification | Record that operational scoring latency is guaranteed by **reserved serving capacity**, not by pool ordering | The Core actionable-latency claim currently depends on a scheduler that can violate it |
| C2-2 | Glossary | Add **Semantic Contract Manifest**, and **intelligence and engine families** with the five sub-types | Naming authority is Chapter 2 |

**No product scope changes.** No capability is added or removed.

### 13.2 Chapter 3 - persistence

| # | Section | Change | Reason |
|---|---|---|---|
| C3-1 | 4.5.12 `feature_snapshots` | State that `storage_uri` points at a **typed columnar artifact** with a declared format and content hash, and that it is the authoritative training input | D-03 |
| C3-2 | 4.5.12 `feature_snapshot_rows` | Demote to an **optional audit sample** with a declared sampling rate | D-10, conditional on B-03 |
| C3-3 | 4.5.12 | Add **`ppiq_plant.sequence_manifests`**, manifest only, payload in object storage | D-04 |
| C3-4 | 4.5.11 area | Add **`ppiq_meta.semantic_manifests`**, content-addressed, no lifecycle | D-02 |
| C3-5 | 4.5.12 | Add nullable `semantic_manifest_hash` to `model_registry`, `feature_snapshots`, `compute_runs`, `prediction_runs`, `practice_learning_runs` | D-02 |
| C3-6 | 4.5.12 | Add `recall_at_k`, `recall_probe_size` and `index_policy` to the index generation record | D-06 |

### 13.3 Chapter 4 - execution and model behaviour

| # | Section | Change | Reason |
|---|---|---|---|
| C4-1 | **5.3.2 mechanism 4** | **Separate `max_concurrency` from `resource_capacity`.** Admission requires both predicates | **Fixes an unsatisfiable predicate** |
| C4-2 | **5.3.2 pool table** | `ml` resolves to three lanes: `ml.training`, `ml.batch_scoring`, `ml.online_scoring`, the last with reserved capacity | Protects the Core actionable-latency requirement |
| C4-3 | 5.3.2 mechanism 9 | Training is pre-emptible at a checkpoint when a reserved lane needs capacity | D-01 |
| C4-4 | **5.6.5 model governance** | Promotion is a three-dimensional gate: quality, serving cost, training cost. Calibration and explanation stability become blocking criteria | D-05 |
| C4-5 | 5.6.2 to 5.6.3 | The encoder promotion inequality of 6.2 | D-11 |
| C4-6 | **5.8.6 boundary rule** | Replace with the governance-based boundary of 8.2 | **Resolves the contradiction with 5.8.7** |
| C4-7 | **5.7 new subsections** | The Assistant runtime of section 9: planner, hybrid retrieval with permission-before-ranking, evidence packing, `ModelServingRuntime`, verification, and gates Q-01 to Q-11 | The Assistant is Core and has no runtime specification |

### 13.4 Chapter 6 - deployment and resource topology

| # | Section | Change | Reason |
|---|---|---|---|
| C6-1 | Container inventory | Split background workers into `ppiq-worker`, **`ppiq-ml-train`** (GPU-capable, pre-emptible) and **`ppiq-ml-online`** (reserved, warm cache) | D-01 |
| C6-2 | Resource model | Declare the reserved share for `ml.online_scoring`, alongside the existing `interactive` reservation | D-01 |
| C6-3 | Backup set | Confirm the feature-snapshot and sequence artifact stores are in the backup set with their own retention | D-03, D-04 |
| C6-4 | Model gateway | Minimum-scoped-payload rule for external providers; provider or model change as a governed release event | D-08 |
| C6-5 | Sizing | Add snapshot read throughput and warm-model memory to the capacity model | B-03, B-05 |

### 13.5 Layer B Rule

| # | Change |
|---|---|
| R-1 | Appendix A.6: replace the absolute text boundary with the governance-based boundary of 8.2 |
| R-2 | Appendix A.6: record the three-lane `ml` resolution and the concurrency-versus-capacity separation |
| R-3 | Appendix A: rename the seven families to **intelligence and engine families** with sub-types |
| R-4 | Appendix A: record the Semantic Contract Manifest as a reproducibility pin, explicitly not a fourth authoring authority |

### 13.6 Layer B Architecture Pack

| # | Change |
|---|---|
| P-1 | SM-01 loses its lifecycle and becomes the Semantic Contract Manifest |
| P-2 | DP-2 gains the sealed columnar artifact as the training read path; G-48 added |
| P-3 | DP-3 becomes a manifest plus object-storage payload |
| P-4 | Section 38 and 47 adopt the three-lane resolution; G-49 and G-50 added |
| P-5 | MF-02 gains the policy selector and the permanent exact-Flat baseline; G-51 added |
| P-6 | Section 29 and 46 adopt the governance-based boundary; G-54 added |
| P-7 | Section 11 gains the runtime pipeline of section 9; G-52 and G-53 added |
| P-8 | Section 6 and 17 adopt the three-dimensional promotion gate |
| P-9 | Terminology corrected throughout |

---

## 14. BENCHMARK REGISTER

Every parameter whose winner depends on hardware or data is left open **with its benchmark defined**, rather than guessed.

| ID | Question | Method | Decides | Blocks |
|---|---|---|---|---|
| **B-01** | `resource_capacity` and `compute_weight` per lane | Instrument peak RAM, CPU seconds and GPU seconds per job class on a representative population; set capacity so the heaviest declared job is admissible with headroom | Lane sizing | C4-1, C4-2 |
| **B-02** | Reserved share for `ml.online_scoring` | Load-test event scoring at target arrival rate while `ml.training` is saturated; find the reservation that holds p95 inside the actionable-deadline budget | Reservation fraction | C6-2 |
| **B-03** | Parquet versus Arrow IPC, and whether `feature_snapshot_rows` can be demoted | Load the same population by both paths and by JSONB; measure epoch time, peak RAM, storage size, seal time | Snapshot format, C3-2 | C3-1, C3-2 |
| **B-04** | Sequence chunk size and compression | Vary chunk size; measure loader throughput, storage amplification and random-access cost | Chunk policy | C3-3 |
| **B-05** | Encoder lift versus its serving cost | Train supervised models with and without embedding columns on the same snapshot; measure lift, p95 latency delta, artifact size, VRAM | Whether MF-01 ships | C4-5 |
| **B-06** | ANN family per size class | Build Flat, HNSW and IVF-PQ on representative populations; measure recall@k against Flat, p95 latency, build time, RAM | `index_policy` thresholds | C3-6 |
| **B-07** | Token budget and evidence-set size | Vary packed evidence size; measure groundedness, citation correctness and answer latency | Budget policy | C4-7 |
| **B-08** | Whether re-ranking earns its latency | With and without a cross-encoder; measure citation correctness delta against added p95 | Ship or drop re-ranking | C4-7 |
| **B-09** | Serving runtime and concurrency | Benchmark candidate runtimes at target concurrency; measure time-to-first-token, throughput, VRAM per session | Runtime selection, sizing | C6-5 |

**No threshold in this pass is asserted without a benchmark that would falsify it.**

---

## 15. FREEZE CRITERION ASSESSMENT

The criterion is not that documents match each other. It is that the best target architecture is selected and all documents are synchronised to it, with no high-impact unresolved question in twelve areas.

| # | Area | State | Note |
|---|---|---|---|
| 1 | Semantic reproducibility | **RESOLVED** | Manifest, D-02 |
| 2 | Training-data layout | **RESOLVED** | Sealed columnar artifact, D-03. Format is B-03, a parameter not a question |
| 3 | Sequence-data layout | **RESOLVED** | Manifest plus object storage, D-04 |
| 4 | Model selection | **RESOLVED** | Three-dimensional gate plus the encoder inequality, D-05, D-11 |
| 5 | Prediction serving | **RESOLVED** | Warm models, reserved lane, cold-start bound, D-01 |
| 6 | Resource isolation | **RESOLVED** | Three lanes, concurrency separated from capacity, D-01 |
| 7 | Operational scoring latency | **RESOLVED** | Independent budget and reservation, D-01 |
| 8 | ANN policy | **RESOLVED** | Policy selector plus permanent exact baseline, D-06 |
| 9 | LLM grounding | **RESOLVED** | Deterministic planner, permission-before-ranking, budgeted packing, deterministic verifier, D-08 |
| 10 | LLM serving | **RESOLVED** | `ModelServingRuntime`, gateway unchanged, egress minimised, D-08 |
| 11 | Model and LLM evaluation | **RESOLVED** | Three-dimensional promotion plus Q-01 to Q-11, D-05, D-08 |
| 12 | Deployment ownership | **RESOLVED** | Chapter 6 amendments C6-1 to C6-5 |

**Twelve of twelve resolved at the architecture level. Nine parameters remain open as benchmarks with defined methods.**

### 15.1 The honest qualifier

This document proposes amendments to Chapters 2, 3, 4 and 6. **Those chapters have not been amended.** Until the amendments are ruled and applied, the Master Design and this target architecture disagree in the places section 13 names, and the disagreement is deliberate and recorded rather than hidden.

**Therefore the status of this pass is:**

```
AI/ML/LLM TARGET ARCHITECTURE OPTIMISED
MASTER DESIGN AMENDMENTS PROPOSED, NOT YET APPLIED
```

The final status you named becomes available once the section 13 amendments are ruled and written into Chapters 2, 3, 4 and 6 and the Rule and Pack are synchronised to them. **That is the one remaining step, and it is a ruling plus a mechanical edit, not another design round.**

### 15.2 The three amendments I would rule first

| Priority | Amendment | Why first |
|---|---|---|
| **1** | **C4-1 and C4-2**, lane separation | The current predicate is unsatisfiable and the Core latency claim is unprotected. This is a live defect, not an improvement |
| **2** | **C4-6**, the modality boundary | Two chapter clauses currently contradict each other, and an implementer reading 5.8.7 will build something 5.8.6 forbids |
| **3** | **C3-1**, the training read path | Every day this is unstated is a day the first implementation may bind training to JSONB and have to be rewritten |

---

*Optimisation pass produced 11 August 2026 against Chapters 1 to 6, the Layer B Rule and Architecture Pack Revision 6.3. Eleven material decisions, eight gates added, nine additions refused, nine benchmarks defined, twenty-two amendments proposed across six documents. Implementation prohibited.*
