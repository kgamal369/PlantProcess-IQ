# PPIQ LAYER B - ARCHITECTURE DESIGN PACK

**Deliverables A to L, produced in one batch**
**Status: DESIGN. NOT IMPLEMENTATION AUTHORISATION.**
**Produced 11 August 2026 against PPIQ_Layer_B_Learned_Intelligence_Engine_Rule.md and PPIQ_Layer_B_Design_Pack_Batch_Mode_Order.md**

> **AUTHORITY.** Chapter 2 governs naming, structure and positioning. Chapter 3 governs the technical contract and persistence. Chapter 4 governs engine, authoring and execution behaviour. The Layer B Rule is a subsystem constitution. This pack is the implementation blueprint and is subordinate to all four.
>
> **READING CONTRACT. Sections 1 to 47 may be read forward in order. No section is cancelled or amended by a later one.** Every contract states the canonical answer where it stands. The evolution of this document, including every architecture that was written and then withdrawn, is recorded in section 48 and nowhere else.

---

## 1. EXECUTIVE ARCHITECTURE SUMMARY

### 1.1 What Layer B is, in one paragraph

Layer B is a set of governed batch pipelines and read-only serving surfaces that consume a **versioned semantic contract** and seven **persistent data products**, produce **seven intelligence and engine families, MF-01 to MF-07**, registered and activated per serving identity in `model_registry`, and emit **seven governed analytical datasets** that the Page Builder and the Assistant consume through the same mechanisms they use for ordinary data. It contains no customer table name, no industry vocabulary, and no per-customer code path.

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
| -> validation gates -> trained version -> activation          |
| GPUs, hours of compute, temporary artifacts, large scans      |
+---------------------------------------------------------------+
                    || SERVING WALL
                    || One-way. Activation writes; serving reads.
                    || No serving request can enter the left side.
+---------------------------------------------------------------+
| SERVING PLANE                 (serving state)                 |
| active models + evidence/prediction stores + vector index     |
| -> Page Builder datasets, Assistant tools                     |
| bounded memory, bounded time, no training dependency;         |
| GPU use is optional and benchmark-driven                      |
+---------------------------------------------------------------+
```

The two walls are the load-bearing elements of the design. The Semantic Wall is what makes the product generic. The Serving Wall is what makes the daytime latency contract enforceable rather than aspirational.

### 1.3 The ten architectural decisions

| ID | Decision | Consequence |
|---|---|---|
| **AD-01** | Layer B consumes **published, immutable canonical definition versions plus the published relationship model plus the governed registry state**, never a live authoring state. A **Semantic Contract Manifest** pins that set for reproducibility | A model can always be explained by the exact contract it was trained under, resolved through one hash rather than five references |
| **AD-02** | The **analytical spine is a node table**, one row per grain instance per process position, not one row per grain instance | Multi-stage plants, graph routes and continuous intervals share one shape |
| **AD-03** | Every feature carries an **availability position and offset**; `prediction_cutoff` is enforced by the catalogue, not by the modeller | Temporal leakage becomes a mechanical gate, not a review |
| **AD-04** | **Canonical.** The feature store is `ppiq_plant.feature_store`: one row per `(material_unit_id, feature_set_version_id)` with `features jsonb`, `label_value`, `label_class`, `lineage_hash`, `is_dirty`. Refresh is incremental by watermark | Idempotency is the UNIQUE key; cost is proportional to what changed, not to what exists |
| **AD-05** | **Canonical.** `ppiq_plant.model_registry` governs per-model lifecycle. Serving identity is `(tenant_id, model_code, outcome_code, grain_code)` plus `model_version`. `status` and `serving_role` are independent axes, with partial-unique indexes giving at most one active and one approved fallback per serving identity | Activation is per serving identity |
| **AD-06** | The **vector index is generational and append-only within a generation**. Weekly inserts create a new immutable generation manifest, not a mutation | A retrieval result is reproducible against a named generation |
| **AD-07** | **Every Layer B output carries a claim class** and an evidence envelope. The claim class is a column, not documentation | The Assistant cannot blur association with effect because the data itself refuses to |
| **AD-08** | **Serving has no path to training.** Separate pooler identity, separate database role, separate pool. Tier 3 creates a job request row; it does not call the trainer | The daytime path cannot accidentally enter the training pipeline |
| **AD-09** | Layer B outputs are **ordinary bindable sources** declared in `ppiq_meta.registry_intelligence_sources`, with `sourceKind = intelligence`, an `entity_link_column` and `columnRoles`. Fact-shaped measures may project through WidgetFact; native-rich sources keep their declared columns | No ML-specific widget code. A prediction and the parameter that drove it can occupy one widget |
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
| 4 | Definition Store and Registry | Authoring | Owns the authoring lifecycle: `definition_store`, `definition_versions`, `definition_dependencies`, plus the extensible role registry | Authored definitions | `definition_versions`, registry rows |
| 5 | Canonical projection jobs | Authoring | Materialise Plant Data from Dump Store per the published transformation definition, which also emits the relationship model | Dump Store, published `definition_versions` | Plant Data, `plant_relationships` |
| 6 | **Layer A Exact BI Engine** | Serving | Deterministic aggregation | Plant Data, intelligence datasets | Nothing |
| 7 | **Data Product Builder** | Learning | Builds spine, features, sequences, outcomes | Plant Data, published `definition_versions`, `plant_relationship_paths` | Data products |
| 8 | **Capability Profiler** | Learning | Measures what this installation supports | Data products | Capability profile |
| 9a | **Learned-model trainers** (MF-01, MF-03, MF-04) | Learning | Fit candidate model versions. `ml.training` lane. Champion/challenger applies | Snapshots, profile | `model_registry` at `trained` |
| 9b | **Retrieval index builder** (MF-02) | Learning | Builds and seals index generations. Not trained; gated on measured recall@k against exact Flat | Embeddings or standardised features | Index generations |
| 9c | **Statistical engines** (MF-05, MF-06) | Learning | Compute associations, effects and envelopes. `analysis` lane. Recomputed, never trained, no champion/challenger | Data products | `correlation_results`, evidence |
| 9d | **Practice engine** (MF-07) | Learning | Canonicalises signatures and computes matched statistics. `analysis` lane. Governed signature version, not a trained model | Data products, events | `practice_signatures`, `practice_statistics` |
| 10 | **Validation Gate Runner** | Learning | Runs G-01..G-55 | Candidate model versions, data products | Gate results |
| 11 | **Model Activator** | Learning -> Serving | Sets `status = 'active'` for a serving identity, retiring the previous holder. The only writer across the Serving Wall | `model_registry`, gate results | `model_registry`, serving stores |
| 12 | **Model Registry** | Both | Versions, lineage, aliases, rollback | - | Registry records |
| 13 | **Inference Service** | Serving | Scores instances with the active model for a serving identity | `model_registry` artifact, feature store | `prediction_runs`, `predictions`, `prediction_current` |
| 14 | **Evidence Materialiser** | Learning | Turns model outputs into governed datasets | Candidates, stores | Evidence store |
| 15 | **Intelligence Orchestrator** | Serving | Routes a question to tier 1, 2 or 3 | Serving stores | Job request rows only |
| 16 | **VectorSimilarityIndex** | Serving | ANN retrieval | Index generation files | Nothing at query time |
| 17 | **Drift Supervisor** | Learning | Monitors and decides actions | Serving and learning stores | Supervisor decisions |
| 18 | **Scheduler / job runtime** | Learning | Runs the three schedules; admits by `max_concurrency` and `resource_capacity` per lane; checkpoints and pre-empts `ml.training` | Job state | Job state |
| 21 | **Snapshot Materialiser** | Learning | **The only component permitted to read `feature_store` for sealing.** Seals the typed columnar artifact and records its hash | `feature_store` | `feature_snapshots`, artifact store |
| 22 | **Manifest Resolver** | Both | Resolves or creates the Semantic Contract Manifest for a run | Canonical version tables | `semantic_manifests` |
| 19 | **Page Builder** | Serving | Binds datasets to widgets | Intelligence metadata, datasets | Widget definitions |
| 20 | **Assistant** | Serving | Orchestrates tools, composes answers | Layer A tools, Layer B tools | Nothing |

### 2.2 The Semantic Wall

**Rule.** No component numbered 7 or higher may reference a customer physical table name, column name, schema name, or industry term. Their only vocabulary is the semantic code space defined in section 4.

**Enforcement, three layers, mirroring the existing isolation doctrine:**

| Layer | Mechanism |
|---|---|
| Database | The Layer B role holds grants on Plant Data and the intelligence schema only. No grant on Dump Store, no grant on any source-shaped schema |
| Application | Every source reference resolves through a published `definition_version`, and every entity correspondence through `RelationshipResolver`. A literal identifier in a Layer B code path has no resolution path and cannot execute |
| Test | An architecture test asserting that no file under the Layer B tree contains a customer identifier, an industry noun from the prohibited-vocabulary list, or a `switch` on tenant, site, or industry. Falsified once before it is trusted |

**The prohibited-vocabulary list is itself configuration**, seeded with the vocabulary of every installation encountered, so it grows as customers are added. It is not a hardcoded steel list.

### 2.3 The Serving Wall

**Rule.** The serving plane may read the active model version and the serving stores. It may never invoke a trainer, allocate a GPU, scan a raw source, or perform an unbounded scan of a data product.

**Enforcement:**

| Layer | Mechanism |
|---|---|
| Process | Serving runs in a separate process group with no import of any trainer module. A dependency test asserts the serving assembly does not reference the training assembly |
| Database | The serving role has SELECT only on serving stores, plus INSERT on the run and prediction tables. No DDL, no access to untrained-model artifacts |
| Runtime | Every serving query carries a cost estimate and a hard statement timeout. Tier 3 inserts a job request row and returns; it does not await |
| Test | A gate asserting no serving code path can reach a training entry point, and that the statement timeout is set on every serving connection |

### 2.4 Where the boundaries fall

| Boundary | Ends at | Starts at |
|---|---|---|
| Physical schema | Dump Store and the customer's own systems | - |
| Semantic model | - | Publication of the transformation and its `definition_version`, which emits the relationship model |
| Training data products | Canonical Plant Data | `journey_spine` materialisation |
| Learned intelligence | Data products | Model training |
| Serving | Activation | `status = 'active'` for a serving identity |

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
 (3) PUBLISHED DEFINITION VERSIONS + RELATIONSHIP MODEL + REGISTRY STATE
     pinned for reproducibility by a SEMANTIC CONTRACT MANIFEST
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
 (8) VALIDATION GATES         G-01..G-55
                               |
                               v
 (9) TRAINED MODEL VERSION --> champion/challenger --> ACTIVATION
                               |
      =============== SERVING WALL (one-way) ===============
                               |
                               v
(10) ACTIVE MODEL PER SERVING IDENTITY + EVIDENCE + PREDICTIONS + INDEX
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

### SM-01 Semantic Contract Manifest

**Canonical: `ppiq_meta.semantic_manifests` (Chapter 3 4.5.11 area, amendment C3-4).**

An **immutable, content-addressed reproducibility pin** over the canonical versions in force. A commit over the semantic contract. **It is not an authoring authority and has no lifecycle**: `definition_versions`, the relationship publication and `model_registry` retain their authority unchanged.

| Field | Type | Notes |
|---|---|---|
| `manifest_id` | uuid | **PK.** The handle artifacts reference |
| `tenant_id` | uuid NOT NULL | |
| `manifest_hash` | varchar(64) NOT NULL | Content hash over the referenced versions |
| `definition_versions` | jsonb NOT NULL | Array of `{definition_id, version_number}` |
| `relationship_source_definition_id` | uuid NOT NULL | |
| `relationship_source_definition_version` | integer NOT NULL | |
| `registry_snapshot_hash` | varchar(64) NOT NULL | Over the registry rows in force |
| `configuration_hash` | varchar(64) NULL | Governed configuration affecting semantics |
| `created_at_utc` | timestamptz NOT NULL | |

**UNIQUE `(tenant_id, manifest_hash)`.** Identical content within a tenant never creates a second row. Identical content across two tenants correctly creates two rows, because a manifest is tenant-owned evidence and a shared global row would be a cross-tenant object.

**No status column. No draft, validated, published or rolled-back state. Nothing updates a manifest.**

**Coverage rule.** Run and artifact tables carry `semantic_manifest_id uuid NULL FK`. **The column is nullable for legacy records only.** Every new governed AI/ML execution must resolve a manifest; a run that cannot is refused rather than recorded without one. Gate G-55.

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

**Convention.** Every table carries `tenant_id`, `site_id` and `created_at_utc`, omitted from the field lists below.

**Reproducibility pin.** Run, artifact and evidence tables carry `semantic_manifest_id uuid NULL FK` and the canonical lifecycle identities they depend on: `source_definition_id` and `source_definition_version` for the relationship publication, and the relevant `definition_version_id`. **This is not a blanket column on every table** - a lookup or projection table that produces no governed result carries no pin.

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

**Lineage.** The transformation `definition_version_id` plus `source_definition_id` and `source_definition_version` reproduce the node exactly. The `semantic_manifest_id` on the run that built it pins the whole contract in one value.

**Retention.** Full history. This is the cheapest product and the most reused.

### DP-2 Feature store

**Canonical: `ppiq_plant.feature_store` (Ch3 4.5.12).**

**Primary grain.** One row per `(material_unit_id, feature_set_version_id)`. **UNIQUE on that pair is the idempotency rule.**

| Column | Type | Notes |
|---|---|---|
| `material_unit_id` | uuid NOT NULL FK | The entity |
| `feature_set_version_id` | uuid NOT NULL FK -> `definition_versions(id)` ON DELETE RESTRICT | The feature-set definition version that produced it |
| `features` | jsonb NOT NULL | The assembled feature values |
| `label_value` | numeric(18,6) | Regression label |
| `label_class` | varchar(100) | Classification label |
| `assembled_at_utc` | timestamptz NOT NULL | |
| `source_batch_high_watermark` | text | |
| `lineage_hash` | varchar(64) NOT NULL | |
| `is_dirty` | boolean NOT NULL DEFAULT false | Marks rows needing recomputation |

Indexes: partial `(feature_set_version_id)` WHERE `is_dirty`; `(feature_set_version_id, assembled_at_utc)`. Partition: hash by `material_unit_id` above Large. Retention: while any active model or snapshot references the version.

**Incremental refresh is the mechanism, not an optimisation.** `feature_refresh_watermarks` holds `last_batch_watermark`, `dirty_entity_count`, `is_invalidated` and `invalidation_reason` per feature-set version. `feature_refresh_runs` records entities resolved, recomputed and dirty-remaining per run. The refresh scope is the distinct entities touched by batches landing since the last watermark:

```
refresh_scope = distinct material_unit_id
                FROM canonical rows
                WHERE import_batch_id IN (batches since last_refresh_watermark)
```

**The cost of an analysis becomes proportional to what changed, not to what exists.** Without this, every correlation run at a mature plant rescans years of observations and no pool tuning saves it.

**Reproducibility is `feature_snapshots`**, immutable. A snapshot records `feature_set_version_id`, `entity_count`, `taken_at_utc`, `source_batch_range`, `lineage_hash`, `storage_uri` and `retention_until_utc`; its rows carry `material_unit_id`, `features`, `label_value`, `label_class` with UNIQUE `(snapshot_id, material_unit_id)`. **Training pins a snapshot id.** A model is therefore always explicable against the exact population it saw.

**The training read path is the sealed columnar artifact, not PostgreSQL.**

```
live governed feature state     feature_store, jsonb, incremental, RLS
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

`feature_store` owns current governed state, lineage, row-level security and incremental refresh. **The artifact owns high-throughput training input.** Deserialising millions of JSONB objects per epoch is bounded by round-trips and JSON parsing rather than by the model.

**G-48: no training or encoding code path queries `feature_store`.** The snapshot materialiser is **exempt by definition** - reading `feature_store` is precisely how it seals the artifact, and it is the only component permitted to do so.

`feature_snapshot_rows` is an **optional audit sample** with a declared sampling rate, not the authoritative copy (amendment C3-2, conditional on B-03).

**Feature availability and the prediction cutoff.** The legality of a feature at a prediction point is a property of the feature-set definition, held in `feature_set_details` (feature list, grain, window, missing-value policy, scaling policy) and enforced at training by `model_training_runs.overlap_rows = 0`. Gate G-06 proves the property; the CHECK is the mechanism.

### DP-3 Sequence store

**Split contract. PostgreSQL holds a manifest; object storage holds the payload.**

**`ppiq_plant.sequence_manifests`** (amendment C3-3). One row per `(subject, channel_set_version, chunk_index)`.

| Field | Type | Notes |
|---|---|---|
| `subject_kind`, `subject_id` | varchar, uuid | Grain identity |
| **`channel_set_version`** | integer NOT NULL | **The channel set the encoder was trained on** |
| `time_from_utc`, `time_to_utc` | timestamptz NOT NULL | |
| `sample_count`, `channel_count` | integer, smallint | |
| `completeness` | numeric(9,6) NOT NULL | Observed fraction |
| `content_hash` | varchar(64) NOT NULL | |
| `storage_uri` | varchar(1000) NOT NULL | Chunk or chunk set |
| `chunk_index` | integer NULL | Where a subject spans chunks |
| `feature_snapshot_id` | uuid NULL FK | Participation in a sealed snapshot |
| `semantic_manifest_id` | uuid NULL FK | |

**Object storage** holds immutable chunked typed numeric arrays: values, offsets where irregular, and a mask. Compressed, partitioned by tenant and time, memory-mappable where the format allows. **The loader consumes bounded chunks, never a giant database row.**

**No numeric payload is stored in PostgreSQL.** This is the largest data product in the system; array columns carry per-row overhead, defeat compression, and put the largest byte volume through WAL, replication, backup and restore.

**`channel_set_version` is the encoder compatibility anchor.** An encoder is not merely frozen or stale; it is compatible or incompatible with the current channel set. Adding a production unit or an instrument changes the set, and G-13 refuses to serve an encoder whose version does not match.

Chunk size and compression are **B-04**. Retention is policy-driven and is the largest storage item in the system; see section 14.4.

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

**How AD-06 resolves CT-02.** Weekly insertion does not mutate a published index. It creates generation N+1 whose manifest names generation N as its base and lists the delta. Search fans out over the base plus the delta and merges. A trained model version pins `index_version`, which names an exact generation chain. A full rebuild seals a new base and resets `generation_no` to zero. **OD-04 (open) sets the rebuild trigger: a generation count ceiling, a delta-fraction ceiling, or a measured recall floor.**

### DP-6 Prediction store

**Primary grain.** One row per (grain_instance, prediction_point, outcome_code, model_version).

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
| `model_version` | text |
| `feature_row_id` | uuid |
| `scored_at_utc` | timestamptz |
| `is_current` | bool |

**Mutability.** Append-only. A rescore under a new model version inserts a new row and clears `is_current` on the old. **This is what allows a customer to ask why an answer changed between two Mondays and receive both rows.**

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
| `model_registry_id`, `model_code`, `model_version` | uuid, varchar, integer | Canonical model identity per Ch3 4.5.12 |
| `semantic_manifest_id` | uuid NULL FK | The Semantic Contract Manifest pinning the contract in force |
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

Nothing later may be built before everything earlier is complete under the same Semantic Contract Manifest.

---

## 6. INTELLIGENCE AND ENGINE FAMILY REGISTRY (Deliverable D)

**Seven families, five sub-types. Three of the seven are not models**, and the sub-type is load-bearing: it determines refresh policy, lane assignment and whether a champion/challenger gate applies at all.

| ID | Family | Sub-type | Lane | Champion/challenger |
|---|---|---|---|---|
| MF-01 | Process encoder | **Learned model** | `ml.training` | Yes, plus the promotion inequality |
| MF-02 | Similarity index | **Retrieval and index** | `ml.training` to build | No. Gated on recall@k |
| MF-03 | Normal and novelty | **Learned model** | `ml.training` | Yes |
| MF-04 | Supervised outcome | **Learned model** | `ml.training` | Yes, three-dimensional |
| MF-05 | Effect and envelope | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-06 | Statistical intelligence | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-07 | Practice learning | **Practice engine** | `analysis` | No. Governed signature version |

