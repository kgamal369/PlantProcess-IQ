# PPIQ LAYER B - ARCHITECTURE DESIGN PACK

**Deliverables A to L, produced in one batch**
**Status: DESIGN. NOT IMPLEMENTATION AUTHORISATION.**
**Produced 11 August 2026 against PPIQ_Layer_B_Learned_Intelligence_Engine_Rule.md and PPIQ_Layer_B_Design_Pack_Batch_Mode_Order.md**

**Acceptance question this pack must answer:**
*If Worker 2 receives this pack tomorrow, can he decompose Layer B into implementation tasks without inventing architecture?*

Answered explicitly in section 17. The short version: yes for sections 1 to 12 and 15, with twelve open decisions listed in section 16 that must be ruled before the tasks they affect can be written. Nine of the twelve block only one work package each.

---

## 1. EXECUTIVE ARCHITECTURE SUMMARY

### 1.1 What Layer B is, in one paragraph

Layer B is a set of governed batch pipelines and read-only serving surfaces that consume a **versioned semantic contract** and seven **persistent data products**, produce five **model families** bundled into a single atomically published **ModelBundle**, and emit **seven governed analytical datasets** that the Page Builder and the Assistant consume through the same mechanisms they use for ordinary data. It contains no customer table name, no industry vocabulary, and no per-customer code path.

### 1.2 The three planes

The whole architecture is organised as three planes separated by two hard walls.

```
+---------------------------------------------------------------+
| AUTHORING PLANE                                               |
| customer sources -> wiring canvas / governed SQL              |
| -> semantic model version (published, immutable)              |
+---------------------------------------------------------------+
                    || SEMANTIC WALL
                    || Layer B reads semantic codes only.
                    || No physical table name crosses this wall.
+---------------------------------------------------------------+
| LEARNING PLANE                (training state)                |
| data products -> capability profile -> model training         |
| -> validation gates -> candidate bundle -> promotion          |
| GPUs, hours of compute, temporary artifacts, large scans      |
+---------------------------------------------------------------+
                    || SERVING WALL
                    || One-way. Promotion writes; serving reads.
                    || No serving request can enter the left side.
+---------------------------------------------------------------+
| SERVING PLANE                 (serving state)                 |
| live bundle + evidence/prediction stores + vector index       |
| -> Page Builder datasets, Assistant tools                     |
| bounded memory, bounded time, no training, no GPU required    |
+---------------------------------------------------------------+
```

The two walls are the load-bearing elements of the design. The Semantic Wall is what makes the product generic. The Serving Wall is what makes the daytime latency contract enforceable rather than aspirational.

### 1.3 The ten architectural decisions

| ID | Decision | Consequence |
|---|---|---|
| **AD-01** | Layer B consumes a **published, immutable SemanticModelVersion**, never a live authoring state | A model can always be explained by the exact contract it was trained under |
| **AD-02** | The **analytical spine is a node table**, one row per grain instance per process position, not one row per grain instance | Multi-stage plants, graph routes and continuous intervals share one shape |
| **AD-03** | Every feature carries an **availability position and offset**; `prediction_cutoff` is enforced by the catalogue, not by the modeller | Temporal leakage becomes a mechanical gate, not a review |
| **AD-04** | The **feature store is physically wide, logically declared**. One physical column per feature code per feature_set_version | Boosting reads a columnar slice; the contract stays declarative |
| **AD-05** | The **ModelBundle is the only publishable unit**. Encoder, index, novelty model, supervised models, calibrations and evidence snapshot are promoted together or not at all | Version skew between an embedding and its index becomes structurally impossible |
| **AD-06** | The **vector index is generational and append-only within a generation**. Weekly inserts create a new immutable generation manifest, not a mutation | Resolves the incremental-index versus immutable-bundle conflict (CT-02) |
| **AD-07** | **Every Layer B output carries a claim class** and an evidence envelope. The claim class is a column, not documentation | The Assistant cannot blur association with effect because the data itself refuses to |
| **AD-08** | **Serving has no path to training.** Separate processes, separate database roles, separate queues. Tier 3 creates a job request row; it does not call the trainer | The daytime path cannot accidentally enter the training pipeline |
| **AD-09** | Layer B outputs are **ordinary governed analytical datasets** projected into the same WidgetFact contract as Layer A, with declared aggregation semantics per measure | No ML-specific widget code; no illegal aggregation of probabilities |
| **AD-10** | **Refusal is a materialised row**, not an exception. Every terminal state persists with its reason and population | A dashboard bound to an unready model shows a stated refusal, not an empty chart |

### 1.4 What is new in this pack relative to the frozen rule

The frozen rule states intent and constraints. This pack adds, and Worker 2 needs, the following that the rule does not contain:

- concrete field-level contracts for eleven semantic input objects and seven data products
- the **prediction point** concept as a first-class object, which the rule implies but never defines
- the **collapsed dimension** rule, without which single-shift and single-variant plants fail
- the **residence model** on process position, without which continuous-flow customers cannot be linked at all
- the **intervention flag** on events, without which the effect layer at Level 3 has no input
- the **abort ladder** for the weekly window, without which the 24 hour budget is a hope
- the **claim-class column** and **aggregation semantics**, which close two contradictions in the rule
- the **channel set version**, which determines when a frozen encoder becomes invalid rather than merely stale

---

## 2. CONTEXT AND COMPONENT ARCHITECTURE (Deliverable A)

### 2.1 Component inventory

| # | Component | Plane | Responsibility | May read | May write |
|---|---|---|---|---|---|
| 1 | Customer sources | External | Systems of record | - | - |
| 2 | Import / DB-link jobs | Authoring | Land raw data | Sources | Dump Store |
| 3 | Wiring Canvas / governed SQL | Authoring | Human declares meaning | Dump Store | Semantic definitions |
| 4 | Semantic Registry | Authoring | Versions and publishes the semantic model, holds the extensible role registry | Definitions | SemanticModelVersion |
| 5 | Canonical projection jobs | Authoring | Materialise Plant Data from Dump Store per the semantic model | Dump Store, SemanticModelVersion | Plant Data |
| 6 | **Layer A Exact BI Engine** | Serving | Deterministic aggregation | Plant Data, intelligence datasets | Nothing |
| 7 | **Data Product Builder** | Learning | Builds spine, features, sequences, outcomes | Plant Data, SemanticModelVersion | Data products |
| 8 | **Capability Profiler** | Learning | Measures what this installation supports | Data products | Capability profile |
| 9 | **Model Trainers** (5 families) | Learning | Fit candidate models | Data products, profile | Candidate artifacts |
| 10 | **Validation Gate Runner** | Learning | Runs G-01..G-20 | Candidates, data products | Gate results |
| 11 | **Bundle Promoter** | Learning -> Serving | Atomic promotion, the only writer across the Serving Wall | Candidates, gate results | Live alias, serving stores |
| 12 | **Model Registry** | Both | Versions, lineage, aliases, rollback | - | Registry records |
| 13 | **Inference Service** | Serving | Scores new instances with the live bundle | Live bundle, feature store slice | Prediction store |
| 14 | **Evidence Materialiser** | Learning | Turns model outputs into governed datasets | Candidates, stores | Evidence store |
| 15 | **Intelligence Orchestrator** | Serving | Routes a question to tier 1, 2 or 3 | Serving stores | Job request rows only |
| 16 | **VectorSimilarityIndex** | Serving | ANN retrieval | Index generation files | Nothing at query time |
| 17 | **Drift Supervisor** | Learning | Monitors and decides actions | Serving and learning stores | Supervisor decisions |
| 18 | **Scheduler / job runtime** | Learning | Runs the three schedules, checkpointing | Job state | Job state |
| 19 | **Page Builder** | Serving | Binds datasets to widgets | Intelligence metadata, datasets | Widget definitions |
| 20 | **Assistant** | Serving | Orchestrates tools, composes answers | Layer A tools, Layer B tools | Nothing |

### 2.2 The Semantic Wall

**Rule.** No component numbered 7 or higher may reference a customer physical table name, column name, schema name, or industry term. Their only vocabulary is the semantic code space defined in section 4.

**Enforcement, three layers, mirroring the existing isolation doctrine:**

| Layer | Mechanism |
|---|---|
| Database | The Layer B role holds grants on Plant Data and the intelligence schema only. No grant on Dump Store, no grant on any source-shaped schema |
| Application | Data Product Builder resolves every source reference through `SemanticModelVersion` -> `definition_ref`. A literal identifier in a Layer B code path has no resolution path and cannot execute |
| Test | An architecture test asserting that no file under the Layer B tree contains a customer identifier, an industry noun from the prohibited-vocabulary list, or a `switch` on tenant, site, or industry. Falsified once before it is trusted |

**The prohibited-vocabulary list is itself configuration**, seeded with the vocabulary of every installation encountered, so it grows as customers are added. It is not a hardcoded steel list.

### 2.3 The Serving Wall

**Rule.** The serving plane may read the live bundle and the serving stores. It may never invoke a trainer, allocate a GPU, scan a raw source, or perform an unbounded scan of a data product.

**Enforcement:**

| Layer | Mechanism |
|---|---|
| Process | Serving runs in a separate process group with no import of any trainer module. A dependency test asserts the serving assembly does not reference the training assembly |
| Database | The serving role has SELECT only on serving stores, plus INSERT on `analysis_job_request` and `prediction_store`. No DDL, no access to candidate artifacts |
| Runtime | Every serving query carries a cost estimate and a hard statement timeout. Tier 3 inserts a job request row and returns; it does not await |
| Test | A gate asserting no serving code path can reach a training entry point, and that the statement timeout is set on every serving connection |

### 2.4 Where the boundaries fall

| Boundary | Ends at | Starts at |
|---|---|---|
| Physical schema | Dump Store and the customer's own systems | - |
| Semantic model | - | `SemanticModelVersion` publication |
| Training data products | Canonical Plant Data | `journey_spine` materialisation |
| Learned intelligence | Data products | Model training |
| Serving | Bundle promotion | Live alias switch |

---

## 3. END-TO-END DATA FLOW (Deliverable A, continued)

