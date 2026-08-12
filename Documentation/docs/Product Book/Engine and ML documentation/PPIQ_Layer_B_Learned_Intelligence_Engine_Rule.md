# PPIQ RULE - LAYER B: LEARNED INDUSTRIAL INTELLIGENCE ENGINE

**Status: BINDING RULE**
**Ruled by Karim - 11 August 2026**
**Scope: all Layer B design, model, orchestration, registry, output-dataset and Assistant-integration work**
**Implementation status: DESIGN ONLY. Implementation is NOT authorised by this rule.**

---

> # CANONICAL CORRECTION NOTICE
>
> **Read before implementing anything from the body of this rule.**
>
> This rule is a **subsystem constitution and a subset instrument**. It is subordinate to Chapter 2, Chapter 3 and Chapter 4 in that order (Appendix A.1). **Appendix A.6 overrides the affected legacy clauses in the body below.**
>
> **The body of this rule uses `ModelBundle` wording in sections 6, 15, 16 and 17. That wording is HISTORICAL AND NON-IMPLEMENTABLE.** There is no ModelBundle object. The canonical authority is `ppiq_plant.model_registry`, governed per **serving identity** `(tenant_id, model_code, outcome_code, grain_code)` plus `model_version`, with `status` and `serving_role` as independent axes (Ch3 4.5.12).
>
> **An implementer must not treat `ModelBundle` as current architecture.**
>
> Other body clauses corrected by Appendix A.6: the staged-confidence L1 to L4 framing is replaced by the nine-check gate of Ch4 5.6.4d and the seven-condition `can_accept` authority of Ch3 4.5.12a; the terminal-state enum is replaced by the canonical error codes; section 29's modality clause is replaced by the Ch4 5.8.6 boundary rule that no statistic, score or value is computed from text; and the no-point-estimate rule is withdrawn.
>
> **Appendix B (11 August 2026) applies the accepted AI/ML/LLM target architecture and overrides Appendix A and the body where they differ.** In particular, Appendix A.6's absolute text boundary is withdrawn and replaced by a governance-based boundary; the `ml` pool resolves to three lanes with hard-reserved online scoring; and the seven families are intelligence and engine families, not ML models.
>
> **Order of authority: the chapters as amended, then Appendix B, then this notice, then Appendix A, then the body.**

---

---

## PURPOSE

Layer B turns a customer's historical and continuously arriving plant data into:

- plant fingerprint
- anomaly / novelty knowledge
- process / outcome relationships
- attributable risk
- practice learning
- operating envelopes
- early prediction
- historical similarity
- evidence-supported corrective / remediation suggestions
- machine-readable intelligence datasets for dashboards
- evidence endpoints for the Assistant

It must be GENERIC across industries.

Fleet-v2 / steel is only one test dataset. Oil, mineral water, pharma, paper, tyres, food and unknown future industries must use the same engine architecture.

---

## 1. DO NOT TRAIN DIRECTLY ON CUSTOMER TABLE NAMES

A customer may have 20 tables, 150 tables or 1,500 tables, across SQL databases, historians, files, APIs, MES, LIMS, maintenance systems and quality systems.

The ML layer must NOT contain code such as:

```
read Coil table
read Heat table
read CastingSpeed
```

Customer physical schemas are handled ABOVE the intelligence engine.

The customer configures their data using the PPIQ authoring mechanisms:

- No-Code Wiring Canvas
- relationship definitions
- governed SQL where needed
- dataset registry
- mappings
- parameter / outcome definitions

The resulting authored definitions persist. Layer B consumes the resulting SEMANTIC / CANONICAL contracts.

Conceptually:

```
CUSTOMER SOURCES
  -> NO-CODE WIRING / GOVERNED SQL
  -> RELATIONSHIP + GRAIN + SEMANTIC DECLARATIONS
  -> CANONICAL / SPINE / FEATURE REPRESENTATION
  -> LEARNED INTELLIGENCE ENGINE
```

---

## 2. CUSTOMER DECLARATION CONTRACT

What Layer B needs from the authored model, at minimum.

### Identity / grain

What is the analytical unit? It may be a discrete item, a batch, a lot, a campaign, a process window, a continuous-flow interval, or another customer-defined unit.

The engine consumes a grain identifier and semantic type, not an industry word. Examples are irrelevant to engine code.

### Process position

The ordered or graph position of an observation, practice or event in the production journey.

### Relationships / genealogy

The customer's Wiring Canvas or relationship authoring supplies:

- parent-child relationships
- stage ordering
- merge / split relationships
- temporal relationships
- cardinality
- confidence where inferred

The ML engine consumes the graph. It does not rediscover database joins blindly.