Plus **orchestration and governance**: the capability profiler, the model-count governor and the supervisor.


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
| **Promotion** | The encoder ships only when it earns its operational cost:<br>`promote_encoder iff metric_lift >= declared_min_lift AND p95_latency_delta <= declared_latency_budget AND artifact_size <= declared_size_class AND explanation_stability >= floor`<br>**If engineered features match it within the lift threshold, the engineered features ship.** Deep learning being available is not a reason to deploy it. Benchmark B-05 |
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
| **Contract** | `VectorSimilarityIndex` with build, seal, extend, search, recall_probe. **FAISS, HNSW, IVF and PQ are implementations selected by measurement. No library name appears in the contract** |
| **Validation** | **Recall@k against exact Flat search on a representative sample, measured on every build and stored on the index generation record.** A build below `recall_floor` does not become the served index. Plus p95 latency and generation-chain recall after N extensions |
| **Index policy** | Selected by measurement from population size, vector dimension, available RAM, required recall@k, p95 latency target, build time and update pattern. **Exact Flat is retained permanently on the representative sample as the correctness baseline.** HNSW, IVF, PQ, quantised and GPU-backed variants ship only where B-06 shows them appropriate |
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
| **Validation** | **Three-dimensional promotion gate.** QUALITY: discrimination or error, **calibration**, out-of-time performance, subgroup and regime stability, missingness robustness, **explanation stability**. SERVING: p50/p95/p99 latency, throughput, artifact size, RAM and VRAM, warm-up time. TRAINING: duration against the weekly window, peak memory against lane capacity, snapshot read throughput. **A better-discriminating, worse-calibrated model is not an improvement**, and **an unstable explanation is worse than none** |
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
3. A per-site **model budget** caps active supervised models. Initial default 50 per site. **OD-07** sets whether the budget is a fixed count, a compute-time budget, or both.
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
| C4 | Publish the feature-set definition | C1, C2 | `definition_versions` row with `feature_set_details` | definition version | CPU, minutes | Abort |
| C5 | Materialise `ppiq_plant.feature_store` | C4 | `feature_store` rows plus `feature_refresh_watermarks` | watermark | CPU, hours | Retry scope |
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
| C18 | Run validation gates G-01..G-55 | C17 | `gate_report` | none | CPU, minutes | Blocking gates abort promotion |
| C19 | Activate the first model version per eligible serving identity | C18 | model registry record, live alias set | none | seconds | Atomic; no partial publish |

The rule's fifteen conceptual stages become nineteen because feature catalogue, candidate enumeration and gate execution are separately restartable in practice.

### 7.2 The skip-and-continue principle

Stages C6, C9, C10, C11 and C12 are marked **skip family, continue**. This is the mechanism behind the poorest-customer requirement. A customer with no time series loses the encoder, the embedding store and the embedding-based index. Commissioning still completes and activates the supervised family, the effect layer, the feature-space similarity index and the novelty model on engineered features.

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
| W4 | Incremental feature refresh | delta | 1.0 h | 4 h | No |
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
| Ingest, spine, outcomes, feature refresh by watermark, sequences, embeddings, index generation | Novelty fit, supervised training, calibration, contributors, effects, profile, gates |

**No model weight is ever incrementally mutated.** Supervised models are retrained from scratch on the governed rolling window. This is cheaper than it sounds and infinitely more reproducible than online updates.

### 8.4 Encoder policy in the weekly window

The encoder is never retrained in W-stages. The Drift Supervisor may *request* an encoder refresh; the request enters the governed refresh queue and executes in a scheduled window with commissioning-class budget, not in the weekly window. On completion it triggers: re-encode reference population, rebuild index generation 0, revalidate, and activate the new encoder version with its dependent models together.

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
| **T1** | DP-6, DP-7, DP-5b, active model version metadata, Layer A summaries | target under 1 s | 5 s hard | Why is risk high; what resembles this; the approved envelope; contributors |
| **T2** | DP-2 recent slice, DP-1, DP-4 through the governed aggregate path | target under 30 s | **60 s hard statement timeout** | Compare two cohorts the user just defined; a bounded correlation on a filtered slice |
| **T3** | Nothing synchronously | returns in under 1 s | n/a | Anything the estimator cannot bound |

**The absolute synchronous ceiling is under 2 minutes**, and the T2 hard timeout of 60 seconds sits well inside it so that orchestration, serialisation and rendering cannot push a request past the ceiling.

### 9.4 The T2 exploratory classification

This closes CT-04. A tier 2 computation is a user-defined analysis executed at query time. It has not passed G-06 leakage checks, has no negative control, and its population was defined by a filter rather than by a governed manifest.

**Therefore every T2 result is emitted with `claim_class = ASSOCIATION` and `evidence_kind = exploratory`, is never written to the evidence store, and can never be cited by the Assistant as a finding.** It is shown to the user with the qualification that it is an exploratory calculation, not a governed finding. A T2 result may be *proposed* for promotion, which creates a manifest row for the governed pipeline to evaluate on the next weekly run. It is never promoted at query time.

### 9.5 Serving state versus training state, stated as a table

| | Training state | Serving state |
|---|---|---|
| Reads | All data products, full history, sealed snapshots | Serving stores, bounded slices |
| Writes | `model_registry` rows at `trained`, evidence, snapshots | Job request rows, plus `prediction_runs`, `predictions` and `prediction_current` from the inference service |
| Compute | GPU permitted, unbounded scans, hours | Bounded scans, seconds. **No training dependency; GPU use is optional and benchmark-driven** |
| DB role | `ppiq_layerb_train` | `ppiq_layerb_serve`, SELECT plus narrow INSERT |
| Process | Scheduler-invoked jobs | Request-response service |
| May call the other | Activation writes to serving stores | **Never** |

---

## 10. GOVERNED OUTPUT DATASETS (Deliverable G)

Seven datasets. Each is an ordinary governed analytical source declared in `ppiq_meta.registry_intelligence_sources` with `sourceKind = 'intelligence'`, bindable in Page Builder with no ML-specific code.

**One result envelope, source-declared row shapes.** The widget execution contract is `columns + rows + warnings` (Ch3 DF7). A source declares its own columns and their `columnRoles`. **A fact-shaped measure may project through WidgetFact into the generic aggregate executor; a native-grain rich source keeps its declared columns and is never flattened into a single value column.** The two classes are specified in section 45.4.

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
| `model_code`, `model_version` | dimensions | |

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

Dimensions: `model_family`, `model_code`, `outcome_code`, `prediction_point_code`, `readiness_state`, `failed_clause`, `model_version`, `champion_status`.
Measures: `measured_value` and `required_threshold` for the failed clause (both non_additive), `training_population_n` (additive), `days_of_history` (non_additive).

**This dataset is what a dashboard binds to when nothing is ready.** It is the reason a fresh installation shows a stated readiness picture rather than empty widgets, and it is the direct product realisation of Rule 2 starting empty without looking broken.

**Seven families is canonical (CT-07 CLOSED).** Model and Readiness Status is the seventh, and it is what a new installation binds to before any model is ready.

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
    "model_code": "...", "model_version": 0,
    "semantic_manifest_id": "...",
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
| `GetModelVersion` | none | Active model version manifest, component versions, training window, promotion time | T1 |
| `GetPrediction` | grain instance, optional prediction point and outcome | Prediction rows | T1 |
| `GetPredictionContributors` | prediction id, top n | Contributor rows, claim class fixed | T1 |
| `FindSimilarJourneys` | subject id, k, optional filters | Neighbour rows with their outcomes and practices | T1 |
| `GetAnomalyEvidence` | subject id or window | Novelty score, percentile, contributing channels, nearest regime | T1 |
| `GetOperatingEnvelope` | parameter, context | Envelope rows with evidence level and population | T1 |
| `GetFinding` | filter by driver, outcome, status | Finding rows including refusals | T1 |
| `ComparePractices` | two cohort definitions, outcome | Matched comparison with support, overlap, conditioning, confounder limits | **T2** |
| `ProposeRemediation` | subject id or condition | Suggestion **only if** MF-05 recommendation rule passes; otherwise refusal | T1 |
| `RequestAnalysisJob` | analysis spec | Job id and state | T3 |

### 11.2a The runtime the tools sit inside

The eleven tools are step 4a of a nine-step runtime specified in Chapter 4 5.7.9 (amendment C4-7):

```
[1] permission and tenant context   [2] intent and entity resolution
[3] DETERMINISTIC TOOL PLANNER      [4a] structured tools  [4b] evidence retrieval
[5] evidence packing, budgeted      [6] model gateway      [7] LLM, phrasing only
[8] deterministic answer verification                      [9] cited answer or refusal
```

Four properties of that runtime bear directly on the Layer B tool contract:

- **The LLM does not choose tools.** A planner maps resolved intent to a declared tool set. Tool-selection accuracy is gated (Q-01)
- **Permission filtering happens before ranking**, not after, so a forbidden chunk cannot displace a permitted one
- **Structured tools take precedence over retrieval for facts and analytical results.** A number never comes from a retrieved chunk when a tool can compute it
- **Verification is deterministic and does not call the LLM.** Every numeric claim must resolve to a handle the tools supplied

### 11.3 Assistant discipline rules

1. The Assistant never queries a model artifact, a training store, or a candidate model version. Only these eleven tools plus the Layer A tools.
2. The Assistant may not upgrade a claim class. A `PREDICTIVE_CONTRIBUTION` may not be phrased as a cause. A tool-response claim class maps to a fixed set of permitted phrasings.
3. When `terminal_state` is not `FINDING`, the Assistant states the refusal. It does not substitute a general-knowledge answer, and it does not soften the refusal into a hedge.
4. A numeric answer to a quantity question must carry the unit from `physical_quantity` plus `unit_code`. A response whose unit does not match the quantity class of the question is a **hard failure**, not an inaccuracy, and gate G-20 tests exactly this.
5. Every answer containing a learned claim carries at least one `evidence_id`.
6. When both layers contribute, the answer separates them explicitly: the exact fact, then the learned finding, then the evidence, then the qualification.

---

## 12. PAGE BUILDER INTEGRATION (Deliverable I)

### 12.1 Registration contract

At activation, the Evidence Materialiser registers each of the seven datasets in the same dataset catalogue Layer A uses. Registration supplies: dataset code, grain, dimension list with semantic types and labels, measure list with units and **aggregation policy**, time field, default filters, and compatibility hints.

**From the Page Builder's perspective there is no difference between an intelligence dataset and any other governed dataset.** The metadata endpoint returns the same shape.

### 12.2 The customer journey, unchanged from ordinary data

```
Add Widget -> select dataset (an intelligence dataset appears in the same list)
  -> choose dimension (from metadata, not a compiled list)
  -> choose measure (aggregation policy enforced by the engine)
  -> chart types narrow automatically by compatibility
  -> filter, save, cross-filter
```

### 12.3 Execution and the two source classes

The widget execution contract is `columns + rows + warnings`, with `sourceKind` of `canonical` or `intelligence`, plus `intelligenceSource` and `columnRoles` (Ch3 DF7).

| Class | Examples | Execution |
|---|---|---|
| **Fact-shaped aggregate source** | Exact canonical measures; aggregateable intelligence measures such as mean risk score by variant, finding count by status | May project through `WidgetFact` into the generic aggregate executor |
| **Native-grain rich source** | Readiness rows, findings, prediction detail, contributors, similarity neighbours, practice matches, value derivation, remediation eligibility | **Keeps its governed multi-column shape.** Never flattened into a single decimal value |

Both classes use the same registry, the same authoring shell, the same selection and filter contract, the same result envelope, the same widget system and the same evidence rules.

The aggregate engine gains **no** knowledge that a measure is learned. It gains exactly one behaviour: it honours `aggregation_policy` and refuses a disallowed aggregation with a named message. That mechanism is generic and applies equally to any Layer A measure declared non-additive.

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
| Intelligence and engine family registry MF-01 to MF-07 | Used | Used | **Identical** |
| Commissioning stages C1..C19 | Used | Used | **Identical** |
| Weekly stages W1..W16 and abort ladder | Used | Used | **Identical** |
| Daytime tiers T1..T3 | Used | Used | **Identical** |
| Output datasets 10.1..10.7 | Used | Used | **Identical** |
| Assistant tools, all eleven | Used | Used | **Identical** |
| Page Builder binding path | Used | Used | **Identical** |
| Validation gates G-01 to G-55 | Used | Used | **Identical** |

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
| **G-13** | **Encoder compatibility**: the model registry record's `channel_set_version` equals current | C9, W6, W16 | **Yes** | Version pair |
| **G-14** | **Embedding-version compatibility**: index generation chain resolves to exactly one encoder version | C11, W7, W16 | **Yes** | Generation chain |
| **G-15** | Model performance above the mandatory baseline model | C13, W10 | Yes | Candidate versus baseline metrics |
| **G-16** | Subgroup and variant stability: no variant level below the declared floor | C13, W10 | Records, blocks promotion on severe | Per-variant metrics |
| **G-17** | Champion versus challenger on the same governed holdout | W14 | **Promotion gate** | Comparison table plus decision reason |
| **G-18** | Reproducibility: seeds, dataset manifest, code identity, environment, artifact hashes all present | C19, W16 | Yes | Manifest |
| **G-19** | **Refusal integrity**: every non-FINDING row names a method-side cause where the cause is method-side, and carries the measured statistic where the cause is data-side | C17, W15 | Yes | Refusal audit |
| **G-20** | **Unit sanity**: every Assistant numeric response unit matches the physical quantity class of the question, tested against a fixed probe set | C19, W16 | **Yes** | Probe results |
| **G-21** | **Tenant isolation**: no training population, index generation, embedding or evidence row crosses tenant boundary | C19, W16 | **Yes** | Boundary scan |
| **G-22** | Rollback drill: the previous model version can be reactivated and reproduces its recorded metrics | C19, monthly | Yes | Drill record |

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

Every model version, feature snapshot, prediction and finding pins a **`semantic_manifest_id`**, plus the canonical identities it depends on: `feature_set_version_id`, `source_definition_id` and `source_definition_version`. A republished definition does not silently invalidate models; it creates a new manifest, and the supervisor decides whether to retrain, quarantine or continue. **Training against a mutable ad-hoc definition is structurally impossible because the trainer reads only published versions and sealed snapshots.**

### 16.5 Weekly scheduler <-> model registry

The scheduler produces candidate model versions and never sets `status = 'active'`. Only the Model Activator does, only after G-17 and all blocking gates pass. A failed weekly run leaves the current active version untouched.

---

## 17. MODEL REGISTRY, ACTIVATION AND ROLLBACK (Deliverable F)

**Canonical: `ppiq_plant.model_registry` (Ch3 4.5.12), governed per serving identity.**

### 17.1 Serving identity

```
serving identity  = ( tenant_id , model_code , outcome_code , grain_code )
serving version   = serving identity + model_version
```

`outcome_code` and `grain_code` are **model identity, not metadata**. A model predicting one outcome at one grain is not interchangeable with one predicting another. Both are set at training from the model definition and are immutable for the version. Every uniqueness rule, activation rule, fallback rule, drift record, artifact cache key and compatibility check uses this five-part identity and never a shorter one.

### 17.2 The two independent axes

| Axis | Column | Values |
|---|---|---|
| **Lifecycle** | `status` | `trained`, `rejected`, `active`, `review`, `retired` |
| **Serving approval** | `serving_role` | `none`, `serving_fallback` |

A model is the active primary when `status = 'active'`. It is an approved fallback when `serving_role = 'serving_fallback'`. **There is no `fallback_approved` lifecycle status, and no state is ever encoded in both columns.**

### 17.3 Constraints that make the relationship unambiguous

| Constraint | Effect |
|---|---|
| Partial UNIQUE `(tenant_id, model_code, outcome_code, grain_code)` WHERE `status = 'active'` | **At most one active version per serving identity** |
| Partial UNIQUE `(tenant_id, model_code, outcome_code, grain_code)` WHERE `serving_role = 'serving_fallback'` | **At most one approved fallback per serving identity** |
| CHECK `serving_role = 'none' OR status IN ('active','trained')` | A retired, rejected or under-review model can never hold a fallback approval |
| CHECK `NOT (status = 'active' AND serving_role = 'serving_fallback')` | One version can never be both primary and fallback for the same identity, because a fallback that is already the primary would silently mask the absence of a safety net |

Every UNIQUE constraint on a tenant-owned table carries `tenant_id` as its first column. Row-level security filters what a query returns; **it does not make a UNIQUE constraint tenant-local**, and a constraint omitting `tenant_id` would leak across tenants through violation messages.

### 17.4 What a version records

`definition_version_id`, `algorithm`, `feature_set_version_id`, `feature_list`, `training_snapshot_id`, `split_strategy`, `missing_value_policy`, `scaling_params`, `hyperparameters`, `metrics`, `acceptance_floor`, `artifact_uri`, `trained_at_utc`, `activated_at_utc`, `retired_at_utc`, `validity_until_utc`.

Training runs are `model_training_runs`, carrying `policies_applied`, row counts, metrics, importance, calibration, and **CHECK `overlap_rows = 0`**, which makes leakage a database-level impossibility rather than a test.

### 17.5 Activation, fallback and rollback

**Activation** sets `status = 'active'` on a version and retires the previous holder of that serving identity. The partial unique index makes the transition atomic per identity.

**Fallback** is an explicit approval, never inferred from the last active version. The six conditions a fallback must satisfy are Ch4 5.6.7a. `prediction_runs.fallback_model_registry_id` and `fallback_reason` record its use, and `prediction_current.fallback_in_use` surfaces it.

**Rollback** activates the prior version of the same serving identity. Because activation is per identity, a rollback of one model never disturbs another.

### 17.6 What every artifact pins

`definition_version_id` and `feature_set_version_id` and `training_snapshot_id`, plus `source_definition_id` and `source_definition_version` for the relationship publication in force. A result is explicable from these alone.

## 18. TRADEOFFS, OPEN DECISIONS, AND WHAT THIS DESIGN DOES NOT DECIDE

### 18.1 Deliberate tradeoffs, with what each costs

| # | Tradeoff | Chosen | Cost accepted |
|---|---|---|---|
| 1 | Boosting over deep networks for the decision | Boosting | Gives up some accuracy on strongly sequential effects; buys attribution, speed and defensibility |
| 2 | Full weekly retrain over incremental updates | Full retrain | Costs hours weekly; buys reproducibility and comparability |
| 3 | Materialised feature store over per-run computation | Materialised, incremental | Costs a refresh pipeline and watermark discipline; buys analysis cost proportional to what changed rather than to what exists |
| 4 | Generational index over live mutation | Generational | Search fans out over generations; buys a reproducible retrieval result |
| 5 | Per-serving-identity activation | Canonical | A dependent set (encoder, index, models) is activated together by procedure and proven by G-13 and G-14, not by a container object |
| 6 | Refusal as data | Materialised rows | Storage and query surface; buys honest dashboards and an auditable engine |
| 7 | Conservative cost estimator | Conservative | Some answerable questions go to tier 3; buys a latency contract that holds |
| 8 | Encoder optional | Optional | Two code paths in MF-02 and MF-03; buys the poorest customer a working product |

### 18.2 Architectural decisions - all closed

**No architectural decision remains open.** The three that governed storage, scope and output families are closed, and the closures are recorded in the Layer B Rule appendices and in section 43.

| ID | Decision | State |
|---|---|---|
| **OD-02** | Storage placement of Layer B outputs and artifacts | **CLOSED.** The three-schema law stands. Customer-derived analytical and intelligence datasets to Plant Data; operational and control-plane metadata to Meta Data; pre-semantic source-shaped data to Dump Store; model binaries, checkpoints and vector-index artifacts to object storage. No fourth application schema. Section 43.4 |
| **OD-13** | Scope authority between the Layer B Rule and the Master Design chapters | **CLOSED.** Chapter 2, then Chapter 3, then Chapter 4, then the Rule as a subsystem constitution. Where the Rule is narrower, the chapters govern. Rule Appendix A |
| **CT-07** | Six or seven governed output dataset families | **CLOSED.** **Seven**, including Model and Readiness Status, so a new installation renders `MODEL_NOT_READY` and `INSUFFICIENT_DATA` truthfully rather than appearing broken |

The remaining open items are **measured parameters with canonical homes**, listed in section 40.2. Each is a number to be measured and written into an existing canonical field. None is an architecture decision.

### 18.3 Rule reconciliation

How this pack reconciles with the Layer B Rule. Every item is closed. The Rule carries Appendix A, which subordinates it to the Master Design chapters and names the capabilities its body omitted.

| ID | Contradiction | Status |
|---|---|---|
| **CT-01** | Section 9 forbids training hundreds of models but sets no mechanism, while sections 12 and 13 imply a model per outcome per prediction point | **Closed here** by the model-count governor, section 6.7. Thresholds are OD-07 |
| **CT-02** | Weekly incremental index insertion versus reproducible retrieval. A mutating index cannot be a pinned artifact | **Closed** by AD-06 generational index |
| **CT-03** | Section 4 forbids using ML to approximate an exact BI fact; section 21 makes Layer B outputs ordinary datasets, which lets a user sum predicted probabilities into something shaped exactly like a fact | **Closed here** by `aggregation_policy` |
| **CT-04** | Section 19 permits tier 2 bounded calculation at query time, which bypasses every gate in section 13 and L, yet its output is presented alongside governed findings | **Closed here** by the exploratory classification, section 9.4 |
| **CT-05** | Sections 11 and 13 require intervention history for effect levels 3 and 4 and for the profile, but the section 2 declaration contract defines no intervention object | **Closed here** by SM-07 `is_intervention` |
| **CT-06** | Section 15 says freeze the encoder between refreshes, but a structural change to the instrument set makes a frozen encoder invalid rather than merely stale. The rule has no concept for this | **Closed here** by `channel_set_version` and G-13 |
| **CT-07** | Six versus seven governed output dataset families | **CLOSED. Seven**, including Model and Readiness Status, which is what an empty installation binds to |
| **CT-08** | Measured sizing versus the two-minute ceiling | **Not a defect, and stated to customers.** The latency contract holds at every tier; what varies by tier is how many questions are answerable synchronously |

