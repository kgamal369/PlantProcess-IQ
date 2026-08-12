# PPIQ LAYER B RULE - REVISION 7

## Learned Industrial Intelligence Engine

**Status: ACTIVE. This is the single implementable body. No overrides to apply.**
**11 August 2026. Supersedes the original rule and its appendices A, A.6 and B in full; those are archived in `PPIQ_Layer_B_Rule_HISTORICAL_ARCHIVE.md` and are non-implementable.**
**Implementation status: DESIGN AUTHORITY. This rule does not authorise implementation.**

> **Authority order.** Chapter 2 governs naming, structure and positioning. Chapter 3 governs the technical contract and persistence. Chapter 4 governs engine, authoring and execution behaviour. Chapter 6 governs deployment and resource topology. This rule is a subsystem constitution subordinate to all four. The Architecture Pack is the implementation blueprint subordinate to this rule.
>
> **One answer per contract.** Every clause below states the canonical answer. Nothing in this document is cancelled by anything else in it.

---

## 1. PURPOSE

Layer B turns a customer's historical and continuously arriving plant data into: plant fingerprint, anomaly and novelty knowledge, process-outcome relationships, attributable risk, practice learning, operating envelopes, early prediction, historical similarity, evidence-supported remediation suggestions, machine-readable intelligence datasets for dashboards, and evidence endpoints for the Assistant.

**It must be generic across industries.** Fleet-v2 and steel are one test dataset. Oil, mineral water, pharma, paper, tyres, food and unknown future industries use the same engine architecture.

---

## 2. THE SEMANTIC WALL

**Layer B contains no customer table name, no column name, no schema name and no industry term.**

Forbidden in any Layer B code path:

```
read Coil table        read Heat table        read CastingSpeed
if (customer == ...)   class OilModel         class BottleModel
```

Customer physical schemas are handled above the intelligence engine. The customer configures data through the PPIQ authoring surfaces: the no-code wiring canvas, relationship declarations, governed SQL where needed, the dataset registry, and parameter and outcome definitions.

**Layer B consumes only published canonical contracts:**

```
CUSTOMER SOURCES
  -> NO-CODE WIRING / GOVERNED SQL
  -> PUBLISHED definition_versions
     + PUBLISHED plant_relationships (emitted by publishing the transformation)
     + GOVERNED registry and configuration state
  -> pinned for reproducibility by a SEMANTIC CONTRACT MANIFEST
  -> CANONICAL / SPINE / FEATURE REPRESENTATION
  -> LEARNED INTELLIGENCE ENGINE
```

Enforced in three layers: the Layer B database role holds grants on Plant Data and the intelligence schema only; every source reference resolves through a published `definition_version` and every entity correspondence through `RelationshipResolver`; and an architecture test asserts no Layer B file contains a customer identifier or an industry noun, falsified once before it is trusted.

---

## 3. THE SEMANTIC CONTRACT MANIFEST

**`ppiq_meta.semantic_manifests` is an immutable, content-addressed reproducibility pin. It is not an authoring authority and has no lifecycle.**

| Column | Type | Notes |
|---|---|---|
| `manifest_id` | uuid **PRIMARY KEY** | The handle artifacts reference |
| `tenant_id` | uuid NOT NULL | |
| `manifest_hash` | varchar(64) NOT NULL | Content hash over the referenced versions |
| `definition_versions` | jsonb NOT NULL | `{definition_id, version_number}` array |
| `relationship_source_definition_id` | uuid NOT NULL | |
| `relationship_source_definition_version` | integer NOT NULL | |
| `registry_snapshot_hash` | varchar(64) NOT NULL | Over the registry rows in force |
| `configuration_hash` | varchar(64) NULL | Governed configuration affecting semantics |
| `created_at_utc` | timestamptz NOT NULL | |

**UNIQUE `(tenant_id, manifest_hash)`.** Identical content within a tenant never creates a second row. Identical content across two tenants correctly creates two rows, because a manifest is tenant-owned evidence.

**No status column. No draft, validated, published or rolled-back state. Nothing updates a manifest.**