### Parameters

For each usable parameter:

- semantic code
- physical quantity
- unit
- data type
- timestamp availability
- stage / process position
- controllable vs observed
- setpoint vs actual where known
- valid range where declared

### Outcomes

For every outcome:

- outcome code
- type: binary, categorical, ordinal, continuous
- grain
- timestamp
- detection stage
- class taxonomy where applicable

### Events / context

- process events
- failures
- alarms
- maintenance
- operator actions
- resource identity
- shift / crew / campaign / context where available

A missing context dimension is acceptable. The capability profile determines which intelligence methods remain eligible.

---

## 3. EXTENSIBLE SEMANTIC MODEL, NOT A FIXED INDUSTRY ONTOLOGY

Core semantic roles may be defined, such as:

- production / process position
- analytical / material unit
- observation
- practice / setpoint
- event
- outcome
- resource
- input
- specification
- relationship / genealogy

These are NOT a permanently closed list. The semantic role registry must be extensible. A future installation may add concepts we did not predict.

Genericity means:

```
new customer semantics -> configuration / registry extension
```

NOT:

```
new industry -> rebuild the application
```

---

## 4. THE INTELLIGENCE ENGINE DOES NOT REPLACE EXACT BI

Layer A and Layer B have different truth contracts.

**Layer A - exact facts:** count, sum, grouped KPI, historical totals, exact filtered population.

**Layer B - learned / estimated intelligence:** risk, similarity, anomaly, attribution, prediction, operating envelope, learned effect, recommendation confidence.

Never use ML to approximate an exact BI fact merely because the dataset is large.

The Assistant may combine both, clearly labelled. For example:

- Layer A: "actual defect rate this week = X."
- Layer B: "the current model attributes most of the increase to A/B/C under these conditions."

---

## 5. REQUIRED DATA PRODUCTS BEFORE TRAINING

Do NOT make the ML models repeatedly scan 150 source tables. Design persistent intermediate data products.

### A. Journey / analytical spine

One governed record per analytical grain / process position / time window, containing:

- stable entity / grain identity
- process position
- start / end time
- genealogy / path references
- variant / context references

### B. Feature store

For every eligible grain and prediction point:

- engineered scalar features
- categorical / context features
- missingness indicators
- process summaries
- event summaries
- historical-window features

### C. Sequence store

For raw time-series shape where representation learning is justified:

- ordered timestamp / value sequences
- channel definitions
- masks
- stage context

### D. Outcome store

Versioned, time-aware target definitions.

### E. Evidence store

Materialised intelligence that the UI and Assistant can consume quickly.

Training jobs read these governed products. They must not reconstruct the plant from raw source databases every weekend.

---

## 6. MODEL 1 - SELF-SUPERVISED PROCESS ENCODER

Purpose: learn a compact representation of a process journey or profile without requiring quality labels.

Candidate architectures are selected by measured performance, not fashion. Start with a baseline comparison such as:

- temporal convolution / 1D CNN
- Transformer-style sequence encoder where sequence length and context justify it

Use PyTorch for the deep-learning implementation. PyTorch's current APIs support automatic mixed precision and distributed / sharded training mechanisms when model or data scale requires them.

Training objective may include:

- masked-value reconstruction
- masked segment reconstruction
- contrastive representation learning
- next-stage / state prediction where semantically valid

Output: a stable fixed-dimensional embedding, initially targeting approximately 128 to 256 dimensions unless benchmarking proves another size preferable.

The embedding dimension is NOT a customer-visible contract.

---

## 7. MODEL 2 - VECTOR / HISTORICAL SIMILARITY INDEX

Purpose: answer "which historical journeys look like this one?"

Use a replaceable ANN abstraction. FAISS is an appropriate initial implementation because it is built for dense-vector similarity search and supports multiple index strategies, batch search and GPU implementations.

Do NOT expose FAISS as the product contract. The product contract is `VectorSimilarityIndex`. The implementation may change later.

Stored neighbour evidence must retain:

- current entity id
- neighbour entity id
- similarity / distance
- model / encoder version
- training / index version
- relevant outcomes / practices available on the neighbour

This is a major component of the plant fingerprint.

---

## 8. MODEL 3 - NORMAL / NOVELTY MODEL

Purpose: learn what this plant normally looks like, even when labels do not exist.

Do not force one algorithm globally. Candidate methods include:

- robust distance / density on embeddings
- isolation-style methods
- reconstruction-error models
- cluster / density methods

Select by calibration and false-positive behaviour.

Output:

- anomaly / novelty score
- percentile
- nearest normal regimes
- reason / evidence where available
- model version