### 18.4 What this design intentionally does NOT decide

So that design is never mistaken for implementation authorisation:

1. No physical storage product is selected. Placement is settled in section 43.4; the storage engine behind each placement is an OD-01 benchmark.
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

## 19. WHAT PART ONE ESTABLISHES

Part One is directly decomposable into work packages. Every item below states a canonical contract with no decision left to the implementer.

- the eleven input contract objects and their validation gates (section 4)
- all seven data products with field-level contracts, mutability, partitioning and lineage, bound to canonical objects in section 43 (section 5)
- the seven intelligence and engine families MF-01 to MF-07 with eligibility expressions, validation metrics and refresh policies (section 6)
- all three orchestration sequences with stage dependencies, checkpoints, budgets and the abort ladder (sections 7, 8, 9)
- the model registry, serving identity, activation, fallback and rollback design (section 17)
- the seven governed output dataset families under one result envelope with source-declared row shapes (sections 10, 45)
- the eleven Assistant tools with a common evidence-bearing response envelope (section 11)
- the Page Builder registration and execution contract (section 12)
- the complete gate inventory G-01 to G-55 (sections 15, 30.2, 40.1)

**Storage placement is settled** and is section 43.4: analytical and intelligence datasets to Plant Data, operational metadata to Meta Data, source-shaped data to Dump Store, binaries to object storage, no fourth application schema.

**What remains is measurement, not architecture.** Eligibility thresholds, the hardware benchmark, retention values and the reserved interactive capacity fraction are numbers to be measured and written into existing canonical fields. Section 40.2 names each field.

---
---

# PART TWO - MASTER DESIGN CONVERGENCE PASS

---

## 20. RESPONSE TO THE TRACEABILITY AUDIT

### 20.1 What I accept without qualification

Seven of the eight gaps are real, and six of them are my errors of scope rather than errors of the audit.

| Gap | My assessment |
|---|---|
| **Statistical and correlation engine** | **Accepted, and it is the worst of the eight.** Part One reduced correlation to one line inside daytime tier 2. The Master Design treats it as a standing governed engine producing findings, running before ML, on its own schedule. A bounded query-time calculation is not that engine. This is a category error on my part, not an omission of detail |
| **Practice learning engine** | **Accepted.** MF-05 conflated two different things: envelope mining, which is a statistical summary, and practice signature learning, which is a canonicalisation and matching problem with its own data product. Collapsing them lost the exact-versus-relaxed matching, the back-off rule and the sensitivity state entirely |
| **Operational prediction contract** | **Accepted.** Part One designed how a prediction is produced and stored, and said nothing about whether it arrives while the plant can still act on it. That is the whole point of goal (c). A prediction delivered after the last actionable stage is a record, not an intervention |
| **Nine-check remediation gate** | **Accepted.** My four-clause rule was a simplification of a safety surface. In a live plant a recommendation reaches a person who may act on it. Four checks where the design requires nine is not a simplification, it is a reduction in safety margin |
| **Decision, outcome, effectiveness, feedback loop** | **Accepted, and this is the largest conceptual gap.** Part One has a learning lifecycle for models. It has no lifecycle for the industrial event: prediction, recommendation, human decision, action, actual outcome, evaluation. Without it the claim that the system becomes the in-house expert is unevidenced |
| **Value engine** | **Accepted as missing.** With one correction to the audit in 20.2 below |
| **Scenario simulation and full supervisor** | **Accepted.** My Drift Supervisor is a monitoring component. The Master Design supervisor proposes bounded adjustments, shadow-runs them, compares on held-out history and requires human approval. Those are different components with the same name |

### 20.2 Where I qualify the audit

**Q1. The percentages should not enter a governance document.** The audit states 85 to 90 percent of the Layer B rule and 65 to 75 percent of the full engine contract. The gap list is correct and actionable; the percentages are not measurable against any defined denominator. The traceability matrix in section 31 replaces them with a per-capability state, which is falsifiable.

**Q2. Text evidence and inspection-image intelligence are not gaps of the same class as the other seven.** The other seven are missing designs for capabilities whose inputs already exist in the semantic model. These two are new **modalities**: a different input contract, a different encoder family, a different storage profile, different eligibility, and for images a materially different compute cost. Designing them inside a convergence pass would produce a shallow design that looks complete and is not. **Section 29 therefore defines the modality extension contract** - how any new modality plugs into the semantic model, the sequence store analogue and the evidence envelope - and explicitly does not design vision or NLP. That keeps the genericity claim honest.

**Q3. The currency in the value engine must not be euro.** The audit says bounded euro impact. A generic product declares its currency per tenant. Section 26 uses `currency_code` throughout. This is a small thing that would have become a genericity defect on the first non-eurozone customer.

**Q4. The feedback loop introduces a write path, and that needs stating explicitly.** PPIQ is a read-only platform with respect to customer systems. Decisions, action assignments and effectiveness records are writes. They are writes into **PPIQ's own governed decision ledger**, never into a customer system and never into a control system. Section 25.6 states this as a hard boundary, because a reviewer encountering accept, reject, defer and assign for the first time will reasonably ask whether the product has started controlling the plant. It has not, and the architecture must say so in a place a reviewer will find it.

### 20.3 What the audit did not find, and I am raising myself

**F1. Closing these gaps makes the Architecture Pack exceed its own governing rule.** The frozen Layer B rule does not contain a statistical engine, a practice signature product, a decision ledger, a value engine or a scenario surface. If Part Two is added, the pack becomes broader than the constitution it was written to satisfy. That is a governance inconsistency, not a technical one, and it has exactly two honest resolutions:

- **Amend the frozen rule** with an appendix naming the eight capabilities as in-scope for Layer B, keeping the rule as the authority; or
- **Rule that the Master Design chapters outrank the Layer B rule** on scope, and record the Layer B rule as a subset instrument.

**OD-13 is CLOSED in favour of the chapters**, which predate the rule. The Rule carries Appendix A recording the authority order, so the two documents no longer claim scope over the same subsystem.

**F2. The eight gaps are not independent, and treating them as a flat list will produce rework.** Section 32 gives the dependency order. Three of the eight can be contract-reserved rather than fully designed now, without blocking backlog decomposition of the rest.

**F3. The statistical engine and the supervised family now overlap and need a boundary rule.** Both can answer "is parameter X related to outcome Y". Without a boundary they will produce two different numbers for the same question and the Assistant will have to choose. Section 21.6 defines the boundary.

---

## 21. STATISTICAL INTELLIGENCE ENGINE (DF9) - MODEL FAMILY MF-06

**Governed correlation is this engine.** Tier 2 calculation in the daytime path (section 9.4) is exploratory only and produces no finding.

### 21.1 Position in the chain

DF9 runs **before** ML training, on its own schedule, and produces findings that stand alone. It is not a preprocessing step for MF-04 and it is not a query-time convenience. A customer whose data never becomes eligible for supervised models still receives DF9 findings, which is why it must not be downstream of the model families.

### 21.2 The method registry, DP-15 `statistical_method_registry`

> **BOUND TO `the registry-driven Group B, C and D block rows of Chapter 4 5.5, with results in ppiq_plant.correlation_results and runs in compute_runs`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Registry-driven, never a hardcoded switch. One row per method.

| Field | Type | Notes |
|---|---|---|
| `method_code` | text | PK |
| `method_family` | enum | correlation, association, group_difference, distribution, trend, lag, stability |
| `x_type` | enum | numeric, ordinal, categorical_binary, categorical_multi, time |
| `y_type` | enum | same domain |
| `assumptions` | text[] | normality, monotonicity, independence, homoscedasticity, expected_cell_count |
| `assumption_tests` | text[] | The test that decides whether each assumption holds |
| `min_population` | int | |
| `min_cell_count` | int | Nullable, categorical methods |
| `effect_size_measure` | text | r, rho, Cramer V, eta squared, Cliff delta, odds ratio, rate difference |
| `significance_measure` | text | p, exact p, permutation p |
| `supports_stratification` | bool | |
| `supports_lag` | bool | |
| `output_dataset` | text | Always the finding dataset |
| `is_enabled` | bool | |

**Seed method set.** Pearson, Spearman, Kendall, point-biserial, chi-square with Fisher exact fallback, Cramer V, ANOVA, Welch ANOVA, Kruskal-Wallis, Mann-Whitney, Cliff delta, logistic association, Theil-Sen trend, Mann-Kendall, cross-correlation with lag, and a distribution-shift test. **The registry ships with these and accepts more without a code change.**

### 21.3 The execution contract, in fixed order

```
1  candidate pair enumeration      (x_code, y_code, position, grain)
2  population alignment            join x and y at a common grain via DP-1
3  eligibility check               type pair has an enabled method, min population met
4  assumption testing              run the declared assumption tests
5  method selection                the eligible method whose assumptions hold
6  confounder and strata policy     stratify by declared conditioning set,
                                    excluding COLLAPSED dimensions
7  estimate                        effect size AND significance, never one alone
8  multiple-testing correction     Benjamini-Hochberg FDR across the family,
                                    q-value stored beside p-value
9  stability check                 bootstrap or split-half resampling;
                                    sign and magnitude stability recorded
10 lag scan                        where supports_lag, scan the declared window
11 negative control                the declared control pair must not move
12 terminal state                  FINDING or one of the five refusals
13 evidence row                    written to DP-7 with claim_class = ASSOCIATION
```

**Step 3 is the one that matters most.** When no enabled method exists for a type pair, the terminal state is `NOT_APPLICABLE` with `refusal_reason` naming the missing method and the pair. **It is never reported as a property of the data.** This is the exact defect class already measured once in this product, where a method gap was reported as zero variance in the customer's data. Gate G-19 already exists for it; G-23 below makes it specific to DF9.

### 21.4 Multiple testing is structural, not optional

A plant with 400 parameters and 6 outcomes generates 2,400 candidate pairs before stratification. At p below 0.05 with no correction, 120 false findings are expected from noise alone. **A finding without a q-value is not publishable by this engine.** The FDR family is defined as all pairs tested within one run for one outcome; the family definition is recorded on every finding so the correction can be reproduced.

### 21.5 Output extension to the finding dataset

Section 10.6 gains: `x_code`, `y_code`, `x_type`, `y_type`, `method_code`, `assumption_results`, `effect_size_measure`, `effect_size_value`, `p_value`, `q_value`, `fdr_family_id`, `fdr_family_size`, `stability_score`, `lag_seconds`, `strata_count`, `negative_control_result`.

Measures follow the aggregation policy of section 10: effect sizes and p-values are `non_additive`. **A dashboard cannot average p-values, and the schema is what stops it.**

### 21.6 The DF9 versus MF-04 boundary rule (finding F3)

| Question shape | Owner | Claim class |
|---|---|---|
| Is X related to Y in the population | **DF9** | ASSOCIATION |
| How much does X contribute to this specific prediction | **MF-04 plus SHAP** | PREDICTIVE_CONTRIBUTION |
| Does changing X change Y | **MF-05** | MATCHED_EFFECT_ESTIMATE or higher |

When both DF9 and MF-04 have something to say about the same pair, the Assistant returns **both**, labelled, and never reconciles them into one number. A population association and a model contribution disagreeing is information, not a defect, and the most common cause is a confounder that the model absorbed and the bivariate test did not.

### 21.7 Registry entry

| Attribute | Value |
|---|---|
| Eligible inputs | DP-1, DP-2, DP-4. Runs without sequences, without embeddings, without labels beyond the outcome itself |
| Minimum population | Per method, from the registry |
| Refresh | Weekly, full recompute. Cheap relative to training |
| Compute class | CPU, minutes to a few hours depending on pair count |
| Refusal states | All six |
| Emitted datasets | Finding dataset |

**DF9 is the engine that a level L0 or L1 customer lives on.** It requires no encoder, no labels beyond a declared outcome, and no genealogy. It should be the first thing built and the first thing demonstrated.

---

## 22. PRACTICE LEARNING ENGINE (DF12) - MF-07 AND DP-16

**Scope boundary.** MF-07 produces practice signatures and matches. MF-05 consumes them for effect estimation and envelopes; it does not derive practices.

### 22.1 What a practice is, as a contract

A practice is the combination of operating parameter values, the operation sequence, and the context, over a defined production window. It is not a single parameter setting, which is why an envelope is not a practice.

### 22.2 DP-16 `practice_signature`

> **BOUND TO `ppiq_plant.practice_signatures`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `signature_id` | uuid | PK |
| `signature_hash` | text | **Canonical hash over the ordered, binned, normalised component set** |
| `signature_version` | int | Increments when the binning or component policy changes |
| `grain_code` / `spine_node_id` | | Where the practice was observed |
| `window_kind` | enum | process_based, time_based, campaign_based |
| `window_start_utc` / `window_end_utc` | timestamptz | |
| `components` | jsonb | Ordered array of `{parameter_code, aggregation, binned_value, bin_edges, unit_code}` |
| `operation_sequence` | text[] | Ordered event or position codes |
| `context_keys` | jsonb | Variant, crew, campaign, ambient, excluding COLLAPSED dimensions |
| `support_count` | bigint | Occurrences of this exact signature |
| `first_seen_utc` / `last_seen_utc` | timestamptz | |
| `is_active` | bool | Seen within the drift window |

**Canonicalisation rules, which are the whole difficulty of this product:**

1. Numeric components are binned by a **declared tolerance**, not by a learned clustering. Tolerance comes from SM-05 `valid_range` and a declared relative tolerance per parameter. Learned binning would make signatures unstable between weeks.
2. Components are ordered by `parameter_code` for hashing, so component ordering cannot change a hash.
3. Categorical components use the declared level code, never a display label.
4. The operation sequence is included in the hash only when the position graph permits ordering variation; on a fixed chain it is constant and adds nothing.
5. **The hash is over `signature_version` plus components plus sequence plus context.** A binning policy change produces a new version and does not silently merge or split historical signatures.

### 22.3 DP-17 `practice_match`

> **BOUND TO `ppiq_plant.practice_statistics`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

The exact-and-relaxed matching contract. One row per (subject, signature, similarity level).

| Field | Type | Notes |
|---|---|---|
| `match_id` | uuid | PK |
| `subject_spine_node_id` | uuid | |
| `signature_id` | uuid | |
| `similarity_level` | enum | **exact, relaxed_1, relaxed_2, relaxed_3** |
| `exact_support_count` | bigint | Population matching on all dimensions |
| `relaxed_support_count` | bigint | Population after back-off |
| `relaxed_dimensions` | text[] | **Which dimensions were dropped to reach support** |
| `backoff_rule_code` | text | The declared rule that governed the back-off |
| `sensitivity_state` | enum | **stable, sensitive, unstable** |
| `sensitivity_detail` | jsonb | Which dropped dimension changed the outcome estimate and by how much |
| `outcome_rate` | numeric | Observed outcome rate in the matched population |

**The back-off rule.** When exact support falls below the declared floor, dimensions are dropped in a **declared priority order**, never a discovered one. Each drop is recorded. Support is recomputed. Back-off stops at the first level meeting the floor, or exhausts and returns `INSUFFICIENT_DATA`.

**The sensitivity state is the honesty mechanism.** After back-off, the estimate is recomputed holding each dropped dimension fixed in turn. If the estimate moves more than the declared tolerance, the state is `sensitive` and the finding carries that word. If it moves more than the estimate itself, the state is `unstable` and **the finding is not emitted as a practice recommendation at all**. A relaxed match that is silently presented as an exact one is the single most misleading output this subsystem could produce.

### 22.4 MF-07 registry entry

| Attribute | Value |
|---|---|
| Purpose | Learn, canonicalise, match, rank and monitor operating practices |
| Eligible inputs | DP-1, DP-2 controllable features, DP-7 events, DP-4 outcomes, SM-08 context |
| Eligibility | At least 30 distinct signatures with support at or above floor; at least one controllable parameter; at least one outcome or productivity measure |
| Outputs | DP-16, DP-17, plus best-practice and failure-associated-practice findings |
| Ranking | Practices ranked by outcome rate **within matched context**, with support and sensitivity shown. **Never ranked by outcome rate alone across contexts** |
| Failure linkage | A practice is failure-associated when its outcome rate exceeds the matched-cohort baseline with the DF9 significance and FDR discipline applied |
| Drift | A signature whose outcome rate shifts beyond tolerance between windows raises a practice-drift finding |
| Benchmarking | Within tenant and site only. Cross-tenant benchmarking is prohibited by section 27 of the frozen rule |
| Refresh | Weekly. Signature version changes are a governed refresh, not weekly |
| Refusal states | All six, plus `INSUFFICIENT_DATA` when back-off exhausts |

### 22.5 What MF-05 keeps

MF-05 remains the effect and envelope layer. It now takes `practice_signature` and `practice_match` as inputs. Envelopes remain a per-parameter product; practices remain a combination product. **They answer different questions and both are needed:** an envelope says where one parameter should sit, a practice says which combination actually worked here.

---

## 23. OPERATIONAL PREDICTION AND EARLY WARNING (DF13a)

**This section specifies delivery while action is still possible.** Production and storage of predictions are section 5 DP-6 and section 9.

### 23.1 DP-18 `prediction_current`

> **BOUND TO `ppiq_plant.prediction_current`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

One row per (grain instance, outcome), holding the live operational state. This is the table the operator surface and the alert interface read.

| Field | Type | Notes |
|---|---|---|
| `grain_instance_key` | text | PK part |
| `outcome_code` | text | PK part |
| `prediction_id` | uuid | FK to DP-6, the current prediction |
| `predicted_probability` / `predicted_value` | numeric | |
| `risk_band` | text | |
| `current_position_code` | text | Where the instance is now |
| **`last_actionable_position_code`** | text | **The last position at which a remediation exists** |
| **`actionable_deadline_utc`** | timestamptz | **When action stops being possible** |
| **`time_remaining_seconds`** | numeric | Derived, refreshed on read |
| **`actionability_state`** | enum | **actionable, closing, expired, not_applicable** |
| `scoring_mode` | enum | event, micro_batch, scheduled |
| `delivery_latency_ms` | int | From triggering data arrival to row write |
| `model_state` | enum | primary, fallback, unavailable |
| `fallback_reason` | text | Nullable |
| `queue_state` | enum | scored, queued, deferred, dropped |
| `stage_already_passed` | bool | True when the prediction arrived after its own prediction point |
| `scored_at_utc` / `valid_until_utc` | timestamptz | |

### 23.2 How the deadline is computed, generically

```
last_actionable_position = the latest position P in the instance's route where
    a remediation candidate exists whose controllable parameter is
    authored at P, and P precedes the outcome's detection_position

actionable_deadline_utc = predicted arrival time at last_actionable_position
    computed from SM-03 residence models along the remaining route

actionability_state =
    expired          when now > deadline
    closing          when time_remaining < declared_warning_fraction of residence
    actionable       otherwise
    not_applicable   when no remediation candidate exists at any remaining position
```

**Nothing in that computation names an industry.** It uses the position graph, the residence models and the controllability flags, all of which are declared.

### 23.3 Scoring modes

| Mode | Trigger | Latency target | When chosen |
|---|---|---|---|
| `event` | Arrival of the data completing a prediction point | seconds | Short residence, tight deadline |
| `micro_batch` | Fixed interval, typically 1 to 15 minutes | interval plus seconds | Moderate residence, high volume |
| `scheduled` | Weekly or daily batch | hours | No actionable window remains; the prediction is analytical, not operational |

Mode is **declared per (outcome, prediction point)** and constrained by the deadline: a prediction point whose remaining residence is shorter than the micro-batch interval may not use micro-batch mode. Gate G-26 asserts this.

### 23.4 Primary and fallback

When the active primary is unavailable, in `review` or `retired`, scoring falls back to the **explicitly approved fallback** for that serving identity, the version carrying `serving_role = 'serving_fallback'`. **A fallback is never inferred from the last active version**, and the six conditions it must satisfy are Ch4 5.6.7a. Use is recorded on `prediction_runs.fallback_model_registry_id` and `fallback_reason` and surfaced through `prediction_current.fallback_in_use`. **Silently serving a fallback as the primary is prohibited**; gate G-27 asserts the fields are populated and surfaced.

### 23.5 The serving-wall consequence

Event and micro-batch scoring run in the serving plane using the active model version. They are inference, not training, and they respect every constraint of section 9.5. **This is the only serving-plane component permitted to write to a data product**, and its write is confined to DP-6 and DP-18.

---

## 24. REMEDIATION SAFETY ARCHITECTURE (DF13b)

