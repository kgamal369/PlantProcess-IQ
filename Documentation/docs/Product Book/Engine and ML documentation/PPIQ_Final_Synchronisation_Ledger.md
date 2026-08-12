# PPIQ - FINAL SYNCHRONISATION LEDGER

**11 August 2026. Every contract changed by the AI/ML/LLM target architecture pass, with its reason and its benchmark where the value stays parametric.**

**Status after this ledger:**

```
AI/ML/LLM TARGET ARCHITECTURE OPTIMISED
MASTER DESIGN SYNCHRONISED
READY FOR IMPLEMENTATION DECOMPOSITION
```

---

## 1. RESOURCE ARCHITECTURE

| # | OLD CONTRACT | NEW CONTRACT | DOCUMENT / SECTION | REASON | BENCHMARK |
|---|---|---|---|---|---|
| 1.1 | `admit while sum(compute_weight of running) + weight(candidate) <= parallelism` | `admit iff running_count < max_concurrency AND sum(weight) + weight(candidate) <= resource_capacity` | Ch4 5.3.2 mech. 4 (C4-1); Pack 38.1, 38.2; Rule B.2 | **The old predicate was unsatisfiable.** `parallelism = 1` with a weight-4 training job gives `4 <= 1`. One number expressed two quantities | **B-01** sets both values per lane |
| 1.2 | `ml` pool, parallelism 1, admitting training and scoring together | `ml` class resolves to `ml.training`, `ml.batch_scoring`, `ml.online_scoring` | Ch4 5.3.2 (C4-2); Pack 38.1, 47; Rule B.2 | A training run could delay a prediction whose value expires at an actionable deadline. **The scheduler could defeat a Core requirement** | **B-01** lane sizing |
| 1.3 | No reservation for operational scoring | `ml.online_scoring` capacity is **hard-reserved**, never available to training or batch admission | Ch4 5.3.2 (C4-2); Ch6 (C6-2); Pack 38.1, 47 | A lane whose capacity can be consumed by training cannot carry a latency guarantee | **B-02** reservation fraction |
| 1.4 | Training not pre-emptible | `ml.training` yields at its next stage checkpoint when a reserved lane needs capacity | Ch4 5.3.2 mech. 9 (C4-3); Pack 38.1 | Checkpointing already exists. Nothing is lost but elapsed time, the correct trade against an expiring prediction | - |
| 1.5 | Single background-worker container | `ppiq-worker` (import, projection, analysis, report, batch scoring), `ppiq-ml-train` (GPU, pre-emptible), **`ppiq-ml-online`** (reserved, warm cache, online scoring only) | Ch6 (C6-1); Pack 47 | Physical isolation makes the Serving Wall enforceable rather than conventional | **B-01**, **B-02** |
| 1.6 | *(interim)* `ml.batch_scoring` may run on either container | **`ppiq-ml-online` admits online scoring only.** Batch, backfill and rescore run on batch and training-class capacity | Ch6 (C6-1); Pack 38.1, 47 | Admitting batch to the online container would consume the reservation the guarantee depends on. Shared hardware is a sizing decision, never permission to consume the reservation | **B-02** must prove the target while training and batch are saturated |
| 1.7 | - | **G-49** lane isolation, **G-50** admission predicate satisfiable | Pack 40.1a | A configuration where a declared job can never be admitted must fail validation, not fail silently at runtime | - |

## 2. SEMANTIC REPRODUCIBILITY

| # | OLD CONTRACT | NEW CONTRACT | DOCUMENT / SECTION | REASON | BENCHMARK |
|---|---|---|---|---|---|
| 2.1 | `SemanticModelVersion` with draft, validated, published, superseded, rolled_back | **Semantic Contract Manifest**: immutable, content-addressed, **no lifecycle** | Ch3 4.5.11 area (C3-4); Pack SM-01; Rule B.3 | The chapters already own version authority. A fourth authoring lifecycle would compete with `definition_versions` | - |
| 2.2 | *(interim)* `manifest_hash` as global primary key | `manifest_id uuid PK`, `tenant_id NOT NULL`, `manifest_hash NOT NULL`, **UNIQUE `(tenant_id, manifest_hash)`** | Ch3 (C3-4); Pack SM-01; Rule B.3 | A global content key would make identical content in two tenants one shared row. **A manifest is tenant-owned evidence** | - |
| 2.3 | No pin over the canonical version set | `semantic_manifest_id uuid NULL FK` on `model_registry`, `feature_snapshots`, `sequence_manifests`, `compute_runs`, `model_training_runs`, `prediction_runs`, `practice_learning_runs`, `scenario_runs`, evidence tables | Ch3 4.5.12 (C3-5); Pack SM-01 | "Which contract produced this artifact" required reconstructing five references from timestamps. Now one value, and a changed answer is a manifest diff | - |
| 2.4 | - | **Nullable for legacy records only. Every new governed AI/ML execution must resolve a manifest**; a run that cannot is refused | Ch3 (C3-5); Pack SM-01; Rule B.3; **G-55** | Nullable without a coverage rule would let the pin quietly never be populated | - |