"Unusual" is not automatically "bad". That distinction must remain explicit.

---

## 9. MODEL 4 - SUPERVISED OUTCOME MODELS

The primary supervised decision family begins with gradient-boosted trees over engineered features, context, and the optional encoder embedding.

LightGBM is a strong initial implementation because its official design uses histogram-based tree learning specifically to improve training speed and memory behaviour.

Do not lock the product permanently to LightGBM. Keep a `SupervisedOutcomeModel` abstraction so XGBoost, CatBoost or other validated models can be benchmarked later.

Model granularity must be governed. Do not automatically train hundreds of models because we can.

A model is created ONLY when:

- outcome definition exists
- population is sufficient
- class distribution is acceptable
- leakage checks pass
- prediction point is meaningful
- validation evidence meets threshold

No-data / insufficient-data is a valid outcome.

---

## 10. ATTRIBUTION / EXPLANATION

For tree models, use SHAP / TreeSHAP as the initial explanation mechanism. SHAP's TreeExplainer is specifically designed for tree ensembles including LightGBM and XGBoost-style models.

However: **SHAP contribution is not causal effect.**

Every intelligence output must classify itself as one of:

- ASSOCIATION
- PREDICTIVE CONTRIBUTION
- MATCHED EFFECT ESTIMATE
- CAUSAL / EXPERIMENTAL EVIDENCE

The Assistant must not blur them together.

---

## 11. MODEL 5 - EFFECT / PRACTICE LEARNING LAYER

This is where operating envelopes, best / worst matched practice, intervention comparisons and potential remediation evidence are produced.

Do NOT begin by claiming causality. Use staged confidence:

- **Level 1** - conditioned association
- **Level 2** - matched / stratified comparison
- **Level 3** - observational treatment / effect estimation when assumptions are defensible
- **Level 4** - experimental / controlled evidence where customer data supports it

Output must state:

- population
- conditioning variables
- effect estimate
- uncertainty
- support / overlap
- confounder limitations
- whether the parameter is controllable
- evidence IDs

If evidence is insufficient: refuse the recommendation.

---

## 12. PREDICTION POINTS AND DATA LEAKAGE

This must be structural.

For every prediction model define `prediction_cutoff`. Features available after that cutoff are prohibited.

If predicting final quality after stage 3, the model may use only data available through stage 3. It may NOT see:

- downstream measurements
- final inspection
- future operator action
- future maintenance / event state

Create automated temporal leakage gates.

**A model with fantastic metrics caused by future information is a failed model.**

---

## 13. CAPABILITY / ELIGIBILITY PROFILE

Before running a method, measure whether the customer's data supports it.

Profile at minimum:

- history depth
- outcome availability
- class balance
- genealogy coverage
- observation density
- temporal alignment quality
- controllable-practice coverage
- equipment / resource attribution
- intervention history
- missingness
- regime stability

Every ML method declares eligibility requirements.

Terminal states:

- FINDING
- INSUFFICIENT DATA
- NOT APPLICABLE
- REFUSED BY GUARD
- CONTRADICTED BY CONTROL
- MODEL NOT READY

**Never turn a method limitation into a false statement about customer data.**

---

## 14. FIRST COMMISSIONING SCHEDULE - HOURS / DAYS ALLOWED

The expensive one-time initial build. Conceptual sequence:

1. validate authored semantic model
2. materialise journey spine
3. build historical feature / sequence / outcome stores
4. calculate capability profile
5. train self-supervised encoder where eligible
6. encode historical journeys
7. build vector index
8. fit normal / novelty model
9. train eligible supervised outcome models
10. calibrate
11. compute explanations
12. compute envelopes / effect evidence
13. populate evidence store
14. run validation gates
15. publish `ModelBundle Version 1`

A multi-hour or multi-day first commissioning run is acceptable. But every stage must be restartable and checkpointed.

**A failure at hour 19 must not require restarting raw ingestion from zero.**

---

## 15. WEEKLY SCHEDULE - MAXIMUM 24-HOUR WINDOW

Do NOT interpret "incremental" as "mutate every model weight a little each week." Use a model-specific refresh policy.

### Incremental every week

- ingest only new / changed data
- extend spine / features
- encode newly eligible journeys using the current encoder
- add their embeddings to the current vector index, or rebuild only if index policy requires
- update exact summaries / evidence
- recompute drift metrics

### Retrain where appropriate

Supervised tree models may be fully retrained on their governed rolling / training window when that is cheaper and more reproducible than online mutation.

### Recalibrate

Probability calibration may be updated against recent held-out history when sufficient data exists.

### Encoder

Do NOT casually retrain the encoder weekly. Its embedding space defines similarity. If the encoder changes, all historical embeddings and index relationships may change.