```
 (1) CUSTOMER SOURCES
      |  import jobs, read-only toward the customer
      v
 (2) DUMP STORE                          as it arrived, uninterpreted
      |  Wiring Canvas / governed SQL. A HUMAN declares meaning.
      |  Output: definitions, not data.
      v
 (3) SEMANTIC MODEL VERSION (immutable, published, hashed)
      |         |
      |         +--> (4) CANONICAL PROJECTION -> PLANT DATA
      |                        |
      |                        v
      |         =============== SEMANTIC WALL ===============
      |                        |
      +----------------------->+
                               v
 (5) DATA PRODUCT BUILDER
      spine -> features -> sequences -> outcomes
                               |
                               v
 (6) CAPABILITY PROFILE       what this installation can support
                               |
                               v
 (7) MODEL TRAINING           encoder, index, novelty, supervised, effect
                               |
                               v
 (8) VALIDATION GATES         G-01..G-20
                               |
                               v
 (9) CANDIDATE BUNDLE  --> champion/challenger --> PROMOTION
                               |
      =============== SERVING WALL (one-way) ===============
                               |
                               v
(10) LIVE MODEL BUNDLE + EVIDENCE STORE + PREDICTION STORE + INDEX
                               |
              +----------------+----------------+
              v                                 v
(11) GOVERNED INTELLIGENCE DATASETS      (12) ASSISTANT TOOLS
              |                                 |
              v                                 v
     PAGE BUILDER WIDGETS                 ASSISTANT ANSWERS
     (same binding path as Layer A)       (fact + finding + evidence
                                           + qualification)
```

**The two facts a reader should take from this diagram.** First, the semantic model is the only thing that crosses from the customer's world into the engine, and it crosses as codes. Second, promotion is the only arrow that crosses the Serving Wall, and it points one way.

---

## 4. LAYER B INPUT CONTRACT (Deliverable B)

Eleven objects. Layer B reads these and nothing else. Every one is tenant-scoped and site-scoped; those columns are omitted from the field lists below and assumed present on all.

### SM-01 SemanticModelVersion

The root pin. Every model, feature row and finding references exactly one.

| Field | Type | Notes |
|---|---|---|
| `semantic_model_version_id` | uuid | PK |
| `status` | enum | draft, validated, published, superseded, rolled_back |
| `published_at_utc` | timestamptz | Null unless published |
| `content_hash` | text | Hash over all SM-02..SM-11 content |
| `predecessor_version_id` | uuid | Null for the first |
| `authored_by` | text | The customer engineer |
| `validation_report_ref` | text | Result of the IV gates below |

**Rule.** Layer B refuses to train against any status other than `published`. A published version is immutable; a correction is a new version.

### SM-02 GrainDefinition

| Field | Type | Notes |
|---|---|---|
| `grain_code` | text | PK within version |
| `grain_kind` | enum | discrete_item, batch, lot, campaign, process_window, flow_interval, custom |
| `semantic_role_code` | text | FK to SM-11 |
| `time_semantics` | enum | instant, interval |
| `identity_definition_ref` | text | Points at the authored definition producing the identity, never a table name in Layer B code |
| `is_primary_analytical_grain` | bool | Exactly one true per version |
| `parent_grain_code` | text | Nullable, for nested grains |
| `expected_cardinality_per_day` | bigint | Declared, used for sizing |

**No industry example appears in engine code.** `grain_kind` is the only thing the engine branches on, and only for time semantics.

### SM-03 ProcessPosition

| Field | Type | Notes |
|---|---|---|
| `position_code` | text | PK within version |
| `position_kind` | enum | unit, stage, operation, virtual |
| `ordinal` | int | Nullable when the route is a graph |
| `predecessor_position_codes` | text[] | Graph edges |
| `successor_position_codes` | text[] | |
| `is_terminal` | bool | |
| `residence_model_kind` | enum | **none, fixed_lag, lag_with_dispersion** |
| `residence_lag_seconds` | numeric | Required when kind is not none |
| `residence_dispersion_seconds` | numeric | Required for lag_with_dispersion |

**Why residence is here.** For a continuous or flow-interval grain there is no tracking system to say which material was at which position when. The residence model is the declared substitute. Without it, a continuous-process customer has genealogy strength zero and loses outputs A and C entirely. This field is the single cheapest thing that opens the continuous-process market.

### SM-04 RelationshipEdge

The genealogy contract. Consumed as a graph; never rediscovered.

| Field | Type | Notes |
|---|---|---|
| `edge_code` | text | PK within version |
| `parent_grain_code` / `child_grain_code` | text | |
| `edge_kind` | enum | identity, transformation, temporal, containment |
| `cardinality` | enum | one_to_one, one_to_many, many_to_one, many_to_many |
| `weight_semantics` | enum | none, proportion |
| `origin` | enum | authored, inferred |
| `confidence` | numeric | Required when origin is inferred |
| `temporal_validity_from` / `_to` | timestamptz | Nullable |
| `definition_ref` | text | The authored join, versioned |

**Genealogy strength is derived, not declared:** `none` when no edge connects two positions, `sequential` when only identity and temporal edges exist, `transformational` when any edge carries proportion weights or many_to_many cardinality.

### SM-05 ParameterDefinition

| Field | Type | Notes |
|---|---|---|
| `parameter_code` | text | PK within version |
| `semantic_role_code` | text | FK to SM-11 |
| `physical_quantity` | text | speed, temperature, pressure, mass_flow, ... from a registry |
| `unit_code` | text | UCUM-style code |
| `data_type` | enum | numeric, categorical, boolean, text |
| `value_kind` | enum | setpoint, actual, derived |
| `controllability` | enum | **controllable, observed, unknown** |
| `grain_code` / `process_position_code` | text | Where it lives |
| `timestamp_availability` | enum | per_sample, per_grain, none |
| `nominal_sampling_hz` | numeric | Nullable, used for sizing and sequence policy |
| `valid_range_min` / `_max` | numeric | Nullable |
| `missing_policy` | enum | drop, impute_declared, treat_as_category |
| `definition_ref` / `definition_version` | text | |

`physical_quantity` plus `unit_code` is what makes unit-sane answers structurally possible rather than hoped for. `controllability` is what separates an actionable finding from a true but useless one.

### SM-06 OutcomeDefinition

| Field | Type | Notes |
|---|---|---|
| `outcome_code` | text | PK within version |
| `outcome_type` | enum | binary, categorical, ordinal, continuous |
| `class_taxonomy_ref` | text | Required for categorical and ordinal |
| `ordinal_rank_map` | jsonb | Required for ordinal |
| `grain_code` | text | |
| `detection_position_code` | text | **Where it becomes known** |
| `detection_timestamp_field` | text | **When it becomes known** |
| `direction` | enum | higher_is_better, lower_is_better, target_band, none |
| `unit_code` | text | For continuous |
| `censoring_policy` | enum | none, right_censored, interval |
| `definition_ref` / `definition_version` | text | |

**`detection_position_code` and `detection_timestamp_field` are the leakage anchors.** Everything in gate G-06 derives from them.

### SM-07 EventDefinition

| Field | Type | Notes |
|---|---|---|
| `event_code` | text | PK within version |
| `event_kind` | enum | alarm, failure, stop, maintenance, operator_action, state_change, **intervention** |
| `scope` | enum | grain, position, resource, site |
| `start_timestamp_field` / `end_timestamp_field` | text | End nullable for instantaneous |
| `severity_field` | text | Nullable |
| `is_intervention` | bool | **True when the event records a deliberate corrective action** |
| `intervention_target_parameter_code` | text | Nullable, what the action was aimed at |
| `intervention_dose_field` | text | Nullable, magnitude of the action |

**Why `is_intervention` exists.** The frozen rule requires a Level 3 and Level 4 effect layer and lists intervention history in the capability profile, but the declaration contract as written has no object that identifies an intervention. Without this flag, the effect layer above Level 2 has no input and must always refuse. This is contradiction CT-05, closed here.

### SM-08 ContextDimension

| Field | Type | Notes |
|---|---|---|
| `dimension_code` | text | PK within version |
| `semantic_role_code` | text | variant, shift, crew, campaign, ambient, other |
| `applies_at` | enum | grain, position, time_window |
| `level_source_ref` | text | Where its levels come from |
| `is_variant_dimension` | bool | True for the dimension along which correct settings change |

**Derived at profile time, not declared:** `observed_level_count` and `collapse_status`.

**The collapse rule.** When `observed_level_count = 1`, the dimension is **COLLAPSED**. A collapsed dimension is removed from every conditioning set, every stratification and every subgroup check, and the removal is stated in the finding's `conditioning` field with reason `collapsed_single_level`. A collapsed dimension is never an error, never a warning, and never causes a method to refuse. A plant with one shift, one variant or one line is a normal customer, not a degraded one.

### SM-09 ResourceDefinition

| Field | Type | Notes |
|---|---|---|
| `resource_code` | text | PK within version |
| `resource_class` | text | From the role registry |
| `instance_identity_available` | bool | Do we know which physical unit |
| `process_position_code` | text | Where it acts |
| `life_counter_parameter_code` | text | Nullable, wear or usage counter |

### SM-10 SpecificationDefinition

| Field | Type | Notes |
|---|---|---|
| `spec_code` | text | PK within version |
| `variant_dimension_code` | text | FK to SM-08 |
| `target_parameter_code` | text | FK to SM-05 |
| `target_value` / `lower_bound` / `upper_bound` | numeric | |
| `applies_at_position_code` | text | |

Specifications are inputs to envelope comparison, never a substitute for learned envelopes.

### SM-11 SemanticRoleRegistry

| Field | Type | Notes |
|---|---|---|
| `role_code` | text | PK |
| `role_kind` | enum | position, unit, observation, practice, event, outcome, resource, input, specification, relationship, **extension** |
| `parent_role_code` | text | Nullable, allows specialisation |
| `is_core` | bool | Core roles ship with the product |
| `added_in_semantic_model_version` | uuid | Provenance for extensions |
| `label` / `description` | text | Human-facing only |

**The registry is extensible by configuration.** A new installation may add roles under `extension` or as children of a core role. Layer B code branches only on `role_kind`, never on `role_code`. Adding a role never requires a code change; this is the mechanical statement of section 3 of the frozen rule.

### SM-12 PredictionPoint (derived object, defined here because the rule assumes it without naming it)

| Field | Type | Notes |
|---|---|---|
| `prediction_point_code` | text | PK |
| `position_code` | text | Position after which prediction is made |
| `offset_seconds` | numeric | Zero means at position exit |
| `cutoff_rule` | enum | position_exit, position_entry, fixed_offset |
| `is_active` | bool | Governed, see the model-count governor in section 6.7 |

A prediction point is what turns "predict quality" into a trainable, gate-checkable object. Every supervised model is keyed on one. Prediction points are generated as candidates from the position graph and activated by the governor, never all at once.

### 4.1 Input validation contract (the relationship-quality gates)

The frozen rule states that a Wiring Canvas line is an authored hypothesis, not proof. These are the measurements that turn that principle into an executable gate. All run before the first training stage and are re-run weekly.

| ID | Metric | Definition | Consumed by |
|---|---|---|---|
| **IV-01** | Join coverage | Fraction of child rows resolving to a parent through the declared edge | Genealogy strength |
| **IV-02** | Orphan rate | Child rows with no parent | G-02 |
| **IV-03** | Duplicate explosion factor | Rows after join divided by rows before | G-02 |
| **IV-04** | Cardinality conformance | Observed cardinality versus declared | G-02 |
| **IV-05** | Temporal validity | Fraction of edges where child start is at or after parent start | G-02, G-06 |
| **IV-06** | Impossible edge count | Edges implying future-to-past causation | **Hard block** |
| **IV-07** | Weight closure | For proportion edges, sum per child within tolerance of 1.0 | G-02 |
| **IV-08** | Temporal alignment quality | Fraction of observations placeable inside a grain's residence window | Capability profile |