**Canonical: Ch4 5.6.4d for the gate, Ch3 4.5.12 and 4.5.12a for persistence and acceptance, Ch3 4.5.12b for escalation.**

### 24.1 Two tables, deliberately separated

A remediation candidate is a **historical template**: a practice difference some population of comparable units benefited from. Whether it is **actionable** is a property of one specific prediction at one moment, because the same template is actionable for a unit two stages away and not for a unit that has passed the stage. **Storing eligibility on the template would be wrong.**

| Object | Role |
|---|---|
| **`ppiq_plant.remediation_candidates`** | The **global historical template**, computed once per condition and reused across predictions. CHECK `support_count >= 20`; CHECK `source_practice_sensitivity_state <> 'unstable'`. A template below support or from an unstable practice is never created, and the insufficiency is reported from the run record instead |
| **`ppiq_plant.prediction_remediation_evaluations`** | The **per-prediction nine-check evaluation**, produced at scoring time and re-evaluated whenever the unit's stage position changes. UNIQUE `(prediction_id, remediation_candidate_id)` |

**Five checks are properties of history** and are evaluated at template generation: support, stratification survival, uncertainty, uplift and source-practice sensitivity. **Four are situational and cannot be**, because they depend on where the unit is now.

### 24.2 The nine checks

| # | Check | Passes when | Refusal |
|---|---|---|---|
| 1 | **Controllability** | Every parameter the candidate would change is declared controllable at the proposed stage in `registry_dimensions.is_controllable` / `controllable_at_stages` / `adjustment_range` | `RM01`, naming the parameter |
| 2 | **Remaining actionable stage** | The proposed stage is still ahead of the unit on its declared route, with a declared minimum lead time | `RM02` |
| 3 | **Operating and specification limits** | Proposed values sit inside `operating_limits` and `product_specifications` for that unit's specification | `RM03`, naming the limit |
| 4 | **Forbidden combinations and safety** | The proposed combination violates no rule in `ppiq_plant.forbidden_combinations` | `RM04`, naming the rule |
| 5 | **Historical support** | Support at or above the floor, at the disclosed similarity level | `RM05`, count shown |
| 6 | **Contextual and confounder survival** | The association survives stratification by the declared confounders in the unit's own context | `RM06`, naming the stratum |
| 7 | **Uncertainty** | The expected-effect interval excludes no-effect, or the candidate is explicitly exploratory | `RM07` |
| 8 | **Causal or uplift evidence where data permits** | An uplift estimate over comparable non-adopters supports the effect; where variation is insufficient the candidate is marked `association_only` | `RM08` |
| 9 | **Sensitivity** | The source practice is `sensitivity_state = 'stable'` | `RM09` |

### 24.3 The four outcomes - canonical mapping

| Outcome | Condition | Presentation |
|---|---|---|
| **`actionable`** | **All nine pass** | A remediation card with the proposed practice, support, expected-effect interval, evidence and limitations. The only outcome styled as a recommendation |
| **`evidence_only`** | **Checks 5 to 9 pass, but one or more of checks 1 to 4 fail for this unit** | Shown in drill-down as an observed historical difference that is not actionable here, with the failing check named. Never styled as a recommendation, no decision control |
| **`exploratory`** | **Checks 1 to 6 pass, check 7 or 8 fails** | Shown behind an explicit exploratory disclosure with the uncertainty and the failed check stated. No accept action at any tier for any role |
| **`suppressed`** | **Check 4 fails on a safety constraint** | Not shown at all in the card list; recorded on the run with `RM04` so the suppression is auditable rather than invisible |

CHECK `eligibility_state <> 'actionable' OR failed_checks IS NULL OR jsonb_array_length(failed_checks) = 0` makes an actionable candidate with a failed check a database-level impossibility.

### 24.4 `can_accept` is NOT equal to actionable

**`can_accept` is the complete seven-condition server-side acceptance authority** (Ch3 4.5.12a). It is false unless every one of these holds:

| # | Condition |
|---|---|
| 1 | `eligibility_state = 'actionable'` - all nine checks passed for this prediction |
| 2 | `remaining_stage_state` is `ahead` or `imminent` - the proposed stage has not passed |
| 3 | The prediction's `actionable_deadline_utc` has not elapsed |
| 4 | The prediction is still open: not already decided, not superseded by a newer scoring run |
| 5 | No safety constraint has become invalidating since the evaluation - re-checked on read |
| 6 | The model that produced the prediction is not in `review` or `retired` |
| 7 | The tenant's entitlement and the caller's role permit a remediation decision |

**The client reads `can_accept` and nothing else.** It renders the Accept affordance from that single boolean and uses `can_accept_blockers` only to explain an absent affordance. **A client that additionally tests the deadline, the stage or the eligibility state has created a second authorisation rule, and the two will eventually disagree.** The server enforces the same boundary on the write path: a decision on an evaluation whose `can_accept` is false is refused with `RM10`, whatever the client believed.

CHECK `can_accept = false OR eligibility_state = 'actionable'`.

### 24.5 The decision boundary is wider than Accept

**Accept, Reject and Defer all exist only where `can_accept` is true.** `evidence_only`, `exploratory` and `suppressed` candidates carry no decision control of any kind, at any tier, for any role. They are not merely un-acceptable: they are **outside the decision record entirely**, because rejecting or deferring an observation would enter it into the effectiveness and feedback statistics as though it had been offered as a recommendation, corrupting exactly the measurements the product exists to produce.

### 24.6 Escalation is a record, never a decision

**`ppiq_plant.remediation_escalations`** carries an `evidence_only` or `exploratory` candidate to engineering investigation through `POST /api/predictions/{id}/escalate`. `actionable` can never be escalated because it can be decided; `suppressed` can never be escalated because it is not shown.

It records `failed_checks_at_escalation` frozen at escalation time, a required reason, the actor, and a resolution from `no_action`, `definition_changed`, `limit_changed`, `controllability_registered`, `data_gap_raised`, `promoted_to_actionable`, `withdrawn`. Partial UNIQUE `(tenant_id, prediction_id, remediation_candidate_id)` WHERE `resolved_at_utc IS NULL` gives at most one open escalation per pair and is also the idempotency rule.

**It creates no `prediction_actions` row, contributes to no `remediation_effectiveness` row, and is excluded from `feedback_records`.** An escalation says an engineer should look at this, not that we decided something. `promoted_to_actionable` is the one resolution that changes product behaviour and is audited as a governed change, not a data edit.

## 25. DECISION, OUTCOME, EFFECTIVENESS AND FEEDBACK (DF14)

The industrial loop, which Part One did not contain.

```
prediction -> recommendation -> HUMAN DECISION -> action -> actual outcome
   -> prediction evaluation -> remediation effectiveness -> feedback record
   -> governed supervisor review -> (never automatic retraining)
```

### 25.1 DP-20 `decision_record`

> **BOUND TO `ppiq_plant.prediction_actions and suggestion_decisions`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `decision_id` | uuid | PK |
| `prediction_id` / `remediation_evaluation_id` | uuid | What was decided on |
| `decision` | enum | **accept, reject, defer** |
| `decided_by` / `decided_at_utc` | | Identity and time |
| `reason_code` / `reason_text` | | **Required on reject and defer** |
| `defer_until_utc` | timestamptz | Required on defer |
| `assigned_to` / `assigned_at_utc` | | Nullable, set on accept |
| `can_accept_at_decision_time` | bool | Snapshot, so a later rule change cannot rewrite history |
| `model_registry_id`, `model_version` | | Which model version produced the recommendation |

**Reject reasons are the highest-value data in this table.** An operator rejecting a recommendation because it is operationally impossible is telling the system something no dataset contains, and the reason taxonomy must be declared, not free text alone.

### 25.2 DP-21 `action_record`

> **BOUND TO `ppiq_plant.prediction_actions, same row`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type |
|---|---|
| `action_id` | uuid PK |
| `decision_id` | uuid |
| `performed` | bool |
| `performed_at_utc` | timestamptz |
| `performed_by` | text |
| `actual_parameter_value` | numeric |
| `deviation_from_suggested` | numeric |
| `performed_within_deadline` | bool |
| `notes` | text |

### 25.3 DP-22 `prediction_evaluation`

> **BOUND TO `ppiq_plant.prediction_evaluations`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Written when the actual outcome arrives from canonical data, never entered by hand.

| Field | Type | Notes |
|---|---|---|
| `evaluation_id` | uuid | PK |
| `prediction_id` | uuid | |
| `actual_outcome_row_id` | uuid | FK to DP-4 |
| `predicted_value` / `actual_value` | | As stored, not recomputed |
| `correctness_class` | enum | true_positive, false_positive, true_negative, false_negative, within_tolerance, outside_tolerance |
| `error` / `absolute_error` | numeric | Regression |
| **`intervened`** | bool | **Was an accepted action performed on this instance** |
| `evaluation_state` | enum | evaluable, **not_evaluable_intervened**, pending, censored |

**`not_evaluable_intervened` is the subtle and essential state.** If the system predicted a defect, a human acted, and no defect occurred, the prediction was not wrong. Counting it as a false positive would train the system to stop warning about the problems it successfully prevented. **Intervened instances are excluded from accuracy metrics and reported separately as prevented-event candidates.** Gate G-29 asserts the exclusion.

### 25.4 DP-23 `remediation_effectiveness`

> **BOUND TO `ppiq_plant.remediation_effectiveness`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Post-action measurement, which is not the same thing as the pre-action effect estimate in MF-05.

| Field | Type | Notes |
|---|---|---|
| `effectiveness_id` | uuid | PK |
| `action_id` | uuid | |
| `comparable_cohort_definition` | jsonb | Matched non-intervened instances |
| `cohort_n` / `cohort_outcome_rate` | | |
| `intervened_outcome` | | |
| `effectiveness_estimate` / `lower` / `upper` | numeric | |
| `claim_class` | enum | **MATCHED_EFFECT_ESTIMATE at best.** A single action never yields causal evidence |
| `terminal_state` | enum | Including INSUFFICIENT_DATA when the cohort is too small |

**One action is never an effectiveness measurement.** Effectiveness accumulates across actions on the same recommendation type, and the schema makes the population explicit so a single anecdote cannot be presented as proof.

### 25.5 DP-24 `feedback_record`

> **BOUND TO `ppiq_plant.feedback_records`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

The governed input to the supervisor.

| Field | Type | Notes |
|---|---|---|
| `feedback_id` | uuid | PK |
| `feedback_kind` | enum | prediction_evaluation, effectiveness, rejection_reason, drift_observation, practice_drift |
| `source_record_id` | uuid | |
| `eligibility_state` | enum | **eligible, insufficient, quarantined** |
| `aggregated_into_review_id` | uuid | Nullable |

**Nothing in this table triggers a retrain.** Feedback accumulates into a supervisor review, a human approves an action, and the action executes in a governed window. Section 28 defines that path. The rule is absolute because a feedback loop that retrains automatically is a loop with no human in it, and in a plant that is the wrong kind of loop.

### 25.6 The write-path boundary (finding Q4)

| PPIQ writes to | PPIQ never writes to |
|---|---|
| Its own decision ledger, DP-20 to DP-24 | Any customer source system |
| Its own data products and evidence | Any MES, LIMS, historian or ERP |
| Its own prediction stores | **Any control system, PLC, DCS or setpoint** |

**An accepted recommendation produces a record that a human acted, not an action.** The product remains read-only toward the plant. This paragraph exists so that the accept, reject, defer and assign vocabulary in this section is never mistaken for control.

---

## 26. VALUE ENGINE

### 26.1 SM-14 `CostAssumptionContract`

> **BOUND TO `the cost inputs of Chapter 4 5.6.4 Value attachment, persisted in ppiq_plant.value_impacts.inputs`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Declared by the customer, versioned, and **never inferred**.

| Field | Type | Notes |
|---|---|---|
| `assumption_code` | text | PK |
| `assumption_version` | int | |
| `currency_code` | text | **Per tenant. Never hardcoded** |
| `basis` | enum | per_unit_scrap, per_unit_downgrade, per_hour_downtime, per_unit_rework, per_unit_yield_loss, per_unit_energy, custom |
| `outcome_code` | text | What this cost attaches to |
| `low_value` / `mid_value` / `high_value` | numeric | **A range, always. A single number is not accepted** |
| `confidence` | enum | declared, estimated, benchmark |
| `valid_from` / `valid_to` | date | |
| `declared_by` | text | |

### 26.2 DP-25 `value_impact`

> **BOUND TO `ppiq_plant.value_impacts`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `value_id` | uuid | PK |
| `subject_kind` | enum | finding, prediction, recommendation, practice |
| `subject_id` | uuid | |
| `assumption_code` / `assumption_version` | | |
| `currency_code` | text | |
| `impact_low` / `impact_mid` / `impact_high` | numeric | **Always a bounded range** |
| `basis_population_n` | bigint | |
| `time_basis` | enum | per_event, per_day, per_month, per_year |
| `derivation` | jsonb | **Every input to the arithmetic, for drill-through** |
| `terminal_state` | enum | Including `INSUFFICIENT_BASIS` |

### 26.3 DP-26 `value_realization_ledger`

> **BOUND TO `ppiq_plant.value_realization_ledger`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Predicted benefit against realised benefit, per accepted recommendation.

| Field | Type |
|---|---|
| `ledger_id` | uuid PK |
| `decision_id` | uuid |
| `predicted_impact_low/mid/high` | numeric |
| `realised_impact_low/mid/high` | numeric |
| `realisation_basis` | jsonb |
| `attribution_confidence` | enum: attributed, partially_attributed, unattributable |
| `payback_days` | numeric |
| `realised_at_utc` | timestamptz |

### 26.4 The abstention rules, which matter more than the arithmetic

1. **Bounds are mandatory when the basis is sufficient.** CHECK `basis_status = 'InsufficientBasis' OR (lower_bound IS NOT NULL AND upper_bound IS NOT NULL)`. A `point_estimate` may sit beside the bounds; it may never stand alone.
2. **No value without a declared assumption.** Missing assumption produces `INSUFFICIENT_BASIS`, never an industry default.
3. **No value on an unrealised recommendation** beyond a clearly labelled potential impact.
4. **No aggregation of potential impacts into a total saving.** The measure is `non_additive` by aggregation policy. **Summing potential savings across findings produces the single most dangerous number this product could display**, because it is the number a buyer will repeat and the one that will be tested against reality first.
5. **Attribution is explicit.** Where a benefit cannot be attributed to the accepted action against a comparable cohort, `attribution_confidence = unattributable` and the realised figure is not claimed.

> **Commercial note, stated plainly.** The value engine is the most persuasive output in the product and the most exposed. A defensible bounded range with a stated basis survives a CFO's questions. A confident total does not survive the first quarter in which it fails to appear in the accounts.

---

## 27. SCENARIO SIMULATION

### 27.1 Contract

| Element | Rule |
|---|---|
| Scenario definition | Named, saved, versioned, owned by a user |
| Allowed variables | **Controllable parameters only.** An observed parameter cannot be set in a scenario |
| Baseline | An explicit population or instance, recorded with the scenario |
| Valid ranges | Each variable constrained to SM-05 `valid_range` intersected with **observed support range**. Extrapolation beyond observed practice is refused, not warned |
| Model pinning | An exact `model_registry_id` and `model_version`, recorded on `scenario_runs`. A scenario run against a different model version is a **new** run, never a silent recomputation |
| Output | Predicted outcome with uncertainty interval, plus contributor breakdown, plus the forbidden-combination check from section 24 |
| Comparison | Up to N scenarios side by side against the baseline |
| Export | Permitted |
| **Write path** | **None. A scenario never writes a setpoint, a recommendation, a decision or a plant value.** It is a read-only calculation over a pinned model |

### 27.2 Execution tier

Scenario evaluation is inference on a pinned model over a small input set. It is **tier 2**, target under 30 seconds. A scenario sweep across many combinations exceeds that and is **tier 3**, an asynchronous job. The cost estimator decides by combination count, and the ceiling is the same as everywhere else.

### 27.3 The honesty constraint

A scenario answer is a model prediction under a counterfactual input, which is a **weaker** claim than a matched effect estimate, because nothing was matched and nothing was observed. Its `claim_class` is `PREDICTIVE_CONTRIBUTION`, never `MATCHED_EFFECT_ESTIMATE`. **Scenario output must not be phrased as what will happen. It is what the model predicts under these inputs, given this training window.**

---

## 28. THE FULL ENGINE SUPERVISOR

**Component 17 of section 2.** Monitoring is one of its six functions, not the whole of it.

### 28.1 The six functions

| # | Function | Description |
|---|---|---|
| 1 | **Observe** | Runs, gates, drift metrics, prediction evaluations, effectiveness, rejection reasons, practice drift |
| 2 | **Propose** | A **bounded** adjustment: threshold, calibration, refresh trigger, eligibility parameter, practice tolerance, model retirement |
| 3 | **Shadow** | Execute the proposal in a dry run against held-out history. Nothing published |
| 4 | **Compare** | Candidate against current on the same governed holdout, using the same metrics as champion-challenger |
| 5 | **Approve** | **Human approval required.** No proposal applies without it |
| 6 | **Apply** | Atomic, versioned, with provenance and a rollback pointer |

### 28.2 The prohibited set, which is the design's safety margin

The supervisor may **never** modify: readiness thresholds that would make an ineligible method eligible, refusal rules, evidence requirements, leakage gates, tenant isolation, the semantic model, or the forbidden-combination set.

**The reason is one sentence.** A component whose job is to improve results, and which can also lower the bar for what counts as a result, will eventually improve results by lowering the bar. The prohibition is what makes the compounding claim honest rather than self-fulfilling.

### 28.3 DP-27 `supervisor_proposal`

> **BOUND TO `ppiq_meta.supervisor_proposals, supervisor_shadow_runs, supervisor_provenance`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type |
|---|---|
| `proposal_id` | uuid PK |
| `trigger_kind` | enum: drift, performance, feedback, effectiveness, schedule |
| `evidence_ids` | uuid[] |
| `proposed_change` | jsonb |
| `shadow_run_ref` | text |
| `holdout_comparison` | jsonb |
| `state` | enum: proposed, shadowed, approved, rejected, applied, rolled_back |
| `approved_by` / `approved_at_utc` | |
| `applied_model_version` | text |
| `rollback_pointer` | text |

### 28.4 Abstention

When the evidence does not support a change, the supervisor **records that it considered and abstained**, with the reason. A supervisor that only speaks when it acts leaves no evidence that it was working during a quiet quarter.

---

## 29. MODALITY EXTENSION CONTRACT AND EXTERNAL INTERFACES

### 29.1 Position, stated honestly

Text evidence and inspection-image intelligence are **not designed in this pack** (finding Q2). What is designed is the contract by which any new modality enters Layer B without a redesign. Designing vision inside a convergence pass would produce something that looks complete and is not.

### 29.2 The extension contract

A new modality is admissible when it supplies:

1. A semantic declaration under SM-11 with `role_kind = extension`, naming its subject grain and its timestamp
2. A **store analogous to DP-3**: subject key, content reference, a `modality_set_version`, a mask or completeness measure, and a content hash
3. An **encoder** registered as an MF family with its own version, eligibility, refusal states and compute class
4. Output that respects the governed-model boundary of Ch4 5.8.6 as amended: **no free-form or model-generated output may become a feature, a score, a statistic or a value.** A modality enters a learned result **only** through Path B below
5. Full participation in the evidence envelope, the claim classes and the terminal states

**Two paths, and the boundary is governance rather than modality.**

**Path A, evidence modality.** Retrieved and cited. Corroborates a deterministic result, never originates one. No feature, no score, no plant fact the model invented.

**Path B, governed multimodal ML.** An explicitly authored model definition, a versioned immutable training snapshot, declared leakage controls, held-out validation, a `model_registry` entry, calibration and drift monitoring, and a learned output carrying a claim class and provenance. **This is how an inspection-image model produces an annotation with a confidence**, which Ch4 5.8.7 requires and the previous absolute wording forbade.

**No implementation scope is added by this distinction.** Both modalities remain interface-designed, future implementation.

**No modality may bypass the leakage gate.** An inspection image carries a capture timestamp and a capture position, and G-06 applies to it exactly as to a numeric feature. An image captured after the prediction point is illegal input regardless of how informative it is.

### 29.3 Named candidates, deferred

| Modality | Canonical persistence and access | Authority | State |
|---|---|---|---|
| **Text evidence** | `ppiq_plant.text_documents` and `text_passages`; full-text plus the embedding path; passages become `assistant_chunks` with `chunk_family = 'DOC'`; `role_scope` per passage, so a passage can be more restricted than the row it describes | Ch4 5.8.6 | **INTERFACE-DESIGNED, future implementation** |
| **Inspection images** | `ppiq_plant.inspection_images` with `storage_uri` only, never database blobs; `image_annotations` with region, confidence and the model version; vision models register in `model_registry` under the same activation, retirement and drift rules; signed time-limited URLs, every access audited | Ch4 5.8.7 | **INTERFACE-DESIGNED, future implementation** |