Default policy: freeze between controlled refreshes.

Retrain on:

- a scheduled monthly / quarterly governance window
- significant representation drift
- major plant regime change
- enough accumulated history to justify it

Then: new encoder -> re-encode historical reference population -> build new index -> validate -> atomically publish the full compatible bundle.

**Never mix embeddings from incompatible encoder versions.**

---

## 16. CHAMPION / CHALLENGER VALIDATION

A new weekly model does not automatically become production.

Compare candidate and current production model on the SAME governed recent holdout.

Classification metrics: discrimination, calibration, precision / recall, class-specific performance.
Regression metrics: error, bias, stability.
All models: drift, subgroup / variant performance, missingness robustness, inference compatibility.

If the candidate is worse or unsupported: retain the champion and record the rejection.

---

## 17. MODEL REGISTRY IS MANDATORY

Every model artifact must be versioned. Use MLflow Model Registry or an equivalent governed abstraction for the initial implementation. MLflow's current registry supports model versions, aliases, lineage and metadata, which fits the champion / candidate / rollback contract.

Every published model or bundle must record:

- tenant / site
- model code
- model family
- version
- training-data window
- training dataset / version / hash
- feature-schema hash
- semantic-model version
- encoder version
- hyperparameters
- metrics
- calibration
- eligibility decision
- validation status
- created timestamp
- promotion timestamp
- reason for promotion / rejection

Use an atomic production alias / pointer. Rollback must be possible.

---

## 18. DO NOT ASSUME ONE SERVER IS ALWAYS ENOUGH

Any claim that these volumes are automatically unremarkable on one server is too strong. Sizing must be measured.

A customer may have hundreds of millions or billions of rows, dense high-frequency sensor sequences, and hundreds or thousands of parameters.

The correct architecture supports:

- chunked / partitioned feature generation
- streaming / batched data loading
- GPU training where useful
- multi-GPU / distributed PyTorch only when justified
- CPU / distributed boosting where justified
- checkpoints
- resume
- bounded memory

PyTorch provides distributed mechanisms including FSDP and DDP when that scale is actually required.

Do not introduce a cluster merely because "enterprise sounds large". Do not promise a single server before benchmarking.

Produce a hardware-sizing model based on: rows, features, sequence length, parameter count, historical window, model family, and GPU / CPU / RAM / storage throughput.

---

## 19. DAYTIME QUERY CONTRACT - NO TRAINING

During working hours: **NO MODEL TRAINING.**

The user asks a question. The intelligence orchestrator chooses one of three tiers.

### Tier 1 - precomputed, target seconds

Reads the evidence store, prediction store, vector index, model outputs and Layer A exact BI summaries.

Examples: "Why is defect risk high?", "What historical runs resemble this?", "What are the dominant contributors?", "What is the approved operating envelope?"

### Tier 2 - bounded calculation, target under 30 seconds where practical

Computes on prepared feature and evidence stores, NOT raw customer sources.

Examples: filter a cohort, compare two operating populations, bounded correlation, calculate a user-selected slice.

### Tier 3 - over budget

Do NOT hang for minutes or hours. Create or suggest an asynchronous analytical job and return a clear state.

**Overall user-facing requirement: normal interactive answers should be fast; the synchronous absolute product target is less than 2 minutes.** If the required analysis cannot meet that honestly, schedule or refuse rather than freeze the UI.

---

## 20. ASSISTANT INTEGRATION

The Assistant is NOT the ML engine. It is an orchestrator and communicator over governed tools.

It can call **Layer A** for exact current and historical facts.

It can call **Layer B** for prediction, explanation, neighbours, anomaly, findings, operating envelopes, learned effects and readiness.

It can call the **evidence store** for citations and provenance.

The Assistant never invents an ML output.

Every learned claim returned to the Assistant carries:

- model / version
- training window
- evidence / finding id
- population
- confidence / uncertainty
- method
- eligibility / readiness
- timestamp

Assistant answer shape: fact + learned finding + evidence + qualification.

---

## 21. DASHBOARD / CHART INTEGRATION

Layer B must NOT return opaque Python objects that only a special ML page understands.

Its outputs must be materialised as GOVERNED ANALYTICAL DATASETS. This is critical.

The Page Builder and ordinary charts must bind to intelligence outputs using the same no-code mechanisms used for normal data.

### Prediction dataset

entity / grain identity, prediction point, outcome code, predicted value / probability, risk class, confidence / calibration, model version, scored time.

### Contributor dataset

prediction / finding id, feature / parameter code, contribution value, direction, rank, feature value, unit, model version.

### Similarity dataset