**IV-06 is the only one that hard-blocks the whole run.** An impossible edge is not a data quality score, it is a declared falsehood, and every downstream number inherits it.

---

## 5. INTELLIGENCE DATA PRODUCT SCHEMAS (Deliverable C)

Seven products. For each: logical contract first, physical implementation named as replaceable.

**Convention.** Every table carries `tenant_id`, `site_id`, `semantic_model_version_id`, `created_at_utc`, `source_definition_version`. These are omitted from the field lists.

### DP-1 Journey / analytical spine

**Purpose.** The one row everything else attaches to.

**Primary grain.** One row per (grain instance, process position). A grain instance visiting five positions produces five spine nodes.

| Field | Type | Notes |
|---|---|---|
| `spine_node_id` | uuid | PK, surrogate |
| `grain_code` | text | FK SM-02 |
| `grain_instance_key` | text | The customer's own identity, as a string, opaque to the engine |
| `process_position_code` | text | FK SM-03 |
| `position_ordinal` | int | Materialised from the route walk, null for pure graphs |
| `start_utc` / `end_utc` | timestamptz | The residence window at this position |
| `start_local` / `plant_tz` / `utc_offset_minutes` | | Local-time semantics preserved |
| `route_path_id` | text | Which route variant this instance took |
| `variant_key` | text | Value of the variant dimension |
| `context_keys` | jsonb | All other context dimension values |
| `parent_spine_node_ids` | uuid[] | Materialised from SM-04 walk |
| `parent_weights` | numeric[] | Aligned with the above, null when weight_semantics is none |
| `genealogy_strength` | enum | none, sequential, transformational |
| `window_origin` | enum | **tracked, residence_derived** |
| `is_superseded` | bool | Correction marker |

**`window_origin` matters.** A residence-derived window is an estimate. Any finding built on residence-derived windows inherits an uncertainty that a tracked window does not have, and the evidence envelope must say so.

**Mutability.** Append-only. A correction inserts a new node and sets `is_superseded` on the old. Never updated in place.

**Partitioning concept.** By tenant, then by `start_utc` month. Route and variant are secondary indexes, not partition keys, because their cardinality varies wildly between customers.

**Lineage.** `source_definition_version` plus `semantic_model_version_id` reproduce the node exactly.

**Retention.** Full history. This is the cheapest product and the most reused.

### DP-2 Feature store

**Purpose.** The training and scoring matrix.

**Primary grain.** One row per (spine_node_id, prediction_point_code, feature_set_version).

**Two parts: a catalogue and a matrix.**

**DP-2a `feature_catalogue`** - the declarative half.

| Field | Type | Notes |
|---|---|---|
| `feature_code` | text | PK within feature_set_version |
| `feature_set_version` | int | |
| `source_parameter_code` | text | Nullable for event and context features |
| `source_event_code` | text | Nullable |
| `aggregation` | enum | mean, std, min, max, first, last, slope, range, count, duration, time_above, time_below, excursion_count, delta_first_last, quantile |
| `aggregation_arg` | numeric | Threshold or quantile |
| `window_kind` | enum | position_residence, rolling_time, rolling_count, since_start |
| `window_arg` | numeric | |
| `at_position_code` | text | Where the feature is computed |
| **`available_from_position_code`** | text | **Earliest position at which the value is legally known** |
| **`available_from_offset_seconds`** | numeric | **Additional lag before it is legally known** |
| `unit_code` / `physical_quantity` | text | Inherited from SM-05 |
| `dtype` | enum | numeric, categorical, boolean |
| `is_controllable` | bool | Inherited from SM-05 controllability |
| `missing_indicator_present` | bool | |

**AD-03 in mechanical form.** A feature is legal for a prediction point if and only if `available_from_position` is at or before the prediction point's position in the route graph, and `available_from_offset` does not push it past the cutoff timestamp. Gate G-06 evaluates exactly this expression. A modeller cannot include an illegal feature because the feature selector reads the catalogue, not a hand-written list.

**DP-2b `feature_matrix`** - the wide half.

| Field | Type |
|---|---|
| `feature_row_id` | uuid PK |
| `spine_node_id` | uuid |
| `grain_instance_key` | text |
| `prediction_point_code` | text |
| `prediction_cutoff_utc` | timestamptz |
| `feature_set_version` | int |
| `feature_schema_hash` | text |
| `f_<feature_code>` | numeric or text, one physical column per catalogue entry |
| `m_<feature_code>` | boolean missing indicator, only where declared |

**Mutability.** Immutable per `feature_set_version`. A changed feature definition produces a new version; it never rewrites history. This is what allows a model to pin `feature_schema_hash` and still be reproducible a year later.

**Physical implementation, replaceable.** Parquet partitioned by tenant and month, read through a columnar engine, with a materialised recent slice in the operational database for serving. **OD-01 (open) selects between DuckDB over Parquet and a columnar Postgres extension after benchmark.** The logical contract above does not change with that choice.

### DP-3 Sequence store

**Purpose.** Raw shape, only where representation learning is justified.

**Primary grain.** One row per (spine_node_id, channel_code).

| Field | Type | Notes |
|---|---|---|
| `sequence_id` | uuid | PK |
| `spine_node_id` | uuid | |
| `channel_code` | text | Derived from parameter_code plus position |
| **`channel_set_version`** | int | **The set of channels the encoder was trained on** |
| `t0_utc` | timestamptz | |
| `sample_interval_ms` | int | Null when irregular |
| `values` | float32[] | |
| `offsets_ms` | int32[] | Present only when irregular |
| `mask` | uint8[] | 1 where observed |
| `length` | int | |
| `unit_code` | text | |
| `resample_policy` | enum | none, linear, last_value, decimate |
| `truncated` | bool | Length cap hit |
| `completeness` | numeric | Observed fraction |

**`channel_set_version` is the answer to CT-06.** An encoder is not merely frozen or stale; it is either compatible or incompatible with the current channel set. Adding a production unit or a new instrument changes the channel set, and an encoder trained on the old set cannot encode the new one. The bundle records both, and gate G-13 refuses to serve an encoder whose `channel_set_version` does not match the current one.

**Mutability.** Immutable. **Retention is policy-driven and is the largest storage item in the system**; see section 14.4.

### DP-4 Outcome store

**Primary grain.** One row per (grain instance, outcome_code, outcome_definition_version).

| Field | Type | Notes |
|---|---|---|
| `outcome_row_id` | uuid | PK |
| `grain_instance_key` | text | |
| `spine_node_id` | uuid | The node where detection occurred |
| `outcome_code` | text | |
| `outcome_definition_version` | int | |
| `value_numeric` | numeric | Continuous |
| `value_class` | text | Categorical |
| `value_ordinal_rank` | int | Ordinal |
| `value_bool` | bool | Binary |
| `detected_at_utc` | timestamptz | **Leakage anchor** |
| `detection_position_code` | text | **Leakage anchor** |
| `observed` | bool | False means known-unobserved, not missing |
| `censored` | bool | |
| `label_confidence` | numeric | Nullable |

**The `observed` flag is not cosmetic.** A grain instance with no defect row may mean "inspected and clean" or "never inspected". Treating the second as the first is the most common silent labelling error in plant data and it inflates every model. The distinction must come from the semantic model, and where it cannot, the capability profile reports outcome availability as `ambiguous_negative` and gate G-08 blocks binary classification on that outcome.

### DP-5 Embedding store and index metadata

**DP-5a `embedding_store`** - one row per (spine_node_id or grain_instance, encoder_version).

| Field | Type |
|---|---|
| `embedding_id` | uuid PK |
| `subject_kind` | enum: spine_node, grain_instance |
| `subject_id` | text |
| `encoder_version` | text |
| `channel_set_version` | int |
| `vector` | float32[] |
| `input_completeness` | numeric |
| `encoded_at_utc` | timestamptz |

**DP-5b `index_generation`** - the manifest that makes AD-06 work.

| Field | Type | Notes |
|---|---|---|
| `index_version` | text | PK |
| `encoder_version` | text | |
| `generation_no` | int | Increments weekly |
| `base_index_version` | text | The generation this one extends |
| `distance_metric` | enum | cosine, l2, inner_product |
| `index_params` | jsonb | Implementation-specific, never a product contract |
| `vector_count` | bigint | |
| `population_filter` | text | Which spine nodes are indexed |
| `built_at_utc` | timestamptz | |
| `is_sealed` | bool | Sealed generations are immutable |

**How AD-06 resolves CT-02.** Weekly insertion does not mutate a published index. It creates generation N+1 whose manifest names generation N as its base and lists the delta. Search fans out over the base plus the delta and merges. The bundle pins `index_version`, which names an exact generation chain. A full rebuild seals a new base and resets `generation_no` to zero. **OD-04 (open) sets the rebuild trigger: a generation count ceiling, a delta-fraction ceiling, or a measured recall floor.**

### DP-6 Prediction store

**Primary grain.** One row per (grain_instance, prediction_point, outcome_code, bundle_version).

| Field | Type |
|---|---|
| `prediction_id` | uuid PK |
| `grain_instance_key` | text |
| `spine_node_id` | uuid |
| `prediction_point_code` | text |
| `outcome_code` | text |
| `predicted_value` | numeric |
| `predicted_class` | text |
| `class_probabilities` | jsonb |
| `risk_band` | text |
| `calibrated` | bool |
| `calibration_version` | text |
| `model_version` | text |
| `bundle_version` | text |
| `feature_row_id` | uuid |
| `scored_at_utc` | timestamptz |
| `is_current` | bool |

**Mutability.** Append-only. A rescore under a new bundle inserts a new row and clears `is_current` on the old. **This is what allows a customer to ask why an answer changed between two Mondays and receive both rows.**

### DP-7 Evidence store

The umbrella product. Every row of every sub-product carries the same envelope.

**The EvidenceEnvelope, embedded in all evidence rows:**