### 29.4 External interface references

These functions exist in the product and are **not owned by Layer B**. Layer B publishes to them and does not implement them.

| Function | Owner | Layer B obligation |
|---|---|---|
| Alert routing and escalation | Notification and workflow subsystem | Publish DP-18 rows with `actionability_state` and deadline. **Layer B does not decide who is notified** |
| Assistant page context, permission-scoped retrieval, glossary and synonym resolution, no-fabrication guard, egress policy | **DF15 Assistant architecture** | Layer B owns only the eleven intelligence tool contracts of section 11 and their evidence envelope |
| User identity, roles and permissions | Platform | Layer B receives a scoped principal and filters by tenant and site |
| Job scheduling infrastructure | Platform scheduler | Layer B declares stages, dependencies and budgets |

**This table exists to prevent both gaps and duplication.** Anything in it is somebody's, and it is not Layer B's.

---

## 30. REVISED CROSS-CONTRACTS AND ADDITIONAL GATES

### 30.1 New and revised interfaces

| Interface | Contract |
|---|---|
| **DF9 <-> MF-04** | Section 21.6 boundary. Both may speak about the same pair; neither reconciles the other; the Assistant returns both labelled |
| **MF-07 <-> MF-05** | Practice engine produces signatures and matches; effect layer consumes them. MF-05 no longer derives practices |
| **DP-18 <-> alert routing** | Layer B publishes state and deadline; the notification subsystem decides recipients |
| **DP-19 <-> decision surface** | `can_accept` is the only field the surface reads. No component re-derives eligibility |
| **DP-22 <-> model metrics** | Intervened instances are excluded from accuracy and reported separately |
| **DP-24 <-> supervisor** | Feedback accumulates into proposals; **nothing retrains automatically** |
| **Value engine <-> Page Builder** | Impact measures are `non_additive`; the aggregate engine refuses to sum them |
| **Scenario <-> model registry** | A scenario pins an exact `model_registry_id` and `model_version`; a version change makes a new run, never a silent recomputation |
| **Decision ledger <-> customer systems** | One-way and internal. PPIQ writes only to its own ledger, never to a plant system |

### 30.2 Additional gates G-23 to G-35

| ID | Gate | When | Blocking |
|---|---|---|---|
| **G-23** | DF9 method-gap integrity: every `NOT_APPLICABLE` names the missing type pair and method, never a data property | DF9 run | **Yes** |
| **G-24** | Multiple-testing correction applied; every finding carries `q_value` and a reproducible `fdr_family_id` | DF9 run | **Yes** |
| **G-25** | Practice signature stability: a hash recomputed from the same inputs under the same `signature_version` is identical | Practice run | **Yes** |
| **G-26** | Scoring mode feasibility: micro-batch interval is shorter than the remaining residence at the prediction point | Activation | **Yes** |
| **G-27** | Fallback transparency: `model_state` populated on every prediction and surfaced downstream | Serving, continuous | **Yes** |
| **G-28** | Remediation completeness: all nine checks evaluated and recorded, including passes | Recommendation emit | **Yes** |
| **G-29** | Intervened exclusion: no intervened instance contributes to an accuracy metric | Evaluation run | **Yes** |
| **G-30** | Value abstention: no point estimate, no value without a declared assumption version, no summed potential impact | Value run | **Yes** |
| **G-31** | Scenario containment: no scenario writes any store other than its own saved definition and result | Serving, continuous | **Yes** |
| **G-32** | Supervisor prohibition: no applied proposal touches readiness, refusal, evidence, leakage, isolation, semantic model or forbidden combinations | Proposal apply | **Yes** |
| **G-33** | Human approval present on every applied supervisor proposal | Proposal apply | **Yes** |
| **G-34** | Sensitivity honesty: no `unstable` practice match is emitted as a recommendation; every `sensitive` one carries the state | Practice run | **Yes** |
| **G-35** | Control-path absence: no Layer B component holds a write path to any customer or control system | Activation, continuous | **Yes** |

Every one is falsified once before it is trusted, per section 15.

---

## 31. MASTER DESIGN CAPABILITY TRACEABILITY MATRIX

Replaces the audit's percentage estimates with a per-capability state that can be checked.

**States.** `COVERED` design complete here. `COVERED-EXT` design complete, extends a Part One section. `RESERVED` contract defined, detailed design deliberately deferred. `EXTERNAL` owned by another subsystem, interface defined here. `OPEN` needs a ruling.

| # | Capability | State | Section |
|---|---|---|---|
| 1 | Generic across any industry | COVERED | 2, 13 |
| 2 | Semantic model instead of physical tables | COVERED | 4 |
| 3 | Cross-stage linking and genealogy | COVERED | 4 SM-04, 5 DP-1 |
| 4 | Continuous-flow plants, residence model | COVERED | 4 SM-03 |
| 5 | Weighted transformational genealogy | COVERED | 5 DP-1 |
| 6 | Feature engineering and historical feature state | COVERED | 5 DP-2 |
| 7 | Time-series sequence processing | COVERED | 5 DP-3 |
| 8 | Computational fingerprint: embeddings, regimes, similarity | COVERED | 6 MF-01, MF-02, MF-03 |
| 9 | **Governed plant fingerprint: semantic model, genealogy, features, outcomes, practices, models, decisions, prediction outcomes, drift, feedback** | **COVERED-EXT** | **31.1 below** |
| 10 | Historical similarity | COVERED | 6 MF-02 |
| 11 | Normal and abnormal regime learning | COVERED | 6 MF-03 |
| 12 | Supervised prediction | COVERED | 6 MF-04 |
| 13 | Prediction explainability and contributors | COVERED | 6, 10.2 |
| 14 | **Statistics and correlation engine** | **COVERED-EXT** | **21** |
| 15 | **Practice signature engine** | **COVERED-EXT** | **22** |
| 16 | **Exact and relaxed practice matching, back-off, sensitivity** | **COVERED-EXT** | **22.3** |
| 17 | **Failure-practice linkage and best-practice ranking** | **COVERED-EXT** | **22.4** |
| 18 | **Operational prediction queue and current-state contract** | **COVERED-EXT** | **23.1** |
| 19 | **Actionable prediction deadline** | **COVERED-EXT** | **23.2** |
| 20 | **Near-real-time, event and micro-batch scoring** | **COVERED-EXT** | **23.3** |
| 21 | **Primary and fallback model state** | **COVERED-EXT** | **23.4** |
| 22 | **Nine-check remediation safety gate** | **COVERED-EXT** | **24** |
| 23 | **Accept, reject, defer** | **COVERED-EXT** | **25.1** |
| 24 | **Action assignment and performance recording** | **COVERED-EXT** | **25.2** |
| 25 | **Actual outcome arrival and evaluation lifecycle** | **COVERED-EXT** | **25.3** |
| 26 | **Prediction correctness evaluation** | **COVERED-EXT** | **25.3** |
| 27 | **Remediation effectiveness measurement** | **COVERED-EXT** | **25.4** |
| 28 | **Governed feedback loop** | **COVERED-EXT** | **25.5** |
| 29 | **Value and ROI impact engine** | **COVERED-EXT** | **26** |
| 30 | **Scenario and what-if simulation** | **COVERED-EXT** | **27** |
| 31 | **Full supervisor: shadow, holdout, approval** | **COVERED-EXT** | **28** |
| 32 | Model registry, versioning, rollback | COVERED | 17 |
| 33 | Initial heavy training | COVERED | 7 |
| 34 | Weekly governed retraining | COVERED | 8 |
| 35 | Daytime pre-trained serving under 2 minutes | COVERED | 9 |
| 36 | Drift detection | COVERED | 15 G-11, 28 |
| 37 | Capability, readiness, refusal | COVERED | 4.1, 10.7, 13 of the rule |
| 38 | Evidence and provenance | COVERED | 5 DP-7 |
| 39 | Intelligence into ordinary charts and widgets | COVERED | 10, 12 |
| 40 | Intelligence into the Assistant | COVERED | 11 |
| 41 | Tenant isolation | COVERED | 15 G-21 |
| 42 | No automatic plant control | COVERED-EXT | 25.6, G-35 |
| 43 | Large-data architecture | COVERED, benchmark pending | 14, OD-11 |
| 44 | Benchmarking | COVERED-EXT, within tenant only | 22.4 |
| 45 | **Text evidence** | **RESERVED** | **29.2, 29.3** |
| 46 | **Inspection-image intelligence** | **RESERVED** | **29.2, 29.3** |
| 47 | **Alert routing and escalation** | **EXTERNAL** | **29.4** |
| 48 | **DF15 Assistant page context, permissions, glossary, egress** | **EXTERNAL** | **29.4** |
| 49 | Scope authority between the Layer B rule and the Master Design chapters | **COVERED** | Rule Appendix A; section 18.2 |

### 31.1 The two fingerprints, reconciled

The audit is right that the pack narrowed the fingerprint to its computational form. Both definitions are now explicit and neither replaces the other.

**Computational fingerprint** - what the models learn: embeddings, learned regimes, similarity structure, model parameters. Owned by MF-01, MF-02, MF-03.

**Governed plant fingerprint** - what the installation accumulates: the semantic model and its versions, the genealogy graph, historical features and outcomes, practice signatures, model versions and their lineage, decisions with their reasons, prediction evaluations, effectiveness records, drift observations and feedback.

> **The commercial sentence follows from the second, not the first.** What makes the system the in-house expert is not the embedding. It is that a new engineer arriving in year three can read what was declared, what was learned, what was recommended, what was accepted, what was rejected and why, what actually happened, and what the system concluded from the difference. **No competitor's model file contains that, because it is not a model, it is an institutional memory.** It is also, not coincidentally, the hardest thing for a customer to leave behind.

---

## 32. REVISED OPEN DECISIONS, SEQUENCING, AND THE COMPLETION TEST

### 32.1 Decision state

All architectural decisions are closed. The items below are **measured parameters**, each with a canonical field to be written into once measured. They are listed here for the benefit of the work packages that consume them.

| ID | Parameter | Canonical home |
|---|---|---|
| OD-05, OD-06 | Encoder and supervised eligibility thresholds | `model_details.acceptance_floor`; the gate minimums of Ch4 5.6.3 |
| OD-11 | Hardware sizing | The capacity model of Ch4 5.3.3 |
| OD-12, OD-21 | Sequence and snapshot retention | `feature_snapshots.retention_until_utc`; per-stage retention policy |
| OD-25 | Reserved interactive capacity fraction | The `interactive` reservation of Ch4 5.3.2 |
| OD-04 | Index rebuild trigger | `index_generation` policy |
| OD-07 | Model budget as count, compute time, or both | The `compute_weight` calibration of Ch4 5.3.2 |

### 32.2 Dependency order (finding F2)

The eight gaps are not a flat list. This is the order in which they can be built without rework.

```
TIER 1  no dependencies, build first
  21 Statistical engine         (needs only spine, features, outcomes)
  22 Practice engine            (needs spine, features, events)

TIER 2  depend on tier 1 and on Part One models
  23 Operational prediction     (needs MF-04, residence models)
  24 Remediation safety         (needs 22 sensitivity, MF-05 effect, SM-13)

TIER 3  depend on tier 2
  25 Decision and feedback loop (needs 23 and 24 to have something to decide on)

TIER 4  depend on tier 3
  26 Value engine               (needs 25 for realised value)
  28 Full supervisor            (needs 25 feedback records as input)

TIER 5  independent, deferrable without rework
  27 Scenario simulation        (needs only MF-04 and a pinned model version)
  29 Modality extensions        (contract only; design deferred)
```

**The consequence for backlog decomposition.** Tiers 1 and 2 can be decomposed and started now. Tier 5 can be contract-reserved indefinitely. Only tiers 3 and 4 must wait, and they wait on implementation of the tiers below them rather than on any decision.

**DF9, the statistical engine, should be first.** It has no dependencies, it is the engine a low-maturity customer lives on, it needs no encoder and no labels beyond a declared outcome, and it is the most demonstrable capability in the product without a completed training run.

### 32.3 Decomposition readiness

Every capability in Part Two states a behaviour and binds to a canonical persistence object in section 43. An implementation lead reads the behaviour here and the table there, and writes tasks against canonical columns.

The dependency order in 32.2 governs sequencing. Tiers 1 and 2 can start immediately. Tier 5 is contract-reserved. Tiers 3 and 4 wait on implementation of the tiers below them, not on any decision.

---
---

# PART THREE - PLATFORM INTEGRATION PASS

**Six ruled requirements, added for the source-reconciliation and freeze pass.**

---

## 34. CANONICAL PLANT DATA TO VERSIONED INTELLIGENCE INPUT

### 34.1 The governed flow, stated as a boundary chain

```
Customer Sources
  -> Transformation / Mapping                     (authoring plane)
  -> Published definition_versions                (immutable)
  -> Governed registry and configuration state
  -> Published transformation, which EMITS the plant
     relationship model. Pinned by source_definition_id
     + source_definition_version. No separate publication act
     and no independent relationship-version object.
  -> CANONICAL PLANT DATA                         <-- INPUT BOUNDARY, mutable
  ================= SNAPSHOT SEAL =================
  -> Versioned Spine / Feature / Sequence / Outcome SNAPSHOTS  (immutable)
  -> Intelligence Engines                         (training)

  and separately:

  CANONICAL PLANT DATA
  -> Prepared Serving Features + Evidence + Live model registry entry   (serving)

  and back:

  Intelligence Outputs
  -> Governed analytical datasets in Plant Data
  -> Layer A / Page Builder / Assistant
```

### 34.2 The distinction, stated so it cannot be blurred

| | Canonical Plant Data | Versioned data products |
|---|---|---|
| Role | **Source of truth for customer data** | **The direct model-training contract** |
| Mutability | Mutable. Corrected, backfilled, extended continuously | **Immutable once sealed** |
| Who reads it | Data Product Builder, Layer A, serving feature preparation | Trainers only |
| Version identity | None. It is a live state | `snapshot_id` plus content hash |
| May a model bind to it | **No** | Yes, and only this |

**Why this is not pedantry.** A model trained directly against a mutable table cannot be reproduced, because the table it was trained on no longer exists. Every claim about why an answer changed between two Mondays depends on this seal.

### 34.3 DP-28 `dataset_snapshot`

> **BOUND TO `ppiq_plant.feature_snapshots and feature_snapshot_rows`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `snapshot_id` | uuid | PK |
| `snapshot_kind` | enum | spine, feature, sequence, outcome, practice, event |
| `semantic_manifest_id` | uuid NULL FK | Pinned. Required for every new governed execution |
| `source_definition_id`, `source_definition_version` | uuid, integer | The transformation publication that emitted the relationship model. **Not a relationship-version object** |
| `feature_set_version` | int | Feature snapshots |
| `window_from_utc` / `window_to_utc` | timestamptz | The population window |
| `scope_filter` | jsonb | Tenant, site, and any declared population restriction |
| `row_count` | bigint | |
| `content_hash` | text | Over the sealed content |
| `storage_ref` | text | Physical location, replaceable |
| `sealed_at_utc` | timestamptz | **After sealing, the snapshot is read-only forever** |
| `built_from_canonical_watermark` | timestamptz | The canonical state it captured |
| `retention_class` | enum | permanent, rolling, sample_retained |
| `superseded_by_snapshot_id` | uuid | Nullable |

**Sealing rule.** A snapshot is sealed by the builder, verified by content hash, and never rewritten. A correction to canonical data produces a **new** snapshot; the old one remains so that the model trained on it remains explicable.

### 34.4 Training binding versus serving binding

| | Training | Serving |
|---|---|---|
| Binds to | `snapshot_id` set, listed in the model registry record | Prepared serving features, evidence store, live `model registry entry` |
| May scan | The full sealed snapshot | A bounded recent slice only |
| Latency | Hours to days | Seconds |
| Consistency requirement | Reproducibility | Freshness |

**Serving never reads a training snapshot and training never reads the serving slice.** They are built from the same canonical source and the same feature definitions, and gate G-38 asserts they agree on a sampled overlap. That check is what stops training-serving skew, which is the most common cause of a model that validates well and performs badly.

### 34.5 The return path

Governed intelligence outputs are projected back as **analytical datasets in Plant Data**, where Layer A, the Page Builder and the Assistant read them through the ordinary path. This is consistent with the Schema Topology contract, which places engine outputs in Plant Data because they exist because of this customer's data, and with the isolation rule that no analytical surface may display a row from outside Plant Data.

**Non-analytical operational artifacts** - model binaries, index files, registry records, gate reports, snapshot manifests, job state and supervisor proposals - are never displayed in an analytical surface. Per section 43.4 they live in Meta Data or in object storage, and the analytical role holds no grant on object storage.

---

## 35. THE RELATIONSHIP MODEL IS MANDATORY FOR EVERY CONSUMER

### 35.1 The ruling in one sentence

**No intelligence engine may compose, infer, or hardcode a join. Every engine resolves entity correspondence through the published canonical relationship publication, or it refuses.**

### 35.2 Why this is the highest-severity rule in the pack

A wrong join does not fail. It produces plausible numbers across every surface, forever, and nothing downstream can attribute the error to its cause. A private join inside one engine is worse still, because two engines then disagree and neither can be shown to be wrong.

The worked case, stated generically: two source systems each carry an identity for the same physical production entity under different names. The customer's engineer declares once, during preparation, that they are the same identity. **From that moment every genealogy walk, every cross-position correlation, every feature that spans the two systems, and every widget showing both together depends on that one declaration.** Nothing re-derives it. Nothing is permitted to.

### 35.3 The canonical relationship authority

**Chapter 2 3.15 positions it. Chapter 3 4.5.10 implements it. Layer B defines no relationship object of its own.**

| Canonical object | Holds |
|---|---|
| `ppiq_meta.plant_relationships` | Left and right entity, join type, cardinality, grain on both sides, `is_grain_converting`, `attribution_rule` NOT NULL when grain-converting, `attribution_expression`, `is_preferred_path`, `ambiguity_state`, `validation_state`, `validation_detail`, `source_definition_id`, `source_definition_version`, `effective_from_utc`, `retired_at_utc` |
| `ppiq_meta.plant_relationship_members` | Ordered composite key pairs, `member_order`, `comparison` |
| `ppiq_meta.plant_relationship_paths` | Materialised transitive paths: `hop_count`, `path_json`, `crosses_grain`, `is_preferred` |

**Publishing a transformation emits the model.** There is no separate publication act.

**Provenance pinning.** A trained model version, a snapshot, a JobRun, an evidence row and a prediction each pin `source_definition_id` and `source_definition_version` for the relationship publication in force. Gate G-36 asserts the pinned version is effective, not retired, and that its `validation_state` permits the caller's declared `purpose`.

**A relationship is deactivated, never deleted**, so a finding computed under a retired relationship stays explainable.

### 35.4 The single resolution authority

One component, `RelationshipResolver`, is the only code in Layer B that converts a declared relationship into an executable path.

| Consumer | Resolves through |
|---|---|
| Statistics and correlation (DF9) | RelationshipResolver |
| Feature engineering | RelationshipResolver |
| Model training | RelationshipResolver |
| Prediction and scoring | RelationshipResolver |
| Practice learning | RelationshipResolver |
| Remediation search | RelationshipResolver |
| Value engine | RelationshipResolver |
| Assistant retrieval | RelationshipResolver |
| Evidence assembly | RelationshipResolver |

**This is the Single Engine Implementation principle applied to joins.** One implementation, no duplicates, no private paths.

### 35.5 Refusal conditions

An analysis refuses, with terminal state `REFUSED_BY_GUARD` and a named reason, when the required relationship is:

| Condition | Reason text names |
|---|---|
| **Unpublished** | The relationship code and that it exists only as a draft |
| **Ambiguous** | The competing paths and why the resolver will not choose |
| **Invalidated** | The invalidation reason and the version that invalidated it |
| **Incompatible** | The model's pinned RMV against the current published RMV |
| **Below quality floor** | The failing IV metric and its measured value |

**The refusal names the relationship, not the data.** This is the same discipline as G-19 and G-23, applied to the join layer.

### 35.6 Provenance pinning

Every model registry entry, training run, evidence row and prediction pins: **`semantic_manifest_id`**, plus `source_definition_id` and **`source_definition_version`** for the relationship publication, `feature_set_version_id`, and the `snapshot_id` lineage. Section 17.1 is extended accordingly.

---

## 36. INTELLIGENCE BLOCKS IN THE NO-CODE ANALYSIS CANVAS

### 36.1 The premise

The customer authors an analysis by dragging, wiring, configuring and saving. The saved graph compiles to one versioned `AnalysisDefinition`. No code, no developer, no per-customer branch.

### 36.2 DP-29 `block_definition` - the block registry

> **BOUND TO `the registry-driven toolbox groups of Chapter 4 5.2.5`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Registry-driven, exactly like the statistical method registry. **A new block is a registry row plus an engine binding, never a new canvas.**