## 3. TRAINING DATA PATH

| # | OLD CONTRACT | NEW CONTRACT | DOCUMENT / SECTION | REASON | BENCHMARK |
|---|---|---|---|---|---|
| 3.1 | Snapshot pinned; physical read path unstated, `feature_snapshot_rows` implied authoritative | **Sealed typed columnar artifact is the training read path.** `feature_snapshots` gains `artifact_format`, `artifact_content_hash`, `artifact_byte_size` | Ch3 4.5.12 (C3-1); Pack DP-2; Rule B.4 | Deserialising millions of JSONB objects per epoch is bounded by round-trips and JSON parsing, not by the model. Columnar gives typed access, projection pushdown, page-cache residency | **B-03** selects format and confirms the ratio |
| 3.2 | `feature_snapshot_rows` holds the full population in PostgreSQL | **Optional audit sample** with a declared sampling rate | Ch3 (C3-2); Pack DP-2 | A full second copy of every training population inside the operational database, carried into replication, backup and restore | **B-03**, conditional |
| 3.3 | - | **G-48**: no training or encoding path queries `feature_store`. **The snapshot materialiser is exempt by definition** and is the only component permitted to read it for sealing | Pack 40.1a, DP-2; Rule B.4 | Without the stated exemption the gate would forbid the one legitimate read and be unimplementable | - |
| 3.4 | - | **Snapshot Materialiser** and **Manifest Resolver** added to the component inventory | Pack section 2 | Both are load-bearing and were implicit | - |

## 4. SEQUENCE DATA PATH

| # | OLD CONTRACT | NEW CONTRACT | DOCUMENT / SECTION | REASON | BENCHMARK |
|---|---|---|---|---|---|
| 4.1 | DP-3 with `values float32[]`, `offsets_ms int32[]`, `mask uint8[]` as PostgreSQL arrays | **`ppiq_plant.sequence_manifests`** in PostgreSQL; immutable chunked typed arrays in object storage | Ch3 4.5.12 (C3-3); Pack DP-3; Rule B.4 | The largest data product in the system. Array columns carry per-row overhead, defeat compression, and put the largest byte volume through WAL, replication, backup and restore | **B-04** chunk size and compression |
| 4.2 | Loader reads whole rows | Loader consumes **bounded chunks**, memory-mappable where the format allows | Pack DP-3 | A giant row per subject must be materialised in full before use | **B-04** |
| 4.3 | Artifact stores' backup status unstated | Feature-snapshot and sequence artifact stores are **in the backup set** with their own retention | Ch6 (C6-3) | An authoritative artifact outside the backup set is a reproducibility claim that does not survive a restore | - |

## 5. MODEL SELECTION AND PROMOTION

| # | OLD CONTRACT | NEW CONTRACT | DOCUMENT / SECTION | REASON | BENCHMARK |
|---|---|---|---|---|---|
| 5.1 | Champion/challenger on quality metrics | **Three-dimensional gate**: QUALITY (discrimination, **calibration**, out-of-time, subgroup stability, missingness robustness, **explanation stability**), SERVING (p50/p95/p99, throughput, artifact size, RAM/VRAM, warm-up), TRAINING (duration, peak memory, snapshot read throughput) | Ch4 5.6.5 (C4-4); Pack MF-04; Rule B.5 | **A better-discriminating, worse-calibrated model is not an improvement** for a product whose output is a risk band a human acts on. **An unstable explanation is worse than none**, because contributors are presented as evidence | **B-05** |
| 5.2 | Encoder optional, no promotion rule | `promote_encoder iff metric_lift >= min_lift AND p95_latency_delta <= budget AND artifact_size <= size_class AND explanation_stability >= floor` | Ch4 5.6.2-3 (C4-5); Pack MF-01; Rule B.5 | **If engineered features match it within the threshold, the engineered features ship.** Deep learning being available is not a reason to deploy it | **B-05** |
| 5.3 | No schema change assessed | **None required.** `model_registry.metrics` and `acceptance_floor` are `jsonb` | Ch4 (C4-4) | The registry already has the shape | - |

## 6. VECTOR SEARCH