| Field | Type | Notes |
|---|---|---|
| `evidence_id` | uuid | PK |
| `evidence_kind` | enum | finding, envelope, contributor, neighbour, anomaly, readiness, refusal |
| **`claim_class`** | enum | **ASSOCIATION, PREDICTIVE_CONTRIBUTION, MATCHED_EFFECT_ESTIMATE, CAUSAL_EVIDENCE** |
| `method_code` | text | The exact method used |
| `terminal_state` | enum | FINDING, INSUFFICIENT_DATA, NOT_APPLICABLE, REFUSED_BY_GUARD, CONTRADICTED_BY_CONTROL, MODEL_NOT_READY |
| `refusal_reason` | text | **Required when terminal_state is not FINDING. Must name the limitation, never blame the data unless the data is the measured cause** |
| `population_n` | bigint | |
| `conditioning` | jsonb | Dimensions conditioned on, plus any marked `collapsed_single_level` |
| `effect_value` / `effect_lower` / `effect_upper` | numeric | |
| `uncertainty_method` | text | |
| `support_overlap` | numeric | For matched estimates |
| `window_origin_mix` | jsonb | Fraction tracked versus residence-derived |
| `bundle_version` / `model_version` / `semantic_model_version_id` | text | |
| `training_window_from` / `_to` | timestamptz | |
| `computed_at_utc` | timestamptz | |
| `supersedes_evidence_id` | uuid | Nullable |

**AD-10 in mechanical form.** A refusal is a row in this table with a terminal state and a reason. It has a population, a method and a version like any finding. It renders. It is queryable. It is never an empty result set.

**The refusal-reason discipline, which exists because of a measured past defect.** When a method is unavailable, `refusal_reason` must attribute the limitation to the method. Attributing it to the data, for example reporting zero variance when the true cause is an unsupported type pairing, is a defect of the same class as a false finding. Gate G-19 asserts that every `NOT_APPLICABLE` row names a method-side cause and every `INSUFFICIENT_DATA` row carries the measured statistic that failed its threshold.

### 5.1 Product dependency order

```
DP-1 spine
  |-> DP-2 features -----------+
  |-> DP-3 sequences --+       |
  |-> DP-4 outcomes ---|-------+
                       v       v
                 DP-5 embeddings/index
                       |       |
                       +-------+--> DP-6 predictions
                                    |
                                    v
                               DP-7 evidence
```

Nothing later may be built before everything earlier is complete for the same `semantic_model_version_id`.

---

## 6. MODEL FAMILY REGISTRY (Deliverable D)

### MF-01 Self-supervised process encoder

| Attribute | Value |
|---|---|
| **Purpose** | Learn a representation of a process journey without labels |
| **Eligible inputs** | DP-3 sequence store, at least 2 channels, and DP-1 spine for context |
| **Eligibility requirements** | Channel count at least 2; median sequence length at least 32; at least 20,000 sequences; sequence completeness median at least 0.6; `channel_set_version` stable across at least 80 percent of the training window |
| **Minimum population** | 20,000 sequences (initial threshold, **OD-05: confirm by benchmark**) |
| **Output** | Fixed-dimension embedding, 128 to 256, plus `encoder_version` and `channel_set_version` |
| **Training method** | Masked-value reconstruction as the baseline objective; contrastive and next-position variants benchmarked as alternates |
| **Candidate architectures** | 1D temporal convolution baseline versus Transformer-style encoder. **Selection by measured downstream lift, not by architecture preference.** The comparison is a required commissioning artifact |
| **Framework** | PyTorch, behind a `ProcessEncoder` abstraction |
| **Validation metrics** | Held-out reconstruction error; kNN outcome purity on labelled subset where labels exist; embedding stability under resampling; downstream lift when added to MF-04 |
| **Explainability** | None directly. The encoder never faces the user; its outputs are inputs to MF-02 and MF-04 |
| **Refresh policy** | **Frozen between governed refreshes.** Retrained on: scheduled quarterly window, representation drift above threshold, `channel_set_version` change, or major regime change declared by MF-supervisor |
| **Compute class** | GPU preferred, CPU acceptable at small scale. Hours to days at commissioning |
| **Refusal states** | MODEL_NOT_READY when population or channel eligibility fails. **A customer with no sequence store is a valid customer**; the whole family is skipped and MF-04 runs without embeddings |
| **Emitted datasets** | DP-5a embeddings. No user-facing dataset |

**Important consequence.** MF-01 is optional. Every downstream family must function without it. A customer with per-grain aggregates only and no time series still receives outputs from MF-02 through MF-05 using engineered features alone.

### MF-02 Vector similarity index

| Attribute | Value |
|---|---|
| **Purpose** | Retrieve historical journeys resembling a subject |
| **Eligible inputs** | DP-5a embeddings, or DP-2 standardised numeric features when MF-01 is skipped |
| **Eligibility** | At least 5,000 indexable subjects |
| **Output** | Neighbour lists with distance, plus the neighbour's available outcomes and practices |
| **Contract** | `VectorSimilarityIndex` with methods build, seal, extend, search, recall_probe. **FAISS is the initial implementation and is not the contract** |
| **Validation** | Recall at k against exact brute-force search on a sample; latency at p95; generation-chain recall after N extensions |
| **Explainability** | Distance plus the feature-space differences between subject and neighbour, rendered as the contributor dataset |
| **Refresh** | Extend weekly as a new sealed generation; full rebuild per OD-04 trigger; mandatory rebuild on encoder change |
| **Compute class** | CPU sufficient at small and medium; GPU optional at very large |
| **Refusal states** | MODEL_NOT_READY below population; NOT_APPLICABLE when embeddings and standardised features are both absent |
| **Emitted datasets** | Similarity dataset (section 10.3) |

**Fallback path matters.** When MF-01 is skipped, MF-02 runs on standardised engineered features with a declared distance metric. Similarity quality is lower and the evidence envelope says so through `method_code`. The customer still gets a fingerprint.

### MF-03 Normal / novelty model

| Attribute | Value |
|---|---|
| **Purpose** | Represent normal operation without labels; score novelty |
| **Eligible inputs** | DP-5a embeddings or DP-2 features |
| **Eligibility** | At least 5,000 subjects; a declarable reference window judged regime-stable by the profiler |
| **Output** | Novelty score, percentile, nearest normal regime, contributing channels or features |
| **Candidates** | Robust Mahalanobis on embeddings, isolation-based methods, reconstruction error, density or cluster methods. **Selected per installation by calibration and false-positive behaviour, recorded in the registry** |
| **Validation** | False-positive rate on a held-out stable window against a declared budget; score stability; separation on known-abnormal periods where they exist |
| **Explainability** | Per-feature or per-channel contribution to the novelty score |
| **Refresh** | Refit weekly on a rolling reference window; cheap |
| **Compute class** | CPU, minutes |
| **Refusal states** | INSUFFICIENT_DATA below population; MODEL_NOT_READY when no regime-stable reference window exists |
| **Emitted datasets** | Anomaly dataset (section 10.4) |

**Product rule carried from the frozen rule into the data.** The anomaly dataset carries `novelty_score` and, separately and only when an outcome model exists, `associated_outcome_rate`. There is no column named "bad". Unusual is not bad, and the schema refuses to imply it.

### MF-04 Supervised outcome family

| Attribute | Value |
|---|---|
| **Purpose** | Predict a declared outcome at a declared prediction point |
| **Eligible inputs** | DP-2 features legal for the prediction point, plus optional DP-5a embedding columns, plus context |
| **Eligibility** | See the eligibility expression below |
| **Output** | Probability or value, risk band, calibrated |
| **Primary implementation** | Gradient-boosted trees, LightGBM initially, behind `SupervisedOutcomeModel` |
| **Alternates** | XGBoost, CatBoost, regularised linear baseline. **A regularised linear or single-tree baseline is mandatory as the floor comparison** |
| **Validation** | Discrimination, calibration error, precision and recall at operating thresholds, class-specific performance, subgroup stability across variant levels, missingness robustness |
| **Explainability** | TreeSHAP, emitted as the contributor dataset with `claim_class = PREDICTIVE_CONTRIBUTION` |
| **Refresh** | Full retrain weekly on the governed rolling window; recalibrate weekly on recent holdout |
| **Compute class** | CPU, minutes to a few hours depending on model count |
| **Refusal states** | INSUFFICIENT_DATA, NOT_APPLICABLE, REFUSED_BY_GUARD (leakage), MODEL_NOT_READY |
| **Emitted datasets** | Prediction dataset, contributor dataset |

**Eligibility expression, evaluated per (outcome_code, prediction_point_code) candidate:**

```
labelled_n            >= 500
AND minority_fraction >= 0.03            (classification only)
AND distinct_values   >= 20              (regression only)
AND legal_feature_n   >= 5
AND leakage_gate_G06  == PASS
AND outcome_availability != ambiguous_negative
AND history_span      >= 90 days
AND regime_stability  >= threshold
```

Thresholds are initial and are **OD-06**: to be confirmed by measurement, not by preference. Any candidate failing the expression produces a `MODEL_NOT_READY` evidence row naming the failed clause and its measured value. It does not silently disappear.

### 6.7 The model-count governor

The frozen rule warns against training hundreds of models but sets no mechanism. Without one, the candidate space is `outcomes x prediction_points x variant_levels` and grows multiplicatively. This closes CT-01.

**Governor rules:**

1. Candidates are enumerated, not trained. The full candidate list with eligibility results is a commissioning artifact.
2. Prediction points are activated in order of **information gain per position**, computed once at commissioning from the staged-attribution curve, not guessed.
3. A per-bundle **model budget** caps active supervised models. Initial default 50 per site. **OD-07** sets whether the budget is a fixed count, a compute-time budget, or both.
4. Variant-level models are never created by default. A single model with variant as a feature is the default; per-variant models require measured evidence of interaction and consume budget.
5. A model that fails champion-challenger twice consecutively is **quarantined** and releases its budget slot.

### MF-05 Effect and practice layer

| Attribute | Value |
|---|---|
| **Purpose** | Operating envelopes, matched practice comparison, intervention effect, remediation evidence |
| **Eligible inputs** | DP-1, DP-2 (controllable features only for recommendations), DP-4, DP-7 events where `is_intervention` |
| **Output** | Envelope rows and finding rows |
| **Staged levels** | L1 conditioned association; L2 matched or stratified comparison; L3 observational effect estimation; L4 experimental evidence |
| **Level eligibility** | L1: population and conditioning available. L2: matched support and overlap above threshold. L3: intervention records present, positivity and overlap defensible, declared confounder set. L4: a declared trial or controlled campaign exists in the data |
| **Validation** | Negative control must land inside its declared band; placebo-in-time test; sensitivity to unmeasured confounding reported; overlap and support reported |
| **Explainability** | The estimate itself plus conditioning, support, and the confounder set are the explanation |
| **Refresh** | Recompute weekly |
| **Compute class** | CPU, minutes to hours |
| **Refusal states** | All six. **CONTRADICTED_BY_CONTROL is specific to this family** and fires when a negative control moves |
| **Emitted datasets** | Envelope dataset, finding dataset |

**The recommendation rule, stated as a hard constraint.** A remediation suggestion may only be emitted when: the driver parameter is `controllable`; the estimate is at L2 or above; the support overlap exceeds the declared floor; and the suggested value lies inside the observed support range. Extrapolating a recommendation beyond observed practice is prohibited. Failing any clause produces a finding with `terminal_state = INSUFFICIENT_DATA` and no recommendation, never a weaker recommendation.