`definition_versions`, the relationship publication and `model_registry` retain their lifecycle authority unchanged. The manifest records which versions were in force; it does not govern them.

**Coverage.** Run, artifact and evidence tables carry `semantic_manifest_id uuid NULL FK`. **Nullable for legacy records only. Every new governed AI/ML execution must resolve a manifest**; a run that cannot is refused rather than recorded without one.

---

## 4. THE RELATIONSHIP MODEL

**Chapter 2 3.15 positions it. Chapter 3 4.5.10 implements it: `plant_relationships`, `plant_relationship_members`, `plant_relationship_paths`, versioned by `source_definition_id` and `source_definition_version` with an effective and retired lifecycle. Publishing the transformation emits the model.**

**No statistical, feature, ML, prediction, practice, remediation, value, Assistant or evidence engine owns a private join.** One resolver serves all sixteen consumers through `GET /api/relationships/resolve?from=&to=&purpose=`.

Four behavioural rules: ambiguity refuses rather than guesses; `validation_state = unproven` permits `explore` and refuses `train`; grain conversion requires attribution weights summing to 1.0; and a relationship is deactivated, never deleted.

---

## 5. TRUTH CONTRACTS

**Layer A** produces exact facts: count, sum, grouped KPI, historical totals, exact filtered population.

**Layer B** produces learned estimates: risk, similarity, anomaly, attribution, prediction, operating envelope, learned effect, recommendation confidence.

**Never use ML to approximate an exact BI fact because the dataset is large.** The Assistant may combine both, clearly labelled.

Every output classifies itself as **ASSOCIATION**, **PREDICTIVE CONTRIBUTION**, **MATCHED EFFECT ESTIMATE** or **CAUSAL EVIDENCE**. **A claim class is never upgraded by language.**

Terminal states: **FINDING, INSUFFICIENT DATA, NOT APPLICABLE, REFUSED BY GUARD, CONTRADICTED BY CONTROL, MODEL NOT READY**, expressed through the canonical error codes. **Never turn a method limitation into a false statement about customer data.**

---

## 6. DATA PRODUCTS AND THE TRAINING PATH

Persistent governed products, not repeated scans of source tables: journey spine, feature store, sequence store, outcome store, evidence store, prediction store, embedding and index metadata.

### 6.1 The training read path

**PostgreSQL JSONB is not the training read path.**

```
live governed feature state     ppiq_plant.feature_store, jsonb, incremental, RLS
      |  seal
      v
immutable snapshot manifest     feature_snapshots: storage_uri, artifact_format,
      |                         artifact_content_hash, artifact_byte_size
      |  materialise
      v
typed columnar artifact         object storage. Format selected by B-03
      |  bounded read, projection pushdown
      v
Python data loader              PyTorch / LightGBM input
```

`feature_store` owns current governed state, lineage, row-level security and incremental refresh by watermark. **The sealed artifact owns high-throughput training input.**

**No training or encoding component queries `feature_store`. The snapshot materialiser is exempt by definition** and is the only component permitted to read it for sealing.

`feature_snapshot_rows` is an optional audit sample, not the authoritative copy.

### 6.2 The sequence path

**`ppiq_plant.sequence_manifests`** in PostgreSQL holds the manifest: subject identity, `channel_set_version`, time range, sample and channel counts, completeness, content hash, storage URI, chunk index.

**Object storage holds the payload**: immutable chunked typed numeric arrays, compressed, partitioned, memory-mappable where the format allows. The loader consumes bounded chunks. **No numeric sequence payload is stored in PostgreSQL.**

---

## 7. THE SEVEN INTELLIGENCE AND ENGINE FAMILIES

**Not seven ML models. Three of the seven are not models, and the sub-type determines lane, refresh policy and whether a champion/challenger gate applies.**