subject identity, neighbour identity, distance / similarity, neighbour outcome, neighbour context, encoder / index version.

### Anomaly dataset

entity identity, anomaly score, percentile, regime, evidence time, model version.

### Envelope dataset

parameter, context / variant, lower bound, upper bound, observed outcome rate, population, evidence / confidence.

### Finding / effect dataset

finding id, method, driver / exposure, outcome, effect / association, uncertainty, population, conditioning, status, evidence references.

These are ordinary governed datasets from the BI system's perspective. Therefore a customer can:

```
Add Widget -> select Intelligence dataset -> choose dimension
  -> choose measure -> choose compatible chart -> save
```

No special code per prediction dashboard.

---

## 22. INTELLIGENCE METADATA ENDPOINT

Layer B publishes metadata just like Layer A.

For every intelligence dataset, expose:

- fields
- semantic type
- units
- dimension / measure eligibility
- categorical / numeric / time role
- human label
- provenance availability
- recommended chart grammar where appropriate

The Page Builder consumes metadata. It must not maintain a compiled ML-field list.

---

## 23. CUSTOMER WIRING IS USEFUL TO ML, BUT NOT TRUSTED BLINDLY

The customer's no-code relationships are valuable inputs. They define intended semantic links.

Before training, validate:

- join coverage
- cardinality
- temporal validity
- orphan rate
- duplicate explosion
- leakage risk
- impossible future-to-past edges

**A Wiring Canvas line is an authored hypothesis and contract. It is not automatically proof that the data relationship is analytically valid.**

Produce relationship quality metrics. If linkage is too weak for a method, refuse that method.

---

## 24. SQL AUTHORING

Governed SQL may be used by customer engineers to define projections, derived features, cohorts, mappings, outcomes and relationship views.

But ML training must consume a versioned, materialised, validated definition. Do not train against mutable ad-hoc SQL whose meaning can change without invalidating the model.

Every trained model pins: `definition version` + `feature schema hash`.

---

## 25. DRIFT SUPERVISOR

The supervisor does not randomly adjust coefficients. It monitors:

- input drift
- representation drift
- outcome drift
- calibration drift
- performance drift
- regime change
- data-quality drift

Actions include: no action, recalibrate, retrain model, retrain encoder, rebuild vector index, quarantine model, revert to previous champion, declare readiness blocked.

Every action has evidence and a reason.

---

## 26. REPRODUCIBILITY

Given tenant, semantic model version, feature definition version, training window and model version, we must be able to reproduce the model and explain why an answer changed.

Use:

- deterministic seeds where practical
- immutable dataset / version manifests
- code / commit identity
- environment / library manifest
- model artifact hash
- feature schema hash

**No `current_model.pkl` with unknown history.**

---

## 27. SECURITY / TENANT ISOLATION

Models, embeddings, neighbours and evidence are tenant-scoped.

Never allow nearest-neighbour search across customers unless an explicit future federated or benchmark product exists with separate governance.

No cross-tenant vector index. No cross-tenant training population by accident.

---

## 28. REQUIRED DESIGN DELIVERABLES

**DO NOT START IMPLEMENTATION.** Produce an architecture and design pack containing:

| ID | Deliverable |
|---|---|
| **A** | Layer-B context diagram: Layer A / semantic model / Layer B / Assistant / UI |
| **B** | Input contract: exactly what semantic data Layer B consumes |
| **C** | Intelligence data products: spine, feature store, sequence store, outcome store, evidence store |
| **D** | Model-family registry: for each model, purpose, input, eligibility, algorithm candidates, output, refresh policy, runtime target |
| **E** | Three-schedule orchestration: initial commissioning, weekly, daytime query |
| **F** | Model registry and lifecycle: candidate / champion / rollback |
| **G** | Output dataset schemas: prediction, contributors, similarity, anomaly, envelopes, findings |
| **H** | Assistant tool contract: what exact data the Assistant receives |
| **I** | Page Builder / BI integration: how intelligence outputs appear in normal widgets without special code |
| **J** | Genericity proof: at least two non-steel conceptual installations, oil and mineral-water. No oil-specific or water-specific engine code. Show that only configuration and schema mappings differ |
| **K** | Scale plan: hardware and execution strategy by data volume |
| **L** | Validation and gates: leakage, drift, calibration, reproducibility, tenant isolation, refusal |

---

## 29. ACCEPTANCE PRINCIPLE

Layer B succeeds when this statement becomes true:

> A completely new industrial customer can map their own data through PPIQ's no-code semantic authoring, run commissioning, obtain only the intelligence their data genuinely supports, receive weekly governed model updates, ask questions during production hours in less than two minutes, and bind learned outputs to ordinary PPIQ widgets without a developer writing industry-specific ML code.