---

## 7. INITIAL COMMISSIONING SEQUENCE (Deliverable E, part 1)

**Budget: hours to days. Every stage checkpointed. Restart resumes at the last successful stage.**

### 7.1 Stage table

| # | Stage | Depends on | Checkpoint artifact | Restart token | Compute | Failure policy |
|---|---|---|---|---|---|---|
| C1 | Validate semantic model, run IV-01..IV-08 | published SMV | `validation_report` | SMV id | CPU, minutes | IV-06 fails: **abort run** |
| C2 | Materialise journey spine | C1 | DP-1 partitions | last partition key | CPU, hours | Retry partition |
| C3 | Build outcome store | C2 | DP-4 | outcome_code | CPU, minutes | Skip outcome, record |
| C4 | Build feature catalogue | C1, C2 | DP-2a plus `feature_schema_hash` | version | CPU, minutes | Abort |
| C5 | Build feature matrix | C4 | DP-2b partitions | partition key | CPU, hours | Retry partition |
| C6 | Build sequence store | C2 | DP-3 partitions plus `channel_set_version` | partition key | CPU or IO, hours | **Skip family, continue** |
| C7 | Compute capability profile | C2..C6 | `capability_profile` | none | CPU, minutes | Abort |
| C8 | Enumerate candidates, apply governor | C7 | `candidate_manifest` | none | CPU, seconds | Abort |
| C9 | Train encoder (MF-01) | C6, C7 | encoder artifact plus epoch checkpoints | epoch | **GPU, hours to days** | Skip family, continue |
| C10 | Encode historical population | C9 | DP-5a partitions | partition key | GPU or CPU, hours | Retry partition |
| C11 | Build vector index generation 0 | C10 or C5 | DP-5b sealed manifest | none | CPU, minutes to hours | Skip family, continue |
| C12 | Fit novelty model (MF-03) | C10 or C5 | model artifact | none | CPU, minutes | Skip family, continue |
| C13 | Train supervised models (MF-04) | C5, C8 | one artifact per model | model key | CPU, hours | Skip model, record |
| C14 | Calibrate | C13 | calibration artifacts | model key | CPU, minutes | Skip model, record |
| C15 | Compute SHAP contributors | C13, C14 | contributor rows | model key | CPU, minutes to hours | Skip model, record |
| C16 | Compute envelopes and effects (MF-05) | C5, C3, C7 | evidence rows | finding key | CPU, hours | Skip finding, record |
| C17 | Materialise evidence store and datasets | C11..C16 | DP-7, DP-6 | dataset key | CPU, minutes | Retry |
| C18 | Run validation gates G-01..G-20 | C17 | `gate_report` | none | CPU, minutes | Blocking gates abort promotion |
| C19 | Assemble and publish ModelBundle v1 | C18 | bundle manifest, live alias set | none | seconds | Atomic; no partial publish |

The rule's fifteen conceptual stages become nineteen because feature catalogue, candidate enumeration and gate execution are separately restartable in practice.

### 7.2 The skip-and-continue principle

Stages C6, C9, C10, C11 and C12 are marked **skip family, continue**. This is the mechanism behind the poorest-customer requirement. A customer with no time series loses the encoder, the embedding store and the embedding-based index. Commissioning still completes and publishes a bundle containing the supervised family, the effect layer, the feature-space similarity index and the novelty model on engineered features.

**A skipped family writes a `MODEL_NOT_READY` evidence row naming the failed eligibility clause.** It does not vanish, and the readiness dataset reports it.

### 7.3 Checkpoint contract

Every stage writes `{run_id, stage_code, status, restart_token, started_at, completed_at, rows_written, artifact_refs[]}`. Restart reads the last row per stage. **No stage may re-read raw sources; every stage reads only the artifacts of prior stages.** This is what makes a failure at hour 19 cost one stage rather than the run.

---

## 8. WEEKLY UPDATE SEQUENCE (Deliverable E, part 2)

**Hard budget: 24 hours. The design must fit with margin, not exactly.**

### 8.1 Stage and budget table

| # | Stage | Mode | Nominal | Ceiling | Droppable |
|---|---|---|---|---|---|
| W1 | Incremental ingest and canonical projection | delta | 1.0 h | 4 h | No |
| W2 | Extend spine | delta | 0.5 h | 2 h | No |
| W3 | Extend outcomes | delta | 0.2 h | 1 h | No |
| W4 | Extend feature matrix | delta | 1.0 h | 4 h | No |
| W5 | Extend sequence store | delta | 0.5 h | 2 h | Yes, tier 4 |
| W6 | Encode new subjects, **frozen encoder** | delta | 0.5 h | 2 h | Yes, tier 4 |
| W7 | Extend index, new sealed generation | delta | 0.2 h | 1 h | Yes, tier 4 |
| W8 | Recompute capability profile and drift | full | 0.3 h | 1 h | No |
| W9 | Refit novelty model | full on window | 0.2 h | 0.5 h | Yes, tier 3 |
| W10 | Retrain supervised models | **full on rolling window** | 3.0 h | 8 h | Yes, tier 2 |
| W11 | Recalibrate | full on recent holdout | 0.2 h | 0.5 h | **No** |
| W12 | Recompute contributors | full | 0.5 h | 2 h | Yes, tier 3 |
| W13 | Recompute envelopes and effects | full | 1.0 h | 3 h | Yes, tier 3 |
| W14 | Champion / challenger validation | full | 0.5 h | 1 h | No |
| W15 | Materialise evidence and datasets | full | 0.5 h | 2 h | No |
| W16 | Gate run and atomic publish | full | 0.3 h | 1 h | No |
| | **Total** | | **10.4 h** | 35 h | |

Nominal fits with better than two times margin. The ceiling column exceeds 24 hours, which is precisely why the abort ladder exists.

### 8.2 The abort ladder

At T+18h the orchestrator evaluates remaining stages against remaining budget and degrades in this order. Every drop writes a supervisor decision row with reason `weekly_budget`.

| Tier | Dropped | Consequence |
|---|---|---|
| 1 | Nothing | Full weekly refresh |
| 2 | W10 supervised retrain | **Champion is retained. W11 recalibration still runs.** Models stay current in confidence even when not retrained |
| 3 | W9, W12, W13 | Novelty, contributors and effects carry last week's values, marked stale in the evidence envelope |
| 4 | W5, W6, W7 | New subjects are not embedded this week. They are queued and encoded next week. **Similarity for those subjects returns MODEL_NOT_READY, not a wrong neighbour** |
| Floor | W1..W4, W8, W11, W14, W15, W16 always run | Data, drift, calibration, validation and publish are never dropped |

**Why W11 recalibration is undroppable while W10 retraining is droppable.** Recalibration costs minutes and corrects the model's confidence to current conditions. Retraining costs hours and changes its structure. If only one can run, calibration delivers more truth per minute by a wide margin.

### 8.3 What is delta and what is full

| Delta | Full |
|---|---|
| Ingest, spine, outcomes, features, sequences, embeddings, index generation | Novelty fit, supervised training, calibration, contributors, effects, profile, gates |

**No model weight is ever incrementally mutated.** Supervised models are retrained from scratch on the governed rolling window. This is cheaper than it sounds and infinitely more reproducible than online updates.

### 8.4 Encoder policy in the weekly window

The encoder is never retrained in W-stages. The Drift Supervisor may *request* an encoder refresh; the request enters the governed refresh queue and executes in a scheduled window with commissioning-class budget, not in the weekly window. On completion it triggers: re-encode reference population, rebuild index generation 0, revalidate, publish a full bundle atomically.

---

## 9. DAYTIME SERVING SEQUENCE (Deliverable E, part 3)

**NO TRAINING. Enforced by AD-08 and the Serving Wall.**

### 9.1 Request path

```
question or widget load
   -> Intelligence Orchestrator
      -> resolve to a tool or dataset request
      -> COST ESTIMATOR
         -> tier decision
            T1: read serving stores            target < 1 s
            T2: bounded compute on prepared    target < 30 s
            T3: insert analysis_job_request    returns immediately
   -> response with evidence envelope
```

### 9.2 Cost estimator inputs

Estimated before any work begins: target dataset partition count, estimated rows after filters, whether an aggregation crosses partitions, whether a join crosses data products, index generation count, and the current hardware tier.

**The estimator is conservative by construction.** When it cannot bound the cost, the answer is tier 3. It never optimistically starts work it may not finish.

### 9.3 Tier routing

| Tier | Reads | Budget | Timeout | Example |
|---|---|---|---|---|
| **T1** | DP-6, DP-7, DP-5b, live bundle metadata, Layer A summaries | target under 1 s | 5 s hard | Why is risk high; what resembles this; the approved envelope; contributors |
| **T2** | DP-2 recent slice, DP-1, DP-4 through the governed aggregate path | target under 30 s | **60 s hard statement timeout** | Compare two cohorts the user just defined; a bounded correlation on a filtered slice |
| **T3** | Nothing synchronously | returns in under 1 s | n/a | Anything the estimator cannot bound |

**The absolute synchronous ceiling is under 2 minutes**, and the T2 hard timeout of 60 seconds sits well inside it so that orchestration, serialisation and rendering cannot push a request past the ceiling.

### 9.4 The T2 exploratory classification

This closes CT-04. A tier 2 computation is a user-defined analysis executed at query time. It has not passed G-06 leakage checks, has no negative control, and its population was defined by a filter rather than by a governed manifest.

**Therefore every T2 result is emitted with `claim_class = ASSOCIATION` and `evidence_kind = exploratory`, is never written to the evidence store, and can never be cited by the Assistant as a finding.** It is shown to the user with the qualification that it is an exploratory calculation, not a governed finding. A T2 result may be *proposed* for promotion, which creates a manifest row for the governed pipeline to evaluate on the next weekly run. It is never promoted at query time.

### 9.5 Serving state versus training state, stated as a table

| | Training state | Serving state |
|---|---|---|
| Reads | All data products, full history | Serving stores, bounded slices |
| Writes | Candidate artifacts, evidence, predictions | `analysis_job_request` only, plus scored predictions from the inference service |
| Compute | GPU permitted, unbounded scans, hours | CPU, bounded scans, seconds |
| DB role | `ppiq_layerb_train` | `ppiq_layerb_serve`, SELECT plus narrow INSERT |
| Process | Scheduler-invoked jobs | Request-response service |
| May call the other | Promotion writes to serving stores | **Never** |

---

## 10. GOVERNED OUTPUT DATASETS (Deliverable G)

Seven datasets. Each is an ordinary governed analytical dataset registered in the same catalogue Layer A uses, projected into the same fact contract, bindable in Page Builder with no ML-specific code.

**Every dataset carries the EvidenceEnvelope columns from section 5, DP-7.** They are not repeated per dataset below.