| ID | Family | Sub-type | Lane | Champion/challenger |
|---|---|---|---|---|
| MF-01 | Process encoder | Learned model | `ml.training` | Yes, plus the promotion inequality |
| MF-02 | Similarity index | **Retrieval and index** | `ml.training` to build | No. Gated on measured recall@k |
| MF-03 | Normal and novelty | Learned model | `ml.training` | Yes |
| MF-04 | Supervised outcome | Learned model | `ml.training` | Yes, three-dimensional |
| MF-05 | Effect and envelope | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-06 | Statistical intelligence | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-07 | Practice learning | **Practice engine** | `analysis` | No. Governed signature version |

Plus **orchestration and governance**: the capability profiler, the model-count governor and the supervisor.

**Framework and implementation choices are replaceable and benchmark-driven.** PyTorch behind a `ProcessEncoder` abstraction; **`VectorSimilarityIndex` is the contract and FAISS is one implementation candidate**; `SupervisedOutcomeModel` with LightGBM as the initial tabular candidate; TreeSHAP as the initial explanation mechanism.

**A mandatory simple baseline is trained first.** A complex model ships only when it beats the baseline on the three-dimensional gate.

---

## 8. MODEL REGISTRY, ACTIVATION AND ROLLBACK

**`ppiq_plant.model_registry`, governed per serving identity. There is no bundle object.**

```
serving identity = ( tenant_id , model_code , outcome_code , grain_code )
serving version  = serving identity + model_version
```

`outcome_code` and `grain_code` are model identity, not metadata.

**Two independent axes:** `status` in `trained, rejected, active, review, retired`, and `serving_role` in `none, serving_fallback`.

Constraints: at most one `active` per serving identity; at most one `serving_fallback` per serving identity; a retired, rejected or under-review model can never hold a fallback approval; **one version can never be both primary and fallback**, because a fallback that is already the primary masks the absence of a safety net. Every UNIQUE carries `tenant_id` first.

**A fallback is never inferred from the last active version.** Use is recorded and surfaced through `prediction_current.fallback_in_use`; silently serving a fallback as primary is prohibited.

`model_training_runs` carries **CHECK `overlap_rows = 0`**, making leakage a database-level impossibility rather than a test.

### 8.1 Promotion is a three-dimensional gate

On the same governed recent holdout as the incumbent:

**QUALITY** - discrimination or error, **calibration**, out-of-time performance, subgroup and regime stability, missingness robustness, **explanation stability**.

**SERVING** - p50, p95 and p99 inference latency, throughput, artifact size, RAM and VRAM, warm-up time.

**TRAINING** - duration against the weekly window, peak memory against lane capacity, snapshot read throughput.

**A better-discriminating, worse-calibrated model is not an improvement** for a product whose output is a risk band a human acts on. **An unstable explanation is worse than none**, because contributors are presented as evidence.

**The encoder ships only when it earns its operational cost:**

```
promote_encoder  iff  metric_lift            >= declared_min_lift
                 AND  p95_latency_delta      <= declared_latency_budget
                 AND  artifact_size          <= declared_size_class
                 AND  explanation_stability  >= floor
```

If engineered features match it within the lift threshold, **the engineered features ship**.

---

## 9. VECTOR SEARCH

`VectorSimilarityIndex` with build, seal, extend, search and recall_probe is the product contract. **FAISS, HNSW, IVF, PQ, quantised and GPU-backed variants are implementations selected by measurement.** No library name appears in the contract.

Index family is selected from population size, vector dimension, available RAM, required recall@k, p95 latency target, build time and update pattern.

**Exact Flat search is retained permanently on a representative sample as the recall baseline.** Recall@k is measured on every build and stored. **A build below the declared recall floor does not become the served index.** An approximate index whose recall has never been measured is an unquantified error source presented as a plant fingerprint.

---

## 10. PREDICTION, REMEDIATION AND DECISION

**Prediction cutoff is structural.** Features available after the cutoff are prohibited; a model with excellent metrics caused by future information is a failed model.

**Operational delivery.** `prediction_current` carries the actionable deadline, remaining stage state, scoring mode, delivery latency and fallback state. A prediction that arrives after its last actionable stage is a record, not an intervention.

**Remediation is a nine-check gate** with four outcomes: **actionable** (all nine pass), **evidence_only** (checks 5 to 9 pass, one or more of 1 to 4 fail for this unit), **exploratory** (checks 1 to 6 pass, 7 or 8 fails), **suppressed** (safety check 4 fails, not shown, audited).

