# PPIQ RULE - LAYER B: LEARNED INDUSTRIAL INTELLIGENCE ENGINE

**Status: BINDING RULE**
**Ruled by Karim - 11 August 2026**
**Scope: all Layer B design, model, orchestration, registry, output-dataset and Assistant-integration work**
**Implementation status: DESIGN ONLY. Implementation is NOT authorised by this rule.**

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