That is the target.

Do not optimise the design for Fleet-v2. Do not optimise it for one neural network.

**Build the GENERIC LEARNED INTELLIGENCE CONTRACT. The algorithms sit behind that contract and can evolve.**

---

*Rule frozen 11 August 2026. Sections 1 to 29 are Karim's ruling. This document is design authority for Layer B; it does not authorise implementation.*

---

# APPENDIX A - AUTHORITY ORDER AND ADDITIONAL INTELLIGENCE CAPABILITIES

**Added 11 August 2026 by ruling. Closes OD-13.**

## A.1 Authority order

This rule is a **subsystem constitution and subset instrument**. It does not govern scope. The authority order is:

| Rank | Document | Governs |
|---|---|---|
| **1** | **Chapter 2** | Product naming, canonical journey, product structure, relationship-model positioning, capability scope |
| **2** | **Chapter 3** | Target technical product contract, schemas, pages, flows, persistence |
| **3** | **Chapter 4** | Detailed engine, authoring, execution and intelligence behaviour |
| **4** | **This rule** | Layer B subsystem constitution, subset of the above |

**Where this rule is narrower than the governing Master Design, the Master Design governs.**

## A.2 Additional intelligence capabilities in scope for Layer B

The body of this rule does not name the following. They are in scope, they are governed by the Master Design chapters, and their architecture is designed in `PPIQ_Layer_B_Architecture_Design_Pack.md` Part Two.

| # | Capability | Pack section |
|---|---|---|
| 1 | Statistical and correlation engine, with method registry, assumption testing, effect size, FDR correction, stratification, stability and lag | 21 |
| 2 | Practice learning engine: canonical signatures, tolerance binning, operation sequence, context matching, exact and relaxed matching with declared back-off, sensitivity state, practice drift, within-tenant benchmarking | 22 |
| 3 | Operational prediction and early warning: current-state contract, actionable deadline, event and micro-batch and scheduled scoring modes, delivery latency, primary and fallback model state | 23 |
| 4 | Complete remediation safety architecture: nine checks, per-prediction evaluation, the four terminal classifications, `can_accept` | 24 |
| 5 | Decision, outcome, effectiveness and feedback loop: accept, reject, defer, action recording, actual outcome arrival, prediction correctness with intervened exclusion, remediation effectiveness, governed feedback | 25 |
| 6 | Value engine: declared cost assumption contract, bounded range impact, value realisation ledger, attribution, abstention | 26 |
| 7 | Scenario and what-if simulation, with no write path | 27 |
| 8 | Full engine supervisor: observe, propose, shadow, compare on held-out history, human approval, atomic apply, with a prohibited set | 28 |
| 9 | Modality extension contract for text evidence and inspection-image intelligence | 29 |
| 10 | Canonical Plant Data as input boundary with sealed snapshots as the training contract | 34 |
| 11 | The single relationship resolution authority, resolving through the canonical `plant_relationships` publication. **No independent Layer B relationship version object** | 35, 50 |
| 12 | Intelligence blocks in the no-code analysis authoring surface | 36 |
| 13 | JobDefinition and JobRun as execution identities, distinct from model instances | 37 |
| 14 | Concurrency and resource governance through the canonical weighted job pools | 38 |

## A.3 Corrections to the body of this rule

| Rule section | Correction | Authority |
|---|---|---|
| **21** | Six output dataset families becomes **seven**, adding Model and Readiness Status. A new installation must render `MODEL_NOT_READY` and `INSUFFICIENT_DATA` truthfully rather than appearing broken | CT-07 ruling |
| **21** | Intelligence outputs are ordinary governed analytical sources. **This does not mean every intelligence row physically becomes one fact row shape.** See A.4 | T-045 measurement |
| **9** | The warning against training hundreds of models is given a mechanism: the model-count governor | Pack 6.7 |
| **15** | Weekly index extension is reconciled with bundle immutability by generational, append-only index versions | Pack AD-06 |
| **2** | The declaration contract gains an intervention flag on events, without which effect levels 3 and 4 have no input | Pack SM-07 |

## A.4 Storage placement (closes OD-02)

The existing three-schema law stands. No fourth application schema.

| Content | Location |
|---|---|
| Customer-derived analytical and intelligence datasets | **Plant Data** |
| Operational and control-plane metadata belonging in the application database | **Meta Data** |
| Pre-semantic, source-shaped data | **Dump Store** |
| Model binaries, checkpoints, large vector-index artifacts and equivalent binaries | **Object / artifact storage** |