**`can_accept` is the complete seven-condition server-side acceptance authority and is not a synonym for actionable.** The client reads `can_accept` alone and must not re-derive any condition. Accept, Reject and Defer exist only where it is true.

**Escalation is a record, never a decision.** It creates no action row, contributes to no effectiveness row and is excluded from feedback.

**Prediction evaluation excludes intervened instances** from accuracy metrics and reports them separately, because a prevented event is not a false positive.

**Value** carries mandatory bounds when the basis is sufficient, with a point estimate permitted beside them, per-tenant currency, and abstention when the basis is absent. Potential impacts are non-additive and are never summed into a total saving.

---

## 11. THE MODALITY BOUNDARY

**The boundary is governance, not modality.**

> **No free-form or model-generated output may become a feature, a score, a statistic or a value.** Text and images may enter a learned result **only** through an explicitly authored model definition carrying the full training contract: a versioned immutable snapshot, declared leakage controls, held-out validation, a `model_registry` entry, calibration and drift monitoring.
>
> Retrieval-derived and LLM-derived content is **evidence only**: it may corroborate a deterministic result and may never originate one.

**Path A, evidence modality.** Operator notes, shift logs, maintenance text, documents. Indexed, retrieved, cited. Never a feature, never a score, never a plant fact the model originated.

**Path B, governed multimodal ML.** The full contract above. This is how an inspection-image model produces an annotation with a confidence under the same activation, retirement and drift rules as any model.

**No implementation scope is added by this boundary.** Both modalities remain interface-designed, future implementation.

---

## 12. EXECUTION LANES AND ADMISSION

**Six logical job classes. The `ml` class resolves to three physical lanes.**

| Lane | Reserved | Pre-emptible | Admits |
|---|---|---|---|
| `ml.training` | no | **yes** | Encoder and supervised training, calibration, SHAP batch, index build |
| `ml.batch_scoring` | no | yes | Scheduled scoring, backfill, rescore after activation |
| **`ml.online_scoring`** | **yes** | **no** | **Event and micro-batch scoring and its required serving functions only** |

**Admission requires both predicates:**

```
admit  iff  running_count < max_concurrency
       AND  sum(compute_weight of running) + compute_weight(candidate) <= resource_capacity
```

`max_concurrency` is how many runs may be in flight; `resource_capacity` is how much scarce resource exists; `compute_weight` is what one run consumes. **One number never expresses two quantities.**

**The online reservation is never available to training or batch admission.** Batch, backfill and rescore work runs on batch and training-class capacity, never on the online container. Where hardware is physically shared, online capacity remains hard-reserved and **B-02 must prove the actionable-latency target while training and batch are saturated**.

**Warm models** for every active serving identity are resident and reference-counted; a newly activated model is warmed before it serves. **Training yields at its next checkpoint** when a reserved lane needs capacity.

**Daytime serving performs no training.** Tier 1 precomputed reads target seconds; tier 2 bounded computation on prepared stores targets under 30 seconds; tier 3 schedules or refuses. **The absolute synchronous ceiling is under 2 minutes.**

---

## 13. THE ASSISTANT RUNTIME

**The Assistant is an orchestrator and communicator over governed tools. It never computes, never originates a figure and never replaces an engine refusal.**

```
[1] permission and tenant context      [2] intent and entity resolution
[3] DETERMINISTIC TOOL PLANNER         [4a] structured tools  [4b] evidence retrieval
[5] token-budgeted evidence packing    [6] model gateway      [7] LLM, phrasing only
[8] deterministic answer verification  [9] cited answer or refusal
```

**The LLM does not choose tools.** A planner maps resolved intent to a declared tool set; tool-selection accuracy is gated. Where intent is ambiguous the planner asks rather than guessing.

**Hybrid retrieval with the permission filter applied before ranking**, not after, so a forbidden chunk cannot displace a permitted one. **Structured tools take precedence over retrieval for facts and analytical results**; a number never comes from a retrieved chunk when a tool can compute it. Re-ranking is optional and ships only if it earns its latency.