| Field | Type | Notes |
|---|---|---|
| `block_code` | text | PK |
| `block_version` | int | |
| `toolbox_group` | smallint | **1 source and output, 2 relational, 3 arithmetic and logic, 4 statistics and correlation, 5 model and feature, 6 condition and action** (Ch4 5.2.5) |
| **`engine_kind`** | enum | **statistical, learned, retrieval, orchestration, governance, projection** |
| `engine_binding` | text | DF9, MF-01..MF-07, value, scenario, or a governance evaluator |
| `input_ports` | jsonb | Ordered, each with a port type |
| `output_ports` | jsonb | Ordered, each with a port type |
| `config_schema` | jsonb | Declarative form definition, rendered by the canvas |
| `eligibility_requirements` | jsonb | Capability-profile clauses |
| `emitted_dataset_code` | text | Nullable for non-output blocks |
| `refusal_states` | enum[] | |
| `compute_class` | enum | interactive, batch_cpu, batch_gpu |
| `is_enabled` | bool | |

### 36.3 The canonical toolbox groups

**Chapter 4 5.2.5. Groups are extended by registry entry, never by a code branch.**

| Group | Contents | Surfaces |
|---|---|---|
| **1 Source and output** | Source table; output to canonical entity; output to named dataset | All |
| **2 Relational** | Join, filter, select columns, rename, group by, sort, union, distinct, limit, pivot, derived column, cast, lookup | S1, S2 |
| **3 Arithmetic, comparison and logic** | **Expression blocks, not board blocks.** They live inside the block they configure, opened by double-click, on all five surfaces without exception | All five |
| **4 Statistics and correlation** | The method catalogue of Ch4 5.5: Group A descriptive, Group B association, **Group C discipline**, Group D process and quality | **S3** |
| **5 Model and feature** | Feature engineering blocks (Ch4 5.6.2), model blocks (5.6.3), prediction and recommendation blocks (5.6.4), practice authoring blocks (5.6.4a) | **S4** |
| **6 Condition and action** | Threshold condition, range condition, routing-deviation condition, emit info, emit warning, emit error | S5 |

**Group C discipline blocks are always applied and never user-selectable.** False-discovery control, effect-size ranking, stratification, bootstrap stability and confounder check run on every association result, and their outputs are stored with the finding as data. A user may inspect them; a user may not switch them off, and a validator refusal states so.

**Control flow does not belong on any board.** `FOR` and `WHILE` are orchestration. A saved definition describes what its output is; how often it runs and over what window belongs to the job that carries it.

### 36.4 Naming discipline, ruled

**Not every intelligence block is an ML model, and the registry field `engine_kind` enforces the distinction in data rather than in documentation.**

| Block | `engine_kind` | What it actually is |
|---|---|---|
| Correlation | `statistical` | A method from the DF9 registry. No model, no training |
| Statistics | `statistical` | Same |
| Deep analysis | `orchestration` | **Composes several engines and returns a combined evidence set.** It is a plan, not an algorithm |
| Anomaly | `learned` | Consumes MF-03 |
| Similarity / fingerprint | `retrieval` | Consumes the encoder and the index. Retrieval, not inference |
| Supervised prediction | `learned` | Consumes a trained MF-04 model from the active model version |
| Practice learning | `statistical` with a learned component | MF-07 |
| Remediation search | `governance` plus `statistical` | The nine-check evaluation over candidates |
| Scenario | `learned` | Inference on a pinned model |
| Value | `projection` | Arithmetic over a declared assumption contract. **Never a model** |

Calling the value block a model would be the clearest possible way to destroy trust in it, because a customer would then ask what it was trained on and the answer is nothing.

### 36.5 Port typing and validation

Ports carry types: `Population`, `Grain`, `Outcome`, `FeatureSet`, `Window`, `ContextScope`, `RelationshipPath`, `ModelRef`, `Finding`, `Prediction`, `Contributor`, `Similarity`, `Anomaly`, `Practice`, `Envelope`, `Value`, `Evidence`.

**Illegal wiring is refused at drag time with a written sentence**, matching the discipline already shipped on the preparation canvas. Five refusal classes at authoring time: type mismatch, missing required scope, relationship not published for the declared path, eligibility not met by the capability profile, and aggregate used outside an aggregation context.

### 36.6 DP-30 `analysis_definition`

> **BOUND TO `ppiq_meta.definition_store + definition_versions + analysis_details`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `analysis_definition_id` | uuid | PK |
| `analysis_definition_version` | int | Immutable once published |
| `status` | enum | draft, validated, published, superseded, rolled_back |
| `graph` | jsonb | Nodes, edges, per-node config |
| `block_versions` | jsonb | **Every block code with its pinned version** |
| `required_definition_version_ids` | jsonb | The published definitions this graph depends on |
| `required_source_definition_version` | uuid | |
| `required_capabilities` | jsonb | Union of block eligibility clauses |
| `emitted_dataset_codes` | text[] | |
| `compiled_plan` | jsonb | Topologically ordered execution plan |
| `content_hash` | text | |

**Compilation is server-side and deterministic**, on the same principle as SQL compilation on the preparation canvas. The client never composes the plan.

### 36.7 Genericity constraint

No block implementation may contain a customer identifier, a source table name, or an industry noun. A block reads its scope from its input ports and its semantics from the semantic model. The Semantic Wall test of section 2.2 extends to the block tree.

---

## 37. JOB DEFINITION, JOB RUN, AND THE MODEL-COPY DISTINCTION

### 37.1 The ruling

**A JobRun is an execution context. It is not a copy of a model.**

Eleven concurrent runs are eleven identities, eleven progress states, eleven lineages, and **one** loaded artifact per distinct model in the active model version.

### 37.2 DP-31 `job_definition`

> **BOUND TO `the job definition contract of Chapter 4 5.3`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type |
|---|---|
| `job_definition_id` | uuid PK |
| `analysis_definition_id` / `analysis_definition_version` | |
| `job_kind` | enum: analysis, training, scoring, evaluation, supervisor |
| `schedule_kind` | enum: manual, cron, event, micro_batch |
| `schedule_spec` | text |
| `scope` | jsonb: tenant, site, population filter, window policy |
| `priority_class` | enum: interactive, standard, background |
| `pool_class` | enum: import, projection, analysis, ml, report |
| `resource_hint` | jsonb |
| `enabled` | bool |

### 37.3 DP-32 `job_run`

> **BOUND TO `the run tables of Chapter 3 4.5.12 (compute_runs, prediction_runs, model_training_runs, practice_learning_runs, feature_refresh_runs)`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `run_id` | uuid | PK |
| `job_definition_id` | uuid | |
| `tenant_id` / `site_id` | | |
| `analysis_definition_version` | int | **Pinned at admission, not at execution** |
| `semantic_manifest_id` | uuid | Pinned at admission. Required for every new governed execution |
| `snapshot_ids` | uuid[] | Training and analysis runs |
| `model_version` | text | Scoring and scenario runs |
| `run_scope` / `filters` / `window` | jsonb | |
| `state` | enum | queued, admitted, running, paused, cancelling, cancelled, succeeded, failed, refused |
| `progress` | jsonb | Stage, percent, rows processed, checkpoint token |
| `cancellation_requested_at` | timestamptz | |
| `retry_of_run_id` | uuid | Nullable |
| `resource_accounting` | jsonb | CPU seconds, peak RAM, GPU seconds, rows scanned |
| `output_evidence_ids` / `output_dataset_refs` | | Lineage |
| `refusal_reason` | text | Populated when state is refused |

**Every field above exists because a customer will ask about it while a job is running.** A run without independent progress and cancellation is a job the customer cannot manage.

### 37.4 The model instance pool

The distinction the requirement demands, made mechanical.

| Concept | Cardinality |
|---|---|
| Analysis definitions | Many. Authoring artifacts, cost nothing at rest |
| Job definitions | Many |
| Concurrent JobRuns | Bounded by admission control, section 38 |
| Distinct model artifacts in the active model version | Bounded by the model-count governor, section 6.7 |
| **Loaded model instances in memory** | **Bounded by a reference-counted replica pool, not by run count** |

The serving runtime keys loaded instances by `(model_version, model_code)`. Runs attach to an existing instance. Replica count is a **measured infrastructure decision** driven by throughput and latency, per section 14, and is invisible to the authoring surface.

**A customer authoring two hundred analyses has not created two hundred models.** Gate G-40 asserts that loaded-instance count is a function of the active model set and measured replica policy, never of run count.

### 37.5 Training runs are categorically separate

Training runs produce **candidate** artifacts in the learning plane. They never attach to the active model version instance pool, and no serving or scoring run may enter a training execution path. This is the Serving Wall of section 2.3 expressed in the job model, and gate G-41 asserts it.

---

## 38. CONCURRENCY AND RESOURCE GOVERNANCE

**Canonical: Ch4 5.3.2. Layer B declares weights and constraints to the existing weighted job pool. It does not introduce a scheduler.**

### 38.1 Classes, lanes and admission

**Six logical job classes, unchanged. The `ml` class resolves to three physical lanes** (amendment C4-2).

```
CLASS         LANE                 CONCURRENCY  CAPACITY  PRE-EMPT  ADMITS
import        -                       B-01        B-01      yes     import, backfill
projection    -                       B-01        B-01      yes     canonical projection,
                                                                    spine, feature refresh,
                                                                    snapshot sealing
analysis      -                       B-01        B-01      yes     statistics, correlation,
                                                                    practice, evidence
ml            ml.training             B-01        B-01     *YES*    encoder + supervised
                                                                    training, calibration,
                                                                    SHAP batch, index build
ml            ml.batch_scoring        B-01        B-01      yes     scheduled scoring,
                                                                    backfill, rescore
ml            ml.online_scoring       B-01     RESERVED     *NO*    event + micro_batch
                                                            B-02    scoring only
report        -                       B-01        B-01      yes     report generation, export
interactive   -                    reserved    reserved     no      never batch
```

**Admission requires both predicates:**

```
admit  iff  running_count < max_concurrency
       AND  sum(compute_weight of running) + compute_weight(candidate) <= resource_capacity
```

`max_concurrency` is how many runs may be in flight. `resource_capacity` is how much scarce resource exists. `compute_weight` is how much one run consumes. **One number never expresses two quantities.** G-50 asserts that for every lane the heaviest declared job is admissible; a configuration where a declared job can never be admitted fails the gate.

**The online reservation is never available to `ml.training` or `ml.batch_scoring` admission**, on the same principle as the `interactive` reservation.

**`ml.online_scoring` carries operational event and micro-batch scoring and its required serving functions only.** Batch, backfill and rescore work runs on batch and training-class capacity. Where a deployment physically shares hardware, online capacity is still hard-reserved and B-02 must prove the actionable-latency target holds while training and batch work are saturated.

**Warm models.** Artifacts for every active serving identity are resident, reference-counted, with a declared eviction policy. A newly activated model is warmed before it serves, and first-score-after-activation latency is bounded and measured.

**Pre-emption.** `ml.training` runs are checkpointed per stage (section 7.3). A training run yields at its next checkpoint when a reserved lane needs capacity and resumes from it. **Nothing is lost except elapsed time, which is the correct trade against an expiring prediction.** Where the runtime cannot pre-empt, the lane falls back to admission-time reservation only, and this is recorded rather than assumed.

### 38.2 Admission

Each job definition carries a **`compute_weight`**, default 1. Admission uses **both predicates of section 38.1**: a run count against `max_concurrency`, and a weight sum against `resource_capacity`. The weight is edited behind a confirmation stating the resulting utilisation, and **G-50 refuses a configuration in which a declared job could never be admitted**.

**Connection discipline.** All pools sit behind a connection pooler, and job workers use a **separate pooler identity** from the interactive path, so batch work physically cannot exhaust the connections the interface needs.

### 38.3 Layer B workload mapped onto the pools

| Layer B workload | Pool |
|---|---|
| Ingest of new and changed source data | `import` |
| Canonical projection, spine build, feature refresh, sequence build, outcome build, snapshot sealing | `projection` |
| Statistical engine DF9, practice engine, envelope and effect computation, capability profiling, drift metrics, evidence materialisation | `analysis` |
| Encoder training, encoding, index build, novelty fit, supervised training, calibration, importance computation, champion evaluation | **`ml.training`** |
| Scheduled scoring, backfill scoring, rescore after activation | **`ml.batch_scoring`** |
| **Event and micro-batch operational scoring** | **`ml.online_scoring`, hard-reserved** |
| Evidence and dataset materialisation for delivery, scheduled report generation | `report` |
| Tier 1 reads, tier 2 bounded analysis, scenario evaluation, Assistant tool calls | `interactive`, reserved |

### 38.4 Run policies

| Policy | Behaviour | Applied to |
|---|---|---|
| **Skip if running** | The tick is dropped and recorded as skipped with the reason | Import, feature refresh |
| **Latest only** | Queued duplicates collapse to the newest request | Alert evaluation, **scoring** |
| **Queue** | Runs accumulate in order, bounded by queue depth | Reports |
| **Reject** | Refused with a named error | User-triggered runs when the pool is saturated |

**A skipped tick is visible in the monitor with its reason.** Silent skipping is how a plant discovers three weeks later that a job never ran.

### 38.5 Degradation

The canonical five-level ladder governs: normal, elevated, high, critical, protective. Report and ML pools reduce first; non-critical cadences stretch; at critical only import and interactive are admitted; at protective new user-triggered runs are refused with a named error and an estimated wait. **Every level is announced.** A product that quietly stops doing analysis is worse than one that says it is behind.

### 38.6 Relationship to the weekly window

The weekly sequence of section 8 executes as jobs in the `projection`, `analysis` and `ml` pools under this admission control. The abort ladder of section 8.2 is the degradation policy for that specific job family and operates above admission, never instead of it.

## 39. THE END-TO-END CANVAS TO OUTPUT CONTRACT

### 39.1 The full flow, with the owning component and the failure mode at each step

| # | Step | Owner | Fails as |
|---|---|---|---|
| 1 | Drag intelligence blocks onto the canvas | Analysis canvas | Block absent from registry or disabled |
| 2 | Wire them | Canvas | **Refusal at drag time with a written sentence**, five classes |
| 3 | Validate semantic and relationship dependencies | Canvas plus RelationshipResolver | Named refusal identifying the unpublished or ambiguous relationship |
| 4 | Compile the graph | Server-side compiler | Cycle, unreachable output, unbound required port |
| 5 | Save `AnalysisDefinition` version | Definition store | Immutable publish, draft otherwise |
| 6 | Readiness check against the capability profile | Capability profiler | `MODEL_NOT_READY` naming the failed clause and its measured value |
| 7 | Create or schedule a `JobDefinition` | Job service | Invalid schedule for the declared scoring mode, gate G-26 |
| 8 | Scheduler admits a `JobRun` | Weighted job pool | Queued with visible position, or refused on quota |
| 9 | Engine resolves pinned versions | Engine host | Refusal on version incompatibility, gate G-36 |
| 10 | Execute | Engine plus pools | Checkpointed, cancellable, retryable |
| 11 | Persist governed result datasets | Evidence materialiser | Refusal rows persisted like findings |
| 12 | Expose to Page Builder, charts and Assistant | Dataset catalogue plus tools | Ordinary governed datasets, no ML-specific path |

### 39.2 The version pin, carried the whole way

An answer on a chart in step 12 resolves back through: dataset row, evidence id, run id, analysis definition version, block versions, `model_registry_id` and `model_version`, feature set version, snapshot id, `source_definition_version`, semantic model version, and the canonical watermark.

**That chain is the product's honesty claim made mechanical.** Any link missing makes "why did this number change" unanswerable.

### 39.3 What the customer never touches

Physical tables, joins, SQL for relationships, model selection, hyperparameters, training schedules, model artifacts, replica counts, pool weights.

### 39.4 What the customer fully controls

Which blocks, wired how, over which population, at which grain, in which window, against which outcome, with which context, on what schedule, at what priority, and whether to accept a recommendation.

---

## 40. PLATFORM INTEGRATION GATES

### 40.1 Additional gates G-36 to G-46

| ID | Gate | When | Blocking |
|---|---|---|---|
| **G-36** | Relationship currency: the pinned `source_definition_version` is effective and not retired, and its `validation_state` permits the caller's `purpose` | Activation, run admission | **Yes** |
| **G-37** | Snapshot immutability: a sealed snapshot's content hash still matches its content | Every training run start | **Yes** |
| **G-38** | **Training-serving parity**: features computed by the serving path match the snapshot on a sampled overlap, within declared tolerance | Activation, weekly | **Yes** |
| **G-39** | **No private join**: no engine, block or query path outside `RelationshipResolver` composes an entity correspondence | Build-time architecture test, falsified once | **Yes** |
| **G-40** | Model instance economy: loaded instance count is a function of the active model set and replica policy, never of JobRun count | Serving, continuous | **Yes** |
| **G-41** | Job separation: no scoring, scenario or analysis run can enter a training execution path | Build-time plus runtime | **Yes** |
| **G-42** | Reserved interactive capacity is enforced and cannot be consumed by analysis or training admission | Admission, continuous | **Yes** |
| **G-43** | Block genericity: no block implementation contains a customer identifier, source table name or industry noun | Build-time | **Yes** |
| **G-44** | Definition pinning: every JobRun pins the definition version, block versions, semantic model version, `source_definition_version`, and its snapshot or model version at admission | Run admission | **Yes** |
| **G-45** | Authoring-time refusal: illegal wiring is refused on the canvas with a written sentence, all five classes exercised | Canvas, falsified once | **Yes** |
| **G-46** | Lineage completeness: every displayed intelligence value resolves to the full chain of section 39.2 with no missing link | Activation, sampled | **Yes** |

**G-39 is the single most important gate in this group.** A private join produces plausible numbers forever and is unattributable after the fact. It must be a build-time architecture test, and it must be falsified against a deliberately introduced private join before it is trusted.

### 40.1a Target architecture gates G-48 to G-55

| ID | Gate | When | Blocking |
|---|---|---|---|
| **G-48** | **Training reads no live feature state.** No training or encoding code path queries `feature_store`; training input resolves only through a sealed snapshot artifact. **The snapshot materialiser is exempt by definition** and is the only component permitted to read `feature_store` for sealing | Build-time and runtime | **Yes** |
| **G-49** | **Lane isolation.** No `ml.online_scoring` process imports a trainer module; the online reservation cannot be consumed by `ml.training` or `ml.batch_scoring` admission | Build-time and admission | **Yes** |
| **G-50** | **Admission predicate satisfiable.** For every lane, `max(compute_weight of any admissible job) <= resource_capacity`, and `max_concurrency >= 1`. A configuration where a declared job can never be admitted fails | Configuration validation | **Yes** |
| **G-51** | **ANN recall floor.** Every index build measures recall@k against exact Flat on the representative sample; a build below `recall_floor` does not become the served index | Index build | **Yes** |
| **G-52** | **Evidence budget integrity.** No packed evidence item lacks a resolvable handle; truncation is recorded and disclosed | Serving | **Yes** |
| **G-53** | **Claim-class integrity in language.** No answer phrases a lower claim class as a higher one. Measured by Q-06 against a fixed adversarial set | Release | **Yes** |
| **G-54** | **Governed-model-only learned output.** No feature, score, statistic or value derives from free-form or model-generated output | Build-time and training | **Yes** |
| **G-55** | **Manifest immutability and coverage.** A `semantic_manifests` row is never updated; identical content within a tenant never creates a second row; **every new governed AI/ML execution resolves a manifest**, legacy records excepted | Trigger plus run admission | **Yes** |

**Total inventory: G-01 to G-55.**

### 40.2 Open items

All remaining open items are **measurement decisions with a canonical home**, not architecture gaps:

| ID | Measurement | Written into |
|---|---|---|
| OD-05, OD-06 | Encoder and supervised eligibility thresholds | `model_details.acceptance_floor`, the gate minimums of Ch4 5.6.3 |
| OD-11 | Hardware sizing benchmark | The capacity model of Ch4 5.3.3 |
| OD-12, OD-21 | Sequence and snapshot retention | `feature_snapshots.retention_until_utc`, per-stage retention policy |
| OD-25 | Reserved interactive capacity fraction | The `interactive` reservation of Ch4 5.3.2 |

Each is a number to be measured and then recorded in an existing canonical field. **None requires a design decision.**

---
---

# PART SIX - DIRECT SOURCE BINDING

**The chapters were read. This part governs where it differs from anything above it.**

---

## 41. SOURCE RECONCILIATION RECORD

### 41.1 Documents read

| Document | Size | Read |
|---|---|---|
| `PPIQ_Chapter1_Marketing_and_Sales.md` | 43 KB | Referenced |
| `PPIQ_Chapter2_Technical_Overview.md` | 74 KB | **Yes** |
| `PPIQ_Chapter3_General_Technical_Function_Description.md` | 324 KB | **Yes** |
| `PPIQ_Chapter4_Specific_Technical_Function_Description.md` | 186 KB | **Yes** |
| `PPIQ_Chapter5_Tutorial_User_Journey.md` | 70 KB | Referenced |
| `PPIQ_Chapter6_Infrastructure_Website_Administration.md` | 154 KB | Referenced |