| # | OLD CONTRACT | NEW CONTRACT | DOCUMENT / SECTION | REASON | BENCHMARK |
|---|---|---|---|---|---|
| 6.1 | FAISS as initial implementation, family unspecified | **Policy selector** from population, dimension, RAM, required recall@k, latency target, build time, update pattern | Pack MF-02; Rule B.6 | A fixed family is wrong at one end of the size range | **B-06** per size class |
| 6.2 | Recall probe available | **Exact Flat retained permanently on a representative sample.** Recall@k measured on every build and stored | Ch3 4.5.12 (C3-6); Pack MF-02; **G-51** | An approximate index whose recall has never been measured is an unquantified error source presented as a plant fingerprint | **B-06** |
| 6.3 | - | A build below `recall_floor` **does not become the served index** | Ch3 (C3-6); Pack MF-02 | Measurement without a blocking consequence is decoration | - |

## 7. UNSTRUCTURED MODALITY BOUNDARY

| # | OLD CONTRACT | NEW CONTRACT | DOCUMENT / SECTION | REASON | BENCHMARK |
|---|---|---|---|---|---|
| 7.1 | "No statistic, score or value is ever computed from text" | **"No free-form or model-generated output may become a feature, a score, a statistic or a value."** Text and images enter a learned result only through an authored model definition with snapshot, leakage controls, held-out validation, registry entry, calibration and drift | Ch4 5.8.6 (C4-6); Pack 29, 46; Rule B.1 | **Ch4 5.8.6 and 5.8.7 contradicted each other.** 5.8.7 registers vision models in `model_registry` under full activation and drift rules; **a registered model forbidden from producing a learned result is not a model.** The hazard was never the modality, it is ungoverned output entering a score | - |
| 7.2 | One path implied | **Path A evidence modality** (retrieved, cited, never a feature) and **Path B governed multimodal ML** (the full training contract) | Ch4 (C4-6); Pack 29 | Names the actual hazard rather than banning a modality | - |
| 7.3 | - | **G-54** governed-model-only learned output | Pack 40.1a | Makes the boundary testable at build time | - |
| 7.4 | - | **No implementation scope added.** Both remain interface-designed, future implementation | Ch4 (C4-6); Pack 46 | Correcting a boundary is not a decision to build behind it | - |

## 8. ASSISTANT AND LLM RUNTIME

| # | OLD CONTRACT | NEW CONTRACT | DOCUMENT / SECTION | REASON | BENCHMARK |
|---|---|---|---|---|---|
| 8.1 | Dock, honesty contract, no-fabrication guard, serving modes, acceptance. **No runtime between question and model** | New Ch4 **5.7.9**: nine-step pipeline | Ch4 5.7.9 (C4-7); Pack 11.2a; Rule B.7 | The Assistant is Core. Groundedness is won or lost in evidence selection and budgeting, which nothing specified | - |
| 8.2 | Tool selection unspecified | **Deterministic tool planner.** The LLM does not choose tools | Ch4 5.7.9.2 | A model choosing tools freely produces a different plan on a rephrasing and cannot be gated. Q-01 measures accuracy | - |
| 8.3 | Retrieval unspecified | **Hybrid: lexical + semantic + fusion. Permission filter BEFORE ranking.** Re-ranking optional, ships only if it earns its latency | Ch4 5.7.9.3 | Filtering after ranking lets a forbidden chunk displace a permitted one, so the answer silently loses evidence the user was entitled to | **B-08** re-ranking |
| 8.4 | Context assembly unspecified | **Token-budgeted packing** with dedup, tool-priority ranking, reserved answer allowance, handles retained, **truncation recorded and disclosed** | Ch4 5.7.9.4; **G-52** | Overflow silently drops evidence and produces a sentence the guard then rejects, wasting a round trip | **B-07** |
| 8.5 | Gateway routes by mode and enforces egress | Plus **minimum-scoped-payload** to external providers; **provider or model change is a governed release event** | Ch4 5.7.9.5; Ch6 (C6-4) | A model change alters answer behaviour with no code change | - |
| 8.6 | Serving container named | **`ModelServingRuntime`** abstraction. No serving library is the product contract | Ch4 5.7.9.6; Rule B.7 | Same reason FAISS is not the contract | **B-09** |
| 8.7 | No-fabrication guard stated | **Deterministic verifier that does not call the LLM.** Four checks, each rejecting before display | Ch4 5.7.9.7; **G-53** | A model checking its own output is not a guard | - |
| 8.8 | Acceptance criteria only | **Q-01 to Q-11** quality gates | Ch4 5.7.9.8 | **Q-05 refusal correctness and Q-06 causal overreach decide credibility**, and both are testable against a fixed probe set before any customer sees them | **B-07**, **B-09** |