**Aggregation semantics, closing CT-03.** Every measure declares an `aggregation_policy`. This is what prevents a Layer B estimate from being summed into something that looks like a Layer A fact.

| Policy | Meaning |
|---|---|
| `additive` | May be summed across any dimension |
| `semi_additive` | May be averaged, never summed |
| `non_additive` | May only be displayed at its native grain; aggregation is refused with a named message |
| `count_only` | The rows may be counted; the value may not be aggregated |

### 10.1 Prediction dataset

**Grain:** grain instance, prediction point, outcome.

| Field | Role | Aggregation |
|---|---|---|
| `grain_instance_key` | dimension identity | |
| `prediction_point_code`, `outcome_code`, `risk_band`, `variant_key`, `route_path_id` | dimensions | |
| `predicted_probability` | measure | **semi_additive** (mean allowed, sum refused) |
| `predicted_value` | measure | semi_additive |
| `subject_count` | measure | additive |
| `calibration_error` | measure | non_additive |
| `scored_at_utc` | time | |
| `model_version`, `bundle_version` | dimensions | |

**A user may chart mean predicted probability by variant. A user may not chart the sum of predicted probabilities, because that number would look like a count of expected defects while carrying none of a count's guarantees.** The refusal is generated by the aggregation policy, not by a special case.

### 10.2 Contributor dataset

**Grain:** prediction or finding, feature.

Dimensions: `parent_evidence_id`, `feature_code`, `parameter_code`, `direction`, `is_controllable`, `physical_quantity`, `unit_code`, `rank`.
Measures: `contribution_value` (semi_additive), `feature_value` (non_additive), `abs_contribution` (semi_additive).

`claim_class` is fixed to `PREDICTIVE_CONTRIBUTION` for SHAP-derived rows. A contributor row is not an effect and the column says so on every row.

### 10.3 Similarity dataset

**Grain:** subject, neighbour.

Dimensions: `subject_key`, `neighbour_key`, `neighbour_variant_key`, `neighbour_outcome_class`, `encoder_version`, `index_version`, `rank`.
Measures: `distance` (non_additive), `similarity` (non_additive), `neighbour_outcome_value` (semi_additive), `neighbour_count` (additive).

### 10.4 Anomaly dataset

**Grain:** spine node.

Dimensions: `grain_instance_key`, `process_position_code`, `regime_code`, `variant_key`, `model_version`.
Measures: `novelty_score` (semi_additive), `novelty_percentile` (non_additive), `subject_count` (additive).

No column asserts that an anomaly is a defect. Where an outcome model exists, `associated_outcome_rate` may be joined; it is a separate measure with its own envelope.

### 10.5 Operating envelope dataset

**Grain:** parameter, context combination, position.

Dimensions: `parameter_code`, `process_position_code`, `variant_key`, `context_keys`, `is_controllable`, `evidence_level` (L1..L4), `unit_code`.
Measures: `lower_bound`, `upper_bound`, `centre` (all non_additive), `observed_outcome_rate` (semi_additive), `population_n` (additive), `confidence` (non_additive).

### 10.6 Finding and effect dataset

**Grain:** finding.

Dimensions: `finding_id`, `driver_code`, `outcome_code`, `method_code`, `claim_class`, `evidence_level`, `terminal_state`, `status`, `is_controllable`, `conditioning`.
Measures: `effect_value`, `effect_lower`, `effect_upper` (non_additive), `population_n` (additive), `support_overlap` (non_additive).

**This dataset carries refusals as rows.** A dashboard filtered to `terminal_state = FINDING` shows findings; unfiltered it shows the honest picture including what the engine could not do and why.

### 10.7 Model and readiness status dataset

**Grain:** model or family, per site.

Dimensions: `model_family`, `model_code`, `outcome_code`, `prediction_point_code`, `readiness_state`, `failed_clause`, `bundle_version`, `champion_status`.
Measures: `measured_value` and `required_threshold` for the failed clause (both non_additive), `training_population_n` (additive), `days_of_history` (non_additive).

**This dataset is what a dashboard binds to when nothing is ready.** It is the reason a fresh installation shows a stated readiness picture rather than empty widgets, and it is the direct product realisation of Rule 2 starting empty without looking broken.

**Note.** The frozen rule section 21 lists six output dataset families; the work order section G lists seven by adding model and readiness status. This pack adopts seven. See CT-07.

---

## 11. ASSISTANT TOOL CONTRACTS (Deliverable H)

### 11.1 Common response envelope

Every Layer B tool returns:

```
{
  "result": <tool-specific payload or null>,
  "terminal_state": "FINDING | INSUFFICIENT_DATA | NOT_APPLICABLE |
                     REFUSED_BY_GUARD | CONTRADICTED_BY_CONTROL | MODEL_NOT_READY",
  "claim_class": "ASSOCIATION | PREDICTIVE_CONTRIBUTION |
                  MATCHED_EFFECT_ESTIMATE | CAUSAL_EVIDENCE | null",
  "refusal_reason": "<sentence, required when terminal_state != FINDING>",
  "evidence": {
    "evidence_ids": [...], "method_code": "...", "population_n": 0,
    "conditioning": {...}, "uncertainty": {...},
    "window_origin_mix": {...}
  },
  "provenance": {
    "bundle_version": "...", "model_version": "...",
    "semantic_model_version_id": "...",
    "training_window": {"from": "...", "to": "..."},
    "computed_at_utc": "...", "staleness_days": 0
  },
  "layer": "B"
}
```

**`layer` is present on every response from both engines.** It is how the Assistant keeps an exact fact and a learned estimate distinguishable in its own context, which is the only way the final answer can label them correctly.

### 11.2 Tool catalogue

| Tool | Input | Returns | Tier |
|---|---|---|---|
| `GetModelReadiness` | scope | Readiness rows per family and model, failed clauses with measured values | T1 |
| `GetModelVersion` | none | Live bundle manifest, component versions, training window, promotion time | T1 |
| `GetPrediction` | grain instance, optional prediction point and outcome | Prediction rows | T1 |
| `GetPredictionContributors` | prediction id, top n | Contributor rows, claim class fixed | T1 |
| `FindSimilarJourneys` | subject id, k, optional filters | Neighbour rows with their outcomes and practices | T1 |
| `GetAnomalyEvidence` | subject id or window | Novelty score, percentile, contributing channels, nearest regime | T1 |
| `GetOperatingEnvelope` | parameter, context | Envelope rows with evidence level and population | T1 |
| `GetFinding` | filter by driver, outcome, status | Finding rows including refusals | T1 |
| `ComparePractices` | two cohort definitions, outcome | Matched comparison with support, overlap, conditioning, confounder limits | **T2** |
| `ProposeRemediation` | subject id or condition | Suggestion **only if** MF-05 recommendation rule passes; otherwise refusal | T1 |
| `RequestAnalysisJob` | analysis spec | Job id and state | T3 |

### 11.3 Assistant discipline rules

1. The Assistant never queries a model artifact, a training store, or a candidate bundle. Only these eleven tools plus the Layer A tools.
2. The Assistant may not upgrade a claim class. A `PREDICTIVE_CONTRIBUTION` may not be phrased as a cause. A tool-response claim class maps to a fixed set of permitted phrasings.
3. When `terminal_state` is not `FINDING`, the Assistant states the refusal. It does not substitute a general-knowledge answer, and it does not soften the refusal into a hedge.
4. A numeric answer to a quantity question must carry the unit from `physical_quantity` plus `unit_code`. A response whose unit does not match the quantity class of the question is a **hard failure**, not an inaccuracy, and gate G-20 tests exactly this.
5. Every answer containing a learned claim carries at least one `evidence_id`.
6. When both layers contribute, the answer separates them explicitly: the exact fact, then the learned finding, then the evidence, then the qualification.

---

## 12. PAGE BUILDER INTEGRATION (Deliverable I)

### 12.1 Registration contract

At bundle promotion, the Evidence Materialiser registers each of the seven datasets in the same dataset catalogue Layer A uses. Registration supplies: dataset code, grain, dimension list with semantic types and labels, measure list with units and **aggregation policy**, time field, default filters, and compatibility hints.

**From the Page Builder's perspective there is no difference between an intelligence dataset and any other governed dataset.** The metadata endpoint returns the same shape.

### 12.2 The customer journey, unchanged from ordinary data

```
Add Widget -> select dataset (an intelligence dataset appears in the same list)
  -> choose dimension (from metadata, not a compiled list)
  -> choose measure (aggregation policy enforced by the engine)
  -> chart types narrow automatically by compatibility
  -> filter, save, cross-filter
```

### 12.3 Projection into the existing aggregate contract

Intelligence datasets project into the same fact contract the generic aggregate engine consumes. The aggregate engine gains **no** knowledge that a measure is learned. It gains exactly one new behaviour: it honours `aggregation_policy` and refuses a disallowed aggregation with a named message. That refusal mechanism is generic and applies equally to any Layer A measure that declares itself non-additive.

### 12.4 Prohibitions, testable

- No `if predictionDashboard`, no `if oilCustomer`, no branch on dataset origin
- No compiled list of intelligence fields anywhere in the Page Builder
- No React component required merely because data came from Layer B
- A special visualisation is permitted only when it expresses a genuinely different visual grammar. **The candidate list is short and is OD-09**: a neighbour-comparison view and a contribution waterfall are the two plausible cases. Both must be justified as grammar, not as origin

### 12.5 The one honest consequence

Because intelligence datasets are ordinary datasets, a customer can build a widget that is statistically misleading, for example an envelope chart over a population of eleven. The mitigation is not to restrict the builder. It is that `population_n` and `terminal_state` are ordinary fields the widget can display, and the default chart templates for intelligence datasets include them. **OD-10** decides whether a minimum-population warning renders automatically on intelligence widgets.

---

## 13. GENERICITY PROOF (Deliverable J)

Two conceptual installations. Industry vocabulary appears **only** in the configuration column of the mapping tables below. It appears nowhere in any contract, schema, sequence, tool or dataset defined in sections 4 through 12.

### 13.1 Installation ONE - oil, refining and blending

| Contract | Configured value |
|---|---|
| SM-02 primary grain | `grain_kind = flow_interval`, one hour intervals per train |
| SM-02 secondary grain | `grain_kind = batch` for blended product tanks |
| SM-03 positions | Six: desalting, atmospheric distillation, hydrotreating, reforming, blending, tank certification. Route is a **graph**, not a chain |
| SM-03 residence | `lag_with_dispersion` on all flow positions. Lag 45 to 900 s, dispersion 20 to 300 s |
| SM-04 edges | `transformation`, many_to_many, `weight_semantics = proportion` for blending; `temporal` edges within trains |
| SM-05 parameters | Feed rate, reactor temperature, pressure, hydrogen partial pressure, reflux ratio, catalyst bed temperature. Mixed controllable and observed |
| SM-06 outcomes | `sulphur_content` continuous, detection at tank certification; `octane_index` continuous; `off_spec_flag` binary |
| SM-07 events | Trips, catalyst regeneration, valve interventions. `is_intervention = true` on operator setpoint changes with recorded dose |
| SM-08 variant dimension | Product quality class. Observed levels: 4 |
| SM-09 resources | Catalyst beds with age counters |
| SM-12 prediction points | After hydrotreating; after reforming |
| Genealogy strength | **transformational** |