Analytical surfaces do not read operational artifact storage. Where operational metadata must become analytically visible, publish a governed Plant Data read model or projection.

---

*Appendix A added 11 August 2026. It removes the scope conflict between this rule and the Master Design chapters by subordinating this rule to them and naming the capabilities it omitted.*

## A.5 Correction to Appendix A, 11 August 2026

**Chapter 3 section 4.5.10 defines the canonical relationship authority.** It is `plant_relationships`, `plant_relationship_members` and `plant_relationship_paths`, versioned through `source_definition_id`, `source_definition_version` and an effective and retired lifecycle. Publishing the transformation emits the relationship model.

Layer B **does not** define a relationship version object. It pins the canonical relationship-definition publication, version and hash. The hard law is unchanged: **no engine owns a private join**, and the RelationshipResolver remains the one implementation authority.

**Chapter 4 section 5.2 and 5.6 define one authoring shell with multiple purposes.** S3 is analysis authoring and emits an Analysis Definition. S4 is model authoring and emits a **Model Definition**. Layer B does not create a canvas and does not collapse the S4 model palette into the S3 method selector.

**Chapter 4 section 5.3.2 defines the canonical `ml` pool as training and scoring**, with scoring using the latest-only scheduling policy where applicable. There is no serving pool.

**Chapter 3 DF7 defines the common widget execution contract** as columns, rows and warnings, with `sourceKind` of canonical or intelligence, plus bindable `intelligenceSource` and `columnRoles`. One envelope, source-declared row shapes, no ML-specific widget type.


## A.6 Final binding, 11 August 2026 (Revision 6 of the Architecture Pack)

Chapters 1 to 6 were read directly. The Architecture Pack is bound to them and is **IMPLEMENTATION DESIGN FROZEN**.

**Corrections to this rule from the chapter text:**

| Rule statement | Canonical | Effect |
|---|---|---|
| Section 6 to 11 model families as independent stacks | Ch4 5.5 Groups A to D and 5.6 Groups E to G are **registry block rows on the S3 and S4 authoring surfaces**. Models are `definition_kind` values in `ppiq_meta.definition_store` | The families are correct as engine concepts; they are authored, versioned and persisted through the unified definition store |
| Section 15 and 17 ModelBundle as the only publishable unit | **`ppiq_plant.model_registry`** governs per-model activation. Serving identity is `(tenant_id, model_code, outcome_code, grain_code)` plus `model_version`, with `status` and `serving_role` as independent axes | **There is no ModelBundle object.** Activation is per serving identity |
| Section 11 staged confidence L1 to L4 | Ch4 5.6.4d **nine checks** with four outcomes: actionable, evidence_only, exploratory, suppressed. Ch3 4.5.12a `can_accept` carries seven conditions | The nine-check gate governs. Accept, Reject and Defer exist only where `can_accept` is true |
| Section 13 terminal states | Canonical error codes `RL01`, `RL02`, `RM01` to `RM10`, `SC01`, `SC02`, `WD07` and the gate states `Ready`, `Partial`, `Blocked` on `compute_runs` | Use the canonical codes, not a parallel enum |
| Section 29 modality outputs feeding similarity and novelty | **Ch4 5.8.6 boundary rule: no statistic, score or value is ever computed from text.** A finding may cite a passage; it may not be derived from one | Modality output cites and links. It never contributes a feature or a score |
| Value: no point estimate | `value_impacts` carries `point_estimate` beside mandatory `lower_bound` and `upper_bound` when `basis_status = 'Sufficient'` | Bounds are mandatory; a point estimate beside them is permitted |

**The relationship authority is final:** Chapter 2 3.15 positions it, Chapter 3 4.5.10 implements it, one resolver serves all sixteen consumers through `GET /api/relationships/resolve?from=&to=&purpose=`, and `validation_state = unproven` permits `explore` while refusing `train`.

---

# APPENDIX B - TARGET ARCHITECTURE SYNCHRONISATION

**Added 11 August 2026. Applies the accepted AI/ML/LLM target architecture. Appendix B overrides Appendix A and the body wherever they differ.**

## B.1 The modality boundary, corrected

**Appendix A.6 stated an absolute rule: no statistic, score or value is ever computed from text. That rule is withdrawn.** It was correct for free-form output and too broad as a permanent boundary, and it contradicted Chapter 4 5.8.7, which registers vision models in `model_registry` under full activation, retirement and drift rules. A registered model forbidden from producing any learned result is not a model.

**The boundary is governance, not modality:**