### 41.2 Sections read directly and used

| Section | Content | Bound in |
|---|---|---|
| **Ch2 authority statement** | Chapter 2 is the naming, structure and positioning authority for seven things including the relationship model and its sixteen consumers | 50 |
| **Ch2 3.15.1 to 3.15.5** | The permanent plant relationship model: declared once, validated once, published once; the seven declarations; the record properties; the sixteen consumers, exhaustive and binding; the reviewer consequences | 50 |
| **Ch3 4.5.10** | `plant_relationships`, `plant_relationship_members`, `plant_relationship_paths` with full DDL; ambiguity refuses; unproven blocks automation not exploration; grain conversion requires attribution; retirement preserves history; **one resolver**; `GET /api/relationships/resolve?from=&to=&purpose=` | 50 |
| **Ch3 4.5.11** | `definition_store`, `definition_versions`, `definition_dependencies`, ten detail tables, export artifacts, one lifecycle | 51.2 |
| **Ch3 4.5.12** | The intelligence tables, full DDL: `compute_runs`, `correlation_results`, `feature_store`, `feature_snapshots`, `model_registry`, `model_training_runs`, `model_drift_observations`, `prediction_runs`, `predictions`, `prediction_drivers`, `prediction_comparables`, `prediction_current`, `practice_signatures`, `practice_statistics`, `practice_learning_runs`, `practice_drift_observations`, `remediation_candidates`, `prediction_remediation_evaluations`, `forbidden_combinations`, `prediction_actions`, `prediction_evaluations`, `remediation_effectiveness`, `suggestions`, `suggestion_decisions`, `feedback_records`, `value_impacts`, `value_realization_ledger`, `assistant_chunks`, `supervisor_proposals`, `supervisor_shadow_runs`, `supervisor_provenance` | 51 |
| **Ch3 4.5.12a** | `can_accept` as the complete acceptance authority, seven conditions, client never re-derives, `RM10` on the write path | 52.3 |
| **Ch3 4.5.12b** | `remediation_escalations` | 52.4 |
| **Ch3 4.5.13** | Intelligence as a bindable source: `registry_dimensions` with the three controllability columns, `registry_measures`, `registry_intelligence_sources`, the three design obligations | 53 |
| **Ch3 DF7 payload** | `columns + rows + warnings`; `sourceKind: canonical \| intelligence`; `intelligenceSource`; `columnRoles` | 53 |
| **Ch4 5.2.1 to 5.2.6** | One shell, five purposes S1 to S5; two modes; four regions; the schema table bar; **toolbox Groups 1 to 6** | 51.2 |
| **Ch4 5.5.1 to 5.5.6** | The statistics catalogue: Group A descriptive, Group B association, **Group C discipline, always applied and never optional**, Group D process and quality; the S3 validator rules | 51.1 |
| **Ch4 5.6.1 to 5.6.7a** | Position; Group E feature blocks; Group F model blocks; Group G prediction and recommendation; G2 practice blocks; the practice-learning engine; the predict-then-remediate pipeline; **the nine-check gate**; model governance; the serving path; the fallback policy | 52 |
| **Ch4 5.3.2** | The nine-mechanism defence stack; **the six pools with parallelism**; `compute_weight`; connection discipline; the five-level degradation ladder | 55 |
| **Ch4 5.4.9, 1539-1580** | The Supervisor: most constrained component; honesty machinery outside its write scope by absent permission; shadow execution | 51.3 |
| **Ch4 5.8.1 to 5.8.8** | Scenario simulation; alert routing; prediction explainability; the feedback loop; internal benchmarking; **unstructured text evidence, future extension with interfaces designed**; **inspection images, same**; actionable prediction latency | 54, 56.2 |

---

## 42. THE RELATIONSHIP MODEL - FINAL AND BINDING

### 42.1 The canonical authority

**Chapter 2 3.15 positions it. Chapter 3 4.5.10 implements it.** Layer B defines no relationship version object of its own.

| Canonical object | Holds |
|---|---|
| `ppiq_meta.plant_relationships` | One declared relationship: left and right entity, join type, cardinality, grain on both sides, `is_grain_converting` generated, `attribution_rule` NOT NULL when grain-converting, `attribution_expression`, `is_preferred_path`, `ambiguity_state`, `validation_state`, `validation_detail`, `source_definition_id`, `source_definition_version`, `effective_from_utc`, `retired_at_utc` |
| `ppiq_meta.plant_relationship_members` | Ordered composite key pairs with `member_order` and `comparison` |
| `ppiq_meta.plant_relationship_paths` | Materialised transitive paths with `hop_count`, `path_json`, `crosses_grain`, `is_preferred` |

**Publishing a transformation emits the model.** Layer B pins `source_definition_id` and `source_definition_version`.

### 42.2 The four behavioural rules Layer B must obey

| Rule | Effect on Layer B |
|---|---|
| **Ambiguity refuses rather than guesses** | Two unretired paths with no preferred path returns `RL01` naming both. No engine picks one |
| **Unproven blocks automation, not exploration** | `validation_state = unproven` permits workspace exploration and **refuses statistical, feature, model, practice and prediction use** with `RL02`. This is stricter than my section 35.5 and it governs |
| **Grain conversion requires attribution** | Weights per child sum to exactly 1.0, enforced by CHECK and by `TR09` at publish. The genealogy-attributed correlation block of Ch4 5.5.3 refuses otherwise |
| **One resolver** | A single path-resolution service is the only code that reads these tables. Every consumer calls `GET /api/relationships/resolve?from=&to=&purpose=`, where `purpose` is one of the sixteen consumers |

**`purpose` is the mechanism I did not have.** An unproven relationship is usable by `explore` and not by `train`. The resolver enforces the distinction; the caller does not.

### 42.3 The sixteen consumers, binding and exhaustive

Canonical projection, registry generation, page and widget query compiler, associative filtering, drill-down, drill-through, genealogy, statistical analysis, correlation, feature engineering, model training, model scoring, practice learning, prediction and remediation search, value calculation, Assistant retrieval and tools. Plus evidence and provenance in reverse.

**A capability that re-derives a join instead of reading the model is a defect.** G-39 stands, now testable against the named service.

---

## 43. CANONICAL BINDING REGISTER

Every semantic concept in this pack, its canonical object, and what the pack adds.

### 43.1 Statistics and correlation

| Pack concept | Canonical | Pack adds | Why no second authority |
|---|---|---|---|
| DF9 statistical engine, MF-06 | Ch4 5.5 Groups A to D as registry block rows; `ppiq_plant.compute_runs`; `ppiq_plant.correlation_results` | Nothing. Section 21's execution order is a restatement of Group C discipline | The blocks are registry rows; the pack adds no block |
| DP-15 method registry | The Group A to D catalogue rows | Nothing | Withdrawn as an object |
| My "FDR is structural" | **Group C discipline blocks are always applied and never user-selectable**; `correlation_results.q_value` NOT NULL in practice | Nothing | Canonical is stricter than my design |
| My effect-size ranking | **Effect-size ranking refuses to order by p-value** | Nothing | Canonical already states it |

**Two canonical columns I did not have:** `correlation_results.framing_text NOT NULL` and `llm_participated`, stored as data so the framing survives an export, a screenshot and a report. Adopted.

### 43.2 Authoring and definitions

| Pack concept | Canonical | Status |
|---|---|---|
| Analysis canvas | **One shell, five purposes**: S1 data preparation, S2 widget and page binding, S3 analysis authoring, S4 model authoring, S5 plant data log | My two-purpose framing withdrawn |
| Block categories | **Toolbox Groups 1 to 6**: source and output; relational; arithmetic, comparison and logic as expression blocks; statistics and correlation (S3); model and feature (S4); condition and action (S5) | My four invented categories withdrawn |
| DP-30 analysis definition | `definition_store` + `definition_versions` + `analysis_details` | Bound |
| DP-33 model definition | `definition_store` + `definition_versions` + `model_details` | Bound. **The object exists; it is a `definition_kind`, not a table** |
| Feature set | `definition_store` + `feature_set_details` | Bound |
| Practice definition | `definition_store` + `practice_details` | Bound |
| Scenario definition | `definition_store` + `scenario_details` | Bound |
| DP-29 block registry | Toolbox groups extended by registry entry, never by a code branch | Bound as the mechanism, not a new table |
| Definition lifecycle | `draft -> validated -> published -> paused_by_drift \| rolled_back -> superseded`, immutable published rows enforced by trigger | Adopted; my lifecycle enum replaced |

**`definition_dependencies` with a cycle-refusing trigger** is canonical and I did not have it. It is what makes the impact preview of Ch2 3.15.5 possible.

### 43.3 Models, predictions, remediation, decisions

| Pack concept | Canonical |
|---|---|
| Feature store DP-2 | `ppiq_plant.feature_store` (`features jsonb`, UNIQUE `(material_unit_id, feature_set_version_id)`, `lineage_hash`, `is_dirty`), plus `feature_refresh_watermarks`, `feature_refresh_runs` |
| Snapshots DP-28 | `feature_snapshots` + `feature_snapshot_rows`, immutable, with `storage_uri` |
| model registry entry | **`model_registry`.** Serving identity `(tenant_id, model_code, outcome_code, grain_code)` plus `model_version`; `status` and `serving_role` independent axes |
| Training run | `model_training_runs`, with **CHECK `overlap_rows = 0`** |
| Drift | `model_drift_observations` |
| Scoring run | `prediction_runs`, with `trigger_kind` and `scoring_mode` as separate columns |
| Predictions | `predictions`, with `actionable_deadline_utc`, `deadline_basis`, `met_actionable_deadline`, `delivery_latency_seconds NOT NULL` |
| Contributors DP-2 of section 10 | `prediction_drivers` |
| Similarity | `prediction_comparables` |
| Operational read model DP-18 | `prediction_current`, PK `(tenant_id, material_unit_id, outcome_code)` |
| Practice DP-16, DP-17 | `practice_signatures`, `practice_statistics`, `practice_learning_runs`, `practice_drift_observations` |
| Remediation template | `remediation_candidates`, CHECK `support_count >= 20` |
| Remediation gate DP-19 | `prediction_remediation_evaluations` |
| Safety rules SM-13 | `ppiq_plant.forbidden_combinations`, imported or customer-authored, never shipped |
| Decisions DP-20, actions DP-21 | `prediction_actions`, append-only |
| Escalation | `remediation_escalations` |
| Evaluation DP-22 | `prediction_evaluations`, CHECK `observed_from = 'canonical'` |
| Effectiveness DP-23 | `remediation_effectiveness` |
| Feedback DP-24 | `feedback_records`, with `quality_state` gating what reaches the Supervisor |
| Value DP-25, DP-26 | `value_impacts`, `value_realization_ledger` |
| Supervisor DP-27 | `supervisor_proposals`, `supervisor_shadow_runs`, `supervisor_provenance` |
| Assistant retrieval | `assistant_chunks` with `role_scope` |
| Scenario | `ppiq_plant.scenario_runs` |

### 43.4 Storage placement

The three-schema law stands. No fourth application schema.

| Content | Location |
|---|---|
| Customer-derived analytical and intelligence datasets: predictions, contributors, similarity, anomalies, envelopes, findings, readiness | **Plant Data** |
| Operational and control-plane metadata belonging in the application database: registry records, model registry records, snapshot manifests, job definitions, job runs, gate reports, supervisor proposals, block definitions, analysis definitions | **Meta Data** |
| Pre-semantic, source-shaped data | **Dump Store** |
| Model binaries, encoder checkpoints, vector index files, large binary artifacts | **Object / artifact storage** |

**Two consequences that must be built, not assumed:**

1. **Analytical surfaces do not read operational artifact storage.** The analytical role holds no grant on it, and the isolation architecture test extends to cover it.
2. Where operational metadata must become analytically visible, for example a readiness view or a job history chart, it is published as a **governed Plant Data read model or projection**. It is never read across the boundary. Gate G-47 asserts this.

**Placement of the thirty-two data products:**

| Products | Location |
|---|---|
| DP-1 spine, DP-2 features, DP-3 sequences, DP-4 outcomes, DP-6 predictions, DP-7 evidence, DP-16 practice signatures, DP-17 practice matches, DP-18 prediction current, DP-19 remediation eligibility, DP-20 decisions, DP-21 actions, DP-22 evaluations, DP-23 effectiveness, DP-25 value impact, DP-26 value ledger | **Plant Data** |
| DP-5a embedding rows, DP-5b index manifests, DP-15 method registry, DP-24 feedback, DP-27 supervisor proposals, DP-28 snapshot manifests, DP-29 block definitions, DP-30 analysis definitions, DP-31 job definitions, DP-32 job runs | **Meta Data** |
| Encoder checkpoints, index binary files, model artifacts, snapshot Parquet content | **Object / artifact storage** |

**DP-5a is the one judgement call in that table.** Embedding vectors are customer-derived and could sit in Plant Data, but they are not analytically displayable and are large. Placing the **rows** in Meta Data with the **vectors** in artifact storage keeps analytical surfaces clean. If Chapter 3 rules otherwise, Chapter 3 wins.

### 43.5 What the pack legitimately adds

Only implementation-neutral explanatory architecture, none of it a second authority:

| Addition | Why it is not a competing authority |
|---|---|
| The Semantic Wall and Serving Wall as named enforcement layers | Restates Ch3 grants and Ch4 pool isolation as testable rules |
| The capability profile and the intelligence ladder | An explanatory frame over canonical readiness gates. Emits no persistent object |
| The three-schedule budget tables and the abort ladder | Sits above Ch4 5.3.2 mechanism 9. The canonical degradation ladder governs; my budgets are planning figures |
| The fifty-five gates | Test specifications over canonical constraints. Where a canonical CHECK exists it is the stronger mechanism and the gate asserts it |
| The genericity proof | Explanatory only |
| The dependency order and sizing framework | Planning, not architecture |

---

## 44. BEHAVIOURAL CORRECTIONS APPLIED FROM THE CHAPTER TEXT

Five material behavioural differences found. **In every case the chapter governs and the pack is corrected.**

### 44.1 The remediation gate outcome mapping was wrong

My section 24.2 mapped outcomes to check numbers incorrectly. The canonical mapping, from Ch4 5.6.4d:

| Outcome | Canonical condition |
|---|---|
| **Actionable** | All nine pass |
| **Evidence only** | **Checks 5 to 9 pass, but 1 to 4 fail for this unit.** Shown in drill-down as an observed historical difference, never styled as a recommendation |
| **Suppressed** | **Check 4 fails on a safety constraint.** Not shown at all; recorded on the run with `RM04` so the suppression is auditable |
| **Exploratory** | **Checks 1 to 6 pass, 7 or 8 fail.** Shown behind an explicit disclosure. No accept action, at any tier, for any role |

**The decision boundary is wider than I stated.** Accept, Reject **and** Defer all exist only where `can_accept` is true. `evidence_only`, `exploratory` and `suppressed` carry no decision control of any kind and are **outside the decision record entirely**, because rejecting or deferring an observation would enter it into the effectiveness and feedback statistics as though it had been offered as a recommendation.

### 44.2 The two-table separation of template and evaluation

`remediation_candidates` is the **global historical template**, computed once per condition. `prediction_remediation_evaluations` is the **per-prediction gate result**. Storing eligibility on the template would be wrong, because the same template is actionable for a unit two stages away and not for one that has passed the stage.

**Five checks are properties of history** and are evaluated at template generation; a template failing them is never created. **Four are situational** and cannot be. My DP-19 conflated the two.

### 44.3 `can_accept` has seven conditions, not one

It is not a synonym for `eligibility_state = 'actionable'`. It additionally requires the stage not passed, the deadline not elapsed, the prediction still open, no safety constraint invalidated since evaluation, the model not in `review` or `retired`, and the caller's entitlement and role. **The client renders the affordance from `can_accept` alone**; `can_accept_blockers` explains only. A UI that additionally tests the deadline has created a second authorisation rule.

### 44.4 `remediation_escalations` is an object I did not have

The record produced when a non-actionable candidate is escalated for engineering investigation. **It is a record, never a decision.** It creates no `prediction_actions` row, contributes to no effectiveness row, and is excluded from `feedback_records`. Only `evidence_only` and `exploratory` can be escalated. `promoted_to_actionable` is the one resolution that changes product behaviour and is audited as a governed change.

### 44.5 Practice enums differ from mine

`practice_statistics.sensitivity_state` is **`stable`, `fragile`, `unstable`, `not_tested`** - four values, not three. `not_tested` is the explicit state for an unevaluated band and **is treated as `fragile` for remediation conversion**. `state` is `benchmark`, `observed_unproven`, `failure_associated`. `backoff_rule` has six values: `exact`, `widened_tolerance`, `coarsened_dimensions`, `sequence_generalisation`, `context_widening`, `weighted_similarity`.

Canonical CHECKs the pack must not restate differently: `state <> 'benchmark' OR sensitivity_state = 'stable'`; `state <> 'benchmark' OR (support_count >= 20 AND sensitivity_state <> 'unstable')`; `similarity_level = 0 OR relaxed_support_count IS NOT NULL`.

### 44.6 Value: canonical permits a point estimate

`value_impacts` carries `lower_bound`, `upper_bound` **and `point_estimate`**, with `basis_status` in `Sufficient` or `InsufficientBasis` and CHECK `basis_status = 'InsufficientBasis' OR (lower_bound IS NOT NULL AND upper_bound IS NOT NULL)`. **My rule that no point estimate is ever emitted is stricter than canonical and is withdrawn as a rule.** The binding constraint is that bounds are mandatory when the basis is sufficient. `currency char(3) DEFAULT 'EUR'` is canonical and is a per-row column, so the genericity concern is already handled.

---

## 45. THE BINDABLE INTELLIGENCE CONTRACT - FINAL

`ppiq_meta.registry_intelligence_sources` is the declaration that makes an intelligence table bindable: `source_code`, `physical_relation`, `grain`, `entity_link_column`, `link_entity`, `default_time_column`, `minimum_role`, `minimum_tier`.

**The three canonical design obligations:**

1. **Registry derivation writes both kinds.** When an intelligence run first produces results, dimension and measure rows are derived for that source. A palette offers `risk_class` beside a canonical dimension with no special case.
2. **The widget query compiler resolves into the results area** through `plant_relationship_paths`, so a prediction and the parameter that drove it can occupy one widget. No path means the binding is refused with `WD07`.
3. **Associative state reaches intelligence.** A selection on a canonical field propagates to intelligence widgets through the same path resolution.

Widget payload: `sourceKind: "canonical" | "intelligence"`, `intelligenceSource`, `columnRoles`. Execution returns **columns, rows and warnings**. Intelligence sources are **read-only and never writable from a widget**.

### 45.4 The two analytical source classes

**Class 1 - Aggregate / fact-shaped sources.** Exact canonical measures, and aggregateable intelligence measures. These may project through `WidgetFact` into the generic aggregate executor.

**Class 2 - Native-grain rich analytical sources.** Readiness rows, findings, prediction detail, evidence, contributors, similarity neighbours, practice matches, value derivation, remediation eligibility. **These retain their governed native multi-column shape and are never flattened into a single value column.**

Both classes are ordinary analytical sources. Both use the same registry, the same authoring shell, the same selection and filter contract where applicable, the same result envelope, the same widget system and the same evidence rules.

#### A dataset may register both classes

The class is a property of the **registered source**, not of the underlying data. One dataset may expose a native-rich source and an aggregate projection, and the customer picks in the authoring shell.

| Dataset | Native-rich source | Aggregate projection |
|---|---|---|
| Prediction | Yes, prediction detail | Yes: mean probability, subject count by dimension |
| Contributors | Yes | Yes: mean absolute contribution by feature |
| Similarity | Yes | Yes: neighbour count by outcome class |
| Anomaly | Yes | Yes: mean novelty score, count by position |
| Envelope | Yes, bounds are multi-column | Yes: population and outcome rate by parameter |
| Finding and effect | Yes | Yes: finding count by status and claim class |
| Readiness | Yes | Count only |

**`registry_dimensions.is_controllable`, `controllable_at_stages`, `adjustment_range`** are the three columns check 1 of the remediation gate reads. The product never assumes a measured parameter can be changed.

---

## 46. TEXT AND IMAGE - FINAL

**State: `INTERFACE-DESIGNED / FUTURE IMPLEMENTATION`.** Ch4 5.8.6 and 5.8.7 specify both fully: persistence, access control, indexing, language handling, extraction, evidence citation, annotation, retention, permission and training separation.

**The boundary is governance, not modality.** An ungoverned output entering a score is the hazard; the modality is not. A free-form LLM summary has no training snapshot, no held-out validation, no calibration, no drift monitor and no leakage control. An authored vision model has all five, which is why Ch4 5.8.7 registers it in `model_registry` under the same activation and drift rules as any other model.