### 13.2 Installation TWO - mineral water, bottling

| Contract | Configured value |
|---|---|
| SM-02 primary grain | `grain_kind = batch`, one production order per format |
| SM-02 secondary grain | `grain_kind = process_window`, fifteen minute windows for filler monitoring |
| SM-03 positions | Five: source treatment, ozonation, blow moulding, filling and capping, palletising. Route is a **chain** |
| SM-03 residence | `fixed_lag` on treatment; `none` on filling, where identity is tracked |
| SM-04 edges | `containment` from batch to pallet; `identity` through the filler; `transformation` at blow moulding with proportion weights from preform lots |
| SM-05 parameters | Ozone dose, preform temperature, blow pressure, fill volume, cap torque, line speed, conductivity, TDS |
| SM-06 outcomes | `microbio_result` binary, detection at lab release **days after production**; `fill_volume_deviation` continuous, detection at filling; `cap_leak_rate` continuous |
| SM-07 events | Line stops, CIP cycles, mould changes. `is_intervention = true` on CIP triggered outside schedule |
| SM-08 variant dimension | Bottle format. Observed levels: 3 |
| SM-08 shift dimension | **Observed levels: 1. Status COLLAPSED.** Removed from all conditioning with reason `collapsed_single_level` |
| SM-09 resources | Moulds and filling valves, instance identity available |
| SM-12 prediction points | After blow moulding; after filling |
| Genealogy strength | **sequential**, transformational at moulding |

### 13.3 The invariance table - what did NOT change

| Element | Oil | Water | Same? |
|---|---|---|---|
| Input contract objects SM-01..SM-12 | Used | Used | **Identical** |
| Data products DP-1..DP-7 schemas | Used | Used | **Identical** |
| Model family registry MF-01..MF-05 | Used | Used | **Identical** |
| Commissioning stages C1..C19 | Used | Used | **Identical** |
| Weekly stages W1..W16 and abort ladder | Used | Used | **Identical** |
| Daytime tiers T1..T3 | Used | Used | **Identical** |
| Output datasets 10.1..10.7 | Used | Used | **Identical** |
| Assistant tools, all eleven | Used | Used | **Identical** |
| Page Builder binding path | Used | Used | **Identical** |
| Validation gates G-01..G-20 | Used | Used | **Identical** |

**No `OilModel`. No `BottleModel`. No branch on industry anywhere.**

### 13.4 The three points where the installations genuinely differ, and how each is handled by configuration

**1. Route topology.** Oil is a graph, water is a chain. Handled by SM-03 `predecessor_position_codes`. The spine builder walks a graph in both cases; a chain is a graph with one path.

**2. Detection lag on the outcome.** Water's microbiological result arrives days after production; oil's sulphur result arrives at certification. Handled by SM-06 `detection_timestamp_field`, consumed by G-06. **The same leakage gate produces a different legal feature set for each installation with no code difference.** This is the sharpest single demonstration in the pack: leakage prevention is generic because the detection anchor is declared.

**3. A collapsed dimension.** Water runs one shift. Handled by the SM-08 collapse rule. Oil runs four; its shift dimension is active. Same code, different profile.

### 13.5 What the proof does not claim

It does not claim that the same *findings* appear, that the same *models* become eligible, or that both installations reach the same ladder level. Oil's transformational genealogy with proportion weights will produce weaker attribution than water's tracked identity through the filler. **That difference is measured and reported by the capability profile, not hidden.** Genericity means one architecture, not equal outcomes.

---

## 14. SCALE AND HARDWARE SIZING STRATEGY (Deliverable K)

### 14.1 Raw data volume is not training feature volume

These are different quantities and conflating them is the most common sizing error.

```
RAW VOLUME       = sum over sources of rows x bytes
                   Dominated by high-frequency sensor history and text logs.
                   Frequently 90 percent application logging with near-zero
                   learning value.

FEATURE VOLUME   = grain_instances
                   x process_positions
                   x prediction_points
                   x features_per_row
                   x 8 bytes

SEQUENCE VOLUME  = grain_instances x positions x channels
                   x mean_sequence_length x 5 bytes
                   (float32 value plus mask, with compression)
```

**Worked illustration.** 25 million grain instances, 5 positions, 2 prediction points, 400 features gives a feature volume near 400 GB uncompressed and materially less columnar-compressed. The same installation's raw volume may be 75 TB. **The learning workload is sized by the middle number, not the first.**

### 14.2 Workload dimensions to measure per installation

Historical grain instances; new instances per week; process positions; prediction points; features per row; sequence channels; mean sequence length; distinct outcomes; active model count; history window in months; variant levels; and the four resource axes CPU cores, RAM, GPU memory, storage throughput.

### 14.3 Execution tiers

| Tier | Shape | What changes architecturally |
|---|---|---|
| **Small** | One CPU server, optional single GPU | Everything in-process. Feature build single-threaded per partition. Index in memory |
| **Medium** | Larger CPU and RAM, one dedicated GPU | Parallel partition workers. Encoder training on GPU. Index memory-mapped |
| **Large** | Partitioned processing, multiple workers, one or more GPUs | Feature build sharded by partition key across workers. Boosting with distributed histogram or per-shard models merged. Index sharded by generation |
| **Very large** | Distributed feature and training execution | Distributed PyTorch (DDP or FSDP) for the encoder only where measured to be necessary. Distributed boosting. Index sharded across nodes with a routing layer |

**No thresholds are stated here, deliberately.** The frozen rule forbids hardcoding them without measurement, and the honest position is that we have not measured. **OD-11** is the benchmark plan that produces them.

### 14.4 Sequence store retention is the dominant storage decision

The sequence store is typically the largest product and the one with the least reuse after the encoder is trained and the population encoded. Three policies, and this is **OD-12**:

| Policy | Storage | Cost |
|---|---|---|
| Full retention | Highest | Encoder retraining always possible on full history |
| Rolling window plus reservoir sample of older data | Moderate | Retraining on a sample; some loss of rare regimes |
| Encode and discard beyond window | Lowest | **Encoder can never be retrained on discarded history**, which conflicts with the quarterly refresh policy |

The third option is cheap and quietly destructive. It must not be selected by default.

### 14.5 What must never be scaled by raising a cap

Raising a row cap to make an aggregate complete is not remediation. Where a computation exceeds its budget the answer is partitioned execution, a governed pre-aggregate, or an explicit bounded refusal. **A plausible partial value is prohibited at every tier.**

---

## 15. VALIDATION AND QUALITY GATES (Deliverable L)

| ID | Gate | When | Blocking | Evidence produced |
|---|---|---|---|---|
| **G-01** | Semantic model published and hash-stable | C1, W16 | Yes | Version and hash |
| **G-02** | Relationship quality IV-01..IV-08 within declared bounds | C1, W8 | **IV-06 yes**, others record | Metric table |
| **G-03** | Spine completeness: every instance has at least one node, no orphan nodes | C2, W2 | Yes | Counts |
| **G-04** | Feature schema hash matches the catalogue that produced it | C5, W4 | Yes | Hash pair |
| **G-05** | Target leakage: no feature derived from the outcome definition | C8, W10 | Yes | Feature-to-outcome dependency trace |
| **G-06** | **Temporal leakage**: for every model, every feature's availability position and offset precede the prediction cutoff, and the outcome detection time follows it | C13, W10 | **Yes** | Per-feature legality table |
| **G-07** | Class balance above floor, or the model is not created | C8, W10 | Yes | Measured fractions |
| **G-08** | Outcome sufficiency: population, observed-flag integrity, no ambiguous negatives | C3, W3 | Yes | Counts, ambiguity flag |
| **G-09** | Feature missingness below ceiling per feature and per row | C5, W4 | Records, blocks per-feature | Missingness table |
| **G-10** | Regime stability across the training window | C7, W8 | Records | Stability statistic |
| **G-11** | Input and representation drift within bounds | W8 | Triggers supervisor action | Drift metrics |
| **G-12** | Calibration error below ceiling on recent holdout | C14, W11 | Yes | Reliability curve summary |
| **G-13** | **Encoder compatibility**: bundle `channel_set_version` equals current | C9, W6, W16 | **Yes** | Version pair |
| **G-14** | **Embedding-version compatibility**: index generation chain resolves to exactly one encoder version | C11, W7, W16 | **Yes** | Generation chain |
| **G-15** | Model performance above the mandatory baseline model | C13, W10 | Yes | Candidate versus baseline metrics |
| **G-16** | Subgroup and variant stability: no variant level below the declared floor | C13, W10 | Records, blocks promotion on severe | Per-variant metrics |
| **G-17** | Champion versus challenger on the same governed holdout | W14 | **Promotion gate** | Comparison table plus decision reason |
| **G-18** | Reproducibility: seeds, dataset manifest, code identity, environment, artifact hashes all present | C19, W16 | Yes | Manifest |
| **G-19** | **Refusal integrity**: every non-FINDING row names a method-side cause where the cause is method-side, and carries the measured statistic where the cause is data-side | C17, W15 | Yes | Refusal audit |
| **G-20** | **Unit sanity**: every Assistant numeric response unit matches the physical quantity class of the question, tested against a fixed probe set | C19, W16 | **Yes** | Probe results |
| **G-21** | **Tenant isolation**: no training population, index generation, embedding or evidence row crosses tenant boundary | C19, W16 | **Yes** | Boundary scan |
| **G-22** | Rollback drill: the previous bundle can be restored and reproduces its recorded metrics | C19, monthly | Yes | Drill record |

**Every gate is falsified once before it is trusted.** A gate that has never failed on a known-bad input is not evidence that the property holds; it is evidence that the gate was never exercised. The falsification record is part of the gate's own artifact.

**No model reaches production because training completed.** Promotion requires G-17 plus every gate marked blocking.

---

## 16. CROSS-CONTRACT RECONCILIATION

### 16.1 Layer A <-> Layer B

| Question | Resolution |
|---|---|
| Who owns exact counts? | **Layer A, always.** Layer B never estimates a fact that Layer A can compute exactly |
| Can Layer B read Layer A outputs? | Yes, as features, provided they pass G-06 |
| Can Layer A read Layer B outputs? | Yes. Intelligence datasets are ordinary governed datasets and the generic aggregate engine reads them, subject to `aggregation_policy` |
| Can a Layer B measure be summed into a fact-shaped number? | **No.** `aggregation_policy` on the measure refuses it by name |
| Where does the boundary appear to the user? | In the `layer` field on every tool response and in the Assistant's answer structure |