**Evidence packing** deduplicates, ranks engine output above documents, enforces a hard token budget with a reserved answer allowance, retains every evidence handle, and **records and discloses truncation**.

**The gateway sends the minimum scoped evidence** to an external provider, never a whole retrieval set and never raw canonical rows. **A provider or model change is a governed release event.**

**`ModelServingRuntime`** is a replaceable abstraction; no serving library is the product contract.

**Answer verification is deterministic and does not call the LLM**, because a model checking its own output is not a guard. Every numeric claim must resolve to a supplied handle; no claim class is upgraded; no refusal is replaced by a phrased answer; a transport failure is never dressed as an abstention.

Quality gates Q-01 to Q-11. **Q-05 refusal correctness and Q-06 causal-overreach rate decide credibility.**

---

## 14. OUTPUT DATASETS AND BINDING

**Seven governed intelligence dataset families**: prediction, contributor, similarity, anomaly, envelope, finding and effect, and **model and readiness status**. The seventh is what a new installation binds to before any model is ready, so it renders truthfully rather than appearing broken.

Intelligence sources are declared in `registry_intelligence_sources` with `sourceKind = 'intelligence'`, an entity link column and `columnRoles`. The widget execution contract is **columns, rows and warnings**.

**Two source classes.** Fact-shaped aggregate sources may project through `WidgetFact` into the generic aggregate executor. **Native-grain rich sources keep their declared columns and are never flattened into a single value column.** Aggregation policy governs which native columns may be aggregated.

**No ML-specific widget type. No branch on dataset origin.**

---

## 15. GOVERNANCE

**Tenant isolation is absolute.** Models, embeddings, neighbours and evidence are tenant-scoped. No cross-tenant vector index, no cross-tenant training population, no cross-tenant benchmarking.

**Reproducibility.** Given tenant, manifest, feature set version, training window and model version, the model is reproducible and a changed answer is explicable. Deterministic seeds where practical, immutable dataset manifests, code identity, environment manifest, artifact hashes.

**The supervisor** observes, proposes a bounded adjustment, shadow-runs it against held-out history, compares, requires **human approval**, and applies atomically with provenance and a rollback pointer. It may **never** modify readiness thresholds, refusal rules, evidence requirements, leakage gates, tenant isolation, the semantic contract or the forbidden-combination set. **A component that can improve results by lowering the bar for what counts as a result will eventually do so.** It records abstention as well as action.

**PPIQ writes only to its own governed stores.** Never to a customer source system, never to a control system, never to a setpoint. An accepted recommendation records that a human acted; it does not act.

**Gate inventory: G-01 to G-55.** No model reaches production because training completed. Every gate is falsified once before it is trusted.

---

## 16. BENCHMARK PARAMETERS

Nine values stay open until measured. **No number in this rule is guessed.**

| ID | Question |
|---|---|
| B-01 | `max_concurrency`, `resource_capacity`, `compute_weight` per lane |
| B-02 | Online scoring reservation fraction |
| B-03 | Columnar snapshot format; whether the audit sample can be demoted |
| B-04 | Sequence chunk size and compression |
| B-05 | Encoder lift versus its serving cost |
| B-06 | ANN family per size class |
| B-07 | Token budget and evidence-set size |
| B-08 | Whether re-ranking earns its latency |
| B-09 | Serving runtime and concurrency |

---

## 17. ACCEPTANCE PRINCIPLE

> A completely new industrial customer can map their own data through PPIQ's no-code semantic authoring, run commissioning, obtain only the intelligence their data genuinely supports, receive weekly governed model updates, ask questions during production hours in less than two minutes, and bind learned outputs to ordinary PPIQ widgets without a developer writing industry-specific ML code.

**Build the generic learned intelligence contract. The algorithms sit behind it and can evolve.**

---

*Layer B Rule Revision 7, 11 August 2026. One active body, no overrides. The original rule and its appendices are archived in `PPIQ_Layer_B_Rule_HISTORICAL_ARCHIVE.md` and are non-implementable.*