## 9. TERMINOLOGY AND INVENTORY

| # | OLD CONTRACT | NEW CONTRACT | DOCUMENT / SECTION | REASON | BENCHMARK |
|---|---|---|---|---|---|
| 9.1 | "Seven model families" / "seven ML models" | **"Seven intelligence and engine families"** with five sub-types: learned model (MF-01, MF-03, MF-04), retrieval and index (MF-02), statistical engine (MF-05, MF-06), practice engine (MF-07), plus orchestration and governance | Ch2 glossary (C2-2); Ch4 (C4-8); Pack 6; Rule B.8 | Three of the seven are not models. **The sub-type is load-bearing**: it determines refresh policy, lane assignment and whether a champion/challenger gate applies at all | - |
| 9.2 | Gate inventory G-01 to G-46 | **G-01 to G-55** | Pack 40.1a; Rule B.9 | Eight target-architecture gates added | - |
| 9.3 | 20 components | **22 components** | Pack section 2 | Snapshot Materialiser, Manifest Resolver | - |
| 9.4 | Glossary lacks the new terms | **Semantic Contract Manifest**, **intelligence and engine families**, **lane** | Ch2 (C2-2) | Chapter 2 is the naming authority | - |
| 9.5 | Core latency guarantee unattributed | Guaranteed by **hard-reserved online capacity**, not by pool ordering | Ch2 3.10 (C2-1) | The claim previously rested on a scheduler that could violate it | **B-02** |

---

## 10. BENCHMARK PARAMETERS - NOT GUESSED

| ID | Question | Method | Decides |
|---|---|---|---|
| **B-01** | `max_concurrency`, `resource_capacity`, `compute_weight` per lane | Instrument peak RAM, CPU seconds and GPU seconds per job class on a representative population; set capacity so the heaviest declared job is admissible with headroom | C4-1, C4-2, C6-1 |
| **B-02** | Online reservation fraction | Load-test event scoring at target arrival rate while training and batch are saturated; find the reservation holding p95 inside the actionable-deadline budget | C4-2, C6-1, C6-2 |
| **B-03** | Columnar format; whether `feature_snapshot_rows` can be demoted | Load the same population by artifact and by JSONB; measure epoch time, peak RAM, storage size, seal time | C3-1, C3-2 |
| **B-04** | Sequence chunk size and compression | Vary chunk size; measure loader throughput, storage amplification, random-access cost | C3-3 |
| **B-05** | Encoder lift versus serving cost | Train with and without embedding columns on the same snapshot; measure lift, p95 latency delta, artifact size, VRAM | C4-5, C6-5 |
| **B-06** | ANN family per size class | Build Flat, HNSW, IVF-PQ on representative populations; measure recall@k against Flat, p95 latency, build time, RAM | C3-6 |
| **B-07** | Token budget and evidence-set size | Vary packed evidence size; measure groundedness, citation correctness, answer latency | C4-7 |
| **B-08** | Whether re-ranking earns its latency | With and without a cross-encoder; measure citation-correctness delta against added p95 | C4-7 |
| **B-09** | Serving runtime and concurrency | Benchmark candidate runtimes at target concurrency; measure time-to-first-token, throughput, VRAM per session | C4-7, C6-5 |

**No value in this ledger is asserted without a benchmark that would falsify it.**

---

## 11. DOCUMENT STATE

| Document | State |
|---|---|
| `PPIQ_Layer_B_Architecture_Design_Pack.md` | **Revision 7, synchronised.** Scan V3 clean, exit 0 |
| `PPIQ_Layer_B_Learned_Intelligence_Engine_Rule.md` | **Appendix B added**, overriding Appendix A and the body where they differ |
| `PPIQ_AI_ML_LLM_Target_Architecture_Optimisation.md` | Decision matrix, both ruled corrections applied |
| `PPIQ_Master_Design_Chapter_Amendment_Pack.md` | **22 amendments specified for Chapters 2, 3, 4 and 6** |
| `PPIQ_Engine_ML_Onboarding_Brief_AR.md` | Synchronised to the target |
| `PPIQ_Layer_B_Pack_Contradiction_Scan.py` | **V3**: 45 negative, 29 positive, 3 structural checks |

**The chapter files themselves are read-only inputs.** The amendments are specified at section level with removal instructions, replacement text and reasons, ready to be applied by whoever holds write access to them.

---

*Final synchronisation ledger, 11 August 2026. Forty-one contract changes across six documents, nine benchmark parameters left open with defined methods, zero guessed values.*