### 16.2 Layer B <-> Page Builder

Layer B emits registered datasets with metadata. Page Builder holds no compiled ML field list and no origin branch. The aggregate engine gains one generic behaviour, aggregation-policy enforcement, which is not ML-specific.

### 16.3 Layer B <-> Assistant

Eleven tools, one response envelope, claim class on every response, refusal as a first-class result, evidence ids mandatory on learned claims, and a fixed mapping from claim class to permitted phrasing.

### 16.4 Semantic model <-> ML

Every model, feature row, prediction and finding pins `semantic_model_version_id`, `feature_schema_hash` and `definition_version`. A republished semantic model does not silently invalidate models; it creates a new lineage, and the supervisor decides whether to retrain, quarantine, or continue. **Training against a mutable ad-hoc definition is structurally impossible because the trainer reads only published versions.**

### 16.5 Weekly scheduler <-> model registry

The scheduler produces candidates and never writes the live alias. Only the Bundle Promoter writes it, only after G-17 and all blocking gates pass, and only atomically. A failed weekly run leaves last week's bundle live and untouched.

---

## 17. THE MODELBUNDLE, REGISTRY AND ROLLBACK (Deliverable F)

### 17.1 Bundle manifest

```
ModelBundle {
  bundle_version, tenant_id, site_id,
  semantic_model_version_id, feature_set_version, feature_schema_hash,
  channel_set_version,
  encoder_version | null,
  index_version | null,
  novelty_model_version | null,
  supervised_models: [ {model_code, model_version, outcome_code,
                        prediction_point_code, calibration_version} ],
  effect_evidence_snapshot_version,
  evidence_store_snapshot_version,
  capability_profile_version,
  gate_report_ref, champion_decision_ref,
  code_commit, environment_manifest_hash,
  created_at, promoted_at, promoted_by, promotion_reason
}
```

**A bundle is the only publishable unit.** No component version is ever promoted alone.

### 17.2 Lifecycle states

`candidate` -> `validated` -> `champion` (live) -> `superseded`
Alternate terminals: `rejected` (failed G-17 or a blocking gate), `quarantined` (supervisor action), `rolled_back` (a promoted bundle demoted after a defect).

### 17.3 Atomic alias

One pointer per tenant and site names the live bundle. Promotion is a single atomic pointer switch after all artifacts are staged and readable. Rollback is the same switch to the predecessor. **Serving resolves the pointer once per request**, so a promotion mid-request cannot mix versions within one answer.

### 17.4 Registry records

Every field required by section 17 of the frozen rule, plus `channel_set_version`, `index_generation_chain`, `capability_profile_version` and `falsification_record_ref` for each gate. **MLflow may implement it; the product contract is `ModelBundleRegistry` and is not MLflow-shaped.**

---

## 18. TRADEOFFS, OPEN DECISIONS, AND WHAT THIS DESIGN DOES NOT DECIDE

### 18.1 Deliberate tradeoffs, with what each costs

| # | Tradeoff | Chosen | Cost accepted |
|---|---|---|---|
| 1 | Boosting over deep networks for the decision | Boosting | Gives up some accuracy on strongly sequential effects; buys attribution, speed and defensibility |
| 2 | Full weekly retrain over incremental updates | Full retrain | Costs hours weekly; buys reproducibility and comparability |
| 3 | Wide physical feature store | Wide | Schema churn on feature changes; buys columnar read speed and a hashable schema |
| 4 | Generational index over live mutation | Generational | Search fans out over generations; buys immutable bundle pinning |
| 5 | Bundle-level atomicity | Bundle | Cannot ship a single fixed model quickly; buys the impossibility of version skew |
| 6 | Refusal as data | Materialised rows | Storage and query surface; buys honest dashboards and an auditable engine |
| 7 | Conservative cost estimator | Conservative | Some answerable questions go to tier 3; buys a latency contract that holds |
| 8 | Encoder optional | Optional | Two code paths in MF-02 and MF-03; buys the poorest customer a working product |

### 18.2 Open architectural decisions requiring a ruling

| ID | Decision | Blocks | Recommendation |
|---|---|---|---|
| **OD-01** | Feature store physical substrate: Parquet plus columnar engine, or columnar Postgres | Feature store work package | Benchmark both on a representative slice before choosing |
| **OD-02** | **Which schema holds Layer B outputs.** The Schema Topology contract states engine outputs live in Plant Data and that no analytical surface may display a row from outside it. Evidence, prediction and dataset rows therefore belong in Plant Data. But model artifacts, registry records, index files, gate reports and job state are neither plant data nor ship-identical metadata | Storage layout, grants, isolation tests | A fourth application schema conflicts with "exactly three". Recommend intelligence datasets in Plant Data, and model or registry artifacts in an operational store outside the three application schemas, with the analytical role holding no grant on it. **This needs your ruling, it is not mine to make** |
| **OD-03** | Whether Layer B may write into Plant Data directly or only through a governed publisher | Promotion design | Publisher only |
| **OD-04** | Index rebuild trigger: generation count, delta fraction, or measured recall floor | Index work package | Measured recall floor, with a generation ceiling as a backstop |
| **OD-05** | Encoder minimum-population threshold | Encoder eligibility | Set by benchmark, not by the placeholder in MF-01 |
| **OD-06** | Supervised eligibility thresholds | Model governor | Same; the values in section 6 are placeholders |
| **OD-07** | Model budget as count, compute time, or both | Governor | Both, with compute time as the binding constraint |
| **OD-08** | Whether tier 2 exploratory results may be saved by a user as a personal artifact | Serving and UI | Saveable but never citable as a finding |
| **OD-09** | Which, if any, special visualisations qualify as different grammar | Page Builder | Neighbour comparison and contribution waterfall only, each justified separately |
| **OD-10** | Automatic minimum-population warning on intelligence widgets | Page Builder | Yes, as a default template element, not a hard block |
| **OD-11** | The benchmark plan that produces the tier thresholds | Sizing | Required before any sizing number is quoted to a customer |
| **OD-12** | Sequence store retention policy | Storage cost, encoder refresh capability | Rolling window plus reservoir sample; never encode-and-discard |

### 18.3 Contradictions found in the frozen Layer B rule

Reported as required by the work order. None is fatal; each needs a ruling or is closed by this pack.

| ID | Contradiction | Status |
|---|---|---|
| **CT-01** | Section 9 forbids training hundreds of models but sets no mechanism, while sections 12 and 13 imply a model per outcome per prediction point | **Closed here** by the model-count governor, section 6.7. Thresholds are OD-07 |
| **CT-02** | Section 15 permits weekly incremental index insertion; sections 17 and F require bundles to pin an index version and forbid independent publication of components. A mutating index cannot be a pinned immutable artifact | **Closed here** by AD-06 generational index |
| **CT-03** | Section 4 forbids using ML to approximate an exact BI fact; section 21 makes Layer B outputs ordinary datasets, which lets a user sum predicted probabilities into something shaped exactly like a fact | **Closed here** by `aggregation_policy` |
| **CT-04** | Section 19 permits tier 2 bounded calculation at query time, which bypasses every gate in section 13 and L, yet its output is presented alongside governed findings | **Closed here** by the exploratory classification, section 9.4 |
| **CT-05** | Sections 11 and 13 require intervention history for effect levels 3 and 4 and for the profile, but the section 2 declaration contract defines no intervention object | **Closed here** by SM-07 `is_intervention` |
| **CT-06** | Section 15 says freeze the encoder between refreshes, but a structural change to the instrument set makes a frozen encoder invalid rather than merely stale. The rule has no concept for this | **Closed here** by `channel_set_version` and G-13 |
| **CT-07** | Section 21 of the rule lists six output dataset families; section G of the work order lists seven by adding model and readiness status | **Needs your ruling.** This pack adopts seven, because a readiness dataset is what an empty installation binds to |
| **CT-08** | Section 18 requires measured sizing and forbids promising one server, while section 19 states an absolute two-minute ceiling independent of tier. On a Small tier more questions must fall to tier 3 to hold the ceiling | **Not a defect, but must be stated to customers.** The latency contract holds at every tier; what varies by tier is how many questions are answerable synchronously |

### 18.4 What this design intentionally does NOT decide

So that design is never mistaken for implementation authorisation:

1. No physical storage product is selected. OD-01 and OD-02 are open.
2. No hyperparameters, no architecture choice between temporal convolution and Transformer. That is a measured comparison, not a design decision.
3. No threshold in this pack is final. Every numeric eligibility value is a placeholder pending OD-05, OD-06 and OD-11.
4. No hardware sizing number is quoted. No customer may be given one until OD-11 completes.
5. No API route shapes, no class names, no module layout, no language-level interfaces.
6. No migration plan from the current codebase to this architecture.
7. No estimate. Nothing here states how long any work package takes.
8. No backlog task. Decomposition is Worker 2's, once the open decisions are ruled.
9. No decision on whether Layer B ships in a licence tier, or which.
10. No commitment that any specific customer's data supports any specific output. That is what the capability profile exists to measure, per installation.

---

## 19. THE ACCEPTANCE QUESTION, ANSWERED HONESTLY

*If Worker 2 receives this pack tomorrow, can he decompose Layer B into implementation tasks without inventing architecture?*

**Yes for the following, which he can task immediately:**

- the eleven input contract objects and their validation gates (section 4)
- all seven data products with field-level schemas, mutability, partitioning and lineage (section 5)
- the five model families with eligibility expressions, validation metrics and refresh policies (section 6)
- all three orchestration sequences with stage dependencies, checkpoints, budgets and the abort ladder (sections 7, 8, 9)
- the bundle, registry, promotion and rollback design (section 17)
- the seven output datasets with grain, dimensions, measures and aggregation policy (section 10)
- the eleven Assistant tools with a common response envelope (section 11)
- the Page Builder registration and projection contract (section 12)
- twenty-two validation gates with timing, blocking behaviour and evidence (section 15)

**No, not until ruled, for:**

- anything touching physical storage layout, because OD-01 and **OD-02 in particular** determine grants, isolation tests and the schema topology. OD-02 is the one that could force rework if guessed
- final eligibility thresholds, which are placeholders pending measurement
- hardware sizing, pending the OD-11 benchmark
- CT-07, the readiness dataset, a one-line ruling

**My honest assessment.** Eleven of the twelve open decisions block exactly one work package each and can be ruled in parallel with early implementation of the parts they do not touch. **OD-02 is the exception.** It determines where every row lives, which database roles exist, and what the isolation architecture test asserts. Guessing it and being wrong means rebuilding the storage layer and the grant model. That single ruling should come before any storage work begins.

---

*Design pack produced 11 August 2026 against the frozen Layer B rule and the batch design mode order. Deliverables A through L are complete. This document is design authority. It does not authorise implementation.*