**The boundary rule, as amended (C4-6):**

> **No free-form or model-generated output may become a feature, a score, a statistic or a value.** Text and images enter a learned result only through an explicitly authored model definition carrying the full training contract: a versioned immutable snapshot, declared leakage controls, held-out validation, a `model_registry` entry, calibration and drift monitoring. Retrieval-derived and LLM-derived content is evidence only: it may corroborate a deterministic result and may never originate one.

Section 29.2 clause 4 states the two paths: **Path A evidence modality**, retrieved and cited, never a feature; and **Path B governed multimodal ML**, the full training contract above, permitted to produce a learned output with a claim class. Images register in `model_registry` with `algorithm` naming the vision family and obey the same activation, retirement, drift and `overlap_rows = 0` rules.

---

## 47. POOL TOPOLOGY - FINAL

**Six logical classes. The `ml` class resolves to three physical lanes.** Full contract in section 38.1.

| Class | Lane | Reserved | Pre-emptible | Rationale |
|---|---|---|---|---|
| `import` | - | no | yes | Network-bound, cheap on CPU |
| `projection` | - | no | yes | Write-heavy, contends on indexes |
| `analysis` | - | no | yes | Read-heavy, bounded by row caps |
| `ml` | `ml.training` | no | **yes** | Memory-heavy, long, checkpointed |
| `ml` | `ml.batch_scoring` | no | yes | Bulk, deadline-insensitive |
| `ml` | **`ml.online_scoring`** | **yes, B-02** | **no** | **Carries the actionable-deadline contract** |
| `report` | - | no | yes | Bursty, low priority |
| `interactive` | - | **yes** | no | The read path is never starved |

**Why online scoring is a separate reserved lane.** `predictions.actionable_deadline_utc` and `delivery_latency_seconds NOT NULL` exist in Chapter 3, and Chapter 4 5.8.8 makes actionable latency Core. A lane whose capacity can be consumed by training work cannot carry a latency guarantee, because the guarantee would depend on what else happened to be running.

**Scoring keeps the `latest-only` policy.** A queued scoring request for a subject is superseded by a newer one rather than both executing, because a stale prediction has no value. It now operates inside a lane that training cannot block.

Admission is by both predicates of section 38.1, using `compute_weight`. Job workers use a **separate pooler identity** from the interactive path, so batch work physically cannot exhaust interface connections. The canonical five-level degradation ladder governs, and **every level is announced**.

**Deployment.** `ppiq-worker` carries import, projection, analysis, report and `ml.batch_scoring`. `ppiq-ml-train` carries `ml.training`, GPU-capable and pre-emptible. **`ppiq-ml-online` carries `ml.online_scoring` only**, with the warm model cache, no training imports and no batch admission (amendment C6-1).

## 48. REVISION HISTORY, DISCREPANCY LEDGER AND STATUS

### 48.1 The ledger

| # | Discrepancy | Resolution |
|---|---|---|
| D-01 | Invented relationship version authority | **Withdrawn.** Bound to Ch3 4.5.10. Section 42 |
| D-02 | Two shell purposes assumed; canonical has five | **Corrected.** S1 to S5. Section 43.2 |
| D-03 | No Model Definition object | **Bound.** It is `definition_kind = 'model'` with `model_details`, not a table |
| D-04 | Invented block categories | **Withdrawn.** Toolbox Groups 1 to 6 |
| D-05 | Canvas framing | **Withdrawn.** One shell, parameterised |
| D-06 | Scoring pool unassigned | **Closed.** `ml`, parallelism 1, `latest-only` |
| D-07 | Envelope lacked `warnings` | **Corrected** |
| D-08 | Ad-hoc dimension and measure terms | **Corrected** to `sourceKind`, `intelligenceSource`, `columnRoles` |
| D-09 | Text and image marked deferred | **Reclassified** to INTERFACE-DESIGNED, and section 29 corrected for the boundary rule. Section 46 |
| D-10 | Three invented pools | **Withdrawn.** Six canonical pools with parallelism. Section 47 |
| D-11 | Thirteen Part Two products possibly duplicating canonical | **Resolved.** All bound in place. Section 43 |
| D-12 | Canonical block names unknown | **Closed.** Groups 1 to 6 and the S3 and S4 catalogues |
| D-13 | Ch2 3.15 content unknown | **Closed.** Read. Section 42 |
| **D-14** | **AD-04 claimed a physically wide feature store.** Canonical is `features jsonb` keyed by `(material_unit_id, feature_set_version_id)` with incremental refresh | **Corrected in place** |
| **D-15** | **AD-05 claimed activated together promotion.** Canonical governs per-model activation through `model_registry` with a five-part serving identity and independent `status` and `serving_role` axes | **Corrected in place.** There is no model registry entry object |
| **D-16** | **Section 24.2 mapped gate outcomes to the wrong checks** | **Corrected.** Section 44.1 |
| **D-17** | **DP-19 conflated the historical template with the per-prediction evaluation** | **Corrected.** Section 44.2 |
| **D-18** | **`can_accept` treated as a synonym for actionable.** Canonical has seven conditions | **Corrected.** Section 44.3 |
| **D-19** | **`remediation_escalations` absent from the pack** | **Added.** Section 44.4 |
| **D-20** | **Practice sensitivity enum wrong**; canonical has four values including `not_tested`, treated as fragile | **Corrected.** Section 44.5 |
| **D-21** | **Value rule stricter than canonical**; `point_estimate` is permitted beside mandatory bounds | **Withdrawn.** Section 44.6 |
| **D-22** | **Section 29 would have derived scores from text and images**, which the canonical boundary rule forbids | **Corrected.** Section 46 |
| **D-23** | **`ml` pool parallelism is 1**; section 37.4 assumed concurrent scoring | **Corrected.** Section 47 |
| **D-24** | Relationship `purpose` parameter absent from the pack; `explore` versus `train` distinction unenforced | **Adopted.** Section 42.2 |
| **D-25** | `correlation_results.framing_text` and `llm_participated` absent | **Adopted.** Section 43.1 |
| **D-26** | `definition_dependencies` cycle trigger and impact preview absent | **Adopted.** Section 43.2 |

**Twenty-six entries. All resolved. No `PENDING SOURCE` rows remain.**

### 48.2 Traceability, final

Capabilities 1 to 68 as recorded in sections 31, 40.3 and 46.3, with these final states:

| # | Capability | Final state |
|---|---|---|
| 45 | Unstructured text evidence | **INTERFACE-DESIGNED / FUTURE IMPLEMENTATION** (Ch4 5.8.6) |
| 46 | Inspection images | **INTERFACE-DESIGNED / FUTURE IMPLEMENTATION** (Ch4 5.8.7) |
| 47 | Alert routing and escalation | **DESIGNED** (Ch4 5.8.2, tables `alert_routing_rules`, `alert_deliveries`) |
| 48 | Assistant page context, permissions, glossary, egress | **DESIGNED**, owned by Ch4 5.7 |
| 67 | Authoring shell | **COVERED**, bound to Ch4 5.2 |
| 68 | Relationship model | **COVERED**, bound to Ch2 3.15 and Ch3 4.5.10 |

**All remaining open items are measurement decisions, not architecture gaps:** OD-05 and OD-06 eligibility thresholds, OD-11 hardware benchmark, OD-12 and OD-21 retention policy, OD-25 reserved capacity fraction. Each is a number to be measured, and each has a canonical home to be written into.

### 48.3 The acceptance test

*An implementation lead can decompose the complete Layer B intelligence engine into bounded work packages without inventing:*

| Must not invent | Where it is defined |
|---|---|
| **A table** | Ch3 4.5.10 to 4.5.13. Every pack data product is bound in section 43 |
| **A join** | Ch2 3.15, Ch3 4.5.10. One resolver, sixteen consumers, `purpose`-scoped |
| **A model lifecycle** | Ch3 4.5.12 `model_registry`; `status` and `serving_role`; Ch4 5.6.5 and 5.6.7a |
| **An authoring grammar** | Ch4 5.2. One shell, five purposes, six toolbox groups, `definition_store` |
| **A result contract** | Ch3 DF7 and 4.5.13. Columns, rows, warnings; `sourceKind`; `columnRoles` |
| **Remediation safety** | Ch4 5.6.4d nine checks; Ch3 4.5.12a `can_accept` seven conditions; 4.5.12b escalation |
| **Concurrency policy** | Ch4 5.3.2. Six pools, `compute_weight`, five-level ladder |

**Every row resolves to a chapter section. Nothing is left to invention.**

### 48.4 Stale contracts removed in Revision 6.1

Each was an active architecture section teaching something a later section cancelled. All are now deleted from the body. **They are recorded here as historical and withdrawn, and nowhere else.**

| # | Withdrawn contract | Replaced by |
|---|---|---|
| 1 | **ModelBundle** as the only publishable unit, with Bundle Promoter, candidate bundle, live bundle, bundle alias, bundle lineage and `bundle_version` | `ppiq_plant.model_registry`, activation per serving identity `(tenant_id, model_code, outcome_code, grain_code)` plus `model_version`. Section 17 |
| 2 | **Physically wide feature store**: `feature_matrix`, one physical column `f_<feature_code>` per feature, wide Parquet as the product contract | `ppiq_plant.feature_store` keyed `(material_unit_id, feature_set_version_id)` with `features jsonb`, `lineage_hash`, `is_dirty`, watermark refresh, immutable `feature_snapshots`. Section 5, DP-2 |
| 3 | **Remediation outcome mapping** that placed evidence-only on checks 1 to 5 and suppressed on checks 2, 3 and 4 | The canonical mapping, written into section 24.3 itself |
| 4 | **`can_accept` as a synonym for actionable** | The seven-condition server-side authority, section 24.4 |
| 5 | **Single-table remediation eligibility** | Template and per-prediction evaluation as two tables, section 24.1 |
| 6 | **Text and image encoders feeding MF-02 and MF-03** as embedding or feature inputs | Cite and link only. No statistic, score or value is computed from text. Sections 29 and 46 |
| 7 | **"No point estimate is ever emitted"** | Bounds mandatory when the basis is sufficient; a point estimate may sit beside them. Section 26 |
| 8 | **Three-pool taxonomy** analysis, training, serving, with invented admission dimensions as product contracts | The six canonical pools with `compute_weight` and the degradation ladder. Sections 38 and 47 |
| 9 | **Independent `RelationshipModelVersion`** with its own compatibility gate and lineage entry | `plant_relationships` publication pinned by `source_definition_id` and `source_definition_version`. Section 42 |
| 10 | **Invented block categories** input_scope, intelligence, governance, output, and the single-canvas framing | One shell, five purposes S1 to S5; toolbox Groups 1 to 6. Sections 36 and 43.2 |
| 11 | **Interim revision parts** carrying superseded status sections and duplicate section numbers 49 to 55 | Deleted. Part Six renumbered to sections 41 to 48 |
| 12 | **All statements that the chapters were unavailable**, and the interim `NOT FROZEN` and `PENDING-SOURCE` states | Section 41 records what was read |

### 48.5 Contradiction scan V3 result

Generated by `PPIQ_Layer_B_Pack_Contradiction_Scan.py` against **this exact file**. The scan splits at the start of section 48; everything before it is the active body. **It exits non-zero on any failure.**

V3 adds twelve negative and nineteen positive checks for the target architecture. **It failed four checks on first run** against the partially synchronised body: a stale single-predicate admission sentence in section 38.2, the absolute text boundary quoted in section 46, "seven model families" in the executive summary, and one positive check that had been written to assert the now-forbidden term. All four were corrected.

```
PPIQ LAYER B ARCHITECTURE PACK - CONTRADICTION SCAN V3
file   : PPIQ_Layer_B_Architecture_Design_Pack.md
bytes  : 239147
lines  : 3283
active : 1 - 3077 (section 48 onward is the historical ledger)

NEGATIVE CHECKS - stale contracts must be absent from the active body
CHECK                                 HITS  RESULT
--------------------------------------------------------------
ModelBundle                              0  PASS
bundle_version                           0  PASS
any use of 'bundle'                      0  PASS
Wide physical feature store              0  PASS
physically wide                          0  PASS
feature_matrix                           0  PASS
feature matrix                           0  PASS
DP-2a                                    0  PASS
DP-2b                                    0  PASS
f_<feature_code>                         0  PASS
Model Trainers (5 families)              0  PASS
five model families                      0  PASS
G-01..G-20                               0  PASS
twenty-two validation gates              0  PASS
supersedes (doc evolution)               0  PASS
Needs your ruling                        0  PASS
not until ruled                          0  PASS
OD-02 remains                            0  PASS
OD-13 open                               0  PASS
Scope authority OPEN                     0  PASS
CT-07 needs                              0  PASS
SM-15 as an object                       0  PASS
RelationshipModelVersion                 0  PASS
separate published version               0  PASS
single fact-row projection               0  PASS
invented block taxonomy                  0  PASS
three-pool taxonomy                      0  PASS
serving pool                             0  PASS
no point estimate rule                   0  PASS
text encoder into MF-02/MF-03            0  PASS
chapters unavailable                     0  PASS
NOT FROZEN status                        0  PASS
supersession notices                     0  PASS
ml parallelism = 1                       0  PASS
single-predicate admission               0  PASS
ml admits training and scoring           0  PASS
batch on online container                0  PASS
absolute text boundary                   0  PASS
manifest_hash as PK                      0  PASS
seven ML models                          0  PASS
seven model families                     0  PASS
PostgreSQL as training path              0  PASS
sequence values as PG array              0  PASS
G-01..G-46 stale                         0  PASS
forty-seven gates                        0  PASS

POSITIVE CHECKS - the canonical final state must be asserted
CHECK                                 HITS  RESULT
--------------------------------------------------------------
seven families, correct term             2  PASS
MF-01 to MF-07 recognised                5  PASS
OD-02 CLOSED                             1  PASS
OD-13 CLOSED                             2  PASS
CT-07 CLOSED                             3  PASS
seven output dataset families            3  PASS
canonical feature_store named            4  PASS
model_registry named                     2  PASS
source_definition_version pin           16  PASS
three ml lanes                           6  PASS
two-predicate admission                  5  PASS
max_concurrency separated                5  PASS
online reservation hard                  2  PASS
Semantic Contract Manifest               2  PASS
manifest_id is PK                        1  PASS
tenant-scoped manifest unique            1  PASS
manifest coverage rule                   2  PASS
columnar training artifact               2  PASS
materialiser exemption                   2  PASS
sequence split contract                  1  PASS
three-dimensional promotion              1  PASS
encoder promotion inequality             1  PASS
exact Flat recall baseline               3  PASS
deterministic tool planner               1  PASS
permission before ranking                1  PASS
governance-based boundary                3  PASS
intelligence and engine families         4  PASS
gate inventory G-01..G-55                6  PASS

STRUCTURAL CHECKS
--------------------------------------------------------------
duplicate model_version on a line        0  PASS
duplicate section numbers                0  PASS
non-ascii characters                     0  PASS
--------------------------------------------------------------
RESULT: SCAN V3 PASSED - FREEZE-SAFE
```

### 48.6 Sections rewritten in Revision 7

| Section | Was | Now |
|---|---|---|
| **SM-01** | `SemanticModelVersion` with a draft/validated/published lifecycle | **Semantic Contract Manifest**: immutable, content-addressed, `manifest_id` PK with UNIQUE `(tenant_id, manifest_hash)`, no lifecycle |
| **DP-2** | Snapshot pinned, physical read path unstated | Sealed **typed columnar artifact** is the training read path; `feature_store` is governance and current state; materialiser exemption stated |
| **DP-3** | `values float32[]` in PostgreSQL | **`sequence_manifests`** plus chunked typed arrays in object storage |
| **6** | Model family registry, five families implied | **Intelligence and engine family registry**, seven families with five sub-types and lane assignment |
| **MF-01** | Optional, no promotion rule | Optional, with the **encoder promotion inequality** |
| **MF-02** | FAISS initial, recall probe mentioned | **Policy selector plus permanent exact-Flat baseline**; recall floor blocks a build |
| **MF-04** | Validation on quality metrics | **Three-dimensional promotion gate**: quality, serving cost, training cost |
| **11.2a** | Tools with no surrounding runtime | The nine-step Assistant runtime, four properties bearing on the tool contract |
| **29, 46** | Absolute no-statistic-from-text rule | **Governance-based boundary**, two paths, resolving the 5.8.6 versus 5.8.7 contradiction |
| **38, 47** | Six pools, `ml` parallelism 1, single-predicate admission | **Three `ml` lanes**, two-predicate admission, hard-reserved online scoring, pre-emptible training |
| **40.1a** | - | **Gates G-48 to G-55** |
| **Components** | 20 components | 22: **Snapshot Materialiser** and **Manifest Resolver** added |

### 48.7 Sections rewritten in Revision 6.3
### 48.7 Sections rewritten in Revision 6.3

Not annotated. Rewritten, so the active text carries only the canonical answer.

| Section | Was | Now |
|---|---|---|
| **10** | Intelligence datasets project into the same fact contract | One result envelope `columns + rows + warnings`; source-declared row shapes; the two classes named |
| **12.3** | Projection into the existing aggregate contract | Execution and the two source classes, with `sourceKind`, `intelligenceSource`, `columnRoles` |
| **35.3** | `SM-15` as an independent relationship publication object with its own status enum | The canonical relationship authority: `plant_relationships`, `_members`, `_paths`, pinned by `source_definition_id` and `source_definition_version` |
| **36.3** | Four invented block categories | The six canonical toolbox groups of Ch4 5.2.5, with Group C discipline stated as never user-selectable |
| **36.2** | `block_category` enum with the invented values | `toolbox_group` 1 to 6 |
| **37.3** | JobRun field `input_scope` | `run_scope`, to remove collision with the withdrawn category name |
| **40** | Revision 3 gates plus a freeze checklist declaring the pack not frozen | Platform integration gates, plus the open items as measurement decisions each with a canonical field to be written into |
| **Part Three preamble** | Reconciliation status saying the chapters were unavailable | Removed |
| **19** | The acceptance question with a pending list | What Part One establishes, with storage settled and only measurement remaining |
| **18.1** | Tradeoff row 3, wide physical feature store versus canonical | Materialised feature store over per-run computation |
| **18.2** | Twelve open decisions requiring a ruling | All architectural decisions closed; OD-02, OD-13 and CT-07 stated as CLOSED with their content |
| **18.3** | Contradictions needing rulings | Rule reconciliation, every item closed |
| **31 row 49** | Scope authority OPEN | COVERED, Rule Appendix A |
| **32.1** | New open decisions | Decision state: measured parameters with canonical homes |
| **32.3** | The completion test re-answered | Decomposition readiness |
| **33** | What Part Three changes in Parts One and Two | **Deleted.** Document evolution belongs in section 48 |
| **34.5** | The return path and what it does to OD-02 | The return path |
| **C4, C5, W4** | Build and extend feature matrix, DP-2a, DP-2b | Publish the feature-set definition; materialise `feature_store`; incremental feature refresh |
| **Component 10** | Runs G-01..G-20 | Runs G-01 to G-55 |
| **Component 9** | Model Trainers, five families | Split into 9a learned-model trainers, 9b retrieval index builder, 9c statistical engines, 9d practice engine |
| **Header** | Part-by-part revision narrative with supersession | A flat reading contract: sections 1 to 47 read forward, none cancelled by a later one |

### 48.7 Status

```
AI/ML/LLM TARGET ARCHITECTURE OPTIMISED
MASTER DESIGN SYNCHRONISED
READY FOR IMPLEMENTATION DECOMPOSITION
```

**Chapter amendments are integrated into revised chapter bodies** delivered as `*_RevisionNext.md`. Convergence is proven by `PPIQ_Convergence_Scan_V4.py`, which scans all eleven documents and is fail-closed on a missing file.

**What frozen means here.** The architecture is bound to the governing chapters and no unresolved architecture invention remains. It does not mean the numbers are settled: five measurement decisions stay open by design, and each is a benchmark task rather than a design gap. It does not authorise implementation; that is a separate ruling.

**One thing worth carrying into decomposition.** The chapters are consistently stricter than this pack was. Leakage is a database CHECK rather than a test. The honesty machinery is outside the Supervisor's write scope by absent permission rather than by convention. A suggestion without resolvable evidence cannot be stored. **Where a canonical constraint and a pack gate cover the same property, the constraint is the mechanism and the gate is the proof that it holds.** Building the gate and skipping the constraint would be the wrong half.

---

*Revision 6.2, 11 August 2026. Chapters 1 to 6 read directly; sections used are recorded in 49.2. Twenty-six discrepancies found and resolved, thirteen of them new in this pass. Every invented persistence object bound or withdrawn. **Status: IMPLEMENTATION DESIGN FROZEN - READY FOR BACKLOG DECOMPOSITION.***
