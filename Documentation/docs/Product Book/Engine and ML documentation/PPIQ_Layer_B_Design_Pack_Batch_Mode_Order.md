# PPIQ RULE - LAYER B DESIGN PACK: BATCH DESIGN MODE ORDER

**Status: BINDING RULE**
**Ruled by Karim - 11 August 2026**
**Companion to: PPIQ_Layer_B_Learned_Intelligence_Engine_Rule.md**
**Implementation status: DESIGN ONLY. This order does NOT authorise implementation.**

---

## STANDING POSITION

The Layer B rule is now **frozen design authority**.

- Do NOT create another governance version.
- Do NOT work on Deliverable A alone and return for permission to start B.

This is DESIGN work, not implementation, so use **BATCH DESIGN MODE**.

Produce the complete A-L architecture pack as one coherent system. The pack may be drafted internally in sections, but it is returned only when the interfaces between the sections have been reconciled.

---

## DESIGN PACK GOAL

One architecture, end to end:

```
CUSTOMER DATA
  -> NO-CODE SEMANTIC AUTHORING
  -> GOVERNED INTELLIGENCE INPUTS
  -> INITIAL COMMISSIONING
  -> WEEKLY LEARNING
  -> LIVE MODEL BUNDLE
  -> DAYTIME INFERENCE / RETRIEVAL
  -> GOVERNED INTELLIGENCE DATASETS
  -> PAGE BUILDER / DASHBOARDS
  -> ASSISTANT
```

**Not twelve independent documents that disagree at their boundaries.**

---

## A + B FIRST INTERNALLY - CONTEXT AND INPUT CONTRACT

### External actors and components

- Customer data sources
- No-Code Wiring / governed SQL
- Semantic / relationship registry
- Layer A Exact BI Engine
- Layer B Intelligence Engine
- Model / evidence stores
- Page Builder
- Assistant
- Scheduler / job runtime
- Model registry

### Boundary rule

Layer B sees semantic contracts, not customer physical table names.

**The customer may change from steel to oil to mineral water with NO Layer-B code branch.**

Show where:

- physical schema ends
- semantic model starts
- training data products start

---

## C - INTELLIGENCE DATA PRODUCTS

Define concrete contracts for:

1. Journey / analytical spine
2. Feature store
3. Sequence store
4. Outcome store
5. Evidence store

Also add if justified:

6. Prediction / result store
7. Embedding store / index metadata

For each, define:

- primary grain
- keys
- tenant / site scope
- time semantics
- version references
- partition strategy concept
- lineage / provenance
- mutable vs immutable behaviour

Do not choose a storage product merely because it is fashionable.

**Separate logical contract from physical implementation.**

---

## D - MODEL FAMILY REGISTRY

A structured registry entry for every model family.

### Self-supervised encoder

- Purpose: learn process / journey representation
- Candidate baseline: 1D CNN / temporal convolution vs Transformer-style encoder
- Framework: PyTorch abstraction
- Output: versioned embedding
- Refresh: controlled, not weekly by default

### Similarity index

- Purpose: nearest historical process / journey retrieval
- Contract: `VectorSimilarityIndex`
- Candidate: FAISS or equivalent

### Novelty model

- Purpose: normal / abnormal regime representation without labels
- Multiple candidates allowed

### Supervised outcome family

- Initial primary: LightGBM-style gradient boosting over engineered features plus optional embedding
- Replaceable abstraction

### Effect / practice layer

- Conditioned association -> matched comparison -> observational effect -> experimental evidence

### For every family, include

- eligible inputs
- minimum population
- target / output type
- training method
- validation metrics
- explainability method
- refresh policy
- expected compute class
- refusal states
- emitted dataset or datasets

---

## E - THREE-SCHEDULE ORCHESTRATION

Three completely different execution windows. This is especially important because it answers the product-performance concern.

### 1. INITIAL COMMISSIONING

Allowed: hours to days. It may process the entire governed historical population once.

Requirements:

- checkpoint every expensive stage
- restart from last successful stage
- no requirement to reread raw sources if a later model fails
- publish one atomic compatible `ModelBundle Version 1`

Define explicit stage dependencies.

### 2. WEEKLY UPDATE

Maximum overall product budget: **24 hours**.

Process primarily:

- new / changed data
- delta feature generation
- new embeddings
- drift
- supervised retraining where justified
- calibration
- evidence refresh
- champion / challenger validation
- atomic publish

Do NOT automatically retrain the deep encoder weekly.

### 3. DAYTIME

**NO TRAINING.**

- Normal answer target: seconds
- Bounded analysis: prefer under approximately 30 seconds
- Absolute synchronous product ceiling: under 2 minutes
- Anything more expensive: async analytical job or explicit refusal

**This is a hard architectural rule.**

---

## F - MODEL BUNDLE / REGISTRY / ROLLBACK

One compatible model-bundle concept. A bundle may reference:

- semantic-model version
- feature-schema version
- encoder version
- embedding / index version
- anomaly-model version
- supervised model versions
- calibration versions
- evidence-store snapshot / version

**Never publish incompatible versions independently.**

Define the states: candidate, champion, rejected, quarantined, superseded, rolled back.

Use an atomic live alias / pointer.

MLflow may be the first implementation, but the product contract must not be MLflow-specific.

---

## G - GOVERNED OUTPUT DATASETS