> **No free-form or model-generated output may become a feature, a score, a statistic or a value.** Text and images may enter a learned result **only** through an explicitly authored model definition carrying the full training contract: a versioned immutable snapshot, declared leakage controls, held-out validation, a `model_registry` entry, calibration and drift monitoring.
>
> Retrieval-derived and LLM-derived content is **evidence only**: it may corroborate a deterministic result and may never originate one.

Two paths: **evidence modality** (retrieved and cited, never a feature) and **governed multimodal ML** (the full contract above). No implementation scope is added; both remain future capabilities with interfaces designed.

## B.2 Execution lanes

The six logical job classes are unchanged. **The `ml` class resolves to three physical lanes**: `ml.training` (pre-emptible, checkpointed), `ml.batch_scoring`, and **`ml.online_scoring` with hard-reserved capacity that training and batch admission can never consume**.

**Admission requires two predicates, not one:**

```
admit  iff  running_count < max_concurrency
       AND  sum(compute_weight of running) + compute_weight(candidate) <= resource_capacity
```

`max_concurrency` is how many runs may be in flight; `resource_capacity` is how much scarce resource exists; `compute_weight` is what one run consumes. **One number never expresses two quantities.** The previous single-number form made a weight-4 training job unadmittable into a capacity of 1.

`ml.online_scoring` carries operational event and micro-batch scoring and its required serving functions only. Batch, backfill and rescore work runs on batch and training-class capacity. Where hardware is physically shared, online capacity remains hard-reserved and B-02 must prove the actionable-latency target while training and batch work are saturated.

## B.3 The Semantic Contract Manifest

**An immutable, content-addressed reproducibility pin over the canonical versions in force. Not a fourth authoring authority.** `ppiq_meta.semantic_manifests` with `manifest_id` as primary key, `tenant_id`, `manifest_hash`, and UNIQUE `(tenant_id, manifest_hash)`. No status column, no lifecycle, no update path.

Run and artifact tables carry `semantic_manifest_id uuid NULL FK`. **Nullable for legacy records only. Every new governed AI/ML execution must resolve a manifest**, and a run that cannot is refused rather than recorded without one.

## B.4 The training read path

**PostgreSQL JSONB is not the training read path.** `ppiq_plant.feature_store` owns current governed state, lineage, row-level security and incremental refresh. **The sealed typed columnar artifact owns high-throughput training input.**

No training or encoding component queries `feature_store`. **The snapshot materialiser is exempt by definition** and is the only component permitted to read it for sealing.

The sequence product follows the same split: a manifest in PostgreSQL, immutable chunked typed arrays in object storage, consumed as bounded chunks.

## B.5 Promotion

Promotion is a **three-dimensional gate** on the same governed holdout: quality (discrimination, **calibration**, out-of-time, subgroup stability, missingness robustness, **explanation stability**), serving cost (p50/p95/p99 latency, throughput, artifact size, RAM and VRAM, warm-up), and training cost (duration, peak memory, snapshot read throughput).

**A better-discriminating, worse-calibrated model is not an improvement. An unstable explanation is worse than none.**

The encoder ships only when `metric_lift >= declared_min_lift AND p95_latency_delta <= declared_latency_budget AND artifact_size <= declared_size_class AND explanation_stability >= floor`.

## B.6 Vector search

`VectorSimilarityIndex` remains the contract. Index family is selected by measurement from population, dimension, RAM, required recall@k, latency target, build time and update pattern. **Exact Flat search is retained permanently on a representative sample as the recall baseline**, and a build below the declared recall floor does not become the served index.

## B.7 Assistant runtime

The Assistant gains an implementation-grade runtime, specified in Chapter 4 5.7.9: permission context, intent and entity resolution, **a deterministic tool planner**, structured tools and hybrid retrieval with **permission filtering before ranking**, token-budgeted evidence packing, the model gateway with a minimum-scoped-payload rule, a replaceable `ModelServingRuntime`, and **deterministic answer verification that does not call the LLM**. Quality gates Q-01 to Q-11.

## B.8 Terminology

**MF-01 to MF-07 are seven intelligence and engine families, not seven ML models.** Sub-types: learned model (MF-01, MF-03, MF-04), retrieval and index (MF-02), statistical engine (MF-05, MF-06), practice engine (MF-07), plus orchestration and governance.

## B.9 Gates

The inventory becomes **G-01 to G-55**, adding G-48 training reads no live feature state, G-49 lane isolation, G-50 admission predicate satisfiable, G-51 ANN recall floor, G-52 evidence budget integrity, G-53 claim-class integrity in language, G-54 governed-model-only learned output, G-55 manifest immutability and coverage.

---

*Appendix B, 11 August 2026. Overrides Appendix A and the body where they differ. Chapter amendments are specified in `PPIQ_Master_Design_Chapter_Amendment_Pack.md`.*