A major requirement. Layer B does NOT terminate at Python model objects.

Define stable generic analytical dataset contracts for:

- Predictions
- Contributors / explanations
- Similarity / historical neighbours
- Anomalies
- Operating envelopes
- Findings / associations / effects
- Model / readiness status

For each dataset specify:

- grain
- dimensions
- measures
- IDs
- units
- timestamps
- model version
- population
- confidence / uncertainty
- evidence / provenance
- tenant / site scope

**These datasets become normal BI assets.**

---

## H - ASSISTANT TOOL CONTRACT

Define the exact Layer-B tools the Assistant can call. For example:

- GetPrediction
- GetPredictionContributors
- FindSimilarJourneys
- GetAnomalyEvidence
- GetOperatingEnvelope
- GetFinding
- ComparePractices
- GetModelReadiness
- GetModelVersion

Do not make the Assistant query model internals directly. Every response must be evidence-bearing.

The Assistant must be able to distinguish: exact Layer-A fact, association, predictive contribution, learned effect, recommendation, refusal.

---

## I - PAGE BUILDER INTEGRATION

Show exactly how a customer creates a dashboard over Layer-B outputs WITHOUT application development.

Generic journey:

```
Add Widget -> select Intelligence dataset -> choose dimensions / measures
  -> choose compatible chart -> filter -> save -> cross-filter
```

Forbidden:

- `if predictionDashboard`
- `if oilCustomer`
- any special React component required merely because the source is ML

Special visualisations are allowed only when they represent a genuinely different visual grammar, never because the data came from Layer B.

---

## J - GENERICITY PROOF

Two conceptual installations.

**Oil.** Terminology appears only in the configuration example. Show how its authored schema maps into the generic contracts.

**Mineral water.** A completely different schema, grain and process configuration. Again only mappings differ.

Then prove these remain unchanged across both:

- model-family registry
- commissioning orchestration
- evidence model
- prediction schemas
- Assistant tools
- dashboard binding

**If the architecture requires an `OilModel` or `BottleModel` product-code class, genericity failed.**

---

## K - SCALE AND HARDWARE PLAN

Do NOT promise one server. Do NOT immediately demand a cluster. Design a sizing framework.

Workload dimensions, at minimum:

- historical rows
- new rows per week
- number of features
- number of sequence channels
- average sequence length
- number of analytical grains
- number of outcomes
- model count
- history window
- CPU
- RAM
- GPU
- storage throughput

Logical execution tiers:

| Tier | Shape |
|---|---|
| **Small** | Single CPU server, optional GPU |
| **Medium** | Larger CPU / RAM plus dedicated GPU |
| **Large** | Partitioned processing and multi-worker execution |
| **Very large** | Distributed feature / training execution where measured evidence requires it |

Do not hardcode thresholds without measurement.

Also distinguish **raw data volume** from **training feature volume**. They are not the same.

---

## L - VALIDATION AND QUALITY GATES

Hard gates covering at minimum:

- data lineage
- relationship quality
- target leakage
- temporal leakage
- class balance
- feature missingness
- outcome sufficiency
- drift
- calibration
- model performance
- subgroup / variant stability
- encoder compatibility
- embedding-version compatibility
- reproducibility
- tenant isolation
- champion / challenger
- rollback
- refusal

**No model reaches production simply because training completed.**

---

## REQUIRED CROSS-CONTRACTS

Explicitly reconcile these interfaces before finalising the pack.

| Interface | Contract |
|---|---|
| **Layer A <-> Layer B** | Exact BI facts remain Layer A. Layer B consumes governed features and emits learned intelligence |
| **Layer B <-> Page Builder** | Layer-B outputs are ordinary governed analytical datasets |
| **Layer B <-> Assistant** | Tool responses are structured and evidence-bearing |
| **Semantic model <-> ML** | Model versions pin the semantic / feature definition used for training |
| **Weekly scheduler <-> model registry** | Nothing becomes live before validation and promotion |

---

## ADDITIONAL DESIGN REQUIREMENT - TRAINING STATE VS SERVING STATE

Show the distinction explicitly.

**Training may involve:** large feature populations, GPUs, hours of compute, temporary candidate artifacts.

**Serving must use:** already-trained models, prepared evidence, prediction stores, vector indexes, bounded feature access.

**The daytime user path must NEVER accidentally enter the training pipeline. This separation is critical.**

---

## FINAL PACK FORMAT

1. Executive architecture summary
2. Context / component architecture
3. End-to-end data flow
4. Input contracts
5. Data-product schemas
6. Model registry
7. Initial commissioning sequence
8. Weekly sequence
9. Daytime sequence
10. Output datasets
11. Assistant integration
12. Page Builder integration
13. Genericity proof
14. Scale / sizing strategy
15. Validation / gates
16. Key tradeoffs and explicitly deferred decisions

Also include **what this design intentionally does NOT decide yet**, so that design is never accidentally read as implementation authorisation.

---

## RETURN CONDITION

Return the complete coherent A-L architecture pack, internally reconciled, together with:

- the major architectural decisions
- the remaining open decisions
- any contradictions found in the existing Layer-B rule

**Do not implement.**

---

*Rule frozen 11 August 2026. This is a work order and a mode of working, not a design in itself. It governs how the A-L pack is produced and what it must contain.*
